# Phase W3-C.6 Product Contract: Text Normalization & Paragraph Assembly

**Phase**: `W3-C.6 — Text Normalization & Paragraph Assembly`  
**Status**: `PLANNING ONLY / APPROVED FOR REVIEW`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Purpose & Scope

Phase **W3-C.6** designs and defines the deterministic, source-aware text normalization and paragraph assembly layer for the AXORA Scholar Kit document extraction pipeline. 

During Phases W3-C.2 through W3-C.5.5, raw extraction engines populated physical and logical pages with verbatim extracted ground truth (`RawText`), while explicitly keeping `NormalizedText = null` (the Two-Tier text invariant). Phase W3-C.6 defines the contract for transforming `RawText` into canonical, clean, and searchable `NormalizedText` on a per-page basis while preserving the author's meaning, vocabulary, mathematical formulas, chemical equations, and structural provenance.

### Strict Scope Boundary:
- **IN SCOPE**:
  - Transforming `ExtractedPageRaw.RawText` into `ExtractedPageRaw.NormalizedText`.
  - Line ending standardization (CRLF/CR -> LF).
  - Non-printable control character removal and zero-width character stripping.
  - Source-format-aware paragraph assembly and whitespace normalization.
  - Typesetting ligature unfolding (`ﬁ` -> `fi`, `ﬂ` -> `fl`, etc.).
  - OCR line fragmentation repair and punctuation spacing correction.
  - DOCX structural Markdown reconciliation.
  - Configurable Unicode normalization modes (Lossless, Formatting, Aggressive).
  - Page-bounded deterministic paragraph construction.
- **EXPLICITLY OUT OF SCOPE**:
  - **Passage Chunking**: W3-C.6 does **NOT** generate `DocumentPassageChunk` instances or slice text into fixed-size retrieval windows. Chunking is owned strictly and exclusively by **Phase W3-C.7**.
  - **Vector Embeddings & RAG**: Semantic vector generation and vector indexing belong to later phases.
  - **AI / LLM Rewriting**: No generative spelling correction, no neural smoothing, and no hallucinated text transformations.

---

## 2. Core Architectural Invariants

### Invariant 1: Immutability of Ground Truth RawText
`RawText` is the unmutated, forensic record of what the underlying extractor discovered in the source document.
- Normalization **MUST NOT** modify, re-encode, truncate, or overwrite `RawText`.
- For every page, `RawText` remains byte-for-byte identical to the output of the extraction engine.
- `NormalizedText` is a derived property computed independently.
- No downstream consumer (search, chunker, chat, UI) may conflate or substitute `RawText` with `NormalizedText`.

### Invariant 2: Page-Local Provenance & Bounded Scope
- Ingestion operates on pages (`ExtractedPageRaw` and `DocumentPage`).
- Normalization is **page-local**: each page produces its own `NormalizedText`.
- Page boundaries are never silently erased or merged across distinct physical pages.
- If a paragraph flows across a page boundary (e.g. from bottom of Page 4 to top of Page 5), each page preserves its local segment, retaining 1:1 attribution to the physical page.

### Invariant 3: Scientific, Mathematical & Notation Protection
- Mathematical superscripts (`x²`, `10⁻³`), subscripts (`H₂O`), Greek symbols (`α`, `β`, `γ`, `Δ`), and math operators (`∫`, `∑`, `√`, `≤`, `≥`, `≠`) must **NEVER** be corrupted or stripped.
- LaTeX formula blocks (`$...$`, `$$...$$`) and chemical notation (`C₆H₁₂O₆`, `Fe²⁺`) must remain intact.
- Unicode NFKC normalization must **NOT** be applied by default, as NFKC canonically decomposes `x²` into `x2` and `H₂O` into `H2O`.

