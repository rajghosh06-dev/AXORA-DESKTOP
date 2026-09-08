# Phase W3-C.7 Architecture: Passage Chunking & Bounded Context Window Formulation

**Phase**: `W3-C.7 — Passage Chunking & Bounded Context Window Formulation`  
**Status**: `PLANNING ONLY / RECONCILED POST-AUDIT`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Architectural Pipeline Position

The passage chunking engine operates downstream of Phase W3-C.6 text normalization and immediately prior to document finalization in `ScholarExtractionOrchestrator`:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     ScholarExtractionOrchestrator                       │
│                                                                         │
│  1. IDocumentFormatDetector.DetectFormat(stream, name)                  │
│  2. IDocumentExtractorEngine.ExtractAsync(stream, options, ct)          │
│     └─► Produces RawExtractionResult (Verbatim RawText ground truth)    │
│  3. ITextNormalizer.NormalizePage(page, format, options)                │
│     └─► Produces ExtractedPageRaw.NormalizedText                        │
│  4. IDocumentPageBuilder.BuildPages(normalizedPages)                    │
│     └─► Produces DocumentPage instances (RawText + NormalizedText)      │
│  5. IPassageChunker.ChunkPage(docId, pageNum, normText, options) ◄── C7 │
│     └─► Populates DocumentPage.Chunks with DocumentPassageChunk[]       │
│  6. ExtractionReport & ScholarDocument Materialization                  │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │
                                     ▼
                    ┌─────────────────────────────────┐
                    │     ScholarLibraryService       │
                    │  (Persists to %APPDATA%\Axora)  │
                    └─────────────────────────────────┘
```

### Ingestion Flow Contract:
1. `ScholarExtractionOrchestrator` receives normalized pages from `DocumentPageBuilder`.
2. For each `DocumentPage`, if `NormalizedText` is non-empty and `IPassageChunker` is registered:
   ```csharp
   var chunks = _passageChunker.ChunkPage(documentId, docPage.PageNumber, docPage.NormalizedText, options.Chunking);
   docPage.Chunks.AddRange(chunks);
   ```
3. `ExtractionReport.TotalChunks` records the aggregate count of all generated passages.
4. Passages are stored directly inside `DocumentPage.Chunks` and serialized into document JSON representation.

---

## 2. Component Decomposition

The chunking engine is structured into four focused internal components:

```
                          ┌──────────────────────┐
                          │   IPassageChunker    │
                          └──────────▲───────────┘
                                     │
                     ┌───────────────┴───────────────┐
                     │        PassageChunker         │
                     │    (Orchestrates Pipeline)    │
                     └───────┬───────────────┬───────┘
                             │               │
            ┌────────────────┴────┐    ┌─────┴────────────────┐
            ▼                     │    │                      ▼
┌───────────────────────────────┐ │    │ ┌───────────────────────────────┐
│    PageStructuralSegmenter    │ │    │ │    PassageWindowPacker        │
│ (Splits page into typed blocks│ │    │ │ (Greedy window accumulation,  │
│  Headings, Code, Tables, Para)│ │    │ │  stride overlap calculation)  │
└───────────────────────────────┘ │    │ └───────────────────────────────┘
                                  ▼    ▼
                     ┌───────────────────────────────┐
                     │    SentenceBoundaryScanner    │
                     │  (Regex/finite state scanner  │
                     │   with abbreviation whitelist)│
                     └───────────────────────────────┘
