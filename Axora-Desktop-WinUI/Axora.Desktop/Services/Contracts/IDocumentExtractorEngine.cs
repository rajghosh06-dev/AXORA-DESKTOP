using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Strategy interface implemented by format-specific document extraction engines.
/// </summary>
public interface IDocumentExtractorEngine
{
    /// <summary>
    /// Unique identifier of this extractor engine.
    /// </summary>
    string EngineIdentifier { get; }

    /// <summary>
    /// Determines whether this engine can extract the specified detected format.
    /// </summary>
    bool CanExtract(DetectedDocumentFormat format);

    /// <summary>
    /// Extracts raw pages, metadata, and diagnostic metrics from the document stream.
    /// Original stream is read-only and never mutated.
    /// </summary>
    Task<RawExtractionResult> ExtractAsync(
        Stream documentStream,
        ExtractionOptions options,
        IProgress<double>? progress = null,
        CancellationToken ct = default);
}

/// <summary>
/// Convenience extension methods for IDocumentExtractorEngine instances.
/// </summary>
public static class DocumentExtractorEngineExtensions
{
    /// <summary>
    /// Opens the specified file with read-only shared access and executes extraction.
    /// Guarantees that the underlying source file is never modified.
    /// </summary>
    public static async Task<RawExtractionResult> ExtractFromFileAsync(
        this IDocumentExtractorEngine engine,
        string filePath,
        ExtractionOptions? options = null,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        options ??= new ExtractionOptions();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
        return await engine.ExtractAsync(stream, options, progress, ct);
    }
}
