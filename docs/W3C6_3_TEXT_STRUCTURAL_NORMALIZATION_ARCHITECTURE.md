# Phase W3-C.6.3 Architecture Specification: Source-Aware Paragraph Assembly & Structural Text Normalization

**Phase**: `W3-C.6.3 — Source-Aware Paragraph Assembly, Line-Wrap Rejoining & DOCX Markdown Token Reconciliation`  
**Status**: `PLANNING ONLY / REVISED SPECIFICATION`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. System Context & Architectural Position

`ITextNormalizer` operates directly between format-specific extraction engines (`IDocumentExtractorEngine`) and downstream passage chunking (`IPassageChunker` in Phase W3-C.7):

```
┌────────────────────────────────────────────────────────────────────────┐
│                   PHASE W3-C EXTRACTION PIPELINE                       │
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│   [ IDocumentExtractorEngine ] (PDF, DOCX, OCR, HTML, TXT, CSV)       │
│                  │                                                     │
│                  ▼                                                     │
│   [ ExtractedPageRaw ] (RawText ground truth, NormalizedText = null)   │
│                  │                                                     │
│                  ▼                                                     │
│ ══════════════════════════════════════════════════════════════════════ │
│   [ PHASE W3-C.6: ITextNormalizer / TextNormalizer ]                   │
│                                                                        │
│   Input: ExtractedPageRaw.RawText                                      │
│                                                                        │
│   ┌────────────────────────────────────────────────────────────────┐   │
│   │ PHASE W3-C.6.2 (Character-Level Foundation):                   │   │
│   │   ├── Stage 1: Fast Sanitization (BOM, Zero-Width, C0 Controls)│   │
│   │   ├── Stage 2: Line Ending Canonicalization (CRLF/CR -> LF)    │   │
│   │   └── Stage 3: Glyphs, Ligatures & NBSP (ﬁ -> fi, \u00A0 -> ' ') │
│   └────────────────────────────────────────────────────────────────┘   │
│                                  │                                     │
│                                  ▼                                     │
│   ┌────────────────────────────────────────────────────────────────┐   │
│   │ PHASE W3-C.6.3 (Structural Text Normalization):                │   │
│   │   ├── Stage 4: Protected Region Classification                 │   │
│   │   │     • Fenced Code Regions (``` / ~~~) -> No rewriting     │   │
│   │   │     • Tables (|...|) -> Structure preserved                │   │
│   │   │     • Headings & List Items -> Tagged for padding/compact  │   │
│   │   │     • DOCX Extractor Scaffolding Reconciler                │   │
│   │   └── Stage 5: Paragraph Assembly & Line Joining               │   │
│   │         • Evidence-Based Line Classification (No short-line cut)│   │
│   │         • PDF Soft-Line Wrap Rejoining                         │   │
│   │         • Restricted OCR Punctuation Spacing Cleanup           │   │
│   │         • Conservative Hyphenation Repair (when enabled)       │   │
│   │         • Canonical \n\n Boundaries with Ambiguity Preservation │   │
│   └────────────────────────────────────────────────────────────────┘   │
│                                  │                                     │
│                                  ▼                                     │
│   Output: ExtractedPageRaw with populated NormalizedText               │
│ ══════════════════════════════════════════════════════════════════════ │
│                  │                                                     │
│                  ▼                                                     │
│   [ PHASE W3-C.7: IPassageChunker ] (350-Char Target Passage Chunks)   │
│                  │                                                     │
│                  ▼                                                     │
│   [ IScholarLibraryService ] (Durable JSON Persistence in %APPDATA%)   │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Protected Region Classification Model (Stage 4)

Before applying any text rejoining or paragraph assembly, Stage 4 classifies lines into structural regions. This prevents structural code, tables, headings, and lists from being corrupted by flowing text joining:

```
[ Stage 3 Cleaned Text ]
            │
            ▼
[ Line-by-Line Structural Classifier ]
            │
            ├── State: Inside Fenced Code Block? ────► [ ProtectedCodeRegion ]
            │                                           (No structural rewriting)
            │
            ├── Line matches Table Row Syntax?   ────► [ ProtectedTableRegion ]
            │                                           (Row breaks preserved)
            │
            ├── Line matches Structural Heading? ────► [ ProtectedHeadingRegion ]
            │                                           (Bound by \n\n)
            │
            ├── Line matches List Item Syntax?   ────► [ ProtectedListRegion ]
            │                                           (Compacted to \n between items)
            │
            ├── Line matches Blockquote Syntax?  ────► [ ProtectedBlockquoteRegion ]
            │                                           (Quote prefix preserved)
            │
            └── Default Line                     ────► [ FlowingBodyText ]
                                                        (Eligible for format-specific joining)
```

