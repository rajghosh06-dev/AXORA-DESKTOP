# Phase W3-D Technical Architecture: Local Vector Embedding & Hybrid Indexing Stage

**Phase**: `W3-D — Local Vector Embedding & Hybrid Indexing Stage`  
**Status**: `PLANNING REMEDIATED (PASS 2) / AWAITING THIRD INDEPENDENT PLANNING AUDIT`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `3b2b37b925cf972bddc655143669302386475c17`  
**Preceding Closed Stages**: `W2-F`, `W3-B`, `W3-C.1..C.7.3`  
**Downstream Dependents**: `Phase W3-E`, `DocumentChatService`, `ScholarKitViewModel`

---

## 1. Architectural Overview & System Topology

Phase **W3-D** establishes the persistent indexing and retrieval engine of the AXORA Scholar platform. It consumes canonical `BoundedContextWindow` objects produced by Phase W3-C.7.3, projects their textual content into dense vector representations, stores them in an optimized dual-file format, and exposes semantic and hybrid retrieval services.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                 AXORA SCHOLAR PLATFORM                                 │
├────────────────────────────────────────────────────────────────────────────────────────┤
│  Ingestion Pipeline (Closed Upstream)                                                  │
│  [ Raw Document ] ──▶ [ NormalizedText ] ──▶ [ Chunks ] ──▶ [ BoundedContextWindows ]  │
│                                                                     │                  │
│  Phase W3-D: Indexing & Retrieval Subsystem                         │                  │
│  ┌──────────────────────────────────────────────────────────────────┴───────────────┐  │
│  │                              IScholarIndexService                                │  │
│  │                                                                                  │  │
│  │   ┌───────────────────────────┐           ┌──────────────────────────────────┐   │  │
│  │   │     IEmbeddingEngine      │           │      IInvertedLexicalIndex       │   │  │
│  │   │                           │           │                                  │   │  │
│  │   │  [Tier 1: DirectML GPU]   │           │  - BM25 Token Frequency Index    │   │  │
│  │   │  [Tier 2: Multithread CPU]│           │  - Inverted Posting Lists        │   │  │
│  │   │  [Tier 3: Lexical Project]│           │  - Exact Keyword Match / Stemming│   │  │
│  │   └─────────────┬─────────────┘           └─────────────────┬────────────────┘   │  │
│  │                 │ Dense float[384]                          │ Lexical Score      │  │
│  │                 ▼                                           ▼                    │  │
│  │   ┌──────────────────────────────────────────────────────────────────────────┐   │  │
│  │   │                       ScholarVectorIndex (Dual-File)                     │   │  │
│  │   │                                                                          │   │  │
│  │   │  %APPDATA%\Axora\Scholar\indexes\{DocumentId}\                              │   │  │
│  │   │    ├── index_manifest.json  (JSON schema v1, window metadata, hashes)   │   │  │
│  │   │    └── vectors.bin          (64-byte header + contiguous IEEE 754 float) │   │  │
│  │   └──────────────────────────────────────────────────────────────────────────┘   │  │
│  │                                         │                                        │  │
│  │                                         ▼                                        │  │
│  │   ┌──────────────────────────────────────────────────────────────────────────┐   │  │
│  │   │                        Hybrid Retrieval Evaluator                        │   │  │
│  │   │                                                                          │   │  │
│  │   │  SIMD Dot-Product Cosine Similarity + Normalized BM25 Convex Fusion:     │   │  │
│  │   │  S_hybrid = alpha * S_vector + (1 - alpha) * S_lexical                   │   │  │
│  │   │  Deterministic multi-key tie-breaking (`INV-W3D-30`)                                    │   │  │
│  │   └──────────────────────────────────────────────────────────────────────────┘   │  │
│  └─────────────────────────────────────────┬────────────────────────────────────────┘  │
│                                            │                                           │
│  Downstream Cognitive Consumers            ▼                                           │
│  - DocumentChatService (Offline RAG Context Windows via C7.3 Mode C Composite)         │
│  - ScholarKitViewModel (Grounded Study Artifacts: Concepts, Quizzes, Summaries)        │
│  - Library Search (Fast Cross-Document Semantic & Lexical Discovery)                  │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Domain Models & Service Contracts

### 2.1 Service Contracts

