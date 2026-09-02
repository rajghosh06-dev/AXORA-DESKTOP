using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axora.Desktop.Models;

/// <summary>
/// Represents a file received or sent via the QuickDrop P2P transfer protocol with live speed & progress.
/// </summary>
public sealed partial class QuickDropItem : ObservableObject
{
    public string ItemId { get; init; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedSize))]
    private long _sizeBytes;

    public string FormattedSize => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{SizeBytes / (1024.0 * 1024):F1} MB",
        _ => $"{SizeBytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;

    [ObservableProperty]
    private string _localPath = string.Empty;

    [ObservableProperty]
    private TransferStatus _status = TransferStatus.Pending;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercent))]
    private double _progress;

    public double ProgressPercent => Math.Clamp(Progress * 100.0, 0, 100);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedSpeed))]
    private double _transferSpeedBps;

    public string FormattedSpeed => TransferSpeedBps switch
    {
        < 1024 => $"{TransferSpeedBps:F0} B/s",
        < 1024 * 1024 => $"{TransferSpeedBps / 1024.0:F1} KB/s",
        _ => $"{TransferSpeedBps / (1024.0 * 1024.0):F1} MB/s"
    };

    [ObservableProperty]
    private string _sourceDeviceName = string.Empty;

    [ObservableProperty]
    private bool _isOutgoing;
}

public enum TransferStatus
{
    Pending,
    Receiving,
    Sending,
    Completed,
    Failed,
    Cancelled
}
