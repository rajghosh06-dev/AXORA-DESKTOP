# Phase W3-C.6 Rule Matrix: Text Normalization & Paragraph Assembly

**Phase**: `W3-C.6 — Text Normalization & Paragraph Assembly`  
**Status**: `PLANNING ONLY / APPROVED FOR REVIEW`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Format-by-Format Normalization Rule Matrix

This matrix governs how `ITextNormalizer` treats each of the supported document formats in AXORA Scholar Kit:

| Document Format | Line Endings | Whitespace Collapsing | Line Joining / Paragraph Assembly | Ligature Unfolding | Structural Artifact Handling | Math / Chemical Protection |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Plain Text** (`.txt`) | CRLF/CR -> LF | Collapse $\ge 2$ spaces to 1 (preserves up to 4 leading spaces for indent) | Preserve double `\n\n` as paragraph breaks; keep single newlines if line indented | Applied if enabled | FormFeed (`\x0C`) converted to `\n\n--- [Page Break] ---\n\n` | 100% (Verbatim ASCII/Unicode symbols) |
| **Markdown** (`.md`) | CRLF/CR -> LF | Collapse outside code blocks; **FROZEN inside code blocks** | Preserve single newlines in lists/tables; double newlines for paragraphs | Applied outside code blocks | Preserve `#`, `-`, `*`, `>`, `\|`, and fenced code blocks (```` ``` ````) | 100% (Preserves `$` and `$$` math blocks) |
| **Delimited Text** (`.csv`, `.tsv`) | CRLF/CR -> LF | **NEVER collapse consecutive commas or tabs** (preserves empty columns) | Preserve rows strictly 1:1; each record on its own line | Disabled | Preserves field delimiters, double-quoted strings, and escaped quotes | 100% (Formulas, numbers, dates intact) |
| **Local HTML** (`.html`, `.htm`) | CRLF/CR -> LF | Collapse consecutive inline spaces to single space | Paragraphs `<p>`, headings `<h1-6>`, lists `<li>` become `\n\n` | Applied | Unescape entities (`&amp;` -> `&`, `&lt;` -> `<`); strip orphan HTML tags | 100% (MathML, sub/sup entities unescaped) |
| **PDF Digital** (`PdfDigital`) | CRLF/CR -> LF | Collapse consecutive spaces to single space | **Soft line wraps joined with space**; terminal punctuation + capitalized next line / blank line forms paragraph | Applied (`ﬁ` -> `fi`, `ﬂ` -> `fl`, etc.) | Strip orphan FormFeed (`\x0C`) and replacement characters (`\uFFFD`) | 100% (Greek `α,β,γ`, sub `H₂O`, sup `x²` preserved) |
| **PDF Scanned** (`PdfScanned`) | CRLF/CR -> LF | Collapse consecutive spaces; trim leading/trailing line whitespace | **Join OCR line fragments**; detect paragraphs via blank lines or indentation | Applied | Fix OCR punctuation spacing (`word , text` -> `word, text`); strip soft hyphens (`\u00AD`) | 100% (Sub/superscript glyphs retained) |
| **PDF Mixed** (`PdfMixed`) | CRLF/CR -> LF | Collapse consecutive spaces | Preserve digital paragraphs; join OCR fragments | Applied | Preserve `[OCR]` delimiter tag as section header with bounding `\n\n` | 100% (Digital and OCR notations preserved) |
| **Word DOCX** (`.docx`) | CRLF/CR -> LF | Collapse consecutive spaces outside tables | Preserve structural headings (`# `), bullet lists (`- `), and tables (`\|`) | Applied | **Reconcile C4 Markdown tokens**: ensure clean `\n\n` padding around headings/tables; do NOT strip `#`, `-`, `\|` | 100% (MathML symbols, sub/sup preserved) |
| **Raster OCR** (PNG, JPG, BMP, WebP) | CRLF/CR -> LF | Collapse consecutive spaces; trim line margins | Join visual wrapped lines into cohesive paragraphs; clean OCR punctuation spacing | Applied | Strip soft hyphens; optional hyphen repair at line break | 100% (Preserve all recognized symbols) |
| **Multi-Frame TIFF OCR** (`.tiff`, `.tif`) | CRLF/CR -> LF | Collapse consecutive spaces per frame | Sequential frame-by-frame paragraph assembly; isolated per page | Applied | Frame-local normalization; no cross-frame text leakage | 100% (Preserve all recognized symbols) |

---

## 2. Character & Glyph Transformation Tables

### 2.1 Typesetting Ligatures
Applied when `TextNormalizationOptions.UnfoldTypesettingLigatures = true`:

| Unicode Codepoint | Ligature Glyph | Normalized Replacement | Unicode Name |
| :---: | :---: | :---: | :--- |
| `\uFB00` | ﬀ | `"ff"` | LATIN SMALL LIGATURE FF |
| `\uFB01` | ﬁ | `"fi"` | LATIN SMALL LIGATURE FI |
| `\uFB02` | ﬂ | `"fl"` | LATIN SMALL LIGATURE FL |
| `\uFB03` | ﬃ | `"ffi"` | LATIN SMALL LIGATURE FFI |
| `\uFB04` | ﬄ | `"ffl"` | LATIN SMALL LIGATURE FFL |
| `\uFB05` | ﬅ | `"ft"` | LATIN SMALL LIGATURE LONG S T |
| `\uFB06` | ﬆ | `"st"` | LATIN SMALL LIGATURE ST |

### 2.2 Whitespace & Non-Printable Characters
Applied in Tier 1 (`Lossless`) and Tier 2 (`Formatting`):

| Character | Codepoint | Transformation (Tier 1) | Transformation (Tier 2) | Rationale |
| :--- | :---: | :---: | :---: | :--- |
| **Byte Order Mark (BOM)** | `\uFEFF` | Stripped | Stripped | Invisible artifact from UTF-8/UTF-16 encoding |
| **Zero-Width Space** | `\u200B` | Stripped | Stripped | Invisible line-break hint |
| **Zero-Width Non-Joiner** | `\u200C` | Stripped | Stripped | Invisible script shaping artifact |
| **Zero-Width Joiner** | `\u200D` | Stripped | Stripped | Invisible script shaping artifact |
| **Left-to-Right Mark** | `\u200E` | Stripped | Stripped | BiDi formatting artifact |
| **Right-to-Left Mark** | `\u200F` | Stripped | Stripped | BiDi formatting artifact |
| **Non-Breaking Space (NBSP)**| `\u00A0` | Preserved | Standard Space `\u0020` | Prevents search mismatch with regular space |
| **Narrow NBSP** | `\u202F` | Preserved | Standard Space `\u0020` | Prevents search mismatch with regular space |
| **Soft Hyphen** | `\u00AD` | Stripped | Stripped | Hidden discretionary hyphen in PDF/OCR |
| **Null / Bell / Backspace** | `\x00`–`\x08` | Stripped | Stripped | Binary / device control artifacts |
| **FormFeed** | `\x0C` | Preserved | Replaced with `\n\n` | Visual page break artifact |
| **Vertical Tab** | `\x0B` | Replaced by `\n` | Replaced by `\n` | Line division |

---

## 3. OCR Punctuation & Spacing Correction Rules

In OCR outputs, recognizers frequently introduce erroneous whitespace immediately before punctuation marks:

| Defective OCR Pattern | Normalized Output | Regular Expression / Match Rule |
| :--- | :--- | :--- |
| `word , next` | `word, next` | `(\w+)\s+([,;:!?])` -> `$1$2` |
| `conclusion .` | `conclusion.` | `(\w+)\s+(\.)(?=\s\|$|\n)` -> `$1$2` |
| `( text )` | `(text)` | `\(\s+` -> `(`, `\s+\)` -> `)` |
| `[ text ]` | `[text]` | `\[\s+` -> `[`, `\s+\]` -> `]` |
| `word - word` | `word - word` | **PRESERVED** (legitimate spaced dash / hyphen) |
| `word- word` | `word-word` | `(\w+)-\s+(\w+)` -> `$1-$2` (if active) |

---

## 4. Scientific, Mathematical & Chemical Invariant Rules

To prevent corruption of academic research papers:

| Content Type | Example Input | Normalization Behavior | Reason |
| :--- | :--- | :--- | :--- |
| **Math Superscripts** | $x^2, 10^{-3}, e^{\pi i}$ | **STRICTLY PRESERVED** | `ApplyUnicodeNfkc = false` prevents flattening $x^2 \to x2$ |
| **Chemical Subscripts** | $\text{H}_2\text{O}, \text{C}_6\text{H}_{12}\text{O}_6, \text{Fe}^{2+}$ | **STRICTLY PRESERVED** | Subscripts $\text{H}_2\text{O}$ must never be changed to $\text{H2O}$ |
| **Greek Letters** | $\alpha, \beta, \gamma, \Delta, \theta, \lambda, \sigma$ | **STRICTLY PRESERVED** | Core scientific variables |
| **Math Operators** | $\int_0^\infty, \sum_{i=1}^n, \sqrt{x}, \le, \ge, \ne, \approx$ | **STRICTLY PRESERVED** | Standard mathematical notation |
| **LaTeX Inline Math** | `$E = mc^2$`, `$\nabla \times B = \mu_0 J$` | **STRICTLY PRESERVED** | Formula delimiters must remain untouched |
| **LaTeX Block Math** | `$$\int_{-\infty}^\infty e^{-x^2} dx = \sqrt{\pi}$$` | **STRICTLY PRESERVED** | Bounded math block |
| **Chemical Arrows** | $\rightarrow, \rightleftharpoons, \leftrightarrow$ | **STRICTLY PRESERVED** | Reaction arrows |
| **Units** | $\mu\text{m}, \text{k}\Omega, \text{\AA}, \text{cm}^3$ | **STRICTLY PRESERVED** | Micro, Ohm, Angstrom symbols |

---

## 5. Line Joining & Paragraph Assembly Rules

### 5.1 Flowing Line Joining (PDF & OCR)
Two consecutive non-empty lines $L_1$ and $L_2$ within a physical page are joined with a space if:
1. $L_1$ does **not** end with terminal punctuation (`.`, `!`, `?`, `:`, or `;`) AND $L_2$ begins with a lowercase letter or number.
2. $L_1$ does **not** match a heading marker (`# `), list bullet (`- `, `* `, `1. `), or table pipe (`|`).
3. $L_2$ does **not** match a heading marker, list bullet, or table pipe.
4. Neither $L_1$ nor $L_2$ is inside a fenced code block (` ``` `).

### 5.2 Paragraph Boundary Preservation
A paragraph boundary (`\n\n`) is preserved between $L_1$ and $L_2$ if:
1. There is an empty line between $L_1$ and $L_2$ in the source text (`\n\n`).
2. $L_1$ ends with terminal punctuation AND $L_2$ is indented by $\ge 2$ spaces or begins a capitalized heading.
3. Either line is a heading (`# `), list item (`- `), or table row (`|`).
