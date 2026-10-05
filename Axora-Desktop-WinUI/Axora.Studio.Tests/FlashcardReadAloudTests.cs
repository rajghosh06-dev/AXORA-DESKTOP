using System.Collections.Concurrent;
using System.Xml.Linq;
using Axora.Studio.Models;
using Axora.Studio.Services;
using Axora.Studio.Services.Contracts;
using Axora.Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.System;

namespace Axora.Studio.Tests;

internal static class FlashcardReadAloudTests
{
    public static IReadOnlyList<string> RequiredCases { get; } = Array.AsReadOnly(new[]
    {
        "lazy-session", "factory-composition", "input", "visible-capture", "complete", "replacement", "latest-only",
        "late-synthesis", "late-events", "late-ui", "stop-synthesis", "stop-playback", "stop-pending", "stop-ended-race", "stop-failed-race",
        "context-flip", "context-next", "context-previous", "context-rating", "context-deck", "context-new", "context-generated",
        "route-departure-return", "metadata", "unavailable", "synthesis-failures", "playback-failures", "handler-cleanup",
        "player-cleanup", "shutdown-unused", "shutdown-synthesis", "shutdown-playback", "shutdown-race", "deadline-late-cleanup",
        "export-start", "export-admitted-deferred", "ui-composition", "privacy", "playback-fence", "host-independent", "backend-contract-failure", "settled-context-ui"
    });
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static FlashcardsViewModel Vm() => new(new(), new(), TimeProvider.System);
    private static ReadAloudRequest Request(string text = "test phrase") => new(Guid.NewGuid(), text);
    private static Task Sync(Action action) { action(); return Task.CompletedTask; }