```

### 2.1 `PassageChunker` (Public Entry Point)
- Implements `IPassageChunker`.
- Validates input arguments: `documentId`, `pageNumber`, `pageText`, `options`.
- Coordinates segmentation, sentence scanning, window packing, and offset calculation.
- Executes defensive exception containment with deterministic bounded fallback.

### 2.2 `PageStructuralSegmenter` (Internal Structural Analyzer)
- Scans `NormalizedText` into a linear sequence of `StructuralBlock` records.
- Identifies structural types:
  - `StructuralBlockKind.Heading`: Lines starting with `#`.
  - `StructuralBlockKind.CodeBlock`: Blocks enclosed by ` ``` ` or `~~~`.
  - `StructuralBlockKind.Table`: Tabular rows with `|` delimiters.
  - `StructuralBlockKind.List`: Consecutive list items (`- `, `* `, `1. `).
  - `StructuralBlockKind.Blockquote`: Lines beginning with `> `.
  - `StructuralBlockKind.Paragraph`: Body text blocks separated by `\n\n`.
- Records exact starting and ending UTF-16 code-unit offsets $[s_{\text{block}}, e_{\text{block}})$ in `NormalizedText`.

### 2.3 `SentenceBoundaryScanner` (Internal Sentence Engine)
- Scans paragraph text for true sentence terminators: `. `, `? `, `! `, `.\n`, `?\n`, `!\n`.
- **Abbreviation Suppression**: Evaluates a high-performance hash set of academic and general abbreviations to suppress false splits:
  - Titles: `Mr.`, `Mrs.`, `Ms.`, `Dr.`, `Prof.`, `Rev.`, `Sr.`, `Jr.`
  - Academic Latin: `e.g.`, `i.e.`, `et al.`, `cf.`, `vs.`, `ibid.`, `op. cit.`
  - Publication Tokens: `Fig.`, `Figs.`, `Tab.`, `Vol.`, `No.`, `pp.`, `p.`, `Eq.`, `Ref.`
  - Decimal & Numerical Guard: Digits surrounding periods (`3.14159`, `$12.50`, `v1.0.4`) never trigger a sentence break.

### 2.4 `PassageWindowPacker` (Internal Budget & Overlap Packer)
- Greedily accumulates structural blocks and sentences up to `TargetChunkSizeChars` (350 UTF-16 code units).
- Applies oversized slicing rules if a single structural unit exceeds `MaxChunkSizeChars` (600 code units).
- Computes stride overlap when `StrideOverlapChars > 0`.
- Executes sentence and word snapping with deterministic tie-breaking.
- Calculates exact trimmed offsets such that `NormalizedText.Substring(StartCharOffset, CharLength) == Text` holds strictly.

---

## 3. Mathematical Stride & Snapping Formulation

Let:
- $T = \text{TargetChunkSizeChars} = 350$ (UTF-16 code units)
- $M = \text{MaxChunkSizeChars} = 600$ (UTF-16 code units)
- $A = \text{AbsoluteMaxChunkSizeChars} = 2000$ (UTF-16 code units)
- $O = \text{StrideOverlapChars} = 60$ (UTF-16 code units)
- $\Delta = \text{SentenceSnapBoundaryDelta} = 40$ (UTF-16 code units)
- $N = \operatorname{Length}(\operatorname{NormalizedText})$

### 3.1 Non-Overlapping Mode ($O = 0$)
When overlap is disabled, passages form a strictly disjoint partition of substantive text:
$$s_{k+1} \ge e_k$$
Inter-block delimiter whitespace occurring in $[e_k, s_{k+1})$ is skipped.

### 3.2 Overlapping Mode ($O > 0$)
When overlap is enabled, subsequent chunks re-include a bounded suffix of the preceding chunk:
1. **Nominal Overlap Start**:
   $$s^* = e_k - O$$
2. **Snap Search Interval**:
   $$I_{\text{snap}} = [s^* - \Delta, \quad s^* + \Delta]$$
3. **Boundary Resolution Hierarchy & Tie-Breaking**:
   - **Step 1: Sentence Start**: If `SnapToSentenceBoundaries == true`, search $I_{\text{snap}}$ for a sentence boundary (first non-whitespace character after `. `, `? `, `! `). If multiple exist, pick the candidate minimizing $|s_{\text{candidate}} - s^*|$. If tied, pick the earlier index.
   - **Step 2: Word Boundary**: If no sentence boundary in $I_{\text{snap}}$, search for a whitespace character. Pick the candidate minimizing $|s_{\text{candidate}} - s^*|$. If tied, pick the earlier index.
   - **Step 3: Stride Fallback**: If no whitespace in $I_{\text{snap}}$, set $s_{k+1} = s^*$.
4. **Monotonicity & Overlap Bounds**:
   - $s_{k+1} > s_k$ is strictly enforced to prevent zero-progress loops.
   - The actual resulting overlap $\text{ActualOverlap} = e_k - s_{k+1}$ satisfies:
     $$0 \le \text{ActualOverlap} \le O + \Delta$$

### 3.3 Page Isolation Guarantee
Overlap is strictly **page-local**. For consecutive pages $P_j$ and $P_{j+1}$:
$$\operatorname{ChunkPage}(D, j+1, \dots) \cap \operatorname{Span}(P_j) = \emptyset$$
At the start of page $j+1$, the overlap buffer is empty, and Chunk 0 begins at index 0 (or first non-whitespace character).

---

## 4. Exact Offset & Trimming Architecture

To guarantee exact provenance equality without caveats:

### 4.1 Authoritative Trimming Invariant
1. Let $[i_{\text{start}}, i_{\text{end}})$ be the raw character range selected by the window packer in `NormalizedText`.
2. The chunker trims leading and trailing whitespace **before** finalizing offsets:
   - `StartCharOffset`: Index of the first character $i \ge i_{\text{start}}$ where `!char.IsWhiteSpace(NormalizedText[i])`.
   - `EndCharOffset`: Index immediately following the last character $j < i_{\text{end}}$ where `!char.IsWhiteSpace(NormalizedText[j])`.
3. `CharLength = EndCharOffset - StartCharOffset`.
4. `chunk.Text = NormalizedText.Substring(StartCharOffset, CharLength)`.

### 4.2 Mathematical Equality Invariant
For every chunk:
$$P.\operatorname{NormalizedText}.\operatorname{Substring}(C.\operatorname{StartCharOffset}, C.\operatorname{CharLength}) == C.\operatorname{Text}$$
This equality holds **100% unconditionally**.

---

## 5. Pure Source-Derived Text (Zero Synthetic Alteration)

`DocumentPassageChunk.Text` contains strictly verbatim substrings of `NormalizedText`:
- **NO Table Header Replication**: In oversized tables, continuation chunks slice strictly along row boundaries from source text. No synthetic table header rows are inserted into `Text`.
- **NO Code Language Tag Injection**: Fenced code continuation chunks do not synthesize artificial language tags.
- **NO Heading Prefix Injection**: Headings are never synthetically prepended to body paragraph text.
- Context enrichment for RAG/prompt assembly is explicitly deferred to downstream retrieval phases (Phases W3-D and W3-E).

---

## 6. Slicing Hierarchy & Oversized Unit Policy

```
[Target Chunk Budget Reached (350 code units)]
                    │
                    ▼
      1. Physical Page Boundary? ──────► HARD CUT (Never Cross Page)
                    │
                    ▼
      2. Structural Block Boundary? ───► Split at Block Boundary (\n\n, Code, Table)
                    │
                    ▼
      3. Sentence Boundary? ───────────► Split at Sentence (. ! ? + whitespace)
                    │                    (Guarded against abbreviations: "e.g.", "Dr.")
                    ▼
      4. Clause Punctuation? ──────────► Split at Clause (; : , —)
                    │
                    ▼
      5. Word Boundary? ───────────────► Split at Whitespace (' ')
                    │
                    ▼
      6. Hard Character Cut ───────────► Deterministic Cut (Pathological single tokens;
                                          Surrogate-pair safe; Ceiling: 2,000 code units)
