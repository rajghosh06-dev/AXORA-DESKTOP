# AXORA WinUI — Phase W2-F Format Matrix & Engine Capabilities

**Document Version**: 1.0.0 (Phase W2-F Architecture Baseline)  
**Target Platform**: Windows 11 (x64) · .NET 9.0 (`net9.0-windows10.0.26100.0`) · Windows App SDK 1.6  
**Status**: **PLANNING / FORMAT-MATRIX GATE**  
**Engineering Baseline**: Baseline 4 (`ae1d314`) + W1 Hardening (`c0ce700`) + W1.5 Extension Manager (`fefb051`) + W2-A..D Core (`687f751`) + W2-E..E.2 GUI (`b6d5ed4`) + W2-E.3 QA Hardening (`d0e64ce`)

---

## 1. Executive Summary

This matrix defines the precise, verified operational capabilities for all input/output format pairs under Phase **W2-F** (Post-Processing, Image Compression & Metadata Optimization). 

Every format is categorized by its native engine adapter, decoding support, encoding support, compression optimization mechanisms, metadata governance behavior, and structural validation requirements. No format combination is listed as supported without a concrete native implementation path.

---

## 2. Universal Converter W2-F Format Matrix

| Input Format | Operation | Output Format | Native Engine Adapter | External Dependency | Compression Type | Metadata Handling | Verification Status |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `.png` | Convert & Compress | `.jpg` / `.jpeg` | `WicImageConversionEngine` (SkiaSharp) | None (Built-in) | Lossy (Quality 1–100) | Strip or Preserve | **VERIFIED (W2-E.1 / W2-E.2)** |
| `.png` | Downscale & Re-encode | `.png` | `WicImageConversionEngine` (SkiaSharp) | None (Built-in) | Lossless (Deflate Level 6) | Strip or Preserve | Planned (W2-F) |
| `.png` | Convert & Optimize | `.webp` | `WicImageConversionEngine` (SkiaSharp) | None (Built-in) | Lossy/Lossless (Quality 1–100) | Strip or Preserve | Planned (W2-F) |
| `.png` | Convert | `.bmp` | `WicImageConversionEngine` (WIC) | None (Built-in) | Uncompressed | No Metadata | Planned (W2-F) |
| `.png` | Convert | `.tiff` | `WicImageConversionEngine` (WIC) | None (Built-in) | Lossless (LZW/Deflate) | Preserve Tags | Planned (W2-F) |
| `.jpg` / `.jpeg` | Convert | `.png` | `WicImageConversionEngine` (SkiaSharp) | None (Built-in) | Lossless | Strip or Preserve | Planned (W2-F) |
| `.jpg` / `.jpeg` | Downscale & Recompress | `.jpg` / `.jpeg` | `WicImageConversionEngine` (SkiaSharp) | None (Built-in) | Lossy (Quality 1–100) | Strip or Preserve | Planned (W2-F) |
| `.jpg` / `.jpeg` | Convert & Optimize | `.webp` | `WicImageConversionEngine` (SkiaSharp) | None (Built-in) | Lossy/Lossless (Quality 1–100) | Strip or Preserve | Planned (W2-F) |
| `.jpg` / `.jpeg` | Convert | `.bmp` | `WicImageConversionEngine` (WIC) | None (Built-in) | Uncompressed | No Metadata | Planned (W2-F) |
| `.jpg` / `.jpeg` | Convert | `.tiff` | `WicImageConversionEngine` (WIC) | None (Built-in) | Lossless (LZW) | Preserve Tags | Planned (W2-F) |
| `.webp` | Convert | `.png` | `WicImageConversionEngine` (SkiaSharp) | None (Built-in) | Lossless | Strip or Preserve | Planned (W2-F) |
| `.webp` | Convert & Compress | `.jpg` / `.jpeg` | `WicImageConversionEngine` (SkiaSharp) | None (Built-in) | Lossy (Quality 1–100) | Strip or Preserve | Planned (W2-F) |
| `.webp` | Downscale & Re-encode | `.webp` | `WicImageConversionEngine` (SkiaSharp) | None (Built-in) | Lossy/Lossless | Strip or Preserve | Planned (W2-F) |
| `.bmp` | Convert & Compress | `.jpg` / `.jpeg` | `WicImageConversionEngine` (SkiaSharp) | None (Built-in) | Lossy (Quality 1–100) | Strip | Planned (W2-F) |
| `.bmp` | Convert | `.png` | `WicImageConversionEngine` (SkiaSharp) | None (Built-in) | Lossless | Strip | Planned (W2-F) |
| `.bmp` | Convert & Optimize | `.webp` | `WicImageConversionEngine` (SkiaSharp) | None (Built-in) | Lossy/Lossless | Strip | Planned (W2-F) |
| `.tiff` / `.tif` | Convert & Compress | `.jpg` / `.jpeg` | `WicImageConversionEngine` (WIC $\to$ Skia) | None (Built-in) | Lossy (Quality 1–100) | Strip or Preserve | Planned (W2-F) |
| `.tiff` / `.tif` | Convert | `.png` | `WicImageConversionEngine` (WIC $\to$ Skia) | None (Built-in) | Lossless | Strip or Preserve | Planned (W2-F) |
| `.pdf` (Page) | Rasterize & Compress | `.jpg` | `WindowsPdfRendererConversionEngine` | None (WinRT PDF) | Lossy (Quality 1–100) | Strip | **VERIFIED (W2-C / PoC)** |
| `.pdf` (Page) | Rasterize & Output | `.png` | `WindowsPdfRendererConversionEngine` | None (WinRT PDF) | Lossless | Strip | **VERIFIED (W2-C / PoC)** |
| `.txt` / `.md` | Render Document | `.pdf` | `PdfDocumentConversionEngine` | None (PdfSharpCore) | Lossless Vector/Text | Document Properties | **VERIFIED (W2-B2 / W2-E.1)** |
| `.md` | Transpile Document | `.html` | `TextMarkdownConversionEngine` | None (Built-in) | Lossless Text | UTF-8 HTML Meta | **VERIFIED (W2-B2 / W2-E.1)** |

