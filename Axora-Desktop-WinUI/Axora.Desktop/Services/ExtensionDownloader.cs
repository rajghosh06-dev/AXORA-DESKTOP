using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Secure HTTPS extension installer downloader with domain verification, staging, and checksum validation.
/// </summary>
public sealed class ExtensionDownloader : IExtensionDownloader
{
    private readonly IExtensionCacheService _cacheService;
    private readonly ILogger<ExtensionDownloader>? _logger;
    private readonly HttpClient _httpClient;

    private static readonly string[] AllowedDomains =
    [
        "imagemagick.org",
        "download.imagemagick.org",
        "github.com",
        "raw.githubusercontent.com",
        "objects.githubusercontent.com",
        "microsoft.com"
    ];

    public ExtensionDownloader(
        IExtensionCacheService cacheService,
        HttpClient? httpClient = null,
        ILogger<ExtensionDownloader>? logger = null)
    {
        _cacheService = cacheService;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _logger = logger;
    }

    public async Task<string> DownloadExtensionAsync(
        ExtensionModel extension,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(extension);

        // 1. Verify Source Security
        VerifySourceSecurity(extension.InstallSource);

        var downloadDir = _cacheService.GetExtensionCacheDirectory(extension.Id);
        var installerDir = _cacheService.GetExtensionInstallerDirectory(extension.Id);

        var fileName = Path.GetFileName(new Uri(extension.InstallSource).LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"{extension.Id}-installer.exe";
        }

        var tempPath = Path.Combine(downloadDir, $"{fileName}.tmp");
        var finalStagedPath = Path.Combine(installerDir, fileName);

        if (File.Exists(tempPath))
        {
            try { File.Delete(tempPath); } catch { }
        }

        // 2. Download with Streaming & Progress
        if (extension.InstallSource.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            // Local file copy for testing / offline installation
            var localSource = new Uri(extension.InstallSource).LocalPath;
            if (!File.Exists(localSource))
                throw new FileNotFoundException($"Local installation package not found at '{localSource}'.");

            File.Copy(localSource, tempPath, overwrite: true);
            progress?.Report(1.0);
        }
        else
        {
            using var response = await _httpClient.GetAsync(
                extension.InstallSource,
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;

                if (totalBytes > 0 && progress != null)
                {
                    var percent = (double)totalRead / totalBytes;
                    progress.Report(Math.Clamp(percent, 0.0, 1.0));
                }
            }
        }

        // 3. Cryptographic Verification
        if (!string.IsNullOrWhiteSpace(extension.Sha256Hash))
        {
            using (var stream = File.OpenRead(tempPath))
            using (var sha256 = SHA256.Create())
            {
                var hash = Convert.ToHexString(sha256.ComputeHash(stream));
                if (!hash.Equals(extension.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(tempPath); } catch { }
                    throw new InvalidOperationException(
                        $"Downloaded installer checksum mismatch for '{extension.DisplayName}'. " +
                        $"Expected: {extension.Sha256Hash}, Computed: {hash}");
                }
            }
        }

        // 4. Move to Staged Installer Directory
        if (File.Exists(finalStagedPath))
        {
            try { File.Delete(finalStagedPath); } catch { }
        }
        File.Move(tempPath, finalStagedPath);

        extension.StagedInstallerPath = finalStagedPath;
        extension.CacheDirectory = installerDir;

        return finalStagedPath;
    }

    private static void VerifySourceSecurity(string sourceUriString)
    {
        if (!Uri.TryCreate(sourceUriString, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Invalid extension installation source URI: '{sourceUriString}'");
        }

        if (uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
        {
            return; // Allow local file URI for tests and local deployments
        }

        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Extension downloads must use secure HTTPS. Insecure scheme '{uri.Scheme}' is rejected.");
        }

        bool isAllowed = false;
        foreach (var allowed in AllowedDomains)
        {
            if (uri.Host.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase))
            {
                isAllowed = true;
                break;
            }
        }

        if (!isAllowed)
        {
            throw new InvalidOperationException($"Host '{uri.Host}' is not in the trusted extension vendor whitelist.");
        }
    }
}