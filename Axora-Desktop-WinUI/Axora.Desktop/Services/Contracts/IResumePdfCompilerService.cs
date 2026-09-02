using System.IO;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Compiles ATS-compliant, vector-searchable, 1-page budgeted PDF documents from structured ResumeDocument models.
/// </summary>
public interface IResumePdfCompilerService
{
    /// <summary>
    /// Compiles the resume document into a standalone vector PDF file.
    /// </summary>
    Task CompileToPdfAsync(ResumeDocument document, string destinationFilePath);

    /// <summary>
    /// Compiles the resume document and returns the PDF bytes stream for in-memory preview or streaming.
    /// </summary>
    Task<byte[]> CompileToBytesAsync(ResumeDocument document);
}
