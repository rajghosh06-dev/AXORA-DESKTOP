using System;

namespace Axora.Desktop.Models;

/// <summary>
/// Domain model and calculation engine for conversion telemetry.
/// Provides pure mathematical formulas for size delta, savings percentage,
/// throughput rate, ETA projection, and localized presentation formatting.
/// </summary>
public static class ConversionTelemetry
{
    /// <summary>
    /// Calculates the size delta in bytes between source and output (Source - Output).
    /// Positive indicates bytes saved; negative indicates file size expansion.
    /// Returns null if output size is not available.
    /// </summary>
    public static long? CalculateSizeDelta(long sourceBytes, long? outputBytes)
    {
        if (!outputBytes.HasValue || outputBytes.Value < 0)
        {
            return null;
        }

        return sourceBytes - outputBytes.Value;
    }

    /// <summary>
    /// Calculates savings percentage: ((Source - Output) / Source) * 100.
    /// Returns null if sourceBytes &lt;= 0 or outputBytes is unavailable/negative.
    /// Negative savings are preserved without clamping (e.g. -20% means output grew by 20%).
    /// </summary>
    public static double? CalculateSavingsPercentage(long sourceBytes, long? outputBytes)
    {
        if (sourceBytes <= 0 || !outputBytes.HasValue || outputBytes.Value < 0)
        {
            return null;
        }

        return ((double)(sourceBytes - outputBytes.Value) / sourceBytes) * 100.0;
    }

    /// <summary>
    /// Calculates processing rate in bytes per second: bytesProcessed / elapsedSeconds.
    /// Returns null if bytesProcessed &lt;= 0, elapsed &lt;= Zero, or elapsed &lt; 1ms (to guard against division-by-zero or sub-millisecond spikes).
    /// </summary>
    public static double? CalculateThroughput(long bytesProcessed, TimeSpan elapsed)
    {
        if (bytesProcessed <= 0 || elapsed <= TimeSpan.Zero || elapsed.TotalSeconds < 0.001)
        {
            return null;
        }

        return (double)bytesProcessed / elapsed.TotalSeconds;
    }

    /// <summary>
    /// Calculates conservative estimated remaining duration (ETA) based on average elapsed time per completed job.
    /// Returns null if completedJobs &lt;= 0, remainingJobs &lt;= 0, or completedElapsedTotal &lt;= Zero.
    /// </summary>
    public static TimeSpan? CalculateEta(int completedJobs, TimeSpan completedElapsedTotal, int remainingJobs)
    {
        if (completedJobs <= 0 || remainingJobs <= 0 || completedElapsedTotal <= TimeSpan.Zero)
        {
            return null;
        }

        double avgSecondsPerJob = completedElapsedTotal.TotalSeconds / completedJobs;
        return TimeSpan.FromSeconds(avgSecondsPerJob * remainingJobs);
    }

    /// <summary>
    /// Formats an elapsed duration into user-friendly text (e.g. "124 ms", "1.2 s", "2m 14s").
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        if (duration < TimeSpan.FromSeconds(1))
        {
            return $"{Math.Max(1, (int)duration.TotalMilliseconds)} ms";
        }

        if (duration < TimeSpan.FromMinutes(1))
        {
            return $"{duration.TotalSeconds:F1} s";
        }

