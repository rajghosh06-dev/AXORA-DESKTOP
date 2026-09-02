using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

public enum ImageProcessingEngine
{
    WindowsHardwareWic,
    ImageMagickStudio
}

public sealed class BatchImageOptions
{
    public ImageProcessingEngine Engine { get; set; } = ImageProcessingEngine.ImageMagickStudio;
    public ImageTargetFormat TargetFormat { get; set; } = ImageTargetFormat.Jpeg;
    public ResizeMode ResizeMode { get; set; } = ResizeMode.None;
    public double ScalePercentage { get; set; } = 100.0;
    public uint TargetWidth { get; set; } = 1920;
    public uint TargetHeight { get; set; } = 1080;

    // Compression Mode & Size controls
    public CompressionSizeMode CompressionMode { get; set; } = CompressionSizeMode.TargetFileSize;
    public double TargetSizeValue { get; set; } = 500.0; // e.g. 500
    public string TargetSizeUnit { get; set; } = "KB";   // "KB", "MB", "GB"
    public int QualityLevel { get; set; } = 85;          // 1-100

    public long TargetSizeBytes => (long)(TargetSizeValue * (TargetSizeUnit.ToUpperInvariant() switch
    {
        "GB" => 1024L * 1024L * 1024L,
        "MB" => 1024L * 1024L,
        _ => 1024L
    }));

    public bool StripExifMetadata { get; set; } = true;
    public string WatermarkText { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public bool IncludeSubfolders { get; set; } = true;
}

public interface IBatchImageProcessorService
{
    Task ProcessBatchAsync(
        IReadOnlyList<BatchImageJob> jobs,
        BatchImageOptions options,
        IProgress<double>? progress = null,
        Action<BatchImageJob>? onItemProcessed = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<string>> ScanFolderForImagesAsync(
        string folderPath,
        bool includeSubfolders = true,
        CancellationToken ct = default);
}
