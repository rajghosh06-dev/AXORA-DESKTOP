using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Axora.Desktop.Services;

/// <summary>
/// Deterministic extractor engine for standalone raster images (PNG, JPEG, BMP, WebP).
/// Adapts on-device OCR through <see cref="IOcrEngine"/> into the Scholar extraction pipeline.
/// Strictly enforces the One-File = One Physical Page invariant with physical page semantics.
/// Two-Tier text invariant: RawText preserves exact recognized text; NormalizedText is strictly null.
/// Original source streams/files are read-only and never mutated.
/// </summary>
public sealed class RasterImageDocumentExtractorEngine : IDocumentExtractorEngine
{
    private readonly IOcrEngine _ocrEngine;
    private readonly IOcrCapabilityStateProvider _capabilityProvider;
    private readonly IDocumentFormatDetector _formatDetector;
    private readonly ILogger<RasterImageDocumentExtractorEngine>? _logger;

    /// <inheritdoc/>
    public string EngineIdentifier => "RasterImageDocumentExtractorEngine";

    public RasterImageDocumentExtractorEngine(
        IOcrEngine? ocrEngine = null,
        IOcrCapabilityStateProvider? capabilityProvider = null,
        IDocumentFormatDetector? formatDetector = null,
        ILogger<RasterImageDocumentExtractorEngine>? logger = null)
    {
        _capabilityProvider = capabilityProvider ?? new WindowsOcrCapabilityStateProvider();
        _ocrEngine = ocrEngine ?? new WindowsMediaOcrEngine(_capabilityProvider);
        _formatDetector = formatDetector ?? new DocumentFormatDetector();
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool CanExtract(DetectedDocumentFormat format)
    {
        return format == DetectedDocumentFormat.RasterImage;
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

        // 1. Security Check: Stream length against MaxFileSizeBytes
        if (documentStream.CanSeek && documentStream.Length > options.Security.MaxFileSizeBytes)
        {
            throw new FileSizeLimitExceededException(documentStream.Length, options.Security.MaxFileSizeBytes);
        }

        // 2. Stream Ingestion: Read caller stream into memory while preserving caller stream state
        long initialPos = documentStream.CanSeek ? documentStream.Position : 0;
        byte[] imageBytes;
        try
        {
            using var memStream = new MemoryStream();
            await documentStream.CopyToAsync(memStream, ct);
            imageBytes = memStream.ToArray();
        }
        finally
        {
            if (documentStream.CanSeek)
            {
                try { documentStream.Position = initialPos; } catch { }
            }
        }

        if (imageBytes.Length == 0)
        {
            throw new DocumentCorruptException("Cannot extract content from empty image stream (0 bytes).");
        }

        if (imageBytes.Length > options.Security.MaxFileSizeBytes)
        {
            throw new FileSizeLimitExceededException(imageBytes.Length, options.Security.MaxFileSizeBytes);
        }

        ct.ThrowIfCancellationRequested();
        progress?.Report(0.20);

        // 3. Format Sniffing & Validation
        using (var detectionStream = new MemoryStream(imageBytes))
        {
            var detection = _formatDetector.DetectFormat(detectionStream);
            if (detection.Format != DetectedDocumentFormat.RasterImage)
            {
                if (detection.Format == DetectedDocumentFormat.MultiPageTiff)
                {
                    throw new UnsupportedDocumentFormatException(detection.MimeType, "Multi-page TIFF format is not supported by RasterImageDocumentExtractorEngine.");
                }

                if (detection.Format == DetectedDocumentFormat.Unknown)
                {
                    throw new DocumentCorruptException($"Unsupported or corrupted image file. Detected MIME: '{detection.MimeType}'.");
                }

                throw new UnsupportedDocumentFormatException(detection.MimeType, $"Document format '{detection.Format}' is not supported by RasterImageDocumentExtractorEngine.");
            }
        }

        // 4. Raster Inspection (Dimensions & Syntax Verification via SkiaSharp)
        int widthPx = 0;
        int heightPx = 0;
        using (var codecStream = new MemoryStream(imageBytes))
        using (var codec = SKCodec.Create(codecStream))
        {
            if (codec == null)
            {
                throw new DocumentCorruptException("Failed to decode raster image header. Corrupted data or invalid image syntax.");
            }
            widthPx = codec.Info.Width;
            heightPx = codec.Info.Height;
        }

        // Security Check: Dimension Limit
        if (widthPx > options.Security.MaxImageDimensionPx || heightPx > options.Security.MaxImageDimensionPx)
        {
            throw new ImageDimensionExceededException(widthPx, heightPx, options.Security.MaxImageDimensionPx);
        }

        // 5. OCR Capability & Language Readiness Check
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

        ct.ThrowIfCancellationRequested();
        progress?.Report(0.50);

        // 6. Execute On-Device OCR Recognition
        using var ocrStream = new MemoryStream(imageBytes);
        OcrResult ocrResult;
        try
        {
            ocrResult = await _ocrEngine.RecognizeImageAsync(ocrStream, options.OcrLanguage, ct);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("40x40") || ex.Message.Contains("below the minimum"))
        {
            throw new DocumentCorruptException($"Image dimensions ({widthPx}x{heightPx}) are below the minimum 40x40 pixels required for OCR.", ex);
        }

        ct.ThrowIfCancellationRequested();
        progress?.Report(0.90);

        // 7. Assemble Single Physical Page (One File = One Physical Page)
        // Convert pixel dimensions to typography points (72 DIP/pt; 1 pt = 72/96 DIP)
        double widthPt = widthPx * 72.0 / 96.0;
        double heightPt = heightPx * 72.0 / 96.0;

        string? diagnosticWarning = null;
        if (ocrResult.Warnings != null && ocrResult.Warnings.Count > 0)
        {
            diagnosticWarning = string.Join("; ", ocrResult.Warnings);
        }

        var page = new ExtractedPageRaw
        {
            PageNumber = 1,
            PageSemantics = PageSemanticsType.PhysicalPage,
            WidthPt = widthPt,
            HeightPt = heightPt,
            RawText = ocrResult.Text,
            NormalizedText = null, // Strictly null (Two-Tier text invariant)
            ExtractedViaOcr = true,
            Confidence = ocrResult.Confidence,
            DiagnosticWarning = diagnosticWarning
        };

        string docTitle = "Raster Image Document";
        if (documentStream is FileStream fs && !string.IsNullOrWhiteSpace(fs.Name))
        {
            try
            {
                string name = Path.GetFileName(fs.Name);
                if (!string.IsNullOrWhiteSpace(name)) docTitle = name;
            }
            catch { }
        }

        stopwatch.Stop();
        progress?.Report(1.0);

        return new RawExtractionResult
        {
            DocumentTitle = docTitle,
            Author = string.Empty,
            Format = DetectedDocumentFormat.RasterImage,
            Pages = [page],
            Duration = stopwatch.Elapsed,
            EngineIdentifier = EngineIdentifier,
            GlobalWarnings = ocrResult.Warnings ?? [],
            IsPartialSuccess = false
        };
    }
}
