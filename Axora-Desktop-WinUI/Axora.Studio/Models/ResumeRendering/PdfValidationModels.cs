using Axora.PdfValidation;

namespace Axora.Studio.Models.ResumeRendering;

internal enum PdfFoundationOutcome
{
    FoundationObserved, DependencyRejected, Busy, AdmissionClosed, Canceled, TimedOut,
    RuntimeUnavailable, RuntimeIntegrityFailure, ProvisioningRequired, IsolationUnavailable,
    StartupFailed, WorkerFailed, ProtocolRejected, PipeLimit, CleanupFailed, SettlementFailed
}

// Private infrastructure evidence only. No PDF bytes, document text, paths or promotion API.
internal sealed record PdfFoundationResult(PdfFoundationOutcome Outcome, PdfFoundationReply? Observation = null,
    bool Settled = true, bool CleanupComplete = true, bool JobMembership = false, bool AppContainer = false,
    bool ZeroCapabilities = false, bool InputSettled = true, uint FinalActiveProcesses = 0)
{
    internal string ArtifactStatus => "FOUNDATION ONLY — PDF NOT VALIDATED";
    internal string PolicySha256 => PdfValidationProtocol.PolicySha256;
}
