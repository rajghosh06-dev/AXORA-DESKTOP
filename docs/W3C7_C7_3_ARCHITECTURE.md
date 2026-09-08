# Phase W3-C.7 Stage C7.3 Architecture: Bounded Context Window Formulation

**Phase**: `W3-C.7 — Passage Chunking & Bounded Context Window Formulation`  
**Stage**: `C7.3 — Bounded Context Window Formulation`  
**Status**: `PLANNING REMEDIATED (PASS 3) / AWAITING THIRD INDEPENDENT PLANNING AUDIT`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Architectural Pipeline Position

In the AXORA Scholar Kit document ingestion architecture, Stage C7.3 sits directly between the stored atomic passage chunks (`DocumentPage.Chunks`) and downstream AI/cognitive consumers:

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                        Document Ingestion Pipeline                           │
│                                                                              │
│  1. Format Sniffing: IDocumentFormatDetector                                 │
│  2. Raw Extraction: IDocumentExtractorEngine (Verbatim RawText)              │
│  3. Normalization: ITextNormalizer (Canonical NormalizedText)                │
│  4. Page Building: IDocumentPageBuilder                                      │
│  5. Passage Chunking: IPassageChunker (C7.1 + C7.2)                          │
│     └─► Emits DocumentPassageChunk[] attached to DocumentPage.Chunks         │
│  6. Persistence: ScholarLibraryService (Writes to %APPDATA%\Axora\Scholar)   │
└──────────────────────────────────────┬───────────────────────────────────────┘
                                       │
                                       ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│              Phase W3-C.7 Stage C7.3: Bounded Context Window                 │
│                                                                              │
│                     IBoundedContextWindowBuilder                             │
│                     (BoundedContextWindowBuilder)                            │
│                                      │                                       │
│          ┌───────────────────────────┼───────────────────────────┐           │
│          ▼                           ▼                           ▼           │
│  [ Mode A: Focal Window ]   [ Mode B: Fallback Window ] [ Mode C: Composite ]│
│  Page-local neighbor        Missing NormalizedText;     Multi-passage prompt │
│  expansion & coordinate     chunk text concatenation;   packing across pages;│
│  stride deduplication.      offsets unmapped (-1).      Budget-capped RAG.   │
│          │                           │                           │           │
│          └───────────────────────────┼───────────────────────────┘           │
│                                      ▼                                       │
│                           [ BoundedContextWindow ]                           │
└──────────────────────────────────────┬───────────────────────────────────────┘
                                       │
                                       ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                            Downstream Consumers                              │
│                                                                              │
│  1. Phase W3-D: Local Vector Embeddings (MiniLM-L6-v2 ONNX Context Windows)  │
│  2. Phase W3-E: Local SLM Synthesis & Grounded Question Answering            │
│  3. DocumentChatService: 100% Offline Multi-Passage RAG Chat Assistant       │
│  4. Study Artifact Extractors: Concept Definitions & Practice Quiz Generator │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Component Architecture

Stage C7.3 introduces a dedicated, high-performance service abstraction:

```
                          ┌────────────────────────────────┐
                          │  IBoundedContextWindowBuilder  │
                          └───────────────▲────────────────┘
                                          │
                    ┌─────────────────────┴─────────────────────┐
                    │        BoundedContextWindowBuilder        │
                    │      (Stateless, Sync & Thread-Safe)      │
                    └──────────┬───────────────────┬────────────┘
                               │                   │
             ┌─────────────────┴─────┐       ┌─────┴─────────────────┐
             ▼                       │       │                       ▼
┌──────────────────────────┐         │       │          ┌──────────────────────────┐
│   FocalWindowFormulator  │         │       │          │  CompositeWindowPacker   │
│  (Page-local neighbor    │         │       │          │ (Multi-passage greedy    │
│   radius expansion &     │         │       │          │  budget knapsack packing │
│   coordinate boundaries) │         │       │          │  with citation headers)  │
└──────────────────────────┘         │       │          └──────────────────────────┘
                                     ▼       ▼
                        ┌──────────────────────────┐
                        │    OverlapDeduplicator   │
                        │ (Extracts continuous span│
                        │  from NormalizedText to  │
                        │  eliminate stutter)      │
                        └──────────────────────────┘
```