```

---

## 7. Dependency Injection & Service Registration

The chunker is registered as a stateless singleton in `Axora-Desktop-WinUI\Axora.Desktop\App.xaml.cs`:
```csharp
services.AddSingleton<IPassageChunker, PassageChunker>();
```
`ScholarExtractionOrchestrator` receives `IPassageChunker?` via its constructor. When registered, the orchestrator automatically chunks each document page during extraction.

---

## 8. Algorithmic Complexity & Concurrency

### 8.1 Time Complexity: Strict $O(N)$
- Single-pass scanning over page `NormalizedText` of length $N$ UTF-16 code units.
- Zero regex catastrophic backtracking (finite-state or pre-compiled bounded lookaheads).
- Linear execution time across all input sizes.

### 8.2 Memory Footprint: $O(N)$
- Internal boundary analysis uses `ReadOnlySpan<char>` without intermediate string allocations.
- Emits newly allocated `string` instances only when constructing the final `DocumentPassageChunk.Text` property.

### 8.3 Thread Safety
- `PassageChunker` is completely stateless and thread-safe.
- Multiple threads can concurrently invoke `ChunkPage` across different pages and documents with zero synchronization contention.

---

## 9. Deterministic Bounded Fallback & Privacy

If an unhandled exception occurs during `ChunkPage`:
1. Caught at page loop boundary in `ScholarExtractionOrchestrator`.
2. Emits bounded fallback chunks of size $\le \text{TargetChunkSizeChars}$ (350 code units) covering `NormalizedText`, with valid offsets and lengths.
3. Records `ERR_CHUNKING_FAILED: {ex.GetType().Name}` in `PageNormalizationTelemetry` and `ExtractionReport`.
4. Raw exception messages, stack traces, and document secrets are strictly excluded.
5. `OperationCanceledException` propagates transparently without suppression.
