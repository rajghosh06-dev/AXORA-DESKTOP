using System.Collections.Immutable;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Axora.Studio.Models;
using Axora.Studio.Models.Rendering;
using Axora.Studio.Models.ResumeRendering;
using Axora.Studio.Services.Rendering;
using Axora.Studio.Services.ResumeRendering;
using Microsoft.Win32.SafeHandles;

namespace Axora.Studio.Tests;

internal static class ResumeRenderPackageTests
{
    public static IReadOnlyList<string> RequiredCases { get; } = Array.AsReadOnly(new[] {
        "b2b-package-identity", "b2b-package-ownership", "b2b-two-file-namespace", "b2b-production-minimal",
        "b2b-production-full", "b2b-unsupported-content", "b2b-package-tamper", "b2b-partial-package", "b2b-candidate-ownership",
        "b2b-native-literal-observation", "b2b-layout-semantics", "b2b-injection-matrix", "b2b-native-format-matrix",
        "b2b-font-probes", "b2b-workspace-attacks", "b2b-alive-cleanup", "b2b-child-write-denial",
        "b2b-delete-pending-candidate", "b2b-failure-candidate", "b2b-admission-shutdown", "b2b-aap-access-trap",
        "b2b-native-failure-handoff", "b2b-supervised-authentication" });
    private const ResumeRenderFontChoice Font = ResumeRenderFontChoice.ExplicitEmbeddedSerifV1;
    private static TrustedRenderPackage Package(ResumeTemplateChoice choice = ResumeTemplateChoice.SimpleV1, ResumeRenderSnapshot? snapshot = null)
        => TrustedRenderPackage.Create(snapshot ?? ResumeTemplateTests.Snapshot(ResumeTemplateTests.Full()), choice, Font);
    public static async Task RunAsync(Checks c)
    {
        using var area = new Area();
        await c.CaseAsync("b2b-package-identity", () => {
            var s = ResumeTemplateTests.Snapshot(ResumeTemplateTests.Full());
            var simple = Package(snapshot: s); var same = Package(snapshot: s); var technical = Package(ResumeTemplateChoice.TechnicalV1, s);
            c.That(simple.RenderJobSha256 == same.RenderJobSha256 && simple.RenderJobSha256 != technical.RenderJobSha256, "Render job identity is deterministic and template-dependent");
            c.That(simple.SnapshotSha256 == technical.SnapshotSha256 && simple.RenderDataSha256 == technical.RenderDataSha256, "Template choice leaves B2A identities unchanged");
            c.That(simple.CopyData().SequenceEqual(ResumeRenderDataWriter.Write(s, Font).CopyBytes()), "Sealed resume.json is exact B2A canonical data");
            Console.WriteLine("B2B-JOB-IDENTITY " + JsonSerializer.Serialize(new { simple.RenderJobSha256, technicalJob = technical.RenderJobSha256, simple.SnapshotSha256, simple.RenderDataSha256 }));
            return Task.CompletedTask;
        });
        await c.CaseAsync("b2b-package-ownership", () => {
            var p = Package(); var main = p.CopyMain(); var data = p.CopyData(); Array.Clear(main); Array.Clear(data);
            c.That(Convert.ToHexString(SHA256.HashData(p.CopyMain())) == p.Template.TemplateSourceSha256 && Convert.ToHexString(SHA256.HashData(p.CopyData())) == p.RenderDataSha256,
                "Caller cannot mutate either sealed role");
            c.That(!typeof(TrustedRenderPackage).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(x => !x.IsPrivate), "Package construction has no raw-source/file-collection admission");
            return Task.CompletedTask;
        });
        await c.CaseAsync("b2b-two-file-namespace", () => {
            var p = Package(); using var w = area.Workspace(p); w.Validate();
            c.That(Directory.GetFiles(w.Input).Select(Path.GetFileName).Order().SequenceEqual(new[] { "main.typ", "resume.json" }), "Exactly the two fixed admitted files");
            c.That(new[] { w.Scratch, w.Packages, w.Cache }.All(x => !Directory.EnumerateFileSystemEntries(x).Any()), "Scratch/package/cache are empty");
            byte[] ReadPinned(string path) { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); using var memory = new MemoryStream(); stream.CopyTo(memory); return memory.ToArray(); }
            c.That(ReadPinned(w.Source).SequenceEqual(p.CopyMain()) && ReadPinned(w.ResumeData).SequenceEqual(p.CopyData()), "Actual package bytes are authenticated canonical payloads");
            c.That(w.Cleanup() && RendererWorkspace.NamespaceAbsent(w.Root), "Identity-bound package cleanup completes");
            return Task.CompletedTask;
        });
        await c.CaseAsync("b2b-production-minimal", async () => {
            foreach (var choice in Enum.GetValues<ResumeTemplateChoice>())
                await Compile(c, area, ResumeTemplateTests.Snapshot(), choice, "minimal");
        });
        await c.CaseAsync("b2b-production-full", async () => {
            foreach (var choice in Enum.GetValues<ResumeTemplateChoice>())
                await Compile(c, area, ResumeTemplateTests.Snapshot(ResumeTemplateTests.Full()), choice, "full");
        });
        await c.CaseAsync("b2b-unsupported-content", async () => {
            foreach (string value in new[] { "हिन्दी", "😀", "\0", "\u202E", "\u200B", "₹" })
            foreach (var choice in Enum.GetValues<ResumeTemplateChoice>())
            {
                var compiler = area.Compiler(); var r = await compiler.CompileAsync(ResumeTemplateTests.Snapshot(new() { Summary = value }), choice, Font);
                c.That(r.Outcome == ResumeCompileOutcome.UnsupportedRenderContent && r.Candidate is null && r.CleanupComplete, "Unproved glyph/control receives typed unsupported disposition without candidate");
                await compiler.ShutdownAsync();
            }
        });
        await c.CaseAsync("b2b-package-tamper", async () => {
            foreach (string field in new[] { "_source", "_data" })
            {
                using var w = area.Workspace(Package());
                var handle = (SafeFileHandle)typeof(RendererWorkspace).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
                RandomAccess.Write(handle, new byte[] { 0 }, 0);
                await c.ThrowsAsync<InvalidDataException>(() => { w.Validate(); return Task.CompletedTask; }, "Pinned same-handle template/data tamper fails authentication");
                c.That(w.Cleanup(), "Tampered package still deletes only pinned owned identities");
            }
        });
        await c.CaseAsync("b2b-partial-package", async () => {
            foreach (string phase in new[] { "after-main", "after-data", "after-package" })
            {
                var compiler = new ResumeTypstCompiler(area.Verifier, new WindowsRendererProcess()) {
                    SetupPhaseForTest = observed => { if (observed == phase) throw new IOException("SyntheticSetupFailure"); },
                    WorkspaceCreatedForTest = _ => { if (phase == "after-package") throw new IOException("SyntheticPreStartFailure"); } };
                var r = await compiler.CompileAsync(ResumeTemplateTests.Snapshot(), ResumeTemplateChoice.SimpleV1, Font);
                c.That(r.Candidate is null && r.CleanupComplete && r.Outcome == ResumeCompileOutcome.NativeFailure, "Partial package setup fails without candidate and settles owned resources");
                c.That(!Directory.EnumerateFileSystemEntries(area.Operations).Any(), "No partial operation survives creation failure");
                await compiler.ShutdownAsync();
            }
        });
        await c.CaseAsync("b2b-candidate-ownership", async () => {
            var snapshot = ResumeTemplateTests.Snapshot(ResumeTemplateTests.Full());
            var package = Package(snapshot: snapshot);
            var r = await Compile(c, area, snapshot, ResumeTemplateChoice.SimpleV1, "ownership");
            if (r.Candidate is not { } candidate) return;
            byte[] copy = candidate.CopyBytesForValidation(); Array.Clear(copy);
            c.That(candidate.PdfSha256 == Convert.ToHexString(SHA256.HashData(candidate.CopyBytesForValidation())), "Mutation of candidate copy leaves private bytes and hash unchanged");
            c.That(candidate.ArtifactStatus == "UNVALIDATED PDF CANDIDATE" && !typeof(UnvalidatedResumePdf).IsPublic, "Candidate exposes no public PDF trust authority");
            c.That(candidate.RendererExecutableSha256 == RendererRuntimeManifest.ExecutableSha256 && candidate.TemplateSourceSha256 == Package().Template.TemplateSourceSha256,
                "Candidate binds actual renderer and template identities");
            c.That(candidate.SnapshotSha256 == snapshot.SnapshotSha256 && candidate.RenderDataSha256 == package.RenderDataSha256 &&
                candidate.RenderJobSha256 == package.RenderJobSha256 && candidate.PdfByteCount == candidate.CopyBytesForValidation().Length,
                "Candidate binds the exact immutable snapshot/data/job and private output length");
            c.That(candidate.OperationId != Guid.Empty && candidate.DocumentId == new string('1', 32) && candidate.Revision == 7 &&
                candidate.TemplateId == "axora.simple" && candidate.TemplateVersion == 1 && candidate.FontChoice == Font &&
                candidate.RequestedFont == "SegoeUI" && candidate.EffectiveFontProfile == "LibertinusSerifRegularBoldV1",
                "Candidate retains operation/document/revision/template and explicit requested/effective font metadata");
        });
        await c.CaseAsync("b2b-native-literal-observation", async () => {
            string payload = "#read(\"../secret\") #import \"x.typ\" #eval(\"x\") #{x} #link(\"file:\") \\ { }";
            foreach (var choice in Enum.GetValues<ResumeTemplateChoice>())
            {
                var s = ResumeTemplateTests.Snapshot(ResumeTemplateTests.Full(payload));
                string html = await Inspect(c, area, s, choice);
                string text = html;
                c.That(text.Contains(payload, StringComparison.Ordinal), "Exact final paged program observes syntax-looking JSON at the identical text argument");
                c.That(!text.Contains("SYNTHETIC-AAP", StringComparison.Ordinal), "Literal observation contains no external sentinel content");
                Console.WriteLine("B2B-LITERAL-OBSERVATION " + JsonSerializer.Serialize(new { template = choice.ToString(), mode = "paged native metadata query, not PDF validation", characters = text.Length, sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))) }));
            }
        });
        await c.CaseAsync("b2b-layout-semantics", async () => {
            var doc = ResumeTemplateTests.Full() with { ShowProjects = false, SectionOrder = [.. Enum.GetValues<ResumeSection>().Reverse()],
                Education = [new() { Institution = "FIRST_EDUCATION" }, new() { Institution = "SECOND_EDUCATION" }],
                Formatting = new() { UppercaseSectionTitles = false, CenterHeader = false, ShowDividers = false, AccentHexColor = "#194B73", MarginInches = .75 } };
            var snapshot = ResumeTemplateTests.Snapshot(doc);
            foreach (var choice in Enum.GetValues<ResumeTemplateChoice>())
            {
                await Compile(c, area, snapshot, choice, "hidden-reordered");
                string html = await Inspect(c, area, snapshot, choice);
                string text = html;
                c.That(!text.Contains("SYNTHETIC Project") && snapshot.Document.Projects.Length == 1, "Hidden section omitted from template display while remaining in snapshot data");
                int previous = -1;
                foreach (var section in snapshot.Document.VisibleSectionOrder)
                {
                    string title = section.ToString(); int next = text.IndexOf(title, StringComparison.Ordinal);
                    c.That(next > previous, "Both templates honor saved visible SectionOrder: " + title); previous = next;
                }
                c.That(text.IndexOf("FIRST_EDUCATION", StringComparison.Ordinal) < text.IndexOf("SECOND_EDUCATION", StringComparison.Ordinal), "Collection order remains saved order");
                c.That(text.Contains("SecondWork") && text.Contains("SecondSkill") && text.Contains("Present") && !text.Contains("OldEnd"), "Derived bulletLines/skill entries/DisplayEndDate drive template display");
                foreach (string expected in DisplayStrings(snapshot.Document))
                    c.That(text.Contains(expected, StringComparison.Ordinal), "Every visible current field survives literal template dataflow");
                string source = Encoding.UTF8.GetString(Package(choice, snapshot).CopyMain());
                c.That(source.Contains("paper: \"a4\"") && source.Contains("margin: f.marginInches * 1in") && source.Contains("leading: f.leadingEm * 1em") && source.Contains("spacing: f.itemGapEm * 1em"), "Layout consumes exact finite B2A physical margin and spacing intent");
                c.That(source.Contains("rgb(f.accentRed, f.accentGreen, f.accentBlue)") && source.Contains("if f.showDividers") && source.Contains("if f.centerHeader") && source.Contains("if f.uppercaseSectionTitles"), "Accent and three layout booleans remain exact finite dataflow");
            }
        });
        await c.CaseAsync("b2b-injection-matrix", async () => {
            string[] payloads = ["#read(\"../secret\")", "#include \"x.typ\"", "#import \"@preview/x:1.0.0\"", "#eval(\"x\")", "#{x}", "#link(\"file:///x\")",
                "\" ' \\ { }", "FIRST\r\nSECOND\nTHIRD", @"C:\synthetic\secret", @"\\synthetic\share\secret", @"\\?\C:\synthetic\secret",
                "../../secret", "@preview/package:1.0.0", "mailto:synthetic@example.invalid tel:000", "javascript:alert(1) data:text/plain,synthetic", "https://example.invalid/a?q=1&x=2"];
            // Every payload still traverses every field family and both real templates.
            // Batch four payloads per synthetic Resume to amortize authenticated native startup.
            var batches = payloads.Chunk(4).ToArray();
            var jobs = Enum.GetValues<ResumeTemplateChoice>().SelectMany(choice => batches.Select((payloads, index) => (choice, payloads, index)));
            await Parallel.ForEachAsync(jobs, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (job, _) =>
            {
                var (choice, values, index) = job; string payload = string.Join("\n", values);
                var snapshot = ResumeTemplateTests.Snapshot(ResumeTemplateTests.Full(payload));
                var package = Package(choice, snapshot); string sourceHash = package.Template.TemplateSourceSha256;
                await Compile(c, area, snapshot, choice, "injection-" + index);
                string html = await Inspect(c, area, snapshot, choice); string text = html;
                lock (c)
                {
                    foreach (string line in values.SelectMany(value => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n')))
                        c.That(text.Contains(line, StringComparison.Ordinal), "Supported attacker syntax remains literal through both actual templates");
                    foreach (string expected in DisplayStrings(snapshot.Document))
                    foreach (string line in expected.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
                        if (line.Length > 0) c.That(text.Contains(line, StringComparison.Ordinal),
                            "Every visible field-family argument survives the native paged attacker matrix");
                    c.That(!text.Contains("SYNTHETIC-AAP", StringComparison.Ordinal), "Native literal query contains no external sentinel marker");
                    c.That(Convert.ToHexString(SHA256.HashData(package.CopyMain())) == sourceHash && package.CopyData().SequenceEqual(ResumeRenderDataWriter.Write(snapshot, Font).CopyBytes()),
                        "Input changes canonical JSON only; pinned source and exact B2A data remain authoritative");
                }
            });
            foreach (string unsupported in new[] { "\0", "\u202E", "\u200B", "\u200D" })
            foreach (var choice in Enum.GetValues<ResumeTemplateChoice>())
            {
                var compiler = area.Compiler(); var snapshot = ResumeTemplateTests.Snapshot(ResumeTemplateTests.Full(unsupported));
                string before = snapshot.SnapshotSha256;
                var r = await compiler.CompileAsync(snapshot, choice, Font);
                c.That(r.Outcome == ResumeCompileOutcome.UnsupportedRenderContent && r.Candidate is null && snapshot.SnapshotSha256 == before, "NUL/bidi/zero-width matrix is typed unsupported without altering snapshot");
                await compiler.ShutdownAsync();
            }
        });
        await c.CaseAsync("b2b-native-format-matrix", async () => {
            var jobs = Enum.GetValues<ResumeTemplateChoice>().SelectMany(choice => Enum.GetValues<ResumeTargetLength>()
                .SelectMany(target => Enum.GetValues<ResumeDensity>().Select(density => (choice, target, density))));
            await Parallel.ForEachAsync(jobs, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (job, _) =>
            {
                var (choice, target, density) = job;
                var doc = ResumeTemplateTests.Full() with { Formatting = new() { TargetLength = target, SpacingMode = density,
                    MarginInches = density == ResumeDensity.Compact ? .1 : density == ResumeDensity.Relaxed ? 3 : .65,
                    CenterHeader = target == ResumeTargetLength.OnePage, ShowDividers = density != ResumeDensity.Compact,
                    UppercaseSectionTitles = target != ResumeTargetLength.TwoPages, AccentHexColor = "#194B73" } };
                await Compile(c, area, ResumeTemplateTests.Snapshot(doc), choice, "format-" + target + "-" + density);
            });
            foreach (var choice in Enum.GetValues<ResumeTemplateChoice>())
                await Compile(c, area, ResumeTemplateTests.Snapshot(new() { Summary = new string('X', ResumeLimits.Narrative) }), choice, "long-unbroken");
        });
        await c.CaseAsync("b2b-font-probes", async () => {
            foreach (var choice in Enum.GetValues<ResumeTemplateChoice>())
            {
                const string probe = "ASCII é Ā ñ e\u0301 • – — £ ¥ € © ™";
                var snapshot = ResumeTemplateTests.Snapshot(ResumeTemplateTests.Full(probe));
                await Compile(c, area, snapshot, choice, "font-regular-bold-probe");
                string observed = await Inspect(c, area, snapshot, choice);
                c.That(observed.Contains(probe, StringComparison.Ordinal), "Supported Latin/combining/symbol probe survives native paged literal observation");
                // Supplementary native paged outlines, never a PDF validation path.
                // Both font weights are present: bold header and regular summary.
                var outlineSnapshot = ResumeTemplateTests.Snapshot(new() { ResumeTitle = "", Header = new() { FullName = probe }, Summary = probe,
                    ShowEducation = false, ShowExperience = false, ShowSkills = false, ShowProjects = false,
                    ShowCertifications = false, ShowAchievements = false, ShowResponsibilities = false,
                    Formatting = new() { ShowDividers = false } });
                using var workspace = area.Workspace(Package(choice, outlineSnapshot));
                using var runtime = area.Verifier.Verify(area.Sandbox);
                var outlined = await new WindowsRendererProcess().RunAsync(new(runtime,
                    ResumeTypstCompiler.CompileArguments(workspace).SetItem(2, "svg"), workspace, true, TimeSpan.FromSeconds(15), RendererLimits.Pdf), default);
                c.That(outlined.Outcome == RendererOutcome.Completed && outlined.Settled && outlined.AppContainer && outlined.ZeroCapabilities && outlined.JobMembership,
                    "Final template produces actual paged regular/bold glyph outlines under B1 ownership");
                string outlinePath = Path.Combine(Environment.GetEnvironmentVariable("AXORA_RENDERER_TEST_ROOT")!, "font-outline-" + choice + ".svg");
                if (outlined.Outcome == RendererOutcome.Completed) System.IO.File.WriteAllBytes(outlinePath, outlined.Output);
                Console.WriteLine("B2B-FONT-OUTLINES " + JsonSerializer.Serialize(new { template = choice.ToString(), outlinePath,
                    sha256 = Convert.ToHexString(SHA256.HashData(outlined.Output)), bytes = outlined.Output.Length,
                    templateSha256 = Package(choice, outlineSnapshot).Template.TemplateSourceSha256 }));
                Array.Clear(outlined.Output); c.That(workspace.Cleanup(), "Native outline observation cleans its exact package");
            }
        });
        await c.CaseAsync("b2b-workspace-attacks", async () => {
            foreach (bool data in new[] { false, true })
            {
                using var w = area.Workspace(Package()); string file = data ? w.ResumeData : w.Source;
                await c.ThrowsAsync<IOException>(() => { System.IO.File.WriteAllBytes(file, [0]); return Task.CompletedTask; }, "Pinned package file denies write substitution");
                await c.ThrowsAsync<IOException>(() => { System.IO.File.Delete(file); return Task.CompletedTask; }, "Pinned package file denies delete/recreate substitution");
                await c.ThrowsAsync<IOException>(() => { System.IO.File.Move(file, file + ".moved"); return Task.CompletedTask; }, "Pinned package file denies namespace rename race");
                await c.ThrowsAsync<IOException>(() => { System.IO.File.CreateSymbolicLink(file, area.Root); return Task.CompletedTask; }, "Existing pinned file rejects reparse substitution");
                string alias = Path.Combine(area.Root, "hardlink-" + Guid.NewGuid().ToString("N"));
                if (CreateHardLinkW(alias, file, IntPtr.Zero))
                {
                    await c.ThrowsAsync<InvalidDataException>(() => { w.Validate(); return Task.CompletedTask; }, "Unexpected hard-link identity fails package authentication");
                    System.IO.File.Delete(alias);
                }
                else c.That(Marshal.GetLastWin32Error() != 0, "Pinned file rejects hard-link substitution attempt");
                w.Validate(); c.That(w.Cleanup(), "Denied identity attacks preserve package cleanup");
            }
            foreach (bool directory in new[] { false, true })
            {
                using var w = area.Workspace(Package()); string extra = Path.Combine(w.Cache, "unexpected");
                if (directory) Directory.CreateDirectory(extra); else System.IO.File.WriteAllText(extra, "SYNTHETIC");
                await c.ThrowsAsync<InvalidDataException>(() => { w.Validate(); return Task.CompletedTask; }, "Unexpected third file/directory fails full namespace validation");
                c.That(!w.Cleanup(), "Unexpected artifact cannot be silently recursively deleted");
                if (directory) Directory.Delete(extra); else System.IO.File.Delete(extra);
                c.That(w.Cleanup() && RendererWorkspace.NamespaceAbsent(w.Root), "Removing only the test-owned artifact allows identity-bound cleanup");
            }
            using (var w = area.Workspace(Package()))
            {
                var junction = typeof(RendererTests).GetMethod("SetJunction", BindingFlags.Static | BindingFlags.NonPublic)!;
                try { junction.Invoke(null, [w.Input, area.Root]); c.That(false, "Pinned input rejects in-place redirect"); }
                catch (TargetInvocationException ex) { c.That(ex.InnerException is Win32Exception, "Pinned input rejects in-place reparse mutation"); }
                w.Validate(); c.That(w.Cleanup(), "In-place reparse denial preserves package identity");
            }
        });
        await c.CaseAsync("b2b-alive-cleanup", async () => {
            using var w = area.Workspace(Package()); using var lease = area.HelperLease(); using var cancellation = new CancellationTokenSource();
            var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var process = new WindowsRendererProcess { BeforePhaseForTest = phase => { if (phase == "before-resume") resumed.TrySetResult(); } };
            var running = process.RunAsync(new(lease, ["hang"], w, true, TimeSpan.FromSeconds(5), 4096), cancellation.Token);
            await resumed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            c.That(!w.Cleanup() && Directory.Exists(w.Root), "Package cleanup refuses deletion while native child is owned/running");
            cancellation.Cancel(); var r = await running;
            c.That(r.Outcome == RendererOutcome.Canceled && r.Settled && r.JobMembership && r.AppContainer && w.Cleanup(), "Cancel settles actual native tree/readers before deleting both package files");
        });
        await c.CaseAsync("b2b-child-write-denial", async () => {
            using var w = area.Workspace(Package()); using var lease = area.HelperLease();
            var r = await new WindowsRendererProcess().RunAsync(new(lease, ["write", w.Input], w, true, TimeSpan.FromSeconds(5), 4096), default);
            c.That(r.Outcome == RendererOutcome.Completed && Encoding.ASCII.GetString(r.Output) == "WRITE_DENIED" && r.Settled && r.AppContainer, "Actual AppContainer cannot add a file to sealed Resume input");
            foreach (string role in new[] { w.Source, w.ResumeData })
            {
                var denied = await new WindowsRendererProcess().RunAsync(new(lease, ["profile-control", role], w, true, TimeSpan.FromSeconds(5), 4096), default);
                c.That(denied.Outcome == RendererOutcome.Completed && denied.Settled && denied.AppContainer &&
                    Encoding.ASCII.GetString(denied.Output) == "PROFILE_CONTROL_DENIED", "Actual child cannot write/delete/change ownership or DACL of either sealed package role");
                Array.Clear(denied.Output);
            }
            w.Validate(); c.That(w.Cleanup(), "Child write denial leaves package intact and cleanable"); Array.Clear(r.Output);
        });
        await c.CaseAsync("b2b-delete-pending-candidate", async () => {
            foreach (bool data in new[] { false, true })
            {
                SafeFileHandle? retained = null; RendererWorkspace? owned = null;
                var compiler = new ResumeTypstCompiler(area.Verifier, new WindowsRendererProcess()) { WorkspaceCreatedForTest = w => {
                    owned = w; retained = OpenFile(data ? w.ResumeData : w.Source, 0, 7, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
                    if (retained.IsInvalid) throw new Win32Exception(); } };
                var r = await compiler.CompileAsync(ResumeTemplateTests.Snapshot(), ResumeTemplateChoice.SimpleV1, Font);
                c.That(r.NativeOutcome == RendererOutcome.DeletePending && r.Candidate is null && !r.CleanupComplete, "Either delete-pending package role prevents candidate promotion");
                c.That((await compiler.CompileAsync(ResumeTemplateTests.Snapshot(), ResumeTemplateChoice.SimpleV1, Font)).Outcome == ResumeCompileOutcome.AdmissionClosed, "Unresolved cleanup closes future compiler admission");
                await c.ThrowsAsync<IOException>(compiler.ShutdownAsync, "Shutdown retains delete-pending failure");
                retained!.Dispose(); _ = owned!.Cleanup(); owned.Dispose();
                c.That(RendererWorkspace.NamespaceAbsent(owned.Root), "Synthetic retained handle released; no final delete-pending operation");
            }
        });
        await c.CaseAsync("b2b-failure-candidate", async () => {
            foreach (var outcome in new[] { RendererOutcome.Canceled, RendererOutcome.TimedOut, RendererOutcome.RendererFailed, RendererOutcome.OutputLimitExceeded, RendererOutcome.DiagnosticLimitExceeded, RendererOutcome.SettlementFailed })
            {
                var compiler = new ResumeTypstCompiler(area.Verifier, new FailedCompile(outcome));
                var r = await compiler.CompileAsync(ResumeTemplateTests.Snapshot(), ResumeTemplateChoice.SimpleV1, Font);
                c.That(r.Candidate is null && r.Outcome != ResumeCompileOutcome.Completed, "Failed/canceled/timed-out/limit/unsettled result cannot promote bytes");
                if (outcome == RendererOutcome.SettlementFailed) await c.ThrowsAsync<IOException>(compiler.ShutdownAsync, "Unsettled native result remains failed at shutdown");
                else await compiler.ShutdownAsync();
            }
            RendererWorkspace? owned = null;
            var cleanupFailure = new ResumeTypstCompiler(area.Verifier, new WindowsRendererProcess()) { WorkspaceCreatedForTest = w => {
                owned = w; w.BeforePhaseForTest = phase => { if (phase == "cleanup") throw new IOException("SyntheticCleanupFailure"); }; } };
            var failed = await cleanupFailure.CompileAsync(ResumeTemplateTests.Snapshot(), ResumeTemplateChoice.SimpleV1, Font);
            c.That(failed.Candidate is null && !failed.CleanupComplete && failed.NativeOutcome == RendererOutcome.CleanupFailed, "Successful native bytes are discarded when package cleanup fails");
            await c.ThrowsAsync<IOException>(cleanupFailure.ShutdownAsync, "Cleanup failure is not forgotten at shutdown");
            owned!.BeforePhaseForTest = null; c.That(owned.Cleanup(), "Test-only cleanup fault removed; retained owned handles settle");
            using var canceledAtCleanup = new CancellationTokenSource();
            var cancelAfterNative = new ResumeTypstCompiler(area.Verifier, new WindowsRendererProcess()) { WorkspaceCreatedForTest = w => {
                w.BeforePhaseForTest = phase => { if (phase == "cleanup") canceledAtCleanup.Cancel(); }; } };
            var canceled = await cancelAfterNative.CompileAsync(ResumeTemplateTests.Snapshot(), ResumeTemplateChoice.SimpleV1, Font, canceledAtCleanup.Token);
            c.That(canceled.Outcome == ResumeCompileOutcome.Canceled && canceled.Candidate is null && canceled.CleanupComplete && canceled.Settled,
                "Cancellation after actual native output and during cleanup prevents candidate promotion");
            await cancelAfterNative.ShutdownAsync();
        });
        await c.CaseAsync("b2b-admission-shutdown", async () => {
            using var barrier = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var process = new WindowsRendererProcess { BeforePhaseForTest = phase => { if (phase == "before-resume") { entered.TrySetResult(); if (!barrier.Wait(5000)) throw new IOException("SyntheticBarrierTimeout"); } } };
            var compiler = new ResumeTypstCompiler(area.Verifier, process);
            var active = compiler.CompileAsync(ResumeTemplateTests.Snapshot(), ResumeTemplateChoice.SimpleV1, Font);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            c.That((await compiler.CompileAsync(ResumeTemplateTests.Snapshot(), ResumeTemplateChoice.SimpleV1, Font)).Outcome == ResumeCompileOutcome.Busy, "One active Resume compile owns admission");
            var stop = compiler.ShutdownAsync(); barrier.Set(); await stop; var r = await active;
            c.That(r.Outcome == ResumeCompileOutcome.Canceled && r.Candidate is null && r.CleanupComplete, "Active shutdown cancels native work and returns no candidate");
            c.That(ReferenceEquals(stop, compiler.ShutdownAsync()) && (await compiler.CompileAsync(ResumeTemplateTests.Snapshot(), ResumeTemplateChoice.SimpleV1, Font)).Outcome == ResumeCompileOutcome.AdmissionClosed, "Repeated shutdown is stable and closed admission is permanent");
        });
        await c.CaseAsync("b2b-aap-access-trap", async () => { await AapTrap(c, area); });
        await c.CaseAsync("b2b-native-failure-handoff", async () => {
            foreach (var pair in new[] { ("hang", RendererOutcome.TimedOut), ("flood", RendererOutcome.OutputLimitExceeded),
                ("stderr", RendererOutcome.DiagnosticLimitExceeded), ("tree", RendererOutcome.RendererFailed), ("crash", RendererOutcome.RendererFailed) })
            {
                var compiler = new ResumeTypstCompiler(area.Verifier, new NativeFaultProcess(area, pair.Item1));
                var r = await compiler.CompileAsync(ResumeTemplateTests.Snapshot(), ResumeTemplateChoice.SimpleV1, Font);
                Console.WriteLine("B2B-NATIVE-FAULT " + JsonSerializer.Serialize(new { mode = pair.Item1, r }));
                c.That(r.Candidate is null && r.NativeOutcome == pair.Item2 && r.CleanupComplete && r.Settled && r.JobMembership && r.AppContainer,
                    "Actual native failure/limit/descendant settlement returns no candidate: " + pair.Item1);
                await compiler.ShutdownAsync();
                if (pair.Item1 == "tree") c.That(r.ProcessCount >= 2, "Root-exit helper has a real pipe-holding descendant, settled with no candidate from empty output");
            }
        });
        await c.CaseAsync("b2b-supervised-authentication", async () => {
            foreach (string field in new[] { "_source", "_data" })
            {
                RendererWorkspace? owned = null; int launches = 0;
                var process = new WindowsRendererProcess { BeforePhaseForTest = phase => {
                    if (phase == "before-resume" && Interlocked.Increment(ref launches) == 2)
                    {
                        var handle = (SafeFileHandle)typeof(RendererWorkspace).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owned!)!;
                        RandomAccess.Write(handle, new byte[] { 0 }, 0);
                    }
                } };
                var compiler = new ResumeTypstCompiler(area.Verifier, process) { WorkspaceCreatedForTest = w => owned = w };
                var r = await compiler.CompileAsync(ResumeTemplateTests.Snapshot(), ResumeTemplateChoice.SimpleV1, Font);
                Console.WriteLine("B2B-SUPERVISED-AUTH " + JsonSerializer.Serialize(new { role = field, r, ownedRoot = owned?.Root }));
                c.That(r.Candidate is null && r.NativeOutcome == RendererOutcome.UnexpectedArtifacts && r.CleanupComplete && RendererWorkspace.NamespaceAbsent(owned!.Root),
                    "Both package roles remain authenticated through native supervision and final settlement");
                await compiler.ShutdownAsync();
            }
            c.That(!Directory.EnumerateFileSystemEntries(area.Operations).Any(), "Final acceptance leaves zero live, pending or unknown operation namespaces");
        });
    }
    private static string HtmlText(string html) => WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase), "<[^>]*>", ""));
    private static IEnumerable<string> DisplayStrings(ResumeSnapshotDocument document)
    {
        yield return document.ResumeTitle;
        foreach (var value in typeof(ResumeSnapshotHeader).GetProperties().Select(x => (string)x.GetValue(document.Header)!)) if (value.Length > 0) yield return value;
        foreach (var section in document.VisibleSectionOrder)
        {
            if (section == ResumeSection.Summary) { if (document.Summary.Length > 0) yield return document.Summary; continue; }
            string name = section switch { ResumeSection.Experience => "Experiences", ResumeSection.Skills => "SkillCategories", _ => section.ToString() };
            foreach (object item in (System.Collections.IEnumerable)typeof(ResumeSnapshotDocument).GetProperty(name)!.GetValue(document)!)
            foreach (var property in item.GetType().GetProperties())
            {
                if (property.Name is "Id" or "BulletsRaw" or "SkillsCsv" or "EndDate") continue;
                if (property.GetValue(item) is string value && value.Length > 0) yield return value;
                if (property.GetValue(item) is ImmutableArray<string> values) foreach (string valueItem in values) if (valueItem.Length > 0) yield return valueItem;
            }
        }
    }
    private sealed class FailedCompile(RendererOutcome outcome) : IRendererProcess
    {
        public Task<RendererProcessResult> RunAsync(RendererLaunch launch, CancellationToken token) => Task.FromResult(new RendererProcessResult(
            launch.Pdf ? outcome : RendererOutcome.Completed, 0, launch.Pdf ? new byte[] { 37, 80, 68, 70 } : Encoding.UTF8.GetBytes(RendererRuntimeManifest.VersionOutput),
            0, 0, 0, 0, 1, true, true, true, !launch.Pdf || outcome != RendererOutcome.SettlementFailed));
    }
    private sealed class NativeFaultProcess(Area area, string mode) : IRendererProcess
    {
        public async Task<RendererProcessResult> RunAsync(RendererLaunch launch, CancellationToken token)
        {
            if (!launch.Pdf) return await new WindowsRendererProcess().RunAsync(launch, token);
            using var helper = area.HelperLease();
            // Test-only failure injection through the same B1 AppContainer/Job ownership path.
            return await new WindowsRendererProcess().RunAsync(new(helper, [mode], launch.Workspace, true,
                mode is "hang" or "tree" ? TimeSpan.FromMilliseconds(80) : TimeSpan.FromSeconds(5), 4096, 4096), token);
        }
    }
    private static async Task AapTrap(Checks c, Area area)
    {
        string parent = Environment.GetEnvironmentVariable("AXORA_RENDERER_TEST_ROOT")!;
        string root = Path.Combine(parent, "aap-b2b-" + Guid.NewGuid().ToString("N"));
        string sentinel = Path.Combine(root, "sentinel.txt");
        using var identity = WindowsIdentity.GetCurrent(); string owner = identity.User!.Value;
        string sddl = $"O:{owner}G:{owner}D:P(A;;FA;;;SY)(A;;FA;;;{owner})(A;;0x1200a9;;;S-1-15-2-1)";
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, 1, out var descriptor, out _)) throw new Win32Exception();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            var security = new WindowsRendererSandbox.SecurityAttributes { Length = Marshal.SizeOf<WindowsRendererSandbox.SecurityAttributes>(), Descriptor = descriptor };
            if (!CreateDirectoryW(root, ref security)) throw new Win32Exception();
            using (var file = CreateSentinel(sentinel, 0x40000000, 1, ref security, 1, 0, IntPtr.Zero))
            { if (file.IsInvalid) throw new Win32Exception(); RandomAccess.Write(file, Encoding.ASCII.GetBytes("SYNTHETIC-AAP"), 0); }
            var acl = new RawSecurityDescriptor(new FileInfo(sentinel).GetAccessControl(AccessControlSections.Access).GetSecurityDescriptorBinaryForm(), 0);
            var dacl = acl.DiscretionaryAcl ?? throw new InvalidDataException("SyntheticSentinelMissingDacl");
            c.That(dacl.Cast<CommonAce>().Any(x => x.SecurityIdentifier.Value == "S-1-15-2-1") &&
                dacl.Cast<CommonAce>().All(x => x.SecurityIdentifier.Value != area.Sandbox.SidText), "External sentinel admits AAP only, with no explicit renderer SID grant");
            var positive = await area.Probe(["aap", sentinel]);
            c.That(positive.Outcome == RendererOutcome.Completed && Encoding.ASCII.GetString(positive.Output) == "AAP_AMBIENT_READ_CONFIRMED" && positive.AppContainer,
                "Positive control: standard renderer AppContainer actually reads the synthetic AAP sentinel");
            Array.Clear(positive.Output);
            listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using (var client = new TcpClient()) { await client.ConnectAsync(IPAddress.Loopback, port); using var accepted = await listener.AcceptTcpClientAsync(); }
            c.That(!listener.Pending(), "Owned loopback request monitor positive control calibrated and drained");
            using (var trap = OpenFile(sentinel, 0x80000000, 0, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero))
            {
                if (trap.IsInvalid) throw new Win32Exception();
                var attempted = await area.Probe(["aap", sentinel]);
                c.That(attempted.Outcome == RendererOutcome.RendererFailed && attempted.ExitCode == 107 && attempted.Settled,
                    "Held-exclusive trap calibration: actual child read attempt necessarily fails at open");
                string payload = "#read(\"" + sentinel.Replace('\\', '/') + "\") http://127.0.0.1:" + port + "/synthetic";
                foreach (var choice in Enum.GetValues<ResumeTemplateChoice>())
                {
                    var r = await Compile(c, area, ResumeTemplateTests.Snapshot(ResumeTemplateTests.Full(payload)), choice, "AAP-held-exclusive-negative");
                    c.That(r.Outcome == ResumeCompileOutcome.Completed && r.DiagnosticBytes == 0,
                        "Final trusted template completes with attacker path strings while calibrated external-open trap is held");
                    c.That(!listener.Pending(), "Neither trusted template requests the attacker-controlled loopback URL");
                    if (r.Candidate is { } pdf)
                    {
                        byte[] bytes = pdf.CopyBytesForValidation();
                        c.That(bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes("SYNTHETIC-AAP")) < 0, "Private stdout candidate contains no raw sentinel marker; not a PDF validation claim");
                        Array.Clear(bytes);
                    }
                }
                Console.WriteLine("B2B-AAP-TRAP " + JsonSerializer.Serialize(new { positiveRead = true, calibratedOpenFailureExit = attempted.ExitCode,
                    bothFinalTemplatesCompleted = true, loopbackRequests = listener.Pending() ? 1 : 0,
                    sourceHashes = ResumeTemplateCatalog.Manifests.Select(x => x.TemplateSourceSha256).ToArray(),
                    boundary = "Calibrated held-exclusive open trap + complete source/call-graph review; not sentinel-unchanged inference" }));
            }
        }
        finally
        {
            listener.Stop(); LocalFree(descriptor);
            if (System.IO.File.Exists(sentinel)) System.IO.File.Delete(sentinel);
            if (Directory.Exists(root)) Directory.Delete(root);
        }
        c.That(RendererWorkspace.NamespaceAbsent(root), "Only the synthetic AAP sentinel removed; no final trap artifact");
    }
    private static async Task<string> Inspect(Checks c, Area area, ResumeRenderSnapshot snapshot, ResumeTemplateChoice choice)
    {
        var package = Package(choice, snapshot);
        using var workspace = area.Workspace(package); using var runtime = area.Verifier.Verify(area.Sandbox);
        // Fixed selector; the production template emits metadata for the same lexical
        // value immediately passed to text(). Query uses the paged layout target.
        ImmutableArray<string> arguments = ["query", "--root", workspace.Input, "--ignore-system-fonts",
            "--package-path", workspace.Packages, "--package-cache-path", workspace.Cache,
            "--diagnostic-format", "short", "--field", "value", workspace.Source, "metadata"];
        var r = await new WindowsRendererProcess().RunAsync(new(runtime, arguments, workspace, true, TimeSpan.FromSeconds(15), RendererLimits.Pdf), default);
        Console.WriteLine("B2B-INSPECT-DISPOSITION " + JsonSerializer.Serialize(new { r.Outcome, r.ExitCode, r.DiagnosticBytes, r.DurationMs, r.Settled }));
        string html = "";
        if (r.Outcome == RendererOutcome.Completed)
        {
            using var observation = JsonDocument.Parse(r.Output);
            var values = observation.RootElement.EnumerateArray().ToArray();
            lock (c) c.That(values.Length > 0 && values.All(x => x.GetProperty("domain").GetString() == "AXORA.Resume.LiteralV1" &&
                x.GetProperty("weight").GetString() is "regular" or "bold"), "Only static literal observations and proved font weights in native paged query");
            html = string.Join("\n", values.Select(x => x.GetProperty("value").GetString()));
        }
        Array.Clear(r.Output);
        bool cleanup = workspace.Cleanup();
        lock (c)
        {
            c.That(r.Outcome == RendererOutcome.Completed && r.Settled && r.AppContainer && r.ZeroCapabilities && r.JobMembership,
                "Test-only inspectable output uses exact pinned template/package and the accepted native sandbox");
            c.That(cleanup, "Test-only literal observation settles/cleans its package");
        }
        return html;
    }
    private static async Task<ResumeCompileResult> Compile(Checks c, Area area, ResumeRenderSnapshot s, ResumeTemplateChoice choice, string label)
    {
        string? operationRoot = null;
        var compiler = new ResumeTypstCompiler(area.Verifier, new WindowsRendererProcess()) { WorkspaceCreatedForTest = w => operationRoot = w.Root };
        var r = await compiler.CompileAsync(s, choice, Font);
        Console.WriteLine("B2B-PRODUCTION " + JsonSerializer.Serialize(new { label, template = choice.ToString(), r.Outcome, r.NativeOutcome, r.CleanupComplete,
            r.JobMembership, r.AppContainer, r.ZeroCapabilities, r.Settled, r.ProcessCount, r.DiagnosticBytes,
            r.Candidate?.OperationId, r.Candidate?.PdfSha256, r.Candidate?.PdfByteCount, r.Candidate?.RenderJobSha256 }));
        lock (c)
        {
            c.That(r.Outcome == ResumeCompileOutcome.Completed && r.Candidate is not null && r.CleanupComplete && r.Settled && r.JobMembership && r.AppContainer && r.ZeroCapabilities,
                "Production Resume compile returns only a settled private UNVALIDATED candidate: " + label);
            c.That(operationRoot is not null && RendererWorkspace.NamespaceAbsent(operationRoot), "Completed compiler leaves no operation workspace");
        }
        await compiler.ShutdownAsync();
        return r;
    }
    internal sealed class Area : IDisposable
    {
        internal WindowsRendererSandbox Sandbox { get; } = new();
        internal string Root { get; }
        internal string Runtime { get; }
        internal string Operations => Path.Combine(Runtime, "operations");
        internal string Helper { get; }
        internal TypstRuntimeVerifier Verifier => new(Runtime);
        internal Area()
        {
            string parent = Environment.GetEnvironmentVariable("AXORA_RENDERER_TEST_ROOT") ?? throw new InvalidOperationException("Native test provisioning required");
            string source = Environment.GetEnvironmentVariable("AXORA_RENDERER_TEST_RUNTIME_SOURCE") ?? throw new InvalidOperationException("Authenticated Typst source required");
            Root = Path.Combine(parent, "b2b-" + Guid.NewGuid().ToString("N"));
            Sandbox.EnsureOwnedDirectory(Root, true);
            Runtime = Path.Combine(Root, "runtime"); TypstRuntimeVerifier.Provision(source, Runtime, Sandbox);
            Sandbox.EnsureOwnedDirectory(Operations, true);
            Helper = Path.Combine(Root, "diagnostic-probe.exe");
            byte[] helper = System.IO.File.ReadAllBytes(Environment.GetEnvironmentVariable("AXORA_RENDERER_TEST_HELPER")!);
            using var output = new FileStream(Sandbox.CreateFile(Helper, true, 0x40000000), FileAccess.Write);
            output.Write(helper); output.Flush(true);
        }
        internal RendererWorkspace Workspace(TrustedRenderPackage p) => RendererWorkspace.Create(Operations, Guid.NewGuid(), p, Sandbox);
        internal ResumeTypstCompiler Compiler() => new(Verifier, new WindowsRendererProcess());
        internal RuntimeLease HelperLease() => new(Helper, new FileStream(TypstRuntimeVerifier.OpenPinnedFile(Helper), FileAccess.Read),
            RendererWorkspace.PinAncestors(Helper), Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(Helper))), () => Sandbox.RequireOwned(Helper, true));
        internal async Task<RendererProcessResult> Probe(ImmutableArray<string> arguments)
        {
            using var w = Workspace(Package()); using var lease = HelperLease();
            var r = await new WindowsRendererProcess().RunAsync(new(lease, arguments, w, true, TimeSpan.FromSeconds(5), 4096), default);
            if (!w.Cleanup()) throw new IOException("SyntheticProbeCleanupFailure");
            return r;
        }
        public void Dispose() => Sandbox.Dispose();
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkW(string link, string existing, IntPtr security);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle OpenFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string text, uint revision, out IntPtr descriptor, out uint size);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateDirectoryW(string path, ref WindowsRendererSandbox.SecurityAttributes security);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateSentinel(string path, uint access, uint share, ref WindowsRendererSandbox.SecurityAttributes security, uint disposition, uint flags, IntPtr template);
}