### 2.1 Interface Definition: `IBoundedContextWindowBuilder`
The interface is intentionally synchronous. Formulation is an in-memory, CPU-bound calculation with a nominal engineering target of sub-millisecond return for typical passage sizes (not an SLA and not validated until benchmarked). `CancellationToken` is omitted from the public contract:

```csharp
namespace Axora.Desktop.Services.Contracts;

using System.Collections.Generic;
using Axora.Desktop.Models;

/// <summary>
/// Service contract for formulating bounded, semantically coherent, and traceable
/// context windows from discrete DocumentPassageChunk instances.
/// Stateless, synchronous, and thread-safe.
/// </summary>
public interface IBoundedContextWindowBuilder
{
    /// <summary>
    /// Formulates a bounded context window around a focal passage chunk on a single page,
    /// expanding to adjacent preceding and succeeding neighbor chunks within configured budget
    /// and seamlessly deduplicating stride overlaps.
    /// </summary>
    BoundedContextWindow FormulateFocalWindow(
        DocumentPassageChunk focalChunk,
        DocumentPage page,
        ContextWindowOptions? options = null);

    /// <summary>
    /// Formulates a sliding sequence of bounded focal context windows for an entire page (stride = 1 chunk).
    /// Useful for dense vector embedding generation and section-level indexing.
    /// </summary>
    IReadOnlyList<BoundedContextWindow> FormulatePageWindows(
        DocumentPage page,
        string documentId,
        ContextWindowOptions? options = null);

    /// <summary>
    /// Assembles multiple candidate passage chunks (potentially across multiple pages and documents)
    /// into a bounded composite context window for downstream synthesis or prompt context,
    /// strictly enforcing maximum character/token budget.
    /// </summary>
    BoundedContextWindow FormulateCompositeWindow(
        IEnumerable<DocumentPassageChunk> retrievedChunks,
        ScholarDocument? document = null,
        ContextWindowOptions? options = null);
}
```

### 2.2 Method Entry Validation Enforcement
Every public service method on `IBoundedContextWindowBuilder` validates options at entry:
```csharp
var resolvedOptions = options ?? new ContextWindowOptions();
resolvedOptions.Validate();
```
If a caller provides an explicit `options` object with any property outside the valid ranges, `Validate()` immediately throws `ArgumentOutOfRangeException`, preventing invalid parameters from bypassing security bounds.

---

## 3. Mathematical Formulation of Overlap Deduplication

### 3.1 The Stride Overlap Stutter Problem
In Stage C7.1, `PassageChunker` generates passages using a target size of $T = 350$ code units and a stride overlap of $O = 60$ code units.

Consider two consecutive chunks $C_k$ and $C_{k+1}$ on page $P$:
- $C_k$ covers character span $[s_k, e_k)$ in `NormalizedText`.
- $C_{k+1}$ covers character span $[s_{k+1}, e_{k+1})$ in `NormalizedText`.
- By definition of stride overlap, $s_{k+1} < e_k$.
- The substring in span $[s_{k+1}, e_k)$ is shared by **both** $C_k$ and $C_{k+1}$.

If a naive retrieval system or context builder constructs a context window via string concatenation:
$$\text{NaiveConcat} = C_k.\operatorname{Text} + \text{" "} + C_{k+1}.\operatorname{Text}$$
The text in $[s_{k+1}, e_k)$ appears **twice** in the output! This causes catastrophic stutter when fed to an LLM or displayed in the UI:
> *Example Naive Stutter*: "...the experiment concluded with positive results. with positive results. Subsequent analysis showed..."

### 3.2 Coordinate Span Resolution (Zero Stutter)
Stage C7.3 solves this by operating on the exact UTF-16 coordinates $[s_k, e_k)$ recorded during chunking:

