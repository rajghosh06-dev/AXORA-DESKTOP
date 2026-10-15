using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Axora.PdfValidation;
using Axora.Studio.Models.ResumeRendering;
using Axora.Studio.Services.Rendering;
using Axora.Studio.Services.ResumeRendering;
using Microsoft.Win32.SafeHandles;

namespace Axora.Studio.Tests;

internal static class ResumePdfValidationTests
{
    internal static IReadOnlyList<string> RequiredCases { get; } = Array.AsReadOnly(new[] {
        "b2c1-protocol-ownership", "b2c1-protocol-bounds", "b2c1-reply-binding", "b2c1-build-authority",
        "b2c1-fresh-provisioning", "b2c1-bundle-authentication", "b2c1-workspace-ownership", "b2c1-native-foundation",
        "b2c1-native-protocol-cancel", "b2c1-runtime-unavailable", "b2c1-admission-shutdown", "b2c1-unresolved-authority" });
    private static PdfFoundationRequest Request(byte[]? pdf = null) => new(Guid.NewGuid(), Guid.NewGuid(),
        PdfValidationProtocol.Hash("SYNTHETIC IDENTITY"u8), PdfValidationWorkerRuntime.Manifest.Sha256, pdf ?? BenignPdf());
    private static PdfFoundationReply Reply(PdfFoundationRequest request) => new(request.RequestId, request.OperationId,
        PdfFoundationStatus.FoundationObserved, PdfFoundationCode.None, 1, 1, 32,
        PdfValidationProtocol.Hash(PdfValidationProtocol.EncodeRequest(request)), request.PdfSha256, request.InputIdentitySha256, request.BundleSha256);
    private static void Reject(Checks c, Action action, string name)
    {
        try { action(); c.That(false, name); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException or Win32Exception or UnauthorizedAccessException) { c.That(true, name); }
    }

