using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Contract for rendering PDF pages into raster image streams for on-device OCR processing.
/// </summary>
public interface IPdfPageRasterizer
{
    /// <summary>
    /// Whether this rasterizer is operational on the current platform.
    /// </summary>
    bool CanRasterize { get; }

    /// <summary>
    /// Renders the specified PDF page into an image stream (PNG/JPEG).
    /// </summary>
    Task<Stream> RasterizePageToPngStreamAsync(
        Stream pdfStream,
        int pageIndex,
        double targetDpi = 300.0,
        CancellationToken ct = default);
}
