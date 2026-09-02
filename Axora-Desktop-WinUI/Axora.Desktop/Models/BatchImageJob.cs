using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axora.Desktop.Models;

public enum ImageTargetFormat
{
    Png,
    Jpeg,
    Bmp,
    Tiff
}

public enum ResizeMode
{
    None,
    Percentage,
    FixedWidth,
    FixedHeight,
    FitBounds
}

public enum CompressionSizeMode
{
    TargetFileSize,    // User specifies explicit target size (e.g., 500 KB, 2 MB)
    QualityPercentage, // User specifies standard quality percentage (1-100%)
    LosslessMaximum    // Maximum lossless compression
}

public enum BatchJobStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
    Skipped
}

public sealed partial class BatchImageJob : ObservableObject
{
    public string JobId { get; init; } = Guid.NewGuid().ToString("N");

    public string SourceFilePath { get; set; } = string.Empty;
    public string OutputFilePath { get; set; } = string.Empty;
    public string FileName => System.IO.Path.GetFileName(SourceFilePath);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedOriginalSize))]
    private long _originalSizeBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedOutputSize))]
    private long _outputSizeBytes;

    [ObservableProperty]
    private BatchJobStatus _status = BatchJobStatus.Queued;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public string FormattedOriginalSize => OriginalSizeBytes switch
    {
        < 1024 => $"{OriginalSizeBytes} B",
        < 1024 * 1024 => $"{OriginalSizeBytes / 1024.0:F1} KB",
        _ => $"{OriginalSizeBytes / (1024.0 * 1024.0):F2} MB"
    };

    public string FormattedOutputSize => OutputSizeBytes switch
    {
        < 1024 => $"{OutputSizeBytes} B",
        < 1024 * 1024 => $"{OutputSizeBytes / 1024.0:F1} KB",
        _ => $"{OutputSizeBytes / (1024.0 * 1024.0):F2} MB"
    };
}
