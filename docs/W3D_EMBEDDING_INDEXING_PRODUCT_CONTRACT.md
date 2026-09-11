# Phase W3-D Product Contract: Local Vector Embedding & Hybrid Indexing Stage

**Phase**: `W3-D — Local Vector Embedding & Hybrid Indexing Stage`  
**Status**: `PLANNING REMEDIATED (PASS 2) / AWAITING THIRD INDEPENDENT PLANNING AUDIT`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `3b2b37b925cf972bddc655143669302386475c17`  
**Preceding Closed Stages**:
- `Phase W2-F — Universal Format Optimization & Quality Presets` (CLOSED — VERIFIED)
- `Phase W3-B — Scholar Domain Models & Local Persistence Layer` (CLOSED — VERIFIED)
- `Phase W3-C.1..C.5 — Document Extractors & Native Windows OCR` (CLOSED — VERIFIED)
- `Phase W3-C.6 — Source-Aware Paragraph Assembly & Text Normalization` (CLOSED — VERIFIED)
- `Phase W3-C.7.1 — Deterministic Passage Chunker` (CLOSED — VERIFIED)
- `Phase W3-C.7.2 — Orchestration & Persistence Integration` (CLOSED — VERIFIED)
- `Phase W3-C.7.3 — Bounded Context Window Formulation` (CLOSED — VERIFIED)
**Downstream Dependents**:
- `Phase W3-E — Context Assembly & Study Synthesis Engine`
- `DocumentChatService` (Offline RAG Assistant)
- `ScholarKitViewModel` (Study Concepts, Practice Quizzes, Executive Summaries)
- Universal Academic Library Search

---

## 1. Executive Summary & User Problem Justification

### 1.1 The User Problem
In preceding stages (W3-C.1 through W3-C.7.3), AXORA Desktop established an offline document ingestion pipeline:
1. Multi-format documents (PDF, DOCX, Markdown, PlainText, HTML, CSV/TSV, TIFF, Raster Images) are parsed into canonical structural text (`NormalizedText`).
2. Structural text is segmented into atomic passage chunks (`DocumentPassageChunk`) respecting sentence boundaries, Markdown blocks, and stride overlaps.
3. Atomic passages are expanded into deterministic, bounded context windows (`BoundedContextWindow`) with exact source coordinate provenance.

However, without a dedicated local vector embedding and indexing stage:
- **No Conceptual Discovery**: The user cannot search across their academic library conceptually. A query for *"fault-tolerant surface codes"* will fail to retrieve a passage discussing *"quantum error-correcting lattices"* unless exact keyword strings match.
- **RAG Latency & Scalability Bottlenecks**: Downstream synthesis features (such as `DocumentChatService`, study concept extraction, and quiz generation) would be forced to perform exhaustive $O(N)$ text scans across hundreds of pages in memory on every query, causing UI freezes and severe battery drain.
- **Lack of Incremental Ingestion**: Any addition, update, or deletion of a document would require reloading the entire library into memory, preventing smooth background indexing.

### 1.2 The W3-D Solution
Phase **W3-D** designs an offline-first, modular, and privacy-preserving **Local Vector Embedding & Hybrid Indexing Engine** (`IScholarIndexService`, `IEmbeddingEngine`).

