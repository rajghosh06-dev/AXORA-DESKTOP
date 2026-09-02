using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
/// Batch Image Studio ViewModel — dual-engine (WIC / ImageMagick), target-size and quality modes,
/// multi-file/folder batch ingestion, and FEAT-10 ETA/elapsed timer display.
/// </summary>
public sealed partial class BatchImageViewModel : ObservableObject, IDisposable
{
    private readonly IBatchImageProcessorService _processorService;
    private readonly IAppSettingsService _settings;
    private readonly DispatcherQueue _dispatcher;
    private readonly Stopwatch _stopwatch = new();
    private CancellationTokenSource? _cts;

    public ObservableCollection<BatchImageJob> Queue { get; } = [];

    [ObservableProperty] private int _selectedEngineIndex = 1;     // 0=WIC, 1=ImageMagick Studio
    [ObservableProperty] private int _selectedFormatIndex = 1;     // 0=PNG, 1=JPEG, 2=BMP, 3=TIFF
    [ObservableProperty] private int _selectedResizeIndex;         // 0=Original, 1=50%, 2=75%, 3=1080p, 4=4K

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTargetSizeMode))]
    [NotifyPropertyChangedFor(nameof(IsQualityMode))]
    private int _compressionModeIndex;  // 0=Target File Size, 1=Quality Percentage, 2=Lossless

    public bool IsTargetSizeMode => CompressionModeIndex == 0;
    public bool IsQualityMode    => CompressionModeIndex == 1;

    [ObservableProperty] private double _targetSizeValue = 500;
    [ObservableProperty] private int _selectedUnitIndex;  // 0=KB, 1=MB, 2=GB
    public string SelectedUnitString => SelectedUnitIndex switch { 1 => "MB", 2 => "GB", _ => "KB" };

    [ObservableProperty] private double _qualityLevel = 85;
    [ObservableProperty] private bool _stripMetadata = true;
    [ObservableProperty] private string _watermarkText = string.Empty;
    [ObservableProperty] private string _customOutputDirectory = string.Empty;
    [ObservableProperty] private bool _useCustomOutputDirectory;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private double _batchProgress;
    [ObservableProperty] private string _statusMessage = "Add images or choose 'Browse Folder' to begin batch processing.";
    [ObservableProperty] private string _processedCountFormatted = "0 / 0";
    [ObservableProperty] private bool _hasItems;

    // FEAT-10: ETA / elapsed timer display
    [ObservableProperty] private string _elapsedEtaText = string.Empty;

    public string EffectiveOutputDirectory => UseCustomOutputDirectory && !string.IsNullOrWhiteSpace(CustomOutputDirectory)
        ? CustomOutputDirectory
        : Path.Combine(_settings.DownloadDirectory, "BatchImages_Export");

    public BatchImageViewModel(IBatchImageProcessorService processorService, IAppSettingsService settings)
    {
        _processorService = processorService;
        _settings = settings;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _customOutputDirectory = Path.Combine(_settings.DownloadDirectory, "BatchImages_Export");
        UpdateState();
    }

    [RelayCommand]
    public void SetTargetPreset(string preset)
    {
        var parts = preset.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && double.TryParse(parts[0], out var val))
        {
            TargetSizeValue = val;
            SelectedUnitIndex = parts[1].ToUpperInvariant() switch { "MB" => 1, "GB" => 2, _ => 0 };
            CompressionModeIndex = 0;
        }
    }

    [RelayCommand]
    public void AddFiles(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths)
        {
            if (File.Exists(path) && IsImageFile(path) && !Queue.Any(j => j.SourceFilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                Queue.Add(new BatchImageJob { SourceFilePath = path, OriginalSizeBytes = new FileInfo(path).Length, Status = BatchJobStatus.Queued });
        }
        UpdateState();
    }

    [RelayCommand]
    public async Task AddFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;
        StatusMessage = $"Scanning folder '{Path.GetFileName(folderPath)}' for image files…";
        var files = await _processorService.ScanFolderForImagesAsync(folderPath, includeSubfolders: true);
        foreach (var path in files)
        {
            if (!Queue.Any(j => j.SourceFilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                Queue.Add(new BatchImageJob { SourceFilePath = path, OriginalSizeBytes = new FileInfo(path).Length, Status = BatchJobStatus.Queued });
        }
        UpdateState();
        StatusMessage = $"Added {files.Count} image(s) from folder. Total in queue: {Queue.Count}";
    }

    [RelayCommand]
    public void ClearQueue()
    {
        Queue.Clear();
        UpdateState();
        StatusMessage = "Queue cleared.";
        ElapsedEtaText = string.Empty;
    }

    [RelayCommand]
    public async Task StartBatchAsync()
    {
        if (Queue.Count == 0 || IsProcessing) return;
        IsProcessing = true;
        BatchProgress = 0;
        ElapsedEtaText = string.Empty;
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _stopwatch.Restart();

        var outDir = EffectiveOutputDirectory;
        Directory.CreateDirectory(outDir);

        var options = new BatchImageOptions
        {
            Engine = SelectedEngineIndex == 1 ? ImageProcessingEngine.ImageMagickStudio : ImageProcessingEngine.WindowsHardwareWic,
            TargetFormat = SelectedFormatIndex switch { 0 => ImageTargetFormat.Png, 2 => ImageTargetFormat.Bmp, 3 => ImageTargetFormat.Tiff, _ => ImageTargetFormat.Jpeg },
            CompressionMode = CompressionModeIndex switch { 0 => CompressionSizeMode.TargetFileSize, 1 => CompressionSizeMode.QualityPercentage, _ => CompressionSizeMode.LosslessMaximum },
            TargetSizeValue = Math.Max(1.0, TargetSizeValue),
            TargetSizeUnit = SelectedUnitString,
            QualityLevel = (int)Math.Clamp(QualityLevel, 1, 100),
            StripExifMetadata = StripMetadata,
            WatermarkText = WatermarkText,
            OutputDirectory = outDir
        };

        switch (SelectedResizeIndex)
        {
            case 1: options.ResizeMode = ResizeMode.Percentage; options.ScalePercentage = 50; break;
            case 2: options.ResizeMode = ResizeMode.Percentage; options.ScalePercentage = 75; break;
            case 3: options.ResizeMode = ResizeMode.FixedWidth; options.TargetWidth = 1920; break;
            case 4: options.ResizeMode = ResizeMode.FixedWidth; options.TargetWidth = 3840; break;
            default: options.ResizeMode = ResizeMode.None; break;
        }

        StatusMessage = $"Processing {Queue.Count} images with {options.Engine}…";

        var progress = new Progress<double>(p =>
        {
            _dispatcher.TryEnqueue(() =>
            {
                BatchProgress = p * 100.0;
                var completed = Queue.Count(j => j.Status is BatchJobStatus.Completed or BatchJobStatus.Failed);
                ProcessedCountFormatted = $"{completed} / {Queue.Count}";
                var elapsed = _stopwatch.Elapsed;
                if (p > 0.001)
                {
                    var etaSecs = elapsed.TotalSeconds / p * (1.0 - p);
                    ElapsedEtaText = $"Elapsed: {elapsed:mm\\:ss} · ETA: {TimeSpan.FromSeconds(etaSecs):mm\\:ss}";
                }
            });
        });

        try
        {
            await _processorService.ProcessBatchAsync(Queue.ToList(), options, progress, job => _dispatcher.TryEnqueue(() => UpdateState()), _cts.Token);
            StatusMessage = $"Batch processing completed! Saved to {outDir}";
        }
        catch (OperationCanceledException) { StatusMessage = "Batch cancelled by user."; }
        catch (Exception ex) { StatusMessage = $"Batch failed: {ex.Message}"; }
        finally
        {
            IsProcessing = false;
            _stopwatch.Stop();
            ElapsedEtaText = string.Empty;
            UpdateState();
        }
    }

    [RelayCommand] public void CancelBatch() => _cts?.Cancel();

    [RelayCommand]
    public void OpenOutputFolder()
    {
        var outDir = EffectiveOutputDirectory;
        Directory.CreateDirectory(outDir);
        Process.Start(new ProcessStartInfo { FileName = outDir, UseShellExecute = true });
    }

    public void SetCustomOutputDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        CustomOutputDirectory = path;
        UseCustomOutputDirectory = true;
        OnPropertyChanged(nameof(EffectiveOutputDirectory));
    }

    private void UpdateState()
    {
        HasItems = Queue.Count > 0;
        var completed = Queue.Count(j => j.Status is BatchJobStatus.Completed or BatchJobStatus.Failed);
        ProcessedCountFormatted = $"{completed} / {Queue.Count}";
    }

    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tiff" or ".webp" or ".heic" or ".raw" or ".gif" or ".svg";
    }

    public void Dispose() { _cts?.Cancel(); _cts?.Dispose(); }
}