Let $k_{\min}$ be the lowest chunk index included in the window, and $k_{\max}$ be the highest chunk index.
1. **Window Span Boundaries in `NormalizedText`**:
   $$s_{\text{window}} = C_{k_{\min}}.\operatorname{StartCharOffset}$$
   $$e_{\text{window}} = C_{k_{\max}}.\operatorname{EndCharOffset}$$
2. **Span Length**:
   $$L_{\text{window}} = e_{\text{window}} - s_{\text{window}}$$
3. **Exact Substring Extraction**:
   When `page.NormalizedText` is available:
   $$\operatorname{FormattedText} = P.\operatorname{NormalizedText}.\operatorname{Substring}(s_{\text{window}}, L_{\text{window}})$$

### Mathematical Guarantees:
1. **Zero Text Duplication**: Because the text is sliced as a single continuous substring from `NormalizedText`, the overlap span $[s_{k+1}, e_k)$ is traversed exactly once.
2. **Substantive Continuity**: Any inter-chunk whitespace or punctuation that was between the chunks is faithfully preserved in its natural reading flow.
3. **100% Substring Provenance for Mode A**:
   $$P.\operatorname{NormalizedText}.\operatorname{Substring}(W.\operatorname{StartCharOffset}, W.\operatorname{CharLength}) \equiv W.\operatorname{FormattedText}$$
   This satisfies Invariant 2 unconditionally for Mode A.

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
`FormulatePageWindows(DocumentPage page, string documentId, ContextWindowOptions? options = null)` formulates a complete sliding sequence of bounded focal context windows for an entire physical page.

#### Algorithmic Specification:
1. **Entry Validation**:
   - `page` and `documentId` are checked for null (throws `ArgumentNullException`).
   - Options are validated: `(options ?? new ContextWindowOptions()).Validate()`.
2. **Empty Page Handling**:
   - If `page.Chunks` is null or `page.Chunks.Count == 0`: return empty list `[]`.
3. **Single-Chunk Page**:
   - If $M = 1$: return exactly 1 window with `ConstituentChunks = [C_0]`, $k_{\min} = 0, k_{\max} = 0$.
4. **Two-Chunk Page**:
   - If $M = 2$: return exactly 2 windows ($k = 0$ and $k = 1$), each evaluating neighbors within $[k_{\min}, k_{\max}]$.
5. **Sliding Focal Window Formulation (Stride = 1 Chunk)**:
   - For each passage chunk index $k \in [0, M-1]$ on the page:
     - Formulate a focal context window centered on $C_k = \text{page.Chunks}[k]$ via `FormulateFocalWindow(C_k, page, options)`.
     - Assign `window.FocalChunk = C_k`.
     - Assign `window.DocumentId = documentId`.
     - Assign deterministic spatial WindowId:
       - Mode A (with `NormalizedText`): `$"win_{documentId}_p{page.PageNumber}_f{k}_c{minChunk}_{maxChunk}"`
       - Mode B (missing `NormalizedText`): `$"win_{documentId}_p{page.PageNumber}_f{k}_c{minChunk}_{maxChunk}_fb"`
6. **Page Boundary Clamping**: At $k = 0$, preceding neighbors clamp to 0. At $k = M - 1$, succeeding neighbors clamp to $M - 1$.
7. **Orphan Focal Chunk**: If a chunk is provided that is not found in `page.Chunks`, emit a standalone window with `ConstituentChunks = [focalChunk]`.
8. **Ordering Guarantee**:
   - Returns strictly $M$ windows, ordered sequentially by focal chunk index $k$ ascending ($0 \dots M-1$).
9. **Oversized Chunk Clamping**:
   - If any chunk $C_k$ exceeds `MaxWindowChars`, it is truncated along sentence/word/surrogate boundaries with `IsTruncated = true`, adhering strictly to `RULE-C73-BDG-04`.

---

## 5. Composite Prompt Assembly Algorithm

For downstream RAG and chat synthesis (`FormulateCompositeWindow`):

```csharp
public BoundedContextWindow FormulateCompositeWindow(
    IEnumerable<DocumentPassageChunk> retrievedChunks,
    ScholarDocument? document = null,
    ContextWindowOptions? options = null)
```

