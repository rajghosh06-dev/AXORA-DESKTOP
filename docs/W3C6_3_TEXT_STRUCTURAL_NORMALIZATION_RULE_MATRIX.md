# Phase W3-C.6.3 Rule Matrix: Source-Aware Paragraph Assembly, Line-Wrap Rejoining & DOCX Markdown Token Reconciliation

**Phase**: `W3-C.6.3 — Source-Aware Paragraph Assembly, Line-Wrap Rejoining & DOCX Markdown Token Reconciliation`  
**Status**: `PLANNING ONLY / REVISED SPECIFICATION`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Format-Specific Structural Policy Matrix

This matrix specifies the structural normalization behavior of `ITextNormalizer` across all 10 supported document formats in AXORA Scholar Kit. When uncertainty exists, **preservation is chosen over rewriting**:

| Document Format | Soft-Wrap Line Joining | OCR Line & Spacing Correction | Hyphenation Repair (if enabled) | Code Region Protection (```) | DOCX Scaffolding Reconciler | Table Preservation | Paragraph Boundary Representation |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **Plain Text** (`PlainText`) | **Disabled** (newlines preserved) | Disabled | Disabled | Active (if fenced) | Disabled | Preserved as-is | `\n\n+` $\to$ `\n\n`; indent preserved ($\le 4$ spaces) |
| **Markdown** (`Markdown`) | **Disabled** (semantic newlines) | Disabled | Disabled | **Active (No structural rewriting)** | Disabled | Preserved (`\| ... \|`) | `\n\n+` $\to$ `\n\n`; headings, lists preserved |
| **Delimited Text** (`DelimitedText`) | **Disabled** (table rows 1:1) | Disabled | Disabled | Disabled | Disabled | **Active (Preserved as-is)** | Each record on its own line; zero collapsing |
| **Local HTML** (`LocalHtml`) | **Disabled** (already block-structured) | Disabled | Disabled | Active (in `<pre>`/fences) | Disabled | Preserved (`\| ... \|`) | Blocks (`<p>`, `<h1-6>`) bounded by `\n\n` |
| **PDF Digital** (`PdfDigital`) | **Active** (flowing lines joined with space) | Disabled | Active | Active (if fenced) | Disabled | Preserved (if tables detected) | `\n\n` or indent with terminal punct $\to$ `\n\n` |
| **PDF Scanned** (`PdfScanned`) | **Active** (OCR lines joined with space) | **Active** (restricted comma/period cleanup)| Active | Active (if fenced) | Disabled | Preserved | `\n\n` or indent $\to$ `\n\n` |
| **PDF Mixed** (`PdfMixed`) | **Active** (page-aware: digital or OCR) | **Active** on OCR pages | Active | Active (if fenced) | Disabled | Preserved | Digital/OCR paragraphs isolated; `[OCR]` tag preserved |
| **Word DOCX** (`Docx`) | **Disabled** (paragraphs already `\n\n`) | Disabled | Disabled | Active (if fenced) | **Active** (compacts lists, pads `#`) | Preserved (`\| ... \|`) | Paragraphs $\to$ `\n\n`; consecutive list items $\to$ `\n` |
| **Raster OCR** (`RasterImage`) | **Active** (visual lines joined with space) | **Active** (restricted comma/period cleanup)| Active | Active (if fenced) | Disabled | Preserved | `\n\n` or indent $\to$ `\n\n` |
| **Multi-Frame TIFF** (`MultiPageTiff`)| **Active** (frame-by-frame isolated) | **Active** (restricted comma/period cleanup)| Active | Active (if fenced) | Disabled | Preserved | Frame-local `\n\n` boundaries; zero cross-frame leaks |

---

## 2. Evidence-Based Line Classification Matrix (PDF & OCR)

This matrix governs line joining decisions for consecutive non-empty lines $L_i$ and $L_{i+1}$ in formats with soft-wrapping enabled (`PdfDigital`, `PdfScanned`, `PdfMixed`, `RasterImage`, `MultiPageTiff`).

> [!IMPORTANT]
> A short line alone must **NEVER** be sufficient evidence of a paragraph boundary. When structural evidence is ambiguous, the newline (`\n`) is preserved.

| Line $L_i$ State | Line $L_{i+1}$ State | Structural Classification | Action Taken | Resulting Text | Rationale |
| :--- | :--- | :--- | :---: | :--- | :--- |
| Any line | In a protected region (code, table) | Block Boundary | **PRESERVE BOUNDARY** | `$L_i\nL_{i+1}$` | Protected region freeze |
| Blank line between | Any line | Explicit Paragraph Break | **PARAGRAPH BREAK** | `$L_i\n\nL_{i+1}$` | Source `\n\s*\n` indicates intentional break |
| Ends with hyphen (`algo-`) | Starts with lowercase (`rithm`) | Line-Break Hyphen | **EVALUATE HYPHEN TABLE** | See Section 3 | Hyphenation condition gates |
| Starts with `# `, `- `, `* `, `\|` | Any line | Block Element | **KEEP BREAK** | `$L_i\nL_{i+1}$` | $L_i$ is a heading, bullet, or table row |
| Any line | Starts with `# `, `- `, `* `, `\|` | Block Element Start | **BLOCK BOUNDARY** | `$L_i\n\nL_{i+1}$` | $L_{i+1}$ begins a structural block |
| Ends with terminal punct (`.`, `!`, `?`) | Starts with indent ($\ge 2$ spaces or `\t`) | Indented Paragraph Break | **PARAGRAPH BREAK** | `$L_i\n\nL_{i+1}$` | Traditional typesetter paragraph indent |
| Lacks terminal punctuation | Starts with lowercase letter | Mid-Sentence Soft Wrap | **SOFT-WRAP JOIN** | `$L_i\ L_{i+1}$` | Definite mid-sentence line wrap |
| Lacks terminal punctuation | Starts with uppercase letter | Mid-Sentence Soft Wrap | **SOFT-WRAP JOIN** | `$L_i\ L_{i+1}$` | Wrap before proper noun / capitalized word |
| Ends with comma, colon, semicolon | Any flowing line | Mid-Clause Soft Wrap | **SOFT-WRAP JOIN** | `$L_i\ L_{i+1}$` | Continuation after clause punctuation |
| Ends with period (abbreviation like `et al.`) | Starts with lowercase letter | Abbreviation Soft Wrap | **SOFT-WRAP JOIN** | `$L_i\ L_{i+1}$` | Period is part of abbreviation stem |
| Ends with terminal punct (`.`, `!`, `?`) | Starts with uppercase, NO blank, NO indent | **Ambiguous Sentence/Paragraph** | **PRESERVE NEWLINE** | `$L_i\nL_{i+1}$` | **Preserves newline on ambiguity**; avoids false join or false `\n\n` |

---

## 3. Formal Hyphenation Repair Decision Matrix

Controlled by `TextNormalizationOptions.RepairLinebreakHyphenation`.

- **Default (`false`)**: All line-break hyphens are preserved. When lines are joined, the hyphen is kept attached to the stem (`computa-` + `\n` + `tion` $\to$ `computa-tion`), avoiding spurious spaces (`computa- tion`).
- **Opt-In (`true`)**: Evaluated against the formal decision table below:

| Line-End Input ($L_1$) | Next-Line Input ($L_2$) | Condition Check Passed? | Action Taken | Output Result | Classification / Reason |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `computa-` | `tion was analyzed` | YES | **REPAIR (STRIP HYPHEN)** | `computation was analyzed` | Standard word stem broken across lines |
| `experi-` | `mental validation` | YES | **REPAIR (STRIP HYPHEN)** | `experimental validation` | Standard word stem broken across lines |
| `multi-` | `faceted approach` | YES | **REPAIR (STRIP HYPHEN)** | `multifaceted approach` | Standard stem with known prefix |
| `state-` | `of-the-art model` | NO (Compound word) | **PRESERVE HYPHEN** | `state-of-the-art model` | Established compound phrase |
| `well-` | `known theorem` | NO (Compound word) | **PRESERVE HYPHEN** | `well-known theorem` | Established compound adjective |
| `α-` | `helix conformation` | NO (Greek symbol) | **PRESERVE HYPHEN** | `α-helix conformation` | Greek scientific symbol prefix |
| `Na-` | `Cl crystal lattice` | NO (Chemical compound)| **PRESERVE HYPHEN** | `Na-Cl crystal lattice` | Chemical element symbol formula |
| `p-` | `value < 0.05` | NO (Single-letter prefix)| **PRESERVE HYPHEN** | `p-value < 0.05` | Statistical parameter prefix |
| `R-` | `squared metric` | NO (Single-letter prefix)| **PRESERVE HYPHEN** | `R-squared metric` | Statistical metric |
| `x-` | `axis coordinate` | NO (Single-letter prefix)| **PRESERVE HYPHEN** | `x-axis coordinate` | Mathematical axis coordinate |
| `Smith-` | `Waterman algorithm` | NO (Capitalized proper) | **PRESERVE HYPHEN** | `Smith-Waterman algorithm` | Proper name compound |
| `10-` | `20 samples analyzed`| NO (Numeric range) | **PRESERVE HYPHEN** | `10-20 samples analyzed` | Numeric range |
| `file-` | `name parameter` | NO (Identifier) | **PRESERVE HYPHEN** | `file-name parameter` | Code / technical identifier |
| `x -` | `y = z` (operator) | NO (Math operator) | **PRESERVE HYPHEN/MINUS**| `x - y = z` | Mathematical subtraction operator |
| `investiga-` (bottom Page 1)| `tion` (top Page 2) | NO (Cross-page) | **NEVER JOIN ACROSS PAGES**| Page 1: `investiga-`<br>Page 2: `tion` | Page boundary isolation invariant |

---

## 4. Restricted OCR Punctuation Spacing Matrix

Active exclusively on OCR formats (`PdfScanned`, `PdfMixed`, `RasterImage`, `MultiPageTiff`).

### 4.1 Permitted Deterministic Corrections

| Defective OCR Input | Target Output | Match Rule / Scope | Preservation Invariant |
| :--- | :--- | :--- | :--- |
| `evidence , discovered` | `evidence, discovered` | `\b([a-zA-Z]{2,})\s+,(?=\s\|$)` $\to$ `$1,` | Comma following alphabetic word |
| `concluded . Next` | `concluded. Next` | `\b([a-zA-Z]{2,})\s+\.(?=\s\|$)` $\to$ `$1.` | Period following alphabetic word |

### 4.2 Explicit Exclusions (Never Modified by OCR Cleanup)

| Excluded Pattern | Input Example | Target Behavior | Reason for Exclusion |
| :--- | :--- | :---: | :--- |
| **Code & Identifiers** | `foo . bar`, `system . out` | **PRESERVED AS-IS** | Spacing may be syntactic or significant in code |
| **URLs & Protocols** | `http : //`, `site . com` | **PRESERVED AS-IS** | Colon and period are URL delimiters |
| **Mathematical Dot Products**| `x . y`, `u . v` | **PRESERVED AS-IS** | Vector dot product operator |
| **Decimals & Numbers** | `1 . 5`, `3 . 1415` | **PRESERVED AS-IS** | Number spacing must not be corrupted |
| **Parentheses / Citations** | `( sample )`, `( 2026 )` | **PRESERVED AS-IS** | Broad bracket rewriting damages formulas and citations |
| **Brackets & Arrays** | `[ 12 ]`, `[ a , b ]` | **PRESERVED AS-IS** | Array indexing and math intervals |
| **Hyphen / Dash Spacing** | `word - word` | **PRESERVED AS-IS** | Em/en dash equivalents |
| **Table Separators** | `\| col 1 \| col 2 \|` | **PRESERVED AS-IS** | Structural table pipes |

---

## 5. DOCX Structural Markdown Reconciliation Matrix

Active for `DetectedDocumentFormat.Docx`:

| DOCX Extracted Pattern | Target Normalized Structure | Reconciliation Rule | Ambiguity Handling |
| :--- | :--- | :--- | :--- |
| `- Item 1\n\n- Item 2\n\n- Item 3` | `- Item 1\n- Item 2\n- Item 3` | Compacts consecutive list items to single newlines; bounds list block with `\n\n` | If non-list text occurs between items, preserve paragraph break |
| `Body\n# Heading` | `Body\n\n# Heading` | Ensures `\n\n` precedes heading | If `#` occurs mid-sentence (e.g. `Issue #42`), leave as-is |
| `# Heading\nBody` | `# Heading\n\nBody` | Ensures `\n\n` follows heading | If heading already has `\n\n`, leave as-is |
| `###Heading` | `### Heading` | Normalizes to single space after `#` | If hashes exceed 6, treat as literal text |
| `\| A \| B \|\n\nBody` | `\| A \| B \|\n\nBody` | Preserves Markdown tables emitted by C4 table extractor | If table syntax is malformed, preserve line breaks |
| `item - detail` (author text) | `item - detail` | Untouched: mid-sentence dashes are not parsed as lists | If intent is ambiguous, **preserve text as extracted** |
| `Chapter #1 notes` (author text)| `Chapter #1 notes` | Untouched: mid-sentence hashes are not parsed as headings | If intent is ambiguous, **preserve text as extracted** |

---

## 6. Scientific & Mathematical Invariant Protection Matrix

| Expression Category | Input Example | Structural Normalization Behavior | Invariant Rationale |
| :--- | :--- | :---: | :--- |
| **Math Superscripts** | $x^2 + y^2 = z^2$ | **PRESERVED BY THIS PASS** | Exponent values are critical for formulas |
| **Scientific Exponents** | $10^{-3}, 6.022 \times 10^{23}$ | **PRESERVED BY THIS PASS** | Magnitude values must not be flattened |
| **Chemical Formulas** | $\text{H}_2\text{O}, \text{CO}_2, \text{C}_6\text{H}_{12}\text{O}_6$ | **PRESERVED BY THIS PASS** | Subscript numbers must not be treated as line numbers |
| **Ion Charges** | $\text{Fe}^{2+}, \text{SO}_4^{2-}$ | **PRESERVED BY THIS PASS** | Superscript charge notation intact |
| **Greek Variables** | $\alpha, \beta, \gamma, \Delta, \theta, \lambda, \mu, \sigma, \omega$| **PRESERVED BY THIS PASS** | Variables are distinct from Latin letters |
| **Calculus Operators** | $\int_0^1 f(x)dx \le \sum_{i=1}^n x_i$ | **PRESERVED BY THIS PASS** | Mathematical integration and summation operators |
| **Comparison Operators**| $\sqrt{x} \approx y \ne z, a \le b, c \ge d$ | **PRESERVED BY THIS PASS** | Relational operators untouched |
| **LaTeX Inline Math** | `$E = mc^2$`, `$\nabla \times \mathbf{B}$` | **PRESERVED BY THIS PASS** | Formula delimiters intact; no spaces inserted |
| **LaTeX Block Math** | `$$\int_{-\infty}^\infty e^{-x^2}dx = \sqrt{\pi}$$`| **PRESERVED BY THIS PASS** | Multi-line math block preserved verbatim |
