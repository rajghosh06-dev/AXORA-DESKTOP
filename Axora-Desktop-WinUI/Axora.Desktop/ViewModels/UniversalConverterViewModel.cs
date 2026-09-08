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
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.ViewModels;

/// <summary>
/// ViewModel coordinating the Universal Converter view.
/// Thin client that delegates all engine routing, execution, scheduling, collision mechanics,
/// and atomic staging to the underlying IConversionOrchestrator.
/// </summary>
public sealed partial class UniversalConverterViewModel : ObservableObject, IDisposable
{
    private readonly IConversionOrchestrator _orchestrator;
    private readonly INotificationService _notifications;
    private readonly IAppSettingsService _settings;
    private readonly ILogger<UniversalConverterViewModel>? _logger;
    private readonly DispatcherQueue? _dispatcher;

    public ObservableCollection<ConversionJobUiModel> Queue { get; } = [];
    public ObservableCollection<string> SupportedSourceFormats { get; } = [];
    public ObservableCollection<string> AvailableTargetFormats { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    private bool _hasItems;

    [ObservableProperty]
    private bool _hasCompletedItems;

    [ObservableProperty]
    private bool _hasFailedItems;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _progressStatusText = "Add files to begin conversion.";

    [ObservableProperty]
    private string _currentJobText = string.Empty;

    [ObservableProperty]
    private string _totalBytesFormatted = "0 B";

    [ObservableProperty]
    private string _selectedTargetFormat = "Auto";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveOutputDirectory))]
    private bool _useSourceDirectory = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveOutputDirectory))]
    private string _customOutputDirectory = string.Empty;

    [ObservableProperty]
    private int _selectedCollisionPolicyIndex = 0; // 0 = AutoRename, 1 = Overwrite, 2 = Skip

    [ObservableProperty]
    private bool _stripMetadata;

    [ObservableProperty]
    private int _jpegQuality = 90;

    [ObservableProperty]
    private int _targetDpi = 150;

    // ── Phase W2-F4: Queue Telemetry Observables ──────────────────────────
    [ObservableProperty]
    private string _queueOutputVsInputText = "0 B";

    [ObservableProperty]
    private string _queueSavingsFormatted = "—";

    [ObservableProperty]
    private string _queueThroughputFormatted = "—";

    [ObservableProperty]
    private string _queueEtaFormatted = "—";

    [ObservableProperty]
    private bool _hasQueueTelemetry;

    [ObservableProperty]
    private int _pendingJobsCount;

    [ObservableProperty]
    private int _runningJobsCount;

    [ObservableProperty]
    private int _completedJobsCount;

    [ObservableProperty]
    private int _failedJobsCount;

    // ── Phase W2-F3: Optimization Presets & Custom Parameters ────────────
    public IReadOnlyList<OptimizationPreset> AvailablePresets => OptimizationPresetCatalog.All;

    [ObservableProperty]
    private OptimizationPreset _selectedPreset = OptimizationPresetCatalog.Default;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomQualityText))]
    private int _customQuality = OptimizationValidation.DefaultQuality;

    [ObservableProperty]
    private int _customMaxDimension = 0;

    [ObservableProperty]
    private int _customTargetDpi = OptimizationValidation.DefaultTargetDpi;

    [ObservableProperty]
    private MetadataHandling _customMetadataPolicy = MetadataHandling.Strip;

    [ObservableProperty]
    private bool _isCustomMode;

    public static IReadOnlyList<DimensionOption> MaxDimensionOptions { get; } =
    [
        new(0, "Original (Unconstrained)"),
        new(1280, "1280 px (HD)"),
        new(1920, "1920 px (Full HD)"),
        new(2560, "2560 px (2K / QHD)"),
        new(3840, "3840 px (4K UHD)")
    ];

    public static IReadOnlyList<DpiOption> TargetDpiOptions { get; } =
    [
        new(72, "72 DPI (Web / Screen)"),
        new(96, "96 DPI (Standard Windows)"),
        new(150, "150 DPI (Balanced Document)"),
        new(300, "300 DPI (High-Res Archival / Print)")
    ];

    public static IReadOnlyList<MetadataOption> MetadataOptions { get; } =
    [
        new(MetadataHandling.Strip, "Strip (Privacy Mode)"),
        new(MetadataHandling.Preserve, "Preserve Supported Properties")
    ];

    public IReadOnlyList<DimensionOption> AvailableDimensionOptions => MaxDimensionOptions;
    public IReadOnlyList<DpiOption> AvailableDpiOptions => TargetDpiOptions;
    public IReadOnlyList<MetadataOption> AvailableMetadataOptions => MetadataOptions;

    [ObservableProperty]
    private DimensionOption _selectedDimensionOption = MaxDimensionOptions[0];

    [ObservableProperty]
    private DpiOption _selectedDpiOption = TargetDpiOptions[2]; // 150 DPI

    [ObservableProperty]
    private MetadataOption _selectedMetadataOption = MetadataOptions[0]; // Strip

    public ConversionProfile EffectiveProfile { get; private set; } = OptimizationPresetCatalog.Default.ToProfile();

    public string EffectiveQualityText => $"{EffectiveProfile?.Quality ?? 85}%";
    public string EffectiveMaxDimensionText => (EffectiveProfile?.MaxDimension ?? 0) == 0 ? "Original" : $"{EffectiveProfile?.MaxDimension} px";
    public string EffectiveTargetDpiText => $"{EffectiveProfile?.TargetDpi ?? 150} DPI";
    public string EffectiveMetadataText => (EffectiveProfile?.MetadataPolicy ?? MetadataHandling.Strip) == MetadataHandling.Strip ? "Strip (Privacy)" : "Preserve supported";
    public string PresetDescriptionText => SelectedPreset?.Description ?? string.Empty;
    public string CustomQualityText => $"{CustomQuality}%";

    // ── Phase W2-F5: Format-Aware Optimization Capabilities ─────────────
    public FormatOptimizationCapabilities CurrentFormatCapabilities =>
        FormatCapabilityCatalog.GetCapabilities(SelectedTargetFormat);

    public bool IsQualityApplicable => CurrentFormatCapabilities.SupportsQuality;

    public bool IsQualityControlEnabled => IsCustomMode && CurrentFormatCapabilities.SupportsQuality;

    public bool IsDimensionControlEnabled => IsCustomMode && CurrentFormatCapabilities.SupportsMaxDimension;

    public bool IsDpiControlEnabled => IsCustomMode && CurrentFormatCapabilities.SupportsTargetDpi;

    public bool IsMetadataControlEnabled => IsCustomMode && CurrentFormatCapabilities.SupportsMetadataPolicy;

    public string QualityApplicabilityText => CurrentFormatCapabilities.QualityExplanation;

    public string DimensionExplanationText => CurrentFormatCapabilities.DimensionExplanation;

    public string DpiExplanationText => CurrentFormatCapabilities.DpiExplanation;

    public string MetadataExplanationText => CurrentFormatCapabilities.MetadataExplanation;

    public string FormatSummaryNote => CurrentFormatCapabilities.SummaryNote;

    public string PresetLockStatusText => IsCustomMode
        ? "Custom parameters unlocked for editing."
        : "Preset parameters locked to ensure canonical profile.";

    public string EffectiveQualityDisplay =>
        CurrentFormatCapabilities.SupportsQuality
            ? $"{EffectiveProfile?.Quality ?? 85}%"
            : "N/A (Lossless)";

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    private string? _rejectedFilesMessage;

    [ObservableProperty]
    private bool _hasRejectedFiles;

    public string EffectiveOutputDirectory
    {
        get
        {
            if (UseSourceDirectory)
            {
                return "Same folder as each source file";
            }

            if (!string.IsNullOrWhiteSpace(CustomOutputDirectory))
            {
                return CustomOutputDirectory;
            }

            return !string.IsNullOrWhiteSpace(_settings.DownloadDirectory)
                ? _settings.DownloadDirectory
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }

    public bool CanStart =>
        HasItems &&
        !IsProcessing &&
        Queue.Any(j => j.Job.State == ConversionJobState.Pending ||
                       j.Job.State == ConversionJobState.Queued ||
                       j.Job.State == ConversionJobState.Failed);

    public UniversalConverterViewModel(
        IConversionOrchestrator orchestrator,
        INotificationService notifications,
        IAppSettingsService settings,
        ILogger<UniversalConverterViewModel>? logger = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _orchestrator.ProgressChanged += OnOrchestratorProgressChanged;
        _orchestrator.JobStateChanged += OnOrchestratorJobStateChanged;

        UpdateEffectiveProfile();
        DiscoverSupportedFormats();
    }

    private void DiscoverSupportedFormats()
    {
        SupportedSourceFormats.Clear();
        string[] standardProbe = [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tiff", ".tif", ".pdf", ".txt", ".md", ".html"];

        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ext in standardProbe)
        {
            var targets = _orchestrator.GetSupportedTargetExtensions(ext);
            if (targets.Count > 0)
            {
                supported.Add(ext.TrimStart('.').ToUpperInvariant());
            }
        }

        foreach (var s in supported.OrderBy(x => x))
        {
            SupportedSourceFormats.Add(s);
        }

        UpdateAvailableTargetFormats();
    }

    private void UpdateAvailableTargetFormats()
    {
        var previousSelection = SelectedTargetFormat;
        AvailableTargetFormats.Clear();
        AvailableTargetFormats.Add("Auto");

        if (Queue.Count == 0)
        {
            SelectedTargetFormat = "Auto";
            return;
        }

        // Aggregate supported targets across all queued items
        var commonTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool first = true;

        foreach (var item in Queue)
        {
            var targets = _orchestrator.GetSupportedTargetExtensions(item.Job.SourceExtension);
            var set = new HashSet<string>(targets.Select(t => t.TrimStart('.').ToUpperInvariant()), StringComparer.OrdinalIgnoreCase);

            if (first)
            {
                commonTargets = set;
                first = false;
            }
            else
            {
                commonTargets.IntersectWith(set);
            }
        }

        foreach (var t in commonTargets.OrderBy(x => x))
        {
            AvailableTargetFormats.Add(t);
        }

        if (AvailableTargetFormats.Contains(previousSelection))
        {
            SelectedTargetFormat = previousSelection;
        }
        else
        {
            SelectedTargetFormat = "Auto";
        }
    }

    private static void Log(string msg)
    {
        try
        {
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] [UniversalConverterViewModel] {msg}\n");
        }
        catch { }
    }

    // ── File Intake ──────────────────────────────────────────────────────────

    [RelayCommand]
    public void AddFiles(IEnumerable<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        Log($"AddFiles called with {filePaths.Count()} paths.");

        int addedCount = 0;
        int duplicateCount = 0;
        var rejectedList = new List<string>();

        foreach (var path in filePaths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);

            // Duplicate prevention
            if (Queue.Any(j => string.Equals(j.SourceFilePath, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                duplicateCount++;
                continue;
            }

            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            var targets = _orchestrator.GetSupportedTargetExtensions(ext);

            if (targets.Count == 0)
            {
                rejectedList.Add(Path.GetFileName(fullPath));
                continue;
            }

            var fi = new FileInfo(fullPath);
            var job = new ConversionJob
            {
                SourceFilePath = fullPath,
                SourceFileSizeBytes = fi.Length,
                TargetExtension = ResolveInitialTarget(ext, targets),
                DestinationDirectory = Path.GetDirectoryName(fullPath) ?? string.Empty
            };

            var uiModel = new ConversionJobUiModel(job);
            Queue.Add(uiModel);
            addedCount++;
        }

        if (rejectedList.Count > 0)
        {
            HasRejectedFiles = true;
            RejectedFilesMessage = rejectedList.Count == 1
                ? $"'{rejectedList[0]}' has no supported conversion targets and was skipped."
                : $"{rejectedList.Count} files have no supported conversion targets and were skipped.";
        }

        UpdateAvailableTargetFormats();
        UpdateTelemetry();

        if (addedCount > 0)
        {
            string msg = addedCount == 1
                ? $"Added '{Path.GetFileName(Queue.Last().SourceFilePath)}' to conversion queue."
                : $"Added {addedCount} files to conversion queue.";

            _notifications.Show(msg, NotificationSeverity.Informational, "Universal Converter", TimeSpan.FromSeconds(3));
        }
    }

    private static string ResolveInitialTarget(string sourceExt, IReadOnlyList<string> supportedTargets)
    {
        // Sensible default targets based on format family
        return sourceExt switch
        {
            ".png" => supportedTargets.Contains(".jpg") ? ".jpg" : supportedTargets[0],
            ".jpg" or ".jpeg" => supportedTargets.Contains(".png") ? ".png" : supportedTargets[0],
            ".webp" or ".bmp" or ".tiff" or ".tif" => supportedTargets.Contains(".png") ? ".png" : supportedTargets[0],
            ".pdf" => supportedTargets.Contains(".txt") ? ".txt" : supportedTargets[0],
            ".txt" or ".md" or ".markdown" => supportedTargets.Contains(".pdf") ? ".pdf" : supportedTargets[0],
            _ => supportedTargets.Count > 0 ? supportedTargets[0] : ".pdf"
        };
    }

    [RelayCommand]
    public async Task AddFolderAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        try
        {
            var files = await Task.Run(() =>
                Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Take(500)
                    .ToList());

            AddFiles(files);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enumerate directory: {Folder}", folderPath);
            _notifications.ShowError($"Could not read folder: {ex.Message}");
        }
    }

    [RelayCommand]
    public void RemoveJob(ConversionJobUiModel? item)
    {
        if (item == null) return;

        if (item.CanCancel)
        {
            _orchestrator.CancelJob(item.JobId);
        }

        Queue.Remove(item);
        item.Dispose();
        UpdateAvailableTargetFormats();
        UpdateTelemetry();
    }

    [RelayCommand]
    public void ClearQueue()
    {
        if (IsProcessing)
        {
            _orchestrator.CancelAll();
        }

        foreach (var item in Queue)
        {
            item.Dispose();
        }

        Queue.Clear();
        _orchestrator.ClearQueue();
        QueueOutputVsInputText = "0 B";
        QueueSavingsFormatted = "—";
        QueueThroughputFormatted = "—";
        QueueEtaFormatted = "—";
        HasQueueTelemetry = false;
        PendingJobsCount = 0;
        RunningJobsCount = 0;
        CompletedJobsCount = 0;
        FailedJobsCount = 0;
        UpdateAvailableTargetFormats();
        UpdateTelemetry();
        ProgressStatusText = "Queue cleared.";
        OverallProgress = 0;
    }

    [RelayCommand]
    public void ClearCompleted()
    {
        var finished = Queue
            .Where(j => j.Job.State == ConversionJobState.Succeeded ||
                        j.Job.State == ConversionJobState.Cancelled ||
                        j.Job.State == ConversionJobState.Skipped)
            .ToList();

        foreach (var f in finished)
        {
            Queue.Remove(f);
            f.Dispose();
        }

        UpdateAvailableTargetFormats();
        UpdateTelemetry();
    }

    // ── Queue Execution & Control ──────────────────────────────────────────

    [RelayCommand]
    public async Task StartConversionAsync()
    {
        Log($"StartConversionAsync called. CanStart={CanStart}, HasItems={HasItems}, IsProcessing={IsProcessing}, QueueCount={Queue.Count}");
        if (!CanStart)
        {
            Log("StartConversionAsync: CanStart is false. Aborting.");
            return;
        }

        var pendingJobs = Queue
            .Where(j => j.Job.State == ConversionJobState.Pending ||
                        j.Job.State == ConversionJobState.Queued ||
                        j.Job.State == ConversionJobState.Failed)
            .ToList();

        Log($"StartConversionAsync: Found {pendingJobs.Count} pending jobs.");
        if (pendingJobs.Count == 0) return;

        IsProcessing = true;
        IsPaused = false;
        ProgressStatusText = $"Starting conversion of {pendingJobs.Count} file(s)…";

        string? targetDir = UseSourceDirectory ? null : EffectiveOutputDirectory;
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            try
            {
                Directory.CreateDirectory(targetDir);
            }
            catch (Exception ex)
            {
                _notifications.ShowError($"Could not create destination directory: {ex.Message}");
                IsProcessing = false;
                return;
            }
        }

        // Apply profile & configuration to each pending job
        UpdateEffectiveProfile();
        var profileToApply = EffectiveProfile;

        foreach (var item in pendingJobs)
        {
            var job = item.Job;

            // Apply selected target format if not Auto
            if (SelectedTargetFormat != "Auto")
            {
                var formattedExt = "." + SelectedTargetFormat.ToLowerInvariant();
                if (_orchestrator.CanConvert(job.SourceExtension, formattedExt))
                {
                    job.TargetExtension = formattedExt;
                }
            }

            if (!string.IsNullOrEmpty(targetDir))
            {
                job.DestinationDirectory = targetDir;
            }
            else if (string.IsNullOrWhiteSpace(job.DestinationDirectory))
            {
                job.DestinationDirectory = Path.GetDirectoryName(job.SourceFilePath) ?? Environment.CurrentDirectory;
            }

            job.Profile = profileToApply;

            // Reset state if retrying
            if (job.State == ConversionJobState.Failed)
            {
                job.TryTransitionTo(ConversionJobState.Queued);
            }

            _orchestrator.Enqueue(job);
            item.Refresh();
        }

        try
        {
            await _orchestrator.StartAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Orchestrator StartAsync failed.");
            _notifications.ShowError($"Failed to start conversions: {ex.Message}");
            IsProcessing = false;
        }
    }

    [RelayCommand]
    public async Task PauseQueueAsync()
    {
        await _orchestrator.PauseAsync();
        IsPaused = true;
        ProgressStatusText = "Queue paused — current conversion continues.";
    }

    [RelayCommand]
    public async Task ResumeQueueAsync()
    {
        await _orchestrator.ResumeAsync();
        IsPaused = false;
        ProgressStatusText = "Resuming queued conversions…";
    }

    [RelayCommand]
    public void CancelJob(string? jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId)) return;
        _orchestrator.CancelJob(jobId);
    }

    [RelayCommand]
    public void CancelAll()
    {
        _orchestrator.CancelAll();
        ProgressStatusText = "Cancelling all jobs…";
    }

    [RelayCommand]
    public async Task RetryJobAsync(ConversionJobUiModel? item)
    {
        if (item == null || !item.CanRetry) return;

        var job = item.Job;
        job.TryTransitionTo(ConversionJobState.Queued);
        item.Refresh();

        _orchestrator.Enqueue(job);
        IsProcessing = true;
        await _orchestrator.StartAsync();
    }

    // ── Output Location Helpers ──────────────────────────────────────────────

    [RelayCommand]
    public void OpenOutputFolder()
    {
        string? targetFolder = null;

        if (!UseSourceDirectory && !string.IsNullOrWhiteSpace(CustomOutputDirectory) && Directory.Exists(CustomOutputDirectory))
        {
            targetFolder = CustomOutputDirectory;
        }
        else
        {
            var firstSucceeded = Queue.FirstOrDefault(j => j.Job.State == ConversionJobState.Succeeded && !string.IsNullOrWhiteSpace(j.OutputFilePath));
            if (firstSucceeded != null && File.Exists(firstSucceeded.OutputFilePath))
            {
                targetFolder = Path.GetDirectoryName(firstSucceeded.OutputFilePath);
            }
            else if (Queue.Count > 0 && File.Exists(Queue[0].SourceFilePath))
            {
                targetFolder = Path.GetDirectoryName(Queue[0].SourceFilePath);
            }
        }

        if (string.IsNullOrEmpty(targetFolder) || !Directory.Exists(targetFolder))
        {
            targetFolder = _settings.DownloadDirectory;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{targetFolder}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to open explorer for {Folder}", targetFolder);
            _notifications.ShowWarning($"Could not open folder: {ex.Message}");
        }
    }

    [RelayCommand]
    public void OpenOutputFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            _notifications.ShowWarning("The specified output file does not exist on disk.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to highlight file {File}", filePath);
        }
    }

    public void SetCustomOutputDirectory(string folderPath)
    {
        CustomOutputDirectory = folderPath;
        UseSourceDirectory = false;
        OnPropertyChanged(nameof(EffectiveOutputDirectory));
    }

    public void DismissRejectedMessage()
    {
        HasRejectedFiles = false;
        RejectedFilesMessage = null;
    }

    // ── Telemetry & Events ───────────────────────────────────────────────────

    private void OnOrchestratorProgressChanged(object? sender, QueueProgressReport report)
    {
        RunOnDispatcher(() =>
        {
            OverallProgress = report.OverallProgressPercentage;
            TotalBytesFormatted = FormatBytes(report.TotalBytesProcessed);
            CurrentJobText = !string.IsNullOrWhiteSpace(report.CurrentJobName)
                ? $"Converting {report.CurrentJobName}"
                : string.Empty;

            if (report.IsCompleted)
            {
                IsProcessing = false;
                IsPaused = false;
                int succeeded = Queue.Count(j => j.Job.State == ConversionJobState.Succeeded);
                int failed = Queue.Count(j => j.Job.State == ConversionJobState.Failed);
                int skipped = Queue.Count(j => j.Job.State == ConversionJobState.Skipped);

                ProgressStatusText = failed > 0
                    ? $"Batch complete · {succeeded} converted, {failed} failed, {skipped} skipped."
                    : $"Conversion complete · {succeeded} of {report.TotalJobs} files converted.";

                if (failed > 0)
                {
                    _notifications.ShowWarning(ProgressStatusText, "Universal Converter");
                }
                else if (succeeded > 0)
                {
                    _notifications.ShowSuccess(ProgressStatusText, "Universal Converter");
                }
            }
            else if (IsPaused)
            {
                ProgressStatusText = "Queue paused — current conversion continues.";
            }
            else
            {
                ProgressStatusText = $"{report.FinishedJobs} of {report.TotalJobs} completed · {report.RunningJobs} converting";
            }

            PendingJobsCount = report.PendingJobs;
            RunningJobsCount = report.RunningJobs;
            CompletedJobsCount = report.CompletedJobs;
            FailedJobsCount = report.FailedJobs;

            if (report.CompletedJobs > 0)
            {
                QueueOutputVsInputText = $"{FormatBytes(report.TotalOutputBytes)} (from {FormatBytes(report.TotalInputBytes)})";
                QueueSavingsFormatted = ConversionTelemetry.FormatAggregateSavings(report.TotalInputBytes, report.TotalOutputBytes, report.CompletedJobs);
            }
            else
            {
                QueueOutputVsInputText = $"{FormatBytes(report.TotalBytesProcessed)} processed";
                QueueSavingsFormatted = "—";
            }

            QueueThroughputFormatted = ConversionTelemetry.FormatThroughput(report.ThroughputBytesPerSecond);
            QueueEtaFormatted = report.IsCompleted ? "Complete" : ConversionTelemetry.FormatEta(report.EstimatedRemaining);
            HasQueueTelemetry = Queue.Count > 0 && (report.CompletedJobs > 0 || IsProcessing);

            UpdateTelemetry();
        });
    }

    private void OnOrchestratorJobStateChanged(object? sender, ConversionJob job)
    {
        RunOnDispatcher(() =>
        {
            var match = Queue.FirstOrDefault(j => j.JobId == job.JobId);
            match?.Refresh();
            UpdateTelemetry();
        });
    }

    private void UpdateTelemetry()
    {
        HasItems = Queue.Count > 0;
        int completedCount = Queue.Count(j => j.Job.State == ConversionJobState.Succeeded);
        CompletedJobsCount = completedCount;
        FailedJobsCount = Queue.Count(j => j.Job.State == ConversionJobState.Failed);
        RunningJobsCount = Queue.Count(j => j.Job.State == ConversionJobState.Running || j.Job.State == ConversionJobState.Cancelling);
        PendingJobsCount = Queue.Count(j => j.Job.State == ConversionJobState.Pending || j.Job.State == ConversionJobState.Queued || j.Job.State == ConversionJobState.Validating);
        HasCompletedItems = Queue.Any(j => j.Job.State == ConversionJobState.Succeeded ||
                                           j.Job.State == ConversionJobState.Cancelled ||
                                           j.Job.State == ConversionJobState.Skipped);
        HasFailedItems = Queue.Any(j => j.Job.State == ConversionJobState.Failed);
        HasQueueTelemetry = Queue.Count > 0 && (completedCount > 0 || IsProcessing);
        OnPropertyChanged(nameof(CanStart));
        StartConversionCommand.NotifyCanExecuteChanged();
        Log($"UpdateTelemetry: HasItems={HasItems}, HasCompletedItems={HasCompletedItems}, HasFailedItems={HasFailedItems}, CanStart={CanStart}, QueueCount={Queue.Count}");
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        <= 0 => "0 B",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F2} MB",
        _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };

    private void RunOnDispatcher(Action action)
    {
        var dq = _dispatcher ?? App.MainAppWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (dq != null && !dq.HasThreadAccess)
        {
            dq.TryEnqueue(DispatcherQueuePriority.Normal, () => action());
        }
        else
        {
            action();
        }
    }

    // ── Phase W2-F3: Preset Selection & Custom Parameter Synchronization ────

    public void SelectPreset(OptimizationPresetId id)
    {
        var match = OptimizationPresetCatalog.GetPreset(id);
        SelectedPreset = match;
    }

    partial void OnSelectedTargetFormatChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentFormatCapabilities));
        OnPropertyChanged(nameof(IsQualityApplicable));
        OnPropertyChanged(nameof(IsQualityControlEnabled));
        OnPropertyChanged(nameof(IsDimensionControlEnabled));
        OnPropertyChanged(nameof(IsDpiControlEnabled));
        OnPropertyChanged(nameof(IsMetadataControlEnabled));
        OnPropertyChanged(nameof(QualityApplicabilityText));
        OnPropertyChanged(nameof(DimensionExplanationText));
        OnPropertyChanged(nameof(DpiExplanationText));
        OnPropertyChanged(nameof(MetadataExplanationText));
        OnPropertyChanged(nameof(FormatSummaryNote));
        OnPropertyChanged(nameof(EffectiveQualityDisplay));
    }

    partial void OnSelectedPresetChanged(OptimizationPreset value)
    {
        if (value == null) return;

        if (value.Id == OptimizationPresetId.Custom)
        {
            IsCustomMode = true;
        }
        else
        {
            IsCustomMode = false;
            // Reflect canonical values into custom editor properties without triggering custom mode
            _customQuality = value.Quality;
            _customMaxDimension = value.MaxDimension;
            _customTargetDpi = value.TargetDpi;
            _customMetadataPolicy = value.MetadataPolicy;

            OnPropertyChanged(nameof(CustomQuality));
            OnPropertyChanged(nameof(CustomQualityText));
            OnPropertyChanged(nameof(CustomMaxDimension));
            OnPropertyChanged(nameof(CustomTargetDpi));
            OnPropertyChanged(nameof(CustomMetadataPolicy));

            SyncOptionSelections();
        }

        OnPropertyChanged(nameof(PresetLockStatusText));
        OnPropertyChanged(nameof(IsQualityControlEnabled));
        OnPropertyChanged(nameof(IsDimensionControlEnabled));
        OnPropertyChanged(nameof(IsDpiControlEnabled));
        OnPropertyChanged(nameof(IsMetadataControlEnabled));

        UpdateEffectiveProfile();
    }

    partial void OnCustomQualityChanged(int value)
    {
        if (SelectedPreset?.Id != OptimizationPresetId.Custom)
        {
            SelectedPreset = OptimizationPresetCatalog.Custom;
        }
        else
        {
            UpdateEffectiveProfile();
        }
    }

    partial void OnCustomMaxDimensionChanged(int value)
    {
        if (SelectedPreset?.Id != OptimizationPresetId.Custom)
        {
            SelectedPreset = OptimizationPresetCatalog.Custom;
        }
        else
        {
            SyncOptionSelections();
            UpdateEffectiveProfile();
        }
    }

    partial void OnCustomTargetDpiChanged(int value)
    {
        if (SelectedPreset?.Id != OptimizationPresetId.Custom)
        {
            SelectedPreset = OptimizationPresetCatalog.Custom;
        }
        else
        {
            SyncOptionSelections();
            UpdateEffectiveProfile();
        }
    }

    partial void OnCustomMetadataPolicyChanged(MetadataHandling value)
    {
        if (SelectedPreset?.Id != OptimizationPresetId.Custom)
        {
            SelectedPreset = OptimizationPresetCatalog.Custom;
        }
        else
        {
            SyncOptionSelections();
            UpdateEffectiveProfile();
        }
    }

    partial void OnSelectedDimensionOptionChanged(DimensionOption value)
    {
        if (value != null && IsCustomMode && CustomMaxDimension != value.MaxDimension)
        {
            CustomMaxDimension = value.MaxDimension;
        }
    }

    partial void OnSelectedDpiOptionChanged(DpiOption value)
    {
        if (value != null && IsCustomMode && CustomTargetDpi != value.Dpi)
        {
            CustomTargetDpi = value.Dpi;
        }
    }

    partial void OnSelectedMetadataOptionChanged(MetadataOption value)
    {
        if (value != null && IsCustomMode && CustomMetadataPolicy != value.Policy)
        {
            CustomMetadataPolicy = value.Policy;
        }
    }

    partial void OnSelectedCollisionPolicyIndexChanged(int value)
    {
        UpdateEffectiveProfile();
    }

    partial void OnStripMetadataChanged(bool value)
    {
        if (SelectedPreset?.Id != OptimizationPresetId.Custom)
        {
            SelectedPreset = OptimizationPresetCatalog.Custom;
        }
        CustomMetadataPolicy = value ? MetadataHandling.Strip : MetadataHandling.Preserve;
    }

    partial void OnJpegQualityChanged(int value)
    {
        if (SelectedPreset?.Id != OptimizationPresetId.Custom)
        {
            SelectedPreset = OptimizationPresetCatalog.Custom;
        }
        CustomQuality = value;
    }

    partial void OnTargetDpiChanged(int value)
    {
        if (SelectedPreset?.Id != OptimizationPresetId.Custom)
        {
            SelectedPreset = OptimizationPresetCatalog.Custom;
        }
        CustomTargetDpi = value;
    }

    private void SyncOptionSelections()
    {
#pragma warning disable MVVMTK0034
        _selectedDimensionOption = MaxDimensionOptions.FirstOrDefault(o => o.MaxDimension == CustomMaxDimension)
                                   ?? MaxDimensionOptions[0];
        _selectedDpiOption = TargetDpiOptions.FirstOrDefault(o => o.Dpi == CustomTargetDpi)
                             ?? TargetDpiOptions[2];
        _selectedMetadataOption = MetadataOptions.FirstOrDefault(o => o.Policy == CustomMetadataPolicy)
                                  ?? MetadataOptions[0];
#pragma warning restore MVVMTK0034

        OnPropertyChanged(nameof(SelectedDimensionOption));
        OnPropertyChanged(nameof(SelectedDpiOption));
        OnPropertyChanged(nameof(SelectedMetadataOption));
    }

    private void UpdateEffectiveProfile()
    {
        var collisionPolicy = SelectedCollisionPolicyIndex switch
        {
            1 => CollisionPolicy.Overwrite,
            2 => CollisionPolicy.Skip,
            _ => CollisionPolicy.AutoRename
        };

        if (SelectedPreset?.Id == OptimizationPresetId.Custom)
        {
            EffectiveProfile = new ConversionProfile
            {
                Name = "Custom",
                Quality = OptimizationValidation.NormalizeQuality(CustomQuality),
                MaxDimension = OptimizationValidation.NormalizeMaxDimension(CustomMaxDimension),
                TargetDpi = OptimizationValidation.NormalizeTargetDpi(CustomTargetDpi),
                MetadataPolicy = OptimizationValidation.NormalizeMetadataPolicy(CustomMetadataPolicy),
                CollisionMode = collisionPolicy
            };
        }
        else
        {
            var preset = SelectedPreset ?? OptimizationPresetCatalog.Default;
            EffectiveProfile = preset.ToProfile(collisionPolicy);
        }

#pragma warning disable MVVMTK0034
        _jpegQuality = EffectiveProfile.Quality;
        _targetDpi = EffectiveProfile.TargetDpi;
        _stripMetadata = EffectiveProfile.MetadataPolicy == MetadataHandling.Strip;
#pragma warning restore MVVMTK0034

        OnPropertyChanged(nameof(EffectiveProfile));
        OnPropertyChanged(nameof(EffectiveQualityText));
        OnPropertyChanged(nameof(EffectiveQualityDisplay));
        OnPropertyChanged(nameof(EffectiveMaxDimensionText));
        OnPropertyChanged(nameof(EffectiveTargetDpiText));
        OnPropertyChanged(nameof(EffectiveMetadataText));
        OnPropertyChanged(nameof(PresetDescriptionText));
        OnPropertyChanged(nameof(JpegQuality));
        OnPropertyChanged(nameof(TargetDpi));
        OnPropertyChanged(nameof(StripMetadata));
    }

    public void Dispose()
    {
        _orchestrator.ProgressChanged -= OnOrchestratorProgressChanged;
        _orchestrator.JobStateChanged -= OnOrchestratorJobStateChanged;

        foreach (var item in Queue)
        {
            item.Dispose();
        }
        Queue.Clear();
    }
}

public sealed record DimensionOption(int MaxDimension, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed record DpiOption(int Dpi, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed record MetadataOption(MetadataHandling Policy, string DisplayName)
{
    public override string ToString() => DisplayName;
}