```csharp
namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Authoritative service interface for document vector indexing, incremental maintenance,
/// and hybrid semantic-lexical retrieval across the Scholar library.
/// </summary>
public interface IScholarIndexService
{
    /// <summary>
    /// Incrementally indexes or re-indexes canonical single-document context windows.
    /// Skips windows whose cryptographic content hash matches the existing index.
    /// Throws ArgumentException (ERR_COMPOSITE_WINDOW_NOT_INDEXABLE) if composite windows are passed.
    /// </summary>
    Task<IndexOperationResult> IndexDocumentAsync(
        ScholarDocument document,
        IReadOnlyList<BoundedContextWindow> windows,
        IndexOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Performs semantic vector search retrieving top-K relevant context windows matching the query.
    /// </summary>
    Task<IReadOnlyList<VectorSearchResult>> SearchVectorAsync(
        string query,
        string? documentIdFilter = null,
        int topK = 5,
        float minSimilarity = 0.25f,
        CancellationToken ct = default);

    /// <summary>
    /// Performs hybrid search combining dense vector cosine similarity with normalized BM25 lexical matching.
    /// </summary>
    Task<IReadOnlyList<HybridSearchResult>> SearchHybridAsync(
        string query,
        string? documentIdFilter = null,
        int topK = 5,
        float alpha = 0.70f,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates the real-time health, staleness, and validity of a document index.
    /// </summary>
    Task<IndexValidationReport> ValidateIndexAsync(
        string documentId,
        CancellationToken ct = default);

    /// <summary>
    /// Completely rebuilds the index for a document or the entire library.
    /// </summary>
    Task<IndexOperationResult> RebuildIndexAsync(
        string? documentId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes derived index files for a deleted document without touching source documents.
    /// </summary>
    Task<bool> DeleteIndexAsync(
        string documentId,
        CancellationToken ct = default);
}

/// <summary>
/// Abstraction for the local embedding generator.
/// Encapsulates model execution, hardware provider selection, Attention-Masked Mean Pooling,
/// and SIMD L2 normalization.
/// </summary>
public interface IEmbeddingEngine : IDisposable
{
    string ModelId { get; }
    string ModelFingerprint { get; }
    int Dimension { get; }
    EmbeddingExecutionProvider ActiveProvider { get; }

    /// <summary>
    /// Generates an L2-normalized float embedding vector (dimension 384) for a single text input.
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken ct = default);

    /// <summary>
    /// Generates L2-normalized float embedding vectors for a batch of text inputs.
    /// Batch size MUST be bounded in [1, 32]. Rejects < 1 or > 32.
    /// </summary>
    Task<IReadOnlyList<float[]>> GenerateBatchEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default);
}

public interface IEmbeddingCapabilityStateProvider
{
    Task<EmbeddingCapabilityState> GetCapabilityStateAsync();
    string GetRecommendedModelPath();
    bool IsHardwareAccelerationSupported();
}
```

### 2.2 Core Data Records & Enums

```csharp
namespace Axora.Desktop.Models;

public enum EmbeddingExecutionProvider
{
    DirectMlGpu,
    CpuMultithreaded,
    DeterministicLexicalProjection,
    OptionalRemoteApi
}

public enum IndexValidationStatus
{
    Valid,
    Stale_ModelMismatch,
    Stale_DimensionMismatch,
    Stale_DocumentModified,
    Corrupt_ChecksumMismatch,
    Corrupt_TruncatedBinary,
    Unsupported_FutureSchema,
    Missing
}

public sealed class IndexOptions
{
    public string PreferredModelId { get; set; } = "all-MiniLM-L6-v2";
    
    private int _batchSize = 16;
    public int BatchSize
    {
        get => _batchSize;
        set
        {
            if (value < 1 || value > 32)
                throw new ArgumentOutOfRangeException(nameof(value), "BatchSize must be strictly between 1 and 32.");
            _batchSize = value;
        }
    }

    public bool AllowDirectMl { get; set; } = true;
    public bool ForceRebuild { get; set; }
}

public sealed class IndexManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string DocumentId { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public string EmbeddingModelId { get; set; } = string.Empty;
    public string ModelFingerprint { get; set; } = string.Empty;
    public int VectorDimension { get; set; } = 384;
    public int TotalRecords { get; set; }
    public long TotalTokensEstimate { get; set; }
    public string BinaryPayloadHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<VectorIndexRecord> Records { get; set; } = [];
}

public sealed class VectorIndexRecord
{
    public string WindowId { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public int FocalChunkIndex { get; set; } = -1;
    public string ContentHash { get; set; } = string.Empty; // SHA-256 of FormattedText
    public int VectorOffset { get; set; }                  // Absolute byte offset in vectors.bin (>= 64)
    public int CharLength { get; set; }
    public int EstimatedTokens { get; set; }
    public List<int> ConstituentChunkIndices { get; set; } = [];
}

public sealed record VectorSearchResult(
    string WindowId,
    string DocumentId,
    int PageNumber,
    float SimilarityScore,
    string Snippet,
    IReadOnlyList<StudyCitation> Citations,
    BoundedContextWindow? HydratedWindow = null);

public sealed record HybridSearchResult(
    string WindowId,
    string DocumentId,
    int PageNumber,
    float CombinedScore,
    float VectorSimilarity,
    float LexicalScore,
    string Snippet,
    IReadOnlyList<StudyCitation> Citations,
    BoundedContextWindow? HydratedWindow = null);
```

