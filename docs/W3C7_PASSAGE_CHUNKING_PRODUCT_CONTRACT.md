# Phase W3-C.7 Product Contract: Passage Chunking & Bounded Context Window Formulation

**Phase**: `W3-C.7 — Passage Chunking & Bounded Context Window Formulation`  
**Status**: `PLANNING ONLY / RECONCILED POST-AUDIT`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  
**Downstream Dependents**: `Phase W3-D (Local Vector Embedding & Hybrid Search)`, `Phase W3-E (Context Assembly & Synthesis)`  

---

## 1. Purpose & Scope

Phase **W3-C.7** establishes the deterministic, source-aware passage chunking and bounded context window formulation engine for the AXORA Scholar Kit document ingestion pipeline.

Following the completion of Phase W3-C.6 (Text Normalization & Diagnostic Telemetry), the document ingestion pipeline maintains two distinct tiers of text per page:
1. `RawText`: The verbatim, immutable ground truth extracted from physical media or document streams.
2. `NormalizedText`: The canonical, clean, and structurally coherent text representation.

Phase **W3-C.7** is the strict and exclusive consumer of `NormalizedText`. Its single architectural responsibility is to slice normalized page text into bounded, semantically coherent, and traceable passages (`DocumentPassageChunk`) without crossing physical page boundaries, losing author content, or injecting synthetic modifications into passage text.

```
┌───────────────────────────┐
│ Extractor Engines (C.2–5) │
└─────────────┬─────────────┘
              ▼
        [ RawText ] (Immutable Ground Truth)
              │
              ▼
┌───────────────────────────┐
│   TextNormalizer (C.6)    │
└─────────────┬─────────────┘
              ▼
     [ NormalizedText ] (Canonical Structural Text)
              │
              ▼
┌───────────────────────────┐
│   IPassageChunker (C.7)   │  ◄── PHASE W3-C.7 BOUNDARY
└─────────────┬─────────────┘
              ▼
 ┌─────────────────────────┐
 │  DocumentPassageChunk[] │ (Deterministic Bounded Passages)
 └─────────────┬───────────┘
              ▼
┌───────────────────────────┐
│ Future Indexing & Search  │ (Phase W3-D Vector Embeddings & Hybrid Search)
└───────────────────────────┘
```