### 2.1 Protected Code Region Detection & Scope
- **Fences Detected**: Lines whose trimmed content begins with at least three consecutive backticks (```` ``` ````) or tildes (`~~~`).
- **State Machine**:
  - `bool inFencedCode = false;`
  - Opening fence toggles `inFencedCode = true`. An optional language identifier (e.g. ```` ```csharp ````) is retained.
  - Closing fence toggles `inFencedCode = false`.
  - **Unclosed Fences**: If an opening fence is not closed before the end of the page, the protected state holds until the end of the current page. The state does **not** cross physical page boundaries.
- **Invariant**:
  - `RawText` remains the unmutated ground truth.
  - C6.3 performs **no structural rewriting** inside recognized protected code regions:
    - Zero soft-line wrap rejoining.
    - Zero indentation or consecutive space collapsing.
    - Zero line-break hyphen repair.
    - Zero OCR punctuation spacing modification.
  - Note: Character-level sanitization from C6.2 (BOM stripping, line endings standardized to LF, control character removal) was applied in earlier stages; C6.3 freezes structural formatting.

### 2.2 Table Region Protection
- A line is classified as a table row if it contains pipe delimiters (`|`) consistent with Markdown table formatting (e.g. starting and ending with `|` or containing `| --- |`).
- Table rows are excluded from generic soft-line wrap rejoining. Consecutive table rows are preserved as single lines separated by `\n`.
- The entire table block is bounded by `\n\n`.

### 2.3 Headings & Blockquotes
- Headings (`# `, `## `, `### `, etc.) are assigned to `ProtectedHeadingRegion`. They are never merged into preceding or following body lines, and are padded with `\n\n`.
- Lines starting with `> ` are assigned to `ProtectedBlockquoteRegion` and kept distinct from regular body paragraphs.

---

## 3. Paragraph Assembly & Line Joining Engine (Stage 5)

### 3.1 Conservative Evidence-Based Line Classification Policy
The simplistic rule (*"line length < 40 chars = paragraph boundary"*) is **REMOVED**. Line length alone must never serve as sufficient evidence of a paragraph boundary, because valid lines in two-column PDFs, captions, verses, addresses, and short sentences frequently contain fewer than 40 characters without terminating a paragraph.

Instead, paragraph boundary decisions require positive structural evidence:

```
                  ┌────────────────────────────────────────┐
                  │ Evaluate Pair: (Line L_i, Line L_{i+1})│
                  └───────────────────┬────────────────────┘
                                      │
               Is either line in a Protected Region?
                 (Code, Table, Heading, List)
                         ├── YES ──► PRESERVE BLOCK BOUNDARY (\n or \n\n)
                         └── NO
                                      │
                 Was there an explicit blank line?
                             (\n\s*\n)
                         ├── YES ──► CANONICAL PARAGRAPH BREAK (\n\n)
                         └── NO
                                      │
            Does L_{i+1} have paragraph indentation?
                  (>= 2 spaces or tab) AND
               L_i ends with terminal punctuation?
                         ├── YES ──► CANONICAL PARAGRAPH BREAK (\n\n)
                         └── NO
                                      │
                  Does L_i end with hyphen ('-')?
                         ├── YES ──► HYPHENATION DECISION TABLE
                         └── NO
                                      │
            Does L_i lack terminal punctuation? (., !, ?)
                         ├── YES ──► SOFT-WRAP JOIN (' ')
                         └── NO
                                      │
                 Does L_{i+1} begin with lowercase?
                   (e.g. after abbreviation "et al.")
                         ├── YES ──► SOFT-WRAP JOIN (' ')
                         └── NO
                                      │
                        [ AMBIGUOUS CASE ]
               (Terminal punct, uppercase next, no blank,
                       no indent, no block marker)
                                      │
                                      ▼
                        PRESERVE ORIGINAL NEWLINE (\n)
                      (Never force an unevidenced join
                       or an unevidenced \n\n break)
```

