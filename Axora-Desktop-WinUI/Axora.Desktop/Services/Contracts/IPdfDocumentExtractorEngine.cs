using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Specialized extractor engine contract for PDF documents.
/// Decouples PDF stream decoding, font CMap handling, and raster rendering from specific vendor packages.
/// </summary>
public interface IPdfDocumentExtractorEngine : IDocumentExtractorEngine
{
    /// <summary>
    /// Queries the capabilities and runtime limitations of the active PDF extraction implementation.
    /// </summary>
    Task<PdfExtractionCapabilities> GetEngineCapabilitiesAsync(CancellationToken ct = default);
}
