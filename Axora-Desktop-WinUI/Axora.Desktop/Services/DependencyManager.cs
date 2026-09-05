using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Primary coordinator for external dependencies and extensions.
/// Strictly enforces explicit user control: no silent installs, upgrades, or removals.
/// </summary>
public sealed class DependencyManager : IDependencyManager
{
    private readonly IExtensionRegistry _registry;
    private readonly IVersionDetector _versionDetector;
    private readonly IExtensionValidator _validator;
    private readonly IExtensionDownloader _downloader;
    private readonly IExtensionInstaller _installer;
    private readonly IExtensionRepairService _repairService;
    private readonly IExtensionCacheService _cacheService;
    private readonly ILogger<DependencyManager>? _logger;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _operationLocks = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<ExtensionStateChangedEventArgs>? DependencyStatusChanged;

    public IReadOnlyList<ExtensionModel> Extensions => _registry.GetAll();

    public DependencyManager(
        IExtensionRegistry registry,
        IVersionDetector versionDetector,
        IExtensionValidator validator,
        IExtensionDownloader downloader,
        IExtensionInstaller installer,
        IExtensionRepairService repairService,
        IExtensionCacheService cacheService,
        ILogger<DependencyManager>? logger = null)
    {
        _registry = registry;
        _versionDetector = versionDetector;
        _validator = validator;
        _downloader = downloader;
        _installer = installer;
        _repairService = repairService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _logger?.LogInformation("Initializing Extension Dependency Manager...");
        await RefreshAllStatusAsync(checkRemoteVersions: false, ct);
    }

    public async Task RefreshAllStatusAsync(bool checkRemoteVersions = false, CancellationToken ct = default)
    {
        foreach (var extension in _registry.GetAll())
        {
            ct.ThrowIfCancellationRequested();
            await CheckStatusAsync(extension.Id, forceRefresh: true, checkRemoteVersion: checkRemoteVersions, ct: ct);
        }
    }

