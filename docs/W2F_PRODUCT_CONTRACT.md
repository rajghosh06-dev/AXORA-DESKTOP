# AXORA WinUI — Phase W2-F Product Contract
## Post-Processing, Image Compression & Metadata Optimization

**Document Version**: 1.0.0 (Phase W2-F Architecture Baseline)  
**Target Platform**: Windows 11 (x64) · .NET 9.0 (`net9.0-windows10.0.26100.0`) · Windows App SDK 1.6  
**Status**: **PLANNING / PRODUCT-CONTRACT GATE**  
**Engineering Baseline**: Baseline 4 (`ae1d314`) + W1 Hardening (`c0ce700`) + W1.5 Extension Manager (`fefb051`) + W2-A..D Core (`687f751`) + W2-E..E.2 GUI (`b6d5ed4`) + W2-E.3 QA Hardening (`d0e64ce`)

---

## 1. Product Objective

Phase **W2-F** extends the native AXORA WinUI Universal Converter with user-controlled post-processing, compression optimization, dimension constraints, and privacy-preserving metadata governance.

The objective is to allow desktop users to achieve deliberate, high-quality trade-offs between **file size**, **visual fidelity**, and **metadata privacy** during local batch conversions without sacrificing offline guarantees, memory safety, or source immutability.

---

## 2. User Problems Solved

1. **File Size Bloat**: Uncompressed camera scans and high-resolution smartphone photos often generate 5–25 MB output files when converted without quality tuning, making them unsuitable for web upload, email sharing, or messaging.
2. **Metadata Privacy Leakage**: Photos and documents routinely contain sensitive metadata—such as GPS coordinates, camera serial numbers, full owner names, and editing software history—which users inadvertently publish online.
3. **Display & Dimension Overflow**: Target platforms often impose maximum resolution bounds (e.g. 1920×1080 for web presentations, 1280×720 for mobile portals). Without downscaling, users must manually seek separate photo editing tools.
4. **Lack of Transparent Telemetry**: Users cannot easily observe whether a conversion achieved meaningful space savings. W2-F provides immediate, per-job compression metrics (`-42% | 2.4 MB -> 1.4 MB`).

---

## 3. Supported Operations

W2-F defines four coherent, non-destructive post-processing operations:

1. **Quality Tuning (Lossy Compression)**:
   - Configurable compression factor (1–100) for lossy formats (JPEG, WebP).
   - Default balanced setting: 85.
   - Lossless passthrough for formats where lossy compression is inapplicable (PNG, BMP).
2. **Bounding-Box Aspect-Preserving Downscaling (Max Dimension Constraint)**:
   - Proportional scaling constrained by a maximum bounding box edge:
     - `Original` (0 = no scaling)
     - `1280 px` (Standard Web / Mobile)
     - `1920 px` (Full HD Desktop)
     - `2560 px` (2K QHD)
     - `3840 px` (4K UHD)
   - Uses high-quality resampling (`SKFilterQuality.High` / `BitmapInterpolationMode.Fant`).
   - Aspect ratio is strictly preserved; images are never stretched, cropped, or padded.
3. **Metadata Governance (Fidelity vs Privacy)**:
   - **Privacy Mode (Strip)**: Strips all EXIF, GPS coordinates, camera/lens serials, author names, and application comments.
   - **Fidelity Mode (Preserve)**: Retains existing EXIF, color space profiles, and timestamps where target format allows.
   - **Orientation Normalization Invariant**: When stripping metadata, the engine MUST apply the original EXIF orientation transform to the raw pixels *before* stripping the EXIF tag, ensuring the output image is never rotated sideways or upside-down.
4. **Rasterization DPI Resolution**:
   - Target rendering density for document rasterization (PDF -> Image): 72, 96, 150, 300 DPI.

---

## 4. Supported Formats & Engine Mapping

All W2-F operations execute through the native built-in engines:

| Format Family | File Extensions | Read Engine | Write Engine | Supported Operations |
|---|---|---|---|---|
| **Raster Image** | `.png` | SkiaSharp / WIC | SkiaSharp | Downscaling, Metadata Strip/Preserve, PNG Level 6/9 Compression |
| **Raster Image** | `.jpg`, `.jpeg` | SkiaSharp / WIC | SkiaSharp | Quality (1–100), Downscaling, EXIF Normalization, Metadata Strip/Preserve |
| **Raster Image** | `.webp` | SkiaSharp / WIC | SkiaSharp | Quality (1–100), Lossless, Downscaling, Metadata Strip/Preserve |
| **Raster Image** | `.bmp` | SkiaSharp / WIC | WIC | Downscaling, 24-bit/32-bit Raw Bitmap |
| **Raster Image** | `.tiff`, `.tif` | WIC | WIC | Downscaling, Target DPI, LZW/Deflate Compression |
| **Document Raster** | `.pdf` -> `.png`, `.jpg` | Windows.Data.Pdf | SkiaSharp | Rendering DPI (72–300), Quality, Downscaling, Metadata Strip |

---

## 5. Quality Presets & Configuration Model

To avoid cognitive overload while retaining power-user flexibility, W2-F introduces four standard presets:

```csharp
public sealed record OptimizationPreset
{
    public string Id { get; init; }
    public string DisplayName { get; init; }
    public string Description { get; init; }
    public int Quality { get; init; }
    public int MaxDimension { get; init; }
    public int TargetDpi { get; init; }
    public MetadataHandling MetadataPolicy { get; init; }
}
```

