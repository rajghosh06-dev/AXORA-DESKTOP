# AXORA Desktop WinUI — Phase W3-C Format Extraction Matrix
## Comprehensive Specification of Document Formats, Encodings, Engines & Fallback Behaviors

**Document Version**: 1.1.0 (Phase W3-C Format Matrix Revision)  
**Target Platform**: Windows 11 (`net9.0-windows10.0.26100.0`) · C# 13 · Windows App SDK 1.6  
**Status**: **PLANNING SPECIFICATION ONLY — ZERO IMPLEMENTATION COMMENCED**  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Master Extraction Matrix

The following matrix formally defines how every candidate format is detected, parsed, paginated, and processed across the W3-C pipeline:

| Format Class | File Extensions | Detection Signature (Magic Bytes) | Primary Extraction Engine | Page Semantics Type | OCR Required? | Determinism Rating | Capability Tier | Fallback Strategy | Error Behavior |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Searchable PDF** | `.pdf` | `%PDF-` (`25 50 44 46`) | `IPdfDocumentExtractorEngine` (Baseline: `PdfSharpCore` Digital Stream Parser) | `PhysicalPage` (1:1 mapping) | No | Fully Deterministic | Core Local | Fallback to OCR if font CMap unreadable | Surface `ERR_FILE_CORRUPTED` if syntax invalid |
| **Scanned PDF** | `.pdf` | `%PDF-` (`25 50 44 46`) | `IOcrService` (Baseline: `WinRtOcrService` via Rendered Bitmap) | `PhysicalPage` (1:1 mapping) | Yes | Reproducible within fixed engine/runtime config | System-Provided (OS Pack) | Non-crashing notice marker if OCR missing | Surface `ERR_OCR_UNAVAILABLE` |
| **Mixed PDF** | `.pdf` | `%PDF-` (`25 50 44 46`) | Hybrid Dispatcher (`IPdfDocumentExtractorEngine` + `IOcrService`) | `PhysicalPage` (1:1 mapping) | Selective (scanned pages only) | Fully deterministic for digital; reproducible for OCR | System-Provided (OS Pack) | Digital pages extract; scanned pages display notice | `PartialSuccess` with per-page warning |
| **Plain Text** | `.txt` | BOM or Utf8/Ascii heuristic | `PlainTextExtractorEngine` | `VirtualPage` (~3,000 chars or `\f`) | No | Fully Deterministic | Core Local | Windows-1252 / ANSI fallback | Surface `ERR_UNSUPPORTED_ENCODING` |
| **Delimited Text** | `.csv`, `.tsv` | Text with `,` or `\t` cadence | `DelimitedTextExtractorEngine` (Bounded Tabular) | `LogicalSection` (Single document / table) | No | Fully Deterministic | Core Local | Raw plain text fallback if rows malformed | Surface malformed row warnings |
| **Markdown** | `.md`, `.markdown` | UTF-8 text with `#`, `*`, `[` | `MarkdownExtractorEngine` (Bounded Structural) | `LogicalSection` (Headings / Sections) | No | Fully Deterministic | Core Local | Plain text fallback | Fall back to unparsed plain text |
| **Local HTML** | `.html`, `.htm` | `<!DOCTYPE` or `<html>` | `LocalHtmlExtractorEngine` (Local-Only DOM Stripper) | `LogicalSection` (Article / Section / Headings) | No | Fully Deterministic | Core Local | Plain text regex tag stripper | Strip unparseable tags |
| **Word DOCX** | `.docx` | `PK\x03\x04` + `[Content_Types].xml` | `DocxDocumentExtractorEngine` (OpenXML) | `LogicalSection` (Section / Explicit Break mapping; Physical page count NOT promised) | No | Fully Deterministic | Core Local | Extract raw text from `document.xml` | Surface `ERR_FILE_CORRUPTED` if ZIP invalid |
| **Single Image** | `.png`, `.jpg`, `.jpeg`, `.bmp` | PNG/JFIF/BMP magic headers | `ImageOcrExtractorEngine` (`IOcrService`) | `PhysicalPage` (`PageNumber = 1`) | Yes | Reproducible within fixed engine/runtime config | System-Provided (OS Pack) | Explanatory OCR required notice | Surface `ERR_OCR_UNAVAILABLE` |
| **Multi-Frame Image** | `.tiff`, `.tif` | `II*\x00` (LE) or `MM\x00*` (BE) | `ImageOcrExtractorEngine` (Frame Enumeration) | `PhysicalPage` (1 frame = 1 page) | Yes | Reproducible within fixed engine/runtime config | System-Provided (OS Pack) | Extract valid frames; report unparseable | `PartialSuccess` if some frames decode |
| **Binary / Unknown** | Any other | Arbitrary bytes | Rejected by `DocumentFormatDetector` | None | N/A | Fully Deterministic | N/A | None | Rejection with `ERR_FORMAT_UNRECOGNIZED` |