---

## 3. Detailed Format Specification & Validation Profiles

### 3.1 JPEG (`.jpg`, `.jpeg`)
- **MIME Type**: `image/jpeg`
- **Decode Support**: `SKBitmap.Decode` (high-speed in-memory), with fallback to Windows Imaging Component (`BitmapDecoder`).
- **Encode Support**: `SKImage.Encode(SKEncodedImageFormat.Jpeg, quality)`.
- **Quality Tuning**: Integer parameter `1–100`. Standard mapping:
  - `Quality = 100`: Archival / Maximum Fidelity (minimal DCT quantization).
  - `Quality = 85`: Balanced Default (imperceptible loss, ~60% size reduction vs uncompressed).
  - `Quality = 75`: Web & Mobile Optimized (~75% size reduction).
  - `Quality = 50`: High-Compression / Thumbnail (~90% size reduction).
- **Metadata Governance**:
  - *Strip Mode*: Omits APP1 (EXIF), APP2 (ICC), APP13 (IPTC). Completely strips GPS coordinates, camera serials, and timestamps.
  - *Preserve Mode*: Retains EXIF metadata where target is JPEG.
  - *Orientation Invariant*: If EXIF Orientation tag (tag `0x0112`) indicates rotation (e.g. 6 = Rotate 90 CW, 8 = Rotate 270 CW, 3 = Rotate 180), pixels are normalized to standard upright orientation before stripping the tag.
- **Structural Validation**: Magic SOI marker `0xFF, 0xD8`; EOI marker `0xFF, 0xD9`.
- **Semantic Validation**: Successful roundtrip instantiation through `SKBitmap.Decode`.

### 3.2 PNG (`.png`)
- **MIME Type**: `image/png`
- **Decode Support**: `SKBitmap.Decode` with WIC fallback.
- **Encode Support**: `SKImage.Encode(SKEncodedImageFormat.Png)`.
- **Compression**: Lossless DEFLATE algorithm.
- **Alpha Channel**: Full 8-bit alpha transparency preserved (`Bgra8888` / `Rgba8888`).
- **Optimization Support**: Downscaling bounding box reduces total pixel count prior to encoding.
- **Metadata Governance**: Strips ancillary chunks (`tEXt`, `zTXt`, `iTXt`, `eXIf`) in Privacy mode.
- **Structural Validation**: 8-byte PNG header: `0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A`.
- **Semantic Validation**: Successful roundtrip instantiation through `SKBitmap.Decode`.