### 1.1 In-Scope Capabilities
- Slicing `DocumentPage.NormalizedText` into discrete, bounded `DocumentPassageChunk` instances.
- Structural boundary awareness: respecting paragraphs (`\n\n`), headings (`#`), lists (`-`, `1.`), code blocks (```), tables (`|`), and blockquotes (`>`).
- Sentence boundary detection with robust academic abbreviation protection (e.g., `e.g.`, `i.e.`, `et al.`, `Dr.`, `Fig.`, `p.`, `3.14`).
- Configurable target chunk size (`TargetChunkSizeChars`), stride overlap (`StrideOverlapChars`), and sentence snap window (`SentenceSnapBoundaryDelta`), measured in UTF-16 code units.
- Deterministic fallback slicing for oversized structural units (long paragraphs, long sentences, long tables, unpunctuated text).
- Strict page-bounded containment: passages never span across physical or logical page boundaries.
- Precise provenance tracking: `DocumentId`, `PageNumber`, `ChunkIndex`, `StartCharOffset`, and `EndCharOffset` relative to `NormalizedText`.
- Fully local, offline-capable, thread-safe, and zero-dependency managed C# execution.

### 1.2 Explicitly Out-of-Scope Capabilities
- **No Modification of RawText**: `RawText` remains 100% immutable; C7 never touches, alters, or falls back to mutating raw extraction ground truth.
- **No Re-Normalization**: C7 assumes `NormalizedText` is already canonical; C7 does not perform Unicode NFKC, ligature unfolding, hyphen repair, or character stripping.
- **No Synthetic Context Injection**: `DocumentPassageChunk.Text` contains strictly source-derived characters. No synthetic table headers, language tags, or section prefixes may be inserted into `Text`.
- **No Vector Embeddings / ML Models**: C7 generates textual passages with `Embedding = null` and `EmbeddingStatus = NoEmbedding`. Embedding computation belongs strictly to Phase W3-D.
- **No External LLM Tokenizer Service**: C7 operates in UTF-16 code-unit budgets. It does not require Python, TikToken, HuggingFace, or ONNX tokenizer runtimes.
- **No Cross-Page Passage Merging**: Academic documents require exact physical page traceability; fusing text across page boundaries is strictly prohibited.

---

## 2. Core Architectural Invariants

### Invariant 1: Immutability of Ground Truth RawText
`RawText` is the unmutated forensic record of the underlying document. Under no circumstances may `IPassageChunker`, options, or fallback algorithms overwrite, truncate, or mutate `RawText`.

### Invariant 2: Pure Downstream Consumer of NormalizedText
C7 processes `NormalizedText` as produced by `TextNormalizer`. If `NormalizedText` is null, empty, or whitespace-only, C7 outputs zero chunks without throwing exceptions or corrupting document state.

### Invariant 3: Deterministic Passage Generation
Within the supported WinUI 3/.NET 9 (x64) Windows runtime, for any identical pair of `NormalizedText` string and `ChunkingOptions`:
$$\operatorname{ChunkPage}(D, P, T, O) \equiv \operatorname{ChunkPage}(D, P, T, O)$$
Every chunk text, sequence index, and character offset is bit-for-bit identical across executions, independent of thread scheduling or concurrent invocation order.

### Invariant 4: Strict Page-Bounded Containment (Zero Cross-Page Chunks)
Academic research demands exact page citations (e.g. "Smith et al. (2024), p. 12, Passage 3"). Every chunk belongs to exactly one `PageNumber`. Passages **MUST NEVER** combine text from multiple pages, even if a paragraph spans across a visual page break.

### Invariant 5: Exact Substring Provenance Equality
For every passage chunk $C$ emitted from page $P$:
$$P.\operatorname{NormalizedText}.\operatorname{Substring}(C.\operatorname{StartCharOffset}, C.\operatorname{CharLength}) == C.\operatorname{Text}$$
`StartCharOffset` points to the index of the first character of `chunk.Text` in `NormalizedText`. `EndCharOffset` is the exclusive end index (`StartCharOffset + CharLength`). This invariant holds **unconditionally with zero exceptions**.

### Invariant 6: Lossless Substantive Content Coverage
Within a single page, no substantive text may be dropped. Let $S_{\text{sub}}$ be the set of character indices in `NormalizedText` that are not inter-block separator whitespace. Then:
$$S_{\text{sub}} \subseteq \bigcup_{k} [C_k.\operatorname{StartCharOffset}, C_k.\operatorname{EndCharOffset})$$
Every substantive character is represented in at least one chunk.

### Invariant 7: Pure Source-Derived Text (Zero Synthetic Alteration)
`DocumentPassageChunk.Text` must be a direct, unadulterated substring of `NormalizedText`. No synthetic table headers, language tags, or artificial context may be prepended or appended to `Text`.

### Invariant 8: Strict Local & Offline Execution
Chunking executes entirely within managed .NET 9 BCL code. Zero network sockets, zero cloud dependencies, zero external process executions, and zero disk writes during chunk calculation.

---

## 3. What is a "Passage" in AXORA?

In AXORA Scholar Kit, a **Passage** (`DocumentPassageChunk`) is defined as:
> *A self-contained, bounded, and continuous segment of normalized academic text, bounded within a single physical page, carrying exact character offsets and physical page provenance.*

A passage is neither a raw visual line nor an entire section. It is the atomic payload for:
1. **Full-Text Keyword Search (FTS / BM25)**: Granular match highlighting without returning overwhelming pages.
2. **Dense Vector Embeddings (Phase W3-D)**: Optimal context sizes for small local embedding models (e.g., 256–512 token context windows).
3. **Synthesis & Retrieval-Augmented Generation (Phase W3-E)**: Context window formulation where multiple relevant passages from across a document can be packed into a compact LLM prompt budget.
4. **User Citation & Evidence Navigation**: Direct UI navigation to the exact page and passage in the Scholar document reader.

---

## 4. Length Budgeting & Three-Tier Size Semantics

AXORA budgets passages in **UTF-16 code units** (`char`), matching .NET string indexing and substring operations.

### 4.1 Three-Tier Size Hierarchy

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Three-Tier Chunk Size Hierarchy                      │
│                                                                         │
│  [TargetChunkSizeChars: 350] ◄── Tier 1: Normal Target Accumulation     │
│  Accumulate sentences and blocks up to 350 UTF-16 code units.           │
│                                                                         │
│  [MaxChunkSizeChars: 600]    ◄── Tier 2: Configured Maximum Ceiling     │
│  Threshold above which atomic constructs are sliced as oversized.       │
│  Normal passages do not exceed 600 UTF-16 code units.                   │
│                                                                         │
│  [AbsoluteMax: 2000]         ◄── Tier 3: Absolute Safety Ceiling        │
│  Hard system ceiling. No chunk exceeds 2,000 UTF-16 code units under   │
│  any circumstance. Pathological unbroken tokens are hard-sliced here.   │
└─────────────────────────────────────────────────────────────────────────┘
```

1. **Tier 1: Normal Target (`TargetChunkSizeChars = 350`)**:
   - The primary budget target for greedy accumulation.
   - Sentences and small blocks are accumulated as long as total length $\le 350$ UTF-16 code units.
2. **Tier 2: Configured Maximum (`MaxChunkSizeChars = 600`)**:
   - The threshold above which a structural unit (paragraph, table, code block, list) is deemed **oversized** and forced into sub-unit slicing.
   - Standard generated passages do not exceed 600 UTF-16 code units.
3. **Tier 3: Absolute Defensive Safety Ceiling (`AbsoluteMaxChunkSizeChars = 2000`)**:
   - The inviolable system ceiling protecting against memory exhaustion or pathological text.
   - If an unbroken token (e.g. continuous Base64, raw binary strings, DNA sequence) exceeds 2,000 code units without whitespace or punctuation, it is forcefully cut at index 2,000 (surrogate-pair safe).

### 4.2 Empirical Token Equivalence
In English and Western academic prose, 1 LLM token corresponds empirically to approximately $3.8 \dots 4.2$ UTF-16 code units:
- **350 UTF-16 code units** $\approx 80 \dots 90$ tokens (ideal for local small embedding models).
- **600 UTF-16 code units** $\approx 140 \dots 150$ tokens (well within the standard 512-token embedding ceiling).
- **50 UTF-16 code units** $\approx 10 \dots 12$ tokens (minimum useful passage size).

---

## 5. Structural Region Policies

C7 applies a structured hierarchy to protect document semantics while strictly preserving source substring fidelity:

| Structural Region | Detection Pattern | Chunking Policy | Oversized Policy ($> 600$ chars) |
| :--- | :--- | :--- | :--- |
| **Standard Paragraph** | Blocks separated by `\n\n` | Preferred atomic unit. Multiple short paragraphs are packed up to target size (350 chars). | Sliced along sentence boundaries (`. `, `? `, `! `), then word boundaries. |
| **Section Heading** | Lines starting with `# `, `## `, `### `, etc. | Context anchor. Kept with the subsequent body block if combined size $\le 350$; standalone if following block exceeds budget. | Sliced on word boundaries if single heading $> 600$ chars. |
| **Fenced Code Block** | Blocks enclosed by ` ``` ` or `~~~` | Protected block. Fences and internal line breaks preserved intact. | Sliced along line boundaries (`\n`); never splits inside a single line unless line $> 600$ chars. Zero synthetic language tags added. |
| **Table** | Blocks with pipe delimiters (`\|`) | Protected block. Rows preserved intact up to 600 chars. | Sliced strictly along row boundaries (`\n`). **NO synthetic table headers replicated in Text**. |
| **List** | Consecutive lines starting with `- `, `* `, `1. ` | Protected list block. Consecutive list items kept together up to target size. | Sliced along item boundaries (`\n- `, `\n1. `); never splits inside an item unless item $> 600$ chars. |
| **Blockquote** | Lines starting with `> ` | Protected quote block. Preserves quote marker `> `. | Sliced along sentence or line boundaries. |
| **Math / Formulas** | LaTeX blocks (`$...$`, `$$...$$`) | Protected inline or display math. Never split across math delimiters. | Kept as atomic chunk if possible; hard sliced only if formula alone $> 600$ chars. |

---

## 6. Slicing Hierarchy & Boundary Preferences

When text exceeds the target chunk size, C7 resolves boundaries using a strict preference hierarchy:

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
                                          Surrogate-pair safe)
```

---

## 7. Overlap Policy & Stride Semantics

Overlap ensures that semantic concepts spanning chunk boundaries are not lost during vector retrieval.

### 7.1 Stride Formulation & Acceptance Bounds
Let $T = \text{TargetChunkSizeChars} = 350$, $O = \text{StrideOverlapChars} = 60$, $\Delta = \text{SentenceSnapBoundaryDelta} = 40$.
1. **Nominal Overlap Start**:
   $$s^* = e_k - O$$
2. **Snapping Search Window**:
   $$I_{\text{snap}} = [s^* - \Delta, \quad s^* + \Delta]$$
3. **Boundary Snapping Hierarchy**:
   - **Sentence Snapping (Priority 1)**: If `SnapToSentenceBoundaries == true`, search $I_{\text{snap}}$ for a sentence start (first non-whitespace character after `. `, `? `, `! `). If multiple exist, pick the candidate closest to $s^*$ (tie-break: earlier index).
   - **Word Snapping (Priority 2)**: If no sentence start in $I_{\text{snap}}$, search for a whitespace boundary. Pick the candidate closest to $s^*$ (tie-break: earlier index).
   - **Exact Stride (Priority 3)**: If no whitespace in $I_{\text{snap}}$, set $s_{k+1} = s^*$.
4. **Monotonicity & Overlap Bounds**:
   - To prevent infinite loops, $s_{k+1} > s_k$ is strictly enforced.
   - The actual resulting overlap $\text{ActualOverlap} = e_k - s_{k+1}$ satisfies:
     $$0 \le \text{ActualOverlap} \le O + \Delta$$

### 7.2 Page Boundary Overlap Invariant
- **CRITICAL**: Overlap is strictly **PAGE-LOCAL**.
- Chunk 0 of Page $P+1$ **MUST NEVER** overlap with the final chunk of Page $P$.
- Page boundaries reset the overlap buffer completely.

---

## 8. Provenance & Coordinate Contract

Each `DocumentPassageChunk` carries forensic provenance connecting it back to `NormalizedText`:

```csharp
public sealed class DocumentPassageChunk
{
    public int ChunkId { get; set; }             // Document-wide unique sequence (0, 1, 2, ...)
    public string DocumentId { get; set; }        // Parent ScholarDocument ID (GUID)
    public int PageNumber { get; set; }           // Physical 1-based page number
    public int ChunkIndex { get; set; }          // Page-local sequence index (0, 1, 2, ...)
    public string Text { get; set; }              // Pure source-derived passage content
    public int StartCharOffset { get; set; }     // 0-based start index in NormalizedText
    public int EndCharOffset { get; set; }       // 0-based exclusive end index in NormalizedText
    public int CharLength { get; set; }          // UTF-16 code units (EndCharOffset - StartCharOffset)
    public PassageEmbeddingStatus EmbeddingStatus { get; set; } // NoEmbedding (Phase W3-C.7)
    public float[]? Embedding { get; set; }       // null in Phase W3-C.7
}
```

### Invariant Equations:
1. `CharLength == EndCharOffset - StartCharOffset`
2. `CharLength == Text.Length`
3. `NormalizedText.Substring(StartCharOffset, CharLength) == Text`

---

## 9. Resource Bounds & Algorithmic Complexity

To protect against denial-of-service or memory exhaustion from adversarial or malformed files, the following constraints are enforced:

| Constraint | Limit | Violation Action |
| :--- | :--- | :--- |
| **Max Passages Per Page** | 1,000 | Halts page chunking; logs diagnostic warning; retains existing chunks. |
| **Max Passages Per Document** | 50,000 | Halts document chunking; marks report partial; preserves valid passages. |
| **Absolute Max Chunk Ceiling** | 2,000 code units | Hard cut enforced; passage cannot exceed 2,000 code units under any circumstance. |
| **Invalid Overlap Budget** | `StrideOverlapChars >= TargetChunkSizeChars` | Throws `ArgumentOutOfRangeException` during `ChunkingOptions.Validate()`. |
| **Empty / Whitespace-Only Page** | `string.IsNullOrWhiteSpace(pageText)` | Returns 0 chunks cleanly. |
| **Unicode Surrogate Protection** | Splitting at surrogate pair | Adjusts split index by $+1$ to keep high and low surrogates together. |
| **Algorithmic Complexity** | Strict $O(N)$ linear time | Single-pass scan over page text of length $N$; zero catastrophic regex backtracking. |

---

## 10. Degraded & Error Handling

If an unexpected exception occurs while chunking a page:
1. **No Application Crash**: The exception is caught at the page boundary in `ScholarExtractionOrchestrator`.
2. **Deterministic Bounded Fallback**: The chunker emits bounded fallback chunks of size $\le \text{TargetChunkSizeChars}$ (350 chars) covering `NormalizedText`, with valid offsets and lengths. Under no circumstance is unbounded text emitted as a single chunk.
3. **Diagnostic Telemetry**: Records `ERR_CHUNKING_FAILED: {ExceptionType}` in `PageNormalizationTelemetry.DiagnosticWarning` and `ExtractionReport.Warnings`. Raw exception messages, stack traces, and document secrets are strictly excluded.
4. **Cancellation Safety**: `OperationCanceledException` is rethrown immediately and never swallowed.
5. **Zero Mutation**: `RawText` and `NormalizedText` remain 100% intact and valid.
