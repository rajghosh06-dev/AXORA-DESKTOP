# AXORA Desktop WinUI — Phase W3-C Product Contract
## Offline OCR & Document Extraction Hardening

**Document Version**: 1.0.0 (Phase W3-C Planning Baseline)  
**Target Platform**: Windows 11 (`net9.0-windows10.0.26100.0`) · C# 13 · Windows App SDK 1.6  
**Status**: **PLANNING CONTRACT ONLY — ZERO PRODUCT CODE MODIFICATIONS**  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Executive Intent & Product Philosophy

In accordance with the [AXORA Product Philosophy](file:///d:/RAJ/GITHUB_REPOSITORY/PROJECTS/AXORA-DESKTOP/docs/AXORA_PRODUCT_PHILOSOPHY.md) and the [Modular Capability Contract](file:///d:/RAJ/GITHUB_REPOSITORY/PROJECTS/AXORA-DESKTOP/docs/AXORA_MODULAR_CAPABILITY_CONTRACT.md), Phase W3-C establishes a trustworthy, page-aware, local-first document extraction engine for Scholar Kit.

The primary product thesis of W3-C is:
> **"Given a user-owned document, extract trustworthy, structured, page-aware content locally and deterministically, while gracefully handling documents that require OCR or optional extraction capabilities."**

Phase W3-C is **NOT** an AI generation phase. It deliberately excludes Large Language Models (LLMs), Small Language Models (SLMs), dense vector embeddings, vector databases, retrieval-augmented generation (RAG), cloud APIs, voice synthesis, and destructive document alterations. Its sole mission is deterministic, structured content extraction that populates the durable [W3-B persistence entities](file:///d:/RAJ/GITHUB_REPOSITORY/PROJECTS/AXORA-DESKTOP/docs/W3_SCHOLAR_KIT_DATA_MODEL.md) (`ScholarDocument`, `DocumentPage`, `DocumentPassageChunk`).

---

## 2. Concrete User Problems Solved

| Problem ID | Real User Problem | Current Flaw / Pain Point | W3-C Solution |
| :--- | :--- | :--- | :--- |
| **UP-1** | **Silent Failures on Scanned PDFs** | Scanned PDFs with no embedded text return blank screens or cryptic "[No text detected]" with zero explanation, confusing students. | PDF stream sniffer inspects page content; if digital text is absent but raster images exist, it dispatches on-device OCR with observable progress. |
| **UP-2** | **Destruction of Page Boundaries** | Existing extraction flattens entire multi-page documents into a single unstructured string, losing physical page context. | Strict 1:1 page modeling preserves `PageNumber`, dimensions (`WidthPt`, `HeightPt`), and per-page text segments. |
| **UP-3** | **Text Encoding & Formatting Corruption** | Broken line breaks, soft hyphens (`hy-\nphen`), ligatures (`ﬁ`, `ﬂ`), control chars (`\0`), and Windows-1252/ANSI glitches corrupt extracted study notes. | Deterministic text normalizer repairs line reflow, normalizes Unicode (NFKC), strips non-printable control codes, and de-hyphenates cleanly without destructive rewriting. |
| **UP-4** | **Unstructured Format Ingestion** | Students frequently study from Markdown notes, Word syllabi (`.docx`), plain text transcripts (`.txt`), or tabular datasets (`.csv`), which currently cannot be ingested. | Native, zero-cloud format extractors for TXT, CSV/TSV, Markdown, HTML, and DOCX seamlessly parse content into structured `ScholarDocument` records. |
| **UP-5** | **Monolithic or Arbitrary Passage Chunking** | Previous chunking created arbitrary single slices (e.g. 400 chars) that severed sentences mid-word and ignored page boundaries. | Page-aware sliding-window chunker (350-character target, 60-character stride overlap) snaps to natural sentence/paragraph boundaries without crossing page borders. |
| **UP-6** | **Source File Corruption Anxiety** | Students worry that software importing their research papers or dissertations might modify, move, or corrupt their original files. | Strict source immutability invariant: original files are opened strictly read-only; zero bytes are ever written to or deleted from user sources. |

---

## 3. Supported Source Categories

W3-C classifies user sources into four primary categories:

```
                                 USER SOURCE DOCUMENT
                                          │
         ┌──────────────────┬─────────────┴───────────────┬──────────────────┐
         ▼                  ▼                             ▼                  ▼
┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│  DIGITAL TEXT    │ │  SCANNED / RASTER│ │  MIXED HYBRID    │ │  STRUCTURED DOC  │
│  - PDF (Digital) │ │  - Scanned PDF   │ │  - PDF with text │ │  - DOCX (OpenXML)│
│  - TXT (UTF8/16) │ │  - PNG / JPEG    │ │    + scanned     │ │  - Markdown      │
│  - CSV / TSV     │ │  - TIFF / BMP    │ │    pages         │ │  - Local HTML    │
│  - Pasted Text   │ │  - WIA Scanner   │ │                  │ │                  │
└──────────────────┘ └──────────────────┘ └──────────────────┘ └──────────────────┘
```

1. **Digital Text Documents**:
   - PDF documents with native digital text streams and valid font CMaps.
   - Plain text files (`.txt`) supporting UTF-8 (with/without BOM), UTF-16LE, UTF-16BE, ASCII, and ANSI (Windows-1252).
   - Delimited data files (`.csv`, `.tsv`) representing tables and scientific data logs.
   - User-pasted clipboard text and system notes.
2. **Scanned / Raster Image Documents**:
   - Image-only scanned PDFs containing full-page raster scans.
   - Standalone graphic files (`.png`, `.jpg`, `.jpeg`, `.bmp`, `.tiff`).
   - Multi-frame image archives (multi-page `.tiff`).
   - Hardware scanner inputs captured via Windows Image Acquisition (WIA).
3. **Mixed Hybrid Documents**:
   - Compound PDFs containing digital vector/text on some pages and bitmap scans or photographs on other pages.
   - Single pages with both digital paragraphs and embedded raster diagrams/tables.
4. **Structured Markup Documents**:
   - Microsoft Word OpenXML (`.docx`) containing paragraph hierarchies, section breaks, and data tables.
   - Academic notes authored in CommonMark / GitHub Flavored Markdown (`.md`, `.markdown`).
   - Local standalone HTML study guides (`.html`, `.htm`) stripped of executable scripts and stylesheets.

---

## 4. Modular Capability Classification

W3-C adheres strictly to the AXORA capability taxonomy:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          CAPABILITY CLASSIFICATION                          │
├───────────────────────────────────┬─────────────────────────────────────────┤
│ CORE LOCAL (Bundled Native Engine)│ - Pure C# / WinUI runtimes              │
│ Zero external downloads           │ - Text format sniffers (BOM, magic)     │
│ Zero model dependencies           │ - PdfSharpCore digital text extractor   │
│ 100% offline & immediate          │ - DOCX OpenXML zip/xml stream parser    │
│                                   │ - CSV / Markdown / HTML text parsers    │
│                                   │ - Deterministic text normalizer         │
│                                   │ - Page-aware passage chunking engine    │
│                                   │ - W3-B local JSON persistence           │
├───────────────────────────────────┼─────────────────────────────────────────┤
│ SYSTEM-PROVIDED (OS Capabilities) │ - Windows.Media.Ocr.OcrEngine           │
│ Provided by Windows 11 platform   │ - Windows installed OS language packs   │
│ Fully local & on-device           │ - Windows Image Acquisition (WIA) COM   │
├───────────────────────────────────┼─────────────────────────────────────────┤
│ OPTIONAL LOCAL (Modular Plugins)  │ - Advanced multi-lingual OCR packs      │
│ Downloaded via Capability Manager │ - Specialized table / formula extractors│
│ Persisted in %APPDATA%\Axora\     │ - Heavy machine-vision layout models    │
│ Capabilities\                     │ - (Deferred to W3-C.4 and beyond)       │
├───────────────────────────────────┼─────────────────────────────────────────┤
│ OPTIONAL NETWORK (Cloud Services) │ - STRICTLY FORBIDDEN IN W3-C            │
│ Cloud APIs / external servers     │ - No data leaves the device             │
└───────────────────────────────────┴─────────────────────────────────────────┘
```

---

## 5. Input & Output Contract

### Ingestion Inputs
1. `Stream` or `string filePath`:
   - Must be accessible for read operations (`FileAccess.Read`, `FileShare.ReadWrite`).
   - May be provided via native file picker, shell drag-and-drop, clipboard stream, or WIA scan.
2. `ExtractionOptions`:
   - `ForceOcr` (`bool`, default `false`): Dispatches OCR even if low-confidence digital text exists.
   - `OcrLanguage` (`string?`, default `null`): Desired BCP-47 language tag; falls back to OS user profile.
   - `MaxPagesToExtract` (`int`, default `1000`): Protection against pathological file expansion.
   - `MaxDegreeOfParallelism` (`int`, default `Environment.ProcessorCount`): Thread pool governor for multi-page extraction.
   - `CancellationToken` (`CancellationToken`): Graceful user cancellation handle.

### Extraction Outputs
The pipeline produces a validated, durable aggregate root conforming directly to the accepted W3-B persistence schema:
- **`ScholarDocument`**: Contains document metadata, file size, hash, page count, and page collection.
- **`IReadOnlyList<DocumentPage>`**: Sequence of 1-indexed pages containing physical dimensions, raw text, and passage chunks.
- **`IReadOnlyList<DocumentPassageChunk>`**: Normalized, page-bounded passages with exact character offsets.
- **`ExtractionReport`**: Metadata diagnostic report detailing:
  - Extractor engine used (e.g., `DigitalPdfExtractor`, `WinRtOcrExtractor`).
  - Total elapsed duration.
  - Page-by-page extraction outcome (e.g., Page 1: Digital; Page 2: OCR; Page 3: Empty).
  - Warnings encountered (e.g., unrecognized font encoding, low-confidence characters, unsupported ligatures).

---

## 6. Data Ownership, Security & Privacy Boundary

1. **Source Immutability**:
   - The user's original document is strictly read-only.
   - AXORA never re-saves, edits, moves, renames, or deletes the original source file.
   - All extracted representations and study artifacts reside exclusively in `%APPDATA%\Axora\Scholar\`.
2. **Zero Network Egress**:
   - No text, image, metadata, or diagnostic metric is ever sent over the network.
   - The extraction engine functions identically with all network adapters disabled.
3. **Local Storage Sandboxing**:
   - All file writes are strictly bounded within `%APPDATA%\Axora\Scholar\`.
   - File identifiers are validated to prohibit directory traversal (`../`, `..\`), root escapes, and NTFS stream exploitation.
4. **Log Sanitization**:
   - Application diagnostic logs record operational outcomes, page counts, durations, and non-sensitive error codes.
   - Extracted document text, user notes, and file contents are never written to log files.

---

## 7. Graceful Degradation & Fallback Policies

```
                                [ Ingestion Source ]
                                         │
                                         ▼
                             [ Format Detection ]
                                         │
                      ┌──────────────────┴──────────────────┐
                      ▼                                     ▼
             [ PDF Document ]                       [ Raster Image ]
                      │                                     │
                      ▼                                     ▼
         Does Page Have Digital Text?               Is WinRT OCR Ready?
             │                 │                            │            │
            YES                NO                          YES           NO
             │                 │                            │            │
             ▼                 ▼                            ▼            ▼
     [ Fast Digital     Is WinRT OCR Ready?           [ Execute     [ Surface
       Text Extract ]      │            │               WinRT OCR ]   Actionable
             │            YES           NO                  │         Warning:
             │             │            │                   │         Install OS
             │             ▼            ▼                   │         Language
             │         [ Execute    [ Surface               │         Pack ]
             │           WinRT OCR]   Actionable            │            │
             │             │          Warning:              │            │
             │             │          Image-Only PDF,       │            │
             │             │          OCR Unavailable ]     │            │
             │             │            │                   │            │
             └─────────────┼────────────┘                   │            │
                           ▼                                ▼            ▼
             [ Page-Aware Normalization ] <─────────────────┴────────────┘
                           │
                           ▼
             [ Deterministic Passage Chunking ]
                           │
                           ▼
             [ W3-B Persistent Library ]
```

1. **OCR Unavailable Fallback**:
   - If a document requires OCR (scanned PDF or bitmap image) and no OCR engine is available (e.g., missing Windows language pack):
     - The document record is created with `DocumentFormatType.Pdf` or `DocumentFormatType.ImageOcr`.
     - The page text is populated with a standardized, non-crashing diagnostic marker:
       `[AXORA OCR Notice: This page contains scanned image content, but no compatible local OCR engine was found. Please install an English or regional Windows language pack in Windows Settings > Time & Language > Language & Region].`
     - The UI surfaces an actionable InfoBar with severity `Warning` and a direct link to Windows Language Settings.
     - The application never crashes, and valid digital pages within the same document remain completely readable.
2. **Partial Page Failure Fallback**:
   - In a 50-page PDF, if page 12 contains a corrupted font CMap or unreadable JPEG stream:
     - Page 12 records `RawText = "[Extraction warning: Unable to parse font stream on page 12]"`.
     - Pages 1–11 and 13–50 continue extraction and persist normally.
     - The overall extraction succeeds with a `PartialSuccess` status and logs the specific page warning.

---

## 8. Failure Semantics & Error Code Taxonomy

All extraction exceptions derive from a unified domain exception base: `ScholarExtractionException`.

| Error Code | Meaning | User Feedback | Recovery Action |
| :--- | :--- | :--- | :--- |
| `ERR_FORMAT_UNRECOGNIZED` | File magic bytes do not match any supported signature. | "Unsupported file format. Please import a PDF, Word document, text file, or image." | Reject file gracefully; reset drop zone. |
| `ERR_FILE_CORRUPTED` | Malformed file structure (e.g. truncated PDF EOF or invalid ZIP directory). | "The selected file appears damaged or incomplete and cannot be opened." | Quarantine attempt; log non-sensitive offset. |
| `ERR_FILE_PASSWORD_PROTECTED` | PDF is encrypted with a user/owner password. | "This document is password-protected. Please provide an unprotected PDF." | Prompt user; do not attempt brute-force. |
| `ERR_FILE_SIZE_LIMIT_EXCEEDED` | File exceeds maximum allowable threshold (250 MB). | "File exceeds the 250 MB limit for local processing." | Block processing; advise splitting file. |
| `ERR_PAGE_LIMIT_EXCEEDED` | Document exceeds maximum page limit (1,000 pages). | "Document exceeds the 1,000 page limit. The first 1,000 pages will be processed." | Process up to limit or prompt user. |
| `ERR_IMAGE_DIMENSIONS_EXCEEDED` | Image dimensions exceed 16,384 x 16,384 px. | "Image resolution is too large for local memory allocation." | Downsample safely or reject. |
| `ERR_OCR_UNAVAILABLE` | OS language pack missing or COM registration broken. | "Windows OCR engine is unavailable. Check installed language packs." | Show InfoBar with Settings navigation. |
| `ERR_OCR_INTERNAL_FAILURE` | Native `RecognizeAsync` returned null or threw internal HRESULT. | "OCR recognition encountered an error processing this image." | Log HRESULT; mark page with warning. |
| `ERR_EXTRACTION_CANCELLED` | User clicked Cancel during multi-page extraction. | "Extraction cancelled by user." | Clean up temporary staging; discard in-flight. |

---

## 9. Acceptance Criteria for Phase W3-C

Phase W3-C will be considered formally complete **only** when all of the following criteria are satisfied:

1. **Multi-Format Extraction**:
   - Deterministic, page-aware text extraction implemented for: Digital PDF, Plain Text (UTF-8/UTF-16/ASCII/ANSI), Delimited Text (CSV/TSV), Markdown, HTML, and DOCX.
2. **Page Semantics Preservation**:
   - Physical page boundaries strictly preserved for PDF documents (1:1 mapping).
   - Logical section/page boundaries preserved for DOCX and text formats.
   - Single image files mapped strictly to `PageNumber = 1`.
   - Empty pages preserved with `RawText = string.Empty` without altering document page numbering.
3. **Deterministic Text Normalization**:
   - CRLF line endings normalized to uniform `\n`.
   - Unicode normalized to NFKC without altering mathematical/scientific symbols.
   - Non-printable control characters removed.
   - Hyphenation across line breaks repaired.
   - Zero LLM rewriting; 100% preservation of author's original words.
4. **Passage Chunking Engine**:
   - Generates chunks with target 350 characters and 60-character stride overlap.
   - Chunks strictly respect page boundaries (never cross pages).
   - Chunks track exact, verifiable character offsets `[StartCharOffset, EndCharOffset]`.
5. **OCR Boundary & Degradation**:
   - Scanned PDFs automatically detect lack of text and trigger WinRT OCR per page.
   - Mixed PDFs correctly extract digital text from digital pages and run OCR on scanned pages.
   - Missing OCR language packs display actionable warning InfoBars without crashing.
6. **W3-B Persistence Integration**:
   - Extracted `ScholarDocument` instances persist directly into `%APPDATA%\Axora\Scholar\documents\` with `schemaVersion = 1`.
   - Session saves link cleanly to ingested document IDs.
7. **Verification & Regression**:
   - Complete existing test suite (632 assertions) remains 100% green.
   - New deterministic unit and integration tests added for all formats, page builders, normalizers, and failure paths.
   - Live GUI runtime test proves end-to-end ingestion and persistence.
8. **Git Governance**:
   - Baseline HEAD preserved at `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`.
   - Zero staged files, zero commits, zero pushes.
   - `Axora-Desktop-MaterialUI` completely untouched.
