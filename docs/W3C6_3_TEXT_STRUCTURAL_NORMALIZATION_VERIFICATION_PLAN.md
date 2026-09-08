# Phase W3-C.6.3 Verification Plan: Source-Aware Paragraph Assembly, Line-Wrap Rejoining & DOCX Markdown Token Reconciliation

**Phase**: `W3-C.6.3 — Source-Aware Paragraph Assembly, Line-Wrap Rejoining & DOCX Markdown Token Reconciliation`  
**Status**: `PLANNING ONLY / REVISED SPECIFICATION`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Verification Strategy & Objectives

The primary objective of Phase W3-C.6.3 verification is to ensure that structural text normalization and paragraph assembly:
1. **Preserves Baseline Integrity**: Zero regressions across all 1,108 currently passing assertions.
2. **Maintains Ground Truth Immutability**: `ExtractedPageRaw.RawText` is never modified or substituted under any circumstances.
3. **Ensures Determinism & Idempotence**: Normalization produces bit-for-bit identical outputs across runs, and $\operatorname{Normalize}(\operatorname{Normalize}(x)) == \operatorname{Normalize}(x)$ holds across clean and malformed structures.
4. **Protects Scientific & Mathematical Notations**: Superscripts, subscripts, Greek variables, and math operators are preserved by this structural pass.
5. **Validates Evidence-Based Paragraph Decisions**: Verifies that soft-wrap joining occurs only when justified by structural evidence, that short lines alone never force paragraph breaks, and that ambiguous line breaks preserve the newline (`\n`).
6. **Validates Negative & Edge Cases**: Prevents false paragraph joins, false hyphen repairs, code block corruption, and cross-page text leakage.

---

## 2. Automated Test Matrix: 22 Verification Categories

The automated test suite will implement at least 25 targeted assertions across 22 distinct verification categories:

| Category | Invariant Tested | Target Format | Expected Outcome |
| :--- | :--- | :--- | :--- |
| **A. Paragraph Boundaries** | `\n\n+` converted to canonical `\n\n` | All formats | Exactly two newlines delineate paragraphs |
| **B. PDF Soft Wraps** | Mid-sentence visual line wraps | `PdfDigital` | Lines joined with single space; sentences flow |
| **C. Indented Paragraph Breaks**| Terminal punct + $\ge 2$ space indent on next line | `PdfDigital` | Preserved as separate paragraphs with `\n\n` |
| **D. Ambiguous Line Breaks** | Terminal punct + uppercase, NO blank, NO indent | `PdfDigital`, OCR | **Preserves newline (`\n`)**; no false join or `\n\n` |
| **E. OCR Line Joining** | Fragmented text lines from OCR | `PdfScanned`, `RasterImage` | Lines joined with space into cohesive paragraph |
| **F. OCR Punctuation Spacing**| Spurious space before `,` and `.` on word stems | OCR formats | Fixed: `word , next` $\to$ `word, next`; `end .` $\to$ `end.` |
| **G. OCR Spacing Exclusions**| Code, URLs, decimals, math, parentheses | OCR formats | **Untouched**: `foo . bar`, `( text )`, `[ 12 ]`, `1 . 5` |
| **H. Hyphenation (Default)** | Line-end hyphen with `RepairLinebreakHyphenation = false` | All formats | Hyphen preserved (`computa-\ntion` $\to$ `computa-tion`) |
| **I. Hyphenation (Opt-In)** | Broken word stem with `RepairLinebreakHyphenation = true` | `PdfDigital`, OCR | Rejoined: `computa-\ntion` $\to$ `computation` |
| **J. False Hyphen Protection**| Hyphenated compounds under opt-in repair | `PdfDigital`, OCR | **Preserves hyphen**: `well-known`, `state-of-the-art` |
| **K. Scientific Hyphen Prot.**| Scientific terms under opt-in repair | `PdfDigital`, OCR | **Preserves hyphen**: `α-helix`, `Na-Cl`, `p-value`, `10-20` |
| **L. DOCX Headings** | Headings (`# Heading`) padding and formatting | `Docx` | Ensures `\n\n` before/after; single space after `#` |
| **M. DOCX Bullet Lists** | Compacting consecutive list item paragraphs | `Docx` | Compacts `- Item 1\n\n- Item 2` to single newline |
| **N. DOCX Tables** | Markdown table preservation from C4 extractor | `Docx` | Table rows preserved with `\n`; bounded by `\n\n` |
| **O. Author Markdown in DOCX**| Inline hashes and dashes in Word text | `Docx` | **Untouched**: `Issue #42`, `item - detail` |
| **P. Fenced Code Region** | Text inside ```` ``` ```` or `~~~` blocks | `Markdown`, `PlainText` | **No structural rewriting**: indentation and newlines kept |
| **Q. Unclosed Code Fence** | Opening fence without closing fence before page end | `Markdown` | Protected to page end; no cross-page leakage |
| **R. Table Row Freeze** | Delimited text / Markdown table integrity | `DelimitedText` | Zero row collapsing; delimiters preserved |
| **S. Scientific Notation** | $x^2$, $\text{H}_2\text{O}$, $\alpha, \beta, \gamma$, $\int$, $\sum$, $\le$ | All formats | Preserved by this structural pass; zero character mutation |
| **T. Page Boundary Isolation**| Words and sentences split across physical pages | `ExtractedPageRaw` | **Never joins across pages**; Page 1 & Page 2 isolated |
| **U. Empty / Whitespace Page**| 0-byte or whitespace-only page text | All formats | Returns `string.Empty` without fabricating breaks |
| **V. Idempotence & Determinism**| Double normalization on clean & malformed text | All formats | Bit-for-bit identical strings under `StringComparison.Ordinal` |

---

## 3. Dedicated Negative & Edge Case Tests

The test plan mandates explicit test coverage for the following negative scenarios:
1. **False Paragraph Join Prevention**:
   - Verify that an ambiguous line (ending with `.` followed by an unindented uppercase line without a blank line) does NOT force a soft join (`' '`), but preserves the newline (`\n`).
2. **False Hyphen Repair Prevention**:
   - Verify that enabling `RepairLinebreakHyphenation = true` does NOT damage `well-known`, `state-of-the-art`, `α-helix`, `Na-Cl`, `p-value`, `x-axis`, or `10-20`.
3. **Cross-Page Split Words**:
   - Verify that `Page 1` ending with `inves-\ntiga-` and `Page 2` starting with `tion` are normalized independently without cross-page merging.
4. **Author-Authored Markdown Scaffolding in DOCX**:
   - Verify that body text such as `"Refer to Section #3 for details - results pending."` does not have `#` or `-` converted into headings or bullet lists.
