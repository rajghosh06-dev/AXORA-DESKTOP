namespace Axora.Studio.Models.Rendering;

public enum RendererFixture { Ascii, UnicodeProbe }
public enum RendererBudget { Preview, Final }
public enum RendererOutcome
{
    Completed, Busy, Canceled, TimedOut, RuntimeUnavailable, RuntimeIntegrityFailure, IsolationUnavailable,
    StartupFailed, RendererFailed, DiagnosticLimitExceeded, OutputLimitExceeded, UnexpectedArtifacts, CleanupFailed, AdmissionClosed, InvalidRequest,
    ProvisioningRequired, DeletePending, SettlementFailed
}
public sealed record RendererRequest(Guid OperationId, RendererFixture Fixture = RendererFixture.Ascii, RendererBudget Budget = RendererBudget.Preview);
public sealed record RendererResult(Guid OperationId, RendererOutcome Outcome, string Reason,
    long PdfBytes = 0, string? PdfSha256 = null, long DiagnosticBytes = 0, double DurationMs = 0,
    double VerificationMs = 0, double SettlementMs = 0, ulong PeakCommittedBytes = 0, uint ProcessCount = 0,
    bool JobMembership = false, bool AppContainer = false, bool ZeroCapabilities = false, bool CleanupComplete = true)
{
    public string ArtifactStatus => Outcome == RendererOutcome.Completed && PdfBytes > 0 && !string.IsNullOrWhiteSpace(PdfSha256)
        ? "UNVALIDATED FIXTURE PDF" : "NO FIXTURE PDF ARTIFACT";
}
internal static class RendererLimits
{
    internal const int Diagnostics = 128 * 1024;
    internal const int Pdf = 16 * 1024 * 1024;
    internal const ulong JobMemory = 512UL * 1024 * 1024;
    internal const int WorkspaceEntries = 32;
    internal static TimeSpan Timeout(RendererBudget budget) => budget == RendererBudget.Final ? TimeSpan.FromSeconds(60) : TimeSpan.FromSeconds(15);
}
