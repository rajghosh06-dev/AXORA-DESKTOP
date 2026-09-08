# Phase W3-C.7 Rule Matrix: Passage Chunking & Bounded Context Window Formulation

**Phase**: `W3-C.7 — Passage Chunking & Bounded Context Window Formulation`  
**Status**: `PLANNING ONLY / RECONCILED POST-AUDIT`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Rule Classification & Identifier Taxonomy

The Phase W3-C.7 rule matrix establishes the complete deterministic decision catalog governing passage chunk generation. Rules are categorized under seven functional classifications:

- **`RULE-C7-STR-xx`**: Structural Element Rules (Headings, Code, Tables, Lists, Quotes)
- **`RULE-C7-OVR-xx`**: Oversized Unit Slicing Rules (Paragraphs, Sentences, Code, Tables)
- **`RULE-C7-SNT-xx`**: Sentence Boundary & Abbreviation Disambiguation Rules
- **`RULE-C7-OVL-xx`**: Overlap, Stride & Snapping Rules
- **`RULE-C7-OFF-xx`**: Offset Tracking & Provenance Rules
- **`RULE-C7-SEC-xx`**: Security, Resource & Pathological Input Rules
- **`RULE-C7-ERR-xx`**: Degraded Mode & Error Recovery Rules

All length and offset budgets are measured in **UTF-16 code units** (`char`).

---

## 2. Structural Element Handling Rules (`RULE-C7-STR`)

