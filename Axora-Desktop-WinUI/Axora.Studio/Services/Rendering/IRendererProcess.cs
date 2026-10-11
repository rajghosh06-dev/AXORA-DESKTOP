using System.Collections.Immutable;
using Axora.Studio.Models.Rendering;

namespace Axora.Studio.Services.Rendering;

internal sealed record RendererLaunch(RuntimeLease Runtime, ImmutableArray<string> Arguments, RendererWorkspace Workspace,
    bool Pdf, TimeSpan Timeout, int OutputLimit, int DiagnosticLimit = RendererLimits.Diagnostics);
internal sealed record RendererProcessResult(RendererOutcome Outcome, uint ExitCode, byte[] Output, long DiagnosticBytes,
    double DurationMs, double SettlementMs, ulong PeakCommittedBytes, uint ProcessCount, bool JobMembership,
    bool AppContainer, bool ZeroCapabilities, bool Settled);
internal interface IRendererProcess
{
    Task<RendererProcessResult> RunAsync(RendererLaunch launch, CancellationToken cancellationToken);
}
