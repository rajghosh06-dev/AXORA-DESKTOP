# Phase W3-C.7 Stage C7.3 Product Contract: Bounded Context Window Formulation

**Phase**: `W3-C.7 — Passage Chunking & Bounded Context Window Formulation`  
**Stage**: `C7.3 — Bounded Context Window Formulation`  
**Status**: `PLANNING REMEDIATED (PASS 3) / AWAITING THIRD INDEPENDENT PLANNING AUDIT`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  
**Preceding Stages**: 
- `Stage C7.1 — Core Passage Chunker` (CLOSED — PASS UNCONDITIONAL)
- `Stage C7.2 — DI & Orchestrator Integration` (CLOSED — PASS UNCONDITIONAL)  
- `Stage C7.3 Planning Remediation Pass 1 & 2` (COMPLETED)
- `Stage C7.3 Planning Closure Audit Pass 1 & 2` (FAIL - REMEDIATED)
**Downstream Dependents**: 
- `Phase W3-D — Local Vector Embedding & Hybrid Search`
- `Phase W3-E — Context Assembly & Synthesis`
- `DocumentChatService` (Offline RAG Assistant — migration deferred to W3-D/E)
- `ScholarKitViewModel` (Study Concepts, Quiz Questions, Executive Summary)

---

## 1. Purpose & Scope

Phase **W3-C.7** is formally titled **"Passage Chunking & Bounded Context Window Formulation"**.

Stages **C7.1** and **C7.2** established the first half of this mandate:
- **C7.1**: Implemented the core deterministic `PassageChunker` engine, which segments canonical `NormalizedText` into discrete atomic passages (`DocumentPassageChunk`) respecting structural Markdown blocks, sentence boundaries, academic abbreviations, and configured stride overlaps.
- **C7.2**: Integrated `IPassageChunker` into the dependency injection container (`App.xaml.cs`) and the document ingestion pipeline (`ScholarExtractionOrchestrator`), attaching generated passages directly to `DocumentPage.Chunks` and persisting them via `ScholarLibraryService`.

Stage **C7.3** fulfills the second half of the Phase W3-C.7 mandate: **Bounded Context Window Formulation**.

