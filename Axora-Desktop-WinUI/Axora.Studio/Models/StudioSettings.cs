using System.Text.Json.Serialization;

namespace Axora.Studio.Models;

public enum StudioTheme { System, Light, Dark }

public sealed record StudioSettings
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;
    [JsonPropertyName("theme")]
    public StudioTheme Theme { get; init; } = StudioTheme.System;
}

public sealed record SettingsLoadResult(StudioSettings Settings, bool CanSave, string? Warning);
public sealed record SettingsSaveResult(bool Published, string Message);
