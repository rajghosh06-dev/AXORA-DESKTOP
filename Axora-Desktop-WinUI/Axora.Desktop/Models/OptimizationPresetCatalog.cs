using System;
using System.Collections.Generic;

namespace Axora.Desktop.Models;

/// <summary>
/// Canonical catalog providing standard optimization presets for the Universal Converter.
/// Guarantees immutable definitions for Balanced, WebOptimized, MaximumFidelity, and Custom presets.
/// </summary>
public static class OptimizationPresetCatalog
{
    /// <summary>
    /// Balanced preset: Optimal balance between file size and visual fidelity (Quality: 85, MaxDim: Original, DPI: 150, Strip metadata).
    /// </summary>
    public static OptimizationPreset Balanced { get; } = new()
    {
        Id = OptimizationPresetId.Balanced,
        DisplayName = "Balanced",
        Description = "Optimal balance between file size and visual fidelity. Recommended for everyday sharing and archiving.",
        Quality = 85,
        MaxDimension = 0,
        TargetDpi = 150,
        MetadataPolicy = MetadataHandling.Strip,
        IsCustomizable = false
    };

    /// <summary>
    /// Web & Mobile Optimized preset: Aggressive compression with safe 1080p dimension clamping (Quality: 75, MaxDim: 1920, DPI: 96, Strip metadata).
    /// </summary>
    public static OptimizationPreset WebOptimized { get; } = new()
    {
        Id = OptimizationPresetId.WebOptimized,
        DisplayName = "Web & Mobile Optimized",
        Description = "Aggressive compression with safe dimension clamping for fast web and mobile transfers.",
        Quality = 75,
        MaxDimension = 1920,
        TargetDpi = 96,
        MetadataPolicy = MetadataHandling.Strip,
        IsCustomizable = false
    };

    /// <summary>
    /// Maximum Fidelity / Archival preset: Maximum-quality encoding preserving all metadata and full dimensions (Quality: 100, MaxDim: Original, DPI: 300, Preserve metadata).
    /// </summary>
    public static OptimizationPreset MaximumFidelity { get; } = new()
    {
        Id = OptimizationPresetId.MaximumFidelity,
        DisplayName = "Maximum Fidelity / Archival",
        Description = "Lossless or maximum-quality encoding preserving all metadata and full original dimensions.",
        Quality = 100,
        MaxDimension = 0,
        TargetDpi = 300,
        MetadataPolicy = MetadataHandling.Preserve,
        IsCustomizable = false
    };

    /// <summary>
    /// Custom preset: User-configurable compression parameters and dimension scaling.
    /// </summary>
    public static OptimizationPreset Custom { get; } = new()
    {
        Id = OptimizationPresetId.Custom,
        DisplayName = "Custom",
        Description = "User-configured compression parameters and dimension scaling.",
        Quality = 85,
        MaxDimension = 0,
        TargetDpi = 150,
        MetadataPolicy = MetadataHandling.Strip,
        IsCustomizable = true
    };

    /// <summary>
    /// Default preset applied when no specific preset is requested.
    /// </summary>
    public static OptimizationPreset Default => Balanced;

    /// <summary>
    /// All canonical optimization presets in logical presentation order.
    /// </summary>
    public static IReadOnlyList<OptimizationPreset> All { get; } = new[]
    {
        Balanced,
        WebOptimized,
        MaximumFidelity,
        Custom
    };

    /// <summary>
    /// Resolves an optimization preset by its identifier. Throws ArgumentOutOfRangeException if unsupported.
    /// </summary>
    public static OptimizationPreset GetPreset(OptimizationPresetId id) => id switch
    {
        OptimizationPresetId.Balanced => Balanced,
        OptimizationPresetId.WebOptimized => WebOptimized,
        OptimizationPresetId.MaximumFidelity => MaximumFidelity,
        OptimizationPresetId.Custom => Custom,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, $"Unsupported optimization preset ID: {id}")
    };

    /// <summary>
    /// Attempts to resolve an optimization preset by its identifier.
    /// </summary>
    public static bool TryGetPreset(OptimizationPresetId id, out OptimizationPreset preset)
    {
        switch (id)
        {
            case OptimizationPresetId.Balanced:
                preset = Balanced;
                return true;
            case OptimizationPresetId.WebOptimized:
                preset = WebOptimized;
                return true;
            case OptimizationPresetId.MaximumFidelity:
                preset = MaximumFidelity;
                return true;
            case OptimizationPresetId.Custom:
                preset = Custom;
                return true;
            default:
                preset = Balanced;
                return false;
        }
    }
}
