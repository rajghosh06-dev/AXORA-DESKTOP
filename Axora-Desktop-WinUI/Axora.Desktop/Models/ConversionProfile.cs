using System;

namespace Axora.Desktop.Models;

/// <summary>
/// Reusable configuration profile for file conversion operations.
/// </summary>
public sealed record ConversionProfile
{
    public string Name { get; init; } = "Standard Quality";
    public int Quality { get; init; } = 85;
    public int TargetDpi { get; init; } = 150;
    public MetadataHandling MetadataPolicy { get; init; } = MetadataHandling.Preserve;
    public CollisionPolicy CollisionMode { get; init; } = CollisionPolicy.AutoRename;
    public int MaxDimension { get; init; } = 0; // 0 = original

    public static ConversionProfile Default => new();

    public static ConversionProfile HighFidelity => new()
    {
        Name = "High Fidelity / Archival",
        Quality = 100,
        TargetDpi = 300,
        MetadataPolicy = MetadataHandling.Preserve,
        CollisionMode = CollisionPolicy.AutoRename
    };

    public static ConversionProfile WebOptimized => new()
    {
        Name = "Web & Mobile Optimized",
        Quality = 75,
        TargetDpi = 96,
        MetadataPolicy = MetadataHandling.Strip,
        CollisionMode = CollisionPolicy.AutoRename
    };

    public static ConversionProfile PrintReady => new()
    {
        Name = "Print Ready",
        Quality = 100,
        TargetDpi = 300,
        MetadataPolicy = MetadataHandling.Preserve,
        CollisionMode = CollisionPolicy.AutoRename
    };
}
