namespace Axora.Desktop.Models;

/// <summary>
/// Aggregate progress telemetry snapshot for the conversion queue.
/// </summary>
public sealed record QueueProgressReport
{
    public int TotalJobs { get; init; }
    public int CompletedJobs { get; init; }
    public int FailedJobs { get; init; }
    public int CancelledJobs { get; init; }
    public int SkippedJobs { get; init; }
    public int RunningJobs { get; init; }
    public int PendingJobs { get; init; }
    public double OverallProgressPercentage { get; init; }
    public long TotalBytesProcessed { get; init; }
    public long TotalInputBytes { get; init; }
    public long TotalOutputBytes { get; init; }
    public TimeSpan TotalElapsedTime { get; init; } = TimeSpan.Zero;
    public TimeSpan? EstimatedRemaining { get; init; }
    public double? ThroughputBytesPerSecond { get; init; }
    public double? SavingsPercentage { get; init; }
    public long? SizeDeltaBytes { get; init; }
    public QueueTelemetry? Telemetry { get; init; }
    public string? CurrentJobName { get; init; }

    public int FinishedJobs => CompletedJobs + FailedJobs + CancelledJobs + SkippedJobs;
    public bool IsCompleted => TotalJobs > 0 && FinishedJobs >= TotalJobs;
    public bool HasFailures => FailedJobs > 0;
}
