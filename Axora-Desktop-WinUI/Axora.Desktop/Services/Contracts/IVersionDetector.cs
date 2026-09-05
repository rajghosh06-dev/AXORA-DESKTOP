using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Probes host environments and managed paths to detect installed versions and compare against latest releases.
/// </summary>
public interface IVersionDetector
{
    Task<(bool isDetected, string? version, string? executablePath)> DetectInstalledVersionAsync(
        ExtensionModel extension,
        CancellationToken ct = default);

    Task<string?> FetchLatestVersionAsync(
        ExtensionModel extension,
        CancellationToken ct = default);

    int CompareVersions(string? versionA, string? versionB);
}