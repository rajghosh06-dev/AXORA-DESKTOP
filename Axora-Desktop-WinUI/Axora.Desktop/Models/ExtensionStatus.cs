namespace Axora.Desktop.Models;

/// <summary>
/// Represents the operational and lifecycle status of an external extension or dependency.
/// </summary>
public enum ExtensionStatus
{
    /// <summary>The extension is not present on the host system or in Axora managed storage.</summary>
    NotInstalled,

    /// <summary>An installation, update, repair, or download is actively executing.</summary>
    Installing,

    /// <summary>The extension is installed, validated, and ready for use.</summary>
    Installed,

    /// <summary>The extension is installed, but an approved newer version is available.</summary>
    UpdateAvailable,

    /// <summary>The extension binaries are present but damaged or missing critical files; repair is required.</summary>
    RepairRequired,

    /// <summary>The extension installation or staged files failed cryptographic/checksum validation.</summary>
    Corrupted,

    /// <summary>The host architecture or OS does not meet the extension requirements.</summary>
    Unsupported,

    /// <summary>An installation, download, or verification operation encountered an unrecoverable failure.</summary>
    Failed
}