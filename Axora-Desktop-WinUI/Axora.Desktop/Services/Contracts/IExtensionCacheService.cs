using System.Threading;
using System.Threading.Tasks;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Controls isolated directories for downloads, installer cache, temporary files, and metadata.
/// Guaranteed not to delete or modify user documents, settings, or project files.
/// </summary>
public interface IExtensionCacheService
{
    string CacheRootDirectory { get; }
    string ExtensionsRootDirectory { get; }

    string GetExtensionCacheDirectory(string extensionId);
    string GetExtensionInstallerDirectory(string extensionId);
    string GetExtensionInstallDirectory(string extensionId);
    string GetExtensionTempDirectory(string extensionId);

    Task<long> GetCacheSizeBytesAsync(string? extensionId = null, CancellationToken ct = default);
    Task ClearCacheAsync(string? extensionId = null, CancellationToken ct = default);
    Task ClearTemporaryFilesAsync(string extensionId, CancellationToken ct = default);
}