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
/// Intelligent Compressor ViewModel — FEAT-10: ETA/elapsed timer display during batch compression.
/// </summary>
public sealed partial class CompressorViewModel : ObservableObject, IDisposable
{
    private readonly IIntelligentCompressorService _compressorService;
    private readonly IAppSettingsService _settings;
    private readonly DispatcherQueue _dispatcher;
    private readonly Stopwatch _stopwatch = new();
    private CancellationTokenSource? _cts;

    public ObservableCollection<CompressionJob> Queue { get; } = [];

    [ObservableProperty] private int _selectedProfileIndex = 1;  // 0=Low, 1=Medium, 2=High
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private double _batchProgress;
    [ObservableProperty] private string _statusMessage = "Add PDF, Word, PowerPoint, or image files to begin compression.";
    [ObservableProperty] private string _totalSavedFormatted = "0.0 MB (0%)";
    [ObservableProperty] private string _compressionRatioFormatted = "1.0 : 1";
    [ObservableProperty] private bool _hasItems;

    // FEAT-10: ETA/elapsed timer display
    [ObservableProperty] private string _elapsedEtaText = string.Empty;

    public CompressorViewModel(IIntelligentCompressorService compressorService, IAppSettingsService settings)
    {
        _compressorService = compressorService;
        _settings = settings;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        UpdateTelemetry();
    }

    [RelayCommand]
    public void AddFiles(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths)
        {
            if (File.Exists(path) && IsCompressibleFile(path))
                Queue.Add(new CompressionJob { SourceFilePath = path, OriginalSizeBytes = new FileInfo(path).Length, Status = BatchJobStatus.Queued });
        }
        UpdateTelemetry();
        StatusMessage = $"Added {Queue.Count} file(s) to compression queue.";
    }

    [RelayCommand]
    public void ClearQueue()
    {
        Queue.Clear();
        UpdateTelemetry();
        StatusMessage = "Queue cleared.";
        ElapsedEtaText = string.Empty;
    }

    [RelayCommand]
    public async Task StartCompressionAsync()
    {
        if (Queue.Count == 0 || IsProcessing) return;
        IsProcessing = true;
        BatchProgress = 0;
        ElapsedEtaText = string.Empty;
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _stopwatch.Restart();

        var outDir = Path.Combine(_settings.DownloadDirectory, "Compressed_Export");
        Directory.CreateDirectory(outDir);

        var profile = SelectedProfileIndex switch
        {
            0 => CompressionProfile.LowQualityLossless,
            2 => CompressionProfile.HighMaxCompression,
            _ => CompressionProfile.MediumBalanced
        };
        StatusMessage = $"Compressing {Queue.Count} files with {profile} profile…";

        var progress = new Progress<double>(p =>
        {
            _dispatcher.TryEnqueue(() =>
            {
                BatchProgress = p * 100.0;
                UpdateTelemetry();
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
            await _compressorService.CompressBatchAsync(Queue.ToList(), profile, outDir, progress, job => _dispatcher.TryEnqueue(() => UpdateTelemetry()), _cts.Token);
            StatusMessage = $"Compression complete! Saved to {outDir}";
        }
        catch (OperationCanceledException) { StatusMessage = "Compression cancelled by user."; }
        catch (Exception ex) { StatusMessage = $"Compression failed: {ex.Message}"; }
        finally
        {
            IsProcessing = false;
            _stopwatch.Stop();
            ElapsedEtaText = string.Empty;
            UpdateTelemetry();
        }
    }

    [RelayCommand] public void CancelCompression() => _cts?.Cancel();

    [RelayCommand]
    public void OpenOutputFolder()
    {
        var outDir = Path.Combine(_settings.DownloadDirectory, "Compressed_Export");
        Directory.CreateDirectory(outDir);
        Process.Start(new ProcessStartInfo { FileName = outDir, UseShellExecute = true });
    }

    private void UpdateTelemetry()
    {
        HasItems = Queue.Count > 0;
        long origTotal = Queue.Where(j => j.Status == BatchJobStatus.Completed).Sum(j => j.OriginalSizeBytes);
        long compTotal = Queue.Where(j => j.Status == BatchJobStatus.Completed).Sum(j => j.CompressedSizeBytes);
        long saved = Math.Max(0, origTotal - compTotal);
        if (origTotal > 0 && compTotal > 0)
        {
            TotalSavedFormatted = $"{saved / (1024.0 * 1024.0):F1} MB ({(double)saved / origTotal * 100:F1}%)";
            CompressionRatioFormatted = $"{(double)origTotal / compTotal:F1} : 1";
        }
    }

    private static bool IsCompressibleFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".pdf" or ".docx" or ".pptx" or ".xlsx" or ".zip" or ".jpg" or ".jpeg" or ".png";
    }

    public void Dispose() { _cts?.Cancel(); _cts?.Dispose(); }
}
