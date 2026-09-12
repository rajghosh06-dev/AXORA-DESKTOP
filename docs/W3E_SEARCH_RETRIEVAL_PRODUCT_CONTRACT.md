# Phase W3-E Product Contract: Search / Retrieval Integration Stage

**Phase**: `W3-E — Search / Retrieval Integration Stage`  
**Status**: `PLANNING REVISED (PASS 2) / AWAITING IMPLEMENTATION AUTHORIZATION`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `96d8531560419eb9e9351575b3231aa27ab4a590` (`feat(winui): complete W3-D vector embedding and hybrid indexing`)  
**Preceding Closed Stages**:
- `Phase W2-F — Universal Format Optimization & Quality Presets` (CLOSED — VERIFIED)
- `Phase W3-B — Scholar Domain Models & Local Persistence Layer` (CLOSED — VERIFIED)
- `Phase W3-C.1..C.5 — Document Extractors & Native Windows OCR` (CLOSED — VERIFIED)
- `Phase W3-C.6 — Source-Aware Paragraph Assembly & Text Normalization` (CLOSED — VERIFIED)
- `Phase W3-C.7.1 — Deterministic Passage Chunker` (CLOSED — VERIFIED)
- `Phase W3-C.7.2 — Orchestration & Persistence Integration` (CLOSED — VERIFIED)
- `Phase W3-C.7.3 — Bounded Context Window Formulation` (CLOSED — VERIFIED)
- `Phase W3-D — Local Vector Embedding & Hybrid Indexing Stage` (CLOSED — VERIFIED)
**Downstream Dependents**:
- `DocumentChatService` (Offline RAG Assistant & Grounded Synthesis)
- `ScholarKitViewModel` (Study Session Search, Concepts, Practice Quizzes, Executive Summaries)
- Universal Academic Library Search UI

---

## 1. Executive Summary & User Problem Justification

