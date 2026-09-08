# Phase W3-C.5: On-Device OCR & Image Extraction — Verification Plan

**AXORA Desktop — WinUI Native Academic Workspace**  
**Document ID:** `AXORA-VERIFY-W3C5-001`  
**Phase:** `W3-C.5` (On-Device OCR Integration & Image Extraction)  
**Target Project:** `Axora-Desktop-WinUI`  
**Status:** `RECONCILED C5.1 VERIFICATION PLAN — IMPLEMENTATION NOT COMMENCED`  
**Protected Git Baseline:** `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Verification Strategy & Scope

The verification strategy for Phase W3-C.5 validates the on-device OCR engine, capability detection, image extraction, orientation normalization, and PDF hybrid integration.

### Core Testing Invariants:
1. **Separation of Deterministic Contracts vs. Host-Dependent Recognition:**
   - **Contract & Architecture Tests:** Must be 100% deterministic, verifying mock providers, state transitions, option validation, exception mapping, and immutability across any test host.
   - **Live OCR Recognition Tests:** Recognize in-memory rendered SkiaSharp text fixtures. Assertions verify keyword presence (e.g. `raw.Contains("Quantum")`) rather than brittle character-by-character equality, accommodating slight rendering variations across Windows OS builds and DPI settings.
2. **Self-Contained Fixture Generation:** Tests generate synthetic image fixtures in memory using SkiaSharp rather than committing heavy binary images to Git.
3. **Graceful Host Adaptation:** If a test runner executes on a machine without installed Windows OCR language packs, tests must verify that `IOcrCapabilityStateProvider` reports `OcrUnavailable` or `OcrLanguageUnavailable` without throwing unhandled exceptions.
4. **Zero Regressions:** All 917 existing test assertions (W2, W3-A, W3-B, W3-C.1..W3-C.4) must continue to pass unconditionally.

---

## 2. Test Classification Matrix

### Group A: Deterministic Contract & State Machine Tests (Host-Independent)
| Test ID | Objective | Verification Method | Expected Outcome |
| :--- | :--- | :--- | :--- |
| `W3C5_1a_ProviderContract` | Verify `IOcrCapabilityStateProvider` resolves via DI. | Dependency Injection probe | Non-null instance with valid `State` enum. |
| `W3C5_1b_LanguagePackEnumeration` | Verify `InstalledLanguages` exposes installed tags. | Query provider | Read-only list populated (or empty if zero packs). |
| `W3C5_1c_ActiveLanguageResolved` | Verify `ActiveLanguageTag` resolves properly. | Query provider | Valid BCP-47 tag or `null` if unavailable. |
| `W3C5_1d_ExplicitLanguageMatch` | Test request for installed language tag. | Request `"en-US"` | Reports `OcrCapabilityState.OcrAvailable`. |
| `W3C5_1e_ExplicitLanguageMissing` | Test request for missing language tag. | Request `"xyz-ZZ"` | Reports `OcrCapabilityState.OcrLanguageUnavailable`. |
| `W3C5_1f_DynamicRefresh` | Verify `RefreshStateAsync()` executes idempotently. | Invoke twice | State identical; zero memory leaks. |
| `W3C5_1g_EngineIdentifier` | Verify `IOcrEngine.EngineId`. | Query engine | Returns `"WindowsMediaOcrEngine"`. |

### Group B: Host-Dependent OCR Recognition Tests (Keyword-Grounded)
| Test ID | Objective | Verification Method | Expected Outcome |
| :--- | :--- | :--- | :--- |
| `W3C5_2a_CleanTextRecognition` | Recognize in-memory rendered PNG with English text. | SkiaSharp render -> OCR | Extracted text contains rendered keywords (e.g. `"Quantum"`). |
| `W3C5_2b_ConfidenceRange` | Verify `Confidence` is bounded in `[0.0, 1.0]`. | Check result confidence | `Confidence >= 0.0 && Confidence <= 1.0`. |
| `W3C5_2c_LanguageTagPropagated` | Verify `OcrResult.LanguageTag` matches executed pack. | Check result language | Non-empty BCP-47 string matching recognizer. |
| `W3C5_2d_ElapsedTimeRecorded` | Verify `OcrResult.Elapsed` is strictly positive. | Check stopwatch timing | `Elapsed > TimeSpan.Zero`. |
| `W3C5_2e_JpegFormatSupported` | Test JPEG stream recognition. | SkiaSharp JPEG -> OCR | Successfully extracts expected keywords. |
| `W3C5_2f_BmpFormatSupported` | Test BMP stream recognition. | SkiaSharp BMP -> OCR | Successfully extracts expected keywords. |
| `W3C5_2g_WebpFormatSupported` | Test WebP stream recognition via SkiaSharp decode. | SkiaSharp WebP -> OCR | Successfully extracts expected keywords. |
| `W3C5_2h_AlphaCompositedOverWhite`| Transparent PNG with black text. | SkiaSharp transparent PNG | Text recognized accurately without black-on-black failure. |

### Group C: Orientation Normalization & Standalone Extractors
| Test ID | Objective | Verification Method | Expected Outcome |
| :--- | :--- | :--- | :--- |
| `W3C5_3a_ExifRotate90CW` | Image with EXIF orientation 6 (90 deg CW). | `ExifOrientationNormalizer` | In-memory buffer rotated upright; OCR extracts legible text. |
| `W3C5_3b_ExifRotate180` | Image with EXIF orientation 3 (180 deg). | `ExifOrientationNormalizer` | In-memory buffer rotated upright; OCR extracts legible text. |
| `W3C5_3c_ExifRotate270CW` | Image with EXIF orientation 8 (270 deg CW). | `ExifOrientationNormalizer` | In-memory buffer rotated upright; OCR extracts legible text. |
| `W3C5_3d_UnspecifiedOrigin` | Image with EXIF orientation 1 or 0. | `ExifOrientationNormalizer` | Buffer passes through unmodified. |
| `W3C5_3e_SourceFileImmutability` | Check SHA-256 of rotated source file on disk. | Pre/post SHA-256 hash | Source file hash is 100% byte-for-byte identical. |
| `W3C5_4a_SingleImagePageCount` | Extract single PNG image via `RasterImageDocumentExtractorEngine`. | ExtractAsync | Exactly 1 physical page (`PageSemantics.PhysicalPage`). |
| `W3C5_4b_SingleImageMetadata` | Verify format and engine identifier. | Query result | `DetectedDocumentFormat.RasterImage`, valid engine ID. |
| `W3C5_4c_TwoTierInvariant` | Verify `NormalizedText` is strictly null. | Query page | `page.NormalizedText == null`. |
| `W3C5_4d_ExtractedViaOcrFlag` | Verify `ExtractedViaOcr` is true. | Query page | `page.ExtractedViaOcr == true`. |
| `W3C5_4e_MultiFrameTiffPages` | Extract 3-frame synthetic TIFF. | WIC `BitmapDecoder` | Exactly 3 physical pages (`PageNumber` 1, 2, 3). |
| `W3C5_4f_MultiFrameTiffContent` | Verify text on each TIFF frame. | Query pages | Unique text on each frame attributed to its respective page. |

### Group D: Scanned & Mixed PDF Hybrid Extraction
| Test ID | Objective | Verification Method | Expected Outcome |
| :--- | :--- | :--- | :--- |
| `W3C5_5a_ScannedPdfDispatched` | Scanned PDF (image-only) processed through hybrid extractor. | Rasterize -> OCR | Pages rasterized via `WindowsPdfPageRasterizer` and OCR'd. |
| `W3C5_5b_ScannedPdfTextExtracted`| Verify OCR text captured into `RawText`. | Query page raw text | Non-empty text matching synthesized page image. |
| `W3C5_5c_MixedPdfSelectiveDispatch` | PDF with Page 1 (Digital text) and Page 2 (Scanned image). | ExtractAsync | Page 1 has `ExtractedViaOcr=false`; Page 2 has `ExtractedViaOcr=true`. |
| `W3C5_5d_DigitalTextPreserved` | Verify digital text on mixed PDF is never replaced by OCR. | Exact character compare | Exact digital character stream intact on Page 1. |
| `W3C5_5e_ForceOcrOption` | Set `options.ForceOcr = true` on digital page. | ExtractAsync with flag | Engine forces OCR rasterization when explicitly requested. |

### Group E: Adversarial, Limits & Resource-Bounding Tests
| Test ID | Objective | Verification Method | Expected Outcome |
| :--- | :--- | :--- | :--- |
| `W3C5_6a_OversizedImageClamped` | Image with 12,000 × 8,000 px dimensions. | RecognizeAsync | Downscaled to <= 10,000 px; warning added; no crash. |
| `W3C5_6b_TinyImageRejected` | Image smaller than 40 × 40 px (< minimum WinRT limit). | RecognizeAsync | Handled safely with warning or `DocumentCorruptException`. |
| `W3C5_6c_CorruptImageStream` | Truncated / garbage byte stream. | RecognizeAsync | Throws `DocumentCorruptException` (`ERR_FILE_CORRUPTED`). |
| `W3C5_6d_MaxFileSizeEnforced` | Image exceeding `MaxFileSizeBytes`. | ExtractAsync | Throws `FileSizeLimitExceededException`. |
| `W3C5_6e_CancellationHonored` | Cancel token during image decoding or OCR. | Cancel CancellationToken | Aborts immediately, throwing `OperationCanceledException`. |
| `W3C5_6f_SequentialMemoryDisposal` | Multi-page PDF OCR extraction loop. | Measure allocations | Working set does not grow linearly O(N) with page count. |
| `W3C5_6g_DeterministicOutput` | Consecutive extractions of same image with fixed language. | Run twice | 100% byte-for-byte identical `RawText` across runs on same host. |

---

## 3. Objective Acceptance Gates

### Gate 1: Toolchain & Build Verification
- Clean build of `Axora.Desktop.sln` on `x64` with **zero compiler errors** and **zero new compiler/analyzer warnings**.
- Zero modifications to `MarkupCompilePass2` or XAML visual states.

### Gate 2: Test Suite Verification
- All 917 pre-existing assertions pass (100% Green).
- All new W3-C.5 assertions execute and pass unconditionally.

### Gate 3: Memory & Resource Safety
- All native WinRT COM references (`SoftwareBitmap`, `InMemoryRandomAccessStream`, `PdfPage`) are verified to be disposed inside `finally` blocks.
- Sequential page streaming verified: exactly one full-resolution raster buffer active in memory at a time.

### Gate 4: Ground Truth & Non-Destructive Invariants
- `RawText` retains exact OCR recognized characters without artificial Markdown table synthesis.
- `NormalizedText` is verified to be strictly `null` across all extracted pages.
- Source files on disk are verified to remain 100% immutable via pre- and post-extraction SHA-256 hash comparison.