---

## 3. Dual-File Storage Format Specification

Index storage is designed for fast sequential reads, zero garbage collection overhead during search, and robust corruption resistance.

```
%APPDATA%\Axora\Scholar\indexes\{DocumentId}\
├── index_manifest.json  (JSON UTF-8, human-readable metadata & records)
└── vectors.bin          (Flat binary IEEE 754 float32 vectors with 64-byte header)
```

### 3.1 Binary File Layout (`vectors.bin`)

```
┌────────────────────────────────────────────────────────────────────────┐
│                   VECTORS.BIN FILE LAYOUT (64-Byte Header)             │
├───────────────────┬──────────────┬──────────────┬──────────────────────┤
│ Field             │ Offset       │ Size (Bytes) │ Description          │
├───────────────────┼──────────────┼──────────────┼──────────────────────┤
│ Magic Bytes       │ 0x00 .. 0x07 │ 8            │ ASCII "AXORAVEC"     │
│ Format Version    │ 0x08 .. 0x09 │ 2 (uint16)   │ Version = 1 (LE)     │
│ Vector Dimension  │ 0x0A .. 0x0B │ 2 (uint16)   │ e.g., 384 (LE)       │
│ Record Count      │ 0x0C .. 0x0F │ 4 (uint32)   │ Number of vectors (LE│
│ Reserved Padding  │ 0x10 .. 0x1F │ 16           │ Zeroed for expansion │
│ Payload SHA-256   │ 0x20 .. 0x3F │ 32 (bytes)   │ SHA-256 of payload   │
├───────────────────┴──────────────┴──────────────┴──────────────────────┤
│                   VECTORS.BIN PAYLOAD (N * Dimension * 4 Bytes)        │
├───────────────────┬──────────────┬──────────────┬──────────────────────┤
│ Vector 0          │ 0x40 (64) .. │ Dim * 4      │ IEEE 754 Float32[]   │
│ Vector 1          │ 0x40 + D*4.. │ Dim * 4      │ IEEE 754 Float32[]   │
│ ...               │ ...          │ ...          │ ...                  │
│ Vector N-1        │ End - D*4 .. │ Dim * 4      │ IEEE 754 Float32[]   │
└────────────────────────────────────────────────────────────────────────┘
```

#### Total File Size Invariant:
$$\text{ExpectedFileSize} = 64 + (N \times D \times 4) \text{ bytes}$$

### 3.2 MemoryMappedFile & NTFS Replacement Lifecycle (AUD2-W3D-05, `INV-W3D-31`)
On Windows NTFS, active memory-mapped views lock the file handle, causing `File.Move(tmp, target, overwrite: true)` to fail with `IOException: Access Denied`.
To eliminate locking collisions:
1. **In-Memory Buffering for Small to Medium Indices**:
   - For document libraries $\le 5,000$ vectors ($5,000 \times 384 \times 4 = 7.68$ MB RAM), `vectors.bin` payload is loaded directly into an owned in-memory byte buffer / `float[]` array.
   - The file stream is closed immediately after loading, holding zero active OS locks.
