using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Executes extension installation, portable extraction, deployment, and removal.
/// Enforces safe staging, automatic rollback on validation failure, and process lock defense (no arbitrary process termination).
/// </summary>
public sealed class ExtensionInstaller : IExtensionInstaller
{
    private readonly IExtensionCacheService _cacheService;
    private readonly IExtensionValidator _validator;
    private readonly IVersionDetector _versionDetector;
    private readonly ILogger<ExtensionInstaller>? _logger;

    public ExtensionInstaller(
        IExtensionCacheService cacheService,
        IExtensionValidator validator,
        IVersionDetector versionDetector,
        ILogger<ExtensionInstaller>? logger = null)
    {
        _cacheService = cacheService;
        _validator = validator;
        _versionDetector = versionDetector;
        _logger = logger;
    }

    public async Task<bool> InstallAsync(
        ExtensionModel extension,
        string stagedInstallerPath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(extension);

        if (string.IsNullOrWhiteSpace(stagedInstallerPath) || !File.Exists(stagedInstallerPath))
        {
            extension.ErrorMessage = "Staged installer file not found.";
            extension.Status = ExtensionStatus.Failed;
            return false;
        }

        var installDir = _cacheService.GetExtensionInstallDirectory(extension.Id);

        // Process Lock Defense: ensure current installation is not in active use before replacement
        if (Directory.Exists(installDir) && IsProcessInUse(extension, installDir, out var inUseMessage))
        {
            _logger?.LogWarning("Cannot install or update {ExtensionId}: process is running.", extension.Id);
            extension.ErrorMessage = inUseMessage;
            return false;
        }

        var tempDir = _cacheService.GetExtensionTempDirectory(extension.Id);
        var stagingDir = Path.Combine(tempDir, $"stage_{Guid.NewGuid():N}");
        var backupDir = Path.Combine(tempDir, $"backup_{Guid.NewGuid():N}");

        try
        {
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(stagingDir);

            var ext = Path.GetExtension(stagedInstallerPath).ToLowerInvariant();
            if (ext == ".zip")
            {
                // Portable ZIP extraction into staging directory
                await Task.Run(() => ZipFile.ExtractToDirectory(stagedInstallerPath, stagingDir, overwriteFiles: true), ct);
            }
            else if (ext == ".exe")
            {
                var targetExe = Path.Combine(stagingDir, extension.ExecutableName);

                // If staged file is the executable itself, copy directly into staging
                if (Path.GetFileName(stagedInstallerPath).Equals(extension.ExecutableName, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(stagedInstallerPath, targetExe, overwrite: true);
                }
                else
                {
                    // Execute installer targeting the staging directory
                    await Task.Run(() =>
                    {
                        try
                        {
                            using var proc = new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = stagedInstallerPath,
                                    Arguments = $"/VERYSILENT /NORESTART /DIR=\"{stagingDir}\"",
                                    CreateNoWindow = true,
                                    UseShellExecute = false
                                }
                            };
                            proc.Start();
                            if (!proc.WaitForExit(60000))
                            {
                                try { proc.Kill(entireProcessTree: true); } catch { }
                                throw new TimeoutException("Installer process timed out after 60 seconds.");
                            }

                            if (proc.ExitCode != 0 && !File.Exists(targetExe))
                            {
                                File.Copy(stagedInstallerPath, targetExe, overwrite: true);
                            }
                        }
                        catch (Win32Exception win32Ex) when (win32Ex.NativeErrorCode == 740)
                        {
                            throw new UnauthorizedAccessException(
                                $"{extension.DisplayName} requires administrator privileges to execute installer. Please run Axora as Administrator or use a portable user-mode distribution.", win32Ex);
                        }
                    }, ct);
                }
            }
            else
            {
                var targetFile = Path.Combine(stagingDir, Path.GetFileName(stagedInstallerPath));
                File.Copy(stagedInstallerPath, targetFile, overwrite: true);
            }

            // Staged verification: verify the executable was placed
            var stagedExe = Path.Combine(stagingDir, extension.ExecutableName);
            if (!File.Exists(stagedExe))
            {
                throw new InvalidOperationException($"Staged installation did not produce expected executable '{extension.ExecutableName}'.");
            }

            // Directory Swap with Rollback Guarantee:
            // 1. Move current installDir to backupDir if present
            if (Directory.Exists(installDir))
            {
                Directory.Move(installDir, backupDir);
            }

            // 2. Move stagingDir to installDir
            try
            {
                Directory.Move(stagingDir, installDir);
            }
            catch (Exception moveEx)
            {
                _logger?.LogError(moveEx, "Failed to move staging directory to {InstallDir}. Initiating rollback.", installDir);
                if (Directory.Exists(backupDir) && !Directory.Exists(installDir))
                {
                    Directory.Move(backupDir, installDir);
                }
                throw;
            }

            // 3. Post-install validation on swapped install directory
            var valResult = await _validator.ValidateExtensionAsync(extension, ct);
            if (!valResult.IsValid)
            {
                _logger?.LogWarning("Post-install verification failed for {ExtensionId}. Rolling back.", extension.Id);
                if (Directory.Exists(installDir))
                {
                    Directory.Delete(installDir, recursive: true);
                }
                if (Directory.Exists(backupDir))
                {
                    Directory.Move(backupDir, installDir);
                }

                extension.ErrorMessage = $"Post-install verification failed: {valResult.ErrorMessage}";
                extension.Status = valResult.RequiresRepair ? ExtensionStatus.RepairRequired : ExtensionStatus.Failed;
                return false;
            }

            // 4. Installation validated successfully; clean up backup
            if (Directory.Exists(backupDir))
            {
                try { Directory.Delete(backupDir, recursive: true); } catch { }
            }

            // Read detected version
            var (_, version, _) = await _versionDetector.DetectInstalledVersionAsync(extension, ct);
            extension.InstalledVersion = version ?? extension.LatestVersion ?? extension.TargetVersion;
            extension.Status = ExtensionStatus.Installed;
            extension.ErrorMessage = null;
            extension.StatusMessage = $"Installed {extension.DisplayName} v{extension.InstalledVersion} successfully.";
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Installation failed for {ExtensionId}", extension.Id);

            // Cleanup staging
            if (Directory.Exists(stagingDir))
            {
                try { Directory.Delete(stagingDir, recursive: true); } catch { }
            }

            // Rollback backup if installDir missing
            if (Directory.Exists(backupDir) && !Directory.Exists(installDir))
            {
                try { Directory.Move(backupDir, installDir); } catch { }
            }

            extension.ErrorMessage = ex.Message;
            extension.Status = ExtensionStatus.Failed;
            return false;
        }
        finally
        {
            if (Directory.Exists(backupDir) && Directory.Exists(installDir))
            {
                try { Directory.Delete(backupDir, recursive: true); } catch { }
            }
        }
    }

    public async Task<bool> UninstallAsync(ExtensionModel extension, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(extension);

        return await Task.Run(() =>
        {
            try
            {
                var installDir = _cacheService.GetExtensionInstallDirectory(extension.Id);
                if (Directory.Exists(installDir))
                {
                    // Check if running from installDir without terminating unrelated user processes
                    if (IsProcessInUse(extension, installDir, out var inUseMessage))
                    {
                        _logger?.LogWarning("Cannot uninstall {ExtensionId}: process is running.", extension.Id);
                        extension.ErrorMessage = inUseMessage;
                        return false;
                    }

                    Directory.Delete(installDir, recursive: true);
                }

                extension.InstalledVersion = null;
                extension.Status = ExtensionStatus.NotInstalled;
                extension.ErrorMessage = null;
                extension.StatusMessage = $"Uninstalled {extension.DisplayName}.";
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to uninstall {ExtensionId}", extension.Id);
                extension.ErrorMessage = $"Failed to uninstall: {ex.Message}";
                return false;
            }
        }, ct);
    }

    private bool IsProcessInUse(ExtensionModel extension, string installDir, out string message)
    {
        message = string.Empty;
        var exeNameWithoutExt = Path.GetFileNameWithoutExtension(extension.ExecutableName);

        try
        {
            var processes = Process.GetProcessesByName(exeNameWithoutExt);
            foreach (var p in processes)
            {
                try
                {
                    if (p.MainModule?.FileName != null &&
                        p.MainModule.FileName.StartsWith(installDir, StringComparison.OrdinalIgnoreCase))
                    {
                        message = $"{extension.DisplayName} is currently in use. Please close active operations before proceeding.";
                        return true;
                    }
                }
                catch
                {
                    // In case permission limits reading process module, check direct file lock below
                }
            }
        }
        catch { }

        // Secondary check: verify if the target executable file itself is locked
        var targetExe = Path.Combine(installDir, extension.ExecutableName);
        if (File.Exists(targetExe) && IsFileLocked(targetExe))
        {
            message = $"{extension.DisplayName} is currently in use. Please close active operations before proceeding.";
            return true;
        }

        return false;
    }

    private static bool IsFileLocked(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }
}
