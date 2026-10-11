using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Axora.Studio.Models.Rendering;

namespace Axora.Studio.Services.Rendering;

public sealed class RendererCoordinator
{
    private readonly object _gate = new();
    private readonly TypstRuntimeVerifier _verifier;
    private readonly IRendererProcess _process;
    private readonly string _workspaceBase;
    private CancellationTokenSource? _activeCancellation;
    private Task<RendererResult>? _active;
    private Task? _shutdown;
    private bool _closed;
    private bool _unresolved;
    internal Action<RendererWorkspace>? WorkspaceCreatedForTest { get; init; }
    public RendererCoordinator() : this(new TypstRuntimeVerifier(), new WindowsRendererProcess(), Path.Combine(RendererRuntimeManifest.DefaultProvisioningRoot, "operations")) { }
    internal RendererCoordinator(TypstRuntimeVerifier verifier, IRendererProcess process, string workspaceBase)
    { _verifier = verifier; _process = process; _workspaceBase = workspaceBase; }
    public bool IsActive { get { lock (_gate) return _active is not null; } }
    public Task<RendererResult> RenderAsync(RendererRequest request, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_closed) return Task.FromResult(Result(request, RendererOutcome.AdmissionClosed));
            if (_active is not null) return Task.FromResult(Result(request, RendererOutcome.Busy));
            if (request.OperationId == Guid.Empty || !Enum.IsDefined(request.Fixture) || !Enum.IsDefined(request.Budget))
                return Task.FromResult(Result(request, RendererOutcome.InvalidRequest));
            _activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var completion = new TaskCompletionSource<RendererResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _active = completion.Task; // Publish ownership before worker execution, including synchronous failures.
            _ = ExecuteAndReleaseAsync(request, _activeCancellation, completion);
            return completion.Task;
        }
    }
    private async Task ExecuteAndReleaseAsync(RendererRequest request, CancellationTokenSource cancellation, TaskCompletionSource<RendererResult> completion)
    {
        RendererResult result;
        try { result = await Task.Run(() => ExecuteAsync(request, cancellation.Token)).ConfigureAwait(false); }
        catch (Exception) { result = Result(request, RendererOutcome.SettlementFailed) with { CleanupComplete = false }; }
        lock (_gate)
        {
            // An unresolved native/security state permanently closes this coordinator.
            if (!result.CleanupComplete) { _closed = true; _unresolved = true; }
            _active = null; _activeCancellation = null;
            cancellation.Dispose(); completion.TrySetResult(result);
        }
    }
    private async Task<RendererResult> ExecuteAsync(RendererRequest request, CancellationToken token)
    {
        var clock = Stopwatch.StartNew(); double verificationMs = 0;
        WindowsRendererSandbox? sandbox = null;
        RuntimeLease? runtime = null;
        RendererWorkspace? workspace = null;
        RendererResult result = Result(request, RendererOutcome.StartupFailed);
        try
        {
            token.ThrowIfCancellationRequested();
            sandbox = new();
            var verification = Stopwatch.StartNew();
            runtime = _verifier.Verify(sandbox);
            verificationMs = verification.Elapsed.TotalMilliseconds;
            workspace = RendererWorkspace.Create(_workspaceBase, request.OperationId, request.Fixture, sandbox);
            WorkspaceCreatedForTest?.Invoke(workspace);
            var version = await _process.RunAsync(new(runtime, ["--version"], workspace, false, TimeSpan.FromSeconds(5), 4096), token).ConfigureAwait(false);
            if (version.Outcome != RendererOutcome.Completed) result = FromProcess(request, version);
            else if (Encoding.UTF8.GetString(version.Output).TrimEnd('\r', '\n') != RendererRuntimeManifest.VersionOutput)
                result = Result(request, RendererOutcome.RuntimeIntegrityFailure);
            else
            {
                var compiled = await _process.RunAsync(new(runtime, ["compile", "--format", "pdf", "--root", workspace.Input,
                    "--ignore-system-fonts", "--package-path", workspace.Packages, "--package-cache-path", workspace.Cache,
                    "--diagnostic-format", "short", workspace.Source, "-"], workspace, true, RendererLimits.Timeout(request.Budget), RendererLimits.Pdf), token).ConfigureAwait(false);
                result = FromProcess(request, compiled);
                if (compiled.Outcome == RendererOutcome.Completed)
                {
                    if (compiled.Output.Length == 0) result = result with { Outcome = RendererOutcome.RendererFailed, Reason = RendererOutcome.RendererFailed.ToString() };
                    else result = result with { PdfBytes = compiled.Output.LongLength, PdfSha256 = Convert.ToHexString(SHA256.HashData(compiled.Output)) };
                }
                Array.Clear(compiled.Output);
            }
            Array.Clear(version.Output);
        }
        catch (OperationCanceledException) { result = Result(request, RendererOutcome.Canceled); }
        catch (FileNotFoundException) { result = Result(request, RendererOutcome.RuntimeUnavailable); }
        catch (DirectoryNotFoundException) { result = Result(request, RendererOutcome.RuntimeUnavailable); }
        catch (RendererProvisioningException) { result = Result(request, RendererOutcome.ProvisioningRequired); }
        catch (RendererWorkspaceSetupException ex) { result = Result(request, ex.DeletePending ? RendererOutcome.DeletePending : RendererOutcome.CleanupFailed) with { CleanupComplete = false }; }
        catch (InvalidDataException) { result = Result(request, RendererOutcome.RuntimeIntegrityFailure); }
        catch (Exception ex) when (RendererWorkspace.Expected(ex)) { result = Result(request, sandbox is null ? RendererOutcome.IsolationUnavailable : RendererOutcome.StartupFailed); }
        finally
        {
            runtime?.Dispose(); sandbox?.Dispose();
            if (workspace is not null)
            {
                bool cleaned = workspace.Cleanup();
                bool pending = workspace.DeletePending;
                workspace.Dispose();
                cleaned &= RendererWorkspace.NamespaceAbsent(workspace.Root);
                if (!cleaned)
                    result = result with { Outcome = pending ? RendererOutcome.DeletePending : RendererOutcome.CleanupFailed,
                        Reason = pending ? "DeletePending" : "CleanupFailed", CleanupComplete = false, PdfBytes = 0, PdfSha256 = null };
            }
        }
        return result with { DurationMs = clock.Elapsed.TotalMilliseconds, VerificationMs = verificationMs };
    }
    private static RendererResult FromProcess(RendererRequest request, RendererProcessResult result) =>
        new(request.OperationId, result.Outcome, result.Outcome.ToString(), DiagnosticBytes: result.DiagnosticBytes,
            SettlementMs: result.SettlementMs, PeakCommittedBytes: result.PeakCommittedBytes, ProcessCount: result.ProcessCount,
            JobMembership: result.JobMembership, AppContainer: result.AppContainer, ZeroCapabilities: result.ZeroCapabilities, CleanupComplete: result.Settled);
    private static RendererResult Result(RendererRequest request, RendererOutcome outcome) => new(request.OperationId, outcome, outcome.ToString());
    public Task ShutdownAsync()
    {
        lock (_gate)
        {
            if (_shutdown is not null) return _shutdown;
            _closed = true;
            _activeCancellation?.Cancel();
            return _shutdown = _unresolved ? Task.FromException(new IOException("Renderer shutdown settlement failed")) : SettleAsync(_active);
        }
    }
    private static async Task SettleAsync(Task<RendererResult>? active)
    {
        if (active is null) return;
        var result = await active.ConfigureAwait(false);
        if (!result.CleanupComplete) throw new IOException("Renderer shutdown settlement failed");
    }
}