5. **Malformed Fences**:
   - Verify that unclosed fences do not crash the parser or corrupt subsequent page processing.

---

## 4. Mandatory Manual Verification Protocol

In addition to automated tests, Phase W3-C.6.3 requires a lightweight, mandatory manual verification protocol using real representative academic documents:

### 4.1 Representative Test Corpus
1. **Digital Academic PDF**: Two-column IEEE or ACM paper with equations, headings, soft-wrapped body text, and citations.
2. **Scanned / OCR PDF**: Historical or scanned academic paper processed via Windows Media OCR with line breaks and OCR artifacts.
3. **Mixed PDF**: Multi-page document with both digital vector pages and scanned OCR pages.
4. **Word DOCX Document**: Document containing Word Headings (H1/H2), multi-level bullet lists, and tables.
5. **Markdown Document**: Technical document containing fenced code blocks (C#/Python) and math formulas.
6. **Scientific Document**: Text containing dense superscripts, subscripts, Greek variables, and chemical formulas.

### 4.2 Manual Verification Checklist
For each document in the representative corpus, inspect `ExtractedPageRaw.NormalizedText` and verify:
- [ ] **Paragraph Boundaries**: Distinct narrative paragraphs are separated by `\n\n`.
- [ ] **Soft-Wrap Joining**: Visual line wraps within body paragraphs flow smoothly with spaces without creating run-on paragraphs.
- [ ] **Code Blocks**: Code indentation, line breaks, and operators are preserved without structural rewriting.
- [ ] **Tables**: Tabular rows and cell alignments are maintained.
- [ ] **Scientific Notation**: Exponents ($x^2$, $10^{-3}$), formulas ($\text{H}_2\text{O}$), and Greek symbols ($\alpha, \beta, \gamma$) are intact.
- [ ] **Headings & Lists**: Headings are bounded by `\n\n`; consecutive list items are compact.
- [ ] **Page Boundaries**: Pages end cleanly with no text bleeding into or from adjacent pages.
- [ ] **Zero Obvious False Joins**: Headings, captions, table rows, and ambiguous sentences are not erroneously fused into body paragraphs.

---

## 5. Verification Execution Protocol

### Step 1: Toolchain & Build Verification
```powershell
dotnet build Axora-Desktop-WinUI\Axora.Desktop.sln -p:Platform=x64
```
- **Criteria**: 0 Errors, 329 pre-existing warnings in viewmodels, 0 normalizer warnings.

### Step 2: Automated Test Execution
```powershell
dotnet build Axora-Desktop-WinUI\Axora.Desktop.Tests\Axora.Desktop.Tests.csproj -p:Platform=x64
& "Axora-Desktop-WinUI\Axora.Desktop.Tests\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.Tests.exe"
```
- **Criteria**:
  - Total assertions: $\ge 1,135$ (1,108 baseline + $\ge 27$ new).
  - 100% PASS, 0 FAIL.
  - Zero regressions across prior suites.

### Step 3: Git Governance Verification
```powershell
git rev-parse HEAD
git status --short
git diff --cached --stat
git diff -- Axora-Desktop-MaterialUI
```
- **Criteria**:
  - Protected HEAD: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`.
  - Zero staged files.
  - `Axora-Desktop-MaterialUI` 100% untouched.

### Step 4: Independent Closure Audit
- Document implementation in `docs/qa/W3C6_3_IMPLEMENTATION_REPORT.md`.
- Subject to independent read-only closure audit producing `docs/qa/W3C6_3_CLOSURE_AUDIT.md`.
