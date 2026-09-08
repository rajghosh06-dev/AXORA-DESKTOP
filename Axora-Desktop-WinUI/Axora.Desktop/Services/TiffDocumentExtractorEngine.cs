using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;
using Microsoft.Extensions.Logging;

namespace Axora.Desktop.Services;

/// <summary>
/// Deterministic extractor engine for single-frame and multi-frame TIFF documents.
/// Enumerates TIFF frames deterministically via native Windows Imaging Component (WIC),
/// processes frames sequentially (one frame at a time with zero multi-frame retention),
/// and transcribes text via on-device <see cref="IOcrEngine"/>.
/// Enforces Two-Tier text model: RawText holds ground truth, NormalizedText is strictly null.
/// PageSemantics is strictly PhysicalPage with sequential 1-indexed PageNumber.
/// Source files and streams are strictly immutable and never modified.
/// </summary>
public sealed class TiffDocumentExtractorEngine : IDocumentExtractorEngine
{
    private readonly IOcrEngine _ocrEngine;
    private readonly IOcrCapabilityStateProvider _capabilityProvider;
    private readonly IDocumentFormatDetector _formatDetector;
    private readonly ILogger<TiffDocumentExtractorEngine>? _logger;

    /// <inheritdoc/>
    public string EngineIdentifier => "TiffDocumentExtractorEngine";