### 1.1 The User Problem
In Phase **W3-D**, AXORA Desktop established the underlying vector embedding and single-document indexing infrastructure (`IScholarIndexService`). W3-D proved:
1. Deterministic generation of 384-dimensional unit-normalized dense vectors using ONNX Runtime (DirectML GPU with CPU fallback and Class-A lexical projection).
2. Staged same-volume dual-file persistence (`index_manifest.json` + `vectors.bin`) under `%APPDATA%\Axora\Scholar\indexes\{DocumentId}\`.
3. Single-document hybrid retrieval combining dense vector cosine similarity with Min-Max normalized lexical BM25 scores and a deterministic 7-level tie-breaker.
4. Automatic corruption detection and directory quarantine.

However, W3-D remains a low-level single-document indexing subsystem. Real-world academic workflows cannot be powered by raw document-level index readers alone because:
- **Multi-Document Session Scope**: Academic research spans multi-document study sessions (`StudySession.DocumentIds`). Users do not query one isolated document in a silo; they query a corpus of textbooks, lecture notes, and papers simultaneously.
- **Leaky Storage Abstractions**: ViewModels and UI surfaces (`ScholarKitViewModel`, search bars, assistant chat) must not be tightly coupled to binary file streams, memory-mapped buffers, inverted index term frequencies, or vector dimensionality.
- **Unformatted & Ungrounded Snippets**: Downstream consumers need formatted, human-readable snippets with keyword match highlights and guaranteed provenance links (`StudyCitation`) directly connected to the underlying source pages.
- **Fragile Partial Index Failure**: In a multi-document search across five documents, if one document has a missing, stale, or corrupted index, the retrieval process must not fail catastrophically; it must return valid results from intact documents with clear, observable degradation feedback.
- **Query Hygiene**: User queries can be empty, whitespace, punctuated, or excessively long. A hardened retrieval engine must sanitize, clamp, and validate queries deterministically.

### 1.2 The W3-E Solution
Phase **W3-E** designs an offline-first, production-oriented **Local Scholar Search & Retrieval Engine** (`IScholarSearchService`).

W3-E:
- Exposes a clean, decoupled search contract (`ScholarSearchRequest`, `ScholarSearchResponse`, `ScholarSearchResultItem`) tailored for ViewModels and synthesis engines.
- Orchestrates **Multi-Document Query Fan-Out** across study sessions, whole libraries, or single documents with bounded concurrency.
- Implements **Global Cross-Document Candidate Pooling & Lexical Normalization** ensuring fair, mathematically consistent hybrid fusion across multi-document sets while guaranteeing deterministic ranking parity with W3-D on single-document queries.
- Guarantees **Strict Citation Grounding**: every search result item is immutably anchored to authoritative local source citations (`StudyCitation`); zero hallucinated provenance.
- Provides **Resilient Partial Degradation Handling**: skips corrupted/quarantined indexes gracefully, reports structured warnings, and serves results from valid documents without throwing unhandled exceptions.
- Enforces **Explicit Lexical-Only Degradation**: when the neural model is uninstalled or hardware fails, effective alpha is strictly forced to $0.0f$, vector similarity is set to $0.0f$, and responses are explicitly labeled as lexical-only.
- Enforces **Local-First Privacy & Zero Network Egress** for Class A/B local retrieval, requiring explicit modal preview (`RemoteTransmissionGuard`) for any Class C opt-in.

---

## 2. Capability Classification & Modularity Governance

In strict compliance with the [AXORA Modular Capability Contract](file:///d:/RAJ/GITHUB_REPOSITORY/PROJECTS/AXORA-DESKTOP/docs/AXORA_MODULAR_CAPABILITY_CONTRACT.md) and [AXORA Product Philosophy](file:///d:/RAJ/GITHUB_REPOSITORY/PROJECTS/AXORA-DESKTOP/docs/AXORA_PRODUCT_PHILOSOPHY.md), W3-E establishes four explicit capability tiers:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          W3-E CAPABILITY CLASSIFICATION                     │
├─────────────────────────────────────────────────────────────────────────────┤
│  CLASS A: CORE LOCAL CAPABILITY (Always Available / Base Installer)         │
│  - Inverted Lexical BM25 Search across multi-document and session scopes.   │
│  - Footprint: 0 MB external download (bundled natively in app installer).   │
│  - Execution: 100% offline, zero network, zero external dependencies.       │
│  - Degradation Mode: When neural models are uninstalled, search executes in │
│    lexical-only mode (effective alpha = 0.0f) with complete BM25 scoring.   │
│  - UI Badge: Ready (Lexical Only)                                           │
├─────────────────────────────────────────────────────────────────────────────┤
│  CLASS B: OPTIONAL LOCAL CAPABILITY (User-Controlled Download)              │
│  - Dense Semantic Vector Retrieval using local ONNX model (all-MiniLM-L6-v2)│
│  - Execution: 100% local and offline once downloaded via Download Manager.  │
│  - Hardware: DirectML DirectX 12 GPU acceleration or multithreaded CPU.     │
│  - UI Badge: Ready (DirectML GPU) or Ready (CPU)                            │
├─────────────────────────────────────────────────────────────────────────────┤
│  CLASS C: OPTIONAL NETWORK CAPABILITY (Explicit Opt-In Only)                │
│  - User-configured remote retrieval endpoint.                               │
│  - Behavior: Strictly disabled by default; marked with distinct (🌐) badge; │
│    requires explicit confirmation via RemoteTransmissionGuard modal preview;│
│    NEVER silently uploads text, queries, or embeddings.                     │
├─────────────────────────────────────────────────────────────────────────────┤
│  CLASS D: SYSTEM-PROVIDED ACCELERATION (Hardware / OS Infrastructure)       │
│  - SIMD AVX2/FMA vector dot-product acceleration.                           │
│  - DirectML DirectX 12 GPU hardware execution provider.                     │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Data Contracts & Service Boundaries

### 3.1 Primary Service Contract (`IScholarSearchService`)
```csharp
namespace Axora.Desktop.Services.Contracts;