| Rule ID | Rule Name | Target Construct | Trigger Condition | Deterministic Chunking Action | Boundary Behavior |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`RULE-C7-STR-01`** | **Heading Preservation** | Markdown ATX Heading (`# ` to `###### `) | Encounter line beginning with `#` | Heading is extracted as an atomic structural block. If heading length + subsequent block $\le 350$, combine into chunk. If subsequent block $> 350$, emit heading as standalone chunk. **No synthetic heading injection**. | Never splits across a heading marker or text. |
| **`RULE-C7-STR-02`** | **Fenced Code Block** | Code fences (` ``` ` or `~~~`) | Encounter opening code fence | The entire code block is treated as an atomic unit. If block length $\le 600$, emit as a single chunk. **Zero synthetic language tag injection**. | Code fences and internal indentation are 100% frozen. |
| **`RULE-C7-STR-03`** | **Markdown Table** | Tabular rows (`\| col \| col \|`) | Consecutive lines starting/ending with `\|` | Entire table is treated as an atomic unit if total table length $\le 600$. **Zero synthetic header replication in passage Text**. | Table delimiters and row integrity preserved. |
| **`RULE-C7-STR-04`** | **Ordered/Unordered List** | List items (`- `, `* `, `+ `, `1. `) | Consecutive list item lines | List items are packed together into chunks up to target budget (350 chars). | Splits strictly between list items (`\n- `); never splits mid-item unless item $> 600$. |
| **`RULE-C7-STR-05`** | **Blockquote** | Blockquote lines (`> `) | Consecutive lines beginning with `> ` | Blockquote is accumulated up to target budget (350 chars). Quote marker `> ` is preserved. | Splits between paragraphs or sentences within the quote. |
| **`RULE-C7-STR-06`** | **Mathematical Formula** | Display math (`$$...$$`) or inline (`$...$`) | Encounter `$$` or `$` delimiters | Formulas are kept intact within the surrounding sentence or paragraph chunk. | Math blocks are never sliced across delimiters unless formula $> 600$. |
| **`RULE-C7-STR-07`** | **Standard Paragraph** | Body text separated by `\n\n` | Consecutive non-empty lines | Paragraphs $\le 350$ are packed into current chunk. If accumulated chunk $+ \text{paragraph} > 350$, close current chunk and begin new chunk. | Splits strictly at `\n\n`. |

---

## 3. Oversized Unit Slicing Rules (`RULE-C7-OVR`)

When a single atomic construct exceeds the configured maximum ($M = 600$ code units) or target size ($T = 350$ code units):

| Rule ID | Rule Name | Target Construct | Trigger Condition | Deterministic Slicing Action | Fallback Strategy |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`RULE-C7-OVR-01`** | **Oversized Paragraph Slicing** | Single Body Paragraph | Paragraph length $> 350$ | Slice paragraph into sentences using `RULE-C7-SNT-01`. Accumulate sentences into chunks up to 350. | If a single sentence $> 350$, apply `RULE-C7-OVR-05`. |
| **`RULE-C7-OVR-02`** | **Oversized Code Slicing** | Fenced Code Block | Code block length $> 600$ | Slice code block strictly along newline boundaries (`\n`). Chunks contain verbatim code rows without synthetic header injection. | If a single code line $> 600$, split at column 600. |
| **`RULE-C7-OVR-03`** | **Oversized Table Slicing** | Markdown Table | Table length $> 600$ | Slice table strictly along row boundaries (`\n`). **NO synthetic table headers replicated in Text**. Each chunk contains verbatim table rows from its source span. | If a single table row $> 600$, split at cell pipe boundary (`\|`). |
| **`RULE-C7-OVR-04`** | **Oversized List Slicing** | List Block | List block length $> 600$ | Slice list along list item boundaries (`\n- `, `\n1. `). Pack whole items up to 350. | If a single list item $> 600$, slice along sentence boundaries. |
| **`RULE-C7-OVR-05`** | **Oversized Sentence Slicing** | Single Sentence | Single sentence length $> 350$ | Slice sentence along clause punctuation in order of preference: `; `, `: `, `, `, ` — `. | If no clause punctuation exists, apply `RULE-C7-OVR-06`. |
| **`RULE-C7-OVR-06`** | **Continuous Word / Unpunctuated Text** | Unpunctuated text or long sentence | Clause length $> 350$ | Slice along whitespace word boundaries (`' '`). | If a single word/token $> 600$, apply `RULE-C7-OVR-07`. |
| **`RULE-C7-OVR-07`** | **Pathological Unbroken Token** | Single unbroken string (e.g. Base64, DNA, Hex) | Single word length $> 600$ | Slice at index 600. Must not exceed absolute safety ceiling of 2,000 code units under any circumstance. | Multi-byte UTF-16 surrogate pairs kept intact (`RULE-C7-SEC-06`). |

---

## 4. Sentence Boundary & Abbreviation Disambiguation Rules (`RULE-C7-SNT`)

| Rule ID | Rule Name | Pattern / Target | Condition | Action |
| :--- | :--- | :--- | :--- | :--- |
| **`RULE-C7-SNT-01`** | **Standard Sentence Terminator** | `[.!?]` followed by whitespace and uppercase letter or EOF | End of sentence detected | Mark valid sentence boundary for slicing. |
| **`RULE-C7-SNT-02`** | **Title Abbreviation Guard** | `Mr.`, `Mrs.`, `Ms.`, `Dr.`, `Prof.`, `Rev.`, `Sr.`, `Jr.` | Preceding period matches title token | **DO NOT SPLIT**. Suppress sentence break. |
| **`RULE-C7-SNT-03`** | **Academic Latin Guard** | `e.g.`, `i.e.`, `et al.`, `cf.`, `vs.`, `ibid.`, `op. cit.` | Preceding period matches Latin token | **DO NOT SPLIT**. Suppress sentence break. |
| **`RULE-C7-SNT-04`** | **Publication Notation Guard** | `Fig.`, `Figs.`, `Tab.`, `Vol.`, `No.`, `pp.`, `p.`, `Eq.`, `Ref.` | Preceding period matches citation token | **DO NOT SPLIT**. Suppress sentence break. |
| **`RULE-C7-SNT-05`** | **Numerical / Decimal Guard** | Digit `.` Digit (e.g. `3.14159`, `$12.50`, `v1.0.4`) | Period surrounded by digits | **DO NOT SPLIT**. Suppress sentence break. |
| **`RULE-C7-SNT-06`** | **Quotation / Bracket Cloture** | `."`, `.'`, `.)`, `.]`, `!"`, `?"` followed by space | Punctuation enclosed by quotes/parens | Mark boundary immediately **after** the closing quote/parenthesis. |

---

## 5. Overlap, Stride & Snapping Rules (`RULE-C7-OVL`)

| Rule ID | Rule Name | Parameter / State | Action | Boundary Behavior |
| :--- | :--- | :--- | :--- | :--- |
| **`RULE-C7-OVL-01`** | **Zero Overlap Mode** | `StrideOverlapChars == 0` | Chunks form a strictly disjoint partition of page substantive text ($s_{k+1} \ge e_k$). | Zero overlapping characters. |
| **`RULE-C7-OVL-02`** | **Sentence Snap Window** | `SnapToSentenceBoundaries == true` | Search interval $[s^* - \Delta, s^* + \Delta]$ for sentence start. If multiple exist, choose candidate closest to $s^*$ (tie-break: earlier index). | Overlapping chunk starts with a complete sentence. |
| **`RULE-C7-OVL-03`** | **Word Snap Fallback** | Sentence boundary not found in snap window | Search interval $[s^* - \Delta, s^* + \Delta]$ for whitespace boundary. Choose candidate closest to $s^*$ (tie-break: earlier index). | Overlapping chunk does not cut a word in half. |
| **`RULE-C7-OVL-04`** | **Hard Stride Fallback** | Whitespace not found in snap window | Set start exactly to character offset $s^*$. | Extreme fallback for unbroken text. |
| **`RULE-C7-OVL-05`** | **Page Boundary Overlap Reset** | Crossing page boundary ($P_j \to P_{j+1}$) | **Zero Overlap across pages**. Overlap buffer reset to empty at start of each page. | Chunk 0 of Page $j+1$ begins at index 0 of Page $j+1$ text. |

---

## 6. Offset Tracking & Provenance Rules (`RULE-C7-OFF`)

| Rule ID | Rule Name | Target Property | Calculation Rule | Integrity Check |
| :--- | :--- | :--- | :--- | :--- |
| **`RULE-C7-OFF-01`** | **StartCharOffset** | `chunk.StartCharOffset` | Index of the first non-whitespace character of the passage in page `NormalizedText`. | Must be $\ge 0$ and $< \operatorname{Length}(\operatorname{NormalizedText})$. |
| **`RULE-C7-OFF-02`** | **EndCharOffset** | `chunk.EndCharOffset` | Index immediately following the last non-whitespace character of the passage in `NormalizedText`. | Must be $> \text{StartCharOffset}$ and $\le \operatorname{Length}(\operatorname{NormalizedText})$. |
| **`RULE-C7-OFF-03`** | **CharLength** | `chunk.CharLength` | $\text{EndCharOffset} - \text{StartCharOffset}$. | Must strictly equal `chunk.Text.Length`. |
| **`RULE-C7-OFF-04`** | **Unconditional Substring Invariant** | `chunk.Text` | Must exactly equal `NormalizedText.Substring(StartCharOffset, CharLength)`. | **Unconditional**: zero exceptions. |
| **`RULE-C7-OFF-05`** | **Sequence Numbering** | `chunk.ChunkId`, `chunk.ChunkIndex` | `ChunkId`: Monotonically increasing across document ($0, 1, 2 \dots$). `ChunkIndex`: Monotonically increasing within page ($0, 1, 2 \dots$). | Deterministic ordering preserved across runs. |

---

## 7. Security, Resource & Pathological Input Rules (`RULE-C7-SEC`)

| Rule ID | Rule Name | Condition / Limit | Enforced Action | Error / Warning |
| :--- | :--- | :--- | :--- | :--- |
| **`RULE-C7-SEC-01`** | **Max Passages Per Page** | Generated chunks for a single page $> 1,000$ | Halt chunk generation for current page; truncate at 1,000 chunks. | Append diagnostic warning `WARN_CHUNKING_PAGE_LIMIT_REACHED`. |
| **`RULE-C7-SEC-02`** | **Max Passages Per Document** | Total document chunks $> 50,000$ | Halt chunk generation for remaining pages; retain generated chunks. | Append diagnostic warning `WARN_CHUNKING_DOC_LIMIT_REACHED`. |
| **`RULE-C7-SEC-03`** | **Absolute Safety Ceiling** | Single chunk length $> 2,000$ code units | Enforce hard character cut at 2,000 code units. Under no configuration may a chunk exceed 2,000 code units. | Defensive ceiling. |
| **`RULE-C7-SEC-04`** | **Invalid Overlap Budget** | `StrideOverlapChars >= TargetChunkSizeChars` | Throw `ArgumentOutOfRangeException` during `ChunkingOptions.Validate()`. | Fast-fail before chunking starts. |
| **`RULE-C7-SEC-05`** | **Whitespace-Only / Empty Page** | `string.IsNullOrWhiteSpace(pageText)` | Return empty list `[]` (0 chunks). | Zero errors; clean short-circuit. |
| **`RULE-C7-SEC-06`** | **Unicode Surrogate Protection** | Splitting at surrogate pair | If split index falls between high and low surrogates, adjust split index by $+1$ to keep pair together. | Prevents corrupted `\uFFFD` replacement characters. |

---

## 8. Degraded Mode & Error Recovery Rules (`RULE-C7-ERR`)

| Rule ID | Rule Name | Failure Scenario | Recovery Behavior | State Impact |
| :--- | :--- | :--- | :--- | :--- |
| **`RULE-C7-ERR-01`** | **Unhandled Chunker Exception** | Exception thrown during `ChunkPage` | Catch exception at page boundary in `ScholarExtractionOrchestrator`. Create bounded fallback chunks of size $\le 350$ code units covering `NormalizedText`, with valid offsets and lengths. | `RawText` and `NormalizedText` are 100% preserved. Document extraction does not abort. |
| **`RULE-C7-ERR-02`** | **Telemetry Warning on Fallback** | Chunker fallback triggered | Populate `ExtractionReport.Warnings` and `PageNormalizationTelemetry.DiagnosticWarning` with `ERR_CHUNKING_FAILED: {ex.GetType().Name}`. | Raw exception messages and secrets are strictly excluded. |
| **`RULE-C7-ERR-03`** | **Cancellation Propagation** | `OperationCanceledException` encountered | Rethrow immediately (`throw;`). Do NOT catch or convert cancellation to a fallback chunk. | Document ingestion cleanly cancels. |