An atomic passage chunk (budgeted at $350 \dots 600$ UTF-16 code units, $\approx 80 \dots 150$ tokens) is optimized for granular keyword hit highlighting and dense vector indexing. However, downstream cognitive operations—including dense vector embedding generation, query-driven multi-passage prompt assembly, offline RAG synthesis, and study artifact extraction—require a **Bounded Context Window**: a coherent, expanded, and deduplicated span of context that surrounds focal passages without exceeding hard token/character ceilings or corrupting exact source provenance.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           Phase W3-C.7 Pipeline                         │
│                                                                         │
│  [ NormalizedText ] (Page Level)                                        │
│          │                                                              │
│          ▼ (Stages C7.1 & C7.2: Passage Chunking)                       │
│  [ DocumentPassageChunk[] ] (Atomic Passages: 350-600 chars, Stride 60) │
│          │                                                              │
│          ▼ (Stage C7.3: Bounded Context Window Formulation)             │
│  [ BoundedContextWindow ]                                               │
│    ├── Mode A: Page-Local Focal Expansion (Neighbors with Stride Dedup) │
│    ├── Mode B: Page-Local Fallback (Missing NormalizedText)             │
│    └── Mode C: Composite Prompt Assembly (Multi-Passage Budget-Capped)  │
│          │                                                              │
│          ▼                                                              │
│  Downstream Consumers (W3-D Embeddings, W3-E RAG, DocumentChatService)  │
└─────────────────────────────────────────────────────────────────────────┘
```

### 1.1 In-Scope Capabilities for Stage C7.3
1. **Domain Abstraction & Interface**:
   - Define `BoundedContextWindow` domain model representing an assembled, bounded, and traceable context window.
   - Define `ContextWindowOptions` record governing expansion radius, budget caps, overlap deduplication, ordering mode, and provenance formatting.
   - Define `CompositeOrderingMode` enum (`DocumentReadingOrder`, `PreserveInputOrder`).
   - Define `IBoundedContextWindowBuilder` interface and concrete `BoundedContextWindowBuilder` service.
2. **Page-Local Focal Window Expansion (Mode A)**:
   - Given a focal `DocumentPassageChunk` and its parent `DocumentPage`, expand context to adjacent preceding ($k - N_p$) and succeeding ($k + N_s$) neighbor chunks.
   - Strict page boundary containment: focal expansion never crosses physical page boundaries into adjacent pages.
3. **Seamless Overlap Deduplication**:
   - Adjacent chunks produced by `PassageChunker` share a 60-character stride overlap ($O > 0$). Simple string concatenation results in duplicate text stutter.
   - Stage C7.3 solves this mathematically: by computing the continuous coordinate span $[C_{k_{\min}}.\operatorname{StartCharOffset}, C_{k_{\max}}.\operatorname{EndCharOffset})$ in `NormalizedText`, the window extracts a continuous, non-duplicating substring that is 100% faithful to the source document.
4. **Fallback Handling (Mode B)**:
   - In degraded scenarios where `page.NormalizedText` is null, concatenate constituent chunk texts with delimiter whitespace, trimming duplicate prefixes, setting `StartCharOffset = -1` and `EndCharOffset = -1`.
5. **Composite Multi-Passage Prompt Window Assembly (Mode C)**:
   - Assemble multiple retrieved passages (potentially spanning multiple pages and documents, e.g., top-K search results) into a structured prompt context.
   - Enforce a strict global budget ceiling (`CompositeBudgetChars`, default: 3,000 chars).
   - Attach explicit page-level and passage-level citation headers (`--- [Page P, Passage C] ---`).
   - Support both `DocumentReadingOrder` and `PreserveInputOrder` (relevance ranking).
   - Multi-document deduplication key: `(DocumentId, PageNumber, ChunkIndex)`.
6. **Deterministic Window Identification**:
   - Deterministic `WindowId` derived from stable coordinate identity. Zero random GUID generation.
7. **Thread Safety, Statelessness & Local Synchronous Execution**:
   - 100% managed .NET 9 BCL code, zero external dependencies, thread-safe, synchronous in-memory execution without async overhead or `CancellationToken` baggage. Nominal engineering target of sub-millisecond return for typical passage sizes (not an SLA and not validated until benchmarked).

### 1.2 Explicitly Out-of-Scope Capabilities for Stage C7.3
- **NO Vector Embedding Generation**: C7.3 does not generate dense float vectors or invoke DirectML/ONNX. That is strictly Phase W3-D.
- **NO LLM / SLM Execution**: C7.3 does not execute local or remote language models. That is strictly Phase W3-E.
- **NO Similarity Scoring / Vector Search**: C7.3 does not compute cosine similarity or rank search results. It formats windows for given chunks.
- **NO Re-Chunking**: C7.3 consumes already-emitted `DocumentPassageChunk` instances. It does not re-slice or mutate `DocumentPassageChunk` fields.
- **NO Mutation of Ground Truth**: `RawText` and `NormalizedText` remain 100% immutable.
- **NO Mutation of DocumentChatService.cs**: `DocumentChatService.cs` is NOT modified in Stage C7.3. Downstream migration belongs strictly to Phase W3-D/W3-E. Compatibility will be verified via standalone simulation tests in `Axora.Desktop.Tests`.
- **NO Schema-Breaking Persistence Changes**: Context windows are ephemeral/on-demand constructs for runtime consumers. They are NOT written into `ScholarDocument.json` on disk, preserving schema version 1.

---

## 2. Core Architectural Invariants

### Invariant 1: Immutability of Document Ground Truth
Under no circumstances may `IBoundedContextWindowBuilder` overwrite, mutate, or alter `DocumentPage.RawText` or `DocumentPage.NormalizedText`.

### Invariant 2: Tri-Modal Provenance Model
Provenance semantics are formally differentiated into three explicit modes:

#### Mode A: Page-Local Focal Window (with `NormalizedText`)
- `PageNumber`: Physical page number ($1 \dots P$).
- `StartCharOffset`: Exact 0-based start index in `NormalizedText` ($\ge 0$).
- `EndCharOffset`: Exact 0-based exclusive end index in `NormalizedText` ($> \text{StartCharOffset}$).
- `CharLength`: `EndCharOffset - StartCharOffset == FormattedText.Length`.
- **Exact Substring Provenance Equality HOLDS 100% UNCONDITIONALLY**:
  $$P.\operatorname{NormalizedText}.\operatorname{Substring}(W.\operatorname{StartCharOffset}, W.\operatorname{CharLength}) == W.\operatorname{FormattedText}$$
- `DeduplicateOverlaps`: True (continuous coordinate span eliminates duplicate text).

#### Mode B: Page-Local Focal Fallback Window (Missing `NormalizedText`)
- Occurs when `page.NormalizedText == null`.
- `PageNumber`: Physical page number ($1 \dots P$).
- `StartCharOffset`: `-1` (explicitly unmapped).
- `EndCharOffset`: `-1` (explicitly unmapped).
- `CharLength`: `FormattedText.Length`.
- **Exact Substring Provenance Equality**: **WAIVED / NOT APPLICABLE**.
- `FormattedText`: Assembled from constituent chunk texts with delimiter whitespace.

#### Mode C: Multi-Page Composite Window
- Represents a prompt assembly across multiple passages or pages.
- `PageNumber`: `0` (indicates cross-page / document-global composite).
- `StartCharOffset`: `-1` (explicitly unmapped; single-integer offset is invalid across multiple pages).
- `EndCharOffset`: `-1` (explicitly unmapped).
- `CharLength`: `FormattedText.Length`.
- **Exact Substring Provenance Equality for FormattedText**: **WAIVED FOR COMPOSITE STRING**. Because `FormattedText` incorporates synthetic citation banners (`\n\n--- [Page P, Passage C] ---\n`), it is not a direct substring of any single page.
- **Constituent Provenance Integrity**: Every constituent passage retains its exact page and passage provenance within `ConstituentChunks` and `Citations`.

### Invariant 3: Overlap Deduplication Guarantee (Mode A)
When expanding a focal window across adjacent chunks that share a stride overlap ($O > 0$), the resulting continuous span in `NormalizedText` traverses the overlap region exactly once:
$$\text{CountOccurrences}(W.\operatorname{FormattedText}, S_{\text{overlap}}) == 1$$
(assuming $S_{\text{overlap}}$ occurs once in that span of `NormalizedText`). Concatenation stutter is strictly eliminated.

### Invariant 4: Strict Budget Enclosure (Zero Budget Overflow)
For any formulated context window $W$ governed by `options.MaxWindowChars` (focal) or `options.CompositeBudgetChars` (composite):
- Focal Window: $W.\operatorname{CharLength} \le \text{options.MaxWindowChars}$
- Composite Window: $W.\operatorname{CharLength} \le \text{options.CompositeBudgetChars}$
This invariant holds unconditionally. No pathological text, neighbor expansion, or header formatting may cause the emitted text length to exceed the configured maximum budget.

### Invariant 5: Page-Bounded Containment for Focal Windows
Academic citations require exact physical page attribution. Focal window expansion around a passage on page $P$ **MUST NEVER** cross page boundaries into page $P-1$ or page $P+1$. If neighbor expansion reaches the boundary of page $P$, it clamps gracefully at chunk index 0 or chunk index $M-1$.

### Invariant 6: Lossless Attribution for Composite Multi-Page Windows
When assembling composite windows across multiple pages, every constituent passage $C$ within the window retains its explicit attribution:
- `DocumentId`
- `PageNumber`
- `ChunkIndex`
- Citation badge (e.g., `[Page P, Passage C]`)

### Invariant 7: Pure Local & Synchronous Execution
Context window formulation is a 100% synchronous, CPU-bound, in-memory string and coordinate slicing operation with a nominal engineering target of sub-millisecond return for typical passage sizes (not an SLA and not validated until benchmarked). It requires zero network sockets, zero cloud dependencies, zero external process executions, and zero thread-context switching. `CancellationToken` is intentionally excluded from the public contract.

### Invariant 8: Deterministic Window Identification & Collision Resistance
Every `BoundedContextWindow` receives a deterministic identifier formulated from stable document and chunk coordinates. Zero runtime randomness (`Guid.NewGuid()`) is used:
- **Mode A (Focal Window)**:
  `$"win_{documentId}_p{pageNumber}_f{focalChunkIndex}_c{minChunk}_{maxChunk}"`
- **Mode B (Fallback Window)**:
  `$"win_{documentId}_p{pageNumber}_f{focalChunkIndex}_c{minChunk}_{maxChunk}_fb"`
  *Identity Scoping Note*: The focal WindowId represents a **spatial coordinate identity** indicating the contiguous constituent chunk range $[k_{\min}, k_{\max}]$ around focal chunk $k$ on page $P$ within a consistent options/configuration context. It identifies the spatial selection of chunks; callers must not assume that different options configurations producing differing formatted texts for the same chunk range will emit distinct spatial coordinate IDs.
- **Mode C (Composite Window)**:
  Formulated from a deterministic canonical coordinate string over the complete ordered sequence of constituents:
  $$S_{\text{coords}} = \text{string.Join}(";", \text{Constituents}.\text{Select}(c \Rightarrow \$"\{c.\text{DocumentId}\}:p\{c.\text{PageNumber}\}:c\{c.\text{ChunkIndex}\}"))$$
  $$H_{64} = \text{Convert.ToHexString}(\text{SHA256.HashData}(\text{Encoding.UTF8.GetBytes}(S_{\text{coords}}))).\text{ToLowerInvariant}()$$
  $$\text{WindowId} = \$\text{"comp\_"}\{H_{64}\} \quad (\text{or } \$\text{"comp\_empty"} \text{ if empty})$$
  This guarantees bit-for-bit repeatability across repeated runs and is cryptographically collision-resistant via full 256-bit SHA-256 digest across distinct chunk selections.

---

## 3. The Bounded Context Window Model & Options

### 3.1 Domain Model: `BoundedContextWindow`
```csharp
namespace Axora.Desktop.Models;

