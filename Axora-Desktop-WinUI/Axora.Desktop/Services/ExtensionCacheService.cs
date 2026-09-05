using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Manages isolated storage and caching for external extensions.
/// Guarantees that user documents, settings, and unrelated data are never modified.
/// </summary>
public sealed class ExtensionCacheService : IExtensionCacheService
{
    private readonly string _cacheRoot;
    private readonly string _extensionsRoot;
    private readonly ILogger<ExtensionCacheService>? _logger;

    public string CacheRootDirectory => _cacheRoot;
    public string ExtensionsRootDirectory => _extensionsRoot;

    public ExtensionCacheService(
        ILogger<ExtensionCacheService>? logger = null,
        string? customCacheRoot = null,
        string? customExtensionsRoot = null)
    {
        _logger = logger;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        }

        _cacheRoot = !string.IsNullOrWhiteSpace(customCacheRoot)
            ? customCacheRoot
            : Path.Combine(localAppData, "Axora", "ExtensionCache");

        _extensionsRoot = !string.IsNullOrWhiteSpace(customExtensionsRoot)
            ? customExtensionsRoot
            : Path.Combine(localAppData, "Axora", "Extensions");

        EnsureDirectoryStructure();
    }

    private void EnsureDirectoryStructure()
    {
        try
        {
            Directory.CreateDirectory(_cacheRoot);
            Directory.CreateDirectory(Path.Combine(_cacheRoot, "downloads"));
            Directory.CreateDirectory(Path.Combine(_cacheRoot, "installers"));
            Directory.CreateDirectory(Path.Combine(_cacheRoot, "temp"));
            Directory.CreateDirectory(Path.Combine(_cacheRoot, "metadata"));
            Directory.CreateDirectory(_extensionsRoot);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to create directory structure for extension cache.");
        }
    }

    public string GetExtensionCacheDirectory(string extensionId)
    {
        var sanitized = SanitizeId(extensionId);
        var dir = Path.Combine(_cacheRoot, "downloads", sanitized);
        ValidatePathWithinRoot(dir, _cacheRoot);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string GetExtensionInstallerDirectory(string extensionId)
    {
        var sanitized = SanitizeId(extensionId);
        var dir = Path.Combine(_cacheRoot, "installers", sanitized);
        ValidatePathWithinRoot(dir, _cacheRoot);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string GetExtensionInstallDirectory(string extensionId)
    {
        var sanitized = SanitizeId(extensionId);
        var dir = Path.Combine(_extensionsRoot, sanitized);
        ValidatePathWithinRoot(dir, _extensionsRoot);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string GetExtensionTempDirectory(string extensionId)
    {
        var sanitized = SanitizeId(extensionId);
        var dir = Path.Combine(_cacheRoot, "temp", sanitized);
        ValidatePathWithinRoot(dir, _cacheRoot);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public async Task<long> GetCacheSizeBytesAsync(string? extensionId = null, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            long total = 0;
            try
            {
                if (!Directory.Exists(_cacheRoot)) return 0L;

                string[] searchDirs;
                if (!string.IsNullOrWhiteSpace(extensionId))
                {
                    var id = SanitizeId(extensionId);
                    searchDirs = [
                        Path.Combine(_cacheRoot, "downloads", id),
                        Path.Combine(_cacheRoot, "installers", id),
                        Path.Combine(_cacheRoot, "temp", id),
                        Path.Combine(_cacheRoot, "metadata", id)
                    ];
                }
                else
                {
                    searchDirs = [_cacheRoot];
                }

                foreach (var dir in searchDirs)
                {
                    if (Directory.Exists(dir))
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                        {
                            ct.ThrowIfCancellationRequested();
                            try
                            {
                                total += file.Length;
                            }
                            catch (Exception) { /* file locked or inaccessible */ }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "Failed to compute cache size.");
            }

            return total;
        }, ct);
    }

    public async Task ClearCacheAsync(string? extensionId = null, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(_cacheRoot)) return;

                if (!string.IsNullOrWhiteSpace(extensionId))
                {
                    var id = SanitizeId(extensionId);
                    string[] targetDirs = [
                        Path.Combine(_cacheRoot, "downloads", id),
                        Path.Combine(_cacheRoot, "installers", id),
                        Path.Combine(_cacheRoot, "temp", id),
                        Path.Combine(_cacheRoot, "metadata", id)
                    ];

                    foreach (var d in targetDirs)
                    {
                        ValidatePathWithinRoot(d, _cacheRoot);
                        if (Directory.Exists(d))
                        {
                            ct.ThrowIfCancellationRequested();
                            var dirInfo = new DirectoryInfo(d);
                            if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                            {
                                dirInfo.Delete();
                            }
                            else
                            {
                                Directory.Delete(d, recursive: true);
                            }
                        }
                    }
                }
                else
                {
                    // Clear all contents within cacheRoot subfolders without deleting cacheRoot itself
                    string[] subCategories = ["downloads", "installers", "temp", "metadata"];
                    foreach (var cat in subCategories)
                    {
                        var catPath = Path.Combine(_cacheRoot, cat);
                        if (Directory.Exists(catPath))
                        {
                            foreach (var dir in Directory.EnumerateDirectories(catPath))
                            {
                                ct.ThrowIfCancellationRequested();
                                try
                                {
                                    var dirInfo = new DirectoryInfo(dir);
                                    if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                                    {
                                        dirInfo.Delete();
                                    }
                                    else
                                    {
                                        Directory.Delete(dir, recursive: true);
                                    }
                                }
                                catch { }
                            }
                            foreach (var file in Directory.EnumerateFiles(catPath))
                            {
                                ct.ThrowIfCancellationRequested();
                                try { File.Delete(file); } catch { }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "Failed to clear extension cache.");
            }
        }, ct);
    }

    public async Task ClearTemporaryFilesAsync(string extensionId, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            try
            {
                var id = SanitizeId(extensionId);
                var tempDir = Path.Combine(_cacheRoot, "temp", id);
                ValidatePathWithinRoot(tempDir, _cacheRoot);
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to clear temporary files for {ExtensionId}.", extensionId);
            }
        }, ct);
    }

    private static string SanitizeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Extension ID cannot be null or empty.", nameof(id));

        // Strictly permit only alphanumeric characters, underscores, and hyphens.
        // Strips any directory traversal characters (e.g. .., /, \) completely.
        var cleaned = System.Text.RegularExpressions.Regex.Replace(id, @"[^a-zA-Z0-9_\-]", "_").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(cleaned) || cleaned.All(c => c == '_'))
            throw new ArgumentException($"Invalid extension ID '{id}'.", nameof(id));

        return cleaned;
    }

    private static string ValidatePathWithinRoot(string targetPath, string rootDirectory)
    {
        var fullTarget = Path.GetFullPath(targetPath);
        var fullRoot = Path.GetFullPath(rootDirectory);

        if (!fullTarget.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Security violation: Path traversal detected. Target '{fullTarget}' is outside root '{fullRoot}'.");
        }

        return fullTarget;
    }
}