2. **Mapped View Disposal for Large Indices**:
   - If `MemoryMappedFile` is used for indices $> 5,000$ vectors, reader accessors are reference-counted.
   - Any rebuild, compaction, or replacement operation must coordinate with readers: all open `MemoryMappedViewAccessor` instances are disposed before initiating `File.Move`.
   - Post-replacement, readers re-open the file, validating the 64-byte header and SHA-256 checksum.

---

## 4. Hardware Acceleration, Token Pooling & Provider Equivalence Contract

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     EMBEDDING ENGINE EXECUTION TIERS                    │
├─────────────────────────────────────────────────────────────────────────┤
│  Tier 1: DirectML (GPU Acceleration · Class B+D)                        │
│  - Targets DirectX 12 feature level 11_0+ via Microsoft.ML.OnnxRuntime. │
│  - SessionOptions: AppendExecutionProvider_DML(deviceId: 0).            │
│  - Memory limit: Bounded batches in [1, 32] (<150 MB VRAM target).      │
│  - Primary supported hardware target on Windows 11 PCs.                 │
│  - NPU note: NPU execution is optional/future capability conditional on │
│    silicon vendor MCDM drivers; DirectX 12 GPU is the authoritative path│
├─────────────────────────────────────────────────────────────────────────┤
│  Tier 2: Multithreaded CPU ONNX (Class B Fallback)                      │
│  - Used if DirectX 12 device is unavailable or returns hardware error. │
│  - SessionOptions: ExecutionMode.ORT_PARALLEL, GraphOptimizationLevel.  │
│  - Throughput: SIMD AVX2/AVX-512 optimized ONNX kernels.                │
├─────────────────────────────────────────────────────────────────────────┤
│  Tier 3: Deterministic Lexical Feature Projection (Class A Fallback)    │
│  - Used if ONNX neural model is NOT downloaded / installed.             │
│  - Token-frequency feature hashing projected onto 384-dim space.        │
│  - SIMD hardware normalized via SimdVectorHelper.Magnitude().           │
│  - Zero download required; 100% offline out-of-the-box.                 │
│  - Note: Provides lexical term matching; NOT learned neural semantics.  │
└─────────────────────────────────────────────────────────────────────────┘
```

### 4.1 Transformer Token Pooling Specification (AUD2-W3D-01, `INV-W3D-29`)
Raw ONNX execution of `all-MiniLM-L6-v2` produces a token tensor of shape $[B, L, 384]$. To produce canonical 384-dimensional sentence vectors:
1. **Attention-Masked Mean Pooling Implementation**:
   ```csharp
   // Inputs:
   // hiddenStates: float[batchSize, seqLen, 384]
   // attentionMask: long[batchSize, seqLen] (1 for real token, 0 for padding)

   float[] pooled = new float[384];
   float maskSum = 0.0f;

   for (int i = 0; i < seqLen; i++)
   {
       float maskWeight = (float)attentionMask[batchIdx, i];
       if (maskWeight <= 0f) continue;

       maskSum += maskWeight;
       for (int d = 0; d < 384; d++)
       {
           pooled[d] += hiddenStates[batchIdx, i, d] * maskWeight;
       }
   }

   float divisor = Math.Max(maskSum, 1.0f);
   for (int d = 0; d < 384; d++)
   {
       pooled[d] /= divisor;
   }

   // L2 Normalization immediately following pooling
   float norm = SimdVectorHelper.Magnitude(pooled);
   float invNorm = norm > 1e-12f ? 1.0f / norm : 0.0f;
   for (int d = 0; d < 384; d++)
   {
       pooled[d] *= invNorm;
   }
   ```
2. **Dimension Transformation**:
   - Before pooling: $[B, L, 384]$
   - After pooling and normalization: $[B, 384]$, satisfying $\|\mathbf{v}\|_2 = 1.0 \pm 10^{-5}$.
3. **Empty/Padding Inputs**: If $\sum \text{mask} = 0$, denominator is $1.0$, producing a safe zero vector before normalization, avoiding division by zero.

### 4.2 Numerical Equivalence Specification (AUD1-W3D-02)
Due to differing floating-point accumulation orders across GPU warps (DirectML) versus CPU vector registers (AVX2/AVX-512 FMA), DirectML and CPU execution **WILL NOT produce bit-identical floating-point vectors**.
W3-D formally defines **Numerical Equivalence within Tolerance**:
1. **Cosine Similarity Equivalence**:
   $$\text{CosineSimilarity}(\mathbf{v}_{\text{dml}}, \mathbf{v}_{\text{cpu}}) \ge 0.999$$
2. **Element-wise Deviation Tolerance**:
   $$\max_{i} |v_{\text{dml}, i} - v_{\text{cpu}, i}| \le 1.0 \times 10^{-3}$$
3. **Strict Normalization Compliance**: Both vectors must independently satisfy $\|\mathbf{v}\|_2 = 1.0 \pm 10^{-5}$.
4. **Cross-Provider Retrieval Compatibility**: A document index generated via DirectML is fully queryable via CPU execution without requiring reindexing.

### 4.3 Device Loss & TDR Recovery Decision Tree
On Windows, GPU driver resets (Timeout Detection and Recovery — TDR) can invalidate DirectML sessions:
1. When `InferenceSession.Run()` throws `OnnxRuntimeException` with `DXGI_ERROR_DEVICE_REMOVED` or `DXGI_ERROR_DEVICE_RESET`:
   - Log non-sensitive warning: `ERR_DIRECTML_DEVICE_LOSS`.
   - Dispose active DirectML session safely.
   - Reinitialize session under **Tier 2 (CPU)**.
   - Retry the active batch on CPU immediately.
   - Application never crashes; user experiences zero dropped jobs.

---

## 5. Ingestion Boundary & Composite Window Exclusion (AUD2-W3D-02, `INV-W3D-27`)

### 5.1 Single-Document Indexing Invariant & Guard Logic
- **Target Ingestion Units**: `ScholarVectorIndex` indexes exclusively canonical single-document page windows formulated by `IBoundedContextWindowBuilder.FormulatePageWindows`.
- **Composite Window Rejection**: C7.3 `FormulateCompositeWindow` (Mode C) produces dynamic multi-passage prompt assemblies. Upstream C7.3 may retain `DocumentId = document.DocumentId` when chunks originate from a single document. Therefore, checking `DocumentId == "composite"` alone is insufficient.
- **Authoritative Discriminator Implementation**:
  ```csharp
  if (window.DocumentId == "composite" ||
      window.PageNumber == 0 ||
      window.WindowId.StartsWith("comp_", StringComparison.Ordinal) ||
      window.FocalChunk == null)
  {
      throw new ArgumentException(
          $"Cannot index composite prompt window '{window.WindowId}'. Only canonical single-document page windows formulated by FormulatePageWindows are indexable.",
          nameof(windows));
  }
  ```
  Failure code `ERR_COMPOSITE_WINDOW_NOT_INDEXABLE` is reported, and zero bytes are persisted.

---

## 6. Incremental Indexing & Invalidation Logic

```
For each Document in Library:
  1. Load existing index_manifest.json (if present).
  2. Validate IndexStatus (ModelId, Fingerprint, SourceHash, Binary Integrity).
  3. If Status == Corrupt or Stale_ModelMismatch:
       Full rebuild required.
  4. If Status == Valid or Stale_DocumentModified:
       For each Window W_i formulated by C7.3:
         Compute ContentHash = SHA256(W_i.FormattedText)
         If record exists in manifest with identical ContentHash:
           Reuse existing vector at VectorOffset (SKIP EMBEDDING GENERATION).
         Else:
           Add W_i to BatchToEmbed.
       Execute Batch Embedding (batchSize in [1, 32]) only for new/modified windows.
       Compact/rewrite vectors.bin and update manifest atomically.
```

---

## 7. Downstream Integration Hand-off

```
┌─────────────────────────────┐        ┌──────────────────────────────┐
│    IScholarIndexService     │        │ IBoundedContextWindowBuilder │
└──────────────┬──────────────┘        └──────────────┬───────────────┘
               │ Top-5 Search Hits                    │
               ▼                                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│                 DocumentChatService (Offline RAG)                   │
│                                                                     │
│  1. Ingests user prompt.                                            │
│  2. Calls IScholarIndexService.SearchHybridAsync(prompt, topK: 5).  │
│  3. Extracts ConstituentChunks from retrieved BoundedContextWindows.│
│  4. Calls IBoundedContextWindowBuilder.FormulateCompositeWindow()   │
│     to format grounded prompt context with exact citations.         │
│  5. Passes composite context to local SLM for final synthesis.      │
└─────────────────────────────────────────────────────────────────────┘
```

Phase W3-D technical architecture remediation pass 2 is complete.