/// <summary>
/// Represents a bounded, semantically coherent, and traceable context window
/// assembled from one or more DocumentPassageChunk instances.
/// Used by downstream consumers (embeddings, RAG synthesis, chat assistant).
/// </summary>
public sealed class BoundedContextWindow
{
    /// <summary>
    /// Deterministic identifier for this context window formulation (e.g. win_doc1_p1_c0_2).
    /// </summary>
    public string WindowId { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the parent ScholarDocument.
    /// For Mode A and Mode B (page-local), this is the parent document ID.
    /// For Mode C (composite): if an explicit ScholarDocument is passed, its DocumentId is used;
    /// if document is null: if all constituent chunks share the same DocumentId, that ID is used;
    /// if constituent chunks span multiple distinct documents, this is set to exactly "composite".
    /// Authoritative per-passage document identity is always preserved in ConstituentChunks and Citations.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Primary page number (1-based) if page-local; 0 if multi-page composite.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// The primary / focal chunk around which this window was formulated (null for unanchored composites).
    /// </summary>
    public DocumentPassageChunk? FocalChunk { get; set; }

    /// <summary>
    /// The constituent passage chunks comprising this window, in document reading order.
    /// </summary>
    public IReadOnlyList<DocumentPassageChunk> ConstituentChunks { get; set; } = [];

    /// <summary>
    /// The ordered list of raw chunk indices included in this window.
    /// Informational only. MUST NOT be treated as a globally unique provenance key.
    /// Authoritative provenance for composite or cross-page windows is carried in ConstituentChunks and Citations.
    /// </summary>
    public IReadOnlyList<int> ConstituentChunkIndices { get; set; } = [];

    /// <summary>
    /// The assembled, bounded, and deduplicated text payload.
    /// </summary>
    public string FormattedText { get; set; } = string.Empty;

    /// <summary>
    /// 0-based start character offset in page NormalizedText (>= 0 for Mode A; -1 for Mode B/C).
    /// </summary>
    public int StartCharOffset { get; set; } = -1;

    /// <summary>
    /// 0-based exclusive end character offset in page NormalizedText (>= 0 for Mode A; -1 for Mode B/C).
    /// </summary>
    public int EndCharOffset { get; set; } = -1;

    /// <summary>
    /// Total character length of FormattedText in UTF-16 code units.
    /// </summary>
    public int CharLength => FormattedText.Length;

    /// <summary>
    /// Heuristic token estimate based on ceil(CharLength / 4.0).
    /// Coarse planning metric for prompt budgeting; not guaranteed exact tokenizer output.
    /// </summary>
    public int EstimatedTokens => (int)Math.Ceiling(CharLength / 4.0);

    /// <summary>
    /// Structured provenance citations for UI and audit navigation.
    /// </summary>
    public IReadOnlyList<StudyCitation> Citations { get; set; } = [];

    /// <summary>
    /// True if candidate text was truncated to respect MaxWindowChars or CompositeBudgetChars.
    /// </summary>
    public bool IsTruncated { get; set; }
}
```

### 3.2 Ordering Mode Enum: `CompositeOrderingMode`
```csharp
namespace Axora.Desktop.Models;

/// <summary>
/// Specifies the ordering policy for multi-passage composite context window assembly.
/// </summary>
public enum CompositeOrderingMode
{
    /// <summary>
    /// Sorts candidate passages into natural document reading order (DocumentId -> PageNumber -> ChunkIndex).
    /// </summary>
    DocumentReadingOrder = 0,

