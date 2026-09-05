namespace Axora.Desktop.Models;

/// <summary>
/// Strict lifecycle states enforced by the ConversionOrchestrator state machine.
/// </summary>
public enum ConversionJobState
{
    /// <summary>Job is newly created and submitted, awaiting validation.</summary>
    Pending,

    /// <summary>Job is undergoing pre-flight validation (format checks, collision resolution, engine allocation).</summary>
    Validating,

    /// <summary>Job has passed validation and is waiting in the queue for an available concurrency slot.</summary>
    Queued,

    /// <summary>Job is actively executing on a worker with its allocated engine.</summary>
    Running,

    /// <summary>Cancellation was requested; worker is cooperatively aborting and rolling back staging.</summary>
    Cancelling,

    /// <summary>Job was cancelled; staging was cleaned and destination was not touched.</summary>
    Cancelled,

    /// <summary>Conversion completed successfully, passed post-flight validation, and was atomically committed.</summary>
    Succeeded,

    /// <summary>Conversion encountered a terminal error.</summary>
    Failed,

    /// <summary>Conversion was skipped (e.g., target file exists and CollisionPolicy is Skip).</summary>
    Skipped
}