        int mins = (int)duration.TotalMinutes;
        int secs = duration.Seconds;
        return $"{mins}m {secs}s";
    }

    /// <summary>
    /// Formats a byte quantity using binary standard prefixes (B, KB, MB, GB).
    /// </summary>
    public static string FormatBytes(long bytes) => bytes switch
    {
        <= 0 => "0 B",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F2} MB",
        _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };

    /// <summary>
    /// Formats throughput rate into standard text (e.g. "4.2 MB/s", "850 KB/s").
    /// </summary>
    public static string FormatThroughput(double? bytesPerSec)
    {
        if (!bytesPerSec.HasValue || bytesPerSec.Value <= 0 || double.IsNaN(bytesPerSec.Value) || double.IsInfinity(bytesPerSec.Value))
        {
            return "—";
        }

        double rate = bytesPerSec.Value;
        return rate switch
        {
            < 1024 => $"{rate:F0} B/s",
            < 1024 * 1024 => $"{rate / 1024.0:F1} KB/s",
            < 1024 * 1024 * 1024 => $"{rate / (1024.0 * 1024.0):F1} MB/s",
            _ => $"{rate / (1024.0 * 1024.0 * 1024.0):F2} GB/s"
        };
    }

    /// <summary>
    /// Formats savings / size delta text for a single completed job (e.g. "Saved 14 KB (45.2%)" or "Increased by 5 KB (-12.5%)").
    /// </summary>
    public static string FormatSavings(long sourceBytes, long? outputBytes, ConversionJobState state = ConversionJobState.Succeeded)
    {
        if (state != ConversionJobState.Succeeded || !outputBytes.HasValue || outputBytes.Value < 0)
        {
            return "—";
        }

        var pct = CalculateSavingsPercentage(sourceBytes, outputBytes);
        if (!pct.HasValue)
        {
            return "—";
        }

        long delta = sourceBytes - outputBytes.Value;
        if (delta > 0)
        {
            return $"Saved {FormatBytes(delta)} ({pct.Value:F1}%)";
        }

        if (delta < 0)
        {
            return $"Increased by {FormatBytes(-delta)} ({pct.Value:F1}%)";
        }

        return "No size change (0.0%)";
    }

    /// <summary>
    /// Formats aggregate savings text across the queue (e.g. "Saved 245 KB (32.1%)" or "Increased by 12 KB (-8.4%)").
    /// </summary>
    public static string FormatAggregateSavings(long totalInputBytes, long totalOutputBytes, int completedJobs)
    {
        if (completedJobs <= 0 || totalInputBytes <= 0)
        {
            return "—";
        }

        var pct = CalculateSavingsPercentage(totalInputBytes, totalOutputBytes);
        if (!pct.HasValue)
        {
            return "—";
        }

        long delta = totalInputBytes - totalOutputBytes;
        if (delta > 0)
        {
            return $"Saved {FormatBytes(delta)} ({pct.Value:F1}%)";
        }

        if (delta < 0)
        {
            return $"Increased by {FormatBytes(-delta)} ({pct.Value:F1}%)";
        }

        return "No size change (0.0%)";
    }

    /// <summary>
    /// Formats estimated remaining time (ETA) for display.
    /// </summary>
    public static string FormatEta(TimeSpan? eta)
    {
        if (!eta.HasValue)
        {
            return "—";
        }

        return $"~{FormatDuration(eta.Value)}";
    }
}

/// <summary>
/// Authoritative telemetry snapshot for a single conversion job.
/// </summary>
public sealed record JobTelemetry
{
    public string JobId { get; init; } = string.Empty;
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public TimeSpan ElapsedTime { get; init; } = TimeSpan.Zero;
    public long SourceSizeBytes { get; init; }
    public long? OutputSizeBytes { get; init; }
    public long? SizeDeltaBytes => ConversionTelemetry.CalculateSizeDelta(SourceSizeBytes, OutputSizeBytes);
    public double? SavingsPercentage => ConversionTelemetry.CalculateSavingsPercentage(SourceSizeBytes, OutputSizeBytes);
    public double? ThroughputBytesPerSecond => ConversionTelemetry.CalculateThroughput(SourceSizeBytes, ElapsedTime);

    public string FormattedElapsed => ConversionTelemetry.FormatDuration(ElapsedTime);
    public string FormattedThroughput => ConversionTelemetry.FormatThroughput(ThroughputBytesPerSecond);
    public string FormattedSavings => ConversionTelemetry.FormatSavings(SourceSizeBytes, OutputSizeBytes);
}

/// <summary>
/// Authoritative telemetry snapshot for the active batch conversion queue.
/// </summary>
public sealed record QueueTelemetry
{
    public int TotalJobs { get; init; }
    public int PendingJobs { get; init; }
    public int RunningJobs { get; init; }
    public int CompletedJobs { get; init; }
    public int FailedJobs { get; init; }
    public int CancelledJobs { get; init; }
    public int SkippedJobs { get; init; }

    public long TotalInputBytes { get; init; }
    public long TotalOutputBytes { get; init; }
    public long TotalBytesProcessed { get; init; }
    public TimeSpan BatchElapsedTime { get; init; } = TimeSpan.Zero;

    public long? AggregateSizeDeltaBytes => CompletedJobs > 0 ? TotalInputBytes - TotalOutputBytes : null;
    public double? AggregateSavingsPercentage => ConversionTelemetry.CalculateSavingsPercentage(TotalInputBytes, CompletedJobs > 0 ? TotalOutputBytes : null);
    public double? ThroughputBytesPerSecond => ConversionTelemetry.CalculateThroughput(TotalBytesProcessed, BatchElapsedTime);
    public TimeSpan? EstimatedRemaining { get; init; }

    public string FormattedTotalInput => ConversionTelemetry.FormatBytes(TotalInputBytes);
    public string FormattedTotalOutput => ConversionTelemetry.FormatBytes(TotalOutputBytes);
    public string FormattedAggregateSavings => ConversionTelemetry.FormatAggregateSavings(TotalInputBytes, TotalOutputBytes, CompletedJobs);
    public string FormattedThroughput => ConversionTelemetry.FormatThroughput(ThroughputBytesPerSecond);
    public string FormattedEstimatedRemaining => ConversionTelemetry.FormatEta(EstimatedRemaining);
}
