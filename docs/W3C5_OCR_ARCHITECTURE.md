# Phase W3-C.5: On-Device OCR & Image Extraction — Architecture Document

**AXORA Desktop — WinUI Native Academic Workspace**  
**Document ID:** `AXORA-ARCH-W3C5-001`  
**Phase:** `W3-C.5` (On-Device OCR Integration & Image Extraction)  
**Target Project:** `Axora-Desktop-WinUI`  
**Status:** `RECONCILED C5.1 ARCHITECTURE — IMPLEMENTATION NOT COMMENCED`  
**Protected Git Baseline:** `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Architectural Pipeline & Flow

The OCR and image extraction layer bridges physical image buffers and scanned documents with the Scholar Kit Two-Tier document extraction pipeline:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        IMAGE / SCANNED MEDIA                           │
│     (Raster Images: PNG/JPG/WebP/BMP | Multi-page TIFF | Scanned PDF)   │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│              1. Image Stream Ingestion & Classification                │
│    • Content sniffing (RasterImage, MultiPageTiff)                     │
│    • Size limit & decompression bomb verification                      │
│    • Source stream opened with FileShare.ReadWrite (Source Immutable)  │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│              2. Canonical Image Preparation & Normalization            │
│    • EXIF SKEncodedOrigin parsing via SkiaSharp                        │
│    • In-Memory Upright Rotation (ExifOrientationNormalizer)             │
│    • Transparent pixels composited over solid opaque white (contrast)  │
│    • Dimension Bounding (Downscale to <= 10,000 px for Windows OCR)    │
│    • Converted to Bgra8 Premultiplied SoftwareBitmap                   │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│              3. Capability Probing (IOcrCapabilityStateProvider)       │
│    • Query OcrEngine.AvailableRecognizerLanguages                      │
│    • Match Options.OcrLanguage or User Profile Language                │
│    • State: OcrAvailable | OcrUnavailable | OcrLanguageUnavailable     │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│              4. Local OCR Execution (IOcrEngine)                        │
│    • Page-by-Page Streaming Execution (One Page at a Time)             │
│    • Windows.Media.Ocr.OcrEngine.RecognizeAsync                         │
│    • Capture Text, TextAngle, Lines, Words, and Elapsed Duration       │
│    • Immediate SoftwareBitmap & Stream Disposal in Finally Blocks      │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│              5. Page-Aware Raw Extraction Result Assembly               │
│    • ExtractedPageRaw: PageNumber (1..N), PhysicalPage Semantics       │
│    • RawText = Lines joined by \n (Ground Truth Plain Text)            │
│    • NormalizedText = null (Strictly Two-Tier Invariant)               │
│    • Granular Provenance: ExtractedViaOcr, Language, Engine ID, Angle  │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│       Downstream Handoff (Phases W3-C.6 / W3-C.7 / W3-C.8 / W3-B)       │
│    • Text Normalization -> Passage Chunking -> Persistence             │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Canonical Image Preparation Pipeline

To guarantee high OCR accuracy and deterministic execution without modifying user files on disk, the canonical image preparation follows five strictly ordered stages:

```
Source Stream (Disk)
       │
       ▼ [Stage 1: Read-Only Ingestion]
Open stream via FileAccess.Read, FileShare.ReadWrite.
Compute initial dimensions without loading full raster if header allows.
       │
       ▼ [Stage 2: EXIF Orientation Extraction]
Inspect EXIF Orientation tag (SKEncodedOrigin via SkiaSharp).
If origin != TopLeft (1), apply physical pixel transforms in memory via ExifOrientationNormalizer.
Source file on disk is untouched (SHA-256 preserved).
       │
       ▼ [Stage 3: Alpha Compositing over White]
If source contains alpha channel (e.g. transparent PNG / WebP),
composite buffer over solid opaque white background (SKColors.White).
Prevents black-on-black contrast failure when OCR treats transparent pixels as black.
       │
       ▼ [Stage 4: Dimension Clamping & Bounding]
Query dimensions. Max allowed by Windows OCR is 10,000 px (OcrEngine.MaxImageDimension).
If Width > 10,000 or Height > 10,000:
   scale = Math.Min(10000.0 / Width, 10000.0 / Height)
   Downscale in memory using high-quality bilinear interpolation.
   Record diagnostic warning noting the downscaling ratio.
If Width < 40 or Height < 40:
   Reject with descriptive warning or DocumentCorruptException (below WinRT OCR minimum).
       │
       ▼ [Stage 5: SoftwareBitmap Conversion]
Convert normalized buffer to BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied.
Pass to Windows.Media.Ocr.OcrEngine.RecognizeAsync().
```

---

## 3. Component Design & Abstraction Boundaries

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                             DI CONTAINER REGISTRATION                        │
├───────────────────────────────────┬─────────────────────────────────────────┤
│ Contract Interface                │ Concrete Implementation                 │
├───────────────────────────────────┼─────────────────────────────────────────┤
│ IOcrCapabilityStateProvider       │ WindowsOcrCapabilityStateProvider       │
│ IOcrEngine                        │ WindowsMediaOcrEngine                   │
│ IOcrService (Legacy Adapter)      │ WinRtOcrService (Refactored)            │
│ IDocumentExtractorEngine (Images) │ RasterImageDocumentExtractorEngine      │
│ IPdfDocumentExtractorEngine       │ PdfDocumentExtractorEngine (Hybrid OCR) │
│ IPdfPageRasterizer                │ WindowsPdfPageRasterizer (Accepted C.3) │
└───────────────────────────────────┴─────────────────────────────────────────┘
```

