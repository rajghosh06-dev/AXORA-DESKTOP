namespace Axora.Desktop.Models;

/// <summary>
/// Immutable representation of an optimization preset configuration.
/// Encapsulates quality, dimension limits, DPI resolution, and metadata handling policy.
/// </summary>
public sealed record OptimizationPreset
{
    public OptimizationPresetId Id { get; init; } = OptimizationPresetId.Balanced;

    public string DisplayName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int Quality { get; init; } = OptimizationValidation.DefaultQuality;

    /// <summary>
    /// Maximum bounding box dimension in pixels (0 represents original / unconstrained).
    /// </summary>
    public int MaxDimension { get; init; } = 0;

    /// <summary>
    /// Target raster resolution in dots per inch (DPI).
    /// </summary>
    public int TargetDpi { get; init; } = OptimizationValidation.DefaultTargetDpi;

    /// <summary>
    /// Metadata preservation policy (Strip or Preserve).
    /// </summary>
    public MetadataHandling MetadataPolicy { get; init; } = MetadataHandling.Strip;

    /// <summary>
    /// Indicates whether the preset allows direct user customization in the UI.
    /// </summary>
    public bool IsCustomizable { get; init; } = false;

    /// <summary>
    /// Converts this optimization preset into a canonical ConversionProfile for engine execution.
    /// Values are normalized to ensure safe execution boundaries.
    /// </summary>
    public ConversionProfile ToProfile(CollisionPolicy collisionMode = CollisionPolicy.AutoRename)
    {
        return new ConversionProfile
        {
            Name = DisplayName,
            Quality = OptimizationValidation.NormalizeQuality(Quality),
            MaxDimension = OptimizationValidation.NormalizeMaxDimension(MaxDimension),
            TargetDpi = OptimizationValidation.NormalizeTargetDpi(TargetDpi),
            MetadataPolicy = OptimizationValidation.NormalizeMetadataPolicy(MetadataPolicy),
            CollisionMode = collisionMode
        };
    }
}
