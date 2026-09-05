using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Probes local systems, managed storage, and executables to detect installed versions and compare releases.
/// </summary>
public sealed class VersionDetector : IVersionDetector
{
    private readonly IExtensionCacheService _cacheService;
    private readonly ILogger<VersionDetector>? _logger;

    private static readonly Regex VersionRegex = new(
        @"(?<version>\d+(?:\.\d+)+(?:[-_]\d+)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public VersionDetector(IExtensionCacheService cacheService, ILogger<VersionDetector>? logger = null)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<(bool isDetected, string? version, string? executablePath)> DetectInstalledVersionAsync(
        ExtensionModel extension,
        CancellationToken ct = default)
    {
        return await Task.Run<(bool isDetected, string? version, string? executablePath)>(() =>
        {
            try
            {
                // 1. Check Axora managed installation directory
                var managedDir = _cacheService.GetExtensionInstallDirectory(extension.Id);
                var managedExe = Path.Combine(managedDir, extension.ExecutableName);
                if (File.Exists(managedExe))
                {
                    var ver = ProbeExecutableVersion(managedExe);
                    return (true, ver, managedExe);
                }

                // 2. Check System PATH
                var pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                    {
                        try
                        {
                            var candidate = Path.Combine(dir.Trim(), extension.ExecutableName);
                            if (File.Exists(candidate))
                            {
                                var ver = ProbeExecutableVersion(candidate);
                                return (true, ver, candidate);
                            }
                        }
                        catch { }
                    }
                }

                // 3. Known vendor installation paths (e.g. Program Files for ImageMagick)
                if (extension.Id.Equals("imagemagick", StringComparison.OrdinalIgnoreCase))
                {
                    var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    if (Directory.Exists(progFiles))
                    {
                        try
                        {
                            foreach (var d in Directory.EnumerateDirectories(progFiles, "ImageMagick*"))
                            {
                                var cand = Path.Combine(d, extension.ExecutableName);
                                if (File.Exists(cand))
                                {
                                    var ver = ProbeExecutableVersion(cand);
                                    return (true, ver, cand);
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to probe installed version for {ExtensionId}.", extension.Id);
            }

            return (false, null, null);
        }, ct);
    }

    private static readonly System.Net.Http.HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task<string?> FetchLatestVersionAsync(ExtensionModel extension, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(extension);

        // Architecture is ready for an authoritative remote metadata provider / manifest endpoint.
        // Currently, no authoritative remote vendor metadata provider or API feed exists for ImageMagick
        // (only the fixed binary installer URL in InstallSource).
        // To avoid fragile network dependencies and prevent false claims of dynamic update discovery,
        // we return null when no authoritative provider endpoint is configured, safely marking
        // UpdateCheckStatus as UnableToCheck without disrupting healthy installed status.
        return await Task.FromResult<string?>(null);
    }

    public int CompareVersions(string? versionA, string? versionB)
    {
        if (string.IsNullOrWhiteSpace(versionA) && string.IsNullOrWhiteSpace(versionB)) return 0;
        if (string.IsNullOrWhiteSpace(versionA)) return -1;
        if (string.IsNullOrWhiteSpace(versionB)) return 1;

        var cleanA = CleanVersion(versionA);
        var cleanB = CleanVersion(versionB);

        var segsA = cleanA.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        var segsB = cleanB.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

        int maxLen = Math.Max(segsA.Length, segsB.Length);
        for (int i = 0; i < maxLen; i++)
        {
            var pA = i < segsA.Length ? segsA[i] : "0";
            var pB = i < segsB.Length ? segsB[i] : "0";

            if (long.TryParse(pA, out var numA) && long.TryParse(pB, out var numB))
            {
                int cmp = numA.CompareTo(numB);
                if (cmp != 0) return cmp;
            }
            else
            {
                int cmp = string.Compare(pA, pB, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
            }
        }

        return 0;
    }

    private static string CleanVersion(string v)
    {
        var match = VersionRegex.Match(v);
        return match.Success ? match.Groups["version"].Value : v.TrimStart('v', 'V').Trim();
    }

    private string? ProbeExecutableVersion(string exePath)
    {
        try
        {
            // Try process execution with -version
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "-version",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(1500);

            if (!string.IsNullOrWhiteSpace(output))
            {
                var match = VersionRegex.Match(output);
                if (match.Success)
                {
                    return match.Groups["version"].Value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Could not run {ExePath} with -version; falling back to FileVersionInfo.", exePath);
        }

        try
        {
            // Fallback to FileVersionInfo
            var info = FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrWhiteSpace(info.ProductVersion))
            {
                var match = VersionRegex.Match(info.ProductVersion);
                if (match.Success) return match.Groups["version"].Value;
                return info.ProductVersion.Trim();
            }
            if (!string.IsNullOrWhiteSpace(info.FileVersion))
            {
                var match = VersionRegex.Match(info.FileVersion);
                if (match.Success) return match.Groups["version"].Value;
                return info.FileVersion.Trim();
            }
        }
        catch { }

        return "1.0.0";
    }
}