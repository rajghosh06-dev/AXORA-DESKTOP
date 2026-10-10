using Axora.Studio.Models;
using Axora.Studio.Services;
using Axora.Studio.Services.Contracts;
using Axora.Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Axora.Studio.Tests;

internal static class HostTests
{
    public static IReadOnlyList<string> RequiredCases { get; } = Array.AsReadOnly(new[]
    {
        "identity-composition", "startup-failure", "close-startup", "shutdown-owner", "shutdown-timeout",
        "paths-defaults-canary", "settings-roundtrip", "corrupt-recovery", "future-schema",
        "unsupported-numeric-schema", "unreadable-settings", "destination-change", "final-recheck-change", "corrupt-classification",
        "staging-failure", "replacement-failure", "commit-region-cancellation", "post-commit-cancellation",
        "writer-lease", "routes", "settings-viewmodel", "resume-lazy", "resume-routes-guard", "resume-inactive-shutdown",
        "resume-active-shutdown", "resume-export-coexistence", "resume-audio-independence"
    });

    public static async Task RunAsync(Checks c)
    {
        await c.CaseAsync("identity-composition", async () =>
        {
            c.That(typeof(StudioLifecycle).Assembly.GetName().Name == "Axora.Studio", "Actual assembly identity");
            c.That(typeof(StudioLifecycle).Namespace == "Axora.Studio", "Production root namespace");
            string[] references = typeof(StudioLifecycle).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? "").ToArray();
            c.That(!references.Any(x => x.StartsWith("Axora.Desktop", StringComparison.Ordinal)), "No legacy assembly/static locator dependency");
            using var fixture = new Fixture();
            var descriptors = new List<ServiceDescriptor>();
            using var host = StudioBootstrap.BuildHost(fixture.Paths, services => descriptors.AddRange(services));
            await host.StartAsync();
            c.That(host.Services.GetRequiredService<StudioSettingsService>() is not null, "Production DI graph resolves settings");
            c.That(host.Services.GetRequiredService<StudioSettingsService>().BeforeFinalRecheckForTest is null,
                "Normal production composition leaves final-recheck test hook dormant");
            c.That(host.Services.GetRequiredService<ShellViewModel>().Routes.Count == 6, "Production DI resolves six real routes");
            c.That(host.Services.GetRequiredService<SettingsViewModel>().Themes.Count == 3, "Production DI resolves settings VM");
            c.That(!host.Services.GetServices<IHostedService>().Any(), "No automatic feature/background startup");
            Type[] owned = descriptors.Select(x => x.ServiceType).Where(t => t.Namespace?.StartsWith("Axora.Studio") == true).ToArray();
            Type[] expected = [typeof(StudioPathService), typeof(StudioWriterLease), typeof(ISettingsFilePublisher),
                typeof(StudioSettingsService), typeof(ShellViewModel), typeof(SettingsViewModel),
                typeof(FlashcardReviewPolicy), typeof(FlashcardTextGenerator), typeof(FlashcardsViewModel),
                typeof(IStudioSavePicker), typeof(IExportFilePublisher), typeof(FlashcardExportCoordinator),
                typeof(ResumeCodec), typeof(ResumeStore), typeof(IResumeFilePublisher), typeof(IResumeFilePicker), typeof(ResumeSession), typeof(ResumeViewModel)];
            c.That(owned.ToHashSet().SetEquals(expected), "Exact H0, Flashcards and lazy Resume registration inventory");
            await host.StopAsync();
        });
        await c.CaseAsync("startup-failure", async () =>
        {
            var host = new ProbeHost();
            int activations = 0;
            var lifecycle = new StudioLifecycle(host, () => Task.CompletedTask);
            await c.ThrowsAsync<InvalidDataException>(() => lifecycle.StartAsync(_ => Task.FromException(new InvalidDataException()),
                () => activations++), "Mandatory initialization failure propagated");
            await lifecycle.ShutdownAsync();
            c.That(activations == 0, "Mandatory failure prevents healthy activation");
            c.That(host.Stops == 1 && host.Disposals == 1, "Partial startup cleaned once");
            var startFailure = new ProbeHost { StartFails = true };
            var failed = new StudioLifecycle(startFailure, () => Task.CompletedTask);
            await c.ThrowsAsync<InvalidOperationException>(() => failed.StartAsync(_ => Task.CompletedTask,
                () => activations++), "Host-start failure propagated");
            await failed.ShutdownAsync();
            c.That(activations == 0 && startFailure.Disposals == 1, "Host-start failure cannot activate");
        });
        await c.CaseAsync("close-startup", async () =>
        {
            var entered = NewSignal(); var release = NewSignal();
            int activated = 0;
            var lifecycle = new StudioLifecycle(new ProbeHost(), () => Task.CompletedTask);
            var startup = lifecycle.StartAsync(async _ => { entered.SetResult(); await release.Task; }, () => activated++);
            await entered.Task;
            var close = lifecycle.ShutdownAsync();
            release.SetResult();
            await c.ThrowsAsync<OperationCanceledException>(() => startup, "Close cancels late activation");
            await close;
            c.That(activated == 0, "Closed startup never activates afterward");
            await c.ThrowsAsync<InvalidOperationException>(() => lifecycle.StartAsync(_ => Task.CompletedTask, () => activated++),
                "Restart admission closed");
        });
        await c.CaseAsync("shutdown-owner", async () =>
        {
            using var fixture = new Fixture();
            var events = new List<string>();
            var host = StudioBootstrap.BuildHost(fixture.Paths, services =>
                services.AddSingleton(_ => new DisposalProbe(events)));
            var probe = host.Services.GetRequiredService<DisposalProbe>();
            var lifecycle = new StudioLifecycle(host, () => { events.Add("operational"); return Task.CompletedTask; },
                msg => events.Add(msg));
            await lifecycle.StartAsync(_ => Task.CompletedTask, () => { });
            Task first = lifecycle.ShutdownAsync(); Task second = lifecycle.ShutdownAsync();
            c.That(ReferenceEquals(first, second), "Published shutdown task cached");
            await first; await lifecycle.ShutdownAsync();
            c.That(probe.Disposals == 1, "Real DI-created service final-disposed exactly once");
            c.That(events.IndexOf("operational") < events.IndexOf("dispose"), "Operational stop precedes DI disposal");
            c.That(events.IndexOf("Host stop settled") < events.IndexOf("dispose"), "Host stop precedes DI disposal");
            var failingHost = new ProbeHost { StopFails = true };
            var stopFailure = new StudioLifecycle(failingHost, () => Task.CompletedTask, _ => throw new IOException());
            await stopFailure.StartAsync(_ => Task.CompletedTask, () => { });
            await c.ThrowsAsync<InvalidOperationException>(() => stopFailure.ShutdownAsync(), "Stop failure stays visible");
            c.That(failingHost.Disposals == 1, "Settled stop/log failure cannot skip final disposal");
        });
        await c.CaseAsync("shutdown-timeout", async () =>
        {
            var host = new ProbeHost(); var owned = NewSignal();
            var lifecycle = new StudioLifecycle(host, () => owned.Task, budget: TimeSpan.FromMilliseconds(60));
            await lifecycle.StartAsync(_ => Task.CompletedTask, () => { });
            await c.ThrowsAsync<OperationCanceledException>(() => lifecycle.ShutdownAsync(), "Incomplete work produces bounded shutdown failure");
            c.That(host.Disposals == 0 && host.Stops == 0, "Live work retains its DI host");
            owned.SetResult();
            host.Dispose(); // Test owns manual cleanup after proving withheld disposal.
        });
        await c.CaseAsync("paths-defaults-canary", async () =>
        {
            using var f = new Fixture();
            string legacy = Path.Combine(f.Base, "Axora", "settings.json");
            byte[] canary = [1, 20, 3, 255];
            await File.WriteAllBytesAsync(legacy, canary);
            c.That(Path.GetFileName(f.Paths.Root) == "Studio" && Path.GetFileName(Path.GetDirectoryName(f.Paths.Root)) == "Axora", "Owned Axora/Studio child root");
            c.That(Path.IsPathFullyQualified(f.Paths.Root) && !f.Paths.Root.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase), "Absolute non-executable data root");
            var loaded = await f.Settings.LoadAsync();
            c.That(loaded.Settings.Theme == StudioTheme.System && loaded.CanSave, "Valid first-launch System defaults");
            c.That(!File.Exists(f.Paths.Settings), "Startup does not write defaults");
            await f.Settings.SaveAsync(StudioTheme.Dark);
            c.That((await File.ReadAllBytesAsync(legacy)).SequenceEqual(canary), "Legacy settings byte-identical after actual save");
            bool rejected = false;
            try { _ = new StudioPathService("relative"); } catch (InvalidOperationException) { rejected = true; }
            c.That(rejected, "Relative root has no working-directory fallback");
        });
        await c.CaseAsync("settings-roundtrip", async () =>
        {
            using var f = new Fixture();
            foreach (StudioTheme theme in Enum.GetValues<StudioTheme>())
            {
                var saved = await f.Settings.SaveAsync(theme);
                var loaded = await f.Settings.LoadAsync();
                c.That(saved.Published && loaded.Settings.Theme == theme, $"Real settings round-trip {theme}");
            }
            c.That(File.Exists(f.Paths.Backup), "Replacement retains backup");
            c.That(Directory.GetFiles(f.Paths.Root, ".settings.*.tmp").Length == 0, "Successful save leaves no staging file");
        });
        await c.CaseAsync("corrupt-recovery", async () =>
        {
            using var f = new Fixture();
            await f.Settings.SaveAsync(StudioTheme.Light); await f.Settings.SaveAsync(StudioTheme.Dark);
            byte[] malformed = "{ broken original"u8.ToArray();
            await File.WriteAllBytesAsync(f.Paths.Settings, malformed);
            var loaded = await f.Settings.LoadAsync();
            c.That(loaded.Settings.Theme == StudioTheme.Light && loaded.Warning is not null, "Corrupt primary uses validated backup with warning");
            c.That((await File.ReadAllBytesAsync(f.Paths.Settings)).SequenceEqual(malformed), "Load preserves malformed original bytes");
            await f.Settings.SaveAsync(StudioTheme.System);
            string recovery = Directory.GetFiles(f.Paths.Root, "settings.corrupt.*.json").Single();
            c.That((await File.ReadAllBytesAsync(recovery)).SequenceEqual(malformed), "Explicit repair retains corrupt bytes separately");
            c.That((await File.ReadAllTextAsync(f.Paths.Backup)).Contains("Light"), "Corrupt replacement does not destroy valid backup");
            await File.WriteAllTextAsync(f.Paths.Settings, "{\"schemaVersion\":1,\"theme\":\"1\"}");
            File.Delete(f.Paths.Backup);
            loaded = await f.Settings.LoadAsync();
            c.That(loaded.Warning is not null && loaded.Settings.Theme == StudioTheme.System, "Numeric theme spelling rejected; defaults warned");
        });
        await c.CaseAsync("future-schema", async () =>
        {
            using var f = new Fixture();
            string future = "{\"schemaVersion\":2,\"theme\":\"FutureChoice\",\"unknown\":true}";
            await File.WriteAllTextAsync(f.Paths.Settings, future);
            var loaded = await f.Settings.LoadAsync();
            c.That(!loaded.CanSave && loaded.Warning is not null, "Future schema visibly read-only");
            var result = await f.Settings.SaveAsync(StudioTheme.Dark);
            c.That(!result.Published && await File.ReadAllTextAsync(f.Paths.Settings) == future, "Future schema not overwritten/downgraded");
            string oversized = future + new string(' ', 65536);
            await File.WriteAllTextAsync(f.Paths.Settings, oversized);
            loaded = await f.Settings.LoadAsync();
            c.That(!loaded.CanSave && loaded.Warning!.Contains("validation limit"), "Oversized unknown schema preserved read-only with accurate warning");
            c.That(!(await f.Settings.SaveAsync(StudioTheme.Light)).Published && await File.ReadAllTextAsync(f.Paths.Settings) == oversized,
                "Bounded reader cannot cause oversized future-schema downgrade");
        });
        await c.CaseAsync("unsupported-numeric-schema", async () =>
        {
            foreach (string version in new[] { "0", "-1", "2", "2147483648", "9223372036854775808", "1e10000", "1.5", "1.0", "1e0" })
            {
                using var f = new Fixture();
                byte[] original = System.Text.Encoding.UTF8.GetBytes($"{{\"schemaVersion\":{version},\"theme\":\"System\"}}");
                await File.WriteAllBytesAsync(f.Paths.Settings, original);
                var loaded = await f.Settings.LoadAsync();
                c.That(!loaded.CanSave && loaded.Warning!.Contains("unsupported schema"), "Numeric schema read-only: " + version);
                var saved = await f.Settings.SaveAsync(StudioTheme.Dark);
                c.That(!saved.Published && saved.Message.Contains("read-only") && !saved.Message.StartsWith("Saved"), "Unsupported schema refuses Save: " + version);
                c.That((await File.ReadAllBytesAsync(f.Paths.Settings)).SequenceEqual(original), "Unsupported schema bytes unchanged: " + version);
                c.That(Directory.GetFiles(f.Paths.Root, ".settings.*.tmp").Length == 0 &&
                    Directory.GetFiles(f.Paths.Root, "settings.corrupt.*.json").Length == 0 && !File.Exists(f.Paths.Backup),
                    "Unsupported schema creates no stage/backup/recovery: " + version);
            }
            using var ambiguous = new Fixture();
            byte[] duplicate = "{\"schemaVersion\":2,\"schemaVersion\":1,\"theme\":\"System\"}"u8.ToArray();
            await File.WriteAllBytesAsync(ambiguous.Paths.Settings, duplicate);
            var result = await ambiguous.Settings.LoadAsync();
            c.That(!result.CanSave && result.Warning!.Contains("ambiguous"), "Duplicate version fields are unclassified, not last-value-wins v1");
            c.That(!(await ambiguous.Settings.SaveAsync(StudioTheme.Dark)).Published &&
                (await File.ReadAllBytesAsync(ambiguous.Paths.Settings)).SequenceEqual(duplicate), "Ambiguous schema preserves original bytes");
        });
        await c.CaseAsync("unreadable-settings", async () =>
        {
            using var f = new Fixture();
            await f.Settings.SaveAsync(StudioTheme.Light);
            byte[] original = await File.ReadAllBytesAsync(f.Paths.Settings);
            using (var obstruction = new FileStream(f.Paths.Settings, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var loaded = await f.Settings.LoadAsync();
                c.That(!loaded.CanSave && loaded.Warning!.Contains("could not be safely read") && !loaded.Warning.Contains("invalid content"),
                    "Real sharing violation is unavailable/read-only, not confirmed corruption");
                var saved = await f.Settings.SaveAsync(StudioTheme.Dark);
                c.That(!saved.Published && saved.Message.Contains("read-only"), "Unreadable destination refuses Save");
                c.That(Directory.GetFiles(f.Paths.Root, ".settings.*.tmp").Length == 0 &&
                    Directory.GetFiles(f.Paths.Root, "settings.corrupt.*.json").Length == 0 && !File.Exists(f.Paths.Backup),
                    "Unreadable destination never stages or creates a corrupt recovery");
            }
            c.That((await File.ReadAllBytesAsync(f.Paths.Settings)).SequenceEqual(original), "Obstructed v1 bytes unchanged after release");
            var retry = await f.Settings.SaveAsync(StudioTheme.Dark);
            c.That(retry.Published && f.Settings.CanSave && f.Settings.Current.Theme == StudioTheme.Dark,
                "Safe direct retry reclassifies v1 after obstruction is released");
            byte[] future = "{\"schemaVersion\":2147483648,\"theme\":\"System\"}"u8.ToArray();
            await File.WriteAllBytesAsync(f.Paths.Settings, future);
            using (var obstruction = new FileStream(f.Paths.Settings, FileMode.Open, FileAccess.Read, FileShare.None))
                c.That(!(await f.Settings.SaveAsync(StudioTheme.Light)).Published, "Locked unsupported schema refuses Save without classification");
            var afterRelease = await f.Settings.LoadAsync();
            c.That(!afterRelease.CanSave && afterRelease.Warning!.Contains("unsupported schema"), "Released obstruction reveals actual unsupported schema");
            c.That(!(await f.Settings.SaveAsync(StudioTheme.Light)).Published &&
                (await File.ReadAllBytesAsync(f.Paths.Settings)).SequenceEqual(future), "Release never makes an unsupported schema writable");
        });
        await c.CaseAsync("destination-change", async () =>
        {
            using var f = new Fixture();
            await f.Settings.LoadAsync(); // Previously absent, writable destination.
            byte[] future = "{\"schemaVersion\":2,\"theme\":\"FutureChoice\"}"u8.ToArray();
            await File.WriteAllBytesAsync(f.Paths.Settings, future);
            var changed = await f.Settings.SaveAsync(StudioTheme.Dark);
            c.That(!changed.Published && !f.Settings.CanSave, "Save reclassifies a destination created after missing Load");
            c.That((await File.ReadAllBytesAsync(f.Paths.Settings)).SequenceEqual(future), "New unsupported destination is not overwritten");
            await File.WriteAllTextAsync(f.Paths.Settings, "{\"schemaVersion\":1,\"theme\":\"Light\"}");
            c.That((await f.Settings.LoadAsync()).CanSave, "Valid v1 reload reopens admission");
            byte[] huge = "{\"schemaVersion\":9223372036854775808,\"theme\":\"System\"}"u8.ToArray();
            await File.WriteAllBytesAsync(f.Paths.Settings, huge);
            c.That(!(await f.Settings.SaveAsync(StudioTheme.Dark)).Published &&
                (await File.ReadAllBytesAsync(f.Paths.Settings)).SequenceEqual(huge), "Stale valid Load cannot authorize a beyond-Int64 destination");
            c.That(Directory.GetFiles(f.Paths.Root, "settings.corrupt.*.json").Length == 0 &&
                Directory.GetFiles(f.Paths.Root, ".settings.*.tmp").Length == 0, "Destination change creates no repair/staging artifacts");
        });
        await c.CaseAsync("final-recheck-change", async () =>
        {
            using var f = new Fixture();
            byte[] original = "{\"schemaVersion\":1,\"theme\":\"System\"}"u8.ToArray();
            byte[] unsupported = "{\"schemaVersion\":2147483648,\"theme\":\"System\"}"u8.ToArray();
            await File.WriteAllBytesAsync(f.Paths.Settings, original);
            string unrelatedStage = Path.Combine(f.Paths.Root, ".settings.unrelated.t2.tmp");
            string unrelatedFile = Path.Combine(f.Paths.Root, "unrelated.txt");
            byte[] sentinel = "unrelated operation owns these bytes"u8.ToArray();
            await File.WriteAllBytesAsync(unrelatedStage, sentinel);
            await File.WriteAllBytesAsync(unrelatedFile, sentinel);
            var boundaryReached = NewSignal(); var resume = NewSignal();
            var boundaryBudget = TimeSpan.FromSeconds(10);
            using var operationBudget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            int hookHits = 0, commitCalls = 0;
            using var service = new StudioSettingsService(f.Paths, f.Lease, new DelegatePublisher((stage, destination, backup) =>
            {
                Interlocked.Increment(ref commitCalls);
                new SettingsFilePublisher().Commit(stage, destination, backup);
            }))
            {
                BeforeFinalRecheckForTest = async ct =>
                {
                    Interlocked.Increment(ref hookHits);
                    boundaryReached.TrySetResult();
                    await resume.Task.WaitAsync(boundaryBudget, ct).ConfigureAwait(false);
                }
            };
            var loaded = await service.LoadAsync(operationBudget.Token).WaitAsync(boundaryBudget);
            c.That(loaded.CanSave && loaded.Settings.Theme == StudioTheme.System,
                "Final-recheck fixture starts with validated writable v1/System");
            var currentBefore = service.Current;
            var saving = service.SaveAsync(StudioTheme.Dark, operationBudget.Token);
            try
            {
                await boundaryReached.Task.WaitAsync(boundaryBudget, operationBudget.Token);
                c.That(Volatile.Read(ref hookHits) == 1 && !saving.IsCompleted,
                    "Save reaches exactly one validated-stage boundary and remains paused before final read");
                c.That((await File.ReadAllBytesAsync(f.Paths.Settings)).SequenceEqual(original),
                    "Writable original destination remains present after first admission and staging");
                string ownedStage = Directory.GetFiles(f.Paths.Root, ".settings.*.tmp")
                    .Except(new[] { unrelatedStage }, StringComparer.OrdinalIgnoreCase).Single();
                using (var closedStage = new FileStream(ownedStage, FileMode.Open, FileAccess.Read, FileShare.None))
                using (var staged = System.Text.Json.JsonDocument.Parse(closedStage))
                    c.That(staged.RootElement.GetProperty("schemaVersion").GetInt32() == 1 &&
                        staged.RootElement.GetProperty("theme").GetString() == "Dark",
                        "Owned stage is closed and contains validated attempted v1/Dark before mutation");
                await File.WriteAllBytesAsync(f.Paths.Settings, unsupported, operationBudget.Token);
                byte[] atBoundary = await File.ReadAllBytesAsync(f.Paths.Settings, operationBudget.Token);
                string boundaryHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(atBoundary));
                c.That(atBoundary.SequenceEqual(unsupported),
                    "Unsupported destination physically present before release; SHA256=" + boundaryHash);
                resume.TrySetResult();
                var result = await saving.WaitAsync(boundaryBudget, operationBudget.Token);
                c.That(!result.Published && !result.Message.StartsWith("Saved", StringComparison.Ordinal) &&
                    !service.CanSave && service.Warning!.Contains("unsupported schema", StringComparison.OrdinalIgnoreCase),
                    "Final destination classification rejects unsupported schema without Published/Saved");
                c.That(Volatile.Read(ref hookHits) == 1, "Final-recheck hook executed exactly once");
                c.That(Volatile.Read(ref commitCalls) == 0, "Rejected final recheck invokes publisher Commit zero times");
                byte[] after = await File.ReadAllBytesAsync(f.Paths.Settings, operationBudget.Token);
                c.That(after.SequenceEqual(atBoundary) &&
                    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(after)) == boundaryHash,
                    "Rejected final recheck preserves exact unsupported destination bytes and hash");
                using (var preserved = System.Text.Json.JsonDocument.Parse(after))
                    c.That(preserved.RootElement.GetProperty("schemaVersion").GetInt64() == 2147483648,
                        "Rejected final recheck never overwrites destination with schemaVersion 1");
                c.That(!File.Exists(f.Paths.Backup), "Rejected final recheck creates no replacement backup");
                c.That(Directory.GetFiles(f.Paths.Root, "settings.corrupt.*.json").Length == 0,
                    "Rejected final recheck creates no corrupt-recovery artifact");
                c.That(!File.Exists(ownedStage) && Directory.GetFiles(f.Paths.Root, ".settings.*.tmp")
                    .SequenceEqual(new[] { unrelatedStage }), "Only this Save's owned stage is removed");
                c.That((await File.ReadAllBytesAsync(unrelatedStage)).SequenceEqual(sentinel) &&
                    (await File.ReadAllBytesAsync(unrelatedFile)).SequenceEqual(sentinel),
                    "Unrelated stage and file remain byte-identical");
                c.That(service.Current == currentBefore && service.Current.Theme == StudioTheme.System,
                    "Rejected final recheck does not advance Current to attempted Dark");
                Console.WriteLine("FINAL-RECHECK-EVIDENCE " + System.Text.Json.JsonSerializer.Serialize(new
                {
                    destination = System.Text.Encoding.UTF8.GetString(atBoundary), sha256 = boundaryHash,
                    hookHits, commitCalls, result.Published, current = service.Current.Theme.ToString(),
                    ownedStageRemoved = !File.Exists(ownedStage)
                }));
                c.That(!(await f.Settings.SaveAsync(StudioTheme.Dark, operationBudget.Token)
                    .WaitAsync(boundaryBudget)).Published && (await File.ReadAllBytesAsync(f.Paths.Settings)).SequenceEqual(unsupported),
                    "Subsequent unhooked Save still refuses unsupported destination");
                await File.WriteAllBytesAsync(f.Paths.Settings, original, operationBudget.Token);
                c.That((await f.Settings.LoadAsync(operationBudget.Token).WaitAsync(boundaryBudget)).CanSave &&
                    (await f.Settings.SaveAsync(StudioTheme.Dark, operationBudget.Token).WaitAsync(boundaryBudget)).Published,
                    "Safe retry succeeds only after fixture explicitly restores valid current data");
                c.That(Volatile.Read(ref hookHits) == 1 && Volatile.Read(ref commitCalls) == 0,
                    "Unhooked retry does not activate isolated test hook or counting publisher");
            }
            finally
            {
                resume.TrySetResult();
                await service.StopAsync().WaitAsync(boundaryBudget);
            }
        });
        await c.CaseAsync("corrupt-classification", async () =>
        {
            foreach (string content in new[] { "{\"schemaVersion\":1,\"theme\":", "{\"theme\":\"Light\"}",
                "{\"schemaVersion\":\"2\",\"theme\":\"Light\"}", "{\"schemaVersion\":1,\"theme\":\"Unknown\"}" })
            {
                using var f = new Fixture();
                byte[] original = System.Text.Encoding.UTF8.GetBytes(content);
                await File.WriteAllBytesAsync(f.Paths.Settings, original);
                var loaded = await f.Settings.LoadAsync();
                c.That(loaded.CanSave && loaded.Settings.Theme == StudioTheme.System && loaded.Warning!.Contains("invalid content"),
                    "Readable invalid content remains confirmed-corrupt with warned defaults");
                c.That((await File.ReadAllBytesAsync(f.Paths.Settings)).SequenceEqual(original), "Corrupt load preserves original bytes");
                var saved = await f.Settings.SaveAsync(StudioTheme.Light);
                string recovery = Directory.GetFiles(f.Paths.Root, "settings.corrupt.*.json").Single();
                c.That(saved.Published && (await File.ReadAllBytesAsync(recovery)).SequenceEqual(original),
                    "Explicit corrupt repair retains established bytes in owned recovery file");
            }
        });
        foreach (string id in new[] { "staging-failure", "replacement-failure" })
            await c.CaseAsync(id, async () =>
            {
                using var f = new Fixture(); await f.Settings.SaveAsync(StudioTheme.Light);
                byte[] before = await File.ReadAllBytesAsync(f.Paths.Settings);
                bool validatedStage = false;
                using var failing = new StudioSettingsService(f.Paths, f.Lease, new DelegatePublisher((stage, _, _) =>
                {
                    validatedStage = File.ReadAllText(stage).Contains("Dark");
                    throw new IOException("Injected pre-commit interruption/replacement failure");
                }));
                var result = await failing.SaveAsync(StudioTheme.Dark);
                c.That(validatedStage && !result.Published, id + " exercised real staged publication boundary");
                c.That((await File.ReadAllBytesAsync(f.Paths.Settings)).SequenceEqual(before), id + " preserves prior bytes");
                c.That(Directory.GetFiles(f.Paths.Root, ".settings.*.tmp").Length == 0, id + " owned stage cleaned");
                await failing.StopAsync();
                if (id == "replacement-failure")
                {
                    using var lockedDestination = new FileStream(f.Paths.Settings, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var actualFailure = await f.Settings.SaveAsync(StudioTheme.Dark);
                    c.That(!actualFailure.Published, "Real Windows replacement fails while destination denies delete sharing");
                    c.That(File.ReadAllBytes(f.Paths.Settings).SequenceEqual(before), "Real replacement failure preserves destination bytes");
                    c.That(Directory.GetFiles(f.Paths.Root, ".settings.*.tmp").Length == 0, "Real replacement failure cleans owned stage");
                }
            });
        await c.CaseAsync("commit-region-cancellation", async () =>
        {
            using var f = new Fixture(); await f.Settings.SaveAsync(StudioTheme.Light);
            byte[] before = await File.ReadAllBytesAsync(f.Paths.Settings);
            using var early = new CancellationTokenSource(); early.Cancel();
            await c.ThrowsAsync<OperationCanceledException>(() => f.Settings.SaveAsync(StudioTheme.Dark, early.Token), "Cancellation before admission produces canceled operation");
            c.That((await File.ReadAllBytesAsync(f.Paths.Settings)).SequenceEqual(before), "Early cancellation preserves settings");
            using var late = new CancellationTokenSource();
            using var service = new StudioSettingsService(f.Paths, f.Lease, new DelegatePublisher((stage, destination, backup) =>
            {
                late.Cancel(); new SettingsFilePublisher().Commit(stage, destination, backup);
            }));
            c.That((await service.SaveAsync(StudioTheme.Dark, late.Token)).Published, "Cancellation during commit region does not misreport published save");
            await service.StopAsync();
        });
        await c.CaseAsync("post-commit-cancellation", async () =>
        {
            using var f = new Fixture(); await f.Settings.SaveAsync(StudioTheme.Light);
            using var afterCommit = new CancellationTokenSource();
            bool committedBeforeCancellation = false;
            using var service = new StudioSettingsService(f.Paths, f.Lease, new DelegatePublisher((stage, destination, backup) =>
            {
                new SettingsFilePublisher().Commit(stage, destination, backup);
                committedBeforeCancellation = !afterCommit.IsCancellationRequested && !File.Exists(stage) &&
                    File.ReadAllText(destination).Contains("Dark");
                afterCommit.Cancel();
            }));
            var result = await service.SaveAsync(StudioTheme.Dark, afterCommit.Token);
            c.That(committedBeforeCancellation && afterCommit.IsCancellationRequested, "Real physical commit precedes cancellation trigger");
            c.That(result.Published && result.Message.StartsWith("Saved") && !result.Message.Contains("canceled", StringComparison.OrdinalIgnoreCase),
                "True post-commit cancellation reports published success, not cancellation");
            c.That(service.Current.Theme == StudioTheme.Dark, "Post-commit cancellation updates Current to persisted theme");
            var loaded = await f.Settings.LoadAsync();
            c.That(loaded.Settings.SchemaVersion == 1 && loaded.Settings.Theme == StudioTheme.Dark && loaded.CanSave,
                "Production reader validates actual newly committed v1 bytes after cancellation");
            c.That(Directory.GetFiles(f.Paths.Root, ".settings.*.tmp").Length == 0, "Post-commit cancellation leaves no staging file");
            await service.StopAsync();
        });
        await c.CaseAsync("writer-lease", async () =>
        {
            using var f = new Fixture();
            using var second = new StudioWriterLease(f.Paths);
            bool blocked = false; try { second.Acquire(); } catch (IOException) { blocked = true; }
            c.That(blocked && !second.IsAcquired && f.Lease.IsAcquired, "Live exclusive handle blocks second writer; first remains healthy");
            f.Lease.Dispose();
            c.That(File.Exists(f.Paths.Lease), "Stale lease file deliberately remains");
            second.Acquire();
            c.That(second.IsAcquired, "Stale file does not block later live lease");
            await f.Settings.StopAsync();
        });
        await c.CaseAsync("routes", () =>
        {
            var vm = new ShellViewModel();
            c.That(StudioRoutes.All.Count == 6 && StudioRoutes.All.Select(r => r.Route).ToHashSet().SetEquals(Enum.GetValues<StudioRoute>()), "Exactly six real routes in catalog");
            foreach (var item in StudioRoutes.All)
            {
                vm.Navigate(item.Route);
                c.That(vm.SelectedRoute == item.Route && StudioRoutes.Resolve(item.Route).PageType.Name == item.Label.Replace(" ", "") + "Page", "Actual page resolution " + item.Label);
            }
            var priorRoute = vm.SelectedRoute;
            bool rejected = false; try { vm.Navigate((StudioRoute)999); } catch (ArgumentOutOfRangeException) { rejected = true; }
            c.That(rejected && vm.SelectedRoute == priorRoute, "Unknown route rejected without changing navigation");
            rejected = false; try { vm.SelectedRoute = (StudioRoute)999; } catch (ArgumentOutOfRangeException) { rejected = true; }
            c.That(rejected && vm.SelectedRoute == priorRoute, "Direct route assignment also rejects invalid values");
            return Task.CompletedTask;
        });
        await c.CaseAsync("settings-viewmodel", async () =>
        {
            using var f = new Fixture(); await f.Settings.LoadAsync();
            var vm = new SettingsViewModel(f.Settings); StudioTheme? applied = null;
            vm.ThemeSaved += theme => applied = theme;
            vm.SelectedTheme = StudioTheme.Dark;
            c.That(!File.Exists(f.Paths.Settings) && vm.Status.Contains("Choose Save"), "Draft selection does not claim persistence");
            await vm.SaveCommand.ExecuteAsync(null);
            c.That(applied == StudioTheme.Dark && f.Settings.Current.Theme == StudioTheme.Dark && vm.Status.StartsWith("Saved"), "Actual Save command commits then applies persisted theme");
            c.That(vm.CanEdit, "Editing reopens after save settles");
            vm.ThemeSaved += _ => throw new IOException("Injected rendering failure");
            vm.SelectedTheme = StudioTheme.Light;
            await vm.SaveCommand.ExecuteAsync(null);
            c.That(f.Settings.Current.Theme == StudioTheme.Light && vm.Status.Contains("Settings saved, but"),
                "Post-commit rendering failure truthfully distinguishes persisted settings");
            await f.Settings.StopAsync();
            c.That(!File.Exists(Path.Combine(f.Base, "Axora", "settings.json")), "Settings VM creates no legacy settings file");
            await c.ThrowsAsync<InvalidOperationException>(() => f.Settings.SaveAsync(StudioTheme.Dark), "Shutdown closes new settings-save admission");
            await c.ThrowsAsync<InvalidOperationException>(() => f.Settings.LoadAsync(), "Shutdown closes new settings-load admission");
        });
        await c.CaseAsync("resume-lazy", async () =>
        {
            using var f = new ResumeTests.Fixture();
            int sessions = 0, pickers = 0;
            using var host = StudioBootstrap.BuildHost(new StudioPathService(f.Base), services =>
            {
                services.AddSingleton<ResumeSession>(sp => { sessions++; return new(sp.GetRequiredService<ResumeStore>(), sp.GetRequiredService<ResumeCodec>(), sp.GetRequiredService<IResumeFilePublisher>()); });
                services.AddSingleton<IResumeFilePicker>(_ => { pickers++; return new ResumePicker(); });
            });
            await host.StartAsync();
            var lazy = new Lazy<ResumeViewModel>(host.Services.GetRequiredService<Func<ResumeViewModel>>());
            foreach (var route in new[] {StudioRoute.Home,StudioRoute.Settings,StudioRoute.About,StudioRoute.Flashcards})
                c.That(MainWindow.ResolveResume(route,lazy) is null,"Unrelated route never resolves Resume");
            c.That(sessions==0 && pickers==0 && !Directory.Exists(f.Store.Root),"Home resolves no session/picker/files or scans");
            c.That(MainWindow.ResolveResume(StudioRoute.ResumeDashboard,lazy) is not null && MainWindow.ResolveResume(StudioRoute.ResumeEditor,lazy) is not null && sessions==1 && pickers==1,"Both Resume routes share exactly one lazy session/picker");
            c.That(host.Services.GetRequiredService<ResumeStore>().Enumerations==0,"Resume resolution itself does not enumerate storage");
            await host.StopAsync();
        });
        await c.CaseAsync("resume-routes-guard", async () =>
        {
            using var f = new ResumeTests.Fixture(); await f.SavedAsync();
            var resume = new ResumeViewModel(f.Session,f.Store,new ResumePicker()); var shell = new ShellViewModel();
            shell.Navigate(StudioRoute.ResumeEditor);
            ResumeDeparture answer = ResumeDeparture.Cancel;
            resume.Configure(()=>0,()=>Task.FromResult(answer),_=>Task.FromResult(true),shell.NavigateAsync,a=>a());
            shell.DepartureGuard = _=>resume.GuardDepartureAsync();
            f.Session.Edit(f.Session.Current!.Document with {Summary="route dirty"});
            foreach(var route in new[] {StudioRoute.ResumeDashboard,StudioRoute.Home,StudioRoute.Settings,StudioRoute.About,StudioRoute.Flashcards})
                c.That(!await shell.NavigateAsync(route) && shell.SelectedRoute==StudioRoute.ResumeEditor,"Every route Cancel preserves editor");
            await c.ThrowsAsync<InvalidOperationException>(()=>{shell.SelectedRoute=StudioRoute.Home;return Task.CompletedTask;},"Direct setter cannot bypass configured guard");
            answer=ResumeDeparture.Save;
            c.That(await shell.NavigateAsync(StudioRoute.ResumeDashboard) && !f.Session.IsDirty,"Route Save persists before committing destination route");
            await shell.NavigateAsync(StudioRoute.ResumeEditor);f.Session.Edit(f.Session.Current!.Document with {Summary="discard route"});answer=ResumeDeparture.Discard;
            c.That(await shell.NavigateAsync(StudioRoute.Home) && f.Session.Current!.Document.Summary=="route dirty","Route Discard restores saved semantic baseline");
        });
        await c.CaseAsync("resume-inactive-shutdown", async () =>
        {
            int resumes=0,exports=0,audio=0,hostStops=0;
            var resume=new Lazy<ResumeViewModel>(()=>{resumes++;throw new InvalidOperationException();});
            var export=new StudioExportSession(()=>{exports++;throw new InvalidOperationException();});
            var speech=new StudioReadAloudSession(()=>{audio++;throw new InvalidOperationException();});
            c.That(!App.HasActiveFileWork(export,resume),"Unused file-work aggregate is inactive");
            await App.RetainFileWorkAsync(speech,export,resume,()=>{hostStops++;return Task.CompletedTask;});
            c.That(resumes==0 && exports==0 && audio==0 && hostStops==1,"Unused shutdown constructs no optional features and settles host once");
        });
        await c.CaseAsync("resume-active-shutdown", async () =>
        {
            using var f=new ResumeTests.Fixture();await f.SavedAsync();var entered=NewSignal();var release=NewSignal();
            var lazy=new Lazy<ResumeViewModel>(()=>new(f.Session,f.Store,new ResumePicker()));_=lazy.Value;
            var host=new ProbeHost();var lifecycle=new StudioLifecycle(host,()=>Task.CompletedTask);
            await lifecycle.StartAsync(_=>Task.CompletedTask,()=>{});
            f.Session.Edit(f.Session.Current!.Document with {Summary="host retention"});
            f.Publisher.BeforePhaseForTest=async phase=>{if(phase=="stage"){entered.SetResult();await release.Task;}};
            var save=f.Session.SaveAsync();await entered.Task;
            var export=new StudioExportSession(()=>throw new InvalidOperationException());var speech=new StudioReadAloudSession(()=>throw new InvalidOperationException());
            c.That(App.HasActiveFileWork(export,lazy),"Resume-only admitted publication is integrity-critical");
            var shutdown=App.RetainFileWorkAsync(speech,export,lazy,lifecycle.ShutdownAsync);
            c.That(!shutdown.IsCompleted && host.Disposals==0,"Host disposal withheld until Resume settlement");
            release.SetResult();await save;await shutdown;
            c.That(host.Disposals==1 && host.Stops==1 && f.Session.IsClosed,"Resume settlement closes admission before single host disposal");
        });
        await c.CaseAsync("resume-export-coexistence", async () =>
        {
            foreach(bool withResume in new[]{false,true}) foreach(bool withExport in new[]{false,true})
            {
                using var f=new ResumeTests.Fixture();await f.SavedAsync();
                var resumeEntered=NewSignal();var resumeRelease=NewSignal();var exportEntered=NewSignal();var exportRelease=NewSignal();
                var lazy=new Lazy<ResumeViewModel>(()=>new(f.Session,f.Store,new ResumePicker()));if(withResume)_=lazy.Value;
                var publisher=new ExportFilePublisher(new StudioPathService(f.Base),(boundary,_)=>{if(boundary==ExportBoundary.CommitAdmitted){exportEntered.SetResult();exportRelease.Task.GetAwaiter().GetResult();}});
                var exports=new StudioExportSession(()=>new(new ResumeExportPicker(Path.Combine(f.Base,"coexist.json")),publisher));
                var audio=new StudioReadAloudSession(()=>throw new InvalidOperationException());
                Task<ResumeResult>? save=null;Task<FlashcardExportOutcome>? export=null;
                if(withResume){f.Session.Edit(f.Session.Current!.Document with {Summary="simultaneous save"});f.Publisher.BeforePhaseForTest=async phase=>{if(phase=="stage"){resumeEntered.SetResult();await resumeRelease.Task;}};save=f.Session.SaveAsync();await resumeEntered.Task;}
                if(withExport)
                {
                    var snapshot=new FlashcardExportSnapshot(DateTimeOffset.UtcNow,new FlashcardExportDeck(Guid.NewGuid().ToString("N"),"fixture","",null,[new FlashcardExportCard(Guid.NewGuid().ToString("N"),"front","back",CardDifficulty.Medium,0,2.5,1,null,null)]));
                    export=exports.ExportAsync(1,FlashcardExportFormat.AxoraJson,()=>new(snapshot,null,""));await exportEntered.Task;
                }
                int hostStops=0;var shutdown=App.RetainFileWorkAsync(audio,exports,lazy,()=>{hostStops++;return Task.CompletedTask;});
                c.That(App.HasActiveFileWork(exports,lazy)==(withResume||withExport),"Aggregate covers A2-only, Resume-only, both and neither");
                c.That(hostStops==((withResume||withExport)?0:1),"Host cannot pass either retained file owner");
                if(withResume){resumeRelease.SetResult();c.That((await save!).Success,"Resume publication survives independent A2 shutdown");}
                if(withExport){c.That(hostStops==0,"A2 remains independently retained after Resume release");exportRelease.SetResult();c.That((await export!).State==ExportResultState.Published,"Admitted A2 publication survives concurrent Resume shutdown");}
                await shutdown;c.That(hostStops==1,"Aggregate file settlement calls host exactly once");
            }
        });
        await c.CaseAsync("resume-audio-independence", async () =>
        {
            using var f=new ResumeTests.Fixture();await f.SavedAsync();var entered=NewSignal();var release=NewSignal();
            var lazy=new Lazy<ResumeViewModel>(()=>new(f.Session,f.Store,new ResumePicker()));_=lazy.Value;
            var backend=new ResumeAudioBackend();var deadlineEntered=NewSignal();var deadlineRelease=NewSignal();
            var service=new FlashcardReadAloudService(backend,token=>{deadlineEntered.TrySetResult();return deadlineRelease.Task.WaitAsync(token);});
            var audio=new StudioReadAloudSession(()=>service);var speech=audio.ReadAsync(new(Guid.NewGuid(),"generic native-free text"));await backend.Entered.Task;
            f.Session.Edit(f.Session.Current!.Document with {Summary="audio-independent save"});f.Publisher.BeforePhaseForTest=async phase=>{if(phase=="stage"){entered.SetResult();await release.Task;}};
            var save=f.Session.SaveAsync();await entered.Task;int hostStops=0;
            var shutdown=App.RetainFileWorkAsync(audio,new StudioExportSession(()=>throw new InvalidOperationException()),lazy,()=>{hostStops++;return Task.CompletedTask;});
            await deadlineEntered.Task;deadlineRelease.SetResult();var audioResult=await audio.StopAsync();
            c.That(audioResult.Cleanup==ReadAloudCleanup.Deferred && hostStops==0,"Bounded optional audio cannot abandon or prematurely dispose Resume work");
            release.SetResult();await save;await shutdown;c.That(hostStops==1,"Host proceeds after file settlement without waiting for late native audio cleanup");
            backend.Release.SetResult(new(Guid.NewGuid(),ReadAloudOutcome.Canceled,"late released"));await speech;
        });
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class ResumePicker : IResumeFilePicker { public Task<string?> SelectImportAsync(nint owner) => Task.FromResult<string?>(null); }
    private sealed class ResumeExportPicker(string path) : IStudioSavePicker
    {
        public Task<StudioPickerResult> SelectAsync(nint owner, FlashcardExportFormat format, string name, CancellationToken token) => Task.FromResult(new StudioPickerResult(StudioPickerState.Selected, path));
        public Task<StudioPickerState> ConfirmReplacementAsync(nint owner, Guid id, ExportDestinationPlan plan, CancellationToken token) => Task.FromResult(StudioPickerState.Selected);
        public void RequestCancel() { }
    }
    private sealed class ResumeAudioBackend : IFlashcardReadAloudBackend
    {
        public TaskCompletionSource Entered = NewSignal();
        public TaskCompletionSource<ReadAloudResult> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ReadAloudResult> RunAsync(ReadAloudRequest request, CancellationToken cancellation, Func<bool> mayPlay, Action<ReadAloudProgress> progress)
        { Entered.SetResult(); return Release.Task; }
    }
    private sealed class DelegatePublisher(Action<string, string, string> commit) : ISettingsFilePublisher
    { public void Commit(string stagedPath, string destination, string backup) => commit(stagedPath, destination, backup); }
    private sealed class DisposalProbe(List<string> events) : IDisposable
    {
        public int Disposals { get; private set; }
        public void Dispose() { Disposals++; events.Add("dispose"); }
    }
    private sealed class ProbeHost : IHost
    {
        public IServiceProvider Services { get; } = new ServiceCollection().BuildServiceProvider();
        public bool StartFails { get; init; }
        public bool StopFails { get; init; }
        public int Stops { get; private set; }
        public int Disposals { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken = default) => StartFails
            ? Task.FromException(new InvalidOperationException("Injected host-start failure")) : Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default)
        { Stops++; return StopFails ? Task.FromException(new IOException("Injected host-stop failure")) : Task.CompletedTask; }
        public void Dispose() { Disposals++; (Services as IDisposable)?.Dispose(); }
    }
    private sealed class Fixture : IDisposable
    {
        public string Base { get; } = Path.Combine(Path.GetTempPath(), "axora-h0-tests-" + Guid.NewGuid().ToString("N"));
        public StudioPathService Paths { get; }
        public StudioWriterLease Lease { get; }
        public StudioSettingsService Settings { get; }
        public Fixture()
        {
            Paths = new(Base); Directory.CreateDirectory(Paths.Root);
            Lease = new(Paths); Lease.Acquire();
            Settings = new(Paths, Lease, new SettingsFilePublisher());
        }
        public void Dispose()
        {
            Settings.StopAsync().GetAwaiter().GetResult(); Settings.Dispose(); Lease.Dispose();
            // Retain exact synthetic fixture roots for inspection; never sweep temp/user data.
        }
    }
}
