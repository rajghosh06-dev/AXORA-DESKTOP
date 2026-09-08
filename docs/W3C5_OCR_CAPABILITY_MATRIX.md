# Phase W3-C.5: On-Device OCR & Image Extraction — Capability Matrix

**AXORA Desktop — WinUI Native Academic Workspace**  
**Document ID:** `AXORA-MATRIX-W3C5-001`  
**Phase:** `W3-C.5` (On-Device OCR Integration & Image Extraction)  
**Target Project:** `Axora-Desktop-WinUI`  
**Status:** `RECONCILED C5.1 CAPABILITY MATRIX — IMPLEMENTATION NOT COMMENCED`  
**Protected Git Baseline:** `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Supported Document & Media Format Matrix

| Format / Container | Magic Bytes / Signature | DetectedDocumentFormat | Multi-Page Support | Extraction Engine | Default DPI / Resolution | Feasibility Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Portable Network Graphics (`.png`)** | `89 50 4E 47 0D 0A 1A 0A` | `RasterImage` | Single Page (Page 1) | `RasterImageDocumentExtractorEngine` | Source Resolution | **VERIFIED** via WIC / SkiaSharp |
| **JPEG / JFIF (`.jpg`, `.jpeg`)** | `FF D8 FF` | `RasterImage` | Single Page (Page 1) | `RasterImageDocumentExtractorEngine` | Source Resolution | **VERIFIED** via WIC / SkiaSharp |
| **Google WebP (`.webp`)** | `52 49 46 46 ... 57 45 42 50` | `RasterImage` | Single Page (Page 1) | `RasterImageDocumentExtractorEngine` | Source Resolution | **VERIFIED** via SkiaSharp |
| **Windows Bitmap (`.bmp`)** | `42 4D` | `RasterImage` | Single Page (Page 1) | `RasterImageDocumentExtractorEngine` | Source Resolution | **VERIFIED** via WIC / SkiaSharp |
| **Single-Page TIFF (`.tif`, `.tiff`)** | `49 49 2A 00` (LE) / `4D 4D 00 2A` (BE) | `RasterImage` | Single Page (Page 1) | `RasterImageDocumentExtractorEngine` | Source Resolution | **VERIFIED** via WIC `TiffDecoderId` |
| **Multi-Frame TIFF (`.tif`, `.tiff`)** | `49 49 2A 00` / `4D 4D 00 2A` (Frames > 1) | `MultiPageTiff` | Physical Pages (1..N) | `RasterImageDocumentExtractorEngine` | Source Resolution per frame | **VERIFIED** via `BitmapDecoder.GetFrameAsync` |
| **Digital PDF (`.pdf`)** | `%PDF-` | `PdfDigital` | Physical Pages (1..N) | `PdfDocumentExtractorEngine` (Direct Vector) | 72 Pt layout vector | **VERIFIED** (Accepted W3-C.3) |
| **Scanned Image PDF (`.pdf`)** | `%PDF-` (No text, images present) | `PdfScanned` | Physical Pages (1..N) | `PdfDocumentExtractorEngine` + `WindowsPdfPageRasterizer` + `IOcrEngine` | 300 DPI Rasterized PNG | **VERIFIED** (Rasterizer accepted W3-C.3) |
| **Mixed PDF (`.pdf`)** | `%PDF-` (Text & image pages) | `PdfMixed` | Physical Pages (1..N) | Selective Dispatch (Vector for text, OCR for scanned) | 300 DPI Rasterized PNG (scanned pages only) | **VERIFIED** (Selective Dispatch) |

---

## 2. Platform Claims & Evidence Status Audit

All architectural claims regarding `Windows.Media.Ocr.OcrEngine` and platform integration were audited against live runtime behavior and Microsoft documentation:

| Platform Feature / Claim | Claimed Behavior | Evidence Status | Verified Details & Behavioral Findings |
| :--- | :--- | :--- | :--- |
| **Minimum Dimension** | Image dimensions must be >= 40 × 40 px | **DOCUMENTED BY MICROSOFT** | `RecognizeAsync` throws managed `ArgumentException` if either width or height is less than 40 px. |
| **Maximum Dimension** | Maximum dimension is 10,000 × 10,000 px | **VERIFIED & DOCUMENTED BY MICROSOFT** | Verified on host OS: `OcrEngine.MaxImageDimension` returns `10000` (uint). Images exceeding this throw managed `ArgumentException`. |
| **Out-of-Bounds Behavior** | Exceeding 10,000 px throws exception | **VERIFIED** | Throws managed `ArgumentException`. (Previous claim of an "uncatchable native crash" was an **OVERCLAIM** and is rejected). |
| **Supported Pixel Formats** | Input `SoftwareBitmap` must be Bgra8 or Nv12 | **VERIFIED & DOCUMENTED BY MICROSOFT** | Verified: `BitmapPixelFormat.Bgra8` with `BitmapAlphaMode.Premultiplied` is natively supported. |
| **Unpackaged App Compatibility** | Works in unpackaged WinUI 3 desktop apps | **VERIFIED & OBSERVED LOCALLY** | Verified on host: `AvailableRecognizerLanguages` queries OS language store without MSIX package identity. |
| **Language Discovery** | Synchronous query of installed OS packs | **VERIFIED & OBSERVED LOCALLY** | Verified on host: `AvailableRecognizerLanguages` returned `["en-US"]`. Empty list returned if zero packs. |
| **Cancellation Behavior** | Supports cancellation token via `.AsTask(ct)` | **VERIFIED** | Cancelling token before or during operation propagates `OperationCanceledException`. |
| **Confidence Metric** | Engine returns confidence score | **IMPLEMENTATION ASSUMPTION** | WinRT `OcrResult` does **NOT** expose a numerical confidence float. It exposes `Text`, `Lines`, `Words`, `BoundingRect`, and `TextAngle`. In AXORA, `OcrResult.Confidence` is an engine-level adapter mapping (defaults to 1.0 on success). |
| **Text Rotation Angle** | Engine detects text tilt angle | **VERIFIED & DOCUMENTED BY MICROSOFT** | `OcrResult.TextAngle` returns a nullable double in degrees indicating detected text tilt. |
| **Multi-Frame TIFF** | Native WIC frame enumeration | **VERIFIED & OBSERVED LOCALLY** | Verified: `BitmapDecoder.FrameCount` and `BitmapDecoder.GetFrameAsync(uint)` provide frame-by-frame decoding without third-party packages. |

---

## 3. Language Discovery & Locale Matrix

### 3.1 Language Pack Discovery Mechanism
`Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages` exposes installed OS OCR language packs.
- Windows manages OCR language packs via **Windows Settings -> Time & Language -> Language & Region -> Add a Language -> Language Options -> Basic Typing / Optical Character Recognition**.
- The table below defines how AXORA maps requested language tags to installed system languages:

| Scenario | Input `ExtractionOptions.OcrLanguage` | Installed OS Languages | Resulting `OcrCapabilityState` | Executed Language | Diagnostic Warning Emitted |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Automatic Profile** | `null` or `""` | `["en-US", "de-DE"]` | `OcrAvailable` | User Profile (e.g. `en-US`) | None |
| **Automatic Fallback** | `null` or `""` | `["es-ES"]` (Profile `fr-FR` missing) | `OcrAvailable` | First Available (`es-ES`) | `"Profile language fr-FR not installed; using es-ES"` |
| **Explicit Match** | `"de-DE"` | `["en-US", "de-DE"]` | `OcrAvailable` | `"de-DE"` | None |
| **Explicit Missing** | `"ja-JP"` | `["en-US", "de-DE"]` | `OcrLanguageUnavailable` | None (Extraction bypassed) | `"Requested OCR language ja-JP is not installed on this device."` |
| **Zero OCR Packs** | Any | `[]` (Empty) | `OcrUnavailable` | None (Extraction bypassed) | `"No Windows OCR language packs are installed on this device."` |

---

## 4. PDF Selective Dispatch & Digital Text Invariants

### 4.1 Evidence-Based Dispatch (Rejecting Arbitrary Thresholds as Semantic Truth)
- Digital text extracted from PDF content streams (`PdfTextExtractor`) is the primary source of truth.
- Character-count thresholds are **heuristics only**:
  - `TextBearingThresholdChars = 50`: Heuristic indicating sufficient digital text to classify a page as text-bearing.
  - `TextSparseThresholdChars = 10`: Heuristic indicating potential image-only scan if accompanied by `HasImageXObjects == true`.
- **The Inviolable Invariant:**
  **VALID DIGITAL TEXT MUST NEVER BE SILENTLY DISCARDED OR OVERWRITTEN BY OCR.**
- When a page contains valid digital text, that digital text is preserved in `RawText` (`ExtractedViaOcr = false`).
- If `options.ForceOcr == true`, OCR is executed on all pages, but a diagnostic warning is appended to denote that digital text was supplemented by OCR.

---

## 5. Confidence Representation & Provenance Matrix

| Metric | Representation in AXORA | Source / Verification | Interpretation & Downstream Policy |
| :--- | :--- | :--- | :--- |
| **Engine Confidence** | `ExtractedPageRaw.Confidence` | Adapter Mapping | Bounded `[0.0, 1.0]`. Defaults to 1.0 on clean recognition, 0.0 on failure/empty. |
| **Text Tilt Angle** | `ExtractedPageRaw.DiagnosticWarning` | `OcrResult.TextAngle` | Orientation angle reported by Windows OCR engine. |
| **ExtractedViaOcr Flag** | `ExtractedPageRaw.ExtractedViaOcr` | Invariant Boolean | `true` for OCR-extracted pages; `false` for native digital text. |
| **Page Semantics** | `ExtractedPageRaw.PageSemantics` | Physical Page Invariant | Strictly `PageSemanticsType.PhysicalPage` (1:1 physical sheet mapping). |
| **Language Provenance** | `OcrResult.LanguageTag` | `RecognizerLanguage.LanguageTag` | Exact BCP-47 tag used by the recognition engine. |