W3-D:
- Ingests canonical single-document `BoundedContextWindow` instances produced by C7.3.
- Generates dense semantic vector embeddings using local hardware acceleration (DirectX 12 GPU via DirectML, with multithreaded CPU fallback).
- Performs deterministic Attention-Masked Mean Pooling and $L_2$ unit normalization.
- Maintains a compact, dual-file persistent index (`index_manifest.json` + `vectors.bin`) under `%APPDATA%\Axora\Scholar\indexes\`.
- Operates alongside a Core Local lexical index to enable **Hybrid Search** (combining dense vector cosine similarity with normalized BM25 keyword matching).
- Implements strict capability boundaries adhering to the **AXORA Modular Capability Contract**: heavy neural weights are optional (Class B) and never silently bundled into the base installer.

---

## 2. Capability Classification & Modularity Governance

In strict adherence to the [AXORA Modular Capability Contract](file:///d:/RAJ/GITHUB_REPOSITORY/PROJECTS/AXORA-DESKTOP/docs/AXORA_MODULAR_CAPABILITY_CONTRACT.md) and [AXORA Product Philosophy](file:///d:/RAJ/GITHUB_REPOSITORY/PROJECTS/AXORA-DESKTOP/docs/AXORA_PRODUCT_PHILOSOPHY.md), W3-D defines explicit capability classes:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          W3-D CAPABILITY CLASSIFICATION                     │
├─────────────────────────────────────────────────────────────────────────────┤
│  CLASS A: CORE LOCAL CAPABILITY (Always Available / Base Installer)         │
│  - Inverted Lexical Index & Deterministic Lexical Feature Projection.       │
│  - Footprint: 0 MB external download (bundled natively in app installer).   │
│  - Execution: 100% offline, zero network, zero external dependencies.       │
│  - Function: Guarantees keyword and deterministic lexical hashing search    │
│    works immediately upon install, even if the user never downloads models. │
│  - UI Badge: Ready (Lexical Only)                                           │
├─────────────────────────────────────────────────────────────────────────────┤
│  CLASS B: OPTIONAL LOCAL CAPABILITY (User-Controlled Download)              │
│  - ONNX Runtime Dense Vector Embedding Model (all-MiniLM-L6-v2, 384-dim).   │
│  - Footprint: ~80 MB download, ~120 MB unpacked in:                         │
│    %APPDATA%\Axora\Capabilities\Models\all-MiniLM-L6-v2\                  │
│    (Engineering estimates; subject to validation against packaging pipeline)│
│  - Execution: 100% local and offline once downloaded.                       │
│  - Activation: Requires explicit user consent via Extension/Download Mgr.   │
│  - UI Badge: Ready (DirectML GPU) or Ready (CPU)                            │
├─────────────────────────────────────────────────────────────────────────────┤
│  CLASS C: OPTIONAL NETWORK CAPABILITY (Explicit Opt-In Only)                │
│  - User-configured external embedding API (e.g., custom endpoint).         │
│  - Behavior: Strictly disabled by default; marked with distinct (🌐) badge; │
│    shows transparent text preview dialog; NEVER silently uploads data.      │
├─────────────────────────────────────────────────────────────────────────────┤
│  CLASS D: SYSTEM-PROVIDED ACCELERATION (Hardware / OS Infrastructure)       │
│  - DirectML DirectX 12 GPU hardware execution provider.                     │
│  - Zero download: Uses Windows 11 graphics driver runtime natively.         │
│  - NPU Qualification: NPU execution is conditional on compatible hardware,  │
│    silicon vendor MCDM drivers, and runtime provider support; classified as │
│    an optional/future capability, with DirectX 12 GPU as the primary target.│
└─────────────────────────────────────────────────────────────────────────────┘
```

### 2.1 The Lean Base Rule & Size Qualifications
The base AXORA installer has an architectural target budget of <150 MB (per AXORA Modular Capability Contract). Heavy ONNX models (Class B) **MUST NOT** be bundled in the installer.
The model download size (~80 MB) and unpacked footprint (~120 MB) are **current engineering estimates**, subject to formal measurement against the final distribution archive.
If a user runs AXORA on a clean machine without downloading optional capabilities:
- Class A (Lexical Index + Deterministic Lexical Feature Projection) takes over automatically.
- All search, document chat, and study workflows remain operational in lexical mode.
- The UI displays a non-intrusive, dismissible badge: `Optional Semantic Engine Available to Download (~80 MB)`.

---

