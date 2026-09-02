using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.ViewModels;

/// <summary>
/// Dashboard ViewModel — real-time Windows OS hardware telemetry (GlobalMemoryStatusEx + GetSystemTimes),
/// active P2P connections, and live QuickDrop transfers.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private readonly PeriodicTimer _telemetryTimer;
    private readonly CancellationTokenSource _cts = new();
    private readonly IDownloadManagerService _downloadManager;
    private readonly IP2pSyncService _p2pSync;
    private readonly IAppSettingsService _settings;

    [ObservableProperty] private SystemTelemetry _telemetry;
    [ObservableProperty] private double _cpuUsagePercent;
    [ObservableProperty] private string _formattedCpu = "0.0%";
    [ObservableProperty] private string _statusBadge = "DirectML GPU Engine: Active | Local Compute Mode | P2P: Port 5050";
    [ObservableProperty] private bool _isTelemetryLoading;
    [ObservableProperty] private int _activeConnectionsCount;
    [ObservableProperty] private string _activeConnectionsText = "0 Active";
    [ObservableProperty] private string _activeConnectionsSubtitle = "Ready for Mobile Link";
    [ObservableProperty] private bool _hasRecentDrops;

    // FEAT-7: System diagnostics properties for hardware capability panel
    [ObservableProperty] private string _systemDiagnosticsText = string.Empty;
    [ObservableProperty] private bool _isSystemDiagnosticsOpen;

    public ObservableCollection<QuickDropItem> RecentDrops => _downloadManager.Transfers;

    public DashboardViewModel(
        IDownloadManagerService downloadManager,
        IP2pSyncService p2pSync,
        IAppSettingsService settings)
    {
        _downloadManager = downloadManager;
        _p2pSync = p2pSync;
        _settings = settings;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _telemetry = SystemTelemetry.Capture(_p2pSync.ConnectedDeviceCount);
        _cpuUsagePercent = _telemetry.CpuUsagePercent;
        _formattedCpu = _telemetry.CpuFormatted;
        UpdateP2pStatus();

        _downloadManager.Transfers.CollectionChanged += (_, _) =>
        {
            _dispatcher.TryEnqueue(() => HasRecentDrops = _downloadManager.Transfers.Count > 0);
        };
        HasRecentDrops = _downloadManager.Transfers.Count > 0;

        _telemetryTimer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        _ = StartTelemetryPollingAsync();
    }

    private void UpdateP2pStatus()
    {
        int count = _p2pSync.ConnectedDeviceCount;
        ActiveConnectionsCount = count;
        ActiveConnectionsText = $"{count} Active";
        ActiveConnectionsSubtitle = count > 0 ? "Encrypted P2P WebSocket" : "Ready for Mobile Link";
    }

    private async Task StartTelemetryPollingAsync()
    {
        try
        {
            while (await _telemetryTimer.WaitForNextTickAsync(_cts.Token))
            {
                int connCount = _p2pSync.ConnectedDeviceCount;
                var snap = await Task.Run(() => SystemTelemetry.Capture(connCount), _cts.Token);

                _dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                {
                    Telemetry = snap;
                    CpuUsagePercent = snap.CpuUsagePercent;
                    FormattedCpu = snap.CpuFormatted;
                    UpdateP2pStatus();
                    HasRecentDrops = _downloadManager.Transfers.Count > 0;
                });
            }
        }
        catch (OperationCanceledException) { /* Graceful shutdown */ }
    }

    [RelayCommand]
    private void RefreshTelemetry()
    {
        IsTelemetryLoading = true;
        Task.Run(() =>
        {
            int connCount = _p2pSync.ConnectedDeviceCount;
            var snap = SystemTelemetry.Capture(connCount);
            _dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                Telemetry = snap;
                CpuUsagePercent = snap.CpuUsagePercent;
                FormattedCpu = snap.CpuFormatted;
                UpdateP2pStatus();
                HasRecentDrops = _downloadManager.Transfers.Count > 0;
                IsTelemetryLoading = false;
            });
        });
    }

    // FEAT-7: Hardware diagnostic command — queries OS/hardware capabilities
    [RelayCommand]
    public async Task ShowSystemDiagnosticsAsync()
    {
        IsSystemDiagnosticsOpen = true;
        SystemDiagnosticsText = await Task.Run(() =>
        {
            var sb = new System.Text.StringBuilder();
            var osVersion = Environment.OSVersion;
            var cpuCount = Environment.ProcessorCount;
            var gcInfo = GC.GetGCMemoryInfo();
            long totalRamMb = gcInfo.TotalAvailableMemoryBytes / (1024 * 1024);
            long totalRamGb = totalRamMb / 1024;

            var drives = System.IO.DriveInfo.GetDrives();
            long freeGb = 0;
            foreach (var d in drives)
            {
                if (d.DriveType == DriveType.Fixed && d.IsReady && d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                    freeGb = d.AvailableFreeSpace / (1024L * 1024 * 1024);
            }

            bool ramOk = totalRamGb >= 4;
            bool diskOk = freeGb >= 1;
            bool cpuOk = cpuCount >= 4;

            sb.AppendLine($"OS: {osVersion.VersionString}");
            sb.AppendLine($"CPU Cores: {cpuCount} {(cpuOk ? "✓" : "⚠ Minimum 4 cores recommended")}");
            sb.AppendLine($"System RAM: {totalRamGb} GB {(ramOk ? "✓" : "⚠ Minimum 4 GB required")}");
            sb.AppendLine($"Free Disk (C:): {freeGb} GB {(diskOk ? "✓" : "⚠ Minimum 1 GB required")}");
            sb.AppendLine($"DirectML (GPU): Available via OnnxRuntime.DirectML");
            sb.AppendLine($"P2P Port: {_settings.P2pPort}");
            return sb.ToString();
        });
    }

    [RelayCommand]
    private void CloseSystemDiagnostics() => IsSystemDiagnosticsOpen = false;

    [RelayCommand]
    private void OpenQuickDropFolder()
    {
        var quickDropDir = _settings.DownloadDirectory;
        Directory.CreateDirectory(quickDropDir);
        Process.Start(new ProcessStartInfo
        {
            FileName = quickDropDir,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenFile(QuickDropItem item)
    {
        _downloadManager.OpenFile(item);
    }

    [RelayCommand]
    private void ShowInExplorer(QuickDropItem item)
    {
        _downloadManager.ShowInExplorer(item);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _telemetryTimer.Dispose();
    }
}