---

## 2. Format Sniffing & Detection Specification

File extensions are treated strictly as non-binding hints. Format detection uses content-based sniffing:

### 2.1. Magic Byte Inspection Table
```
Hex Signature                           Format Classification
─────────────────────────────────────────────────────────────────────────────
25 50 44 46                             PDF Document (%PDF)
50 4B 03 04                             ZIP Archive (Candidate DOCX)
89 50 4E 47 0D 0A 1A 0A                 PNG Graphic File
FF D8 FF                                JPEG Graphic File
42 4D                                   BMP Bitmap Graphic
49 49 2A 00                             TIFF Graphic (Little Endian / Intel)
4D 4D 00 2A                             TIFF Graphic (Big Endian / Motorola)
EF BB BF                                UTF-8 Text with BOM
FE FF                                   UTF-16 Big Endian Text with BOM
FF FE                                   UTF-16 Little Endian Text with BOM
```

### 2.2. DOCX Signature Verification
To prevent misclassifying generic `.zip` archives or malicious files as Word documents:
1. File must begin with `PK`.
2. Parser inspects the ZIP central directory for mandatory OpenXML parts:
   - `[Content_Types].xml`
   - `word/document.xml`
3. If both parts are verified, the format is declared `DetectedDocumentFormat.Docx`. Otherwise, it is classified as unsupported binary.

### 2.3. Character Encoding Sniffing Algorithm (Plain Text)
When a text file lacks a Byte Order Mark (BOM):
1. **UTF-8 Validation**: The engine tests the byte stream against RFC 3629 UTF-8 state transitions.
2. **ASCII Check**: If all bytes are `<= 0x7F`, the stream is treated as standard 7-bit ASCII.
3. **ANSI / Windows-1252 Fallback**: If invalid UTF-8 sequences are encountered, the engine falls back to Windows-1252 with diagnostic logging, preventing unhandled decoding exceptions.

---

## 3. Deep-Dive Format Specifications

### 3.1. Searchable Digital PDF (`.pdf`)
- **Engine Abstraction**: `IPdfDocumentExtractorEngine` (Baseline: `PdfSharpCore` content stream parser).
- **Existing Implementation**: Leverages `PdfExtractionService` and `PdfTextExtractor`.
- **Known Limitations & Fallback Strategy**:
  - `PdfSharpCore` lacks native rasterization and has limited CMap support for certain CID-keyed composite fonts.
  - Decoupled behind `IPdfDocumentExtractorEngine` to permit future drop-in evaluation of alternative engines (e.g. `Windows.Data.Pdf.PdfDocument` or PDFium).
- **Invariants**:
  - Does NOT alter the original PDF file.
  - Does NOT execute PDF JavaScript (`/JS` objects are ignored).
  - Skips embedded binary attachments.
  - Preserves exact physical page count (1:1 mapping with `PageSemanticsType.PhysicalPage`).