### 3.1 `IOcrCapabilityStateProvider` & `WindowsOcrCapabilityStateProvider`
- **Purpose:** Synchronously queries `Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages` to report installed language tags and overall capability state.
- **State Semantics:**
  - `OcrAvailable`: Engine functional and requested (or default) language pack installed.
  - `OcrUnavailable`: Host OS has zero OCR language packs installed.
  - `OcrLanguageUnavailable`: Host OS has OCR, but the requested BCP-47 language tag is missing.
  - `OcrFailed`: Initialization or hardware exception during probing.
- **Dynamic Refresh:** `RefreshStateAsync(ct)` executes idempotently to detect OS language packs installed while the app is running.

### 3.2 `IOcrEngine` & `WindowsMediaOcrEngine`
- **Purpose:** Encapsulates `Windows.Media.Ocr.OcrEngine` behind the pure `IOcrEngine` contract, preventing platform WinRT types (`SoftwareBitmap`, `InMemoryRandomAccessStream`) from leaking into ViewModels or Scholar library services.
- **Result Model:** Returns `OcrResult` with recognized text, confidence score, actual language used, detected text angle (`TextAngle`), warnings, and elapsed duration.
- **Resource Cleanup:** All `SoftwareBitmap` instances, `BitmapDecoder`, and `InMemoryRandomAccessStream` objects are disposed inside `finally` blocks immediately upon completion of recognition.

### 3.3 Legacy `IOcrService` Harmonization
- Refactors `WinRtOcrService` to delegate to `IOcrEngine`. Legacy callers (scanner, batch image tools) continue functioning without duplicate OCR execution logic.

### 3.4 `RasterImageDocumentExtractorEngine` (TIFF & Image Support)
- Implements `IDocumentExtractorEngine` for `RasterImage` and `MultiPageTiff`.
- Single images (`.png`, `.jpg`, `.jpeg`, `.bmp`, `.webp`): Processed into a single `ExtractedPageRaw` (`PageNumber = 1`, `PageSemantics = PhysicalPage`).
- Multi-Frame TIFFs (`.tif`, `.tiff`): Uses `Windows.Graphics.Imaging.BitmapDecoder` to enumerate frames (`decoder.FrameCount`), decoding each frame sequentially via `await decoder.GetFrameAsync(index)`. Produces `N` discrete physical pages.

### 3.5 Hybrid PDF Page-Aware OCR Dispatch (`PdfDocumentExtractorEngine`)
- Enhances `PdfDocumentExtractorEngine` from Phase W3-C.3:
  - If a page has clean digital text `>= 50 chars`, digital text is preserved (`ExtractedViaOcr = false`).
  - If a page is sparse (`< 10 chars`) AND image XObjects are present (`HasImageXObjects == true`), the page is rasterized at 300 DPI via `WindowsPdfPageRasterizer` and recognized via `IOcrEngine` (`ExtractedViaOcr = true`).
  - If `options.ForceOcr == true`, OCR is executed on all pages, but digital text pages record a diagnostic warning.
  - Digital text is **NEVER** silently replaced or discarded.

---

## 4. Memory Engineering Contract (Sequential Streaming Discipline)

AXORA does not make unsubstantiated claims of an absolute process working-set ceiling (e.g. `< 100 MB`). Instead, the architecture enforces strict, verifiable invariants:

1. **Zero Multi-Page Raster Retention:** The extraction pipeline never accumulates multiple uncompressed page rasters in memory.
2. **Strict Sequential Single-Page Lifetime:**
   ```csharp
   for (int i = 0; i < pageCount; i++)
   {
       ct.ThrowIfCancellationRequested();
       using (var pageRasterStream = await rasterizer.RasterizePageToPngStreamAsync(pdfStream, i, 300, ct))
       using (var inMemStream = new InMemoryRandomAccessStream())
       {
           // Decode, convert to SoftwareBitmap, recognize, and assemble page
           var ocrResult = await ocrEngine.RecognizeImageAsync(pageRasterStream, options.OcrLanguage, ct);
           pages.Add(BuildExtractedPageRaw(i + 1, ocrResult));
       }
       // Memory for page raster stream and native SoftwareBitmap is completely freed here
   }
   ```
3. **Deterministic Maximum Buffer Bounds:**
   - Maximum single dimension: 10,000 px.
   - Theoretical single-page peak: 10,000 × 10,000 × 4 bytes = 400 MB.
   - Typical 300 DPI Letter page: 2550 × 3300 × 4 bytes = ~33.6 MB.
   - The buffer is disposed before the next page begins, guaranteeing that memory consumption does not grow linearly $O(N)$ with document page count.

---

## 5. Provenance & Result Contract

Every page extracted via OCR populates a complete provenance record in `ExtractedPageRaw`:
- `ExtractedViaOcr`: `true`
- `Confidence`: Bounded `[0.0, 1.0]`
- `RawText`: Exact recognized text lines joined with `\n`. (No synthetic Markdown tables or headers).
- `NormalizedText`: `null` (Two-Tier invariant preserved).
- `DiagnosticWarning`: Contains the engine identifier, requested vs actual language tag, detected text rotation angle (`TextAngle`), and any downscaling applied.
