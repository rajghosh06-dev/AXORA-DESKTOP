using System;
using System.Collections.Generic;
using System.Linq;

namespace Axora.Desktop.Models;

/// <summary>
/// Centralized validation and normalization rules for optimization presets and conversion profiles.
/// Guarantees deterministic parameter clamping and prevents corrupt or out-of-bounds conversion settings.
/// </summary>
public static class OptimizationValidation
{
    public const int MinQuality = 1;
    public const int MaxQuality = 100;
    public const int DefaultQuality = 85;

    public const int DefaultTargetDpi = 150;

    /// <summary>
    /// Allowed discrete values for maximum dimension scaling (0 indicates original / unconstrained).
    /// </summary>
    public static readonly IReadOnlyList<int> AllowedMaxDimensions = new[] { 0, 1280, 1920, 2560, 3840 };

    /// <summary>
    /// Allowed discrete values for target DPI resolution.
    /// </summary>
    public static readonly IReadOnlyList<int> AllowedTargetDpis = new[] { 72, 96, 150, 300 };

    /// <summary>
    /// Determines whether the specified quality integer falls within the valid range [1, 100].
    /// </summary>
    public static bool IsValidQuality(int quality) => quality is >= MinQuality and <= MaxQuality;

    /// <summary>
    /// Clamps quality to the valid range [1, 100].
    /// </summary>
    public static int NormalizeQuality(int quality) => Math.Clamp(quality, MinQuality, MaxQuality);

    /// <summary>
    /// Determines whether the specified maximum dimension is one of the supported canonical values (0, 1280, 1920, 2560, 3840).
    /// </summary>
    public static bool IsValidMaxDimension(int dimension) =>
        dimension == 0 || dimension == 1280 || dimension == 1920 || dimension == 2560 || dimension == 3840;

    /// <summary>
    /// Normalizes maximum dimension. Non-positive values clamp to 0 (unconstrained original).
    /// Arbitrary positive values map deterministically to the nearest allowed dimension.
    /// </summary>
    public static int NormalizeMaxDimension(int dimension)
    {
        if (dimension <= 0)
        {
            return 0;
        }

        if (IsValidMaxDimension(dimension))
        {
            return dimension;
        }

        // Find closest valid dimension among allowed set
        return AllowedMaxDimensions
            .OrderBy(v => Math.Abs(v - dimension))
            .First();
    }

    /// <summary>
    /// Determines whether the specified target DPI is one of the supported standard DPI values (72, 96, 150, 300).
    /// </summary>
    public static bool IsValidTargetDpi(int dpi) =>
        dpi == 72 || dpi == 96 || dpi == 150 || dpi == 300;

    /// <summary>
    /// Normalizes target DPI. Non-positive or unsupported values map deterministically to the closest allowed DPI, defaulting to 150.
    /// </summary>
    public static int NormalizeTargetDpi(int dpi)
    {
        if (IsValidTargetDpi(dpi))
        {
            return dpi;
        }

        if (dpi <= 0)
        {
            return DefaultTargetDpi;
        }

        return AllowedTargetDpis
            .OrderBy(v => Math.Abs(v - dpi))
            .First();
    }

    /// <summary>
    /// Determines whether the metadata policy is a recognized enum value.
    /// </summary>
    public static bool IsValidMetadataPolicy(MetadataHandling policy) =>
        Enum.IsDefined(policy);

    /// <summary>
    /// Normalizes metadata policy, falling back to Strip if an invalid value is supplied.
    /// </summary>
    public static MetadataHandling NormalizeMetadataPolicy(MetadataHandling policy) =>
        Enum.IsDefined(policy) ? policy : MetadataHandling.Strip;

    /// <summary>
    /// Validates a ConversionProfile against canonical domain rules.
    /// </summary>
    public static bool TryValidateProfile(ConversionProfile? profile, out IReadOnlyList<string> errors)
    {
        var errorList = new List<string>();

        if (profile == null)
        {
            errorList.Add("Profile cannot be null.");
            errors = errorList;
            return false;
        }

        if (!IsValidQuality(profile.Quality))
        {
            errorList.Add($"Quality must be between {MinQuality} and {MaxQuality}. Received: {profile.Quality}.");
        }

        if (!IsValidMaxDimension(profile.MaxDimension))
        {
            errorList.Add($"MaxDimension must be one of {string.Join(", ", AllowedMaxDimensions)}. Received: {profile.MaxDimension}.");
        }

        if (!IsValidTargetDpi(profile.TargetDpi))
        {
            errorList.Add($"TargetDpi must be one of {string.Join(", ", AllowedTargetDpis)}. Received: {profile.TargetDpi}.");
        }

        if (!IsValidMetadataPolicy(profile.MetadataPolicy))
        {
            errorList.Add($"MetadataPolicy must be a valid MetadataHandling enum value. Received: {profile.MetadataPolicy}.");
        }

        errors = errorList;
        return errorList.Count == 0;
    }

    /// <summary>
    /// Returns a new ConversionProfile with all parameters normalized to canonical bounds.
    /// </summary>
    public static ConversionProfile NormalizeProfile(ConversionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return profile with
        {
            Quality = NormalizeQuality(profile.Quality),
            MaxDimension = NormalizeMaxDimension(profile.MaxDimension),
            TargetDpi = NormalizeTargetDpi(profile.TargetDpi),
            MetadataPolicy = NormalizeMetadataPolicy(profile.MetadataPolicy)
        };
    }

    /// <summary>
    /// Validates an OptimizationPreset against canonical domain rules.
    /// </summary>
    public static bool TryValidatePreset(OptimizationPreset? preset, out IReadOnlyList<string> errors)
    {
        var errorList = new List<string>();

        if (preset == null)
        {
            errorList.Add("Preset cannot be null.");
            errors = errorList;
            return false;
        }

        if (!Enum.IsDefined(preset.Id))
        {
            errorList.Add($"Preset ID must be a recognized OptimizationPresetId. Received: {preset.Id}.");
        }

        if (string.IsNullOrWhiteSpace(preset.DisplayName))
        {
            errorList.Add("DisplayName cannot be empty.");
        }

        if (!IsValidQuality(preset.Quality))
        {
            errorList.Add($"Quality must be between {MinQuality} and {MaxQuality}. Received: {preset.Quality}.");
        }

        if (!IsValidMaxDimension(preset.MaxDimension))
        {
            errorList.Add($"MaxDimension must be one of {string.Join(", ", AllowedMaxDimensions)}. Received: {preset.MaxDimension}.");
        }

        if (!IsValidTargetDpi(preset.TargetDpi))
        {
            errorList.Add($"TargetDpi must be one of {string.Join(", ", AllowedTargetDpis)}. Received: {preset.TargetDpi}.");
        }

        if (!IsValidMetadataPolicy(preset.MetadataPolicy))
        {
            errorList.Add($"MetadataPolicy must be a valid MetadataHandling enum value. Received: {preset.MetadataPolicy}.");
        }

        errors = errorList;
        return errorList.Count == 0;
    }

    /// <summary>
    /// Returns a new OptimizationPreset with all parameters normalized to canonical bounds.
    /// </summary>
    public static OptimizationPreset NormalizePreset(OptimizationPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        return preset with
        {
            Quality = NormalizeQuality(preset.Quality),
            MaxDimension = NormalizeMaxDimension(preset.MaxDimension),
            TargetDpi = NormalizeTargetDpi(preset.TargetDpi),
            MetadataPolicy = NormalizeMetadataPolicy(preset.MetadataPolicy)
        };
    }
}