### 3.2 Evidence-Based Principles
1. **Positive Evidence for Paragraph Break (`\n\n`)**:
   - An explicit blank line in the source (`\n\s*\n`).
   - Transition to or from a protected structural block (heading, list, table, code).
   - Paragraph indentation ($\ge 2$ spaces or `\t`) at the start of $L_{i+1}$ following terminal punctuation on $L_i$.
2. **Positive Evidence for Soft-Wrap Continuation (`' '`)**:
   - $L_i$ does not end with terminal punctuation (`.`, `!`, `?`, `:`, `;`), indicating mid-clause continuation.
   - $L_i$ ends with a period, but $L_{i+1}$ begins with a lowercase letter (indicating an abbreviation like `et al.`, `i.e.`, `Fig.`, `e.g.`).
3. **Ambiguity Preservation Rule**:
   - If $L_i$ ends with terminal punctuation and $L_{i+1}$ begins with an uppercase letter, but there is NO blank line and NO indentation:
   - This may represent a new sentence in the same paragraph OR a new unindented paragraph.
   - **Resolution**: Preserve the newline (`\n`). Do not force a soft-join (`' '`) and do not synthesize a double newline (`\n\n`). This ensures that neither text meaning nor layout structure is corrupted.

---

## 4. DOCX Structural Reconciliation Architecture

`DocxDocumentExtractorEngine` (Phase W3-C.4) emits structural Markdown markers based on OpenXML elements (`w:pStyle`, `w:numPr`, `w:tbl`). It appends `\n\n` to every extracted paragraph.

### 4.1 Distinguishing Extractor Scaffolding from Author Markdown
1. **Extractor Headings**:
   - Emitted from OpenXML heading styles (`heading1`..`heading6`, `title`, `subtitle`) or `outlineLvl`.
   - Consistently appear at paragraph start with `#+ ` and are followed by `\n\n`.
2. **Extractor List Items**:
   - Emitted from OpenXML `w:numPr`.
   - Always prefixed with `- ` and followed by `\n\n`.
3. **Extractor Tables**:
   - Emitted from OpenXML `w:tbl`. Formatted as Markdown tables (`| ... |\n| --- | ... |`).
4. **Author-Authored Markdown-Like Text**:
   - Plain text containing characters such as `#` or `-` within body sentences (e.g. `Issue #42`, `item - detail`).
   - Plain paragraphs that happen to start with `#` or `-` without corresponding OpenXML styles.

### 4.2 Reconciliation Rules
- **List Item Compacting**:
  - When consecutive paragraphs both match list item syntax (`- Item 1\n\n- Item 2`), the intermediate `\n\n` is compacted to a single `\n`:
    ```markdown
    - Item 1
    - Item 2
    ```
  - The boundary preceding the first list item and following the last list item remains `\n\n`.
- **Heading & Table Padding**:
  - Ensure headings and tables are bounded by `\n\n`.
- **Ambiguity Rule**:
  - If a structural marker's intent cannot be deterministically verified (e.g. ambiguous inline dashes or numbers), **preserve the text as extracted**. Do not delete or transform tokens.

---

## 5. Restricted OCR Punctuation Spacing Architecture

Active exclusively on OCR formats (`PdfScanned`, `PdfMixed`, `RasterImage`, `MultiPageTiff`):

### 5.1 Approved Deterministic Transformations
Only narrow, high-confidence corrections are permitted:
1. Spurious whitespace immediately preceding a comma or period after an alphabetic word stem:
   - Pattern: `\b([a-zA-Z]{2,})\s+([,.])(?=\s|$)` $\to$ `$1$2`
   - Examples: `concluded . Next` $\to$ `concluded. Next`; `observed , but` $\to$ `observed, but`.

### 5.2 Mandatory Exclusions (Never Modified)
- **Code & Identifiers**: `foo . bar`, `x . y`, `system . out` $\to$ untouched.
- **URLs & Paths**: `http : //`, `site . com` $\to$ untouched.
- **Numbers & Decimals**: `1 . 5`, `10 - 20` $\to$ untouched.
- **Mathematical Expressions**: `f ( x )`, `[ a , b ]`, `x . y` $\to$ untouched.
- **Brackets & Parentheses**: General bracket spacing (e.g. `( inside )` or `[ 12 ]`) is **EXCLUDED** from rewriting, because spaced parentheses are common in math, formulas, and chemistry.
- **Tables & Pipes**: Untouched.

