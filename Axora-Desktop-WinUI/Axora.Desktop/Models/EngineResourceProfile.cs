using System;

namespace Axora.Desktop.Models;

/// <summary>
/// Defines the concurrency limits and memory requirements for an engine.
/// Used by the orchestrator to schedule safe, bounded parallelism.
/// </summary>
public sealed record EngineResourceProfile
{
    public EngineExecutionAffinity Affinity { get; init; } = EngineExecutionAffinity.CpuBound;
    public int MaxConcurrentJobs { get; init; } = Math.Clamp(Environment.ProcessorCount, 2, 8);
    public long EstimatedMemoryPerJobBytes { get; init; } = 64 * 1024 * 1024; // 64 MB default

    public static EngineResourceProfile CpuBoundDefault => new()
    {
        Affinity = EngineExecutionAffinity.CpuBound,
        MaxConcurrentJobs = Math.Clamp(Environment.ProcessorCount, 2, 8),
        EstimatedMemoryPerJobBytes = 64 * 1024 * 1024
    };

    public static EngineResourceProfile MemoryBoundHeavy => new()
    {
        Affinity = EngineExecutionAffinity.MemoryBound,
        MaxConcurrentJobs = 2,
        EstimatedMemoryPerJobBytes = 256 * 1024 * 1024 // 256 MB
    };

    public static EngineResourceProfile ProcessBoundDefault => new()
    {
        Affinity = EngineExecutionAffinity.ProcessBound,
        MaxConcurrentJobs = Math.Clamp(Environment.ProcessorCount / 2, 1, 4),
        EstimatedMemoryPerJobBytes = 128 * 1024 * 1024 // 128 MB
    };

    public static EngineResourceProfile ExclusiveCom => new()
    {
        Affinity = EngineExecutionAffinity.ExclusiveSingleThreaded,
        MaxConcurrentJobs = 1,
        EstimatedMemoryPerJobBytes = 128 * 1024 * 1024 // 128 MB
    };
}
