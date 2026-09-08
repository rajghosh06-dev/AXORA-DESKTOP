using System.Collections.Generic;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Contract for deterministic, page-bounded passage chunking.
/// Slices text into granular passages with exact character offsets without crossing page borders.
/// </summary>
public interface IPassageChunker
{
    /// <summary>
    /// Generates passage chunks strictly bounded within a single document page.
    /// </summary>
    IReadOnlyList<DocumentPassageChunk> ChunkPage(
        string documentId,
        int pageNumber,
        string pageText,
        ChunkingOptions? options = null);
}
