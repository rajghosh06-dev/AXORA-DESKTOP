namespace Axora.Desktop.Models;

/// <summary>
/// Concurrency and system resource affinity for a conversion engine.
/// </summary>
public enum EngineExecutionAffinity
{
    /// <summary>Pure CPU-bound work (e.g., WIC rasters, CSV/JSON). Scalable across cores.</summary>
    CpuBound,

    /// <summary>Memory-intensive operations (e.g., multi-page PDF compilation). Throttled to 2-3 jobs.</summary>
    MemoryBound,

    /// <summary>Out-of-process CLI tool (e.g., magick.exe). Bounded to prevent process thrashing.</summary>
    ProcessBound,

    /// <summary>Single-threaded COM STA or lock-sensitive engine (e.g., Word.Application COM). Concurrency = 1.</summary>
    ExclusiveSingleThreaded
}
