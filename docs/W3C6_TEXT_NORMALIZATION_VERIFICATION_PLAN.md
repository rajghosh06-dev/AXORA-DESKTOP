# Phase W3-C.6 Verification Plan: Text Normalization & Paragraph Assembly

**Phase**: `W3-C.6 — Text Normalization & Paragraph Assembly`  
**Status**: `PLANNING ONLY / APPROVED FOR REVIEW`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Verification Strategy & Objectives

The verification plan for Phase **W3-C.6** establishes an exhaustive, multi-tiered testing harness to validate the correctness, determinism, idempotence, security, and mathematical integrity of `TextNormalizer` before any downstream chunking or persistence logic is implemented.

### Core Testing Objectives:
1. **RawText Immutability**: Prove that `rawPage.RawText` remains bit-for-bit identical before, during, and after normalization.
2. **Deterministic & Idempotent Transformations**: Prove that $\operatorname{Normalize}(\operatorname{Normalize}(x)) = \operatorname{Normalize}(x)$ across all modes and formats.
3. **Format-Aware Fidelity**: Validate format-specific rules for PDF, DOCX, Markdown, CSV, HTML, and OCR without cross-format contamination.
4. **DOCX C4 Reconciliation**: Prove that structural Markdown tokens (`# `, `- `, `|`) in DOCX text are properly spaced while literal user characters are preserved.
5. **OCR Fragmentation & Punctuation Cleanup**: Prove that fragmented OCR lines join correctly into paragraphs and punctuation spacing artifacts are eliminated.
6. **Scientific & Notation Protection**: Prove that superscripts, subscripts, chemical formulas, and Greek letters remain uncorrupted.
7. **Security & ReDoS Immunity**: Prove that pathological text inputs complete within strict timeouts without catastrophic backtracking.

---

## 2. Test Suite Architecture: `RunW3_C6TextNormalizationTests()`

All tests will be implemented in `Axora-Desktop-WinUI/Axora.Desktop.Tests/Program.cs` under the dedicated harness method `RunW3_C6TextNormalizationTests()`, organized into 7 distinct groups:

### Group A: Whitespace & Line Ending Sanitization
- `W3C6_1a_CrlfToLf`: Replaces Windows CRLF (`\r\n`) and lone CR (`\r`) with Unix LF (`\n`).
- `W3C6_1b_BomStripped`: Strips UTF-8/UTF-16 Byte Order Marks (`\uFEFF`) from start or middle of text.
- `W3C6_1c_ZeroWidthCharsStripped`: Strips zero-width space (`\u200B`), joiner (`\u200D`), and non-joiner (`\u200C`).
- `W3C6_1d_ControlCharsStripped`: Strips non-printable control characters ($0\times00$–$0\times08$, $0\times0E$–$0\times1F$, $0\times7F$) while preserving `\t` and `\n`.
- `W3C6_1e_ConsecutiveSpacesCollapsed`: Collapses runs of $\ge 2$ spaces on a line to single space (`\u0020`).
- `W3C6_1f_ConsecutiveNewlinesCollapsed`: Collapses $\ge 3$ consecutive newlines to double newlines (`\n\n`).

### Group B: Unicode, Ligatures & Mathematical Protection
- `W3C6_2a_LigatureFi`: Unfolds `\uFB01` (`ﬁ`) to `"fi"`.
- `W3C6_2b_LigatureFl`: Unfolds `\uFB02` (`ﬂ`) to `"fl"`.
- `W3C6_2c_LigatureFfiFfl`: Unfolds `\uFB03` (`ﬃ`) to `"ffi"` and `\uFB04` (`ﬄ`) to `"ffl"`.
- `W3C6_2d_NbspNormalized`: Converts non-breaking spaces (`\u00A0`, `\u202F`) to standard space `\u0020`.
- `W3C6_2e_SoftHyphenStripped`: Strips discretionary soft hyphens (`\u00AD`).
- `W3C6_2f_MathSuperscriptsPreserved`: Verifies $x^2$, $10^{-3}$ are preserved without corruption.
- `W3C6_2g_ChemicalSubscriptsPreserved`: Verifies $\text{H}_2\text{O}$, $\text{C}_6\text{H}_{12}\text{O}_6$, $\text{Fe}^{2+}$ are preserved verbatim.
- `W3C6_2h_GreekMathSymbolsPreserved`: Verifies $\alpha, \beta, \gamma, \int, \sum, \sqrt{}, \le, \ge$ remain intact.
- `W3C6_2i_NfkcOptInOnly`: Verifies NFKC decomposition occurs ONLY when explicitly enabled (`ApplyUnicodeNfkc = true`).

