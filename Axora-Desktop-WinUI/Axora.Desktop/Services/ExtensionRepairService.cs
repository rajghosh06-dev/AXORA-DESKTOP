using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Implements three distinct maintenance workflows: Repair, Reinstall, and Clean Reinstall.
/// Strictly enforces safety boundaries: user documents, projects, and settings are never removed.
/// </summary>
public sealed class ExtensionRepairService : IExtensionRepairService
{
    private readonly IExtensionCacheService _cacheService;
    private readonly IExtensionValidator _validator;
    private readonly IExtensionDownloader _downloader;
    private readonly IExtensionInstaller _installer;
    private readonly IVersionDetector _versionDetector;
    private readonly ILogger<ExtensionRepairService>? _logger;

    public ExtensionRepairService(
        IExtensionCacheService cacheService,
        IExtensionValidator validator,
        IExtensionDownloader downloader,
        IExtensionInstaller installer,
        IVersionDetector versionDetector,
        ILogger<ExtensionRepairService>? logger = null)
    {
        _cacheService = cacheService;
        _validator = validator;
        _downloader = downloader;
        _installer = installer;
        _versionDetector = versionDetector;
        _logger = logger;
    }

    /// <summary>
    /// REPAIR: Preserves user configuration, revalidates binaries, and restores missing/corrupted files.
    /// </summary>
    public async Task<bool> RepairAsync(ExtensionModel extension, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(extension);

        _logger?.LogInformation("Starting Repair operation for {ExtensionId}", extension.Id);
        extension.IsBusy = true;
        extension.StatusMessage = "Diagnosing extension files for repair…";

        try
        {
            var valResult = await _validator.ValidateExtensionAsync(extension, ct);
            if (valResult.IsValid)
            {
                extension.Status = ExtensionStatus.Installed;
                extension.StatusMessage = $"{extension.DisplayName} is verified and healthy.";
                return true;
            }

            // Ensure staged installer is available
            string installerPath = extension.StagedInstallerPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath))
            {
                extension.StatusMessage = "Downloading healthy installation package for repair…";
                var progress = new Progress<double>(p => extension.DownloadProgress = p * 100.0);
                installerPath = await _downloader.DownloadExtensionAsync(extension, progress, ct);
            }

            extension.StatusMessage = "Restoring extension binaries…";
            var success = await _installer.InstallAsync(extension, installerPath, ct);
            if (success)
            {
                extension.StatusMessage = $"Repair completed: {extension.DisplayName} restored.";
            }
            return success;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Repair failed for {ExtensionId}", extension.Id);
            extension.Status = ExtensionStatus.RepairRequired;
            extension.ErrorMessage = $"Repair failed: {ex.Message}";
            return false;
        }
        finally
        {
            extension.IsBusy = false;
            extension.NotifyActionsChanged();
        }
    }

    /// <summary>
    /// REINSTALL: Uninstalls the extension, then installs the approved version again.
    /// </summary>
    public async Task<bool> ReinstallAsync(ExtensionModel extension, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(extension);

        _logger?.LogInformation("Starting Reinstall operation for {ExtensionId}", extension.Id);
        extension.IsBusy = true;
        extension.StatusMessage = "Uninstalling existing version…";

        try
        {
            await _installer.UninstallAsync(extension, ct);

            string installerPath = extension.StagedInstallerPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath))
            {
                extension.StatusMessage = "Downloading fresh installer…";
                var progress = new Progress<double>(p => extension.DownloadProgress = p * 100.0);
                installerPath = await _downloader.DownloadExtensionAsync(extension, progress, ct);
            }

            extension.StatusMessage = "Reinstalling extension…";
            var success = await _installer.InstallAsync(extension, installerPath, ct);
            if (success)
            {
                extension.StatusMessage = $"{extension.DisplayName} reinstalled successfully.";
            }
            return success;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Reinstall failed for {ExtensionId}", extension.Id);
            extension.Status = ExtensionStatus.Failed;
            extension.ErrorMessage = $"Reinstall failed: {ex.Message}";
            return false;
        }
        finally
        {
            extension.IsBusy = false;
            extension.NotifyActionsChanged();
        }
    }

    /// <summary>
    /// CLEAN REINSTALL: Removes extension binaries, purges Axora extension cache and temp files,
    /// and performs a pristine download and installation.
    /// Strictly guarantees user documents and unrelated data are NEVER touched.
    /// </summary>
    public async Task<bool> CleanReinstallAsync(ExtensionModel extension, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(extension);

        _logger?.LogInformation("Starting Clean Reinstall operation for {ExtensionId}", extension.Id);
        extension.IsBusy = true;
        extension.StatusMessage = "Wiping extension binaries and staging cache…";

        try
        {
            // 1. Uninstall binaries
            await _installer.UninstallAsync(extension, ct);

            // 2. Clear isolated extension cache (downloads, installers, temp, metadata for this ID only)
            await _cacheService.ClearCacheAsync(extension.Id, ct);
            await _cacheService.ClearTemporaryFilesAsync(extension.Id, ct);

            extension.StagedInstallerPath = null;
            extension.CacheSizeBytes = 0;

            // 3. Pristine download from official vendor
            extension.StatusMessage = "Performing pristine download from distribution source…";
            var progress = new Progress<double>(p => extension.DownloadProgress = p * 100.0);
            var freshInstallerPath = await _downloader.DownloadExtensionAsync(extension, progress, ct);

            // 4. Clean installation
            extension.StatusMessage = "Executing clean installation…";
            var success = await _installer.InstallAsync(extension, freshInstallerPath, ct);

            // 5. Update cache metrics
            extension.CacheSizeBytes = await _cacheService.GetCacheSizeBytesAsync(extension.Id, ct);

            if (success)
            {
                extension.StatusMessage = $"Clean reinstallation of {extension.DisplayName} completed.";
            }
            return success;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Clean Reinstall failed for {ExtensionId}", extension.Id);
            extension.Status = ExtensionStatus.Failed;
            extension.ErrorMessage = $"Clean Reinstall failed: {ex.Message}";
            return false;
        }
        finally
        {
            extension.IsBusy = false;
            extension.NotifyActionsChanged();
        }
    }
}