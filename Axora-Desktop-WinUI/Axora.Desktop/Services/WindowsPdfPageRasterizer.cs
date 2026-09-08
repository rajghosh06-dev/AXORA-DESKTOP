using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Native Windows physical page rasterizer for PDF documents.
/// Implements IPdfPageRasterizer using Windows.Data.Pdf to render high-fidelity PNG raster images
/// for on-device OCR recognition. Fully local, zero external network, deterministically bounded.
/// </summary>
public sealed class WindowsPdfPageRasterizer : IPdfPageRasterizer
{
    private const uint MaxAllowedPixelDimension = 16384; // 16K max boundary

    public bool CanRasterize => true;

    public async Task<Stream> RasterizePageToPngStreamAsync(
        Stream pdfStream,
        int pageIndex,
        double targetDpi = 300.0,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);
        if (pageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Page index must be non-negative (0-indexed).");
        }

        ct.ThrowIfCancellationRequested();

        // 1. Copy caller stream to in-memory random access stream without modifying caller stream
        byte[] pdfBytes;
        if (pdfStream is MemoryStream ms && ms.TryGetBuffer(out var seg))
        {
            pdfBytes = seg.ToArray();
        }
        else
        {
            using var mem = new MemoryStream();
            if (pdfStream.CanSeek)
            {
                long pos = pdfStream.Position;
                await pdfStream.CopyToAsync(mem, ct);
                pdfStream.Position = pos;
            }
            else
            {
                await pdfStream.CopyToAsync(mem, ct);
            }
            pdfBytes = mem.ToArray();
        }

        ct.ThrowIfCancellationRequested();

        var inStream = new InMemoryRandomAccessStream();
        try
        {
            using (var writer = new DataWriter(inStream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(pdfBytes);
                await writer.StoreAsync().AsTask(ct);
                await writer.FlushAsync().AsTask(ct);
                writer.DetachStream();
            }

            inStream.Seek(0);
            ct.ThrowIfCancellationRequested();

            var winDoc = await PdfDocument.LoadFromStreamAsync(inStream).AsTask(ct);

            if ((uint)pageIndex >= winDoc.PageCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pageIndex),
                    $"Page index {pageIndex} is out of bounds (document has {winDoc.PageCount} pages).");
            }

            ct.ThrowIfCancellationRequested();

            using var pdfPage = winDoc.GetPage((uint)pageIndex);
            using var outStream = new InMemoryRandomAccessStream();

            var renderOptions = new PdfPageRenderOptions
            {
                BitmapEncoderId = BitmapEncoder.PngEncoderId,
                BackgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255) // Solid opaque white
            };

            // Calculate scaled pixel dimensions based on targetDpi (page.Size is in 96-DPI DIPs)
            double effectiveDpi = Math.Clamp(targetDpi, 36.0, 600.0);
            double scale = effectiveDpi / 96.0;

            uint targetWidth = (uint)Math.Max(1, Math.Round(pdfPage.Size.Width * scale));
            uint targetHeight = (uint)Math.Max(1, Math.Round(pdfPage.Size.Height * scale));

            // Oversized image protection (cap at 16K)
            if (targetWidth > MaxAllowedPixelDimension || targetHeight > MaxAllowedPixelDimension)
            {
                double downscale = Math.Min(
                    (double)MaxAllowedPixelDimension / targetWidth,
                    (double)MaxAllowedPixelDimension / targetHeight);
                targetWidth = (uint)Math.Max(1, Math.Round(targetWidth * downscale));
                targetHeight = (uint)Math.Max(1, Math.Round(targetHeight * downscale));
            }

            renderOptions.DestinationWidth = targetWidth;
            renderOptions.DestinationHeight = targetHeight;

            await pdfPage.RenderToStreamAsync(outStream, renderOptions).AsTask(ct);

            // Copy outStream to returned MemoryStream and dispose WinRT resources cleanly
            outStream.Seek(0);
            var resultStream = new MemoryStream((int)outStream.Size);
            await outStream.AsStreamForRead().CopyToAsync(resultStream, ct);
            resultStream.Position = 0;

            return resultStream;
        }
        finally
        {
            inStream.Dispose();
        }
    }
}