### Invariant 4: Determinism & Idempotence
For any given input text $x$, format $F$, and options $O$:
1. **Determinism**: $\operatorname{Normalize}(x, F, O)$ produces the identical string output across every execution on every machine.
2. **Idempotence**:
   $$\operatorname{Normalize}(\operatorname{Normalize}(x, F, O), F, O) = \operatorname{Normalize}(x, F, O)$$
   Re-normalizing an already normalized text produces the exact same output with zero drift.

### Invariant 5: Local-First & Offline Operation
- Normalization is 100% on-device, implemented in managed C# with standard BCL facilities.
- Zero network calls, zero cloud dependencies, zero telemetry, and zero credentials.
- Document text is never written to log files.

---

## 3. Two-Tier Text Model: RawText vs. NormalizedText

| Property | `RawText` | `NormalizedText` |
| :--- | :--- | :--- |
| **Origin** | Extracted directly by format-specific engine | Derived by `ITextNormalizer` from `RawText` |
| **Lifecycle** | Set at extraction time, immutable thereafter | Computed during normalization stage |
| **Whitespace** | Contains source whitespace, indentation, wrapping | Standardized single spaces, normalized paragraph breaks |
| **Linebreaks** | Source-dependent (often hard wraps per visual line) | Flowing lines joined into coherent paragraphs; double `\n\n` for breaks |
| **Ligatures** | May contain typesetting ligatures (`ﬁ`, `ﬂ`, `ﬃ`) | Unfolded to ASCII graphemes (`fi`, `fl`, `ffi`) |
| **Control Chars** | May contain BOMs, zero-width spaces, FormFeeds | Cleaned: non-printable control chars and BOMs stripped |
| **Primary Use** | Forensic audit, verbatim citation, source alignment | Substring search, keyword indexing, RAG chunking (C.7) |
| **Nullability** | `required string` (never null, may be empty) | `string?` (null until normalized; never null after C.6) |

---

## 4. Source-Format-Aware Normalization Contracts

Normalization must recognize the idiosyncrasies of each source format rather than applying a naive uniform regex:

### 4.1 Plain Text (`PlainText`)
- Preserves intentional indentation (up to 4 spaces or tabs for code/tables).
- Normalizes CRLF/CR to LF.
- Replaces FormFeed (`\x0C`) with standardized logical section markers if present.
- Collapses $\ge 3$ consecutive newlines to double newlines (`\n\n`).

