using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axora.Desktop.Models;

/// <summary>
/// Reusable extension and dependency domain model.
/// Follows MVVMTK 8.4 field-backed [ObservableProperty] convention for AOT / WinRT stability.
/// </summary>
public sealed partial class ExtensionModel : ObservableObject
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedInstalledVersion))]
    [NotifyPropertyChangedFor(nameof(CanInstallAction))]
    [NotifyPropertyChangedFor(nameof(CanUpdateAction))]
    [NotifyPropertyChangedFor(nameof(CanRepairAction))]
    [NotifyPropertyChangedFor(nameof(CanReinstallAction))]
    [NotifyPropertyChangedFor(nameof(CanCleanReinstallAction))]
    [NotifyPropertyChangedFor(nameof(CanRemoveAction))]
    private string? _installedVersion;

    /// <summary>Baseline version targeted and distributed with the current Axora release.</summary>
    public string TargetVersion { get; set; } = "1.0.0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedLatestVersion))]
    [NotifyPropertyChangedFor(nameof(CanUpdateAction))]
    private string? _latestVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedLatestVersion))]
    [NotifyPropertyChangedFor(nameof(CanUpdateAction))]
    private UpdateCheckStatus _updateStatus = UpdateCheckStatus.NotChecked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedStatus))]
    [NotifyPropertyChangedFor(nameof(CanInstallAction))]
    [NotifyPropertyChangedFor(nameof(CanUpdateAction))]
    [NotifyPropertyChangedFor(nameof(CanRepairAction))]
    [NotifyPropertyChangedFor(nameof(CanReinstallAction))]
    [NotifyPropertyChangedFor(nameof(CanCleanReinstallAction))]
    [NotifyPropertyChangedFor(nameof(CanRemoveAction))]
    private ExtensionStatus _status = ExtensionStatus.NotInstalled;

    public IReadOnlyList<string> RequiredBy { get; init; } = Array.Empty<string>();

    public bool IsRequired { get; init; }
    public bool IsOptional => !IsRequired;

    public required string InstallSource { get; init; }
    public required string DetectionStrategy { get; init; }
    public required string ExecutableName { get; init; }
    public string CapabilityInformation { get; init; } = string.Empty;
    public string SupportedArchitecture { get; init; } = "x64";
    public string SupportedOs { get; init; } = "Windows 10/11 (64-bit)";

    [ObservableProperty]
    private string? _cacheDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedCacheSize))]
    private long _cacheSizeBytes;

    public bool CanRepair { get; init; } = true;
    public bool CanReinstall { get; init; } = true;
    public bool CanCleanReinstall { get; init; } = true;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstallAction))]
    [NotifyPropertyChangedFor(nameof(CanUpdateAction))]
    [NotifyPropertyChangedFor(nameof(CanRepairAction))]
    [NotifyPropertyChangedFor(nameof(CanReinstallAction))]
    [NotifyPropertyChangedFor(nameof(CanCleanReinstallAction))]
    [NotifyPropertyChangedFor(nameof(CanRemoveAction))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isHighlighted;

    [ObservableProperty]
    private string? _stagedInstallerPath;

    public string? Sha256Hash { get; init; }

    // ── Computed UI Helpers ──────────────────────────────────────────────────

    public string FormattedInstalledVersion =>
        string.IsNullOrWhiteSpace(InstalledVersion) ? "Not Installed" : $"v{InstalledVersion}";

    public string FormattedLatestVersion => UpdateStatus switch
    {
        UpdateCheckStatus.Checking => "Checking…",
        UpdateCheckStatus.UnableToCheck => "Latest version unavailable",
        UpdateCheckStatus.UpToDate => !string.IsNullOrWhiteSpace(LatestVersion) ? $"v{LatestVersion} (Up to Date)" : $"v{TargetVersion} (Up to Date)",
        UpdateCheckStatus.UpdateAvailable => !string.IsNullOrWhiteSpace(LatestVersion) ? $"v{LatestVersion} (Update Available)" : "Update Available",
        _ => !string.IsNullOrWhiteSpace(LatestVersion) ? $"v{LatestVersion}" : "Latest version unavailable"
    };

    public string RequiredByDisplay =>
        RequiredBy.Count > 0 ? string.Join(", ", RequiredBy) : "None (Optional Component)";

    public string FormattedStatus => Status switch
    {
        ExtensionStatus.NotInstalled   => "Not Installed",
        ExtensionStatus.Installing     => "Installing…",
        ExtensionStatus.Installed      => "Installed",
        ExtensionStatus.UpdateAvailable=> "Update Available",
        ExtensionStatus.RepairRequired => "Repair Required",
        ExtensionStatus.Corrupted      => "Corrupted",
        ExtensionStatus.Unsupported    => "Unsupported Architecture",
        ExtensionStatus.Failed         => "Operation Failed",
        _                              => Status.ToString()
    };

    public string FormattedCacheSize => FormatBytes(CacheSizeBytes);

    // Action visibility booleans: strictly enforce valid actions per state
    public bool CanInstallAction =>
        !IsBusy && (Status is ExtensionStatus.NotInstalled or ExtensionStatus.Failed);

    public bool CanUpdateAction =>
        !IsBusy && (Status is ExtensionStatus.UpdateAvailable || UpdateStatus is UpdateCheckStatus.UpdateAvailable);

    public bool CanRepairAction =>
        !IsBusy && CanRepair && (Status is ExtensionStatus.Installed or ExtensionStatus.UpdateAvailable or ExtensionStatus.RepairRequired or ExtensionStatus.Corrupted);

    public bool CanReinstallAction =>
        !IsBusy && CanReinstall && (Status is ExtensionStatus.Installed or ExtensionStatus.UpdateAvailable or ExtensionStatus.RepairRequired or ExtensionStatus.Corrupted);

    public bool CanCleanReinstallAction =>
        !IsBusy && CanCleanReinstall;

    public bool CanRemoveAction =>
        !IsBusy && (Status is ExtensionStatus.Installed or ExtensionStatus.UpdateAvailable or ExtensionStatus.RepairRequired or ExtensionStatus.Corrupted);

    public void NotifyActionsChanged()
    {
        OnPropertyChanged(nameof(CanInstallAction));
        OnPropertyChanged(nameof(CanUpdateAction));
        OnPropertyChanged(nameof(CanRepairAction));
        OnPropertyChanged(nameof(CanReinstallAction));
        OnPropertyChanged(nameof(CanCleanReinstallAction));
        OnPropertyChanged(nameof(CanRemoveAction));
        OnPropertyChanged(nameof(FormattedStatus));
        OnPropertyChanged(nameof(FormattedInstalledVersion));
        OnPropertyChanged(nameof(FormattedLatestVersion));
        OnPropertyChanged(nameof(FormattedCacheSize));
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