1. **Method Entry Validation**:
   - Validate options: `(options ?? new ContextWindowOptions()).Validate()`.
2. **Input Filtering & Deduplication**:
   - Filter out null chunks or chunks with empty text.
   - If candidate list is empty, return empty `BoundedContextWindow` with `FormattedText = string.Empty`, `WindowId = "comp_empty"`, `DocumentId = document?.DocumentId ?? "composite"`, and `Citations = []`.
   - Deduplicate using composite key: `(DocumentId, PageNumber, ChunkIndex)`. If a chunk with the same tuple was already observed, ignore subsequent duplicates. Under `PreserveInputOrder`, the first occurrence is retained.
3. **Ordering Policy**:
   - If `options.OrderingMode == CompositeOrderingMode.DocumentReadingOrder`:
     Sort candidate chunks by `DocumentId` ascending, then `PageNumber` ascending, then `ChunkIndex` ascending.
   - If `options.OrderingMode == CompositeOrderingMode.PreserveInputOrder`:
     Preserve the exact order yielded by the caller (e.g., descending similarity rank from vector retrieval).
4. **Greedy Budget & Delimiter Accumulation**:
   - Maintain a `StringBuilder` and a running budget counter against `options.CompositeBudgetChars` (default: 3,000 chars, ceiling: 50,000 chars).
   - For candidate passage index $i = 0$:
     - Determine prefix:
       - If `options.IncludeProvenanceHeaders == true`:
         `prefix = $"\n\n--- [Page {C.PageNumber}, Passage {C.ChunkIndex}] ---\n";`
       - If `options.IncludeProvenanceHeaders == false`:
         `prefix = "";`
     - If `prefix.Length + C.Text.Length <= options.CompositeBudgetChars`:
       - Append `prefix` and `C.Text`.
       - Add $C$ to `ConstituentChunks`.
       - Add citation to `Citations`.
     - Else:
       - Calculate available text budget: `availableTextBudget = options.CompositeBudgetChars - prefix.Length`.
       - If `availableTextBudget >= 50`:
         Truncate $C$ to fit `availableTextBudget` using the deterministic 3-tier fallback (sentence $\to$ word $\to$ hard slice with surrogate protection).
         Append `prefix` and truncated text.
         Add $C$ to `ConstituentChunks`.
         Add citation to `Citations`.
         Set `IsTruncated = true`.
         Stop further accumulation (break loop).
       - Else (`availableTextBudget < 50`):
         Omit candidate #1. Emit empty `BoundedContextWindow` with `FormattedText = ""`, `WindowId = "comp_empty"`, and `IsTruncated = true`.
         Stop further accumulation (break loop).
   - For candidate passage index $i > 0$:
     - Determine prefix:
       - If `options.IncludeProvenanceHeaders == true`:
         `prefix = $"\n\n--- [Page {C.PageNumber}, Passage {C.ChunkIndex}] ---\n";`
       - If `options.IncludeProvenanceHeaders == false`:
         `prefix = "\n\n";`
     - Calculate projected added length: `prefix.Length + C.Text.Length`.
     - If `currentLength + addedLength <= options.CompositeBudgetChars`:
       - Append `prefix` and `C.Text`.
       - Add $C$ to `ConstituentChunks`.
       - Add citation to `Citations`.
     - Else:
       - Stop accumulating further chunks.
       - Set `IsTruncated = true`.
       - Break loop.
5. **Result Construction & DocumentId Resolution**:
   - Resolve `DocumentId`:
     - If `document != null`: `window.DocumentId = document.DocumentId`.
     - If `document == null`:
       - If all constituent chunks share the same `DocumentId`: use that `DocumentId`.
       - If constituent chunks span multiple distinct `DocumentId` values (or if empty): set `window.DocumentId = "composite"`.
   - Authoritative per-passage document identity is always preserved in `ConstituentChunks` and `Citations`.
   - `ConstituentChunkIndices` is populated with raw `ChunkIndex` values (informational only; not a globally unique key).
   - Emit `BoundedContextWindow` with `PageNumber = 0` (composite), `StartCharOffset = -1`, `EndCharOffset = -1`, populated `FormattedText`, `Citations`, and deterministic, cryptographically collision-resistant `WindowId`.

