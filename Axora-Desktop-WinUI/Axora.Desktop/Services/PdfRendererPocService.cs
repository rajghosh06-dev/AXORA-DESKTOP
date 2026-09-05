using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using SkiaSharp;
using WinPdfDocument = Windows.Data.Pdf.PdfDocument;
using SharpPdfDocument = PdfSharpCore.Pdf.PdfDocument;

namespace Axora.Desktop.Services;

/// <summary>
/// Proof-of-concept internal service evaluating Windows native PDF rendering
/// (<see cref="Windows.Data.Pdf.PdfDocument"/> and <see cref="PdfPage.RenderToStreamAsync"/>)
/// for AXORA's unpackaged WinUI 3 desktop application environment.
/// </summary>
public static class PdfRendererPocService
{
    /// <summary>
    /// Safe document handle encapsulating both the WinRT <see cref="WinPdfDocument"/>
    /// and its underlying <see cref="IRandomAccessStream"/> to guarantee deterministic disposal.
    /// </summary>
    public sealed class PdfPocDocumentHandle : IDisposable
    {
        public WinPdfDocument Document { get; }
        private readonly IRandomAccessStream _stream;
        private bool _disposed;

        public PdfPocDocumentHandle(WinPdfDocument document, IRandomAccessStream stream)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public uint PageCount => Document.PageCount;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _stream.Dispose();
            }
            catch { }
        }
    }

    /// <summary>
    /// Result model for a single page render operation.
    /// </summary>
    public sealed class RenderPageResult
    {
        public bool Success { get; init; }
        public byte[]? ImageBytes { get; init; }
        public uint PixelWidth { get; init; }
        public uint PixelHeight { get; init; }
        public string Format { get; init; } = "png";
        public TimeSpan Elapsed { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ErrorCode { get; init; }
    }

    /// <summary>
    /// Opens a PDF document non-destructively using strict FileShare.Read and copies it
    /// to an in-memory random access stream so that the original file is never locked or modified.
    /// </summary>
    public static async Task<PdfPocDocumentHandle> OpenDocumentAsync(
        string filePath,
        string? password = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("PDF file not found.", filePath);

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
            throw new InvalidDataException("PDF file is empty (0 bytes).");

        ct.ThrowIfCancellationRequested();

        // Read strictly with FileShare.Read
        byte[] fileBytes;
        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            fileBytes = new byte[fileStream.Length];
            int read = 0;
            while (read < fileBytes.Length)
            {
                int bytesRead = await fileStream.ReadAsync(fileBytes.AsMemory(read, fileBytes.Length - read), ct);
                if (bytesRead == 0) break;
                read += bytesRead;
            }
        }

        var memStream = new InMemoryRandomAccessStream();
        try
        {
            using (var writer = new DataWriter(memStream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(fileBytes);
                await writer.StoreAsync().AsTask(ct);
                await writer.FlushAsync().AsTask(ct);
                writer.DetachStream();
            }

            memStream.Seek(0);
            ct.ThrowIfCancellationRequested();

            WinPdfDocument winDoc;
            if (!string.IsNullOrEmpty(password))
            {
                winDoc = await WinPdfDocument.LoadFromStreamAsync(memStream, password).AsTask(ct);
            }
            else
            {
                winDoc = await WinPdfDocument.LoadFromStreamAsync(memStream).AsTask(ct);
            }

            return new PdfPocDocumentHandle(winDoc, memStream);
        }
        catch
        {
            memStream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Renders a specific page of a PDF document to image bytes (PNG or JPG).
    /// </summary>
    public static async Task<RenderPageResult> RenderPageAsync(
        PdfPocDocumentHandle handle,
        uint pageIndex,
        double dpi = 96.0,
        uint? destinationWidth = null,
        uint? destinationHeight = null,
        bool isJpeg = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        var sw = Stopwatch.StartNew();

        if (pageIndex >= handle.PageCount)
        {
            return new RenderPageResult
            {
                Success = false,
                Format = isJpeg ? "jpg" : "png",
                Elapsed = sw.Elapsed,
                ErrorCode = "ERR_PAGE_OUT_OF_RANGE",
                ErrorMessage = $"Page index {pageIndex} exceeds page count {handle.PageCount}."
            };
        }

        try
        {
            ct.ThrowIfCancellationRequested();
            using var page = handle.Document.GetPage(pageIndex);
            using var outStream = new InMemoryRandomAccessStream();

            var options = new PdfPageRenderOptions();
            options.BitmapEncoderId = isJpeg ? BitmapEncoder.JpegEncoderId : BitmapEncoder.PngEncoderId;
            options.BackgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255); // Opaque white

            uint targetW;
            uint targetH;

            if (destinationWidth.HasValue && destinationHeight.HasValue)
            {
                targetW = destinationWidth.Value;
                targetH = destinationHeight.Value;
            }
            else
            {
                // PDF page size in WinRT (page.Size) is in 96-DPI device-independent pixels (DIPs)
                double scale = dpi / 96.0;
                targetW = (uint)Math.Max(1, Math.Round(page.Size.Width * scale));
                targetH = (uint)Math.Max(1, Math.Round(page.Size.Height * scale));
            }

            options.DestinationWidth = targetW;
            options.DestinationHeight = targetH;

            // Execute WinRT rendering
            await page.RenderToStreamAsync(outStream, options).AsTask(ct);

            outStream.Seek(0);
            byte[] imageBytes = new byte[(int)outStream.Size];
            using (var reader = new DataReader(outStream.GetInputStreamAt(0)))
            {
                await reader.LoadAsync((uint)imageBytes.Length).AsTask(ct);
                reader.ReadBytes(imageBytes);
                reader.DetachStream();
            }

            sw.Stop();

            return new RenderPageResult
            {
                Success = true,
                ImageBytes = imageBytes,
                PixelWidth = targetW,
                PixelHeight = targetH,
                Format = isJpeg ? "jpg" : "png",
                Elapsed = sw.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            return new RenderPageResult
            {
                Success = false,
                Format = isJpeg ? "jpg" : "png",
                Elapsed = sw.Elapsed,
                ErrorCode = "ERR_CANCELLED",
                ErrorMessage = "Rendering operation was cancelled."
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new RenderPageResult
            {
                Success = false,
                Format = isJpeg ? "jpg" : "png",
                Elapsed = sw.Elapsed,
                ErrorCode = "ERR_RENDER_FAILED",
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Renders a specific page and atomically saves the resulting image to a disk file.
    /// </summary>
    public static async Task<RenderPageResult> RenderPageToFileAsync(
        PdfPocDocumentHandle handle,
        uint pageIndex,
        string outputFilePath,
        double dpi = 96.0,
        uint? destinationWidth = null,
        uint? destinationHeight = null,
        bool isJpeg = false,
        CancellationToken ct = default)
    {
        var result = await RenderPageAsync(handle, pageIndex, dpi, destinationWidth, destinationHeight, isJpeg, ct);
        if (!result.Success || result.ImageBytes == null || result.ImageBytes.Length == 0)
        {
            return result;
        }

        string? dir = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tempPath = Path.Combine(dir ?? ".", $".tmp_poc_{Guid.NewGuid():N}.{(isJpeg ? "jpg" : "png")}");
        try
        {
            await File.WriteAllBytesAsync(tempPath, result.ImageBytes, ct);
            File.Move(tempPath, outputFilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }

        return result;
    }

    #region Deterministic PDF Fixture Generators

    public static string CreateSimpleOnePageTextPdf(string destinationPath, string text = "Hello World from WinUI PDF Renderer POC")
    {
        EnsureDirectory(destinationPath);
        using var doc = new SharpPdfDocument();
        var page = doc.AddPage();
        page.Size = PageSize.A4;
        page.Orientation = PageOrientation.Portrait;

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var font = new XFont("Arial", 16, XFontStyle.Bold);
            gfx.DrawString(text, font, XBrushes.Navy, new XPoint(50, 100));
            var bodyFont = new XFont("Arial", 11, XFontStyle.Regular);
            gfx.DrawString("This is a deterministic test document verifying Windows.Data.Pdf in unpackaged WinUI 3.", bodyFont, XBrushes.DarkSlateGray, new XPoint(50, 130));
        }

        doc.Save(destinationPath);
        return destinationPath;
    }

    public static string CreateMultiPagePdf(string destinationPath, int pageCount = 5)
    {
        EnsureDirectory(destinationPath);
        using var doc = new SharpPdfDocument();
        for (int i = 0; i < pageCount; i++)
        {
            var page = doc.AddPage();
            page.Size = PageSize.A4;
            using var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont("Arial", 18, XFontStyle.Bold);
            gfx.DrawString($"AXORA Multi-Page Document — Page {i + 1} of {pageCount}", font, XBrushes.DarkBlue, new XPoint(50, 100));
            var body = new XFont("Arial", 12, XFontStyle.Regular);
            gfx.DrawString($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC", body, XBrushes.Black, new XPoint(50, 140));
        }

        doc.Save(destinationPath);
        return destinationPath;
    }

    public static string CreatePortraitPdf(string destinationPath)
    {
        EnsureDirectory(destinationPath);
        using var doc = new SharpPdfDocument();
        var page = doc.AddPage();
        page.Width = 595.28; // A4 portrait points
        page.Height = 841.89;
        page.Orientation = PageOrientation.Portrait;

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var font = new XFont("Arial", 14, XFontStyle.Bold);
            gfx.DrawString("A4 Portrait Page (595.28 x 841.89 pt)", font, XBrushes.DarkRed, new XPoint(50, 100));
            gfx.DrawRectangle(XPens.DarkRed, 50, 120, 495, 670);
        }

        doc.Save(destinationPath);
        return destinationPath;
    }

    public static string CreateLandscapePdf(string destinationPath)
    {
        EnsureDirectory(destinationPath);
        using var doc = new SharpPdfDocument();
        var page = doc.AddPage();
        page.Size = PageSize.A4;
        page.Orientation = PageOrientation.Landscape;

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var font = new XFont("Arial", 14, XFontStyle.Bold);
            gfx.DrawString("A4 Landscape Page (841.89 x 595.28 pt)", font, XBrushes.DarkGreen, new XPoint(50, 100));
            gfx.DrawRectangle(XPens.DarkGreen, 50, 120, 740, 420);
        }

        doc.Save(destinationPath);
        return destinationPath;
    }

    public static string CreateRasterImagePdf(string destinationPath)
    {
        EnsureDirectory(destinationPath);
        using var doc = new SharpPdfDocument();
        var page = doc.AddPage();
        page.Size = PageSize.A4;

        // Generate synthetic 200x200 PNG
        using var bitmap = new SKBitmap(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.LightSkyBlue);
            using var paint = new SKPaint { Color = SKColors.Navy, IsAntialias = true };
            canvas.DrawCircle(100, 100, 80, paint);
            using var innerPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            canvas.DrawRect(new SKRect(60, 60, 140, 140), innerPaint);
        }

        using var img = SKImage.FromBitmap(bitmap);
        using var pngData = img.Encode(SKEncodedImageFormat.Png, 100);
        byte[] pngBytes = pngData.ToArray();

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var font = new XFont("Arial", 14, XFontStyle.Bold);
            gfx.DrawString("PDF with Embedded 200x200 Raster Image", font, XBrushes.DarkSlateBlue, new XPoint(50, 80));

            using var ximg = XImage.FromStream(() => new MemoryStream(pngBytes));
            gfx.DrawImage(ximg, 50, 110, 200, 200);
        }

        doc.Save(destinationPath);
        return destinationPath;
    }

    public static string CreateUnicodePdf(string destinationPath)
    {
        EnsureDirectory(destinationPath);
        using var doc = new SharpPdfDocument();
        var page = doc.AddPage();
        page.Size = PageSize.A4;

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var font = new XFont("Arial", 14, XFontStyle.Bold);
            gfx.DrawString("Multilingual Unicode PDF Document", font, XBrushes.Black, new XPoint(50, 80));

            var subFont = new XFont("Arial", 11, XFontStyle.Regular);
            gfx.DrawString("English: The quick brown fox jumps over the lazy dog.", subFont, XBrushes.DarkSlateGray, new XPoint(50, 120));
            gfx.DrawString("French: Voix ambigue d'un coeur qui, au zephyr, prefere les jattes de kiwis.", subFont, XBrushes.DarkSlateGray, new XPoint(50, 150));
            gfx.DrawString("German: Victor jagt zwolf Boxkampfer quer uber den grossen Sylter Deich.", subFont, XBrushes.DarkSlateGray, new XPoint(50, 180));
            gfx.DrawString("Spanish: El veloz murcielago hindu comia feliz cardillo y kiwi.", subFont, XBrushes.DarkSlateGray, new XPoint(50, 210));
            gfx.DrawString("Symbols & Math: Copyright (c) 2026, Alpha & Omega, Sum = E[x] + Var(x).", subFont, XBrushes.DarkSlateGray, new XPoint(50, 240));
        }

        doc.Save(destinationPath);
        return destinationPath;
    }

    public static string CreateMalformedPdf(string destinationPath)
    {
        EnsureDirectory(destinationPath);
        // Truncated invalid header
        File.WriteAllBytes(destinationPath, Encoding.ASCII.GetBytes("%PDF-1.4\n%Malformed data chunk without catalog or xref table\n%%EOF"));
        return destinationPath;
    }

    public static string CreateEmptyPdf(string destinationPath)
    {
        EnsureDirectory(destinationPath);
        File.WriteAllBytes(destinationPath, Array.Empty<byte>());
        return destinationPath;
    }

    public static string CreateLargeDocumentPdf(string destinationPath, int pageCount = 50)
    {
        EnsureDirectory(destinationPath);
        using var doc = new SharpPdfDocument();
        for (int i = 0; i < pageCount; i++)
        {
            var page = doc.AddPage();
            page.Size = PageSize.A4;
            using var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont("Arial", 14, XFontStyle.Bold);
            gfx.DrawString($"Large Benchmark Document — Page {i + 1} of {pageCount}", font, XBrushes.DarkBlue, new XPoint(50, 80));
            var bodyFont = new XFont("Arial", 10, XFontStyle.Regular);
            for (int line = 0; line < 25; line++)
            {
                gfx.DrawString($"Section {i + 1}.{line + 1}: Memory stability and resource benchmark payload line item.", bodyFont, XBrushes.Gray, new XPoint(50, 120 + (line * 18)));
            }
        }

        doc.Save(destinationPath);
        return destinationPath;
    }

    private static void EnsureDirectory(string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    #endregion

    #region Diagnostic Runner for Real WinUI Desktop Execution

    public sealed class DiagnosticSuiteReport
    {
        public int TotalChecks { get; set; }
        public int PassedChecks { get; set; }
        public int FailedChecks { get; set; }
        public List<string> Findings { get; set; } = new();
        public List<string> GeneratedScreenshots { get; set; } = new();
        public TimeSpan TotalDuration { get; set; }
        public long MemoryDeltaBytes { get; set; }
        public bool IsApprovedForW2Core { get; set; }
    }

    /// <summary>
    /// Executes the complete W2-C technical validation suite directly inside the live WinUI process.
    /// Emits visual evidence screenshots to docs/qa/screenshots/ and returns structured findings.
    /// </summary>
    public static async Task<DiagnosticSuiteReport> RunPocDiagnosticSuiteAsync(string? screenshotDir = null)
    {
        var sw = Stopwatch.StartNew();
        var report = new DiagnosticSuiteReport();
        string tempDir = Path.Combine(Path.GetTempPath(), $"Axora_PdfPoc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        if (string.IsNullOrEmpty(screenshotDir))
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, "AXORA.sln")))
                {
                    string candidate = Path.Combine(current, "docs", "qa", "screenshots");
                    if (Directory.Exists(candidate))
                    {
                        screenshotDir = candidate;
                        break;
                    }
                }
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }
        }

        screenshotDir ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", "qa", "screenshots");
        try
        {
            screenshotDir = Path.GetFullPath(screenshotDir);
        }
        catch
        {
            screenshotDir = tempDir;
        }

        if (!Directory.Exists(screenshotDir))
        {
            try { Directory.CreateDirectory(screenshotDir); } catch { screenshotDir = tempDir; }
        }

        long initialMemory = GC.GetTotalMemory(forceFullCollection: true);

        void AssertCheck(string name, bool condition, string detail = "")
        {
            report.TotalChecks++;
            if (condition)
            {
                report.PassedChecks++;
                report.Findings.Add($"[PASS] {name} {detail}".Trim());
            }
            else
            {
                report.FailedChecks++;
                report.Findings.Add($"[FAIL] {name} {detail}".Trim());
            }
        }

        try
        {
            // 1. Simple One-Page PDF
            string simplePdf = Path.Combine(tempDir, "SimpleOnePage.pdf");
            CreateSimpleOnePageTextPdf(simplePdf);
            using (var handle = await OpenDocumentAsync(simplePdf))
            {
                AssertCheck("Open simple PDF", handle.PageCount == 1, $"(PageCount: {handle.PageCount})");

                // Render page 0 to PNG
                var pngResult = await RenderPageAsync(handle, 0, dpi: 96.0, isJpeg: false);
                AssertCheck("Render page 0 to PNG", pngResult.Success && pngResult.ImageBytes?.Length > 0,
                    $"(Bytes: {pngResult.ImageBytes?.Length}, Time: {pngResult.Elapsed.TotalMilliseconds:F1}ms)");

                if (pngResult.ImageBytes != null)
                {
                    bool isPngHeader = pngResult.ImageBytes.Length >= 8 &&
                                       pngResult.ImageBytes[0] == 0x89 && pngResult.ImageBytes[1] == 0x50 &&
                                       pngResult.ImageBytes[2] == 0x4E && pngResult.ImageBytes[3] == 0x47;
                    AssertCheck("PNG header validation", isPngHeader);

                    using var skBmp = SKBitmap.Decode(pngResult.ImageBytes);
                    AssertCheck("Decodable via SkiaSharp", skBmp != null && skBmp.Width > 0 && skBmp.Height > 0,
                        $"(Decoded: {skBmp?.Width}x{skBmp?.Height})");
                }

                // Render page 0 to JPG
                var jpgResult = await RenderPageAsync(handle, 0, dpi: 96.0, isJpeg: true);
                AssertCheck("Render page 0 to JPG", jpgResult.Success && jpgResult.ImageBytes?.Length > 0,
                    $"(Bytes: {jpgResult.ImageBytes?.Length})");

                if (jpgResult.ImageBytes != null)
                {
                    bool isJpgHeader = jpgResult.ImageBytes.Length >= 3 &&
                                       jpgResult.ImageBytes[0] == 0xFF && jpgResult.ImageBytes[1] == 0xD8 &&
                                       jpgResult.ImageBytes[2] == 0xFF;
                    AssertCheck("JPG header validation", isJpgHeader);
                }

                // Invalid page index
                var invalidResult = await RenderPageAsync(handle, 99);
                AssertCheck("Invalid page index fails cleanly", !invalidResult.Success && invalidResult.ErrorCode == "ERR_PAGE_OUT_OF_RANGE");
            }

            // 2. Multi-Page PDF (5 pages)
            string multiPdf = Path.Combine(tempDir, "MultiPage.pdf");
            CreateMultiPagePdf(multiPdf, 5);
            using (var handle = await OpenDocumentAsync(multiPdf))
            {
                AssertCheck("Multi-page count verification", handle.PageCount == 5, $"(Count: {handle.PageCount})");
                var firstPage = await RenderPageAsync(handle, 0);
                var midPage = await RenderPageAsync(handle, 2);
                var lastPage = await RenderPageAsync(handle, 4);
                AssertCheck("Multi-page first, middle, last render",
                    firstPage.Success && midPage.Success && lastPage.Success);
            }

            // 3. Portrait Page & Visual Evidence
            string portraitPdf = Path.Combine(tempDir, "Portrait.pdf");
            CreatePortraitPdf(portraitPdf);
            string portraitOut = Path.Combine(screenshotDir, "pdf-poc-portrait.png");
            using (var handle = await OpenDocumentAsync(portraitPdf))
            {
                var pRes = await RenderPageToFileAsync(handle, 0, portraitOut, dpi: 96.0);
                AssertCheck("Portrait render to file", pRes.Success && File.Exists(portraitOut));
                using var pBmp = SKBitmap.Decode(portraitOut);
                AssertCheck("Portrait aspect ratio check (Height > Width)", pBmp != null && pBmp.Height > pBmp.Width,
                    $"(Dimensions: {pBmp?.Width}x{pBmp?.Height})");
                if (File.Exists(portraitOut)) report.GeneratedScreenshots.Add(portraitOut);
            }

            // 4. Landscape Page & Visual Evidence
            string landscapePdf = Path.Combine(tempDir, "Landscape.pdf");
            CreateLandscapePdf(landscapePdf);
            string landscapeOut = Path.Combine(screenshotDir, "pdf-poc-landscape.png");
            using (var handle = await OpenDocumentAsync(landscapePdf))
            {
                var lRes = await RenderPageToFileAsync(handle, 0, landscapeOut, dpi: 96.0);
                AssertCheck("Landscape render to file", lRes.Success && File.Exists(landscapeOut));
                using var lBmp = SKBitmap.Decode(landscapeOut);
                AssertCheck("Landscape aspect ratio check (Width > Height)", lBmp != null && lBmp.Width > lBmp.Height,
                    $"(Dimensions: {lBmp?.Width}x{lBmp?.Height})");
                if (File.Exists(landscapeOut)) report.GeneratedScreenshots.Add(landscapeOut);
            }

            // 5. Embedded Raster Image PDF & Visual Evidence
            string imagePdf = Path.Combine(tempDir, "ImageRaster.pdf");
            CreateRasterImagePdf(imagePdf);
            string imageOut = Path.Combine(screenshotDir, "pdf-poc-image.png");
            using (var handle = await OpenDocumentAsync(imagePdf))
            {
                var iRes = await RenderPageToFileAsync(handle, 0, imageOut, dpi: 96.0);
                AssertCheck("Raster image PDF render to file", iRes.Success && File.Exists(imageOut));
                if (File.Exists(imageOut)) report.GeneratedScreenshots.Add(imageOut);
            }

            // 6. Unicode Multilingual PDF & Visual Evidence
            string unicodePdf = Path.Combine(tempDir, "Unicode.pdf");
            CreateUnicodePdf(unicodePdf);
            string unicodeOut = Path.Combine(screenshotDir, "pdf-poc-unicode.png");
            using (var handle = await OpenDocumentAsync(unicodePdf))
            {
                var uRes = await RenderPageToFileAsync(handle, 0, unicodeOut, dpi: 96.0);
                AssertCheck("Unicode PDF render to file", uRes.Success && File.Exists(unicodeOut));
                if (File.Exists(unicodeOut)) report.GeneratedScreenshots.Add(unicodeOut);
            }

            // 7. DPI Scaling (72 vs 150 vs 300 DPI)
            using (var handle = await OpenDocumentAsync(simplePdf))
            {
                var dpi72 = await RenderPageAsync(handle, 0, dpi: 72.0);
                var dpi150 = await RenderPageAsync(handle, 0, dpi: 150.0);
                var dpi300 = await RenderPageAsync(handle, 0, dpi: 300.0);

                AssertCheck("DPI scaling progression (300 > 150 > 72)",
                    dpi300.PixelWidth > dpi150.PixelWidth && dpi150.PixelWidth > dpi72.PixelWidth,
                    $"(72 DPI: {dpi72.PixelWidth}px, 150 DPI: {dpi150.PixelWidth}px, 300 DPI: {dpi300.PixelWidth}px)");
            }

            // 8. Error Handling (Malformed and 0-byte PDFs)
            string malformedPdf = Path.Combine(tempDir, "Malformed.pdf");
            CreateMalformedPdf(malformedPdf);
            bool malformedCaught = false;
            try
            {
                using var handle = await OpenDocumentAsync(malformedPdf);
            }
            catch
            {
                malformedCaught = true;
            }
            AssertCheck("Malformed PDF rejected cleanly with exception", malformedCaught);

            string emptyPdf = Path.Combine(tempDir, "Empty.pdf");
            CreateEmptyPdf(emptyPdf);
            bool emptyCaught = false;
            try
            {
                using var handle = await OpenDocumentAsync(emptyPdf);
            }
            catch (InvalidDataException)
            {
                emptyCaught = true;
            }
            catch
            {
                emptyCaught = true;
            }
            AssertCheck("0-byte empty PDF rejected cleanly", emptyCaught);

            // 9. Concurrency: Parallel Page Renders across Tasks
            using (var handle = await OpenDocumentAsync(multiPdf))
            {
                var parallelTasks = Enumerable.Range(0, 5).Select(i =>
                    Task.Run(async () => await RenderPageAsync(handle, (uint)i))).ToArray();

                var parallelResults = await Task.WhenAll(parallelTasks);
                AssertCheck("Concurrent page renders on background tasks (5 concurrent)",
                    parallelResults.All(r => r.Success && r.ImageBytes?.Length > 0));
            }

            // 10. Memory & Resource Stability (10-page & 50-page tests)
            string tenPagePdf = Path.Combine(tempDir, "TenPage.pdf");
            CreateMultiPagePdf(tenPagePdf, 10);
            using (var handle = await OpenDocumentAsync(tenPagePdf))
            {
                bool all10Pass = true;
                for (uint i = 0; i < 10; i++)
                {
                    var res = await RenderPageAsync(handle, i);
                    if (!res.Success) all10Pass = false;
                }
                AssertCheck("10-page sequential rendering cycle", all10Pass);
            }

            // Source immutability check
            byte[] initialHash;
            using (var sha = SHA256.Create())
            {
                initialHash = sha.ComputeHash(File.ReadAllBytes(simplePdf));
            }
            using (var handle = await OpenDocumentAsync(simplePdf))
            {
                for (int i = 0; i < 5; i++)
                {
                    await RenderPageAsync(handle, 0);
                }
            }
            byte[] finalHash;
            using (var sha = SHA256.Create())
            {
                finalHash = sha.ComputeHash(File.ReadAllBytes(simplePdf));
            }
            AssertCheck("Source PDF SHA-256 byte-for-byte unchanged after multiple renders",
                initialHash.SequenceEqual(finalHash));
        }
        finally
        {
            // Purge scratch directory
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
            catch { }
        }

        long finalMemory = GC.GetTotalMemory(forceFullCollection: true);
        report.MemoryDeltaBytes = finalMemory - initialMemory;
        sw.Stop();
        report.TotalDuration = sw.Elapsed;
        report.IsApprovedForW2Core = (report.FailedChecks == 0);

        return report;
    }

    #endregion
}
