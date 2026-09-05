namespace Axora.Desktop.Models;

/// <summary>
/// Result of an extension integrity and executable validation check.
/// </summary>
public sealed class ExtensionValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsCorrupted { get; set; }
    public bool RequiresRepair { get; set; }
    public string? ExecutablePath { get; set; }
    public string? DetectedVersion { get; set; }

    public static ExtensionValidationResult Success(string? executablePath = null, string? detectedVersion = null) =>
        new() { IsValid = true, ExecutablePath = executablePath, DetectedVersion = detectedVersion };

    public static ExtensionValidationResult Failed(string errorMessage, bool isCorrupted = false, bool requiresRepair = false) =>
        new() { IsValid = false, ErrorMessage = errorMessage, IsCorrupted = isCorrupted, RequiresRepair = requiresRepair };
}