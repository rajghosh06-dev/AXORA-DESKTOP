# Phase W3-C.5: On-Device OCR Integration & Image-Based Extraction — Product Contract

**AXORA Desktop — WinUI Native Academic Workspace**  
**Document ID:** `AXORA-CONTRACT-W3C5-001`  
**Phase:** `W3-C.5` (On-Device OCR Integration & Image Extraction)  
**Target Project:** `Axora-Desktop-WinUI`  
**Status:** `RECONCILED C5.1 CONTRACT — IMPLEMENTATION NOT COMMENCED`  
**Protected Git Baseline:** `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Scope, Philosophy & Non-Goals

### 1.1 Core Philosophy
Phase **W3-C.5** establishes on-device, capability-driven optical character recognition (OCR) and raster image extraction for AXORA Scholar Kit. In alignment with the [AXORA Product Philosophy](file:///d:/RAJ/GITHUB_REPOSITORY/PROJECTS/AXORA-DESKTOP/docs/AXORA_PRODUCT_PHILOSOPHY.md), OCR operations must adhere strictly to five pillars:
1. **Local-First & Offline-Capable:** 100% of recognition executes on the local host CPU/GPU. Zero network connectivity, zero cloud telemetry, zero remote model dependencies.
2. **Capability-Driven:** OCR availability is treated as an observable, hardware/OS-dependent capability. Absence of OCR or a specific language pack degrades gracefully without crashing or corrupting documents.
3. **User-Controlled:** OCR runs on-demand or per configured user extraction policy. It never silently modifies files or runs unexpected background uploads.
4. **Source Immutability:** User source documents (PDFs, images, TIFFs) are strictly opened read-only and never modified, overwritten, or locked against external applications.
5. **Two-Tier Text Invariant:** OCR-recognized characters are captured as `RawText` ground truth on physical pages (`ExtractedViaOcr = true`). `NormalizedText` is strictly initialized to `null`, deferring tokenization and glyph normalization to Phase W3-C.6.

### 1.2 In-Scope Items (Subphases W3-C.5.1 to W3-C.5.7)
- **OCR Capability Provider:** Concrete implementation of `IOcrCapabilityStateProvider` detecting installed system language packs and reporting capability readiness states (`OcrAvailable`, `OcrUnavailable`, `OcrLanguageUnavailable`, `OcrFailed`).
- **OCR Engine Abstraction:** Concrete implementation of `IOcrEngine` (`WindowsMediaOcrEngine`) adapting platform OCR facilities without leaking platform-specific types across the service boundary.
- **Raster Image Extractor:** Implementation of `IDocumentExtractorEngine` for standalone raster images (`.png`, `.jpg`, `.jpeg`, `.bmp`, `.webp`) producing single-page physical representations (`PageSemanticsType.PhysicalPage`).
- **Multi-Frame TIFF Processing:** Handling of multi-page TIFF images (`.tif`, `.tiff`) via WIC (`Windows.Graphics.Imaging.BitmapDecoder`), decomposing frames into discrete, sequential physical pages.
- **Page-Aware PDF OCR Dispatch:** Hybrid integration with `PdfDocumentExtractorEngine` (from W3-C.3) to selectively dispatch scanned or sparse pages through `IPdfPageRasterizer` and `IOcrEngine`.
- **Orientation Normalization:** Detection of EXIF orientation and in-memory upright correction via `ExifOrientationNormalizer` prior to recognition, preserving original source files.
- **Bounded Resource Governors:** Strict capping of image dimensions (max 10,000 px for Windows OCR) and per-page raster buffer disposal to prevent memory exhaustion.

### 1.3 Strict Non-Goals (Explicitly Deferred / Prohibited)
- **NO Cloud OCR:** Absolutely zero Google Vision API, Azure Computer Vision, AWS Textract, or external web service calls.
- **NO Third-Party OCR NuGet Packages:** No Tesseract OCR native binaries, no Leptonica, no Python runtimes, and no external ONNX vision transformers in W3-C.5 baseline.
- **NO Silent Replacement of Digital Text:** A PDF page bearing valid digital text must NEVER be discarded or overwritten by OCR text unless the user explicitly passes `ExtractionOptions.ForceOcr = true`.
- **NO Text Normalization / Chunking:** `NormalizedText` must remain `null`. Passage chunking (`IPassageChunker`) and RAG ingestion belong to Phases W3-C.6 and W3-C.7.
- **NO UI Redesign:** No new tabs, no custom OCR settings controls, no ViewModel re-architecting in W3-C.5.
- **NO Capability Manager Implementation:** The future capability manager infrastructure remains unbuilt; W3-C.5 only exposes the standard `IOcrCapabilityStateProvider` contract.

---

## 2. Capability Classes & Operating System Integration

AXORA classifies OCR resources according to the approved [Modular Capability Contract](file:///d:/RAJ/GITHUB_REPOSITORY/PROJECTS/AXORA-DESKTOP/docs/AXORA_MODULAR_CAPABILITY_CONTRACT.md):

```
┌────────────────────────────────────────────────────────────────────────┐
│                   AXORA Scholar Capability Boundary                    │
├───────────────────┬────────────────────────────────────────────────────┤
│ Capability Tier   │ Components & Runtime Rules                         │
├───────────────────┼────────────────────────────────────────────────────┤
│ CORE LOCAL        │ • PDF SharpCore vector extraction (W3-C.3)         │
│ (Always Present)  │ • WindowsPdfPageRasterizer (Windows.Data.Pdf)      │
│                   │ • ExifOrientationNormalizer (SkiaSharp)            │
│                   │ • IOcrEngine & IOcrCapabilityStateProvider         │
│                   │ • Deterministic Fallbacks (Corrupt / Size Bounds)  │
├───────────────────┼────────────────────────────────────────────────────┤
│ SYSTEM-PROVIDED   │ • Windows.Media.Ocr.OcrEngine runtime APIs         │
│ (Environment-Dep) │ • OS Language Packs (installed via Windows Lang)   │
│                   │ • Hardware-accelerated WinRT Bgra8 decoders        │
├───────────────────┼────────────────────────────────────────────────────┤
│ OPTIONAL LOCAL    │ • Future offline language packs (e.g. Tesseract)   │
│ (Deferred)        │ • Future DirectML vision transformers (Surya/Layout│
│                   │ • Future mathematical formula / table OCR          │
├───────────────────┼────────────────────────────────────────────────────┤
│ NETWORK / CLOUD   │ • EXCLUDED BY ARCHITECTURAL LAW                    │
│ (Forbidden)       │ • Zero telemetry, zero web extraction APIs         │
└───────────────────┴────────────────────────────────────────────────────┘
```

---

## 3. OCR State Model & Lifecycle

OCR readiness follows the five-state lifecycle model defined in Phase W3-C.1 (`OcrCapabilityState`):

```
                       ┌─────────────────────────┐
                       │      Application /      │
                       │    System Inspection    │
                       └────────────┬────────────┘
                                    │
                                    ▼
                     ┌─────────────────────────────┐
                     │ OcrEngine.AvailableLanguages│
                     │          Count > 0?         │
                     └──────┬───────────────┬──────┘
                       Yes  │               │ No
                            │               ▼
                            │    ┌────────────────────┐
                            │    │   OcrUnavailable   │
                            │    └────────────────────┘
                            ▼
               ┌───────────────────────────┐
               │ Target Language Installed?│
               └────┬─────────────────┬────┘
                Yes │                 │ No
                    ▼                 ▼
          ┌───────────────────┐ ┌───────────────────────────┐
          │   OcrAvailable    │ │  OcrLanguageUnavailable   │
          └─────────┬─────────┘ └───────────────────────────┘
                    │
            Execute Recognition
                    │
         ┌──────────┴──────────┐
         ▼                     ▼
┌─────────────────┐   ┌─────────────────┐
│     Success     │   │    OcrFailed    │ (Unhandled native/OS error)
└─────────────────┘   └─────────────────┘
         │
         ▼ (Multi-page batch with partial failures)
┌─────────────────────────┐
│  OcrPartiallyCompleted  │
└─────────────────────────┘
```

### Exact State Semantics & Boundary Rules:
- **`OcrAvailable` (0):** The OCR engine is functional, OS language resources are present, and the requested (or default) language pack is installed and ready for invocation.
- **`OcrUnavailable` (1):** No OCR runtime capability exists on the operating system, or `AvailableRecognizerLanguages` has zero installed language packs.
- **`OcrLanguageUnavailable` (2):** The host OS supports OCR, but the explicitly requested BCP-47 language tag (e.g. `"ja-JP"`) is not installed in the OS language store.
- **`OcrFailed` (3):** The OCR engine threw an exception during initialization or execution of a specific image/page.
- **`OcrPartiallyCompleted` (4):** In a multi-page document or multi-frame TIFF, some pages were successfully extracted via OCR while others encountered failures or exceeded resource bounds. **`OcrPartiallyCompleted` is strictly a batch page outcome and must NEVER be used to represent low character recognition confidence.**

---

## 4. Ground Truth & Provenance Invariants

When OCR extracts text from an image or scanned page:

1. **Page Association:** Every OCR result maps to exactly one `ExtractedPageRaw` instance with:
   - `PageNumber`: Physical 1-indexed page or image frame number.
   - `PageSemantics`: Strictly `PageSemanticsType.PhysicalPage`.
   - `WidthPt` & `HeightPt`: Physical dimensions of the image or PDF page in points (72 DIP/pt).
   - `RawText`: Exact text recognized from the OCR engine lines, separated by newline characters (`\n`).
   - `NormalizedText`: Strictly `null`.
   - `ExtractedViaOcr`: Strictly `true`.
   - `Confidence`: Bounded floating-point value `[0.0, 1.0]` representing recognition reliability.
   - `DiagnosticWarning`: Contains the recognition engine identifier, language tag used, and any dimension scaling notes.
2. **Provenance Granularity:**
   `ExtractedViaOcr = true` is a primary flag, but the complete provenance record includes:
   - `EngineIdentifier` (e.g. `"WindowsMediaOcrEngine"`).
   - `RequestedLanguage` (e.g. `"en-US"` or `"auto"`).
   - `ActualLanguage` (BCP-47 tag from recognizer, e.g. `"en-US"`).
   - `TextAngle` (orientation angle detected by OCR engine, if reported).
   - `ExifOrientationApplied` (orientation transform applied during image prep).
3. **Cataloging W3-C.4 Finding (Structural Markdown in RawText):**
   - In Phase W3-C.4, DOCX extraction introduced Markdown structural tokens (`# `, `- `, `|`) into `RawText`.
   - In Phase W3-C.5, OCR output is plain text lines. It does **NOT** synthesize artificial Markdown table grids or heading symbols.
   - This architectural discrepancy is cataloged and preserved until the unified text representation consolidation boundary (Phase W3-C.8 / W3-C.9).

---

## 5. Security & Memory Engineering Contract

### 5.1 Memory Discipline (Zero Multi-Page Raster Retention)
Instead of claiming an impossible process-wide working-set ceiling (e.g. `< 100 MB`), AXORA enforces strict architectural invariants:
1. **Zero Multi-Page Raster Retention:** AXORA never accumulates or retains multiple full-resolution page raster streams or decoded `SoftwareBitmap` objects simultaneously.
2. **Sequential Single-Page Lifetime:** In multi-page PDFs or multi-frame TIFFs, each page raster is decoded, processed through OCR, and its unmanaged memory explicitly disposed before the next page is rasterized.
3. **Maximum Pixel & Allocation Bounds:**
   - Maximum single dimension: 10,000 pixels (enforced via proportional downscaling).
   - Maximum uncompressed single-page buffer: 10,000 × 10,000 × 4 bytes = 400 MB (worst-case theoretical peak).
   - Standard 300 DPI Letter page (2550 × 3300 px): ~33.6 MB per page, immediately freed upon page completion.

### 5.2 Dimension Bounding
- Windows OCR API (`Windows.Media.Ocr.OcrEngine`) natively enforces:
  - Minimum dimension: 40 × 40 pixels.
  - Maximum dimension: 10,000 × 10,000 pixels (`OcrEngine.MaxImageDimension`).
- If an input raster exceeds 10,000 pixels in width or height, the engine proportionally downscales the in-memory processing buffer to fit within 10,000 px and appends a diagnostic warning.
- Images smaller than 40 × 40 pixels are rejected with a descriptive warning or `DocumentCorruptException`.

---

## 6. PDF OCR Dispatch & Preservation of Digital Text

1. **Digital Text Preservation Invariant:**
   **VALID DIGITAL TEXT MUST NEVER BE SILENTLY DISCARDED OR OVERWRITTEN BY OCR.**
2. **Evidence-Based Dispatch:**
   - Content stream extraction (`PdfTextExtractor`) is primary.
   - If clean text length exceeds heuristic threshold (`>= 50 chars`), digital text is preserved (`ExtractedViaOcr = false`).
   - If clean text length is sparse (`< 10 chars`) AND image XObjects exist (`HasImageXObjects == true`), page is dispatched for rasterization and OCR (`ExtractedViaOcr = true`).
3. **`ForceOcr` Semantics:**
   - If `options.ForceOcr == true`: The engine rasterizes and runs OCR on all pages, including digital text pages, but annotates `DiagnosticWarning: "OCR forced by user options on text-bearing digital page"`.
   - If `options.ForceOcr == false`: Digital text pages are never rasterized or submitted to OCR.

---

## 7. Privacy & Logging Invariants

1. **Local Confinement:** Recognized OCR text must never leave the local process memory.
2. **Zero Text Leakage in Logs:** System logs (`ILogger`) may record:
   - Operation names and elapsed durations
   - Image pixel dimensions and DPI
   - Installed and selected BCP-47 language tags
   - Error codes and failure classifications
   - Character counts and page indices
3. System logs must **NEVER** output:
   - Recognized sentence fragments or extracted words
   - User notes or document titles
   - Image binary payloads or disk temp paths containing user names
