using System;
using System.IO;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axora.Desktop.Models;

/// <summary>
/// Domain model representing a single file conversion work item in the Universal Converter.
/// Strictly non-destructive: holds file references, configuration, and execution telemetry without performing I/O.
/// </summary>
public sealed partial class ConversionJob : ObservableObject, IDisposable
{
    private CancellationTokenSource? _cts = new();

    public string JobId { get; init; } = Guid.NewGuid().ToString("N");

    public string SourceFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Optional sequence of source files to combine into a single output document (e.g. Image Sequence -> PDF).
    /// When populated, takes precedence over SourceFilePath.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<string>? _sourceFileSequence;

    public string SourceFileName => !string.IsNullOrWhiteSpace(SourceFilePath)
        ? Path.GetFileName(SourceFilePath)
        : string.Empty;

    public string SourceExtension => !string.IsNullOrWhiteSpace(SourceFilePath)
        ? Path.GetExtension(SourceFilePath).ToLowerInvariant()
        : string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedSourceSize))]
    private long _sourceFileSizeBytes;

    [ObservableProperty]
    private string _targetExtension = string.Empty;

    [ObservableProperty]
    private string _destinationDirectory = string.Empty;

    [ObservableProperty]
    private string _outputFilePath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedStatus))]
    private ConversionJobStatus _status = ConversionJobStatus.Queued;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _diagnosticDetails;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedOutputSize))]
    private long _outputSizeBytes;

    [ObservableProperty]
    private TimeSpan _elapsedTime = TimeSpan.Zero;

    [ObservableProperty]
    private ConversionProfile _profile = ConversionProfile.Default;

    [ObservableProperty]
    private ConversionJobState _state = ConversionJobState.Pending;

    [ObservableProperty]
    private DateTimeOffset? _queuedAt;

    [ObservableProperty]
    private DateTimeOffset? _startedAt;

    [ObservableProperty]
    private DateTimeOffset? _completedAt;

    [ObservableProperty]
    private DateTimeOffset? _failedAt;

    [ObservableProperty]
    private DateTimeOffset? _cancelledAt;

    [ObservableProperty]
    private int _retryAttempt;

    private readonly object _stateLock = new();

    public bool IsCancellationRequested => _cts?.IsCancellationRequested ?? true;

    public CancellationToken CancellationToken => _cts?.Token ?? CancellationToken.None;

    public void SetCancellationTokenSource(CancellationTokenSource cts)
    {
        ArgumentNullException.ThrowIfNull(cts);
        _cts = cts;
    }

    public bool CanTransitionTo(ConversionJobState targetState)
    {
        lock (_stateLock)
        {
            return IsValidTransition(State, targetState);
        }
    }

    public bool TryTransitionTo(ConversionJobState newState)
    {
        lock (_stateLock)
        {
            if (!IsValidTransition(State, newState))
            {
                return false;
            }

            State = newState;
            SyncStatusAndTimestamps(newState);
            return true;
        }
    }

    public void TransitionTo(ConversionJobState newState)
    {
        if (!TryTransitionTo(newState))
        {
            throw new InvalidOperationException($"Invalid conversion job state transition from {State} to {newState}.");
        }
    }

    private static bool IsValidTransition(ConversionJobState current, ConversionJobState next) => (current, next) switch
    {
        (ConversionJobState.Pending, ConversionJobState.Validating) => true,
        (ConversionJobState.Pending, ConversionJobState.Cancelled) => true,

        (ConversionJobState.Validating, ConversionJobState.Queued) => true,
        (ConversionJobState.Validating, ConversionJobState.Failed) => true,
        (ConversionJobState.Validating, ConversionJobState.Skipped) => true,
        (ConversionJobState.Validating, ConversionJobState.Cancelled) => true,

        (ConversionJobState.Queued, ConversionJobState.Running) => true,
        (ConversionJobState.Queued, ConversionJobState.Cancelled) => true,

        (ConversionJobState.Running, ConversionJobState.Succeeded) => true,
        (ConversionJobState.Running, ConversionJobState.Failed) => true,
        (ConversionJobState.Running, ConversionJobState.Cancelling) => true,
        (ConversionJobState.Running, ConversionJobState.Cancelled) => true,

        (ConversionJobState.Cancelling, ConversionJobState.Cancelled) => true,
        (ConversionJobState.Cancelling, ConversionJobState.Failed) => true,

        // Retry path: Failed -> Queued
        (ConversionJobState.Failed, ConversionJobState.Queued) => true,

        _ => false
    };

    private void SyncStatusAndTimestamps(ConversionJobState state)
    {
        var now = DateTimeOffset.UtcNow;
        switch (state)
        {
            case ConversionJobState.Pending:
                Status = ConversionJobStatus.Queued;
                break;
            case ConversionJobState.Validating:
                Status = ConversionJobStatus.Preparing;
                break;
            case ConversionJobState.Queued:
                Status = ConversionJobStatus.Queued;
                QueuedAt ??= now;
                break;
            case ConversionJobState.Running:
                Status = ConversionJobStatus.Running;
                StartedAt ??= now;
                break;
            case ConversionJobState.Cancelling:
                break;
            case ConversionJobState.Cancelled:
                Status = ConversionJobStatus.Cancelled;
                CancelledAt = now;
                break;
            case ConversionJobState.Succeeded:
                Status = ConversionJobStatus.Completed;
                CompletedAt = now;
                break;
            case ConversionJobState.Failed:
                Status = ConversionJobStatus.Failed;
                FailedAt = now;
                break;
            case ConversionJobState.Skipped:
                Status = ConversionJobStatus.Skipped;
                CompletedAt = now;
                break;
        }
    }

    public void Cancel()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            try { _cts.Cancel(); } catch { }
        }

        lock (_stateLock)
        {
            if (State == ConversionJobState.Running)
            {
                TryTransitionTo(ConversionJobState.Cancelling);
            }
            else if (State == ConversionJobState.Pending || State == ConversionJobState.Validating || State == ConversionJobState.Queued)
            {
                TryTransitionTo(ConversionJobState.Cancelled);
            }
            else
            {
                Status = ConversionJobStatus.Cancelled;
            }
        }
    }

    public string FormattedSourceSize => FormatBytes(SourceFileSizeBytes);
    public string FormattedOutputSize => FormatBytes(OutputSizeBytes);

    public string FormattedStatus => Status switch
    {
        ConversionJobStatus.Queued => "Queued",
        ConversionJobStatus.Preparing => "Preparing…",
        ConversionJobStatus.Running => $"Converting ({ProgressPercentage:F0}%)",
        ConversionJobStatus.Completed => "Completed",
        ConversionJobStatus.Failed => "Failed",
        ConversionJobStatus.Cancelled => "Cancelled",
        ConversionJobStatus.Skipped => "Skipped",
        _ => Status.ToString()
    };

    private static string FormatBytes(long bytes) => bytes switch
    {
        <= 0 => "0 B",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F2} MB",
        _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };

    public void Dispose()
    {
        _cts?.Dispose();
        _cts = null;
    }
}
