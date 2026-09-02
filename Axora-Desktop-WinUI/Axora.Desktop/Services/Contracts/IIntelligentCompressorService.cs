using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

public interface IIntelligentCompressorService
{
    Task CompressBatchAsync(
        IReadOnlyList<CompressionJob> jobs,
        CompressionProfile profile,
        string outputDirectory,
        IProgress<double>? progress = null,
        Action<CompressionJob>? onItemProcessed = null,
        CancellationToken ct = default);

    Task<long> CompressSingleFileAsync(
        string inputPath,
        string outputPath,
        CompressionProfile profile,
        CancellationToken ct = default);
}