    public async Task<ExtensionStatus> CheckStatusAsync(
        string extensionId,
        bool forceRefresh = false,
        bool checkRemoteVersion = false,
        CancellationToken ct = default)
    {
        var extension = _registry.GetById(extensionId);
        if (extension == null) return ExtensionStatus.NotInstalled;

        var sem = _operationLocks.GetOrAdd(extensionId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        try
        {
            return await CheckStatusInternalAsync(extension, checkRemoteVersion, ct);
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<ExtensionStatus> CheckStatusInternalAsync(ExtensionModel extension, bool checkRemoteVersion, CancellationToken ct)
    {
        var oldStatus = extension.Status;

        // 1. Detect Installed Version
        var (isDetected, version, exePath) = await _versionDetector.DetectInstalledVersionAsync(extension, ct);

        if (!isDetected || string.IsNullOrWhiteSpace(exePath))
        {
            extension.InstalledVersion = null;
            extension.Status = ExtensionStatus.NotInstalled;
            extension.ErrorMessage = null;
            if (checkRemoteVersion)
            {
                extension.UpdateStatus = UpdateCheckStatus.NotChecked;
            }
        }
        else
        {
            extension.InstalledVersion = version;

            // 2. Validate Binary Integrity
            var valResult = await _validator.ValidateExtensionAsync(extension, ct);
            if (!valResult.IsValid)
            {
                if (valResult.IsCorrupted)
                    extension.Status = ExtensionStatus.Corrupted;
                else if (valResult.RequiresRepair)
                    extension.Status = ExtensionStatus.RepairRequired;
                else
                    extension.Status = ExtensionStatus.Failed;

                extension.ErrorMessage = valResult.ErrorMessage;
            }
            else
            {
                // Extension binaries are valid; base operational status is Installed
                extension.Status = ExtensionStatus.Installed;
                extension.ErrorMessage = null;

                // 3. Remote Version / Update Check
                if (checkRemoteVersion)
                {
                    extension.UpdateStatus = UpdateCheckStatus.Checking;
                    try
                    {
                        var remoteLatest = await _versionDetector.FetchLatestVersionAsync(extension, ct);
                        if (string.IsNullOrWhiteSpace(remoteLatest))
                        {
                            // Network failure or vendor unreachable: preserve healthy Installed status!
                            extension.UpdateStatus = UpdateCheckStatus.UnableToCheck;
                        }
                        else
                        {
                            extension.LatestVersion = remoteLatest;
                            var cmp = _versionDetector.CompareVersions(version, remoteLatest);
                            if (cmp < 0)
                            {
                                extension.Status = ExtensionStatus.UpdateAvailable;
                                extension.UpdateStatus = UpdateCheckStatus.UpdateAvailable;
                            }
                            else
                            {
                                extension.Status = ExtensionStatus.Installed;
                                extension.UpdateStatus = UpdateCheckStatus.UpToDate;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Update check failed for {ExtensionId}", extension.Id);
                        extension.UpdateStatus = UpdateCheckStatus.UnableToCheck;
                        // Operational status remains ExtensionStatus.Installed
                    }
                }
                else
                {
                    var compareVersion = extension.LatestVersion ?? extension.TargetVersion;
                    if (!string.IsNullOrWhiteSpace(compareVersion))
                    {
                        var cmp = _versionDetector.CompareVersions(version, compareVersion);
                        if (cmp < 0)
                        {
                            extension.Status = ExtensionStatus.UpdateAvailable;
                            extension.UpdateStatus = UpdateCheckStatus.UpdateAvailable;
                        }
                        else
                        {
                            extension.Status = ExtensionStatus.Installed;
                        }
                    }
                }
            }
        }

        // 4. Update Cache Metrics
        extension.CacheSizeBytes = await _cacheService.GetCacheSizeBytesAsync(extension.Id, ct);
        extension.CacheDirectory = _cacheService.GetExtensionCacheDirectory(extension.Id);
        extension.NotifyActionsChanged();

        if (oldStatus != extension.Status)
        {
            DependencyStatusChanged?.Invoke(this, new ExtensionStateChangedEventArgs(
                extension.Id, oldStatus, extension.Status, extension));
        }

        return extension.Status;
    }

    public bool IsDependencyReady(string extensionId)
    {
        var ext = _registry.GetById(extensionId);
        if (ext == null) return false;
        return ext.Status is ExtensionStatus.Installed or ExtensionStatus.UpdateAvailable;
    }

    public ExtensionModel? GetExtension(string extensionId)
    {
        return _registry.GetById(extensionId);
    }

    public async Task<bool> InstallExtensionAsync(string extensionId, CancellationToken ct = default)
    {
        var extension = _registry.GetById(extensionId);
        if (extension == null) return false;

        var sem = _operationLocks.GetOrAdd(extensionId, _ => new SemaphoreSlim(1, 1));
        if (!await sem.WaitAsync(0, ct))
        {
            _logger?.LogWarning("Operation already in progress for {ExtensionId}", extensionId);
            return false;
        }

        try
        {
            if (extension.IsBusy) return false;

            extension.IsBusy = true;
            var oldStatus = extension.Status;
            extension.Status = ExtensionStatus.Installing;
            extension.StatusMessage = $"Downloading {extension.DisplayName}…";
            extension.NotifyActionsChanged();

            DependencyStatusChanged?.Invoke(this, new ExtensionStateChangedEventArgs(
                extension.Id, oldStatus, extension.Status, extension));

            var progress = new Progress<double>(p => extension.DownloadProgress = p * 100.0);
            var stagedInstaller = await _downloader.DownloadExtensionAsync(extension, progress, ct);

            extension.StatusMessage = $"Installing {extension.DisplayName}…";
            var success = await _installer.InstallAsync(extension, stagedInstaller, ct);

            await CheckStatusInternalAsync(extension, false, ct);
            return success;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Installation failed for {ExtensionId}", extensionId);
            extension.Status = ExtensionStatus.Failed;
            extension.ErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            extension.IsBusy = false;
            extension.NotifyActionsChanged();
            sem.Release();
        }
    }

    public async Task<bool> UpdateExtensionAsync(string extensionId, CancellationToken ct = default)
    {
        // Explicit user action required. Updates are handled identically to install with fresh download.
        return await InstallExtensionAsync(extensionId, ct);
    }

    public async Task<bool> RepairExtensionAsync(string extensionId, CancellationToken ct = default)
    {
        var extension = _registry.GetById(extensionId);
        if (extension == null) return false;

        var sem = _operationLocks.GetOrAdd(extensionId, _ => new SemaphoreSlim(1, 1));
        if (!await sem.WaitAsync(0, ct)) return false;

        try
        {
            var oldStatus = extension.Status;
            extension.Status = ExtensionStatus.Installing;
            DependencyStatusChanged?.Invoke(this, new ExtensionStateChangedEventArgs(
                extension.Id, oldStatus, extension.Status, extension));

            var success = await _repairService.RepairAsync(extension, ct);
            await CheckStatusInternalAsync(extension, false, ct);
            return success;
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<bool> ReinstallExtensionAsync(string extensionId, CancellationToken ct = default)
    {
        var extension = _registry.GetById(extensionId);
        if (extension == null) return false;

        var sem = _operationLocks.GetOrAdd(extensionId, _ => new SemaphoreSlim(1, 1));
        if (!await sem.WaitAsync(0, ct)) return false;

        try
        {
            var oldStatus = extension.Status;
            extension.Status = ExtensionStatus.Installing;
            DependencyStatusChanged?.Invoke(this, new ExtensionStateChangedEventArgs(
                extension.Id, oldStatus, extension.Status, extension));

            var success = await _repairService.ReinstallAsync(extension, ct);
            await CheckStatusInternalAsync(extension, false, ct);
            return success;
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<bool> CleanReinstallExtensionAsync(string extensionId, CancellationToken ct = default)
    {
        var extension = _registry.GetById(extensionId);
        if (extension == null) return false;

        var sem = _operationLocks.GetOrAdd(extensionId, _ => new SemaphoreSlim(1, 1));
        if (!await sem.WaitAsync(0, ct)) return false;

        try
        {
            var oldStatus = extension.Status;
            extension.Status = ExtensionStatus.Installing;
            DependencyStatusChanged?.Invoke(this, new ExtensionStateChangedEventArgs(
                extension.Id, oldStatus, extension.Status, extension));

            var success = await _repairService.CleanReinstallAsync(extension, ct);
            await CheckStatusInternalAsync(extension, false, ct);
            return success;
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<bool> RemoveExtensionAsync(string extensionId, CancellationToken ct = default)
    {
        var extension = _registry.GetById(extensionId);
        if (extension == null) return false;

        var sem = _operationLocks.GetOrAdd(extensionId, _ => new SemaphoreSlim(1, 1));
        if (!await sem.WaitAsync(0, ct)) return false;

        try
        {
            extension.IsBusy = true;
            extension.StatusMessage = $"Uninstalling {extension.DisplayName}…";
            var success = await _installer.UninstallAsync(extension, ct);
            await CheckStatusInternalAsync(extension, false, ct);
            return success;
        }
        finally
        {
            extension.IsBusy = false;
            extension.NotifyActionsChanged();
            sem.Release();
        }
    }

    public async Task ClearAllCacheAsync(CancellationToken ct = default)
    {
        await _cacheService.ClearCacheAsync(null, ct);
        foreach (var ext in _registry.GetAll())
        {
            ext.CacheSizeBytes = 0;
            ext.StagedInstallerPath = null;
            ext.NotifyActionsChanged();
        }
    }
}