### 3.2. Scanned & Image-Only PDF (`.pdf`)
- **Detection**: A page is classified as scanned if extracted digital text contains fewer than 50 characters AND the page's `/Resources/XObject` dictionary contains one or more `/Subtype /Image` entries.
- **Rendering Pipeline**: The page is rasterized into a high-resolution bitmap (target 300 DPI) in memory.
- **OCR Execution**: Rendered image buffer is passed to `IOcrService.ExtractTextAsync()`.
- **Reproducibility**: Text recognition output is reproducible within a fixed OCR engine, language pack version, and OS build configuration.

### 3.3. Microsoft Word OpenXML (`.docx`)
- **Engine**: Native streaming XML reader over `System.IO.Compression.ZipArchive`. Zero dependency on Microsoft Office or COM automation.
- **Page Semantics**: **Flow-based logical sections** (`PageSemanticsType.LogicalSection`). Physical page breaks in DOCX are renderer-dependent (varying by printer, font rasterizer, and margins). The engine parses:
  - `<w:p>`: Paragraph boundary.
  - `<w:t>`: Text run.
  - `<w:tab>`: Tabular spacing (`	`).
  - `<w:br>`: Line break (`
`).
  - `<w:lastRenderedPageBreak>` / `<w:br w:type="page">`: Explicit author/saved page break.
  - `<w:sectPr>`: Section break boundary.
  - `<w:tbl>`: Table node; formatted into structured Markdown tables.
- **Physical Page Invariant**: AXORA explicitly documents to users that DOCX page divisions represent logical document sections rather than exact printed sheets.

### 3.4. Delimited Text (`.csv`, `.tsv`)
- **Engine**: Native streaming RFC 4180 parser.
- **Dialect Handling**: Autodetects separator (comma `,` vs tab `	` vs semicolon `;`).
- **Quoting**: Handles escaped quotes (`""`) and multiline fields enclosed in quotes.
- **Bounded Extraction**: Capped at configurable limits (default: 50,000 rows, 100 columns) to prevent memory exhaustion.
- **Representation**: Formats rows into clean Markdown tables with column headers for high-fidelity rendering.

### 3.5. CommonMark / GFM Markdown (`.md`, `.markdown`)
- **Engine**: Line-based structural parser.
- **Structure Extraction**: Identifies headings (`#` through `######`), bullet lists (`-`, `*`), numbered lists, and fenced code blocks (` ``` `).
- **Metadata Handling**: Extracts YAML frontmatter (`---`) into `ScholarDocument.Metadata` without polluting body text.
- **Page Semantics**: Mapped to `PageSemanticsType.LogicalSection` based on top-level headings (`#`) or horizontal rules (`---`).

### 3.6. Local HTML (`.html`, `.htm`)
- **Security Invariant**: Strictly local-only parsing.
  - Zero network requests.
  - Zero remote stylesheet, image, font, or script fetching.
  - Zero JavaScript execution.
- **Tag Sanitization**: Completely strips `<script>`, `<style>`, `<iframe>`, `<object>`, `<embed>`, and all event handler attributes (`onload`, `onclick`).
- **Structure**: Extracts text from semantic tags (`<article>`, `<main>`, `<section>`, `<h1>`..`<h6>`, `<p>`).
- **Entities**: Resolves HTML5 character entities (`&nbsp;`, `&amp;`, `&lt;`, `&gt;`, `&quot;`).

---

## 4. Unsupported Format Handling

Any file matching the following criteria is strictly rejected at Stage 1:
- Executable binaries (`.exe`, `.dll`, `.sys`, `.so`).
- Audio/Video media files (`.mp3`, `.wav`, `.mp4`, `.mkv`).
- Generic compression archives without document payloads (`.zip`, `.rar`, `.7z`, `.tar`, `.gz`).
- Encrypted or password-protected archives.

**Rejection Protocol**:
1. Pipeline throws `UnsupportedDocumentFormatException(fileName, detectedMimeType)`.
2. UI displays an actionable InfoBar:
   `"AXORA cannot open '[FileName]' because it is not a supported document format. Supported formats include PDF, Word (.docx), Plain Text, Markdown, CSV, and Images."`
3. Zero temporary files are created; zero persistence records are written.
