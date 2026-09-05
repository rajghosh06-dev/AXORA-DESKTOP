namespace Axora.Desktop.Models;

/// <summary>
/// Lifecycle states for an individual conversion job in the Universal Converter.
/// </summary>
public enum ConversionJobStatus
{
    /// <summary>Job is registered in the queue and waiting for scheduler pickup.</summary>
    Queued,

    /// <summary>Job is undergoing pre-flight checks (path resolution, disk validation, engine allocation).</summary>
    Preparing,

    /// <summary>Job is actively being transformed by an allocated conversion engine.</summary>
    Running,

    /// <summary>Conversion finished successfully, passed post-flight validation, and was atomically committed.</summary>
    Completed,

    /// <summary>Conversion encountered a terminal error.</summary>
    Failed,

    /// <summary>Conversion was cancelled by user request or parent queue abort.</summary>
    Cancelled,

    /// <summary>Conversion was skipped (e.g. destination file already exists and policy is Skip).</summary>
    Skipped
}
