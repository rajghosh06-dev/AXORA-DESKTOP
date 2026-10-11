using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Security.Principal;
using Microsoft.Win32;
using Axora.Studio.Models.Rendering;
using Axora.Studio.Services.Rendering;
using Microsoft.Win32.SafeHandles;

namespace Axora.Studio.Tests;

internal static class RendererTests
{
    public static IReadOnlyList<string> RequiredCases { get; } = Array.AsReadOnly(new[] {
        "runtime-absent", "provisioning-required", "valid-runtime", "hash-mismatch", "wrong-version",
        "architecture-mismatch", "unexecutable", "runtime-substitution", "runtime-reparse", "startup-failure",
        "fixture-success", "unicode-probe", "binary-bytes", "pdf-bound", "stderr-bound", "dual-flood",
        "timeout", "cancel", "nonzero", "job-assignment-failure", "resume-failure", "descendant-pipe",
        "workspace-traversal", "workspace-substitution", "workspace-reparse", "fixture-tamper",
        "owned-acl", "partial-sandbox", "delete-pending", "unexpected-artifact", "busy", "active-shutdown",
        "repeated-shutdown", "unused-lazy-shutdown", "offline-clean-cache", "private-denial",
        "network-denial", "privacy", "acl-nonmutation", "admission-shutdown-race", "inplace-reparse",
        "typed-delete-pending", "native-active-shutdown", "owned-write-denial", "launch-substitution",
        "artifact-status", "profile-state", "profile-write-denial", "profile-registry-denial", "profile-control-denial", "profile-collision", "profile-registry-collision", "aap-ambient-surface"
    });
    public static async Task RunAsync(Checks c)
    {
        using var area = new Area();
        var before = area.CaptureStable();
        await c.CaseAsync("runtime-absent", async () => {
            var coordinator = new RendererCoordinator(new TypstRuntimeVerifier(Path.Combine(area.Root, "absent")), new WindowsRendererProcess(), area.Operations);
            var r = await coordinator.RenderAsync(new(Guid.NewGuid()));
            c.That(r.Outcome == RendererOutcome.RuntimeUnavailable && r.CleanupComplete, "Absent runtime is optional and typed");
            await coordinator.ShutdownAsync();
        });
        await c.CaseAsync("provisioning-required", async () => {
            string dir = Path.Combine(area.Root, "preexisting-unowned"); Directory.CreateDirectory(dir);
            string acl = Sddl(dir);
            await c.ThrowsAsync<RendererProvisioningException>(() => { area.Sandbox.EnsureOwnedDirectory(dir, true); return Task.CompletedTask; }, "Unowned existing resource requires provisioning");
            c.That(acl == Sddl(dir), "Rejected resource ACL remains byte-for-byte equivalent");
        });
        await c.CaseAsync("valid-runtime", () => {
            using var lease = area.Verifier.Verify(area.Sandbox); lease.VerifyAgain();
            c.That(lease.Path == RendererRuntimeManifest.ExecutablePath(area.Runtime), "Strict official path and identity verified");
            return Task.CompletedTask;
        });
        await c.CaseAsync("hash-mismatch", async () => {
            string root = area.MakeCorruptRuntime([1, 2, 3]);
            await c.ThrowsAsync<InvalidDataException>(() => { using var lease = new TypstRuntimeVerifier(root).Verify(area.Sandbox); return Task.CompletedTask; }, "Hash mismatch rejected before native creation");
        });
        await c.CaseAsync("wrong-version", async () => {
            var fake = new FakeProcess { Version = "typst 0.0.0" };
            var r = await area.Coordinator(fake).RenderAsync(new(Guid.NewGuid()));
            c.That(r.Outcome == RendererOutcome.RuntimeIntegrityFailure && fake.Calls == 1 && r.CleanupComplete, "Version mismatch never reaches compile");
        });
        await c.CaseAsync("architecture-mismatch", async () => {
            byte[] bytes = File.ReadAllBytes(RendererRuntimeManifest.ExecutablePath(area.Runtime));
            int pe = BitConverter.ToInt32(bytes, 0x3c); bytes[pe + 4] = 0x4c; bytes[pe + 5] = 0x01;
            string root = area.MakeCorruptRuntime(bytes);
            await c.ThrowsAsync<InvalidDataException>(() => { using var lease = new TypstRuntimeVerifier(root).Verify(area.Sandbox); return Task.CompletedTask; }, "Architecture substitution fails authenticated identity");
        });
        await c.CaseAsync("unexecutable", async () => {
            string path = Path.Combine(area.Owned, "unexecutable.exe"); area.CopyOwnedBytes(path, [1, 2, 3, 4]);
            using var lease = area.Lease(path);
            using var workspace = area.Workspace();
            var r = await new WindowsRendererProcess().RunAsync(new(lease, [], workspace, true, TimeSpan.FromSeconds(2), 1024), default);
            c.That(r.Outcome == RendererOutcome.StartupFailed && r.Settled && !r.JobMembership, "Unexecutable runtime fails and settles");
            c.That(workspace.Cleanup(), "Unexecutable workspace removed");
        });
        await c.CaseAsync("runtime-substitution", async () => {
            using var lease = area.Verifier.Verify(area.Sandbox);
            await c.ThrowsAsync<IOException>(() => { File.Move(lease.Path, lease.Path + ".moved"); return Task.CompletedTask; }, "Pinned runtime cannot be replaced");
            await c.ThrowsAsync<IOException>(() => { Directory.Move(Path.GetDirectoryName(lease.Path)!, Path.GetDirectoryName(lease.Path)! + ".moved"); return Task.CompletedTask; }, "Pinned runtime parent cannot be replaced");
            lease.VerifyAgain(); c.That(true, "Pinned runtime remains authentic");
        });
        await c.CaseAsync("runtime-reparse", async () => {
            string link = Path.Combine(area.Root, "runtime-link"); CreateJunction(link, area.Runtime);
            await c.ThrowsAsync<InvalidDataException>(() => { _ = new TypstRuntimeVerifier(link); return Task.CompletedTask; }, "Runtime reparse root rejected");
            Directory.Delete(link);
        });
        await c.CaseAsync("startup-failure", async () => {
            var r = await area.Probe("binary", hook: phase => { if (phase == "before-create") throw new Win32Exception(5); });
            c.That(r.Outcome == RendererOutcome.StartupFailed && r.Settled && r.ProcessCount == 0, "Precreation failure owns no process and settles");
        });
        RendererResult? successful = null;
        await c.CaseAsync("fixture-success", async () => {
            successful = await area.Coordinator().RenderAsync(new(Guid.NewGuid()));
            c.That(successful.Outcome == RendererOutcome.Completed, "Actual authenticated Typst compiles trusted fixture");
            c.That(successful.PdfBytes > 10 && successful.PdfBytes <= RendererLimits.Pdf && successful.PdfSha256?.Length == 64, "Binary fixture length and hash recorded without export");
            c.That(successful.AppContainer && successful.ZeroCapabilities && successful.JobMembership, "Actual compile token and Job membership proven before resume");
            c.That(successful.CleanupComplete && Directory.GetDirectories(area.Operations).Length == 0, "Actual compile settles readers/tree and removes operation namespace");
            Console.WriteLine("RENDERER-NATIVE " + JsonSerializer.Serialize(successful));
        });
        await c.CaseAsync("unicode-probe", async () => {
            var r = await area.Coordinator().RenderAsync(new(Guid.NewGuid(), RendererFixture.UnicodeProbe));
            c.That(r.Outcome == RendererOutcome.Completed && r.CleanupComplete && r.PdfBytes > 10, "Unicode fixture is a process probe; no Hindi layout verdict");
            Console.WriteLine("RENDERER-NATIVE " + JsonSerializer.Serialize(r));
        });
        await c.CaseAsync("binary-bytes", async () => {
            var r = await area.Probe("binary");
            c.That(r.Outcome == RendererOutcome.Completed && r.Output.SequenceEqual(new byte[] { 37,80,68,70,45,0,255,128,13,10 }), "NUL/non-UTF8 PDF stdout remains exact binary bytes");
            c.That(r.Settled && r.JobMembership && r.AppContainer && r.ZeroCapabilities, "Binary probe has native isolation and settlement");
        });
        await c.CaseAsync("pdf-bound", async () => {
            var r = await area.Probe("flood", outputLimit: 64);
            c.That(r.Outcome == RendererOutcome.OutputLimitExceeded && r.Output.Length == 0 && r.Settled, "PDF stdout bound terminates and discards incomplete output");
        });
        await c.CaseAsync("stderr-bound", async () => {
            var r = await area.Probe("stderr", diagnosticLimit: 64);
            c.That(r.Outcome == RendererOutcome.DiagnosticLimitExceeded && r.DiagnosticBytes > 64 && r.Settled, "Stderr bound is independent and settled");
        });
        await c.CaseAsync("dual-flood", async () => {
            var r = await area.Probe("both", outputLimit: 64, diagnosticLimit: 64);
            c.That(r.Outcome is RendererOutcome.OutputLimitExceeded or RendererOutcome.DiagnosticLimitExceeded && r.Settled, "Concurrent stdout/stderr saturation settles without deadlock");
        });
        await c.CaseAsync("timeout", async () => {
            var r = await area.Probe("hang", timeout: TimeSpan.FromMilliseconds(100));
            c.That(r.Outcome == RendererOutcome.TimedOut && r.Settled && r.DurationMs < 5000, "Native deadline kills and settles tree within bounded budget");
        });
        await c.CaseAsync("cancel", async () => {
            using var cancel = new CancellationTokenSource();
            var r = await area.Probe("hang", token: cancel.Token, hook: phase => { if (phase == "before-resume") cancel.Cancel(); });
            c.That(r.Outcome == RendererOutcome.Canceled && r.Settled && r.JobMembership, "Cancel while suspended settles without executing");
            using var later = new CancellationTokenSource(150);
            r = await area.Probe("hang", token: later.Token);
            c.That(r.Outcome == RendererOutcome.Canceled && r.Settled, "Active native cancellation settles");
        });
        await c.CaseAsync("nonzero", async () => {
            var r = await area.Probe("crash");
            c.That(r.Outcome == RendererOutcome.RendererFailed && r.ExitCode == 19 && r.Settled, "Nonzero child result is typed and settled");
        });
        await c.CaseAsync("job-assignment-failure", async () => {
            using (var job = new WindowsRendererJob())
            using (var invalid = new SafeFileHandle(IntPtr.Zero, false))
                await c.ThrowsAsync<Win32Exception>(() => { job.Assign(invalid); return Task.CompletedTask; }, "Actual AssignProcessToJobObject rejects an invalid native process handle");
            bool resumeReached = false;
            var r = await area.Probe("binary", hook: phase => {
                if (phase == "before-assign") throw new Win32Exception(5);
                if (phase == "before-resume") resumeReached = true;
            });
            c.That(r.Outcome == RendererOutcome.StartupFailed && !r.JobMembership && !resumeReached && r.Output.Length == 0 && r.Settled, "Assignment failure terminates suspended child; no execution before assignment");
        });
        await c.CaseAsync("resume-failure", async () => {
            var r = await area.Probe("binary", hook: phase => { if (phase == "before-resume") throw new Win32Exception(5); });
            c.That(r.Outcome == RendererOutcome.RendererFailed && r.JobMembership && r.Output.Length == 0 && r.Settled, "Pre-resume native failure settles assigned suspended child");
        });
        await c.CaseAsync("descendant-pipe", async () => {
            var r = await area.Probe("tree");
            c.That(r.Outcome == RendererOutcome.Completed && r.ProcessCount >= 2 && r.Settled && r.SettlementMs < 4000, "Root exit kills and settles pipe-holding descendant");
        });
        await c.CaseAsync("workspace-traversal", async () => {
            foreach (string path in new[] { @"..\elsewhere", Path.Combine(area.Root, "..", "outside"), area.Root + ":stream", @"\\server\share", @"\\?\C:\temp" })
                await c.ThrowsAsync<InvalidDataException>(() => { RendererWorkspace.RejectRedirects(path); return Task.CompletedTask; }, "Noncanonical workspace path rejected");
        });
        await c.CaseAsync("workspace-substitution", async () => {
            using var w = area.Workspace();
            await c.ThrowsAsync<IOException>(() => { Directory.Move(w.Root, w.Root + "-replaced"); return Task.CompletedTask; }, "Owned root remains pinned");
            await c.ThrowsAsync<IOException>(() => { Directory.Move(w.Input, w.Input + "-replaced"); return Task.CompletedTask; }, "Owned input remains pinned");
            c.That(w.Cleanup(), "Identity-bound workspace removal succeeds");
        });
        await c.CaseAsync("workspace-reparse", async () => {
            string link = Path.Combine(area.Root, "workspace-link"); CreateJunction(link, area.Operations);
            await c.ThrowsAsync<InvalidDataException>(() => { using var w = RendererWorkspace.Create(link, Guid.NewGuid(), RendererFixture.Ascii, area.Sandbox); return Task.CompletedTask; }, "Reparse workspace rejected");
            Directory.Delete(link);
        });
        await c.CaseAsync("fixture-tamper", async () => {
            using var w = area.Workspace();
            await c.ThrowsAsync<IOException>(() => { File.WriteAllText(w.Source, "tamper"); return Task.CompletedTask; }, "Trusted fixture write race denied by handle sharing");
            await c.ThrowsAsync<IOException>(() => { File.Move(w.Source, w.Source + ".moved"); return Task.CompletedTask; }, "Trusted fixture substitution denied");
            w.Validate(); c.That(w.Cleanup(), "Trusted bytes survive attempted tamper and cleanup");
        });
        await c.CaseAsync("owned-acl", () => {
            using var w = area.Workspace();
            foreach (string p in new[] { w.Root, w.Input, w.Source, w.Scratch, w.Packages, w.Cache }) { area.Sandbox.RequireOwned(p, true); c.That(true, "Owned resource protected exact DACL/owner/group"); }
            c.That(w.Cleanup(), "Owned ACL resources fully removed; no residual temporary ACE");
            return Task.CompletedTask;
        });
        await c.CaseAsync("partial-sandbox", async () => {
            var r = await area.Probe("binary", hook: phase => { if (phase == "sandbox-ready") throw new Win32Exception(5); });
            c.That(r.Outcome == RendererOutcome.StartupFailed && r.Settled && !r.AppContainer && !r.JobMembership, "Partial native setup unwinds without claiming execution or security success");
            using var next = new WindowsRendererSandbox();
            c.That(next.SidText == area.Sandbox.SidText, "Deterministic profile survives partial setup without lifecycle collision");
        });
        await c.CaseAsync("delete-pending", () => {
            using var w = area.Workspace();
            using var extra = OpenZeroAccess(w.Source);
            c.That(!extra.IsInvalid, "Test holds a delete-sharing resource handle");
            c.That(!w.Cleanup() && w.DeletePending, "Delete-pending is explicit failure, never namespace absence");
            extra.Dispose(); _ = w.Cleanup();
            c.That(RendererWorkspace.NamespaceAbsent(w.Root), "Test releases retained handle and physically settles resource");
            return Task.CompletedTask;
        });
        await c.CaseAsync("unexpected-artifact", () => {
            using var w = area.Workspace(); string extra = Path.Combine(w.Input, "unexpected");
            File.WriteAllText(extra, "synthetic");
            bool rejected = false; try { w.Validate(); } catch (InvalidDataException) { rejected = true; }
            c.That(rejected && !w.Cleanup(), "Unexpected files are rejected and never swept");
            c.That(File.Exists(extra), "Unrecognized artifact is preserved");
            File.Delete(extra); c.That(w.Cleanup(), "Test removes its synthetic artifact then settles owned handles");
            return Task.CompletedTask;
        });
        await c.CaseAsync("busy", async () => {
            var fake = new FakeProcess { HoldCompile = true }; var coordinator = area.Coordinator(fake);
            Task<RendererResult> active = coordinator.RenderAsync(new(Guid.NewGuid()));
            await fake.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var busy = await coordinator.RenderAsync(new(Guid.NewGuid()));
            c.That(busy.Outcome == RendererOutcome.Busy && fake.Calls == 2, "Second request cannot launch while first owns workspace/process");
            fake.Release.TrySetResult(); var first = await active; c.That(first.CleanupComplete, "First owner settles before admission release");
        });
        await c.CaseAsync("active-shutdown", async () => {
            var fake = new FakeProcess { HoldCompile = true }; var coordinator = area.Coordinator(fake);
            var active = coordinator.RenderAsync(new(Guid.NewGuid())); await fake.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var shutdown = coordinator.ShutdownAsync(); await shutdown;
            c.That((await active).Outcome == RendererOutcome.Canceled && !coordinator.IsActive, "Shutdown cancels and awaits active ownership");
            c.That((await coordinator.RenderAsync(new(Guid.NewGuid()))).Outcome == RendererOutcome.AdmissionClosed, "Shutdown closes future admission");
        });
        await c.CaseAsync("repeated-shutdown", async () => {
            var coordinator = area.Coordinator(); var first = coordinator.ShutdownAsync(); await first;
            c.That(ReferenceEquals(first, coordinator.ShutdownAsync()), "Repeated coordinator shutdown retains exact task");
        });
        await c.CaseAsync("unused-lazy-shutdown", async () => {
            int creates = 0; var session = new StudioRendererSession(() => { creates++; return area.Coordinator(); });
            await session.ShutdownAsync(); await session.ShutdownAsync();
            c.That(creates == 0 && !session.IsCreated, "Unused lazy shutdown creates no coordinator or native resources");
        });
        await c.CaseAsync("offline-clean-cache", async () => {
            using var lease = area.Verifier.Verify(area.Sandbox); using var w = area.Workspace();
            c.That(!Directory.EnumerateFileSystemEntries(w.Cache).Any() && !Directory.EnumerateFileSystemEntries(w.Packages).Any(), "Compile starts with empty owned package/cache directories");
            var r = await new WindowsRendererProcess().RunAsync(new(lease, Compile(w), w, true, TimeSpan.FromSeconds(15), RendererLimits.Pdf), default);
            c.That(r.Outcome == RendererOutcome.Completed && r.ZeroCapabilities && r.Output.Length > 10, "Authenticated offline compile succeeds without network capability");
            c.That(!Directory.EnumerateFileSystemEntries(w.Cache).Any() && !Directory.EnumerateFileSystemEntries(w.Packages).Any() && w.Cleanup(), "No package resolution/cache writes and complete cleanup");
        });
        await c.CaseAsync("private-denial", async () => {
            string dir = Path.Combine(area.Root, "private"); area.Sandbox.EnsureOwnedDirectory(dir, false);
            string file = Path.Combine(dir, "sentinel.txt"); area.CopyOwnedBytes(file, Encoding.UTF8.GetBytes("SYNTHETIC-PRIVATE-SENTINEL"), false);
            var r = await area.Probe("private", extra: file);
            c.That(r.Outcome == RendererOutcome.Completed && Encoding.ASCII.GetString(r.Output) == "PRIVATE_DENIED" && r.AppContainer, "OS denies private-file access on production native process path");
        });
        await c.CaseAsync("network-denial", async () => {
            var r = await area.Probe("network");
            c.That(r.Outcome == RendererOutcome.Completed && Encoding.ASCII.GetString(r.Output).StartsWith("NETWORK_DENIED_10013") && r.ZeroCapabilities, "OS denies socket network access with WSAEACCES, not merely timeout");
        });
        await c.CaseAsync("privacy", () => {
            string json = JsonSerializer.Serialize(successful);
            c.That(!json.Contains(area.Root, StringComparison.OrdinalIgnoreCase) && !json.Contains("synthetic fixture") && !json.Contains("SYNTHETIC-PRIVATE-SENTINEL"), "Public result excludes paths/source/private diagnostic text");
            c.That(successful?.ArtifactStatus == "UNVALIDATED FIXTURE PDF", "No PDF validation claim");
            return Task.CompletedTask;
        });
        await c.CaseAsync("acl-nonmutation", () => {
            var after = area.CaptureStable();
            foreach (string path in before.Keys) c.That(before[path] == after[path], "Preexisting/stable owner/group/DACL non-mutation: " + Path.GetFileName(path));
            c.That(Directory.GetDirectories(area.Operations).Length == 0, "No temporary operation or residual operation ACE remains");
            File.WriteAllText(Path.Combine(area.Root, "acl-nonmutation.json"), JsonSerializer.Serialize(new { before, after }));
            return Task.CompletedTask;
        });
        await c.CaseAsync("admission-shutdown-race", async () => {
            for (int i=0; i<10; i++) {
                var co = area.Coordinator(new FakeProcess());
                var render = Task.Run(() => co.RenderAsync(new(Guid.NewGuid())));
                var stop = Task.Run(co.ShutdownAsync);
                await stop; var r = await render;
                c.That(r.Outcome is RendererOutcome.Completed or RendererOutcome.Canceled or RendererOutcome.AdmissionClosed && r.CleanupComplete && !co.IsActive, "Concurrent shutdown/admission retains one settled owner");
            }
        });
        await c.CaseAsync("inplace-reparse", async () => {
            using var w=area.Workspace();
            await c.ThrowsAsync<Win32Exception>(() => { SetJunction(w.Cache, area.Root); return Task.CompletedTask; }, "Pinned directory rejects in-place reparse mutation");
            w.Validate(); c.That(w.Cleanup(), "Reparse mutation denial preserves owned cleanup");
        });
        await c.CaseAsync("typed-delete-pending", async () => {
            SafeFileHandle? extra=null; RendererWorkspace? owned=null;
            var co=new RendererCoordinator(area.Verifier,new FakeProcess(),area.Operations) { WorkspaceCreatedForTest=w=>{owned=w;extra=OpenZeroAccess(w.Source);} };
            var r=await co.RenderAsync(new(Guid.NewGuid()));
            c.That(r.Outcome==RendererOutcome.DeletePending && !r.CleanupComplete && r.PdfBytes==0, "Coordinator returns typed delete-pending failure after cleanup");
            c.That((await co.RenderAsync(new(Guid.NewGuid()))).Outcome==RendererOutcome.AdmissionClosed, "Unresolved cleanup permanently closes admission");
            await c.ThrowsAsync<IOException>(co.ShutdownAsync,"Later shutdown retains unresolved cleanup failure");
            c.That(ReferenceEquals(co.ShutdownAsync(),co.ShutdownAsync()), "Repeated failed shutdown retains exact faulted task");
            extra!.Dispose(); _=owned!.Cleanup(); owned.Dispose();
            c.That(RendererWorkspace.NamespaceAbsent(owned.Root), "Synthetic retained handle released; no final delete-pending artifact");
        });
        await c.CaseAsync("native-active-shutdown", async () => {
            string profileBefore = area.Sandbox.CaptureProfileState();
            using var release=new ManualResetEventSlim();
            var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var process=new WindowsRendererProcess { BeforePhaseForTest=phase=>{if(phase=="before-resume"){entered.TrySetResult();if(!release.Wait(5000))throw new IOException("Test barrier timeout");}} };
            var co=area.Coordinator(process); var render=co.RenderAsync(new(Guid.NewGuid()));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); var shutdown=co.ShutdownAsync(); release.Set(); await shutdown;
            var r=await render;
            c.That(r.Outcome==RendererOutcome.Canceled && r.CleanupComplete && r.JobMembership && r.AppContainer, "Active native shutdown settles assigned AppContainer and workspace");
            string profileAfter = area.Sandbox.CaptureProfileState();
            c.That(profileBefore == profileAfter, "Real authenticated Typst shutdown preserves complete profile filesystem/registry security and semantics");
            File.WriteAllText(Path.Combine(area.Root,"profile-shutdown.json"),JsonSerializer.Serialize(new {before=profileBefore,after=profileAfter,r.Outcome,r.CleanupComplete,r.JobMembership,r.AppContainer}));
        });
        await c.CaseAsync("owned-write-denial", async () => {
            var r=await area.Probe("write",extra:area.Operations);
            c.That(r.Outcome==RendererOutcome.Completed && Encoding.ASCII.GetString(r.Output)=="WRITE_DENIED", "AppContainer cannot write even app-owned renderer resources");
        });
        await c.CaseAsync("launch-substitution", async () => {
            bool denied=false;
            var r=await area.Probe("binary",hook:phase=>{if(phase=="before-create"){try{File.WriteAllBytes(area.Helper,[0]);}catch(IOException){denied=true;}}});
            c.That(denied && r.Outcome==RendererOutcome.Completed && r.Output.Length==10, "Verification-to-launch write substitution is denied under exact file lease");
        });
        await c.CaseAsync("artifact-status", () => {
            var fixture = new RendererResult(Guid.NewGuid(), RendererOutcome.Completed, "Completed", 10, new string('A',64));
            c.That(fixture.ArtifactStatus == "UNVALIDATED FIXTURE PDF", "Present successful artifact keeps unvalidated wording");
            foreach (var absent in new[] { fixture with { Outcome = RendererOutcome.RendererFailed }, fixture with { PdfBytes = 0 }, fixture with { PdfSha256 = null }, fixture with { PdfSha256 = " " } })
                c.That(absent.ArtifactStatus == "NO FIXTURE PDF ARTIFACT", "Failure, zero bytes, absent or blank hash cannot claim a PDF artifact");
            return Task.CompletedTask;
        });
        await c.CaseAsync("profile-state", async () => {
            var evidence = new List<object>();
            using var runtime = area.Verifier.Verify(area.Sandbox);
            foreach (string operation in new[] { "version", "normal", "unicode", "offline", "cancel", "timeout" })
            {
                string beforeState = area.Sandbox.CaptureProfileState();
                using var workspace = RendererWorkspace.Create(area.Operations, Guid.NewGuid(), operation == "unicode" ? RendererFixture.UnicodeProbe : RendererFixture.Ascii, area.Sandbox);
                using var cancel = new CancellationTokenSource();
                var process = new WindowsRendererProcess { BeforePhaseForTest = phase => { if (operation == "cancel" && phase == "before-resume") cancel.Cancel(); } };
                c.That(!Directory.EnumerateFileSystemEntries(workspace.Cache).Any() && !Directory.EnumerateFileSystemEntries(workspace.Packages).Any(), "Real profile-state operation starts with empty cache/package roots: " + operation);
                var result = await process.RunAsync(new(runtime, operation == "version" ? ["--version"] : Compile(workspace), workspace, operation != "version",
                    operation == "timeout" ? TimeSpan.FromTicks(1) : TimeSpan.FromSeconds(15), RendererLimits.Pdf), cancel.Token);
                var expected = operation == "cancel" ? RendererOutcome.Canceled : operation == "timeout" ? RendererOutcome.TimedOut : RendererOutcome.Completed;
                c.That(result.Outcome == expected && result.Settled && result.AppContainer && result.ZeroCapabilities && result.JobMembership, "Authenticated Typst native profile-state result: " + operation);
                if (operation is "normal" or "unicode" or "offline") c.That(result.Output.Length > 10, "Real fixture PDF produced for " + operation);
                if (operation == "version") c.That(Encoding.UTF8.GetString(result.Output).Trim() == RendererRuntimeManifest.VersionOutput, "Profile-state version is authenticated Typst");
                c.That(workspace.Cleanup(), "Profile-state workspace completely removed: " + operation);
                string afterState = area.Sandbox.CaptureProfileState();
                c.That(beforeState == afterState, "PROFILE BYTE/SEMANTICALLY UNCHANGED including registry owner/group/DACL: " + operation);
                evidence.Add(new { operation, beforeState, afterState, result.Outcome, result.Settled, result.AppContainer, result.ZeroCapabilities, result.JobMembership });
                Array.Clear(result.Output);
            }
            File.WriteAllText(Path.Combine(area.Root,"profile-state.json"),JsonSerializer.Serialize(evidence));
        });
        await c.CaseAsync("profile-write-denial", async () => {
            string profile = area.Sandbox.ProfileDirectory;
            using var w = area.Workspace(); using var helper = area.Lease(area.Helper);
            var args = ImmutableArray.Create("profile-directories", Path.GetDirectoryName(profile)!, profile, Path.Combine(profile,"INetCache"), Path.Combine(profile,"INetCookies"), Path.Combine(profile,"INetHistory"), Path.Combine(profile,"Temp"));
            string state = area.Sandbox.CaptureProfileState();
            var r = await new WindowsRendererProcess().RunAsync(new(helper,args,w,false,TimeSpan.FromSeconds(5),1024),default);
            c.That(r.Outcome==RendererOutcome.Completed && Encoding.ASCII.GetString(r.Output)=="PROFILE_ALL_SIX_DENIED_5" && r.Settled, "OS denies direct writes in every managed package/profile directory");
            c.That(w.Cleanup() && state==area.Sandbox.CaptureProfileState(), "Profile write attack leaves no file, registry, or security change");
        });
        await c.CaseAsync("profile-registry-denial", async () => {
            string state=area.Sandbox.CaptureProfileState();var r=await area.Probe("profile-registry");
            c.That(r.Outcome==RendererOutcome.Completed && Encoding.ASCII.GetString(r.Output)=="PROFILE_REGISTRY_DENIED_5", "Native profile registry is readable but writable location is E_ACCESSDENIED");
            c.That(state==area.Sandbox.CaptureProfileState(), "Registry write attack leaves exact filesystem/registry state unchanged");
        });
        await c.CaseAsync("profile-collision", async () => {
            string state=area.Sandbox.CaptureProfileState();
            using(var repeated=new WindowsRendererSandbox())c.That(state==repeated.CaptureProfileState(), "Existing hardened profile reuse does not repair ACLs or recreate storage");
            string collision=Path.Combine(area.Sandbox.ProfileDirectory,"axora-collision-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(collision);
            string acl=Sddl(collision);
            try {
                await c.ThrowsAsync<RendererProvisioningException>(()=>{using var rejected=new WindowsRendererSandbox();return Task.CompletedTask;},"Existing profile namespace collision fails closed without launch");
                c.That(acl==Sddl(collision), "Rejected profile collision ACL is never mutated");
            } finally { Directory.Delete(collision); }
            c.That(state==area.Sandbox.CaptureProfileState(), "Only synthetic collision removed; profile returns to exact managed state");
        });
        await c.CaseAsync("profile-control-denial", async () => {
            string state=area.Sandbox.CaptureProfileState();
            var r=await area.Probe("profile-control",extra:area.Sandbox.ProfileDirectory);
            c.That(r.Outcome==RendererOutcome.Completed && Encoding.ASCII.GetString(r.Output)=="PROFILE_CONTROL_DENIED", "Native filesystem/registry WRITE_DAC, WRITE_OWNER, DELETE, and write access individually denied");
            c.That(state==area.Sandbox.CaptureProfileState(), "Renderer cannot reopen managed profile for ACL/ownership/deletion changes");
        });
        await c.CaseAsync("aap-ambient-surface", async () => {
            string root=Path.Combine(area.Root,"aap-"+Guid.NewGuid().ToString("N"));string file=Path.Combine(root,"sentinel.txt");
            using var identity=WindowsIdentity.GetCurrent();string owner=identity.User!.Value;
            string sddl=$"O:{owner}G:{owner}D:P(A;;FA;;;SY)(A;;FA;;;{owner})(A;;0x1200a9;;;S-1-15-2-1)";
            if(!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl,1,out var sd,out _))throw new Win32Exception();
            try {
                var security=new WindowsRendererSandbox.SecurityAttributes{Length=Marshal.SizeOf<WindowsRendererSandbox.SecurityAttributes>(),Descriptor=sd};
                if(!CreateDirectoryW(root,ref security))throw new Win32Exception();
                using(var handle=CreateSentinel(file,0x40000000,1,ref security,1,0,IntPtr.Zero)){if(handle.IsInvalid)throw new Win32Exception();RandomAccess.Write(handle,Encoding.ASCII.GetBytes("SYNTHETIC-AAP"),0);}
                var r=await area.Probe("aap",extra:file);
                c.That(r.Outcome==RendererOutcome.Completed && Encoding.ASCII.GetString(r.Output)=="AAP_AMBIENT_READ_CONFIRMED", "Standard AppContainer retains measured AAP-only ambient read; not equated with zero capabilities");
                var descriptor=new RawSecurityDescriptor(new FileInfo(file).GetAccessControl(AccessControlSections.Access).GetSecurityDescriptorBinaryForm(),0);
                c.That(descriptor.DiscretionaryAcl!.Cast<CommonAce>().All(x=>x.SecurityIdentifier.Value!=area.Sandbox.SidText), "AAP sentinel grants no explicit renderer SID access");
                File.WriteAllText(Path.Combine(area.Root,"aap-ambient.json"),JsonSerializer.Serialize(new {Acl=sddl,Outcome=r.Outcome,Classification="Known standard AppContainer ambient AAP read; LPAC incompatible with required descendant probe (Win32 5)"}));
            } finally {LocalFree(sd);if(File.Exists(file))File.Delete(file);if(Directory.Exists(root))Directory.Delete(root);}
            c.That(RendererWorkspace.NamespaceAbsent(root),"Only synthetic AAP sentinel removed completely");
        });
        await c.CaseAsync("profile-registry-collision", async () => {
            string state=area.Sandbox.CaptureProfileState();
            string storage=@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppContainer\Storage\"+WindowsRendererSandbox.ProfileName.ToLowerInvariant();
            using var key=Registry.CurrentUser.OpenSubKey(storage,true) ?? throw new IOException("Missing managed registry storage");
            string value="AXORA_SYNTHETIC_COLLISION_"+Guid.NewGuid().ToString("N");key.SetValue(value,"SYNTHETIC",RegistryValueKind.String);
            try {
                await c.ThrowsAsync<RendererProvisioningException>(()=>{using var rejected=new WindowsRendererSandbox();return Task.CompletedTask;},"Unknown managed registry value fails admission without repair");
                c.That((string?)key.GetValue(value)=="SYNTHETIC", "Rejected registry collision remains untouched");
            } finally {key.DeleteValue(value);}
            c.That(state==area.Sandbox.CaptureProfileState(), "Only synthetic registry value removed; full managed state restored");
        });
    }
    private static ImmutableArray<string> Compile(RendererWorkspace w) => ["compile","--format","pdf","--root",w.Input,"--ignore-system-fonts","--package-path",w.Packages,"--package-cache-path",w.Cache,"--diagnostic-format","short",w.Source,"-"];
    private static string Sddl(string path)
    {
        var sections = AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group;
        FileSystemSecurity security = Directory.Exists(path) ? new DirectoryInfo(path).GetAccessControl(sections) : new FileInfo(path).GetAccessControl(sections);
        return Convert.ToHexString(security.GetSecurityDescriptorBinaryForm());
    }
    private sealed class FakeProcess : IRendererProcess
    {
        internal string Version = RendererRuntimeManifest.VersionOutput;
        internal bool HoldCompile;
        internal int Calls;
        internal TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<RendererProcessResult> RunAsync(RendererLaunch launch, CancellationToken token)
        {
            Calls++;
            RendererOutcome outcome = RendererOutcome.Completed;
            if (launch.Pdf && HoldCompile) { Entered.TrySetResult(); try { await Release.Task.WaitAsync(token); } catch (OperationCanceledException) { outcome = RendererOutcome.Canceled; } }
            return new(outcome,0,launch.Pdf ? [37,80,68,70] : Encoding.UTF8.GetBytes(Version),0,0,0,0,1,true,true,true,true);
        }
    }
    private sealed class Area : IDisposable
    {
        internal string Root { get; }
        internal string Owned { get; }
        internal string Runtime { get; }
        internal string Operations => Path.Combine(Runtime, "operations");
        internal string Helper { get; }
        internal WindowsRendererSandbox Sandbox { get; } = new();
        internal TypstRuntimeVerifier Verifier => new(Runtime);
        private readonly string _source;
        internal Area()
        {
            Root = Environment.GetEnvironmentVariable("AXORA_RENDERER_TEST_ROOT") ?? throw new InvalidOperationException("Run run-m2-renderer.ps1 to provision external native prerequisites");
            _source = Environment.GetEnvironmentVariable("AXORA_RENDERER_TEST_RUNTIME_SOURCE")!;
            Owned = Path.Combine(Root, "owned"); Sandbox.EnsureOwnedDirectory(Owned,true);
            Runtime = Path.Combine(Owned,"runtime"); TypstRuntimeVerifier.Provision(_source,Runtime,Sandbox);
            Sandbox.EnsureOwnedDirectory(Operations,true);
            Helper = Path.Combine(Owned,"probe.exe");
            CopyOwnedBytes(Helper, File.ReadAllBytes(Environment.GetEnvironmentVariable("AXORA_RENDERER_TEST_HELPER")!));
        }
        internal void CopyOwnedBytes(string path, byte[] bytes, bool readable = true)
        {
            using var stream = new FileStream(Sandbox.CreateFile(path,readable,0x40000000), FileAccess.Write);
            stream.Write(bytes); stream.Flush(true);
        }
        internal string MakeCorruptRuntime(byte[] bytes)
        {
            string root=Path.Combine(Owned,"corrupt-"+Guid.NewGuid().ToString("N"));
            foreach(string path in TypstRuntimeVerifier.OwnedDirectories(root)) Sandbox.EnsureOwnedDirectory(path,true);
            CopyOwnedBytes(RendererRuntimeManifest.ExecutablePath(root),bytes);
            return root;
        }
        internal RendererWorkspace Workspace() => RendererWorkspace.Create(Operations,Guid.NewGuid(),RendererFixture.Ascii,Sandbox);
        internal RuntimeLease Lease(string path) => new(path,new FileStream(TypstRuntimeVerifier.OpenPinnedFile(path),FileAccess.Read),RendererWorkspace.PinAncestors(path),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        internal RendererCoordinator Coordinator(IRendererProcess? process=null) => new(Verifier,process ?? new WindowsRendererProcess(),Operations);
        internal async Task<RendererProcessResult> Probe(string mode, int outputLimit=1024, int diagnosticLimit=1024, TimeSpan? timeout=null, CancellationToken token=default, Action<string>? hook=null, string? extra=null)
        {
            using var w=Workspace(); using var lease=Lease(Helper);
            var args=extra is null ? ImmutableArray.Create(mode) : ImmutableArray.Create(mode,extra);
            var r=await new WindowsRendererProcess { BeforePhaseForTest=hook }.RunAsync(new(lease,args,w,true,timeout ?? TimeSpan.FromSeconds(5),outputLimit,diagnosticLimit),token);
            bool cleanup=w.Cleanup();
            if(!cleanup)throw new IOException("Native probe workspace cleanup failed");
            Console.WriteLine("RENDERER-PROBE "+JsonSerializer.Serialize(new { mode,r.Outcome,r.ExitCode,Bytes=r.Output.Length,r.DiagnosticBytes,r.DurationMs,r.SettlementMs,r.ProcessCount,r.JobMembership,r.AppContainer,r.ZeroCapabilities,r.Settled,cleanup }));
            return r;
        }
        internal Dictionary<string,string> CaptureStable()
        {
            var paths=new List<string> { Root,Path.GetDirectoryName(Root)!,_source,Owned,Helper,Runtime,Operations };
            // Known repository locations only. The unrelated archive is never opened or enumerated.
            paths.Add(@"D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP");
            paths.Add(@"D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP\Axora-Desktop-WinUI");
            paths.AddRange(TypstRuntimeVerifier.OwnedDirectories(Runtime));
            paths.Add(RendererRuntimeManifest.ExecutablePath(Runtime));
            return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(p=>p,Sddl,StringComparer.OrdinalIgnoreCase);
        }
        public void Dispose() => Sandbox.Dispose(); // Stable external test resources retained for audit.
    }
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true, EntryPoint="CreateFileW")]
    private static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
    private static SafeFileHandle OpenZeroAccess(string path) => CreateFile(path,0,7,IntPtr.Zero,3,0x00200000,IntPtr.Zero);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string text,uint revision,out IntPtr descriptor,out uint size);
    [DllImport("kernel32.dll")]private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool CreateDirectoryW(string path,ref WindowsRendererSandbox.SecurityAttributes security);
    [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafeFileHandle CreateSentinel(string path,uint access,uint share,ref WindowsRendererSandbox.SecurityAttributes security,uint disposition,uint flags,IntPtr template);
    private static void CreateJunction(string link, string target)
    {
        Directory.CreateDirectory(link);
        SetJunction(link,target);
    }
    private static void SetJunction(string link, string target)
    {
        using var handle = CreateFile(link,0x40000000,0,IntPtr.Zero,3,0x02200000,IntPtr.Zero);
        if(handle.IsInvalid) throw new Win32Exception();
        string substitute = @"\??\" + Path.GetFullPath(target);
        byte[] sub=Encoding.Unicode.GetBytes(substitute), print=Encoding.Unicode.GetBytes(target);
        using var buffer=new MemoryStream(); using var writer=new BinaryWriter(buffer);
        writer.Write(0xA0000003u); writer.Write(checked((ushort)(8+sub.Length+2+print.Length+2))); writer.Write((ushort)0);
        writer.Write((ushort)0); writer.Write((ushort)sub.Length); writer.Write((ushort)(sub.Length+2)); writer.Write((ushort)print.Length);
        writer.Write(sub); writer.Write((ushort)0); writer.Write(print); writer.Write((ushort)0);
        byte[] bytes=buffer.ToArray();
        if(!DeviceIoControl(handle,0x000900A4,bytes,bytes.Length,IntPtr.Zero,0,out _,IntPtr.Zero)) throw new Win32Exception();
    }
    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool DeviceIoControl(SafeFileHandle file,uint code,byte[] input,int bytes,IntPtr output,int outputBytes,out int returned,IntPtr overlapped);
}