### 4.2 Markdown (`Markdown`)
- Preserves Markdown block indicators: headings (`# `), list bullets (`- `, `* `, `1. `), blockquotes (`> `), table rows (`|`), and fenced code blocks (` ``` `).
- Inside fenced code blocks: whitespace and line breaks are **100% frozen** (zero modification).
- Outside code blocks: normalizes line endings and collapses erratic trailing whitespace.

### 4.3 Delimited Text (`DelimitedText` - CSV / TSV)
- **CRITICAL**: Never collapse consecutive commas or tabs, as they represent empty table cells!
- Normalizes row line endings to LF.
- Preserves quoted fields and escaped delimiters.

### 4.4 Local HTML (`LocalHtml`)
- Strips residual non-text HTML entities while unescaping standard entities (`&amp;` -> `&`, `&lt;` -> `<`, etc.).
- Consolidates block boundaries (`<p>`, `<div>`, `<h1>`-`<h6>`, `<li>`) into clean double/single newlines.
- Collapses internal inline whitespace runs to single space.

### 4.5 Digital PDF (`PdfDigital`)
- Resolves soft line wrapping: lines within a paragraph that wrap visually are joined with a space.
- Distinguishes paragraph breaks: lines followed by blank lines or with significant indentation or terminal punctuation followed by a capitalized line.
- Unfolds typesetting ligatures (`ﬁ`, `ﬂ`, `ﬃ`, `ﬄ`, `ﬀ`).
- Strips PDF artifact characters (e.g. orphan FormFeed, replacement characters `\uFFFD`).

### 4.6 Scanned PDF & Raster / TIFF OCR (`PdfScanned`, `PdfMixed`, `RasterImage`, `MultiPageTiff`)
- Joins OCR-fragmented lines into cohesive paragraphs.
- Corrects OCR punctuation spacing: eliminates spurious spaces before punctuation (e.g. `word , next` -> `word, next`; `end .` -> `end.`).
- Cleans orphaned hyphenation at line ends (`algo-` + `rithm` -> `algorithm` when `RepairLinebreakHyphenation` is active).
- Strips soft hyphens (`\u00AD`).
- Preserves hybrid PDF extraction tags (`[OCR]`) as structured section demarcations.

---

## 5. DOCX C4 Reconciliation Contract

During Phase W3-C.4, `DocxDocumentExtractorEngine` introduced structural Markdown tokens (`# `, `## `, `- `, `|`) into `RawText` to represent Word headings, bullet lists, and tables.

### Contractual Resolution:
1. **`RawText` Unaltered**: The existing `RawText` output from `DocxDocumentExtractorEngine` is preserved as-is.
2. **`NormalizedText` Handling**:
   - `ITextNormalizer` recognizes these structural tokens when format is `DetectedDocumentFormat.Docx`.
   - **Do NOT strip `#`, `-`, or `|` blindly**: Doing so would destroy genuine table cells, list items, and headings.
   - Standardize whitespace surrounding structural markers:
     - Headings: Ensure `# Heading` has a single leading `#` and is bounded by `\n\n`.
     - Lists: Ensure bullet items (`- Item`) have consistent single space after bullet and single newline between items.
     - Tables: Preserve pipe separators (`| Col1 | Col2 |`), trim cell contents, and preserve table integrity.
   - User-authored text containing literal `#`, `-`, or `|` within body paragraphs is preserved verbatim.

---

## 6. Normalization Options & Modes Matrix

The contract defines three canonical normalization tiers:

| Setting | Tier 1: `Lossless` | Tier 2: `Formatting` (Default) | Tier 3: `Aggressive` (Opt-in) |
| :--- | :---: | :---: | :---: |
| `NormalizeLineEndings` (CRLF -> LF) | Yes | Yes | Yes |
| `StripNonPrintableControlChars` | Yes | Yes | Yes |
| `StripBOMAndZeroWidthChars` | Yes | Yes | Yes |
| `CollapseConsecutiveSpaces` | No | Yes | Yes |
| `PreserveParagraphBreaks` | Yes | Yes | Yes |
| `UnfoldTypesettingLigatures` | No | Yes | Yes |
| `NBSP to Standard Space` | No | Yes | Yes |
| `RepairLinebreakHyphenation` | No | No (Default False) | Yes |
| `ApplyUnicodeNfkc` | No | No (Default False) | Yes |
| **Mathematical / Chemical Safety** | 100% | 100% | Degraded (e.g. $x^2 \to x2$) |
| **Target Workload** | High-precision forensics | Standard academic search & notes | Broad fuzzy retrieval |

---

## 7. Paragraph Assembly Contract (Paragraph != Chunk)

- **Paragraph**: A semantically coherent block of thought authored by the creator, delineated by paragraph breaks (blank lines, heading transitions, or block boundaries).
- **Passage Chunk**: A fixed-size, bounded window of text (e.g. 350 characters with 60 character overlap) constructed for vector embedding and retrieval in Phase W3-C.7.
- **Contract**: `ITextNormalizer` produces `NormalizedText` structured into paragraphs separated by `\n\n`. It **MUST NOT** slice, stride, or segment paragraphs into passage chunks.

---

## 8. Failure Policy & Diagnostic Guarantees

If text normalization encounters an unexpected exception or pathological input:
1. The extraction pipeline **MUST NOT CRASH**.
2. `RawText` remains 100% intact and uncorrupted.
3. `NormalizedText` falls back gracefully to `RawText` (with minimal lossless line ending cleanup).
4. The page's `DiagnosticWarning` records `ERR_NORMALIZATION_FAILED` with the diagnostic message.
5. The document's `GlobalWarnings` logs the failure, and `IsPartialSuccess` reflects degraded normalization if applicable.