## 3. Data Flow & Boundary Contracts

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                             W3-D DATA FLOW PIPELINE                         │
│                                                                             │
│   [ ScholarDocument ] (from ScholarLibraryService)                          │
│          │                                                                  │
│          ▼                                                                  │
│   [ BoundedContextWindow[] ] (from IBoundedContextWindowBuilder)            │
│          │                                                                  │
│          ├── FormattedText (Bounded context content, max 4,000 chars)       │
│          ├── WindowId (e.g., win_doc1_p1_f0_c0_2)                           │
│          └── Provenance Coordinates (DocumentId, PageNumber, ChunkIndex)    │
│          │                                                                  │
│          ▼                                                                  │
│   ┌──────────────────────────────────────────────────────────────────────┐  │
│   │                      IEmbeddingEngine Provider                       │  │
│   │                                                                      │  │
│   │  Tier 1: DirectML ONNX (all-MiniLM-L6-v2 · DirectX 12 GPU · Class B) │  │
│   │      └─▶ Fallback: CPU ONNX (Multithreaded · Class B)                │  │
│   │          └─▶ Fallback: Lexical Feature Vectorizer (Class A)          │  │
│   │                                                                      │  │
│   │  Mean Pooling over attention_mask ──▶ L2 Normalization (||v||_2=1.0) │  │
│   └──────────────────────────────────────────────────────────────────────┘  │
│          │                                                                  │
│          ▼ Normalized Vector: float[384], ||v||_2 = 1.0                     │
│   ┌──────────────────────────────────────────────────────────────────────┐  │
│   │                  ScholarVectorIndex Persistence Layer                │  │
│   │                                                                      │  │
│   │  %APPDATA%\Axora\Scholar\indexes\{DocumentId}\                       │  │
│   │    ├── index_manifest.json (Metadata, Model Fingerprint, Schema V1)  │  │
│   │    └── vectors.bin         (Flat IEEE 754 float32 array, SHA256 sum) │  │
│   └──────────────────────────────────────────────────────────────────────┘  │
│          │                                                                  │
│          ▼                                                                  │
│   [ IScholarIndexService ] ──▶ Downstream: DocumentChatService, W3-E RAG    │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 3.1 Input Data Contract (Consuming W3-C.7.3)
W3-D accepts as inputs:
1. `ScholarDocument`:
   - `DocumentId`: string (alphanumeric GUID or sanitized string).
   - `SourcePath`: string (source metadata only; ground truth source file is never modified or deleted).
   - `SourceHash`: string (SHA-256 hash of original document content).
   - `Pages`: `List<DocumentPage>` with `PageNumber`, `NormalizedText`, and `Chunks`.
2. `BoundedContextWindow`:
   - Must be a canonical single-document window (from `FormulatePageWindows` or single-document `FormulateFocalWindow`).
   - `WindowId`: Unique window identifier (e.g. `win_{docId}_p{page}_f{k}_c{min}_{max}`).
   - `FormattedText`: Canonical context string formatted with passage boundaries and citation tags.
   - `DocumentId`, `PageNumber`, `StartCharOffset`, `EndCharOffset`.
   - `FocalChunk`, `ConstituentChunks`, `Citations`.
3. `IndexOptions`:
   - `PreferredModelId`: string (default: `"all-MiniLM-L6-v2"`).
   - `BatchSize`: integer in $[1, 32]$ (default: `16`). Values $< 1$ or $> 32$ are strictly rejected with `ArgumentOutOfRangeException`.
   - `AllowDirectMl`: boolean (default: `true`).
   - `ForceRebuild`: boolean (default: `false`).

### 3.2 Authoritative Ingestion Boundary & Composite Window Exclusion (AUD2-W3D-02, `INV-W3D-27`)
`BoundedContextWindowBuilder.FormulateCompositeWindow` (C7.3 Mode C) is exclusively a **dynamic query-time context assembly tool** used downstream by `DocumentChatService` to package retrieved passages into prompt budgets.
- **Authoritative Rule**: `ScholarVectorIndex` indexes exclusively canonical single-document page windows formulated by `FormulatePageWindows`.
- **Comprehensive Rejection Discriminator**: If a caller passes a `BoundedContextWindow` where **ANY** of the following conditions is true:
  1. `window.DocumentId == "composite"`
  2. `window.PageNumber == 0`
  3. `window.WindowId.StartsWith("comp_", StringComparison.Ordinal)`
  4. `window.FocalChunk == null`
  the service **MUST REJECT** the window immediately by throwing an `ArgumentException` with diagnostic error code `ERR_COMPOSITE_WINDOW_NOT_INDEXABLE`.
