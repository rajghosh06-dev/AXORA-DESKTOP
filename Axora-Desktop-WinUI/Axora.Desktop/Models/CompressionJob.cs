using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axora.Desktop.Models;

public enum CompressionProfile
{
    LowQualityLossless, // Max Quality (Strip metadata, remove revision history, optimal Deflate)
    MediumBalanced,     // Balanced (150 DPI, 75% quality)
    HighMaxCompression  // Min Size (72 DPI, 50% quality, stream compression)
}

public sealed partial class CompressionJob : ObservableObject
{
    public string JobId { get; init; } = Guid.NewGuid().ToString("N");

    public string SourceFilePath { get; set; } = string.Empty;
    public string OutputFilePath { get; set; } = string.Empty;
    public string FileName => System.IO.Path.GetFileName(SourceFilePath);

    [ObservableProperty]
    private long _originalSizeBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpaceSavedBytes))]
    [NotifyPropertyChangedFor(nameof(ReductionPercentage))]
    [NotifyPropertyChangedFor(nameof(FormattedOutputSize))]
    [NotifyPropertyChangedFor(nameof(FormattedSpaceSaved))]
    private long _compressedSizeBytes;

    [ObservableProperty]
    private BatchJobStatus _status = BatchJobStatus.Queued;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public long SpaceSavedBytes => Math.Max(0, OriginalSizeBytes - CompressedSizeBytes);

    public double ReductionPercentage => OriginalSizeBytes > 0 && CompressedSizeBytes > 0
        ? Math.Max(0, (1.0 - ((double)CompressedSizeBytes / OriginalSizeBytes)) * 100.0)
        : 0.0;

    public string FormattedOriginalSize => OriginalSizeBytes switch
    {
        < 1024 => $"{OriginalSizeBytes} B",
        < 1024 * 1024 => $"{OriginalSizeBytes / 1024.0:F1} KB",
        _ => $"{OriginalSizeBytes / (1024.0 * 1024.0):F2} MB"
    };

    public string FormattedOutputSize => CompressedSizeBytes switch
    {
        < 1024 => $"{CompressedSizeBytes} B",
        < 1024 * 1024 => $"{CompressedSizeBytes / 1024.0:F1} KB",
        _ => $"{CompressedSizeBytes / (1024.0 * 1024.0):F2} MB"
    };

    public string FormattedSpaceSaved => SpaceSavedBytes switch
    {
        < 1024 => $"{SpaceSavedBytes} B",
        < 1024 * 1024 => $"{SpaceSavedBytes / 1024.0:F1} KB",
        _ => $"{SpaceSavedBytes / (1024.0 * 1024.0):F2} MB"
    };
}
