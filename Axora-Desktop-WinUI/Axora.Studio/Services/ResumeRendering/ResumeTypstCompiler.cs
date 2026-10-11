using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Axora.Studio.Models.Rendering;
using Axora.Studio.Models.ResumeRendering;
using Axora.Studio.Services.Rendering;

namespace Axora.Studio.Services.ResumeRendering;

internal enum ResumeCompileOutcome { Completed, Busy, AdmissionClosed, Canceled, UnsupportedTemplate,
    UnsupportedFormattingPreference, UnsupportedRenderContent, TemplateIntegrityFailure, NativeFailure }
internal sealed record ResumeCompileResult(ResumeCompileOutcome Outcome, UnvalidatedResumePdf? Candidate = null,
    RendererOutcome? NativeOutcome = null, bool CleanupComplete = true, bool JobMembership = false,
    bool AppContainer = false, bool ZeroCapabilities = false, bool Settled = true, uint ProcessCount = 0,
    long DiagnosticBytes = 0);

// Finite rendering input only. Native infrastructure is fixed in production; no caller path,
// source, argument, environment, output destination, or PDF publication API exists.
internal sealed class ResumeTypstCompiler
{
    private readonly TypstRuntimeVerifier verifier;
    private readonly IRendererProcess process;
    private readonly object gate = new();
    private CancellationTokenSource? activeCancellation;
    private Task<ResumeCompileResult>? active;
    private Task? shutdown;
    private bool closed, unresolved;
    internal Action<RendererWorkspace>? WorkspaceCreatedForTest { get; init; }
    internal Action<string>? SetupPhaseForTest { get; init; }
    internal ResumeTypstCompiler() : this(new TypstRuntimeVerifier(), new WindowsRendererProcess()) { }
    internal ResumeTypstCompiler(TypstRuntimeVerifier verifier, IRendererProcess process)
    { this.verifier = verifier; this.process = process; }
    internal Task<ResumeCompileResult> CompileAsync(ResumeRenderSnapshot snapshot, ResumeTemplateChoice template,
        ResumeRenderFontChoice font, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (closed) return Task.FromResult(new ResumeCompileResult(ResumeCompileOutcome.AdmissionClosed));
            if (active is not null) return Task.FromResult(new ResumeCompileResult(ResumeCompileOutcome.Busy));
            activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var completion = new TaskCompletionSource<ResumeCompileResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            active = completion.Task;
            _ = ExecuteAndRelease(snapshot, template, font, activeCancellation, completion);
            return completion.Task;
        }
    }
    private async Task ExecuteAndRelease(ResumeRenderSnapshot snapshot, ResumeTemplateChoice template, ResumeRenderFontChoice font,
        CancellationTokenSource cancellation, TaskCompletionSource<ResumeCompileResult> completion)
    {
        ResumeCompileResult result;
        try { result = await Task.Run(() => Execute(snapshot, template, font, cancellation.Token)).ConfigureAwait(false); }
        catch (Exception) { result = new(ResumeCompileOutcome.NativeFailure, NativeOutcome: RendererOutcome.SettlementFailed, CleanupComplete: false, Settled: false); }
        lock (gate)
        {
            if (!result.CleanupComplete || !result.Settled) { unresolved = true; closed = true; }
            if (cancellation.IsCancellationRequested && result.Candidate is not null)
                result = result with { Outcome = ResumeCompileOutcome.Canceled, Candidate = null };
            active = null; activeCancellation = null; cancellation.Dispose(); completion.TrySetResult(result);
        }
    }
    private async Task<ResumeCompileResult> Execute(ResumeRenderSnapshot snapshot, ResumeTemplateChoice template,
        ResumeRenderFontChoice font, CancellationToken token)
    {
        Guid operation = Guid.NewGuid();
        TrustedRenderPackage? package = null;
        WindowsRendererSandbox? sandbox = null;
        RendererWorkspace? workspace = null;
        RuntimeLease? runtime = null;
        byte[] bytes = [];
        ResumeCompileResult result = new(ResumeCompileOutcome.NativeFailure, NativeOutcome: RendererOutcome.StartupFailed);
        try
        {
            token.ThrowIfCancellationRequested();
            package = TrustedRenderPackage.Create(snapshot, template, font);
            RequireSupportedContent(package.CopyData());
            sandbox = new();
            runtime = verifier.Verify(sandbox);
            workspace = RendererWorkspace.Create(Path.Combine(verifier.Root, "operations"), operation, package, sandbox, SetupPhaseForTest);
            WorkspaceCreatedForTest?.Invoke(workspace);
            var version = await process.RunAsync(new(runtime, ["--version"], workspace, false, TimeSpan.FromSeconds(5), 4096), token).ConfigureAwait(false);
            try
            {
                if (version.Outcome != RendererOutcome.Completed || !version.Settled)
                    result = FromProcess(version);
                else if (Encoding.UTF8.GetString(version.Output).TrimEnd('\r', '\n') != RendererRuntimeManifest.VersionOutput)
                    result = new(ResumeCompileOutcome.NativeFailure, NativeOutcome: RendererOutcome.RuntimeIntegrityFailure);
                else
                {
                    var rendered = await process.RunAsync(new(runtime, CompileArguments(workspace), workspace, true,
                        TimeSpan.FromSeconds(60), RendererLimits.Pdf), token).ConfigureAwait(false);
                    result = FromProcess(rendered);
                    if (rendered.Outcome == RendererOutcome.Completed && rendered.Settled && rendered.JobMembership &&
                        rendered.AppContainer && rendered.ZeroCapabilities && rendered.ExitCode == 0 && rendered.Output.Length is > 0 and <= RendererLimits.Pdf)
                        bytes = rendered.Output;
                    else
                    {
                        Array.Clear(rendered.Output);
                        if (result.Outcome == ResumeCompileOutcome.Completed)
                            result = result with { Outcome = ResumeCompileOutcome.NativeFailure, NativeOutcome = RendererOutcome.RendererFailed };
                    }
                }
            }
            finally { Array.Clear(version.Output); }
        }
        catch (OperationCanceledException) { result = new(ResumeCompileOutcome.Canceled, NativeOutcome: RendererOutcome.Canceled); }
        catch (ResumeTemplateException ex) { result = new(ex.Failure switch {
            ResumeTemplateFailure.UnsupportedRenderContent => ResumeCompileOutcome.UnsupportedRenderContent,
            ResumeTemplateFailure.TemplateIntegrityFailure => ResumeCompileOutcome.TemplateIntegrityFailure,
            _ => ResumeCompileOutcome.UnsupportedTemplate }); }
        catch (ResumeRenderException) { result = new(ResumeCompileOutcome.UnsupportedFormattingPreference); }
        catch (FileNotFoundException) { result = new(ResumeCompileOutcome.NativeFailure, NativeOutcome: RendererOutcome.RuntimeUnavailable); }
        catch (DirectoryNotFoundException) { result = new(ResumeCompileOutcome.NativeFailure, NativeOutcome: RendererOutcome.RuntimeUnavailable); }
        catch (RendererProvisioningException) { result = new(ResumeCompileOutcome.NativeFailure, NativeOutcome: RendererOutcome.ProvisioningRequired); }
        catch (RendererWorkspaceSetupException ex) { result = new(ResumeCompileOutcome.NativeFailure,
            NativeOutcome: ex.DeletePending ? RendererOutcome.DeletePending : RendererOutcome.CleanupFailed, CleanupComplete: false); }
        catch (InvalidDataException) { result = new(ResumeCompileOutcome.NativeFailure,
            NativeOutcome: package is not null && runtime is null ? RendererOutcome.RuntimeIntegrityFailure : RendererOutcome.UnexpectedArtifacts); }
        catch (Exception ex) when (RendererWorkspace.Expected(ex))
        { result = new(ResumeCompileOutcome.NativeFailure, NativeOutcome: sandbox is null ? RendererOutcome.IsolationUnavailable : RendererOutcome.StartupFailed); }
        finally
        {
            if (workspace is not null)
            {
                try { workspace.Validate(); }
                catch (Exception ex) when (ex is InvalidDataException || RendererWorkspace.Expected(ex))
                { result = result with { Outcome = ResumeCompileOutcome.NativeFailure, NativeOutcome = RendererOutcome.UnexpectedArtifacts }; }
                bool cleaned = workspace.Cleanup();
                bool pending = workspace.DeletePending;
                workspace.Dispose();
                cleaned &= RendererWorkspace.NamespaceAbsent(workspace.Root);
                if (!cleaned) result = result with { Outcome = ResumeCompileOutcome.NativeFailure,
                    NativeOutcome = pending ? RendererOutcome.DeletePending : RendererOutcome.CleanupFailed, CleanupComplete = false };
            }
            runtime?.Dispose(); sandbox?.Dispose();
        }
        try
        {
            if (result.Outcome == ResumeCompileOutcome.Completed && result.CleanupComplete && result.Settled &&
                !token.IsCancellationRequested && bytes.Length > 0)
                return result with { Candidate = new(operation, package!, bytes) };
            if (token.IsCancellationRequested && result.CleanupComplete && result.Settled)
                result = result with { Outcome = ResumeCompileOutcome.Canceled, NativeOutcome = RendererOutcome.Canceled };
            return result;
        }
        finally { Array.Clear(bytes); }
    }
    private static ResumeCompileResult FromProcess(RendererProcessResult r) => new(
        r.Outcome == RendererOutcome.Completed && r.Settled ? ResumeCompileOutcome.Completed :
        r.Outcome == RendererOutcome.Canceled ? ResumeCompileOutcome.Canceled : ResumeCompileOutcome.NativeFailure,
        NativeOutcome: r.Settled ? r.Outcome : RendererOutcome.SettlementFailed, JobMembership: r.JobMembership, AppContainer: r.AppContainer,
        ZeroCapabilities: r.ZeroCapabilities, Settled: r.Settled, ProcessCount: r.ProcessCount, DiagnosticBytes: r.DiagnosticBytes);
    internal static ImmutableArray<string> CompileArguments(RendererWorkspace w) => ["compile", "--format", "pdf", "--root", w.Input,
        "--ignore-system-fonts", "--package-path", w.Packages, "--package-cache-path", w.Cache,
        "--diagnostic-format", "short", w.Source, "-"];
    internal Task ShutdownAsync()
    {
        lock (gate)
        {
            if (shutdown is not null) return shutdown;
            closed = true; activeCancellation?.Cancel();
            return shutdown = unresolved ? Task.FromException(new IOException("ResumeRendererSettlementFailed")) : Settle(active);
        }
    }
    private static async Task Settle(Task<ResumeCompileResult>? task)
    {
        if (task is null) return;
        var r = await task.ConfigureAwait(false);
        if (!r.CleanupComplete || !r.Settled) throw new IOException("ResumeRendererSettlementFailed");
    }

    // Unicode cmap intersection of the REGULAR and BOLD embedded Libertinus Serif faces
    // in the exact pinned 0.15.1 executable. No external/system font is consulted.
    // The independently decoded evidence also records per-face coverage and native probes.
    private static readonly (int First, int Last)[] GlyphRanges = [(32, 126), (160, 740), (746, 846), (848, 879), (884, 885), (890, 894), (900, 906), (908, 908), (910, 929), (931, 974), (976, 993), (1008, 1014), (1016, 1017), (1019, 1019), (1021, 1119), (1122, 1123), (1136, 1143), (1168, 1220), (1223, 1228), (1232, 1273), (1456, 1475), (1478, 1478), (1488, 1514), (1520, 1524), (7477, 7477), (7491, 7519), (7522, 7527), (7580, 7580), (7584, 7584), (7588, 7588), (7606, 7606), (7611, 7611), (7680, 7835), (7838, 7838), (7840, 7929), (7936, 7957), (7960, 7965), (7968, 8005), (8008, 8013), (8016, 8023), (8025, 8025), (8027, 8027), (8029, 8029), (8031, 8061), (8064, 8116), (8118, 8132), (8134, 8147), (8150, 8155), (8157, 8175), (8178, 8180), (8182, 8190), (8192, 8231), (8239, 8247), (8249, 8254), (8258, 8258), (8260, 8260), (8263, 8267), (8271, 8271), (8304, 8305), (8308, 8334), (8336, 8348), (8354, 8356), (8359, 8360), (8363, 8364), (8367, 8367), (8369, 8369), (8383, 8383), (8448, 8451), (8453, 8454), (8457, 8457), (8460, 8463), (8465, 8465), (8467, 8467), (8469, 8470), (8473, 8474), (8476, 8477), (8480, 8480), (8482, 8482), (8484, 8484), (8486, 8487), (8494, 8494), (8501, 8505), (8527, 8527), (8531, 8580), (8592, 8603), (8616, 8616), (8622, 8622), (8636, 8637), (8640, 8641), (8651, 8665), (8704, 8717), (8719, 8732), (8734, 8735), (8739, 8747), (8758, 8758), (8764, 8764), (8769, 8769), (8773, 8773), (8776, 8777), (8793, 8793), (8800, 8802), (8804, 8805), (8810, 8811), (8814, 8817), (8834, 8837), (8853, 8856), (8901, 8901), (8943, 8943), (8962, 8963), (8976, 8976), (8992, 8993), (8998, 8999), (9001, 9003), (9171, 9171), (9312, 9331), (9450, 9450), (9471, 9471), (9632, 9633), (9650, 9651), (9654, 9655), (9660, 9661), (9664, 9665), (9670, 9671), (9673, 9676), (9678, 9687), (9733, 9733), (9753, 9753), (9755, 9755), (9758, 9758), (9767, 9767), (9775, 9775), (9785, 9811), (9824, 9824), (9827, 9827), (9829, 9830), (9833, 9836), (9877, 9877), (9880, 9880), (9890, 9893), (10087, 10087), (10102, 10111), (10214, 10217), (11360, 11372), (11380, 11383), (11799, 11800), (42779, 42781), (42784, 42785), (42889, 42892), (59393, 59395), (64297, 64310), (64312, 64316), (64318, 64318), (64320, 64321), (64323, 64324), (64326, 64334), (65533, 65533), (119046, 119047), (127279, 127279)];
    internal static bool SupportsScalar(Rune value)
    {
        if (value.Value is 9 or 10 or 13) return true;
        var category = Rune.GetUnicodeCategory(value);
        if (category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned)
            return false;
        // Conservative B2B policy: cmap presence alone is not proof of arbitrary
        // script shaping. Admit Latin, combining Latin and common symbols only;
        // other scripts need a separately reviewed native fidelity policy.
        if (!(value.Value is >= 32 and <= 0x024F or >= 0x0300 and <= 0x036F or
            >= 0x1E00 and <= 0x1EFF or >= 0x2000 and <= 0x206F or
            >= 0x20A0 and <= 0x20CF or >= 0x2100 and <= 0x214F)) return false;
        return GlyphRanges.Any(r => value.Value >= r.First && value.Value <= r.Last);
    }
    private static void RequireSupportedContent(byte[] canonical)
    {
        using var json = JsonDocument.Parse(canonical);
        var document = json.RootElement.GetProperty("document");
        Check(document.GetProperty("resumeTitle")); Check(document.GetProperty("header"));
        var visible = document.GetProperty("visibleSectionOrder").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (string key in visible)
            Check(document.GetProperty(key switch { "experience" => "experiences", "skills" => "skillCategories", _ => key }));
        static void Check(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                foreach (var rune in value.GetString()!.EnumerateRunes())
                    if (!SupportsScalar(rune)) throw new ResumeTemplateException(ResumeTemplateFailure.UnsupportedRenderContent, "UnsupportedRenderContent");
            }
            else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Check(item);
            else if (value.ValueKind == JsonValueKind.Object) foreach (var item in value.EnumerateObject())
                // These are retained identity data, not template display input.
                if (item.Name is not ("id" or "bulletsRaw" or "skillsCsv" or "endDate")) Check(item.Value);
        }
    }
}