    public TiffDocumentExtractorEngine(
        IOcrEngine? ocrEngine = null,
        IOcrCapabilityStateProvider? capabilityProvider = null,
        IDocumentFormatDetector? formatDetector = null,
        ILogger<TiffDocumentExtractorEngine>? logger = null)
    {
        _capabilityProvider = capabilityProvider ?? new WindowsOcrCapabilityStateProvider();
        _ocrEngine = ocrEngine ?? new WindowsMediaOcrEngine(_capabilityProvider);
        _formatDetector = formatDetector ?? new DocumentFormatDetector();
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool CanExtract(DetectedDocumentFormat format)
    {
        return format == DetectedDocumentFormat.MultiPageTiff;
    }

    /// <inheritdoc/>
    public async Task<RawExtractionResult> ExtractAsync(
        Stream documentStream,
        ExtractionOptions options,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(documentStream);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        progress?.Report(0.05);
        ct.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();

        // 1. Security Check: Stream length against MaxFileSizeBytes if seekable
        if (documentStream.CanSeek && documentStream.Length > options.Security.MaxFileSizeBytes)
        {
            throw new FileSizeLimitExceededException(documentStream.Length, options.Security.MaxFileSizeBytes);
        }

        // 2. Stream Ingestion: Read caller stream safely while preserving original stream position
        long initialPos = documentStream.CanSeek ? documentStream.Position : 0;
        byte[] tiffBytes;
        try
        {
            using var memStream = new MemoryStream();
            await documentStream.CopyToAsync(memStream, ct);
            tiffBytes = memStream.ToArray();
        }
        finally
        {
            if (documentStream.CanSeek)
            {
                try { documentStream.Position = initialPos; } catch { }
            }
        }

        if (tiffBytes.Length == 0)
        {
            throw new DocumentCorruptException("Cannot extract content from empty TIFF stream (0 bytes).");
        }

        if (tiffBytes.Length > options.Security.MaxFileSizeBytes)
        {
            throw new FileSizeLimitExceededException(tiffBytes.Length, options.Security.MaxFileSizeBytes);
        }

        ct.ThrowIfCancellationRequested();
        progress?.Report(0.15);

        // 3. Format Sniffing & Validation
        using (var detectionStream = new MemoryStream(tiffBytes))
        {
            var detection = _formatDetector.DetectFormat(detectionStream);
            if (detection.Format != DetectedDocumentFormat.MultiPageTiff)
            {
                if (detection.Format == DetectedDocumentFormat.Unknown)
                {
                    throw new DocumentCorruptException($"Unsupported or corrupted TIFF file. Detected MIME: '{detection.MimeType}'.");
                }

                throw new UnsupportedDocumentFormatException(detection.MimeType, $"Document format '{detection.Format}' is not supported by TiffDocumentExtractorEngine.");
            }
        }

        // 4. Initialize WIC BitmapDecoder
        using var inMemStream = new MemoryStream(tiffBytes, writable: false);
        using var inRaStream = inMemStream.AsRandomAccessStream();
        inRaStream.Seek(0);

        BitmapDecoder decoder;
        try
        {
            decoder = await BitmapDecoder.CreateAsync(inRaStream).AsTask(ct);
        }
        catch (Exception ex) when (ex is not ScholarExtractionException && ex is not OperationCanceledException)
        {
            throw new DocumentCorruptException($"Failed to decode TIFF image container: {ex.Message}", ex);
        }

        uint frameCount = decoder.FrameCount;
        if (frameCount == 0)
        {
            throw new DocumentCorruptException("TIFF document container contains 0 frames.");
        }

        // 5. Page/Frame Security Limits
        if (options.MaxPages.HasValue && frameCount > (uint)options.MaxPages.Value)
        {
            throw new PageLimitExceededException((int)frameCount, options.MaxPages.Value);
        }
        if (frameCount > (uint)options.Security.MaxPagesToExtract)
        {
            throw new PageLimitExceededException((int)frameCount, options.Security.MaxPagesToExtract);
        }

        // 6. Upfront Capability & Language Verification
        if (_capabilityProvider.State == OcrCapabilityState.OcrUnavailable)
        {
            throw new OcrUnavailableException("On-device OCR capability is not available on this system.");
        }

        if (_capabilityProvider.State == OcrCapabilityState.OcrFailed)
        {
            throw new OcrExecutionException("On-device OCR engine is in a failed state.");
        }

        if (!string.IsNullOrWhiteSpace(options.OcrLanguage))
        {
            if (_capabilityProvider is WindowsOcrCapabilityStateProvider winProvider)
            {
                var langState = winProvider.CheckLanguageState(options.OcrLanguage);
                if (langState == OcrCapabilityState.OcrLanguageUnavailable)
                {
                    throw new OcrLanguageUnavailableException(options.OcrLanguage, $"Requested OCR language '{options.OcrLanguage}' is not installed.");
                }
                if (langState == OcrCapabilityState.OcrUnavailable)
                {
                    throw new OcrUnavailableException($"OCR capability is unavailable for requested language '{options.OcrLanguage}'.");
                }
            }
            else if (_capabilityProvider.InstalledLanguages != null &&
                     _capabilityProvider.InstalledLanguages.Count > 0 &&
                     !_capabilityProvider.InstalledLanguages.Any(l => string.Equals(l, options.OcrLanguage, StringComparison.OrdinalIgnoreCase)))
            {
                throw new OcrLanguageUnavailableException(options.OcrLanguage, $"Requested OCR language '{options.OcrLanguage}' is not installed.");
            }
        }

        // 7. Sequential Single-Frame Extraction Loop
        int totalFrames = (int)frameCount;
        var pages = new List<ExtractedPageRaw>(totalFrames);
        var globalWarnings = new List<string>();
        int successfulFrames = 0;
        int totalExtractedChars = 0;

        for (int i = 0; i < totalFrames; i++)
        {
            ct.ThrowIfCancellationRequested();

            BitmapFrame frame;
            try
            {
                frame = await decoder.GetFrameAsync((uint)i).AsTask(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                string frameErr = $"ERR_TIFF_FRAME_CORRUPT: Failed to read frame {i + 1}: {ex.Message}";
                globalWarnings.Add(frameErr);
                pages.Add(new ExtractedPageRaw
                {
                    PageNumber = i + 1,
                    PageSemantics = PageSemanticsType.PhysicalPage,
                    WidthPt = 0,
                    HeightPt = 0,
                    RawText = string.Empty,
                    NormalizedText = null,
                    ExtractedViaOcr = false,
                    Confidence = 0.0,
                    DiagnosticWarning = frameErr
                });
                continue;
            }

            uint pixelW = frame.PixelWidth;
            uint pixelH = frame.PixelHeight;

            // Security: MaxImageDimensionPx check
            if (pixelW > (uint)options.Security.MaxImageDimensionPx || pixelH > (uint)options.Security.MaxImageDimensionPx)
            {
                throw new ImageDimensionExceededException((int)pixelW, (int)pixelH, options.Security.MaxImageDimensionPx);
            }

            // Physical dimensions derived from DPI or 96 DIP baseline
            double dpiX = frame.DpiX;
            double dpiY = frame.DpiY;
            double widthPt = (dpiX > 0 && Math.Abs(dpiX - 96.0) > 1.0) ? (pixelW / dpiX) * 72.0 : (pixelW * 72.0 / 96.0);
            double heightPt = (dpiY > 0 && Math.Abs(dpiY - 96.0) > 1.0) ? (pixelH / dpiY) * 72.0 : (pixelH * 72.0 / 96.0);

            string frameRawText = string.Empty;
            bool extractedViaOcr = false;
            double confidence = 1.0;
            string? frameWarning = null;

            try
            {
                // Retrieve pixel data with WIC EXIF orientation applied
                var pixelData = await frame.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    new BitmapTransform(),
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.ColorManageToSRgb).AsTask(ct);

                byte[] bgraPixels = pixelData.DetachPixelData();
                uint orientedW = frame.OrientedPixelWidth != 0 ? frame.OrientedPixelWidth : pixelW;
                uint orientedH = frame.OrientedPixelHeight != 0 ? frame.OrientedPixelHeight : pixelH;

                // Render into single-frame PNG memory stream for IOcrEngine using InMemoryRandomAccessStream
                using var framePngStream = new MemoryStream();
                using (var inMemRaStream = new InMemoryRandomAccessStream())
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, inMemRaStream).AsTask(ct);
                    encoder.SetPixelData(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied,
                        orientedW,
                        orientedH,
                        dpiX > 0 ? dpiX : 96.0,
                        dpiY > 0 ? dpiY : 96.0,
                        bgraPixels);
                    await encoder.FlushAsync().AsTask(ct);

                    inMemRaStream.Seek(0);
                    await inMemRaStream.AsStreamForRead().CopyToAsync(framePngStream, ct);
                }
                framePngStream.Position = 0;

                ct.ThrowIfCancellationRequested();

                // Recognize image via IOcrEngine
                var ocrResult = await _ocrEngine.RecognizeImageAsync(framePngStream, options.OcrLanguage, ct);

                if (!string.IsNullOrWhiteSpace(ocrResult.Text))
                {
                    extractedViaOcr = true;
                    successfulFrames++;
                    frameRawText = ocrResult.Text;
                    confidence = ocrResult.Confidence;
                    if (ocrResult.Warnings != null && ocrResult.Warnings.Count > 0)
                    {
                        frameWarning = string.Join("; ", ocrResult.Warnings);
                    }
                }
                else
                {
                    confidence = 0.0;
                }
            }
            catch (OcrUnavailableException ocrEx)
            {
                string msg = $"ERR_OCR_UNAVAILABLE: {ocrEx.Message}";
                frameWarning = frameWarning != null ? $"{frameWarning}; {msg}" : msg;
                globalWarnings.Add($"Frame {i + 1}: {msg}");
                confidence = 0.0;
            }
            catch (OcrLanguageUnavailableException langEx)
            {
                string msg = $"ERR_OCR_LANGUAGE_UNAVAILABLE: {langEx.Message}";
                frameWarning = frameWarning != null ? $"{frameWarning}; {msg}" : msg;
                globalWarnings.Add($"Frame {i + 1}: {msg}");
                confidence = 0.0;
            }
            catch (OcrExecutionException execEx)
            {
                string msg = $"ERR_OCR_INTERNAL_FAILURE: {execEx.Message}";
                frameWarning = frameWarning != null ? $"{frameWarning}; {msg}" : msg;
                globalWarnings.Add($"Frame {i + 1}: {msg}");
                confidence = 0.0;
            }
            catch (ArgumentException argEx) when (argEx.Message.Contains("40x40") || argEx.Message.Contains("below the minimum"))
            {
                string msg = $"Image dimensions ({pixelW}x{pixelH}) are below the minimum 40x40 pixels required for OCR.";
                frameWarning = frameWarning != null ? $"{frameWarning}; {msg}" : msg;
                globalWarnings.Add($"Frame {i + 1}: {msg}");
                confidence = 0.0;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                string msg = $"ERR_OCR_FAILED: Frame {i + 1} OCR execution failed: {ex.Message}";
                frameWarning = frameWarning != null ? $"{frameWarning}; {msg}" : msg;
                globalWarnings.Add(msg);
                confidence = 0.0;
            }

            totalExtractedChars += frameRawText.Length;
            if (totalExtractedChars > options.Security.MaxXmlDocumentChars)
            {
                throw new ExtractionSecurityLimitException(
                    "ERR_SECURITY_LIMIT_EXCEEDED",
                    "Extracted TIFF character count exceeds local safety threshold.",
                    $"Extracted {totalExtractedChars} characters exceeding threshold {options.Security.MaxXmlDocumentChars}.");
            }

            pages.Add(new ExtractedPageRaw
            {
                PageNumber = i + 1,
                PageSemantics = PageSemanticsType.PhysicalPage,
                WidthPt = widthPt,
                HeightPt = heightPt,
                RawText = frameRawText,
                NormalizedText = null, // Two-Tier text invariant
                ExtractedViaOcr = extractedViaOcr,
                Confidence = confidence,
                DiagnosticWarning = frameWarning
            });

            double stepProgress = 0.15 + (0.80 * ((double)(i + 1) / totalFrames));
            progress?.Report(stepProgress);
        }

        // 8. Result Assembly & Partial Success determination
        bool isPartialSuccess = successfulFrames > 0 && successfulFrames < totalFrames;
        stopwatch.Stop();
        progress?.Report(1.0);

        string docTitle = "TIFF Document";
        if (documentStream is FileStream fs && !string.IsNullOrWhiteSpace(fs.Name))
        {
            try
            {
                string name = Path.GetFileName(fs.Name);
                if (!string.IsNullOrWhiteSpace(name)) docTitle = name;
            }
            catch { }
        }

        return new RawExtractionResult
        {
            DocumentTitle = docTitle,
            Author = string.Empty,
            Format = DetectedDocumentFormat.MultiPageTiff,
            Pages = pages,
            Duration = stopwatch.Elapsed,
            EngineIdentifier = EngineIdentifier,
            GlobalWarnings = globalWarnings,
            IsPartialSuccess = isPartialSuccess
        };
    }
}