---

## 6. Formal Hyphenation Repair Decision Architecture

Controlled by `TextNormalizationOptions.RepairLinebreakHyphenation`:
- **Default (`false`)**: All line-break hyphens are preserved. If lines are joined, the hyphen is kept attached to the stem (`computa-` + `\n` + `tion` $\to$ `computa-tion`), avoiding spurious spaces (`computa- tion`).
- **Opt-In (`true`)**: Hyphen is repaired (removed and joined) ONLY when all of the following condition gates pass:

| Condition Gate | Verification Check | Action if Failed |
| :--- | :--- | :--- |
| **Gate 1: Trailing Hyphen** | Line $L_1$ ends with `[a-zA-Z]{3,}-$` | Preserve hyphen & line |
| **Gate 2: Lowercase Continuation** | Line $L_2$ begins with `^[a-z]{2,}\b` | Preserve hyphen & line |
| **Gate 3: Alphabetic Purity** | Neither segment contains numbers, math, or Greek | Preserve hyphen & line |
| **Gate 4: Single-Letter Prefix Exclusion** | Not `x-`, `n-`, `p-`, `k-`, etc. | Preserve hyphen: `p-value`, `x-axis` |
| **Gate 5: Greek Symbol Exclusion** | Not `α-`, `β-`, `γ-`, etc. | Preserve hyphen: `α-helix`, `β-sheet` |
| **Gate 6: Chemical Compound Exclusion** | Not `Na-Cl`, `cis-`, `trans-`, etc. | Preserve hyphen: `Na-Cl`, `cis-trans` |
| **Gate 7: Proper Name Exclusion** | Neither part is capitalized proper noun | Preserve hyphen: `Smith-Waterman` |
| **Gate 8: Known Compound Word Exclusion** | Not `well-known`, `state-of-the-art`, etc. | Preserve hyphen: `well-known` |
| **Gate 9: Same Physical Page** | $L_1$ and $L_2$ belong to the same page | **NEVER** join across page boundary |

---

## 7. Page Boundary Isolation Invariant

- `NormalizePage` processes an individual `ExtractedPageRaw` in isolation.
- Under no circumstances does C6.3 inspect, buffer, or join lines across Page $N$ and Page $N+1$.
- Words split across pages (e.g. Page 1 ends with `hypo-\n` and Page 2 starts with `thesis`) remain on their respective pages:
  - Page 1 `NormalizedText` ends with `hypo-`.
  - Page 2 `NormalizedText` starts with `thesis`.
- This ensures that 1:1 page provenance, visual coordinate mapping, and academic citations remain accurate.

---

## 8. Format-Specific Dispatch Specification

Each format dispatches to an explicitly defined structural handler:

```csharp
private string NormalizeStructural(string sanitizedText, DetectedDocumentFormat format, TextNormalizationOptions options)
{
    return format switch
    {
        DetectedDocumentFormat.PlainText => 
            AssemblePlainTextStructural(sanitizedText, options),

        DetectedDocumentFormat.Markdown => 
            AssembleMarkdownStructural(sanitizedText, options),

        DetectedDocumentFormat.DelimitedText => 
            sanitizedText, // Table records preserved 1:1; no line joining

        DetectedDocumentFormat.LocalHtml => 
            AssembleHtmlStructural(sanitizedText, options),

        DetectedDocumentFormat.Docx => 
            ReconcileDocxStructural(sanitizedText, options),

        DetectedDocumentFormat.PdfDigital => 
            RejoinPdfDigitalStructural(sanitizedText, options),

        DetectedDocumentFormat.PdfScanned or
        DetectedDocumentFormat.PdfMixed or
        DetectedDocumentFormat.RasterImage or
        DetectedDocumentFormat.MultiPageTiff => 
            RejoinOcrStructural(sanitizedText, format, options),

        _ => AssemblePlainTextStructural(sanitizedText, options)
    };
}
```

- When uncertainty exists in any handler, **preservation is chosen over rewriting**.
