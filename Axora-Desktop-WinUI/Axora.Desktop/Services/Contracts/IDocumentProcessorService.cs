using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Axora.Desktop.Services.Contracts;

public enum CompressionLevel
{
    Low,     // Max Quality (metadata stripping, 95% image quality)
    Medium,  // Balanced (150 DPI, 75% quality)
    High     // Max Compression (72 DPI, 50% quality, compact XML)
}

public interface IDocumentProcessorService
{
    Task<string> ConvertTextToPdfAsync(string text, string outputPath, CancellationToken ct = default);
    Task<string> PackageImagesToPdfAsync(string[] imagePaths, string outputPath, CancellationToken ct = default);
    Task<long> CompressPdfAsync(string inputPath, string outputPath, CompressionLevel level, CancellationToken ct = default);
}