    public static async Task RunAsync(Checks c)
    {
        await c.CaseAsync("lazy-session", async () =>
        {
            int created = 0; var h = new Harness(); var session = new StudioReadAloudSession(() => { created++; return h.Service; });
            var vm = Vm(); vm.ConfigureReadAloud(session, a => a());
            var lazy = new Lazy<FlashcardsViewModel>(() => vm);
            foreach (var route in new[] { StudioRoute.Home, StudioRoute.Settings, StudioRoute.About, StudioRoute.Flashcards })
                MainWindow.ResolveFlashcards(route, lazy);
            vm.FlipCard(); vm.NextCard(); vm.PreviousCard(); vm.RateCard(CardDifficulty.Easy); vm.CreateDeck();
            vm.GenerateCardsFromText("Term: explanation", "test"); await session.CancelCurrentAsync();
            c.That(created == 0 && !session.IsCreated && !session.IsActive && h.Created == 0, "Routes, study, notes and unused Cancel do not construct speech");
            c.That((await session.ReadAsync(Request(" "))).Outcome == ReadAloudOutcome.InvalidText && created == 0, "Invalid Read creates no service");
            for (int i = 0; i < 2; i++)
            {
                var n = h.Add(); n.Synthesis.SetResult(); n.AutoEnd = true;
                c.That((await session.ReadAsync(Request())).Outcome == ReadAloudOutcome.Completed, "Valid request completes through retained session");
            }
            c.That(created == 1 && session.IsCreated && h.Created == 2 && h.MaximumOwners == 1, "Service constructs once; each operation owns its own native request");
            await session.StopAsync();
        });
        await c.CaseAsync("factory-composition", () => Sync(() =>
        {
            using var host = StudioBootstrap.BuildHost(new StudioPathService(Path.GetTempPath()));
            var factory = host.Services.GetRequiredService<Func<IFlashcardReadAloudService>>();
            var session = new StudioReadAloudSession(factory);
            c.That(!session.IsCreated && !session.IsActive, "Production factory resolves without constructing feature");
            c.That(factory.Method is not null && typeof(FlashcardReadAloudService).Assembly == typeof(StudioLifecycle).Assembly, "Read aloud remains Studio-local");
        }));
        await c.CaseAsync("input", async () =>
        {
            int created = 0; var session = new StudioReadAloudSession(() => { created++; throw new InvalidOperationException(); });
            foreach (string bad in new[] { "", " \r\n", "\ud800", "\udc00", new string('x', 4097), string.Concat(Enumerable.Repeat("😀", 4097)) })
                c.That((await session.ReadAsync(Request(bad))).Outcome == ReadAloudOutcome.InvalidText, "Invalid text rejected before creation");
            c.That(created == 0, "All invalid inputs leave factory dormant");
            foreach (string text in new[] { "a", "😀", new string('x', 4096), string.Concat(Enumerable.Repeat("😀", 4096)) })
            {
                var h = new Harness(); var n = h.Add(); n.AutoEnd = true; n.Synthesis.SetResult();
                c.That((await h.Service.ReadAsync(Request(text))).Outcome == ReadAloudOutcome.Completed && n.Text == text,
                    "Valid full scalar field passes unchanged to synthesis");
            }
        });
        await c.CaseAsync("visible-capture", async () =>
        {
            var h = new Harness(); var vm = Vm(); vm.ConfigureReadAloud(new(() => h.Service), a => a());
            string front = vm.CurrentCard!.Front, back = vm.CurrentCard.Back;
            var a = h.Add(); var read = vm.ReadAloudAsync(); await a.SynthEntered.Task;
            c.That(a.Text == front && vm.ReadAloudPhase == ReadAloudPhase.Preparing && vm.CanStopReading && vm.CanReadAloud, "Visible front captured; replacement remains enabled while preparing");
            vm.FlipCard(); await read; await vm.ReadAloudCancellationSettlement;
            var b = h.Add(); read = vm.ReadAloudAsync(); await b.SynthEntered.Task; b.Synthesis.SetResult(); await b.PlayEntered.Task;
            c.That(b.Text == back && a.Text == front && vm.ReadAloudStatus == "Reading answer…", "Answer capture does not change old immutable input");
            vm.StopReading(); await read; await vm.ReadAloudCancellationSettlement;
            c.That(vm.ReadAloudStatus == "Read aloud stopped." && !vm.CanStopReading, "Stop settles dedicated UI status");
        });
        await c.CaseAsync("complete", async () =>
        {
            var h = new Harness(); var n = h.Add(); var read = h.Service.ReadAsync(Request()); await n.SynthEntered.Task;
            n.Synthesis.SetResult(); await n.PlayEntered.Task; n.End(); var result = await read;
            c.That(result.Outcome == ReadAloudOutcome.Completed && result.Cleanup == ReadAloudCleanup.Released && !h.Service.IsActive, "MediaEnded plus cleanup is Completed");
            c.That(n.Releases.SequenceEqual(new[] { "detach", "pause-clear", "player", "source", "stream", "synth" }) && h.Owners == 0, "Release follows player-before-dependencies order");
        });
        foreach (string id in new[] { "replacement", "latest-only", "late-synthesis" })
        await c.CaseAsync(id, async () =>
        {
            var h = new Harness(); var a = h.Add(); a.AutoCancel = false; var b = h.Add();
            var first = h.Service.ReadAsync(Request("A")); await a.SynthEntered.Task;
            var second = h.Service.ReadAsync(Request("B")); await a.CancelEntered.Task;
            c.That(h.Created == 1 && !b.SynthEntered.Task.IsCompleted, "Pending request creates no native object before old release");
            Task<ReadAloudResult> latest = second; NativeFake next = b;
            if (id == "latest-only")
            {
                latest = h.Service.ReadAsync(Request("C"));
                c.That((await second).Outcome == ReadAloudOutcome.Replaced && h.Created == 1, "C removes pending B without a native owner");
            }
            a.Synthesis.SetResult(); await next.SynthEntered.Task;
            c.That((await first).Outcome == ReadAloudOutcome.Replaced && a.Plays == 0 && a.Releases.Contains("stream"), "Late A synthesis is released without playback");
            next.Synthesis.SetResult(); await next.PlayEntered.Task;
            c.That(next.Text == (id == "latest-only" ? "C" : "B") && h.MaximumOwners == 1, "Only latest request becomes next native owner");
            a.End(); a.Fail(); c.That(next.Releases.Count == 0 && next.Plays == 1, "Old callbacks cannot stop or dispose next owner");
            next.End(); c.That((await latest).Outcome == ReadAloudOutcome.Completed && h.Owners == 0, "Latest request settles normally");
        });
        await c.CaseAsync("late-events", async () =>
        {
            var h = new Harness(); var a = h.Add(); var b = h.Add(); a.Synthesis.SetResult();
            var first = h.Service.ReadAsync(Request("A")); await a.PlayEntered.Task;
            var second = h.Service.ReadAsync(Request("B")); await b.SynthEntered.Task;
            a.End(); a.Fail(); b.Synthesis.SetResult(); await b.PlayEntered.Task;
            c.That((await first).Outcome == ReadAloudOutcome.Replaced && b.Releases.Count == 0 && h.MaximumOwners == 1, "Detached old events cannot affect B");
            b.End(); c.That((await second).Outcome == ReadAloudOutcome.Completed, "B has independent terminal owner");
        });
        await c.CaseAsync("late-ui", async () =>
        {
            var h = new Harness(); var queue = new ConcurrentQueue<Action>(); var vm = Vm(); vm.ConfigureReadAloud(new(() => h.Service), queue.Enqueue);
            var a = h.Add(); a.Synthesis.SetResult(); var first = vm.ReadAloudAsync(); await a.PlayEntered.Task;
            vm.NextCard(); await first; await vm.ReadAloudCancellationSettlement;
            var b = h.Add(); var second = vm.ReadAloudAsync(); await b.SynthEntered.Task;
            while (queue.TryDequeue(out var callback)) callback();
            c.That(vm.ReadAloudStatus == "Preparing read aloud…" && vm.CurrentCardIndex == 1, "Queued A UI updates recheck generation inside dispatcher");
            vm.StopReading(); await second; await vm.ReadAloudCancellationSettlement;
            while (queue.TryDequeue(out var callback)) callback();
            c.That(vm.ReadAloudStatus == "Read aloud stopped.", "Current cancellation publishes once after queue drain");
            var completed = new Harness(); var callbacks = new ConcurrentQueue<Action>(); var other = Vm(); other.ConfigureReadAloud(new(() => completed.Service), callbacks.Enqueue);
            var n = completed.Add(); n.AutoEnd = true; n.Synthesis.SetResult(); await other.ReadAloudAsync();
            var delayed = callbacks.ToArray(); delayed[^1](); // Deliver terminal before already queued progress adversarially.
            foreach (var callback in delayed[..^1]) callback();
            c.That(other.ReadAloudStatus == "Read aloud finished." && !other.CanStopReading, "Terminal UI settlement fences delayed same-request progress");
        });
        await c.CaseAsync("settled-context-ui", async () =>
        {
            Action<FlashcardsViewModel>[] changes = [vm => vm.StopReading(), vm => vm.FlipCard(), vm => vm.NextCard(),
                vm => vm.PreviousCard(), vm => vm.RateCard(CardDifficulty.Easy), vm => vm.SelectDeck(vm.Decks[1]),
                vm => vm.CreateDeck(), vm => vm.GenerateCardsFromText("Term: answer", "test")];
            foreach (var change in changes)
            {
                var h = new Harness(); var callbacks = new ConcurrentQueue<Action>(); var vm = Vm();
                vm.ConfigureReadAloud(new(() => h.Service), callbacks.Enqueue);
                var n = h.Add(); n.AutoEnd = true; n.Synthesis.SetResult(); await vm.ReadAloudAsync();
                // Native ownership is already released; the owning UI has not delivered its queued terminal callback.
                change(vm);
                c.That(!h.Service.IsActive && !vm.CanStopReading && vm.ReadAloudStatus == "Read aloud stopped.",
                    "Context change clears stale busy UI after native release but before terminal dispatch");
                while (callbacks.TryDequeue(out var callback)) callback();
                c.That(!vm.CanStopReading && vm.ReadAloudStatus == "Read aloud stopped.",
                    "Fenced old progress and terminal callbacks cannot resurrect settled context status");
            }
        });
        foreach (string id in new[] { "stop-synthesis", "stop-playback", "stop-pending", "stop-ended-race", "stop-failed-race" })
        await c.CaseAsync(id, async () =>
        {
            var h = new Harness(); var n = h.Add(); var read = h.Service.ReadAsync(Request()); await n.SynthEntered.Task;
            if (id == "stop-pending") n.AutoCancel = false;
            if (id != "stop-synthesis" && id != "stop-pending") { n.Synthesis.SetResult(); await n.PlayEntered.Task; }
            Task<ReadAloudResult>? pending = id == "stop-pending" ? h.Service.ReadAsync(Request("pending")) : null;
            var cancel = h.Service.CancelCurrentAsync(); var repeated = h.Service.CancelCurrentAsync();
            if (id == "stop-ended-race") n.End();
            if (id == "stop-failed-race") n.Fail();
            if (id == "stop-pending") { await n.CancelEntered.Task; n.Synthesis.SetResult(); }
            var result = await read; await cancel; await repeated;
            c.That(result.Outcome is ReadAloudOutcome.Canceled or ReadAloudOutcome.PlaybackFailed && !h.Service.IsActive && n.Releases.Count(x => x == "player") == 1, "Stop/event race has one settlement and one cleanup");
            if (pending is not null) c.That((await pending).Outcome == ReadAloudOutcome.Canceled && h.Created == 1, "Stop clears pending intent");
            n.End(); n.Fail(); var next = h.Add(); next.AutoEnd = true; next.Synthesis.SetResult();
            c.That((await h.Service.ReadAsync(Request())).Outcome == ReadAloudOutcome.Completed, "Later Read succeeds after clean Stop");
        });
        var contexts = new (string Id, Action<FlashcardsViewModel> Change)[]
        {
            ("context-flip", vm => vm.FlipCard()), ("context-next", vm => vm.NextCard()), ("context-previous", vm => vm.PreviousCard()),
            ("context-rating", vm => vm.RateCard(CardDifficulty.Hard)), ("context-deck", vm => vm.SelectDeck(vm.Decks[1])),
            ("context-new", vm => vm.CreateDeck()), ("context-generated", vm => vm.GenerateCardsFromText("Term: answer", "test"))
        };
        foreach (var context in contexts)
        await c.CaseAsync(context.Id, async () =>
        {
            var h = new Harness(); var vm = Vm(); vm.ConfigureReadAloud(new(() => h.Service), a => a());
            var n = h.Add(); n.AutoCancel = false; var read = vm.ReadAloudAsync(); await n.SynthEntered.Task;
            var before = vm.CaptureExportSnapshot().Snapshot!; context.Change(vm);
            c.That(!read.IsCompleted && vm.ReadAloudPhase == ReadAloudPhase.Stopping, "Study action proceeds before native cancellation settles");
            c.That(context.Id == "context-rating" ? vm.Decks[0].Cards[0].ReviewCount == 1 : vm.Decks[0].Cards.All(x => x.ReviewCount == 0), "Only actual rating changes review metadata");
            c.That(context.Id switch { "context-flip" => vm.IsCardFlipped, "context-next" => vm.CurrentCardIndex == 1,
                "context-previous" => vm.CurrentCardIndex == 1, "context-rating" => vm.CurrentCardIndex == 1,
                _ => vm.ActiveDeck != vm.Decks[0] }, "Requested study context changed promptly");
            await n.CancelEntered.Task; n.Synthesis.SetResult(); await read; await vm.ReadAloudCancellationSettlement;
            c.That(n.Plays == 0 && !h.Service.IsActive, "Context cancellation fences late synthesis");
        });
        await c.CaseAsync("route-departure-return", async () =>
        {
            var h = new Harness(); var vm = Vm(); var session = new StudioReadAloudSession(() => h.Service); vm.ConfigureReadAloud(session, a => a());
            var feature = new Lazy<FlashcardsViewModel>(() => vm); MainWindow.ResolveFlashcards(StudioRoute.Flashcards, feature);
            var n = h.Add(); n.Synthesis.SetResult(); var read = vm.ReadAloudAsync(); await n.PlayEntered.Task;
            MainWindow.DepartFlashcards(StudioRoute.Home, feature); c.That(!vm.CanReadAloud, "Departure disables speech before page replacement");
            await read; await vm.ReadAloudCancellationSettlement;
            vm.ConfigureReadAloud(session, a => a());
            c.That(!session.IsActive && vm.CanReadAloud && !vm.CanStopReading && h.Created == 1, "Route return does not resume old speech");
        });
        await c.CaseAsync("metadata", async () =>
        {
            var h = new Harness(); var vm = Vm(); vm.ConfigureReadAloud(new(() => h.Service), a => a());
            var stamp = vm.ActiveDeck!.LastStudied; var n = h.Add(); n.AutoEnd = true; n.Synthesis.SetResult();
            await vm.ReadAloudAsync();
            c.That(vm.ActiveDeck.LastStudied == stamp && vm.ActiveDeck.Cards.All(x => x.ReviewCount == 0 && x.LastReviewed is null), "Speech leaves study timestamps and reviews untouched");
        });
        await c.CaseAsync("unavailable", async () =>
        {
            foreach (string category in new[] { "NoDefaultVoice", "NoInstalledVoice", "MissingApi", "UnsupportedEnvironment" })
            {
                var service = new FlashcardReadAloudService(new WindowsFlashcardReadAloudBackend(create: () => throw new NotSupportedException(category)));
                var vm = Vm(); vm.Status = "notes sentinel"; vm.ExportStatus = "export sentinel"; vm.ConfigureReadAloud(new(() => service), a => a());
                await vm.ReadAloudAsync();
                c.That(vm.ReadAloudStatus == "Read aloud isn't available on this device." && !vm.CanReadAloud && vm.CanExport && vm.HasCard, "Unavailable mapping preserves ordinary study/export");
                vm.NextCard(); c.That(vm.Status == "notes sentinel" && vm.ExportStatus == "export sentinel", "Speech never overwrites notes or export status");
            }
        });
        await c.CaseAsync("synthesis-failures", async () =>
        {
            foreach (string fault in new[] { "synthesis-start", "synthesis-task", "invalid-stream", "unsupported-text" })
            {
                var h = new Harness(); var n = h.Add(); n.Fault = fault;
                if (fault == "synthesis-task") n.Synthesis.SetException(new InvalidOperationException("sensitive phrase"));
                else if (fault == "unsupported-text") n.Synthesis.SetException(new NotSupportedException("sensitive unsupported text"));
                else n.Synthesis.SetResult();
                var result = await h.Service.ReadAsync(Request());
                c.That(result.Outcome == (fault == "unsupported-text" ? ReadAloudOutcome.Unavailable : ReadAloudOutcome.SynthesisFailed)
                    && result.Cleanup == ReadAloudCleanup.Released && n.Plays == 0 && h.Owners == 0, "Synthesis/invalid-stream/unsupported-text failure releases without playback");
            }
        });
        await c.CaseAsync("playback-failures", async () =>
        {
            foreach (string fault in new[] { "source", "play", "media-failed" })
            {
                var h = new Harness(); var n = h.Add(); n.Fault = fault; n.Synthesis.SetResult();
                var read = h.Service.ReadAsync(Request()); if (fault == "media-failed") { await n.PlayEntered.Task; n.Fail(); }
                var result = await read;
                c.That(result.Outcome == ReadAloudOutcome.PlaybackFailed && result.Cleanup == ReadAloudCleanup.Released && h.Owners == 0, "Source/start/MediaFailed uses playback result and releases owner");
            }
        });
        await c.CaseAsync("handler-cleanup", async () =>
        {
            var h = new Harness(); var n = h.Add(); n.Fault = "detach"; n.AutoEnd = true; n.Synthesis.SetResult();
            var result = await h.Service.ReadAsync(Request());
            c.That(result.Outcome == ReadAloudOutcome.PlaybackFailed && n.Releases.Contains("player") && n.Releases.Contains("stream") && h.Owners == 0, "Handler failure still releases player before dependencies and remains a failure");
        });
        await c.CaseAsync("player-cleanup", async () =>
        {
            var h = new Harness(); var n = h.Add(); n.BlockPlayerRelease = true; n.AutoEnd = true; n.Synthesis.SetResult();
            var read = h.Service.ReadAsync(Request()); await h.RetryEntered.Task;
            c.That(!read.IsCompleted && !n.Releases.Contains("stream") && h.Owners == 1, "Unreleased player retains source/stream/synth");
            c.That((await h.Service.ReadAsync(Request("denied"))).Outcome == ReadAloudOutcome.Unavailable && h.Created == 1, "Failed player release immediately closes new admission");
            n.BlockPlayerRelease = false; h.RetryRelease.SetResult();
            c.That((await read).Outcome == ReadAloudOutcome.PlaybackFailed && h.Owners == 0, "Late retry releases exact owner without masking cleanup failure");
        });
        await c.CaseAsync("shutdown-unused", async () =>
        {
            int creates = 0; var session = new StudioReadAloudSession(() => { creates++; throw new InvalidOperationException(); });
            var stop = session.StopAsync(); c.That(ReferenceEquals(stop, session.StopAsync()), "Unused session retains one shutdown task"); await stop;
            c.That(creates == 0 && !session.IsCreated && (await session.ReadAsync(Request())).Outcome == ReadAloudOutcome.Unavailable, "Unused shutdown closes admission without construction");
        });
        foreach (string id in new[] { "shutdown-synthesis", "shutdown-playback", "shutdown-race" })
        await c.CaseAsync(id, async () =>
        {
            var h = new Harness(); var n = h.Add(); var session = new StudioReadAloudSession(() => h.Service);
            var read = session.ReadAsync(Request()); await n.SynthEntered.Task;
            if (id == "shutdown-playback") { n.Synthesis.SetResult(); await n.PlayEntered.Task; }
            var stop = session.StopAsync(); var directStop = h.Service.StopAsync();
            c.That(ReferenceEquals(stop, session.StopAsync()) && ReferenceEquals(directStop, h.Service.StopAsync()), "Session and service each preserve Stop task identity");
            c.That((await session.ReadAsync(Request("new"))).Outcome == ReadAloudOutcome.Unavailable, "Read racing/after shutdown is permanently denied");
            await stop; c.That((await read).Outcome == ReadAloudOutcome.Canceled && h.Created == 1 && h.Owners == 0, "Shutdown settles current native operation once");
        });
        await c.CaseAsync("deadline-late-cleanup", async () =>
        {
            var h = new Harness(); var n = h.Add(); n.AutoCancel = false; var read = h.Service.ReadAsync(Request()); await n.SynthEntered.Task;
            var stop = h.Service.StopAsync(); await h.DeadlineEntered.Task; h.DeadlineRelease.SetResult();
            var result = await stop;
            c.That(result.Outcome == ReadAloudOutcome.Unavailable && result.Cleanup == ReadAloudCleanup.Deferred && h.Service.IsActive && !n.Releases.Contains("stream"), "Injected deadline returns deferred while retaining native owner");
            c.That(ReferenceEquals(stop, h.Service.StopAsync()) && (await h.Service.ReadAsync(Request())).Outcome == ReadAloudOutcome.Unavailable, "Deadline never reopens admission");
            n.Synthesis.SetResult(); await h.LateCleanup.Task;
            c.That(n.Plays == 0 && h.Owners == 0 && !h.Service.IsActive && (await read).Cleanup == ReadAloudCleanup.Deferred, "Late cleanup releases without resurrecting result or playback");
        });
        await c.CaseAsync("export-start", async () =>
        {
            var h = new Harness(); var n = h.Add(); n.AutoCancel = false; var vm = Vm(); var session = new StudioReadAloudSession(() => h.Service); vm.ConfigureReadAloud(session, a => a());
            var picker = new Picker(); vm.ConfigureExport(new(() => new(picker, new ExportFilePublisher(new StudioPathService(Path.GetTempPath())))));
            var read = vm.ReadAloudAsync(); await n.SynthEntered.Task; var export = vm.ExportAsync(FlashcardExportFormat.Csv, 1); await picker.Entered.Task;
            c.That(vm.IsExportBusy && !vm.CanReadAloud && vm.HasCard && !read.IsCompleted, "Picker starts while speech cancellation is still unsettled");
            vm.NextCard(); picker.Selection.SetResult(new(StudioPickerState.Canceled)); await export;
            n.Synthesis.SetResult(); await read; await vm.ReadAloudCancellationSettlement;
            c.That(vm.CurrentCardIndex == 1 && vm.ExportStatus.Contains("canceled") && n.Plays == 0, "Export cancel and ordinary study retain A2 behavior");
            var unused = new StudioReadAloudSession(() => throw new InvalidOperationException()); var other = Vm(); other.ConfigureReadAloud(unused, a => a());
            var p = new Picker(); p.Selection.SetResult(new(StudioPickerState.Canceled)); other.ConfigureExport(new(() => new(p, new ExportFilePublisher(new StudioPathService(Path.GetTempPath())))));
            await other.ExportAsync(FlashcardExportFormat.Csv, 1); c.That(!unused.IsCreated, "Export without Read does not create speech");
        });
        await c.CaseAsync("export-admitted-deferred", async () =>
        {
            string root = Path.Combine(Path.GetTempPath(), "Axora-A3-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            using var release = new ManualResetEventSlim(); var admitted = Signal(); int hostStops = 0;
            try
            {
                var h = new Harness(); var n = h.Add(); n.AutoCancel = false; var speech = new StudioReadAloudSession(() => h.Service);
                var read = speech.ReadAsync(Request()); await n.SynthEntered.Task;
                var picker = new Picker(); picker.Selection.SetResult(new(StudioPickerState.Selected, Path.Combine(root, "cards.json")));
                var publisher = new ExportFilePublisher(new StudioPathService(Path.Combine(root, "appdata")), (boundary, _) =>
                { if (boundary == ExportBoundary.CommitAdmitted) { admitted.TrySetResult(); if (!release.Wait(TimeSpan.FromSeconds(15))) throw new IOException("Barrier timeout"); } });
                var exports = new StudioExportSession(() => new(picker, publisher)); var vm = Vm(); int captures = 0;
                var export = exports.ExportAsync(1, FlashcardExportFormat.AxoraJson, () => { captures++; return vm.CaptureExportSnapshot(); });
                await admitted.Task;
                var shutdown = speech.ShutdownAsync(exports, () => { hostStops++; return Task.CompletedTask; });
                c.That(ReferenceEquals(shutdown, speech.ShutdownAsync(exports, () => throw new InvalidOperationException())), "Combined App shutdown task identity retained");
                await h.DeadlineEntered.Task; h.DeadlineRelease.SetResult(); await read;
                c.That(!shutdown.IsCompleted && exports.IsActive && hostStops == 0 && captures == 1, "Deferred audio cannot abandon admitted export or dispose H0");
                release.Set(); await shutdown;
                var outcome = await export;
                c.That(outcome.State == ExportResultState.Published && outcome.Publication is { CommitAdmitted: true } && hostStops == 1, "Real publisher verifies/cleans before H0 after optional audio deadline");
                c.That(File.Exists(Path.Combine(root, "cards.json")), "Admitted user file publication survives speech shutdown");
                n.Synthesis.SetResult(); await h.LateCleanup.Task; c.That(h.Owners == 0, "Late speech cleanup has no Host dependency");
            }
            finally { release.Set(); Directory.Delete(root, recursive: true); }
        });
        await c.CaseAsync("ui-composition", () => Sync(() =>
        {
            string source = Source("Axora.Studio/Views/FlashcardsPage.xaml"); XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var xml = XDocument.Parse(source); var elements = xml.Descendants().ToArray();
            var card = elements.Single(e => (string?)e.Attribute(x + "Name") == "CardButton");
            var read = elements.Single(e => (string?)e.Attribute(x + "Name") == "ReadAloudButton");
            var stop = elements.Single(e => (string?)e.Attribute(x + "Name") == "StopReadingButton");
            c.That(read.Parent == stop.Parent && !read.Ancestors().Contains(card) && !stop.Ancestors().Contains(card), "Read/Stop are siblings outside flip control");
            c.That((string?)read.Attribute("AutomationProperties.Name") == "Read aloud" && (string?)stop.Attribute("AutomationProperties.Name") == "Stop reading" && source.Contains("AutomationProperties.LiveSetting=\"Polite\""), "Accessible names and polite status are explicit");
            foreach (var key in new[] { VirtualKey.Space, VirtualKey.Enter })
                c.That(!Views.FlashcardsPage.ShouldHandleStudyKey(key, false, false, true, false), "Focused Read/Stop buttons own Space/Enter once");
            c.That(!Views.FlashcardsPage.ShouldHandleStudyKey(VirtualKey.D, true, false, false, false)
                && !Views.FlashcardsPage.ShouldHandleStudyKey(VirtualKey.D, false, true, false, false), "Handled-event/editing guards preserved");
            c.That(!Source("Axora.Studio/ViewModels/FlashcardsViewModel.cs").Contains(".Focus("), "Async speech status never moves focus");
            c.That(Source("Axora.Studio/Views/HomePage.xaml").Contains("when Windows speech is available"), "Home states conditional installed Windows speech capability");
        }));
        await c.CaseAsync("privacy", async () =>
        {
            var h = new Harness(); var n = h.Add(); n.Fault = "synthesis-task"; n.Synthesis.SetException(new InvalidOperationException("secret academic phrase"));
            var result = await h.Service.ReadAsync(Request("secret academic phrase"));
            c.That(!string.Join("\n", h.Logs).Contains("secret academic phrase") && !result.ReasonCode.Contains("secret"), "Content and raw failure messages stay out of diagnostics/results");
        });
        await c.CaseAsync("playback-fence", async () =>
        {
            var n = new NativeFake(); n.Synthesis.SetResult(); bool allowed = true;
            var backend = new WindowsFlashcardReadAloudBackend(create: () => n);
            var result = await backend.RunAsync(Request(), CancellationToken.None, () => { bool previous = allowed; allowed = false; return previous; }, _ => { });
            c.That(result.Outcome == ReadAloudOutcome.Canceled && n.Plays == 0 && n.Releases.Contains("stream"), "Final playback admission rechecks current generation");
        });
        await c.CaseAsync("host-independent", async () =>
        {
            var h = new Harness(); var n = h.Add(); n.AutoCancel = false; var session = new StudioReadAloudSession(() => h.Service);
            var read = session.ReadAsync(Request()); await n.SynthEntered.Task;
            var exports = new StudioExportSession(() => throw new InvalidOperationException()); int stopped = 0;
            var shutdown = session.ShutdownAsync(exports, () => { stopped++; return Task.CompletedTask; });
            await h.DeadlineEntered.Task; c.That(stopped == 1 && !shutdown.IsCompleted, "H0 can settle independently while optional audio cancellation is pending");
            h.DeadlineRelease.SetResult(); await shutdown; n.Synthesis.SetResult(); await h.LateCleanup.Task;
            c.That(h.Owners == 0 && (await read).Cleanup == ReadAloudCleanup.Deferred && !exports.IsCreated, "Late native cleanup requires neither UI pump nor Host nor export creation");
        });
        await c.CaseAsync("backend-contract-failure", async () =>
        {
            var service = new FlashcardReadAloudService(new FaultedBackend());
            var result = await service.ReadAsync(Request());
            c.That(result.Outcome == ReadAloudOutcome.Unavailable && result.Cleanup == ReadAloudCleanup.Deferred && service.IsActive,
                "Unknown backend settlement never claims resource release or idle");
            c.That((await service.ReadAsync(Request())).Outcome == ReadAloudOutcome.Unavailable && (await service.StopAsync()).Cleanup == ReadAloudCleanup.Deferred,
                "Unknown resource owner closes admission and bounds shutdown observation");
        });
    }

    private static string Source(string relative)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Axora.Studio"))) directory = directory.Parent;
        return File.ReadAllText(Path.Combine(directory!.FullName, relative));
    }
    private sealed class Harness
    {
        private readonly ConcurrentQueue<NativeFake> _natives = new();
        public int Created, Owners, MaximumOwners;
        public readonly ConcurrentQueue<string> Logs = new();
        public readonly TaskCompletionSource DeadlineEntered = Signal(), DeadlineRelease = Signal(), RetryEntered = Signal(), RetryRelease = Signal(), LateCleanup = Signal();
        public FlashcardReadAloudService Service { get; }
        public Harness()
        {
            Action<string> log = message => { Logs.Enqueue(message); if (message.Contains("phase=LateCleanup")) LateCleanup.TrySetResult(); };
            var backend = new WindowsFlashcardReadAloudBackend(log, () =>
            {
                if (!_natives.TryDequeue(out var native)) throw new InvalidOperationException("Missing fake request");
                Interlocked.Increment(ref Created); int owners = Interlocked.Increment(ref Owners); MaximumOwners = Math.Max(MaximumOwners, owners);
                native.OnRelease = () => Interlocked.Decrement(ref Owners); return native;
            }, () => { RetryEntered.TrySetResult(); return RetryRelease.Task; });
            Service = new(backend, token => { DeadlineEntered.TrySetResult(); return DeadlineRelease.Task.WaitAsync(token); }, log);
        }
        public NativeFake Add() { var native = new NativeFake(); _natives.Enqueue(native); return native; }
    }
    private sealed class NativeFake : IReadAloudNativeRequest
    {
        public readonly TaskCompletionSource Synthesis = Signal(), SynthEntered = Signal(), PlayEntered = Signal(), CancelEntered = Signal();
        public readonly List<string> Releases = [];
        public bool AutoCancel = true, AutoEnd, BlockPlayerRelease;
        public string Fault = "", Text = "";
        public int Plays;
        public Action? OnRelease;
        private Action? _end, _fail;
        public Task SynthesizeAsync(string text)
        {
            Text = text; SynthEntered.TrySetResult();
            if (Fault == "synthesis-start") throw new InvalidOperationException("sensitive synthesis error");
            return Synthesis.Task;
        }
        public bool HasUsableStream => Fault != "invalid-stream";
        public void CancelSynthesis() { CancelEntered.TrySetResult(); if (AutoCancel) Synthesis.TrySetCanceled(); }
        public void Play(Action started, Action ended, Action failed)
        {
            if (Fault is "source" or "play") throw new InvalidOperationException("sensitive playback error");
            Plays++; _end = ended; _fail = failed; started(); PlayEntered.TrySetResult(); if (AutoEnd) ended();
        }
        public void End() => _end?.Invoke(); public void Fail() => _fail?.Invoke();
        public void DetachHandlers() { Releases.Add("detach"); if (Fault == "detach") throw new InvalidOperationException(); }
        public void PauseAndClearSource() => Releases.Add("pause-clear");
        public void ReleasePlayer() { if (BlockPlayerRelease) throw new InvalidOperationException(); Releases.Add("player"); }
        public void ReleaseSource() => Releases.Add("source");
        public void ReleaseStream() => Releases.Add("stream");
        public void ReleaseSynthesizer() { Releases.Add("synth"); OnRelease?.Invoke(); }
    }
    private sealed class Picker : IStudioSavePicker
    {
        public readonly TaskCompletionSource Entered = Signal();
        public readonly TaskCompletionSource<StudioPickerResult> Selection = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<StudioPickerResult> SelectAsync(nint owner, FlashcardExportFormat format, string suggestedName, CancellationToken token)
        { Entered.TrySetResult(); return Selection.Task; }
        public Task<StudioPickerState> ConfirmReplacementAsync(nint owner, Guid operationId, ExportDestinationPlan plan, CancellationToken token) => Task.FromResult(StudioPickerState.Selected);
        public void RequestCancel() => Selection.TrySetResult(new(StudioPickerState.Canceled));
    }
    private sealed class FaultedBackend : IFlashcardReadAloudBackend
    {
        public Task<ReadAloudResult> RunAsync(ReadAloudRequest request, CancellationToken cancellation, Func<bool> mayPlay, Action<ReadAloudProgress> progress)
            => Task.FromException<ReadAloudResult>(new InvalidOperationException("sensitive backend failure"));
    }
}