    internal static async Task RunAsync(Checks c)
    {
        async Task CaseAsync(string id, Func<Task> body) => await c.CaseAsync(id, async () => {
            var watch = Stopwatch.StartNew();
            try { await body(); }
            finally { Console.WriteLine("B2C1-CASE " + JsonSerializer.Serialize(new { id, durationMs = watch.ElapsedMilliseconds })); }
        });
        using var f = new Fixture();
        await CaseAsync("b2c1-protocol-ownership", () => {
            byte[] pdf = BenignPdf(); var request = Request(pdf); byte[] original = (byte[])pdf.Clone(); Array.Clear(pdf);
            c.That(request.CopyPdf().SequenceEqual(original), "Protocol owns its PDF copy; caller alias cannot mutate input");
            byte[] frame = PdfValidationProtocol.EncodeRequest(request);
            using var stream = new MemoryStream(frame); var decoded = PdfValidationProtocol.ReadRequest(stream);
            c.That(decoded.RequestId == request.RequestId && decoded.OperationId == request.OperationId && decoded.PdfSha256 == request.PdfSha256 &&
                decoded.InputIdentitySha256 == request.InputIdentitySha256 && decoded.BundleSha256 == request.BundleSha256 && decoded.CopyPdf().SequenceEqual(original), "Binary frame preserves every bounded identity and payload byte");
            byte[] copy = request.CopyPdf(); Array.Clear(copy);
            c.That(request.CopyPdf().SequenceEqual(original), "Returned protocol copies cannot mutate retained authority");
            decoded.Clear(); request.Clear(); return Task.CompletedTask;
        });
        await CaseAsync("b2c1-protocol-bounds", () => {
            var request = Request(); byte[] frame = PdfValidationProtocol.EncodeRequest(request);
            foreach (int length in new[] { 0, 1, 3, 4, 12, 47, 100, 147, frame.Length - 1 })
                Reject(c, () => { using var s = new MemoryStream(frame[..length]); PdfValidationProtocol.ReadRequest(s); }, "Short request is rejected before interpretation: " + length);
            foreach (uint length in new[] { 0u, 144u, uint.MaxValue, (uint)(PdfValidationProtocol.PdfLimit + PdfValidationProtocol.RequestHeader + 1) })
            { var bad = (byte[])frame.Clone(); BinaryPrimitives.WriteUInt32LittleEndian(bad, length); RejectRequest(c, bad, "Oversize/inconsistent prefix rejects without unbounded allocation"); }
            foreach (int offset in new[] { 4, 12, 14, 52, frame.Length - 1 })
            { var bad = (byte[])frame.Clone(); bad[offset] ^= 0x7F; RejectRequest(c, bad, "Magic/version/policy/hash/payload mismatch rejected at " + offset); }
            foreach (int offset in new[] { 16, 32 })
            { var bad = (byte[])frame.Clone(); Array.Clear(bad, offset, 16); RejectRequest(c, bad, "Empty request/operation identity rejected"); }
            var badSize = (byte[])frame.Clone(); BinaryPrimitives.WriteUInt32LittleEndian(badSize.AsSpan(48), uint.MaxValue); RejectRequest(c, badSize, "Payload arithmetic is widened and bounded");
            RejectRequest(c, frame.Concat(new byte[] { 0 }).ToArray(), "Trailing byte rejected");
            RejectRequest(c, frame.Concat(frame).ToArray(), "Second frame/replay on one-shot pipe rejected");
            Reject(c, () => Request(Array.Empty<byte>()), "Zero-byte PDF frame rejected");
            request.Clear(); return Task.CompletedTask;
        });
        await CaseAsync("b2c1-reply-binding", () => {
            var request = Request(); var good = Reply(request);
            using var stream = new MemoryStream(PdfValidationProtocol.EncodeReply(good)); var decoded = PdfValidationProtocol.ReadReply(stream);
            PdfValidationProtocol.RequireReplyIdentity(decoded, request, good.RequestSha256);
            c.That(decoded == good, "Fixed reply round-trips identities and integer-only observations");
            foreach (var bad in new[] { good with { RequestId = Guid.NewGuid() }, good with { OperationId = Guid.NewGuid() },
                good with { RequestSha256 = new('0', 64) }, good with { PdfSha256 = new('0', 64) },
                good with { InputIdentitySha256 = new('0', 64) }, good with { BundleSha256 = new('0', 64) } })
                Reject(c, () => PdfValidationProtocol.RequireReplyIdentity(bad, request, good.RequestSha256), "Each reply identity must match the exact host request");
            foreach (var bad in new[] { good with { Status = (PdfFoundationStatus)99 }, good with { Code = (PdfFoundationCode)99 },
                good with { Status = PdfFoundationStatus.DependencyRejected }, good with { ParserPages = -1 }, good with { NativePages = 65 },
                good with { RasterBytes = long.MaxValue }, good with { BundleSha256 = "lowercase" },
                good with { ParserPages = 0 }, good with { NativePages = 0 }, good with { RasterBytes = 0 } })
                Reject(c, () => PdfValidationProtocol.EncodeReply(bad), "Unknown status/code and inconsistent or unbounded metrics rejected");
            byte[] bytes = PdfValidationProtocol.EncodeReply(good);
            foreach (byte[] bad in new[] { bytes[..^1], bytes.Concat(new byte[] { 1 }).ToArray() })
                Reject(c, () => { using var s = new MemoryStream(bad); PdfValidationProtocol.ReadReply(s); }, "Reply truncation/trailing bytes rejected");
            bytes[12]++; Reject(c, () => { using var s = new MemoryStream(bytes); PdfValidationProtocol.ReadReply(s); }, "Unknown reply version rejected");
            request.Clear(); return Task.CompletedTask;
        });
        await CaseAsync("b2c1-build-authority", () => {
            var manifest = PdfValidationWorkerRuntime.Manifest;
            c.That(manifest.Files.Length is > 1 and <= 512 && manifest.Files.Sum(x => x.Bytes) <= PdfWorkerManifest.ByteLimit, "Embedded build authority has finite file and byte bounds");
            c.That(manifest.Files.Select(x => x.Name).SequenceEqual(manifest.Files.Select(x => x.Name).Order(StringComparer.Ordinal)), "Manifest names are canonical ordinal order");
            foreach (string name in new[] { "../worker.exe", "..", "C:worker.exe", "x/y.dll", "CON.txt", "NUL", "name.", "bad name.dll" })
                c.That(!PdfWorkerManifest.SafeName(name), "Unsafe/device/ambiguous manifest name rejected: " + name);
            c.That(!typeof(PdfValidationWorkerHost).Assembly.GetReferencedAssemblies().Any(x => x.Name!.StartsWith("UglyToad.PdfPig", StringComparison.Ordinal)), "PdfPig remains out of Studio's assembly dependency graph");
            using var deps = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(f.Source, "Axora.PdfValidation.Worker.deps.json")));
            c.That(deps.RootElement.GetProperty("libraries").TryGetProperty("PdfPig/0.1.16", out _), "Actual worker dependency metadata pins PdfPig 0.1.16");
            c.That(manifest.Files.Any(x => x.Name == "Microsoft.Windows.SDK.NET.dll") && manifest.Files.Any(x => x.Name == "WinRT.Runtime.dll"), "Published worker carries Windows SDK projections");
            c.That(PdfValidationProtocol.Hash(File.ReadAllBytes(Path.Combine(f.Source, "PdfPig-LICENSE.txt"))) == "4C510E162F896EA43B4B1CF1B743641D7C756F1004D0E5EAF0615B28B7DED409", "Pinned upstream license is retained in authenticated bundle");
            return Task.CompletedTask;
        });
        await CaseAsync("b2c1-fresh-provisioning", () => {
            f.Provision(); using var lease = f.Runtime.Verify(f.Sandbox); lease.VerifyAgain();
            c.That(Directory.GetFiles(f.Runtime.Root).Length == PdfValidationWorkerRuntime.Manifest.Files.Length, "Fresh explicit provisioning copies exactly the embedded file set");
            Reject(c, () => PdfValidationWorkerRuntime.Provision(f.Source, f.Runtime.Root, f.Sandbox), "Existing destination is never repaired/replaced/adopted");
            Reject(c, () => { using var file = File.Open(lease.Executable, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); }, "Pinned runtime denies concurrent writes through settlement");
            return Task.CompletedTask;
        });
        await CaseAsync("b2c1-bundle-authentication", () => {
            var first = PdfValidationWorkerRuntime.Manifest.Files[0];
            c.That(first.Name == "Axora.PdfValidation.Worker.deps.json" && first.Bytes is > 1 and < 65536,
                "Identity-negative fixture targets the first small metadata file, before any other hash check");
            string? identityRoot = null;
            foreach (string kind in new[] { "extra", "missing", "length", "same-length", "renamed", "adjacent-manifest" })
            {
                // Set negatives reject before opening files. Identity negatives share one fully
                // authenticated bundle: later invalid stubs must not mask a missing SHA check.
                bool identityNegative = kind is "length" or "same-length";
                if (identityNegative && identityRoot is null)
                {
                    identityRoot = Path.Combine(f.Root, "identity-" + Guid.NewGuid().ToString("N"));
                    PdfValidationWorkerRuntime.Provision(f.Source, identityRoot, f.Sandbox);
                }
                string root = identityNegative ? identityRoot! : f.NewBundleNamespace();
                string path = Path.Combine(root, first.Name);
                if (identityNegative) File.WriteAllBytes(path, File.ReadAllBytes(Path.Combine(f.Source, first.Name)));
                switch (kind)
                {
                    case "extra": File.WriteAllBytes(Path.Combine(root, "extra.txt"), [1]); break;
                    case "missing": File.Delete(path); break;
                    case "length": File.AppendAllText(path, "x"); break;
                    case "same-length": byte[] bytes = File.ReadAllBytes(path); bytes[0] ^= 1; File.WriteAllBytes(path, bytes); break;
                    case "renamed": File.Move(path, Path.Combine(root, "renamed.txt")); break;
                    case "adjacent-manifest": File.WriteAllText(Path.Combine(root, "manifest.json"), "{}"); break;
                }
                string expected = kind is "length" or "same-length" ? "PdfWorkerBundleIdentityRejected" : "PdfWorkerBundleFileSetRejected";
                try { using var lease = new PdfValidationWorkerRuntime(root).Verify(f.Sandbox); c.That(false, "Bundle rejected for " + kind); }
                catch (InvalidDataException ex) { c.That(ex.Message == expected, "Exact production authentication branch rejects " + kind); }
            }
            string linkedRoot = f.NewBundleNamespace(), link = Path.Combine(f.Root, "owned-metadata-link.txt");
            if (!CreateHardLinkW(link, Path.Combine(linkedRoot, first.Name), IntPtr.Zero)) throw new Win32Exception();
            try {
                try { using var lease = new PdfValidationWorkerRuntime(linkedRoot).Verify(f.Sandbox); c.That(false, "Hardlink rejected"); }
                catch (InvalidDataException ex) { c.That(ex.Message == "Runtime file identity rejected", "Hardlink rejects at unchanged B1 file-pin identity check before hashing"); }
            }
            finally { File.Delete(link); }
            string junction = Path.Combine(f.Root, "owned-bundle-junction"); CreateJunction(junction, f.Runtime.Root);
            try { Reject(c, () => { _ = new PdfValidationWorkerRuntime(junction); }, "Reparse bundle root rejected before any bundle file read"); }
            finally { Directory.Delete(junction); }
            return Task.CompletedTask;
        });
        await CaseAsync("b2c1-workspace-ownership", () => {
            using var workspace = PdfValidationWorkspace.Create(f.Operations, f.Sandbox); workspace.Validate();
            c.That(!Directory.GetFiles(workspace.Root).Any() && !Directory.EnumerateFileSystemEntries(workspace.Scratch).Any(), "Operation stages no PDF, metadata or source file");
            workspace.MarkRunning(); c.That(!workspace.Cleanup() && Directory.Exists(workspace.Root), "Unsettled operation cannot delete its namespace");
            workspace.MarkSettled(); c.That(workspace.Cleanup() && RendererWorkspace.NamespaceAbsent(workspace.Root), "Settled exact-handle workspace cleanup removes the owned namespace");
            return Task.CompletedTask;
        });
        await CaseAsync("b2c1-native-foundation", async () => {
            string state = f.Sandbox.CaptureProfileState();
            foreach (var choice in Enum.GetValues<ResumeTemplateChoice>())
            {
                var candidate = await f.Compile(choice);
                var host = f.Host(new PdfFoundationProcess());
                var result = await host.ProbeAsync(candidate); await host.ShutdownAsync();
                c.That(result.Outcome == PdfFoundationOutcome.FoundationObserved && result.Observation is { ParserPages: > 0, NativePages: > 0, RasterBytes: > 0 }, "Actual B2B candidate exercises PdfPig and in-memory Windows native raster through the fixed worker: " + choice);
                c.That(result.Settled && result.CleanupComplete && result.InputSettled && result.JobMembership && result.AppContainer && result.ZeroCapabilities && result.FinalActiveProcesses == 0,
                    "Production-shaped worker has zero-capability AppContainer, Job ownership and fully settled duplex pipes");
                c.That(result.ArtifactStatus == "FOUNDATION ONLY — PDF NOT VALIDATED" && result.Observation!.PdfSha256 == candidate.PdfSha256, "Dependency observations bind bytes but grant no PDF authority");
                Console.WriteLine("B2C1-FOUNDATION " + JsonSerializer.Serialize(new { template = choice.ToString(), result }));
            }
            c.That(state == f.Sandbox.CaptureProfileState() && !Directory.EnumerateFileSystemEntries(f.Operations).Any(), "Native dependency probes leave profile unchanged and no operation namespaces");
        });
        await CaseAsync("b2c1-native-protocol-cancel", async () => {
            using (var lease = f.Runtime.Verify(f.Sandbox))
            using (var workspace = PdfValidationWorkspace.Create(f.Operations, f.Sandbox))
            {
                byte[] prefix = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(prefix, uint.MaxValue);
                var rejected = await new PdfFoundationProcess().RunAsync(lease, workspace, prefix, default);
                c.That(rejected.Outcome == PdfFoundationOutcome.WorkerFailed && rejected.Output.Length == 0 && rejected.Settled && rejected.InputSettled && rejected.FinalActive == 0,
                    "Benign malformed framing is rejected by the real worker and both IPC directions settle");
                c.That(workspace.Cleanup(), "Malformed framing leaves no operation namespace");
            }
            using var cancel = new CancellationTokenSource(); bool reached = false;
            var native = new PdfFoundationProcess { BeforeResumeForTest = () => { reached = true; cancel.Cancel(); } };
            var host = f.Host(native); var result = await host.ProbeAsync(f.Candidate(), cancel.Token); await host.ShutdownAsync();
            c.That(reached && result.Outcome == PdfFoundationOutcome.Canceled && result.Observation is null && result.Settled && result.InputSettled && result.CleanupComplete && result.FinalActiveProcesses == 0,
                "Cancellation after real suspended creation settles Job and duplex pipes before returning");
        });
        await CaseAsync("b2c1-runtime-unavailable", async () => {
            var unavailable = new PdfValidationWorkerHost(new PdfValidationWorkerRuntime(Path.Combine(f.Root, "absent-worker")), f.Operations, new NeverProcess());
            var result = await unavailable.ProbeAsync(f.Candidate()); await unavailable.ShutdownAsync();
            c.That(result.Outcome == PdfFoundationOutcome.RuntimeUnavailable && result.Observation is null && !Directory.Exists(Path.Combine(f.Root, "absent-worker")), "Missing bundle returns explicit unavailability without discovery/provision/fallback");
            var lazy = f.Host(new NeverProcess()); await lazy.ShutdownAsync(); await lazy.ShutdownAsync();
            c.That((await lazy.ProbeAsync(f.Candidate())).Outcome == PdfFoundationOutcome.AdmissionClosed, "Unused lazy shutdown closes admission without worker execution");
        });
        await CaseAsync("b2c1-admission-shutdown", async () => {
            var process = new ControlledProcess(); var host = f.Host(process);
            Task<PdfFoundationResult> first = host.ProbeAsync(f.Candidate()); await process.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            c.That((await host.ProbeAsync(f.Candidate())).Outcome == PdfFoundationOutcome.Busy, "Only one owned operation can be active");
            Task stop = host.ShutdownAsync(); await stop; var result = await first;
            c.That(result.Outcome == PdfFoundationOutcome.Canceled && result.CleanupComplete && result.Settled && result.Observation is null, "Active shutdown cancels and waits for lifecycle ownership to settle (controlled process)");
            c.That(ReferenceEquals(stop, host.ShutdownAsync()) && (await host.ProbeAsync(f.Candidate())).Outcome == PdfFoundationOutcome.AdmissionClosed, "Repeated shutdown shares completion and never reopens admission");
            foreach (string kind in new[] { "reply-replay", "reply-truncated", "pipe-limit", "missing-isolation" })
            {
                var h = f.Host(new ReplyProcess(kind)); var r = await h.ProbeAsync(f.Candidate()); await h.ShutdownAsync();
                var expected = kind == "pipe-limit" ? PdfFoundationOutcome.PipeLimit : kind == "missing-isolation" ? PdfFoundationOutcome.WorkerFailed : PdfFoundationOutcome.ProtocolRejected;
                c.That(r.Outcome == expected && r.Observation is null && r.CleanupComplete,
                    "Host rejects controlled malformed/limit result without authority: " + kind);
            }
        });
        await CaseAsync("b2c1-unresolved-authority", async () => {
            var partial = new PdfValidationWorkerHost(f.Runtime, f.Operations, new NeverProcess()) { WorkspaceSetupForTest = path => {
                File.WriteAllText(Path.Combine(path, "owned-setup-fixture.txt"), "SYNTHETIC"); throw new IOException("Synthetic setup interruption"); } };
            var setup = await partial.ProbeAsync(f.Candidate());
            c.That(setup.Outcome == PdfFoundationOutcome.CleanupFailed && !setup.CleanupComplete && setup.Observation is null &&
                (await partial.ProbeAsync(f.Candidate())).Outcome == PdfFoundationOutcome.AdmissionClosed, "Partial setup with unresolved owned content closes admission without deleting it");
            await c.ThrowsAsync<IOException>(() => partial.ShutdownAsync(), "Partial setup cleanup is never silently reported complete");
            var inconsistent = f.Host(new ActiveProcess()); var active = await inconsistent.ProbeAsync(f.Candidate());
            c.That(!active.Settled && !active.CleanupComplete && active.FinalActiveProcesses == 1 && active.Observation is null &&
                (await inconsistent.ProbeAsync(f.Candidate())).Outcome == PdfFoundationOutcome.AdmissionClosed, "A live process count overrides an inconsistent settled flag and preserves ownership");
            await c.ThrowsAsync<IOException>(() => inconsistent.ShutdownAsync(), "Inconsistent process settlement cannot satisfy shutdown");
            var host = f.Host(new UnsettledProcess()); var result = await host.ProbeAsync(f.Candidate());
            c.That(!result.Settled && !result.CleanupComplete && result.Observation is null && (await host.ProbeAsync(f.Candidate())).Outcome == PdfFoundationOutcome.AdmissionClosed,
                "Controlled unresolved settlement preserves resources and permanently closes admission");
            await c.ThrowsAsync<IOException>(() => host.ShutdownAsync(), "Unresolved shutdown is explicit failure, never a completed cleanup claim");
            c.That(!typeof(PdfFoundationResult).IsPublic && !typeof(PdfValidationWorkerHost).IsPublic && !typeof(PdfFoundationResult).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Any(p => p.PropertyType == typeof(byte[])), "Foundation evidence has no public trust/byte artifact authority");
            c.That(!typeof(PdfValidationWorkerHost).Assembly.GetTypes().Any(t => t.Name == "ValidatedResumePdf"), "No validated-PDF type or promotion added while H1/M1 remain open");
        });
    }

    private static void RejectRequest(Checks c, byte[] bytes, string message)
        => Reject(c, () => { using var stream = new MemoryStream(bytes); PdfValidationProtocol.ReadRequest(stream); }, message);
    private static byte[] BenignPdf()
    {
        var text = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int> { 0 };
        string[] objects = ["<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << >> /Contents 4 0 R >>", "<< /Length 0 >>\nstream\n\nendstream"];
        for (int i = 0; i < objects.Length; i++) { offsets.Add(text.Length); text.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        int xref = text.Length; text.Append("xref\n0 5\n0000000000 65535 f \n");
        foreach (int offset in offsets.Skip(1)) text.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        text.Append($"trailer\n<< /Size 5 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(text.ToString());
    }
    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Environment.GetEnvironmentVariable("AXORA_RENDERER_TEST_ROOT") ?? throw new InvalidOperationException("Prepared native test context required"), "b2c1-" + Guid.NewGuid().ToString("N"));
        internal string Source => Path.Combine(AppContext.BaseDirectory, "PdfFoundationTestBundle");
        internal string Operations => Path.Combine(Root, "operations");
        internal WindowsRendererSandbox Sandbox { get; } = new();
        internal PdfValidationWorkerRuntime Runtime { get; }
        private string? typst;
        internal Fixture() { Directory.CreateDirectory(Root); Sandbox.EnsureOwnedDirectory(Operations, true); Runtime = new(Path.Combine(Root, "worker")); }
        internal void Provision() => PdfValidationWorkerRuntime.Provision(Source, Runtime.Root, Sandbox);
        internal string NewBundleNamespace()
        {
            string root = Path.Combine(Root, "namespace-" + Guid.NewGuid().ToString("N"));
            using var parents = RendererWorkspace.PinAncestors(root);
            using (Sandbox.CreatePinnedDirectory(root, true))
                foreach (var entry in PdfValidationWorkerRuntime.Manifest.Files)
                    using (Sandbox.CreateFile(Path.Combine(root, entry.Name), true, 0x40000000)) { }
            return root;
        }
        internal PdfValidationWorkerHost Host(IPdfFoundationProcess process) => new(Runtime, Operations, process);
        internal UnvalidatedResumePdf Candidate() => new(Guid.NewGuid(), TrustedRenderPackage.Create(ResumeTemplateTests.Snapshot(),
            ResumeTemplateChoice.SimpleV1, ResumeRenderFontChoice.ExplicitEmbeddedSerifV1), BenignPdf());
        internal async Task<UnvalidatedResumePdf> Compile(ResumeTemplateChoice choice)
        {
            if (typst is null)
            { typst = Path.Combine(Root, "typst-source"); TypstRuntimeVerifier.Provision(Environment.GetEnvironmentVariable("AXORA_RENDERER_TEST_RUNTIME_SOURCE")!, typst, Sandbox); }
            var compiler = new ResumeTypstCompiler(new TypstRuntimeVerifier(typst), new WindowsRendererProcess());
            var result = await compiler.CompileAsync(ResumeTemplateTests.Snapshot(), choice, ResumeRenderFontChoice.ExplicitEmbeddedSerifV1);
            await compiler.ShutdownAsync();
            return result.Candidate ?? throw new InvalidOperationException("Benign candidate compile unavailable");
        }
        public void Dispose() => Sandbox.Dispose(); // Test-owned runtime assets retained; operations clean themselves or explicitly quarantine.
    }
    private sealed class NeverProcess : IPdfFoundationProcess
    { public Task<PdfFoundationNativeResult> RunAsync(PdfWorkerBundleLease b, PdfValidationWorkspace w, byte[] f, CancellationToken t) => throw new InvalidOperationException("Unexpected worker invocation"); }
    private sealed class ControlledProcess : IPdfFoundationProcess
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<PdfFoundationNativeResult> RunAsync(PdfWorkerBundleLease b, PdfValidationWorkspace w, byte[] f, CancellationToken token)
        { Entered.TrySetResult(); try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { } return new(PdfFoundationOutcome.Canceled, []); }
    }
    private sealed class ReplyProcess(string kind) : IPdfFoundationProcess
    {
        public Task<PdfFoundationNativeResult> RunAsync(PdfWorkerBundleLease b, PdfValidationWorkspace w, byte[] frame, CancellationToken token)
        {
            if (kind == "pipe-limit") return Task.FromResult(new PdfFoundationNativeResult(PdfFoundationOutcome.PipeLimit, []));
            using var input = new MemoryStream(frame); var request = PdfValidationProtocol.ReadRequest(input); var reply = Reply(request);
            if (kind == "reply-replay") reply = reply with { RequestId = Guid.NewGuid() };
            byte[] output = PdfValidationProtocol.EncodeReply(reply); request.Clear();
            return Task.FromResult(new PdfFoundationNativeResult(PdfFoundationOutcome.FoundationObserved, kind == "reply-truncated" ? output[..^1] : output,
                JobMembership: kind != "missing-isolation", AppContainer: true, ZeroCapabilities: true, InputWritten: true));
        }
    }
    private sealed class UnsettledProcess : IPdfFoundationProcess
    { public Task<PdfFoundationNativeResult> RunAsync(PdfWorkerBundleLease b, PdfValidationWorkspace w, byte[] f, CancellationToken t) => Task.FromResult(new PdfFoundationNativeResult(PdfFoundationOutcome.SettlementFailed, [], Settled: false, InputSettled: false)); }
    private sealed class ActiveProcess : IPdfFoundationProcess
    { public Task<PdfFoundationNativeResult> RunAsync(PdfWorkerBundleLease b, PdfValidationWorkspace w, byte[] f, CancellationToken t) => Task.FromResult(new PdfFoundationNativeResult(PdfFoundationOutcome.WorkerFailed, [], FinalActive: 1)); }

    // Benign, test-owned filesystem identities only; no font/PDF attack fixture.
    private static void CreateJunction(string link, string target)
    {
        Directory.CreateDirectory(link);
        using var handle = CreateFileW(link, 0x40000000, 0, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception();
        byte[] sub = Encoding.Unicode.GetBytes(@"\??\" + Path.GetFullPath(target)), print = Encoding.Unicode.GetBytes(target);
        using var buffer = new MemoryStream(); using var writer = new BinaryWriter(buffer);
        writer.Write(0xA0000003u); writer.Write(checked((ushort)(8 + sub.Length + 2 + print.Length + 2))); writer.Write((ushort)0);
        writer.Write((ushort)0); writer.Write((ushort)sub.Length); writer.Write((ushort)(sub.Length + 2)); writer.Write((ushort)print.Length);
        writer.Write(sub); writer.Write((ushort)0); writer.Write(print); writer.Write((ushort)0); byte[] bytes = buffer.ToArray();
        if (!DeviceIoControl(handle, 0x000900A4, bytes, bytes.Length, IntPtr.Zero, 0, out _, IntPtr.Zero)) throw new Win32Exception();
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateHardLinkW(string path, string existing, IntPtr security);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool DeviceIoControl(SafeFileHandle file, uint code, byte[] input, int bytes, IntPtr output, int outputBytes, out int returned, IntPtr overlapped);
}