### Group C: Source-Format-Aware Normalization
- `W3C6_3a_PdfSoftLineWrapsJoined`: Flowing wrapped lines in digital PDF join with single space.
- `W3C6_3b_PdfParagraphBreakPreserved`: Lines separated by blank line or terminal punctuation + indent remain separate paragraphs.
- `W3C6_3c_DelimitedTextCommasNotCollapsed`: Consecutive commas in CSV are NOT collapsed into a single comma.
- `W3C6_3d_DelimitedTextTabsNotCollapsed`: Consecutive tabs in TSV are NOT collapsed.
- `W3C6_3e_MarkdownCodeBlocksFrozen`: Whitespace, indentation, and newlines inside fenced code blocks (```` ``` ````) are 100% frozen.
- `W3C6_3f_LocalHtmlEntitiesUnescaped`: HTML entities (`&amp;`, `&lt;`, `&quot;`) are cleanly unescaped to characters.
- `W3C6_3g_PlainTextIndentPreserved`: Intentional code/table indentation up to 4 spaces preserved in plain text.

### Group D: DOCX Structural Markdown Reconciliation
- `W3C6_4a_DocxHeadingsPadded`: `# Heading` tokens emitted by C4 DOCX extractor receive proper `\n\n` boundaries.
- `W3C6_4b_DocxBulletListsPadded`: `- Bullet` items receive clean spacing between items.
- `W3C6_4c_DocxTablesPreserved`: Table pipes (`| Col1 | Col2 |`) and separator rows (`|---|---|`) preserved intact.
- `W3C6_4d_LiteralMarkdownCharsPreserved`: User text containing literal `#` or `-` within a paragraph is not stripped or corrupted.

### Group E: OCR Normalization & Paragraph Assembly
- `W3C6_5a_OcrFragmentedLinesJoined`: Scanned/OCR line fragments join into coherent paragraph flow.
- `W3C6_5b_OcrPunctuationSpacingCleaned`: Erroneous spaces before punctuation (`word , text` -> `word, text`) are removed.
- `W3C6_5c_OcrParenthesesSpacingCleaned`: Inner spaces in parentheses (`( text )` -> `(text)`) are cleaned.
- `W3C6_5d_OcrHybridDelimiterPreserved`: Hybrid PDF `[OCR]` delimiter tag preserved with clear section breaks.
- `W3C6_5e_TiffMultiFrameIsolated`: Multi-frame TIFF frames normalize individually without cross-page buffer leaks.

### Group F: Determinism, Idempotence & Ground Truth Immutability
- `W3C6_6a_RawTextImmutability`: `rawPage.RawText` is byte-for-byte identical before and after `NormalizePage`.
- `W3C6_6b_IdempotenceProof`: $\operatorname{Normalize}(\operatorname{Normalize}(x)) == \operatorname{Normalize}(x)$ across 50 diverse academic text samples.
- `W3C6_6c_DeterministicOutput`: Multiple repeated runs on identical input produce strictly identical output.
- `W3C6_6d_PageProvenancePreserved`: `PageNumber`, `PageSemantics`, `WidthPt`, `HeightPt`, `ExtractedViaOcr`, and `Confidence` are preserved untouched.

### Group G: Security, ReDoS Defense & Failure Resilience
- `W3C6_7a_ReDoSPathologicalInput`: Repetitive pathological strings (e.g. 10,000 nested spaces and punctuation) complete in $< 50 \text{ ms}$.
- `W3C6_7b_LargeDocumentPagePerformance`: 100,000-character page normalizes in $< 100 \text{ ms}$.
- `W3C6_7c_GracefulFailureFallback`: If an internal stage encounters an exception, `RawText` is preserved, `NormalizedText = RawText`, and `ERR_NORMALIZATION_FAILED` is recorded in `DiagnosticWarning`.
- `W3C6_7d_EmptyAndWhitespaceInput`: Empty strings, whitespace-only strings, and single-character strings normalize without throwing.
- `W3C6_7e_DiResolution`: Service container resolves `ITextNormalizer` as Singleton `TextNormalizer`.

---

## 3. Test Accounting Plan

- **Current Baseline Assertions (W3-C.5.5)**: **1,074**
- **Planned C6 Assertions**: Approximately 35–42 targeted assertions across Groups A through G.
- **Projected Integrated Suite**: $\approx 1,110$ assertions (100% Green).

---

## 4. Acceptance Gates

| Gate # | Gate Name | Validation Criteria | Pass Threshold |
| :---: | :--- | :--- | :---: |
| **Gate 1** | **Build & Compiler** | Clean compile in Release/x64 and Debug configurations. | 0 errors, 0 new warnings |
| **Gate 2** | **Baseline Regression** | All pre-existing assertions (W1 through W3-C.5.5) execute cleanly. | 1,074 / 1,074 passing |
| **Gate 3** | **RawText Immutability** | SHA-256 and byte equality of `RawText` before and after normalization. | 100% identical |
| **Gate 4** | **Determinism & Idempotence** | Double normalization yields identical output across all test fixtures. | 100% match |
| **Gate 5** | **Format-Aware Integrity** | PDF, DOCX, CSV, HTML, Markdown, and OCR rules apply accurately. | 100% passing |
| **Gate 6** | **Scientific Protection** | Mathematical formulas, Greek letters, and chemical notations preserved. | 100% uncorrupted |
| **Gate 7** | **Security & ReDoS** | No catastrophic backtracking or unhandled exceptions on pathological text. | Complete in $< 100 \text{ ms}$ |
| **Gate 8** | **Git Governance** | Protected HEAD pinned, zero staged files, MaterialUI untouched. | Verified clean |