- **Zero-Persistence Guarantee**: On rejection, zero files, records, or bytes are written to `%APPDATA%\Axora\Scholar\indexes\`.
- **Canonical Preservation**: Canonical single-document page windows (where `DocumentId != "composite"`, `PageNumber >= 1`, `WindowId.StartsWith("win_")`, and `FocalChunk != null`) remain unconditionally indexable.

### 3.3 Output Data Contract & BM25 Normalization (AUD2-W3D-03, `INV-W3D-30`)
W3-D produces:
1. `VectorIndexRecord`:
   - `WindowId`: string.
   - `DocumentId`: string.
   - `PageNumber`: integer.
   - `ContentHash`: string (cryptographic 256-bit SHA-256 digest of `FormattedText`).
   - `VectorOffset`: integer (absolute byte offset in `vectors.bin`, beginning at 64).
   - `VectorDimension`: integer (e.g. `384`).
   - `CharLength`: integer.
   - `EstimatedTokens`: integer.
2. `ScholarVectorIndex`:
   - In-memory search structure backing fast SIMD cosine similarity.
   - Dual-file persistent disk representation.
3. `VectorSearchResult`:
   - `WindowId`: string.
   - `DocumentId`: string.
   - `PageNumber`: integer.
   - `SimilarityScore`: float in $[-1.0f, 1.0f]$ (cosine similarity).
   - `Window`: `BoundedContextWindow?` (hydrated on demand).
   - `Citations`: `IReadOnlyList<StudyCitation>`.
4. `HybridSearchResult`:
   - Combines dense vector score $S_{\text{vector}} \in [0, 1]$ and normalized lexical BM25 score $S_{\text{lexical}} \in [0, 1]$:
     $$S_{\text{hybrid}} = \alpha \cdot S_{\text{vector}} + (1 - \alpha) \cdot S_{\text{lexical}}, \quad \alpha \in [0.0f, 1.0f], \text{ default } \alpha = 0.70f$$

#### Deterministic BM25 Lexical Score Normalization Algorithm:
Raw BM25 scores $b_i \ge 0$ are unbounded positive values. For a candidate pool of $K$ matching documents with raw scores $\{b_0, b_1, \dots, b_{K-1}\}$:
1. **Empty Pool**: If $K = 0$, return an empty result list `[]`.
2. **Single Candidate**: If $K = 1$, set $S_{\text{lexical}, 0} = 1.0f$ if $b_0 > 0$, else $0.0f$.
3. **Uniform Pool**: If $\max(b) - \min(b) < 10^{-7}$: set $S_{\text{lexical}, i} = 1.0f$ if $\max(b) > 0$, else $0.0f$ for all candidates.
4. **General Pool**:
   $$S_{\text{lexical}, i} = \text{Math.Clamp}\left(\frac{b_i - \min(b)}{\max(b) - \min(b)}, 0.0f, 1.0f\right)$$

#### Deterministic Final Ranking & Tie-Breaking Policy:
Results are sorted deterministically using the following multi-key comparator:
1. Primary: Descending $S_{\text{hybrid}}$
2. Secondary: Descending $S_{\text{vector}}$ (dense semantic priority)
3. Tertiary: Descending $S_{\text{lexical}}$
4. Tie-breaker 1: Ascending `DocumentId` (`StringComparer.Ordinal`)
5. Tie-breaker 2: Ascending `PageNumber`
6. Tie-breaker 3: Ascending `FocalChunkIndex`
7. Tie-breaker 4: Ascending `WindowId` (`StringComparer.Ordinal`)

#### Query Edge Cases:
- **Empty or Whitespace Query**: Returns `[]` immediately without throwing.
- **Zero Lexical Matches**: $S_{\text{lexical}} = 0.0f$; candidate ranking is determined by $S_{\text{hybrid}} = \alpha \cdot S_{\text{vector}}$.
- **Zero Vector Matches / Lexical-Only Mode**: $S_{\text{vector}} = 0.0f$; candidate ranking is determined by $S_{\text{hybrid}} = (1 - \alpha) \cdot S_{\text{lexical}}$.
- **Duplicate WindowIds in Ingestion**: Retains the record with higher similarity score.
- **NaN / Infinity Sanitation**: Any NaN or Infinite floating-point value is clamped/coerced to $0.0f$.

---

## 4. Storage & Persistence Boundary Contract

AXORA strictly separates **User Data Ground Truth** from **Derived Ephemeral Index Data** and **Capability Weights**:

```
%APPDATA%\Axora\
├── Scholar\
│   ├── documents\          [USER DATA GROUND TRUTH - NEVER WIPED]
│   │   └── {DocId}.json    [Persisted ScholarDocument]
│   ├── sessions\           [USER DATA GROUND TRUTH - NEVER WIPED]
│   │   └── {SessionId}.json[Study notes, chat sessions, quiz results]
│   ├── indexes\            [DERIVED INDEX DATA - SAFE TO WIPE / REBUILD]
│   │   └── {DocId}\
│   │       ├── index_manifest.json
│   │       └── vectors.bin
│   └── quarantine\         [CORRUPT DATA PRESERVATION]
│       └── indexes_{timestamp}\
└── Capabilities\           [OPTIONAL CAPABILITY ASSETS - SEPARATE NAMESPACE]
    └── Models\
        └── all-MiniLM-L6-v2\
            ├── model.onnx
            ├── vocab.txt
            └── config.json
