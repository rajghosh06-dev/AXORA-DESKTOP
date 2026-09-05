using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Cryptographic and binary integrity validator for external extensions and installers.
/// </summary>
public sealed class ExtensionValidator : IExtensionValidator
{
    private readonly IExtensionCacheService _cacheService;
    private readonly IVersionDetector _versionDetector;
    private readonly ILogger<ExtensionValidator>? _logger;

    public ExtensionValidator(
        IExtensionCacheService cacheService,
        IVersionDetector versionDetector,
        ILogger<ExtensionValidator>? logger = null)
    {
        _cacheService = cacheService;
        _versionDetector = versionDetector;
        _logger = logger;
    }

    public async Task<ExtensionValidationResult> ValidateExtensionAsync(
        ExtensionModel extension,
        CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            // 1. Validate Architecture
            if (!ValidateHostArchitecture(extension.SupportedArchitecture))
            {
                return ExtensionValidationResult.Failed(
                    $"Current system architecture does not support {extension.SupportedArchitecture}.",
                    isCorrupted: false,
                    requiresRepair: false);
            }

            // 2. Detect installation & executable
            var (isDetected, version, exePath) = await _versionDetector.DetectInstalledVersionAsync(extension, ct);
            if (!isDetected || string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                return ExtensionValidationResult.Failed(
                    $"Executable '{extension.ExecutableName}' not found on host.",
                    isCorrupted: false,
                    requiresRepair: false);
            }

            // 3. File length check (guard against 0-byte corrupt files)
            try
            {
                var fileInfo = new FileInfo(exePath);
                if (fileInfo.Length == 0)
                {
                    return ExtensionValidationResult.Failed(
                        $"Executable '{extension.ExecutableName}' is 0 bytes (corrupted).",
                        isCorrupted: true,
                        requiresRepair: true);
                }
            }
            catch (Exception ex)
            {
                return ExtensionValidationResult.Failed(
                    $"Unable to inspect executable file: {ex.Message}",
                    isCorrupted: true,
                    requiresRepair: true);
            }

            return ExtensionValidationResult.Success(exePath, version);
        }, ct);
    }

    public async Task<bool> ValidateInstallerAsync(
        string installerPath,
        string? expectedSha256,
        string supportedArch,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            if (!File.Exists(installerPath)) return false;

            // Architecture verification
            if (!ValidateHostArchitecture(supportedArch)) return false;

            var info = new FileInfo(installerPath);
            if (info.Length == 0) return false;

            // SHA256 checksum verification
            if (!string.IsNullOrWhiteSpace(expectedSha256))
            {
                try
                {
                    using var stream = File.OpenRead(installerPath);
                    using var sha256 = SHA256.Create();
                    var hashBytes = sha256.ComputeHash(stream);
                    var computedHash = Convert.ToHexString(hashBytes);

                    if (!computedHash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogWarning("Installer hash mismatch! Expected: {Expected}, Computed: {Computed}",
                            expectedSha256, computedHash);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error computing checksum for installer at {Path}", installerPath);
                    return false;
                }
            }

            return true;
        }, ct);
    }

    private static bool ValidateHostArchitecture(string supportedArch)
    {
        if (string.IsNullOrWhiteSpace(supportedArch) ||
            supportedArch.Equals("any", StringComparison.OrdinalIgnoreCase) ||
            supportedArch.Equals("neutral", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (supportedArch.Equals("x64", StringComparison.OrdinalIgnoreCase) ||
            supportedArch.Equals("win-x64", StringComparison.OrdinalIgnoreCase))
        {
            return Environment.Is64BitOperatingSystem;
        }

        if (supportedArch.Equals("arm64", StringComparison.OrdinalIgnoreCase) ||
            supportedArch.Equals("win-arm64", StringComparison.OrdinalIgnoreCase))
        {
            return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture ==
                   System.Runtime.InteropServices.Architecture.Arm64;
        }

        if (supportedArch.Equals("x86", StringComparison.OrdinalIgnoreCase) ||
            supportedArch.Equals("win-x86", StringComparison.OrdinalIgnoreCase))
        {
            return !Environment.Is64BitProcess;
        }

        return false;
    }
}