    /// <summary>
    /// Preserves the exact ordering provided by the caller (e.g. descending relevance rank from vector search).
    /// </summary>
    PreserveInputOrder = 1
}
```

### 3.3 Configuration Model: `ContextWindowOptions`
```csharp
namespace Axora.Desktop.Models;

/// <summary>
/// Options governing bounded context window formulation.
/// All bounds are strictly enforced by Validate().
/// </summary>
public sealed record ContextWindowOptions
{
    /// <summary>
    /// Preferred target character budget for greedy window formulation (default: 800 chars ~ 200 tokens).
    /// Bounded in [50, MaxWindowChars].
    /// </summary>
    public int TargetWindowChars { get; init; } = 800;

    /// <summary>
    /// Hard maximum character ceiling for a context window (default: 1,500 chars ~ 375 tokens).
    /// Bounded in [100, 10,000].
    /// </summary>
    public int MaxWindowChars { get; init; } = 1500;

    /// <summary>
    /// Number of preceding neighbor chunks to include when expanding a focal chunk (default: 1).
    /// Bounded in [0, 10].
    /// </summary>
    public int PrecedingNeighborCount { get; init; } = 1;

    /// <summary>
    /// Number of succeeding neighbor chunks to include when expanding a focal chunk (default: 1).
    /// Bounded in [0, 10].
    /// </summary>
    public int SucceedingNeighborCount { get; init; } = 1;

