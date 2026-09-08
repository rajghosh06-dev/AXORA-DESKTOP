using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Service contract for detecting document formats and encodings via magic bytes and content sniffing.
/// </summary>
public interface IDocumentFormatDetector
{
    /// <summary>
    /// Sniffs the format of a document from its content stream without seeking past required headers.
    /// </summary>
    FormatDetectionResult DetectFormat(Stream stream, string? fileNameHint = null);

    /// <summary>
    /// Sniffs the format of a document file asynchronously from a file path.
    /// </summary>
    Task<FormatDetectionResult> DetectFormatAsync(string filePath, CancellationToken ct = default);
}