```

### 4.1 Inviolable Storage Invariants & MemoryMappedFile Policy (AUD2-W3D-05, `INV-W3D-31`)
1. **Source Document Protection**: W3-D index operations **MUST NEVER** alter, overwrite, or delete user source documents on disk or JSON records in `%APPDATA%\Axora\Scholar\documents\`.
2. **Rebuild Safety**: Clearing or rebuilding an index simply removes files in `%APPDATA%\Axora\Scholar\indexes\{DocId}\` and regenerates them from the underlying `ScholarDocument`. Zero user ground truth is lost.
3. **Capability Independence**: Deleting, updating, or repairing an optional model in `%APPDATA%\Axora\Capabilities\` **MUST NEVER** delete user study data in `%APPDATA%\Axora\Scholar\`.
4. **Staged Replacement Semantics**: All index writes use temporary files (`.tmp`) followed by same-volume atomic replacement (`File.Move(..., overwrite: true)`). Interrupted saves leave existing valid indexes untouched.
5. **MemoryMappedFile / NTFS File-Lock Policy**:
   - Small to medium indices ($\le 5,000$ vectors, requiring $<8$ MB RAM) are loaded into owned in-memory byte buffers/arrays. This eliminates open file handles and prevents `IOException: Access Denied` on Windows NTFS during atomic replacement.
   - Indices $> 5,000$ vectors may utilize `MemoryMappedFile` as an engineering optimization.
   - All active mapped view accessors MUST be closed/disposed prior to `File.Move(..., overwrite: true)`. Writers must not attempt file replacement while active reader mappings remain open.

---

## 5. Identity, Versioning & Invalidation Contracts

### 5.1 Composite Index Identity
Index records rely on deterministic identity. To avoid identity collisions or desynchronization:
- **Composite Key**: `rec_{DocumentId}_{WindowId}_{ModelId}_{Dimension}`
- **Content Fingerprint**: 64-character lowercase hexadecimal SHA-256 digest of `FormattedText`.
- **Model Fingerprint**: SHA-256 digest of the ONNX model binary (or `"builtin-simd-v1"` for the heuristic model).

> [!IMPORTANT]
> **Cryptographic Terminology Standard**: In compliance with technical precision rules, AXORA documentation and code **MUST NOT** claim hashes are "mathematically collision-free" or "100% unique". Such claims must be accurately stated as: *"Cryptographic collision-resistant 256-bit SHA-256 digest with negligible collision probability ($< 2^{-128}$)."*

### 5.2 Deterministic Invalidation Matrix
An index is checked upon loading. If any invalidation trigger matches, the index state transitions accordingly:

| Trigger Condition | Evaluated Check | State Transition | Action Required |
| :--- | :--- | :--- | :--- |
| **Model Changed** | `manifest.ModelFingerprint != activeEngine.ModelFingerprint` | `Stale_ModelMismatch` | Prompt user / auto-reindex with active model |
| **Dimension Mismatch** | `manifest.VectorDimension != activeEngine.Dimension` | `Stale_DimensionMismatch` | Flag stale; trigger reindex |
| **Document Modified** | `manifest.SourceHash != document.SourceHash` | `Stale_DocumentModified` | Re-extract & incrementally re-embed |
| **Schema Mismatch** | `manifest.SchemaVersion > CurrentSchemaVersion` | `Unsupported_FutureSchema` | Read-only refusal; notify user to update app |
| **Binary Checksum Fail** | `ComputeSha256(vectors.bin) != manifest.BinaryPayloadHash` | `Corrupt_ChecksumMismatch` | Quarantine corrupt files; initiate clean rebuild |
| **Length Mismatch** | `FileInfo(vectors.bin).Length != RecordCount * Dimension * 4` | `Corrupt_TruncatedBinary` | Quarantine corrupt files; initiate clean rebuild |
| **Normal / Clean** | All checks pass | `Valid` | Direct memory mapping / query execution |

---

## 6. Token Pooling & Provider Numerical Equivalence Contract

### 6.1 Transformer Token Pooling Specification (AUD2-W3D-01, `INV-W3D-29`)
The primary semantic embedding model is `all-MiniLM-L6-v2`. Raw ONNX transformer graph execution produces unpooled token hidden states:
1. **Raw ONNX Tensor Shapes**:
   - Inputs: `input_ids` $[B, L]$, `attention_mask` $[B, L]$, `token_type_ids` $[B, L]$ (int64).
   - Output: `last_hidden_state` of shape $[B, L, 384]$ (float32), where $B \in [1, 32]$ is batch size and $L \le 512$ is sequence length.
2. **Integrated vs. Engine-Side Pooling**:
   - Packaging pipeline tests verify whether the distributed `model.onnx` artifact embeds an internal pooling node producing $[B, 384]$.
   - If unpooled (standard transformer export), `IEmbeddingEngine` implements **attention-mask-aware mean pooling**:
     $$\text{pooled}_{b, d} = \frac{\sum_{i=0}^{L-1} (\text{hidden}_{b, i, d} \times \text{attention\_mask}_{b, i})}{\max\left(\sum_{i=0}^{L-1} \text{attention\_mask}_{b, i}, 1\right)}$$
3. **Padding & Zero-Sequence Handling**:
   - Tokens where $\text{attention\_mask}_{b, i} = 0$ contribute $0$ to numerator and denominator.
   - If an input sequence is all-padding or empty ($\sum \text{mask} = 0$), the denominator evaluates to $\max(0, 1) = 1$, yielding a safe zero vector $\mathbf{0}_{384}$ and avoiding divide-by-zero.
4. **$L_2$ Unit Normalization Point**:
   - Applied immediately following mean pooling and prior to persistence or similarity search:
     $$\mathbf{v} = \frac{\text{pooled}}{\max(\|\text{pooled}\|_2, 10^{-12})}$$
   - Guarantee: $\|\mathbf{v}\|_2 = 1.0 \pm 10^{-5}$.
5. **Final Output Dimension**: Exactly 384 floating-point elements per input text.
6. **Execution Parity**: Both DirectML GPU and multithreaded CPU providers execute the identical pooling and normalization math.

### 6.2 Provider Numerical Equivalence vs. Bit-Identity (AUD1-W3D-02)
Due to differing floating-point accumulation orders across GPU warps (DirectML) versus CPU vector registers (AVX2/AVX-512 FMA), DirectML and CPU execution **WILL NOT produce bit-identical floating-point vectors**.
W3-D formally mandates **Numerical Equivalence within Tolerance**:
1. **Cosine Similarity Equivalence**:
   $$\text{CosineSimilarity}(\mathbf{v}_{\text{dml}}, \mathbf{v}_{\text{cpu}}) \ge 0.999$$
2. **Element-wise Deviation Tolerance**:
   $$\max_{i} |v_{\text{dml}, i} - v_{\text{cpu}, i}| \le 1.0 \times 10^{-3}$$
3. **Strict Normalization Compliance**: Both vectors must independently satisfy $\|\mathbf{v}\|_2 = 1.0 \pm 10^{-5}$.
4. **Retrieval Semantic Equivalence**: Cross-provider queries (e.g. querying a DirectML-indexed document using CPU) produce equivalent top-$K$ semantic rankings.

---

## 7. Privacy & Data Boundary Contract

1. **Zero Silent Network Activity**:
   - Class A (Core Local) and Class B (Optional Local) execute strictly on the local CPU or DirectX 12 GPU.
   - Under no circumstances will text, embeddings, queries, or document hashes be transmitted across a network socket during local indexing or search.
2. **Zero Text in Diagnostics**:
   - Diagnostic log messages and telemetry structures must never log document text, window excerpts, or query strings.
   - Logs may only record: `DocumentId`, `WindowId`, duration, record counts, vector dimensions, device provider (`DirectML`, `CPU`, `Heuristic`), and standardized error codes (`ERR_MODEL_MISSING`, `ERR_DIRECTML_DEVICE_LOSS`, etc.).
3. **Optional Network Boundary (Class C)**:
   - If an optional cloud embedding endpoint is configured, the UI must display a mandatory confirmation dialog displaying the endpoint URL, model name, and exact character payload count before any network transmission occurs.

---

## 8. Graceful Degradation & Capability States

The UI and application lifecycle must remain responsive regardless of embedding engine state:

| Engine State | Search Capabilities | UI Badge | User Impact |
| :--- | :--- | :--- | :--- |
| **DirectML Active (Class B+D)** | Full Semantic + Lexical Hybrid Search (Hardware accelerated GPU) | `Ready (DirectML GPU)` | Maximum retrieval speed and learned semantic matching. |
| **CPU Fallback (Class B)** | Full Semantic + Lexical Hybrid Search (Multithreaded CPU) | `Ready (CPU)` | Learned semantic matching; slightly higher CPU utilization. |
| **Model Missing (Class A)** | BM25 Lexical + Deterministic Feature Projection (Class A) | `Ready (Lexical Only)` | Keyword retrieval; banner offers optional ~80 MB download for full semantic capabilities. |
| **Device Loss / TDR** | Seamless fallback from DirectML to CPU or Lexical Projector | `Device Reset (CPU Fallback)` | Zero crash; active indexing job resumes on CPU immediately. |
| **Corrupt Index** | On-the-fly lexical search over in-memory document text | `Reindexing Required` | Notification displayed; auto-quarantine triggered; user clicks "Reindex". |

---

## 9. Non-Functional & Bounded Resource Limits

Verification of these limits is governed by the two-tier verification suite: **Tier 1 (Unconditional Acceptance Suite)** and **Tier 2 (Environment-Dependent Integration Suite)**.


To ensure system stability, W3-D establishes concrete resource boundaries:

| Resource Metric | Bounded Limit | Classification & Role |
| :--- | :--- | :--- |
| **Max Vector Dimension** | `1024` | Engineering guardrail: prevents unbounded memory allocation across model generations. |
| **Batch Size Range** | `[1, 32]` windows | Engineering guardrail: rejects $<1$ or $>32$; bounds VRAM footprint ($<150$ MB VRAM target). |
| **Max Context Window Length** | `4,000` UTF-16 chars | Contractual invariant: enforced by C7.3 `MaxWindowChars` to respect model context ceilings. |
| **Vector Normalization** | $L_2$ norm ($\sum v_i^2 = 1.0 \pm 10^{-5}$) | Mathematical invariant: guarantees cosine similarity simplifies to SIMD dot product $\mathbf{u} \cdot \mathbf{v}$. |
| **File Read Mode** | MemoryMappedFile for libraries $> 5,000$ vectors | Engineering policy: avoids heap allocation for large libraries while ensuring in-memory buffering for small libraries. |
| **Cancellation Responsiveness** | Checked at every batch boundary ($\le 32$ windows) | Engineering target: guarantees responsive cancellation in $<100$ ms on reference hardware under normal execution. |

---

## 10. Downstream Integration & Handoff

W3-D completes the foundation required for:
1. **`DocumentChatService` (Offline RAG Assistant)**:
   - Replaces existing in-memory heuristic chat indexing with `IScholarIndexService.SearchHybridAsync(query, topK: 5)`.
   - Passes retrieved canonical `BoundedContextWindow` objects into C7.3 `FormulateCompositeWindow` for grounded prompt assembly.
2. **`ScholarKitViewModel` (Study Artifact Generation)**:
   - Grounded generation of Executive Summaries, Study Concepts, and Practice Quiz Questions using top semantically ranked windows.
3. **Cross-Document Library Explorer**:
   - Multi-document semantic filtering and passage clustering.

Phase W3-D product contract remediation pass 2 is complete.
