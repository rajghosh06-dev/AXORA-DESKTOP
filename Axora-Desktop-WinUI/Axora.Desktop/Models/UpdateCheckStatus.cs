namespace Axora.Desktop.Models;

/// <summary>
/// Status of remote extension update availability checking.
/// Separated from operational status so network check failures do not corrupt healthy installations.
/// </summary>
public enum UpdateCheckStatus
{
    /// <summary>Remote update check has not yet been executed.</summary>
    NotChecked,

    /// <summary>Remote check is actively in progress.</summary>
    Checking,

    /// <summary>Installed version matches or exceeds latest approved release.</summary>
    UpToDate,

    /// <summary>An approved newer release exists and is available for explicit user installation.</summary>
    UpdateAvailable,

    /// <summary>Unable to check remote server (network offline, timeout, or vendor unreachable). Installed status remains intact.</summary>
    UnableToCheck
}
