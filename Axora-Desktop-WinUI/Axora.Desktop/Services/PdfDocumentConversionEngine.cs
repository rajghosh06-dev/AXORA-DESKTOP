using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.Content;
using PdfSharpCore.Pdf.Content.Objects;
using PdfSharpCore.Pdf.IO;
using SkiaSharp;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Native PDF document and vector conversion engine using PdfSharpCore and SkiaSharp.
/// Executes offline, local-first conversion for:
/// 1. Text (.txt) -> PDF (.pdf)
/// 2. Markdown (.md) -> PDF (.pdf)
/// 3. PDF (.pdf) -> Text (.txt)
/// 4. Image sequence (.png, .jpg, .jpeg, .bmp, .tiff, .tif, .webp) -> Single PDF (.pdf)
/// Strictly non-destructive: never alters or locks original source files.
/// </summary>
public sealed class PdfDocumentConversionEngine : IConversionEngine
{
    private readonly ILogger<PdfDocumentConversionEngine> _logger;

    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".webp"
    };

    public string EngineId => "pdfsharp";
    public string DisplayName => "Native PDF Document Engine (PdfSharpCore)";
    public bool IsAvailable => true;
    public string? RequiredDependencyId => null;

    public EngineResourceProfile ResourceProfile => EngineResourceProfile.MemoryBoundHeavy;

    public PdfDocumentConversionEngine(ILogger<PdfDocumentConversionEngine>? logger = null)
    {
        _logger = logger ?? NullLogger<PdfDocumentConversionEngine>.Instance;
    }

    /// <inheritdoc/>
    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        string src = NormalizeExtension(sourceExtension);
        string tgt = NormalizeExtension(targetExtension);

        // PDF -> TXT
        if (src == ".pdf" && tgt == ".txt")
            return true;

        // Target PDF
        if (tgt == ".pdf")
        {
            if (src is ".txt" or ".md" or ".markdown")
                return true;

            if (SupportedImageExtensions.Contains(src))
                return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertAsync(
        ConversionJob job,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        var stopwatch = Stopwatch.StartNew();

        if (ct.IsCancellationRequested || job.IsCancellationRequested)
        {
            return ConversionResult.Cancelled(stopwatch.Elapsed);
        }

        string srcExt = NormalizeExtension(job.SourceExtension);
        string tgtExt = NormalizeExtension(job.TargetExtension);

        // Handle image sequence if SourceFileSequence is populated
        if (job.SourceFileSequence != null && job.SourceFileSequence.Count > 0 && tgtExt == ".pdf")
        {
            return await ConvertImageSequenceToPdfAsync(
                job.SourceFileSequence,
                job.OutputFilePath,
                job.Profile,
                progress,
                ct).ConfigureAwait(false);
        }

        if (!CanConvert(srcExt, tgtExt))
        {
            return ConversionResult.Failure(
                "ERR_FORMAT_NOT_SUPPORTED",
                $"Format conversion from '{srcExt}' to '{tgtExt}' is not supported by {DisplayName}.",
                "Supported conversions: .txt/.md/images -> .pdf, and .pdf -> .txt.");
        }

        // Validate source file existence
        if (string.IsNullOrWhiteSpace(job.SourceFilePath) || !File.Exists(job.SourceFilePath))
        {
            return ConversionResult.Failure(
                "ERR_INPUT_NOT_FOUND",
                "The source file does not exist or cannot be accessed.",
                $"Source file not found: '{job.SourceFilePath}'");
        }

        // Validate source size
        var sourceInfo = new FileInfo(job.SourceFilePath);
        if (sourceInfo.Length == 0)
        {
            return ConversionResult.Failure(
                "ERR_INPUT_EMPTY",
                "The source file is 0 bytes and contains no content to convert.",
                $"File length is 0 bytes for: '{job.SourceFilePath}'");
        }

        // Non-destructive invariant: output cannot be source
        if (string.Equals(
            Path.GetFullPath(job.SourceFilePath),
            Path.GetFullPath(string.IsNullOrWhiteSpace(job.OutputFilePath) ? job.SourceFilePath : job.OutputFilePath),
            StringComparison.OrdinalIgnoreCase))
        {
            return ConversionResult.Failure(
                "ERR_OUTPUT_SAME_AS_SOURCE",
                "Destination path cannot match source path.",
                "Non-destructive file invariant prevented in-place overwrite.");
        }

        // Check source file readability (detect exclusive locks early)
        try
        {
            using var testStream = new FileStream(job.SourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (IOException ioEx)
        {
            return ConversionResult.Failure(
                "ERR_INPUT_READ_FAILED",
                $"The source file '{Path.GetFileName(job.SourceFilePath)}' is locked or in use by another process.",
                ioEx.Message);
        }
        catch (UnauthorizedAccessException authEx)
        {
            return ConversionResult.Failure(
                "ERR_INPUT_ACCESS_DENIED",
                $"Access to source file '{Path.GetFileName(job.SourceFilePath)}' was denied.",
                authEx.Message);
        }

        // Ensure output directory exists
        string outputDir = Path.GetDirectoryName(job.OutputFilePath) ?? string.Empty;
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                return ConversionResult.Failure(
                    "ERR_OUTPUT_WRITE_FAILED",
                    "Failed to create destination directory.",
                    ex.Message);
            }
        }

        // Route by conversion pair
        try
        {
            if (srcExt == ".txt" && tgtExt == ".pdf")
            {
                return await ConvertTxtToPdfAsync(job, progress, ct).ConfigureAwait(false);
            }
            else if ((srcExt == ".md" || srcExt == ".markdown") && tgtExt == ".pdf")
            {
                return await ConvertMarkdownToPdfAsync(job, progress, ct).ConfigureAwait(false);
            }
            else if (srcExt == ".pdf" && tgtExt == ".txt")
            {
                return await ConvertPdfToTxtAsync(job, progress, ct).ConfigureAwait(false);
            }
            else if (SupportedImageExtensions.Contains(srcExt) && tgtExt == ".pdf")
            {
                return await ConvertImageSequenceToPdfAsync(
                    new[] { job.SourceFilePath },
                    job.OutputFilePath,
                    job.Profile,
                    progress,
                    ct).ConfigureAwait(false);
            }
            else
            {
                return ConversionResult.Failure(
                    "ERR_FORMAT_NOT_SUPPORTED",
                    $"Conversion from '{srcExt}' to '{tgtExt}' is not supported.",
                    $"Unsupported route: {srcExt} -> {tgtExt}");
            }
        }
        catch (OperationCanceledException)
        {
            TryCleanupFile(job.OutputFilePath);
            return ConversionResult.Cancelled(stopwatch.Elapsed);
        }
        catch (UnauthorizedAccessException uex)
        {
            TryCleanupFile(job.OutputFilePath);
            return ConversionResult.Failure(
                "ERR_INPUT_ACCESS_DENIED",
                "Access denied reading the source file.",
                uex.Message);
        }
        catch (IOException ioEx)
        {
            TryCleanupFile(job.OutputFilePath);
            return ConversionResult.Failure(
                "ERR_INPUT_READ_FAILED",
                "Failed to open or read the source file.",
                ioEx.Message);
        }
        catch (OutOfMemoryException oom)
        {
            TryCleanupFile(job.OutputFilePath);
            return ConversionResult.Failure(
                "ERR_INSUFFICIENT_MEMORY",
                "Insufficient memory to process this document.",
                oom.ToString());
        }
        catch (Exception ex)
        {
            TryCleanupFile(job.OutputFilePath);
            return ConversionResult.Failure(
                "ERR_OUTPUT_WRITE_FAILED",
                "An unexpected error occurred during document conversion.",
                ex.Message);
        }
    }

    /// <summary>
    /// Packages a sequence of raster images into a single multi-page PDF document.
    /// Preserves image ordering, scales to page with aspect ratio preservation (no distortion),
    /// and handles cancellation cleanly between pages.
    /// </summary>
    public async Task<ConversionResult> ConvertImageSequenceToPdfAsync(
        IReadOnlyList<string> imagePaths,
        string outputPath,
        ConversionProfile? profile = null,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imagePaths);
        var stopwatch = Stopwatch.StartNew();

        if (imagePaths.Count == 0)
        {
            return ConversionResult.Failure(
                "ERR_INPUT_EMPTY",
                "No images provided in the image sequence.",
                "Image list is empty.");
        }

        // Validate each image path
        for (int i = 0; i < imagePaths.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var imgPath = imagePaths[i];

            if (string.IsNullOrWhiteSpace(imgPath) || !File.Exists(imgPath))
            {
                return ConversionResult.Failure(
                    "ERR_INPUT_NOT_FOUND",
                    $"Source image not found: '{Path.GetFileName(imgPath)}'",
                    $"Path does not exist: {imgPath}");
            }

            var fi = new FileInfo(imgPath);
            if (fi.Length == 0)
            {
                return ConversionResult.Failure(
                    "ERR_INPUT_EMPTY",
                    $"Source image is 0 bytes: '{Path.GetFileName(imgPath)}'",
                    $"0-byte image file: {imgPath}");
            }

            string ext = NormalizeExtension(Path.GetExtension(imgPath));
            if (!SupportedImageExtensions.Contains(ext))
            {
                return ConversionResult.Failure(
                    "ERR_FORMAT_NOT_SUPPORTED",
                    $"Image format '{ext}' is not supported for PDF packaging.",
                    $"Unsupported extension on file: {imgPath}");
            }

            if (string.Equals(Path.GetFullPath(imgPath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
            {
                return ConversionResult.Failure(
                    "ERR_OUTPUT_SAME_AS_SOURCE",
                    "Destination PDF path cannot match any source image path.",
                    "Non-destructive invariant violated.");
            }
        }

        // Ensure output dir
        string outDir = Path.GetDirectoryName(outputPath) ?? string.Empty;
        if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        return await Task.Run(async () =>
        {
            using var pdfDoc = new PdfDocument();
            ApplyMetadata(pdfDoc, "Axora Scanned Package", profile);

            const double margin = 20.0;

            for (int i = 0; i < imagePaths.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var imgPath = imagePaths[i];

                SKBitmap? skBitmap;
                try
                {
                    skBitmap = await DecodeImageFileAsync(imgPath, ct).ConfigureAwait(false);
                }
                catch (IOException ioEx)
                {
                    return ConversionResult.Failure(
                        "ERR_INPUT_READ_FAILED",
                        $"Failed to read image '{Path.GetFileName(imgPath)}'. File may be locked.",
                        ioEx.Message);
                }

                if (skBitmap == null)
                {
                    return ConversionResult.Failure(
                        "ERR_INPUT_CORRUPT",
                        $"Unable to decode image '{Path.GetFileName(imgPath)}'. File header may be corrupt.",
                        $"DecodeImageFileAsync returned null for: {imgPath}");
                }

                using (skBitmap)
                {
                    // Re-encode to high-fidelity PNG stream for PdfSharpCore XImage embedding
                    using var pngData = skBitmap.Encode(SKEncodedImageFormat.Png, 100);
                    if (pngData == null)
                    {
                        return ConversionResult.Failure(
                            "ERR_INPUT_CORRUPT",
                            $"Failed to encode bitmap data for '{Path.GetFileName(imgPath)}'.",
                            "SkiaSharp PNG encode returned null.");
                    }

                    byte[] pngBytes = pngData.ToArray();
                    using var xImage = XImage.FromStream(() => new MemoryStream(pngBytes));

                    var page = pdfDoc.AddPage();
                    // Set orientation matching image aspect ratio
                    page.Orientation = xImage.PixelWidth > xImage.PixelHeight
                        ? PageOrientation.Landscape
                        : PageOrientation.Portrait;

                    double availW = page.Width - (2 * margin);
                    double availH = page.Height - (2 * margin);

                    // Preserve aspect ratio without distortion
                    double scale = Math.Min(availW / xImage.PixelWidth, availH / xImage.PixelHeight);
                    double drawW = xImage.PixelWidth * scale;
                    double drawH = xImage.PixelHeight * scale;
                    double drawX = margin + ((availW - drawW) / 2);
                    double drawY = margin + ((availH - drawH) / 2);

                    using var gfx = XGraphics.FromPdfPage(page);
                    gfx.DrawImage(xImage, drawX, drawY, drawW, drawH);
                }

                double pct = ((i + 1.0) / imagePaths.Count) * 100.0;
                progress?.Report(pct);
            }

            ct.ThrowIfCancellationRequested();
            pdfDoc.Save(outputPath);

            var outFi = new FileInfo(outputPath);
            stopwatch.Stop();
            _logger.LogInformation("Packaged {Count} images into PDF '{Path}' ({Bytes}B in {Elapsed}ms)",
                imagePaths.Count, outputPath, outFi.Length, stopwatch.ElapsedMilliseconds);

            return ConversionResult.Success(outputPath, outFi.Length, stopwatch.Elapsed);
        }, ct);
    }

    private static async Task<SKBitmap?> DecodeImageFileAsync(string imgPath, CancellationToken ct)
    {
        // 1. Try SkiaSharp first (PNG, JPG, WebP, BMP)
        try
        {
            using var fileStream = new FileStream(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var codec = SKCodec.Create(fileStream);
            if (codec != null)
            {
                var bmp = SKBitmap.Decode(codec);
                if (bmp != null) return bmp;
            }
        }
        catch { /* Fallback to WIC */ }

        // 2. Fallback to Windows Imaging Component (WIC) for TIFF and other native formats
        try
        {
            using var fileStream = new FileStream(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var raStream = fileStream.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(raStream).AsTask(ct).ConfigureAwait(false);
            var pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                new BitmapTransform(),
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb).AsTask(ct).ConfigureAwait(false);

            var info = new SKImageInfo((int)decoder.PixelWidth, (int)decoder.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            var skBmp = new SKBitmap(info);
            byte[] pixels = pixelData.DetachPixelData();
            Marshal.Copy(pixels, 0, skBmp.GetPixels(), pixels.Length);
            return skBmp;
        }
        catch
        {
            return null;
        }
    }

    private async Task<ConversionResult> ConvertTxtToPdfAsync(
        ConversionJob job,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        return await Task.Run(() =>
        {
            using var pdfDoc = new PdfDocument();
            ApplyMetadata(pdfDoc, Path.GetFileNameWithoutExtension(job.SourceFilePath), job.Profile);

            const double pageWidth = 595.28; // A4 pt
            const double pageHeight = 841.89;
            const double margin = 40.0;
            const double printableWidth = pageWidth - (2 * margin);
            const double maxY = pageHeight - margin;

            var font = new XFont("Arial", 10, XFontStyle.Regular);
            var titleFont = new XFont("Arial", 14, XFontStyle.Bold);
            var brush = XBrushes.Black;
            var headerBrush = new XSolidBrush(XColor.FromArgb(255, 30, 45, 70));
            var dividerPen = new XPen(XColor.FromArgb(255, 220, 225, 230), 0.75);

            double lineHeight = 14.0;
            var currentPage = pdfDoc.AddPage();
            currentPage.Size = PageSize.A4;
            var gfx = XGraphics.FromPdfPage(currentPage);
            double currentY = margin;

            // Draw header if not stripped
            if (job.Profile.MetadataPolicy != MetadataHandling.Strip)
            {
                string headerTitle = Path.GetFileName(job.SourceFilePath);
                MarkdownProcessor.SafeDrawString(gfx, headerTitle, titleFont, headerBrush, new XPoint(margin, currentY + titleFont.Size));
                currentY += titleFont.Size + 6;
                gfx.DrawLine(dividerPen, margin, currentY, margin + printableWidth, currentY);
                currentY += 12;
            }

            void EnsureSpace(double requiredHeight)
            {
                if (currentY + requiredHeight > maxY)
                {
                    gfx.Dispose();
                    currentPage = pdfDoc.AddPage();
                    currentPage.Size = PageSize.A4;
                    gfx = XGraphics.FromPdfPage(currentPage);
                    currentY = margin;
                }
            }

            try
            {
                using var stream = new FileStream(job.SourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    ct.ThrowIfCancellationRequested();

                    if (string.IsNullOrEmpty(line))
                    {
                        EnsureSpace(lineHeight);
                        currentY += lineHeight;
                        continue;
                    }

                    // Word wrap line with support for oversized continuous tokens
                    var words = line.Split(' ');
                    string currentLineText = "";

                    foreach (var word in words)
                    {
                        ct.ThrowIfCancellationRequested();

                        // If word itself exceeds printable width, slice it
                        var wordMeasure = gfx.MeasureString(word, font);
                        if (wordMeasure.Width > printableWidth)
                        {
                            if (!string.IsNullOrEmpty(currentLineText))
                            {
                                EnsureSpace(lineHeight);
                                MarkdownProcessor.SafeDrawString(gfx, currentLineText, font, brush, new XPoint(margin, currentY + font.Size));
                                currentY += lineHeight;
                                currentLineText = "";
                            }

                            int charIdx = 0;
                            while (charIdx < word.Length)
                            {
                                ct.ThrowIfCancellationRequested();
                                int take = 1;
                                while (charIdx + take <= word.Length && gfx.MeasureString(word.Substring(charIdx, take), font).Width <= printableWidth)
                                {
                                    take++;
                                }
                                take = Math.Max(1, take - 1);
                                var chunk = word.Substring(charIdx, take);
                                EnsureSpace(lineHeight);
                                MarkdownProcessor.SafeDrawString(gfx, chunk, font, brush, new XPoint(margin, currentY + font.Size));
                                currentY += lineHeight;
                                charIdx += take;
                            }
                            continue;
                        }

                        var testLine = string.IsNullOrEmpty(currentLineText) ? word : $"{currentLineText} {word}";
                        var size = gfx.MeasureString(testLine, font);

                        if (size.Width > printableWidth && !string.IsNullOrEmpty(currentLineText))
                        {
                            EnsureSpace(lineHeight);
                            MarkdownProcessor.SafeDrawString(gfx, currentLineText, font, brush, new XPoint(margin, currentY + font.Size));
                            currentY += lineHeight;
                            currentLineText = word;
                        }
                        else
                        {
                            currentLineText = testLine;
                        }
                    }

                    if (!string.IsNullOrEmpty(currentLineText))
                    {
                        EnsureSpace(lineHeight);
                        MarkdownProcessor.SafeDrawString(gfx, currentLineText, font, brush, new XPoint(margin, currentY + font.Size));
                        currentY += lineHeight;
                    }
                }
            }
            finally
            {
                gfx.Dispose();
            }

            ct.ThrowIfCancellationRequested();
            pdfDoc.Save(job.OutputFilePath);

            var outFi = new FileInfo(job.OutputFilePath);
            stopwatch.Stop();
            progress?.Report(100.0);

            _logger.LogInformation("Converted TXT -> PDF '{Path}' ({Bytes}B in {Elapsed}ms)",
                job.OutputFilePath, outFi.Length, stopwatch.ElapsedMilliseconds);

            return ConversionResult.Success(job.OutputFilePath, outFi.Length, stopwatch.Elapsed);
        }, ct);
    }

    private async Task<ConversionResult> ConvertMarkdownToPdfAsync(
        ConversionJob job,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        string markdownText;
        using (var stream = new FileStream(job.SourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            markdownText = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        }

        if (ct.IsCancellationRequested || job.IsCancellationRequested)
        {
            return ConversionResult.Cancelled(stopwatch.Elapsed);
        }

        progress?.Report(30.0);

        return await Task.Run(() =>
        {
            using var pdfDoc = new PdfDocument();
            ApplyMetadata(pdfDoc, Path.GetFileNameWithoutExtension(job.SourceFilePath), job.Profile);

            MarkdownProcessor.RenderToPdf(markdownText, pdfDoc, job.Profile, ct);

            ct.ThrowIfCancellationRequested();
            pdfDoc.Save(job.OutputFilePath);

            var outFi = new FileInfo(job.OutputFilePath);
            stopwatch.Stop();
            progress?.Report(100.0);

            _logger.LogInformation("Converted Markdown -> PDF '{Path}' ({Bytes}B in {Elapsed}ms)",
                job.OutputFilePath, outFi.Length, stopwatch.ElapsedMilliseconds);

            return ConversionResult.Success(job.OutputFilePath, outFi.Length, stopwatch.Elapsed);
        }, ct);
    }

    private async Task<ConversionResult> ConvertPdfToTxtAsync(
        ConversionJob job,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        return await Task.Run(() =>
        {
            PdfDocument document;
            try
            {
                using var fileStream = new FileStream(job.SourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                document = PdfReader.Open(fileStream, PdfDocumentOpenMode.ReadOnly);
            }
            catch (PdfReaderException prEx)
            {
                return ConversionResult.Failure(
                    "ERR_INPUT_CORRUPT",
                    "The source PDF file is corrupted or could not be parsed.",
                    prEx.Message);
            }
            catch (Exception ex)
            {
                return ConversionResult.Failure(
                    "ERR_INPUT_READ_FAILED",
                    "Failed to open source PDF file.",
                    ex.Message);
            }

            using (document)
            {
                if (document.PageCount == 0)
                {
                    return ConversionResult.Failure(
                        "ERR_INPUT_EMPTY",
                        "The source PDF document contains no pages.",
                        "Page count is 0.");
                }

                var sb = new StringBuilder();
                int totalTextChars = 0;

                for (int i = 0; i < document.PageCount; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var page = document.Pages[i];

                    try
                    {
                        string pageText = PdfTextExtractor.ExtractTextFromPage(page).Trim();
                        if (!string.IsNullOrEmpty(pageText))
                        {
                            sb.AppendLine($"--- Page {i + 1} ---");
                            sb.AppendLine(pageText);
                            sb.AppendLine();
                            totalTextChars += pageText.Length;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to extract content on page {Page} of '{File}'", i + 1, job.SourceFilePath);
                    }

                    double pct = ((i + 1.0) / document.PageCount) * 80.0;
                    progress?.Report(pct);
                }

                // If zero extractable text found across all pages, return ERR_NO_EXTRACTABLE_TEXT
                if (totalTextChars == 0)
                {
                    return ConversionResult.Failure(
                        "ERR_NO_EXTRACTABLE_TEXT",
                        "This PDF contains no extractable text. OCR is required.",
                        "Zero text objects or content streams containing text characters were found in the PDF.");
                }

                ct.ThrowIfCancellationRequested();

                var utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                File.WriteAllText(job.OutputFilePath, sb.ToString().TrimEnd(), utf8WithoutBom);

                var outFi = new FileInfo(job.OutputFilePath);
                stopwatch.Stop();
                progress?.Report(100.0);

                _logger.LogInformation("Converted PDF -> TXT '{Path}' ({Bytes}B, {Chars} chars in {Elapsed}ms)",
                    job.OutputFilePath, outFi.Length, totalTextChars, stopwatch.ElapsedMilliseconds);

                return ConversionResult.Success(job.OutputFilePath, outFi.Length, stopwatch.Elapsed);
            }
        }, ct);
    }

    private static void ApplyMetadata(PdfDocument doc, string title, ConversionProfile? profile)
    {
        if (profile?.MetadataPolicy == MetadataHandling.Strip)
        {
            doc.Info.Title = string.Empty;
            doc.Info.Author = string.Empty;
            doc.Info.Subject = string.Empty;
            doc.Info.Creator = string.Empty;
        }
        else
        {
            doc.Info.Title = title;
            doc.Info.Author = "Axora Desktop";
            doc.Info.Subject = "Universal Converter Export";
            doc.Info.Creator = "Axora Universal Converter";
        }
    }

    private static string NormalizeExtension(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return string.Empty;
        var trimmed = ext.Trim().ToLowerInvariant();
        return trimmed.StartsWith('.') ? trimmed : $".{trimmed}";
    }

    private static void TryCleanupFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { /* best-effort cleanup */ }
    }
}