### 3.3 WebP (`.webp`)
- **MIME Type**: `image/webp`
- **Decode Support**: `SKBitmap.Decode` (bundled Google libwebp native adapter via SkiaSharp).
- **Encode Support**: `SKImage.Encode(SKEncodedImageFormat.Webp, quality)`.
- **Quality Tuning**: Lossy VP8 (quality 1–99) and Lossless VP8L (quality 100).
- **Efficiency Metric**: Delivers 25% to 34% smaller files than JPEG at equivalent Structural Similarity (SSIM) index.
- **Metadata Governance**: Excludes RIFF `EXIF` and `XMP ` chunks in Privacy mode.
- **Structural Validation**: RIFF container signature: bytes 0–3 = `RIFF` (`0x52, 0x49, 0x46, 0x46`), bytes 8–11 = `WEBP` (`0x57, 0x45, 0x42, 0x50`).
- **Semantic Validation**: Successful roundtrip instantiation through `SKBitmap.Decode`.

### 3.4 BMP (`.bmp`)
- **MIME Type**: `image/bmp`
- **Decode Support**: `SKBitmap.Decode` / WIC.
- **Encode Support**: WIC (`BitmapEncoder.BmpEncoderId`).
- **Compression**: Uncompressed raster DIB.
- **Metadata Support**: None (format does not support standard EXIF).
- **Structural Validation**: Bytes 0–1 = `BM` (`0x42, 0x4D`).

### 3.5 TIFF (`.tiff`, `.tif`)
- **MIME Type**: `image/tiff`
- **Decode Support**: Native Windows WIC (`BitmapDecoder.TiffDecoderId`).
- **Encode Support**: Native Windows WIC (`BitmapEncoder.TiffEncoderId`).
- **Compression**: LZW or Deflate lossless compression.
- **Target DPI**: Preserves physical resolution metadata (e.g. 300 DPI for archival scans).
- **Structural Validation**: Little-endian `0x49, 0x49, 0x2A, 0x00` (`II*`) or Big-endian `0x4D, 0x4D, 0x00, 0x2A` (`MM*`).

### 3.6 PDF Rasterization (`.pdf` $\to$ `.png`, `.jpg`)
- **Engine**: `Windows.Data.Pdf.PdfDocument` via WinRT.
- **DPI Support**: Configurable rasterization scale:
  - 72 DPI (Draft / Screen Thumbnail)
  - 96 DPI (Standard Windows Screen)
  - 150 DPI (Balanced Default)
  - 300 DPI (High-Resolution Print Quality)
- **Output Validation**: Validated via `ConversionOutputValidator` according to target image extension.

---

## 4. Explicitly Unsupported Combinations & Non-Goals

The following formats and operations are explicitly **UNSUPPORTED** in W2-F and will be rejected with clear UI/telemetry error codes:

| Format / Operation | Status | Reason | Rejection Code |
|---|:---:|---|---|
| **Video Files** (`.mp4`, `.mov`, `.mkv`, `.avi`, `.webm`) | **UNSUPPORTED** | Universal Converter is raster/doc only; video transcoding requires FFmpeg (Roadmap W6). | `ERR_UNSUPPORTED_FORMAT` |
| **Audio Files** (`.mp3`, `.wav`, `.flac`, `.aac`) | **UNSUPPORTED** | Universal Converter is raster/doc only. | `ERR_UNSUPPORTED_FORMAT` |
| **Camera RAW** (`.cr2`, `.nef`, `.arw`, `.dng`) | **UNSUPPORTED** | Proprietary sensor Bayer patterns require specialized RAW decoders; out of scope. | `ERR_UNSUPPORTED_FORMAT` |
| **Vector Graphics** (`.ai`, `.eps`, `.cdr`) | **UNSUPPORTED** | Proprietary vector formats require commercial PostScript interpreters. | `ERR_UNSUPPORTED_FORMAT` |
| **Microsoft Office** (`.docx`, `.xlsx`, `.pptx`) | **UNSUPPORTED** | Document conversion requires Office COM or LibreOffice (Strict Non-Goal). | `ERR_UNSUPPORTED_FORMAT` |
| **In-Place Compression** (Overwrite input file in place) | **PROHIBITED** | Violates fundamental source immutability safety invariant. | `ERR_OUTPUT_SAME_AS_SOURCE` |
| **Gigapixel Images** (>16,384 px or >512 MB raw buffer) | **REJECTED** | Safety guard against runaway desktop Out-Of-Memory exceptions. | `ERR_IMAGE_DIMENSIONS_EXCEEDED` |
| **Corrupted Image Headers** (Truncated byte stream) | **REJECTED** | Fails structural and decode validation before publication. | `ERR_INPUT_CORRUPT` |
