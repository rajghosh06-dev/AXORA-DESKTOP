using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// High-level orchestration facade coordinating format detection, engine dispatch, page building,
/// normalization, chunking, and persistence integration.
/// </summary>
public interface IScholarExtractionOrchestrator
{
    /// <summary>
    /// Ingests and processes a document stream end-to-end.
    /// </summary>
    Task<ScholarExtractionResult> IngestAndProcessAsync(
        Stream sourceStream,
        string fileName,
        ExtractionOptions options,
        IProgress<double>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Ingests and processes a local document file end-to-end. Source file is strictly read-only.
    /// </summary>
    Task<ScholarExtractionResult> IngestAndProcessFileAsync(
        string sourceFilePath,
        ExtractionOptions options,
        IProgress<double>? progress = null,
        CancellationToken ct = default);
}
