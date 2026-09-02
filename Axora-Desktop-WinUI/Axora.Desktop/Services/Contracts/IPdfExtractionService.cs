using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Service contract for PDF document text extraction and structural analysis.
/// 100% local, zero-cloud processing.
/// </summary>
public interface IPdfExtractionService
{
    /// <summary>
    /// Extracts text and page metadata from a PDF stream using PdfSharpCore.
    /// </summary>
    Task<string> ExtractPdfContentAsync(Stream pdfStream, CancellationToken ct = default);

    /// <summary>
    /// Extracts text and page metadata from a PDF file path.
    /// </summary>
    Task<string> ExtractPdfFromFileAsync(string filePath, CancellationToken ct = default);
}