### Preset Definitions:
1. **Balanced (Recommended Default)**:
   - Quality: 85%
   - Max Dimension: Original (0)
   - Target DPI: 150
   - Metadata: Strip (Privacy Default)
2. **Web & Mobile Optimized**:
   - Quality: 75%
   - Max Dimension: 1920 px
   - Target DPI: 96
   - Metadata: Strip (Strict Privacy)
3. **Archival / Maximum Fidelity**:
   - Quality: 100%
   - Max Dimension: Original (0)
   - Target DPI: 300
   - Metadata: Preserve (Full Exif & ICC)
4. **Custom**:
   - User-adjusted Quality Slider (1–100), Max Dimension selector, and Metadata toggle.

---

## 6. Resource Governance & Memory Safety

1. **Gigapixel Protection Boundary**:
   - Images with width or height exceeding **16,384 pixels** or raw uncompressed pixel buffer exceeding **512 MB** (`width * height * 4 bytes > 536,870,912`) are rejected with `ERR_IMAGE_DIMENSIONS_EXCEEDED` before full buffer allocation.
   - Prevents Out-Of-Memory (OOM) crashes on constrained desktop devices.
2. **Memory Budget**:
   - Bounded concurrency per engine via `EngineResourceProfile`: max 2 to 4 concurrent image operations.
   - Maximum estimated memory per conversion job: **128 MB**.
3. **CPU Throttling**:
   - Encoding runs strictly on background thread pool (`Task.Run` / MTA background tasks) with low-priority background scheduling. The WinUI UI thread is never blocked.
4. **Cancellation Responsiveness**:
   - Cancellation token checked before decode, before resize, before encode, and before atomic publication.
   - Maximum cancellation response latency: **<500 ms**.

---

## 7. File Safety & Atomicity Invariants

1. **Source File Immutability**:
   - Source files are opened with `FileAccess.Read, FileShare.Read`.
   - Source files are NEVER modified in place, truncated, or overwritten.
   - Verified by checking byte-for-byte SHA-256 hash before and after conversion.
2. **Staged Publication Pipeline**:
   - Source -> Staged Temp File (.tmp_axora_*) -> Validation -> Atomic Move to Target.
3. **Collision Policy Governance**:
   - Collision policy is enforced centrally by `ConversionOrchestrator`:
     - `AutoRename`: appends `(1)`, `(2)`, etc.
     - `Overwrite`: atomically replaces destination.
     - `Skip`: marks job skipped without writing.
4. **Zero Orphaned Leftovers**:
   - Any failure or cancellation triggers cleanup of `.tmp_axora_*` files in `finally` blocks.

---

## 8. Privacy & Security Policies

1. **Local-First & Fully Offline**:
   - 100% of decoding, resizing, compression, and encoding executes locally on the device CPU/GPU.
   - Zero outbound network traffic, zero external API calls, zero telemetry transmission.
2. **Default Privacy Posture**:
   - Default preset ("Balanced") selects `MetadataHandling.Strip` to protect user privacy against accidental location or identity leaks.
   - Stripping guarantees the complete excision of:
     - GPS latitude, longitude, and altitude tags.
     - Device manufacturer, model, and camera serial number.
     - User comments, author names, and copyright strings.

---

## 9. UX & Accessibility Specifications

1. **Options Shelf Integration**:
   - Housed inside the 310 DIP left options pane of `UniversalConverterPage.xaml`.
   - Compact layout supports minimum window dimensions (960×600 DIP) without clipping or horizontal scrolling.
   - Controls:
     - Preset ComboBox: "Balanced", "Web Optimized", "Maximum Quality", "Custom".
     - Expandable/Collapsible "Fine-Tune Quality" drawer when "Custom" is selected (Quality Slider 1–100, Max Edge ComboBox).
     - Metadata Handling ToggleSwitch.
2. **Queue Card Telemetry**:
   - Real-time display upon job completion:
     - `Completed` badge with checkmark glyph.
     - Source file size -> Output file size (`2.4 MB -> 1.4 MB`).
     - Compression delta percentage (`-42%`).
3. **Accessibility (AutomationProperties)**:
   - `OptimizationPresetComboBox`: `AutomationProperties.Name="Optimization Preset"`
   - `QualitySlider`: `AutomationProperties.Name="Image Compression Quality"`
   - `MaxDimensionComboBox`: `AutomationProperties.Name="Maximum Edge Dimension"`
   - `MetadataHandlingToggle`: `AutomationProperties.Name="Metadata Handling Toggle"`

---

## 10. Non-Goals (Explicit Exclusions)

The following capabilities are explicitly declared **OUT OF SCOPE** for Phase W2-F:
- **No In-Place File Rewriting**: Standalone "compress in place" without converting is excluded to protect user data.
- **No Video Transcoding / FFmpeg**: Video processing belongs to future media milestones (W6).
- **No Optical Character Recognition (OCR)**: Document OCR remains isolated to ScholarKit.
- **No Office COM / LibreOffice / Pandoc**: External document authoring suites remain excluded.
- **No Interactive Crop / Freeform Rotation / Photo Editing**: AXORA is a local batch conversion tool, not a raster photo editor (e.g. Photoshop/GIMP).
- **No System Recovery, Driver Backup, or DISM**: Strictly unrelated to Universal Converter.