public interface IScholarSearchService
{
    /// <summary>
    /// Executes a fully-configured search request across the requested document scope,
    /// applying hybrid scoring, location filtering, score thresholds, and tie-breaking.
    /// </summary>
    Task<ScholarSearchResponse> SearchAsync(
        ScholarSearchRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Convenience method for targeted single-document queries.
    /// Reuses the authoritative W3-D scoring path for deterministic parity.
    /// </summary>
    Task<ScholarSearchResponse> SearchDocumentAsync(
        string documentId,
        string queryText,
        int topK = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Convenience method for querying all documents enrolled in an active study session.
    /// </summary>
    Task<ScholarSearchResponse> SearchSessionAsync(
        string sessionId,
        string queryText,
        int topK = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates current search capability and active hardware/provider status.
    /// </summary>
    Task<SearchCapabilityStatus> GetCapabilityStatusAsync(
        CancellationToken ct = default);
}
```

### 3.2 Request Model (`ScholarSearchRequest`)
```csharp
public sealed class ScholarSearchRequest
{
    /// <summary>
    /// Raw query text input by user or synthesis engine. Must be non-empty and <= 2000 chars.
    /// </summary>
    public string QueryText { get; set; } = string.Empty;

    /// <summary>
    /// Document scope governing which documents to search.
    /// </summary>
    public SearchScope Scope { get; set; } = SearchScope.All();

    /// <summary>
    /// Maximum number of ranked results to return. Bounded in [1, 100]. Default is 10.
    /// </summary>
    public int TopK { get; set; } = 10;

    /// <summary>
    /// Optional page/chunk location filter. When set, only windows matching the filter are considered.
    /// Evaluated prior to candidate scoring.
    /// </summary>
    public LocationFilter? LocationScope { get; set; }

    /// <summary>
    /// Minimum combined score threshold in [0.0, 1.0]. Hits below this threshold are omitted.
    /// </summary>
    public float MinScoreThreshold { get; set; } = 0.0f;

    /// <summary>
    /// Convex combination weighting factor alpha in [0.0, 1.0] (default 0.70f).
    /// CombinedScore = effectiveAlpha * VectorSimilarity + (1 - effectiveAlpha) * NormalizedBM25.
    /// Note: If dense model is unavailable, effective alpha is strictly forced to 0.0f.
    /// </summary>
    public float HybridAlpha { get; set; } = 0.70f;

    /// <summary>
    /// When true, includes the underlying BoundedContextWindow instances on result items.
    /// </summary>
    public bool IncludeHydratedWindows { get; set; } = false;
}
```

### 3.3 Scope & Location Filters
```csharp
public enum SearchScopeKind
{
    AllDocuments = 0,
    SessionDocuments = 1,
    SingleDocument = 2,
    ExplicitDocuments = 3
}

public sealed class SearchScope
{
    public SearchScopeKind Kind { get; private set; }
    public string? TargetId { get; private set; }
    public IReadOnlyList<string> DocumentIds { get; private set; } = [];

    public static SearchScope All() => new() { Kind = SearchScopeKind.AllDocuments };
    public static SearchScope Session(string sessionId) => new() { Kind = SearchScopeKind.SessionDocuments, TargetId = sessionId };
    public static SearchScope Single(string documentId) => new() { Kind = SearchScopeKind.SingleDocument, TargetId = documentId, DocumentIds = [documentId] };
    public static SearchScope Explicit(IEnumerable<string> documentIds) => new() { Kind = SearchScopeKind.ExplicitDocuments, DocumentIds = documentIds.Distinct(StringComparer.Ordinal).ToList() };
}

public sealed class LocationFilter
{
    public int? StartPage { get; set; }
    public int? EndPage { get; set; }
    public IReadOnlyList<int>? SpecificPages { get; set; }

    public bool Matches(int pageNumber)
    {
        if (SpecificPages != null && SpecificPages.Count > 0 && !SpecificPages.Contains(pageNumber))
            return false;
        if (StartPage.HasValue && pageNumber < StartPage.Value)
            return false;
        if (EndPage.HasValue && pageNumber > EndPage.Value)
            return false;
        return true;
    }
}
```

### 3.4 Response & Result Item Models
```csharp
public enum SearchDegradationStatus
{
    FullHybrid = 0,
    LexicalOnly_ModelMissing = 1,
    LexicalOnly_HardwareFallback = 2,
    PartialResults_CorruptedIndexSkipped = 3,
    PartialResults_MissingIndexSkipped = 4,
    ZeroResults = 5,
    NoIndexedDocuments = 6
}

public sealed class SearchWarning
{
    public string DocumentId { get; set; } = string.Empty;
    public string WarningCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class ScholarSearchResponse
{
    public static readonly ScholarSearchResponse Empty = new()
    {
        Items = [],
        TotalCandidatesEvaluated = 0,
        Elapsed = TimeSpan.Zero,
        DegradationStatus = SearchDegradationStatus.ZeroResults
    };

    public IReadOnlyList<ScholarSearchResultItem> Items { get; set; } = [];
    public int TotalCandidatesEvaluated { get; set; }
    public TimeSpan Elapsed { get; set; }
    public SearchDegradationStatus DegradationStatus { get; set; } = SearchDegradationStatus.FullHybrid;
    public IReadOnlyList<SearchWarning> Warnings { get; set; } = [];
}

public sealed class ScholarSearchResultItem
{
    public int Rank { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string WindowId { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public int FocalChunkIndex { get; set; }
    public IReadOnlyList<int> ConstituentChunkIndices { get; set; } = [];

    public float CombinedScore { get; set; }
    public float VectorSimilarity { get; set; }
    public float LexicalScore { get; set; }

    public string FormattedSnippet { get; set; } = string.Empty;
    public IReadOnlyList<StudyCitation> Citations { get; set; } = [];
    public SourceAvailabilityStatus SourceStatus { get; set; } = SourceAvailabilityStatus.None;
    public BoundedContextWindow? HydratedWindow { get; set; }
}
```

---

## 4. Single-Document Parity Contract

To avoid unsubstantiated floating-point equality claims across distinct execution paths, single-document parity is formally defined as:
1. **Authoritative Path Reuse**: Single-document queries (`SearchDocumentAsync` or `SearchScope.Single(docId)`) reuse the exact underlying W3-D scoring implementation (`IScholarIndexService.SearchHybridAsync`) or its exact candidate formulation logic directly.
2. **Deterministic Ranking Parity**: For any query $Q$, alpha $lpha$, and top-$K$, W3-E and W3-D produce identical ranking sequences of `WindowId` items.
3. **Regression Test Fixtures**: Verification relies on explicit regression fixtures executing both W3-E and W3-D against identical test document indexes and asserting invariant ranking order and score parity within standard IEEE 754 float tolerance ($\Delta \le 10^{-6}$).
4. **No Unsupported Absolute Claims**: Claims of "bit-for-bit" identical floats are prohibited; the contract guarantees identical ranking order, identical candidate pool selection, and equivalent mathematical scoring within precision limits.

---

## 5. Lexical-Only Degradation Semantics

When dense neural vector scoring is unavailable (Class A mode, uninstalled model weights, or DirectML/CPU runtime failure):
1. **Effective Alpha Forcing**: The effective combination weight $lpha_{	ext{eff}}$ MUST be forced to `0.0f`, regardless of the user's requested `HybridAlpha`.
2. **Degradation State Exposure**: `ScholarSearchResponse.DegradationStatus` MUST be set to `LexicalOnly_ModelMissing` or `LexicalOnly_HardwareFallback`. It is strictly forbidden for a response to report `FullHybrid` when dense retrieval was inactive.
3. **Vector Similarity Zeroing**: For every returned `ScholarSearchResultItem`, `VectorSimilarity` MUST be set to `0.0f` (signaling zero dense semantic contribution).
4. **Combined Score Identity**: The combined score becomes strictly equal to the normalized lexical score:
   $$S = 0.0f \cdot 0.0f + (1.0f - 0.0f) \cdot s_{	ext{lex}} = s_{	ext{lex}}$$
5. **No Hybrid Facade**: UI badges must clearly display `Lexical Search Only`; no result item may imply neural semantic matching occurred.

---

## 6. Candidate Pool Formation & Merging Semantics

To eliminate ambiguity regarding the "$\le 500$ candidates per document" constraint, the per-document candidate pool is formed via a deterministic 5-step procedure:

1. **Step 1: Early Location Filtering**:
   - For all $R$ records in `index.Manifest.Records`, evaluate `LocationFilter.Matches(record.PageNumber)`. Records failing the filter are excluded immediately from all subsequent scoring.
2. **Step 2: Dense Candidate Selection (Top 250)**:
   - For all passing records, evaluate dense cosine similarity $s_{	ext{vec}}$.
   - Filter to records with $s_{	ext{vec}} > 0.0f$.
   - Sort descending by $s_{	ext{vec}}$, with ties broken by `RecordIndex` ascending.
   - Select up to the top 250 dense candidates: $\mathcal{C}_{	ext{dense}}$.
3. **Step 3: Lexical Candidate Selection (Top 250)**:
   - For all passing records, evaluate raw BM25 score $s_{	ext{bm25}}$ using `index.LexicalInvertedIndex`.
   - Filter to records with $s_{	ext{bm25}} > 0.0f$.
   - Sort descending by $s_{	ext{bm25}}$, with ties broken by `RecordIndex` ascending.
   - Select up to the top 250 lexical candidates: $\mathcal{C}_{	ext{lex}}$.
4. **Step 4: Deduplicated Union & Capacity Fill**:
   - Merge $\mathcal{C}_{	ext{dense}}$ and $\mathcal{C}_{	ext{lex}}$ into a deduplicated set $\mathcal{C}_{	ext{doc}}$ keyed by `RecordIndex` (or `WindowId`). The initial union size is bounded in $[0, 500]$.
   - If $|\mathcal{C}_{	ext{doc}}| < 500$ and additional qualifying records with $s_{	ext{vec}} > 0 \lor s_{	ext{bm25}} > 0$ exist that fell outside the top-250 cuts, backfill remaining capacity up to 500 total candidates by ordering remaining candidates by $\max(s_{	ext{vec}}, 	ext{doc\_raw\_bm25})$ descending, then `RecordIndex` ascending.
5. **Step 5: Global Normalization Input**:
   - The union of the bounded per-document candidate pools across all documents in the query scope forms the global candidate pool $\mathcal{C} = igcup_{d} \mathcal{C}_{	ext{doc}, d}$ passed into global Min-Max normalization.

---

## 7. Citation Grounding & Provenance Guarantees

1. **Authentic Provenance Anchor**: Every `ScholarSearchResultItem` encapsulates direct citation provenance anchored to local source pages. Citations originate exclusively from the authoritative `StudyCitation` instances constructed in W3-C.7.3 and persisted into `VectorIndexRecord.Citations` in W3-D.
2. **Zero Hallucination Guarantee**: Synthetic, generative, or ungrounded citations are an absolute violation. No citation may have an empty `DocumentId` or `PageNumber <= 0`.
3. **Missing Disk File Behavior**: If a source document file is moved or deleted from disk after indexing:
   - The indexed text in `VectorIndexRecord.FormattedText` remains fully queryable.
   - `ScholarDocument.CheckSourceAvailability()` flags `SourceStatus = SourceAvailabilityStatus.Missing`.
   - Result items are returned cleanly without throwing exceptions, allowing the UI to display: `Indexed (Source Missing on Disk)`.

---

## 8. Measurable Performance Verification Framework

Performance compliance is verified via explicit, reproducible benchmarks:

### 8.1 Retrieval Conditions:
- **Cold Retrieval**: First query invocation following application launch or index cache eviction. Includes disk file reading/memory mapping and inverted index hydration.
- **Warm Retrieval**: Subsequent query invocations where `ScholarVectorIndex` is memory-resident and runtime buffers are allocated.
- **Model Initialization Boundary**: One-time ONNX Runtime session creation and DirectML device compilation (500ms–2000ms) is **EXCLUDED** from query latency and measured separately as `ModelInitializationLatency`.
- **Snippet & Metadata Boundary**: In-memory snippet extraction and fast disk file existence checks are **INCLUDED** in query latency.

### 8.2 Standard Benchmark Fixtures:
- **Small Fixture**: 1 document, 100 context windows (~100 vectors).
- **Medium Fixture (Multi-Doc Session)**: 5 documents, 2,000 context windows (~2,000 vectors).
- **Stress Fixture**: 10 documents, 10,000 context windows (~10,000 vectors).

### 8.3 Measurement Methodology:
- Execute $N=20$ benchmark query iterations per fixture. Discard iteration 1 for warm benchmarks.
- Record elapsed durations in milliseconds using `Stopwatch.GetTimestamp()`.
- Calculate and report both $p50$ (median) and $p95$ (95th percentile).
- Record environment metadata: CPU architecture, core count, available RAM, OS build, and active execution provider (DirectML GPU or CPU).

### 8.4 Quantitative Latency Budgets (Warm Retrieval, p95):
- Single-Document Hybrid (Small Fixture, 100 vectors): $\le 30	ext{ ms}$.
- Single-Document Hybrid (Medium Fixture, 2,000 vectors): $\le 75	ext{ ms}$.
- Multi-Document Session Hybrid (Medium Fixture, 5 docs, 2,000 vectors): $\le 150	ext{ ms}$.
- Multi-Document Lexical-Only (Medium Fixture, 5 docs): $\le 60	ext{ ms}$.

---

## 9. Privacy & Offline Boundaries

1. **Zero Network Sockets (Class A & B)**: Local search operations MUST NOT create sockets or make HTTP/TCP calls. 100% offline local execution.
2. **Diagnostic Log Sanitization**: Log messages MUST NOT record user queries, passage snippets, extracted text, or student notes to disk log files. Only operation names, elapsed ms, candidate counts, and error codes are logged.
3. **Class C Remote Guard**: If remote retrieval is requested, `RemoteTransmissionGuard.ValidateTransmission()` enforces an explicit modal user confirmation dialog displaying destination endpoint, character count, and snippet preview.
