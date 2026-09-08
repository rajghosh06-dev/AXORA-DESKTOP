namespace Axora.Desktop.Models;

/// <summary>
/// Identifiers for canonical optimization presets and custom compression profiles.
/// </summary>
public enum OptimizationPresetId
{
    /// <summary>Optimal balance between file size and visual fidelity (Quality: 85, MaxDimension: 0 [original], DPI: 150, Strip metadata).</summary>
    Balanced = 0,

    /// <summary>Aggressive compression for web/mobile distribution (Quality: 75, MaxDimension: 1920, DPI: 96, Strip metadata).</summary>
    WebOptimized = 1,

    /// <summary>Maximum fidelity / archival preservation (Quality: 100, MaxDimension: 0 [original], DPI: 300, Preserve metadata).</summary>
    MaximumFidelity = 2,

    /// <summary>User-customizable compression and dimension scaling profile.</summary>
    Custom = 3
}
