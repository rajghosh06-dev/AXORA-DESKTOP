using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.ViewModels;

/// <summary>
/// ViewModel coordinating the user-controlled Extension & Download Manager.
/// Manages Installed, Available, and Update collections and drives maintenance/cache operations.
/// </summary>
public sealed partial class DownloadManagerViewModel : ObservableObject
{
    private readonly IDependencyManager _dependencyManager;
    private readonly IExtensionCacheService _cacheService;
    private readonly INotificationService _notificationService;
    private readonly DispatcherQueue? _dispatcher;

    public ObservableCollection<ExtensionModel> InstalledExtensions { get; } = [];
    public ObservableCollection<ExtensionModel> AvailableExtensions { get; } = [];
    public ObservableCollection<ExtensionModel> UpdateExtensions { get; } = [];

    [ObservableProperty]
    private string _cacheLocation = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedCacheSize))]
    private long _cacheSizeBytes;

    public string FormattedCacheSize => FormatBytes(CacheSizeBytes);

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private bool _isMaintenanceBusy;

    [ObservableProperty]
    private string? _activeOperationTitle;

    [ObservableProperty]
    private string? _activeOperationMessage;

    [ObservableProperty]
    private double _activeOperationProgress;

    [ObservableProperty]
    private bool _isActiveOperationVisible;

    [ObservableProperty]
    private bool _hasUpdates;

    [ObservableProperty]
    private bool _hasInstalled;

    [ObservableProperty]
    private bool _hasAvailable;

    public DownloadManagerViewModel(
        IDependencyManager dependencyManager,
        IExtensionCacheService cacheService,
        INotificationService notificationService)
    {
        _dependencyManager = dependencyManager;
        _cacheService = cacheService;
        _notificationService = notificationService;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        CacheLocation = _cacheService.CacheRootDirectory;

        _dependencyManager.DependencyStatusChanged += OnDependencyStatusChanged;

        RefreshCollections();
        _ = RefreshMetricsAsync();
    }

    public async Task InitializeAsync()
    {
        await _dependencyManager.InitializeAsync();
        RefreshCollections();
        await RefreshMetricsAsync();
    }

    private void OnDependencyStatusChanged(object? sender, ExtensionStateChangedEventArgs e)
    {
        if (_dispatcher != null)
        {
            _dispatcher.TryEnqueue(() =>
            {
                RefreshCollections();
                _ = RefreshMetricsAsync();
            });
        }
        else
        {
            RefreshCollections();
            _ = RefreshMetricsAsync();
        }
    }

    public void RefreshCollections()
    {
        var all = _dependencyManager.Extensions;

        InstalledExtensions.Clear();
        AvailableExtensions.Clear();
        UpdateExtensions.Clear();

        foreach (var ext in all)
        {
            if (ext.Status == ExtensionStatus.UpdateAvailable)
            {
                UpdateExtensions.Add(ext);
                InstalledExtensions.Add(ext);
            }
            else if (ext.Status is ExtensionStatus.Installed or ExtensionStatus.RepairRequired or ExtensionStatus.Corrupted)
            {
                InstalledExtensions.Add(ext);
            }
            else
            {
                AvailableExtensions.Add(ext);
            }
        }

        HasUpdates = UpdateExtensions.Count > 0;
        HasInstalled = InstalledExtensions.Count > 0;
        HasAvailable = AvailableExtensions.Count > 0;
    }

    public async Task RefreshMetricsAsync()
    {
        var totalBytes = await _cacheService.GetCacheSizeBytesAsync();
        if (_dispatcher != null)
        {
            _dispatcher.TryEnqueue(() =>
            {
                CacheSizeBytes = totalBytes;
            });
        }
        else
        {
            CacheSizeBytes = totalBytes;
        }
    }

    public void HighlightExtension(string extensionId)
    {
        foreach (var ext in _dependencyManager.Extensions)
        {
            ext.IsHighlighted = ext.Id.Equals(extensionId, StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        if (IsCheckingForUpdates) return;
        IsCheckingForUpdates = true;

        try
        {
            await _dependencyManager.RefreshAllStatusAsync(checkRemoteVersions: true);
            RefreshCollections();
            await RefreshMetricsAsync();

            int updatesCount = UpdateExtensions.Count;
            int unableToCheckCount = _dependencyManager.Extensions.Count(e => e.UpdateStatus == UpdateCheckStatus.UnableToCheck);
            if (updatesCount > 0)
            {
                _notificationService.Show(
                    $"{updatesCount} extension update(s) are ready for review.",
                    NotificationSeverity.Informational,
                    "Updates Available");
            }
            else if (unableToCheckCount > 0)
            {
                _notificationService.Show(
                    "Vendor update metadata is currently unavailable. Installed extensions remain healthy.",
                    NotificationSeverity.Informational,
                    "Check Complete");
            }
            else
            {
                _notificationService.ShowSuccess(
                    "No pending updates found for installed components.",
                    "All Extensions Up to Date");
            }
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    [RelayCommand]
    public async Task ClearCacheAsync()
    {
        if (IsMaintenanceBusy) return;
        IsMaintenanceBusy = true;

        try
        {
            await _dependencyManager.ClearAllCacheAsync();
            await RefreshMetricsAsync();
            RefreshCollections();

            _notificationService.ShowSuccess(
                "Extension download and installer cache purged successfully.",
                "Cache Cleared");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                ex.Message,
                "Cache Purge Failed");
        }
        finally
        {
            IsMaintenanceBusy = false;
        }
    }

    [RelayCommand]
    public async Task InstallAsync(ExtensionModel extension)
    {
        if (extension == null || extension.IsBusy) return;

        IsActiveOperationVisible = true;
        ActiveOperationTitle = $"Installing {extension.DisplayName}";
        ActiveOperationMessage = "Downloading and configuring files…";
        ActiveOperationProgress = 0;

        try
        {
            var success = await _dependencyManager.InstallExtensionAsync(extension.Id);
            RefreshCollections();
            await RefreshMetricsAsync();

            if (success)
            {
                _notificationService.ShowSuccess(
                    $"{extension.DisplayName} installed successfully.",
                    "Installation Complete");
            }
            else
            {
                _notificationService.ShowError(
                    extension.ErrorMessage ?? "The extension could not be installed.",
                    "Installation Failed");
            }
        }
        finally
        {
            IsActiveOperationVisible = false;
        }
    }

    [RelayCommand]
    public async Task UpdateAsync(ExtensionModel extension)
    {
        if (extension == null || extension.IsBusy) return;

        IsActiveOperationVisible = true;
        ActiveOperationTitle = $"Updating {extension.DisplayName}";
        ActiveOperationMessage = $"Upgrading to v{extension.LatestVersion}…";
        ActiveOperationProgress = 0;

        try
        {
            var success = await _dependencyManager.UpdateExtensionAsync(extension.Id);
            RefreshCollections();
            await RefreshMetricsAsync();

            if (success)
            {
                _notificationService.ShowSuccess(
                    $"{extension.DisplayName} updated to v{extension.InstalledVersion}.",
                    "Update Complete");
            }
            else
            {
                _notificationService.ShowError(
                    extension.ErrorMessage ?? "The extension could not be updated.",
                    "Update Failed");
            }
        }
        finally
        {
            IsActiveOperationVisible = false;
        }
    }

    [RelayCommand]
    public async Task RepairAsync(ExtensionModel extension)
    {
        if (extension == null || extension.IsBusy) return;

        IsActiveOperationVisible = true;
        ActiveOperationTitle = $"Repairing {extension.DisplayName}";
        ActiveOperationMessage = "Verifying and restoring missing binaries…";

        try
        {
            var success = await _dependencyManager.RepairExtensionAsync(extension.Id);
            RefreshCollections();
            await RefreshMetricsAsync();

            if (success)
            {
                _notificationService.ShowSuccess(
                    $"{extension.DisplayName} repaired and verified healthy.",
                    "Repair Complete");
            }
            else
            {
                _notificationService.ShowError(
                    extension.ErrorMessage ?? "The extension could not be repaired.",
                    "Repair Failed");
            }
        }
        finally
        {
            IsActiveOperationVisible = false;
        }
    }

    [RelayCommand]
    public async Task ReinstallAsync(ExtensionModel extension)
    {
        if (extension == null || extension.IsBusy) return;

        IsActiveOperationVisible = true;
        ActiveOperationTitle = $"Reinstalling {extension.DisplayName}";
        ActiveOperationMessage = "Replacing existing installation…";

        try
        {
            var success = await _dependencyManager.ReinstallExtensionAsync(extension.Id);
            RefreshCollections();
            await RefreshMetricsAsync();

            if (success)
            {
                _notificationService.ShowSuccess(
                    $"{extension.DisplayName} reinstalled successfully.",
                    "Reinstall Complete");
            }
            else
            {
                _notificationService.ShowError(
                    extension.ErrorMessage ?? "Reinstallation failed.",
                    "Reinstall Failed");
            }
        }
        finally
        {
            IsActiveOperationVisible = false;
        }
    }

    [RelayCommand]
    public async Task CleanReinstallAsync(ExtensionModel extension)
    {
        if (extension == null || extension.IsBusy) return;

        IsActiveOperationVisible = true;
        ActiveOperationTitle = $"Clean Reinstalling {extension.DisplayName}";
        ActiveOperationMessage = "Purging cache and reinstalling cleanly…";

        try
        {
            var success = await _dependencyManager.CleanReinstallExtensionAsync(extension.Id);
            RefreshCollections();
            await RefreshMetricsAsync();

            if (success)
            {
                _notificationService.ShowSuccess(
                    $"{extension.DisplayName} cleanly reinstalled.",
                    "Clean Reinstall Complete");
            }
            else
            {
                _notificationService.ShowError(
                    extension.ErrorMessage ?? "Clean reinstallation failed.",
                    "Clean Reinstall Failed");
            }
        }
        finally
        {
            IsActiveOperationVisible = false;
        }
    }

    [RelayCommand]
    public async Task RemoveAsync(ExtensionModel extension)
    {
        if (extension == null || extension.IsBusy) return;

        IsActiveOperationVisible = true;
        ActiveOperationTitle = $"Removing {extension.DisplayName}";
        ActiveOperationMessage = "Deleting managed binaries…";

        try
        {
            var success = await _dependencyManager.RemoveExtensionAsync(extension.Id);
            RefreshCollections();
            await RefreshMetricsAsync();

            if (success)
            {
                _notificationService.Show(
                    $"{extension.DisplayName} uninstalled.",
                    NotificationSeverity.Informational,
                    "Extension Removed");
            }
        }
        finally
        {
            IsActiveOperationVisible = false;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        int digitGroups = (int)(Math.Log(bytes) / Math.Log(1024));
        digitGroups = Math.Clamp(digitGroups, 0, units.Length - 1);
        return $"{bytes / Math.Pow(1024, digitGroups):F1} {units[digitGroups]}";
    }
}