---

## 6. Deterministic Window Identification & Collision Resistance

Every formulated window receives a deterministic identifier derived from stable coordinates:
- **Mode A (Focal Window)**:
  `WindowId = $"win_{documentId}_p{pageNumber}_f{focalChunkIndex}_c{minChunk}_{maxChunk}"`
- **Mode B (Fallback Window)**:
  `WindowId = $"win_{documentId}_p{pageNumber}_f{focalChunkIndex}_c{minChunk}_{maxChunk}_fb"`
  *Identity Scoping Note*: The focal WindowId represents a **spatial coordinate identity** indicating the contiguous constituent chunk range $[k_{\min}, k_{\max}]$ around focal chunk $k$ on page $P$ within a consistent options/configuration context. It identifies the spatial selection of chunks; callers must not assume that different options configurations producing differing formatted texts for the same chunk range will emit distinct spatial coordinate IDs.
- **Mode C (Composite Window)**:
  Derived from a canonical coordinate string over the complete ordered sequence of constituent chunks:
  $$S_{\text{coords}} = \text{string.Join}(";", \text{Constituents}.\text{Select}(c \Rightarrow \$"\{c.\text{DocumentId}\}:p\{c.\text{PageNumber}\}:c\{c.\text{ChunkIndex}\}"))$$
  $$H_{64} = \text{Convert.ToHexString}(\text{SHA256.HashData}(\text{Encoding.UTF8.GetBytes}(S_{\text{coords}}))).\text{ToLowerInvariant}()$$
  $$\text{WindowId} = \$\text{"comp\_"}\{H_{64}\} \quad (\text{or } \$\text{"comp\_empty"} \text{ if empty})$$

Zero `Guid.NewGuid()` runtime randomness is used. Repeated runs yield bit-for-bit identical `WindowId` values, and different sets of constituent chunks are cryptographically collision-resistant via full 256-bit SHA-256 digest.

---

## 7. Dependency Injection & Downstream Scope

### 7.1 Dependency Injection Registration
`IBoundedContextWindowBuilder` will be registered in `Axora.Desktop/App.xaml.cs`:
```csharp
services.AddSingleton<IBoundedContextWindowBuilder, BoundedContextWindowBuilder>();
```
The service is completely stateless and thread-safe.

### 7.2 Downstream Scope Containment (`DocumentChatService.cs`)
- `DocumentChatService.cs` **SHALL NOT** be modified during Stage C7.3.
- Stage C7.3 establishes the public domain contracts and implementation.
- Compatibility with `DocumentChatService` will be verified via a simulated adapter test in `Axora.Desktop.Tests/Program.cs`.
- Refactoring `DocumentChatService.cs` to consume `IBoundedContextWindowBuilder` is formally deferred to Phase W3-D/W3-E.

---

## 8. Algorithmic Complexity & Concurrency

| Metric | Bound | Justification |
| :--- | :--- | :--- |
| **Time Complexity (Focal)** | $O(K)$ where $K \le 21$ | Under default options ($N_p=1, N_s=1$), $K \le 3$. Under maximum options ($N_p \le 10, N_s \le 10$), $K \le 21$. Substring extraction is $O(1)$ relative to page size. |
| **Time Complexity (Composite)** | $O(N \log N + M)$ | $N$ is candidate chunks sorted by ordering mode ($N \le 100$); $M$ is accumulated characters ($M \le 50,000$). |
| **Space Complexity** | $O(W)$ | $W$ is the character length of the formulated window ($W \le 10,000$ focal; $W \le 50,000$ composite). |
| **Thread Safety** | Fully Reentrant | Stateless singleton. No shared mutable state. Fully concurrent across threads and documents. |
| **Resource Safety** | Hard Ceilings Enforced | `ContextWindowOptions.Validate()` enforces: `MaxWindowChars <= 10,000`, `CompositeBudgetChars <= 50,000`, and `NeighborCount <= 10`. |