    /// <summary>
    /// If true, adjacent overlapping chunks are merged seamlessly using NormalizedText coordinates
    /// rather than string concatenation, eliminating stride overlap duplication (default: true).
    /// </summary>
    public bool DeduplicateOverlaps { get; init; } = true;

    /// <summary>
    /// If true, multi-passage composite windows format each passage with a citation banner (default: true).
    /// </summary>
    public bool IncludeProvenanceHeaders { get; init; } = true;

    /// <summary>
    /// Maximum character budget for composite multi-page prompt windows (default: 3,000 chars ~ 750 tokens).
    /// Bounded in [200, 50,000].
    /// </summary>
    public int CompositeBudgetChars { get; init; } = 3000;

    /// <summary>
    /// Ordering policy for composite prompt window assembly (default: DocumentReadingOrder).
    /// </summary>
    public CompositeOrderingMode OrderingMode { get; init; } = CompositeOrderingMode.DocumentReadingOrder;

    /// <summary>
    /// Optional default file name used for citation formatting when DocumentPage has no parent reference.
    /// </summary>
    public string DefaultFileName { get; init; } = string.Empty;

    public void Validate()
    {
        if (MaxWindowChars < 100 || MaxWindowChars > 10000)
            throw new ArgumentOutOfRangeException(nameof(MaxWindowChars), "Max window chars must be between 100 and 10,000.");
        if (TargetWindowChars < 50 || TargetWindowChars > MaxWindowChars)
            throw new ArgumentOutOfRangeException(nameof(TargetWindowChars), "Target window chars must be between 50 and MaxWindowChars.");
        if (PrecedingNeighborCount < 0 || PrecedingNeighborCount > 10)
            throw new ArgumentOutOfRangeException(nameof(PrecedingNeighborCount), "Preceding neighbor count must be between 0 and 10.");
        if (SucceedingNeighborCount < 0 || SucceedingNeighborCount > 10)
            throw new ArgumentOutOfRangeException(nameof(SucceedingNeighborCount), "Succeeding neighbor count must be between 0 and 10.");
        if (CompositeBudgetChars < 200 || CompositeBudgetChars > 50000)
            throw new ArgumentOutOfRangeException(nameof(CompositeBudgetChars), "Composite budget chars must be between 200 and 50,000.");
    }
}
```

### 3.4 Method Entry Validation Enforcement
Every public service entry point on `IBoundedContextWindowBuilder` (`FormulateFocalWindow`, `FormulatePageWindows`, `FormulateCompositeWindow`) MUST validate options before performing any formulation work:
- If `options == null`: instantiate default `ContextWindowOptions` (which provides valid defaults).
- If `options != null`: invoke `options.Validate()`.
- If any option property violates bounds: `Validate()` immediately throws `ArgumentOutOfRangeException`.
This eliminates any possibility of bypassing security bounds through unvalidated caller options.

---

## 4. Target vs. Maximum Operational Semantics

The interaction between `TargetWindowChars` and `MaxWindowChars` is defined by an explicit deterministic decision tree:

### 4.1 In `FormulateFocalWindow`:
1. **Candidate Pool Bounding Invariant**:
   For focal chunk $C_k$ among $M$ ordered chunks on the page ($k \in [0, M-1]$):
   $$k_{\min} = \max(0, k - \text{PrecedingNeighborCount})$$
   $$k_{\max} = \min(M - 1, k + \text{SucceedingNeighborCount})$$
   Candidate accumulation MUST remain strictly within $[k_{\min}, k_{\max}]$. Target accumulation CANNOT expand beyond $k_{\min}$ or $k_{\max}$ under any circumstances.

2. **Candidate Traversal Sequence**:
   Neighbors are evaluated strictly in alternating order starting immediately adjacent to $k$:
   $$\text{Sequence: } (k - 1), (k + 1), (k - 2), (k + 2), \dots, (k - i), (k + i)$$
   after filtering/clamping to $[k_{\min}, k_{\max}]$ and removing $k$ itself.
   If $k - i < k_{\min}$, only $k + i$ is evaluated; if $k + i > k_{\max}$, only $k - i$ is evaluated.

3. **Greedy Expansion & Halting Rules**:
   - Start with focal chunk only: initial span $[cur_{\min} = k, cur_{\max} = k]$.
   - Evaluate candidate neighbors in the alternating order above.
   - For each candidate neighbor $C_{\text{next}}$, calculate prospective continuous `NormalizedText` span $[new_{\min}, new_{\max}] = [\min(cur_{\min}, C_{\text{next}}.\text{ChunkIndex}), \max(cur_{\max}, C_{\text{next}}.\text{ChunkIndex})]$.
   - Calculate projected continuous span length:
     $$\text{spanLength} = \text{page.Chunks}[new_{\max}].\text{EndCharOffset} - \text{page.Chunks}[new_{\min}].\text{StartCharOffset}$$
   - **Case 1**: If $\text{spanLength} \le \text{options.TargetWindowChars}$:
     Accept neighbor ($cur_{\min} = new_{\min}, cur_{\max} = new_{\max}$) and continue loop.
   - **Case 2**: If $\text{spanLength} > \text{options.TargetWindowChars}$ AND $\text{spanLength} \le \text{options.MaxWindowChars}$:
     Accept neighbor as **FINAL expansion** ($cur_{\min} = new_{\min}, cur_{\max} = new_{\max}$) and **immediately stop expansion**.
   - **Case 3**: If $\text{spanLength} > \text{options.MaxWindowChars}$:
     Reject neighbor and **immediately stop expansion**.
   - **Invariant**: Never continue searching for another farther neighbor after any candidate causes `MaxWindowChars` rejection.
   - **Invariant**: Never add a chunk outside $[k_{\min}, k_{\max}]$.
   - **Invariant**: Preserve source chunk ordering in `ConstituentChunks`: chunks $[cur_{\min} \dots cur_{\max}]$ in ascending index order.

4. **Oversized Focal Chunk Clamping**:
   If the focal chunk $C_k$ alone exceeds `options.MaxWindowChars`:
   - It is clamped to `options.MaxWindowChars` via a deterministic 3-tier fallback:
     - **Tier 1 (Sentence Boundary)**: Nearest sentence boundary at or below `options.MaxWindowChars`.
       *Rule*: Character '.', '?', or '!' followed by whitespace (`char.IsWhiteSpace`) or end of string, searching backwards from `options.MaxWindowChars`. The sentence-ending punctuation mark is included in the truncated text.
     - **Tier 2 (Word Boundary Fallback)**: If no sentence boundary $\ge \text{options.TargetWindowChars}$ exists, nearest word boundary at or below `options.MaxWindowChars`.
       *Rule*: Whitespace character (`char.IsWhiteSpace`) searching backwards from `options.MaxWindowChars`. The slice is taken up to the whitespace.
     - **Tier 3 (Hard UTF-16-Safe Character Slice Fallback)**: If no word boundary exists (e.g. unbroken token or URL), hard character slice at `options.MaxWindowChars`.
       *Surrogate-Pair Protection Rule*: If index `options.MaxWindowChars` falls between a high surrogate (`\uD800..\uDBFF`) and a low surrogate (`\uDC00..\uDFFF`), decrement the slice length by 1 to prevent emitting a malformed surrogate pair.
   - State assignment:
     - `IsTruncated = true`
     - `ConstituentChunks = [focalChunk]`
     - `FocalChunk` remains `focalChunk`
     - `StartCharOffset = focalChunk.StartCharOffset`
     - `EndCharOffset = focalChunk.StartCharOffset + FormattedText.Length`
     - `FormattedText` remains an exact substring of `page.NormalizedText`.

### 4.2 In `FormulatePageWindows`:
`FormulatePageWindows(DocumentPage page, string documentId, ContextWindowOptions? options = null)` formulates a sequence of bounded context windows for an entire physical page.

#### Algorithmic Specification:
1. **Validation**: Validate `page != null` and `documentId != null` (throw `ArgumentNullException`). Validate options: `(options ?? new ContextWindowOptions()).Validate()`.
2. **Empty Page Handling**: If `page.Chunks` is null or `page.Chunks.Count == 0`, return empty list `[]`.
3. **Single-Chunk Page**: If $M = 1$, return exactly 1 window with `ConstituentChunks = [C_0]`, $k_{\min} = 0, k_{\max} = 0$.
4. **Two-Chunk Page**: If $M = 2$, return exactly 2 windows ($k = 0$ and $k = 1$), each evaluating neighbors within $[k_{\min}, k_{\max}]$.
5. **Sliding Focal Window Formulation (Stride = 1 Chunk)**:
   - For each chunk index $k \in [0, M-1]$ (where $M = \text{page.Chunks.Count}$):
     - Formulate a focal window using $C_k = \text{page.Chunks}[k]$ as the focal anchor via `FormulateFocalWindow(C_k, page, options)`.
     - Assign `window.FocalChunk = C_k`.
     - Assign `window.DocumentId = documentId`.
     - Assign deterministic spatial WindowId:
       - Mode A: `$"win_{documentId}_p{page.PageNumber}_f{k}_c{minChunk}_{maxChunk}"`
       - Mode B: `$"win_{documentId}_p{page.PageNumber}_f{k}_c{minChunk}_{maxChunk}_fb"`
6. **Page Boundary Clamping**: At $k = 0$, preceding neighbors clamp to 0. At $k = M - 1$, succeeding neighbors clamp to $M - 1$.
7. **Orphan Focal Chunk**: If a chunk is provided that is not found in `page.Chunks`, emit a standalone window with `ConstituentChunks = [focalChunk]`.
8. **Ordering & Preservation Guarantee**: Returns strictly $M$ windows ordered sequentially by focal chunk index $k$ ascending ($0 \dots M-1$).
9. **Oversized Chunk Clamping**: If any chunk $C_k$ exceeds `MaxWindowChars`, it is truncated along sentence/word/surrogate boundaries with `IsTruncated = true`.

### 4.3 In `FormulateCompositeWindow` (Delimiter & First-Passage Budgeting):
- **Delimiter & Header Accounting**:
  - When `options.IncludeProvenanceHeaders == true`:
    `prefix = $"\n\n--- [Page {C.PageNumber}, Passage {C.ChunkIndex}] ---\n"`
    Header characters count toward `options.CompositeBudgetChars`.
  - When `options.IncludeProvenanceHeaders == false`:
    `prefix = (i == 0) ? "" : "\n\n"`
    Delimiter `\n\n` (2 characters) counts toward `options.CompositeBudgetChars`.
- **First Candidate Passage Exceeding Budget**:
  If candidate passage #1 alone (plus its prefix) exceeds `options.CompositeBudgetChars`:
  - Calculate available text budget:
    $$\text{availableTextBudget} = \text{options.CompositeBudgetChars} - \text{prefix.Length}$$
  - **If $\text{availableTextBudget} \ge 50$**:
    Truncate candidate passage #1 to fit `availableTextBudget` using the deterministic 3-tier fallback (sentence $\to$ word $\to$ hard slice with surrogate-pair protection).
    Append `prefix` and truncated text.
    Add passage to `ConstituentChunks`.
    Add citation to `Citations`.
    Set `IsTruncated = true`.
    Halt further accumulation immediately.
  - **If $\text{availableTextBudget} < 50$**:
    Emit no passage. Return empty `BoundedContextWindow` with `FormattedText = ""`, `WindowId = "comp_empty"`, `ConstituentChunks = []`, `Citations = []`, and `IsTruncated = true`.
- **Subsequent Passages Exceeding Budget**:
  For candidate passage $i > 0$, if adding `prefix.Length + C.Text.Length` would cause total length to exceed `options.CompositeBudgetChars`:
  - Stop accumulating further chunks.
  - Break loop.
  - Set `IsTruncated = true`.
- **Hard Invariant**:
  $$\text{FormattedText.Length} \le \text{options.CompositeBudgetChars} \quad \text{unconditionally.}$$

---

## 5. Persistence & State Invariants

1. **Ephemeral / On-Demand Construction**:
   - `BoundedContextWindow` is NOT persisted into `ScholarDocument.json` files on disk.
   - `DocumentPage.Chunks` remains the single persisted source of truth for passage units.
   - Pre-computing and saving context windows to disk would introduce massive redundant text duplication.
   - Because `BoundedContextWindowBuilder` executes in-memory with a nominal sub-millisecond engineering target, windows are formulated on-demand during query execution, retrieval, or synthesis.
2. **Schema Version Preservation**:
   - `ScholarPersistenceConstants.CurrentSchemaVersion` remains `1`.
   - Zero database migrations or breaking changes to existing `%APPDATA%\Axora\Scholar\documents\` files.

---

## 6. Verification & Acceptance Criteria

Stage C7.3 will be verified via a dedicated integration suite containing **49 concrete assertions** across Categories A through M in `Axora.Desktop.Tests/Program.cs`:
1. Focal window single chunk (radius 0) (Cat A: 4 assertions).
2. Focal window preceding and succeeding neighbor expansion (Cat B: 4 assertions).
3. Overlap deduplication mathematical proof (zero text stutter) (Cat C: 2 assertions).
4. Page boundary clamping at extremities (Cat D: 3 assertions).
5. Tri-modal provenance semantics (Modes A, B, C) (Cat E: 4 assertions).
6. Budget clamping, target/max decision tree, and truncation flags (Cat F: 4 assertions).
7. Composite multi-page prompt packing with `DocumentReadingOrder`, `PreserveInputOrder`, cross-document deduplication, delimiter budgeting, and first-passage overflow (Cat G: 7 assertions).
8. Structured provenance citations and heuristic token estimation (Cat H: 3 assertions).
9. Determinism, stable spatial WindowId, and full 64-hex SHA-256 composite WindowId (Cat I: 3 assertions).
10. Multi-threaded concurrency across 20 parallel threads (Cat J: 1 assertion).
11. Adversarial edge cases, option validation bounds, and method entry validation (Cat K: 9 assertions).
12. Page window formulation sliding sequence, single chunk, and oversized clamping (Cat L: 4 assertions).
13. Downstream consumer simulation (`DocumentChatService` adapter) (Cat M: 1 assertion).

Total planned Stage C7.3 assertions: **49**.  
Baseline passing assertions: **1,278**.  
Post-C7.3 expected passing assertions: **1,327**.

---

## 7. Contract Closure & Progression Gate

Stage **W3-C.7.3** represents the completion of Phase W3-C.7. Upon implementation, verification, and independent code audit of C7.3:
- Phase **W3-C.7 (Passage Chunking & Bounded Context Window Formulation)** will be formally **CLOSED**.
- Progression to **Phase W3-D (Local Vector Embedding & Hybrid Search)** will be **AUTHORIZED**.
