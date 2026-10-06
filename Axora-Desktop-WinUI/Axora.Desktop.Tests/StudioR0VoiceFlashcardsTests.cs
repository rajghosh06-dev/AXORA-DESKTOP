using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Helpers;
using Axora.Desktop.Models;
using Axora.Desktop.Models.Voice;
using Axora.Desktop.Services;
using Axora.Desktop.Services.Contracts;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop.Tests;

public partial class Program
{
    private static async Task RunStudioR0VoiceFlashcardsTests()
    {
        Console.WriteLine("--- Studio R0: terminal voice orchestration and Flashcards ownership ---");

        VerifyR0ContractShape();
        await VerifyProductionRecognitionAdapterAsync();
        await VerifyPostNativeStartupPublicationAsync();
        await VerifyCoordinatorActivationHandshakeAsync();
        await VerifyPersistedNavigationRestartAsync();
        await VerifyTerminalSettlementRacesAsync();
        await VerifyFlashcardsRequestOwnershipAsync();
        await VerifyFlashcardsCancellationRacesAsync();
        await VerifyRecognitionStartTruthAsync();
        await VerifyCommandBridgeAndGenerationAsync();
        await VerifyDispatcherFailureIsTerminalAsync();
        await VerifyPlaybackTerminalStateAndResumeAsync();
        await VerifyPlaybackResultsAsync();
        await VerifySpeechSerializationAndCancellationIsolationAsync();
        await VerifyExplicitStopAndDeviceLossAsync();
        await VerifyCrossedRecognitionModesAsync();
        await VerifySuspendedDeviceLossAsync();
        await VerifyBoundedFifoAsync();
        await VerifyScholarCrossFeatureIsolationAsync();
        await VerifyShellDesiredToggleAsync();
        await VerifySettingsTestSpeechUsesCoordinatorAsync();
        await VerifyR0ShutdownOwnershipAsync();
        VerifyScholarLegacyFlashcardsPush();
    }

    private static void VerifyScholarLegacyFlashcardsPush()
    {
        Console.WriteLine("--- M1 legacy fallback: production Scholar Push command ---");
        const string success = "Created flashcards in AXORA Desktop.";
        const string empty = "Scholar content is empty. Enter text first.";
        const string unavailable = "Flashcards are unavailable in AXORA Desktop.";
        const string noCards = "No flashcards were created. Existing decks are unchanged.";
        const string failed = "Couldn't create flashcards. Existing decks are unchanged.";
        const string manual = "Created cards in AXORA Desktop. Open Flashcards manually.";

        (ScholarKitViewModel Scholar, FlashcardsViewModel Flashcards) Fixture()
        {
            var voice = new R0FeatureVoiceCoordinator { AutoComplete = true };
            var scholar = new ScholarKitViewModel(new DummyOcrService(), new DummyPdfExtractionService(),
                new DummyDocumentProcessorService(), new DummyVoiceTranscriberService(),
                new DummyDocumentChatService(), new W4MockSynthesizer(), new DummyScannerService(),
                new AppSettingsService(customDirectory: Path.Combine(Path.GetTempPath(), "AxoraPush_" + Guid.NewGuid().ToString("N"))),
                voiceCoordinator: voice);
            var flashcards = new FlashcardsViewModel(voice);
            flashcards.NextCard();
            flashcards.FlipCard();
            flashcards.ExportStatus = "Prior export status";
            scholar.FlashcardsPushTargetOverride = () => flashcards;
            return (scholar, flashcards);
        }

        Action PreservedState(FlashcardsViewModel vm, string name)
        {
            var decks = vm.Decks.ToArray();
            var active = vm.ActiveDeck;
            var card = vm.CurrentCard;
            int index = vm.CurrentCardIndex;
            bool flipped = vm.IsCardFlipped;
            string progress = vm.SessionProgress, stats = vm.DeckStats, export = vm.ExportStatus;
            var studied = active!.LastStudied;
            int reviews = card!.ReviewCount;
            return () => Assert(vm.Decks.SequenceEqual(decks) && ReferenceEquals(vm.ActiveDeck, active) &&
                ReferenceEquals(vm.CurrentCard, card) && vm.CurrentCardIndex == index && vm.IsCardFlipped == flipped &&
                vm.SessionProgress == progress && vm.DeckStats == stats && vm.ExportStatus == export &&
                active.LastStudied == studied && card.ReviewCount == reviews,
                $"M1-PUSH {name}: existing deck identities, selected card, review and study state preserved");
        }

        foreach (string? text in new string?[] { null, "", " ", "\r\n\t" })
        {
            var (scholar, flashcards) = Fixture();
            using (scholar) using (flashcards)
            {
                var check = PreservedState(flashcards, "empty");
                int resolves = 0, navigations = 0;
                scholar.FlashcardsPushTargetOverride = () => { resolves++; return flashcards; };
                scholar.FlashcardsPushNavigationOverride = _ => navigations++;
                scholar.OcrResultText = text!;
                scholar.LastOperationStatus = "Stale success";
                scholar.PushToFlashcardsCommand.Execute(null);
                Assert(scholar.LastOperationStatus == empty && resolves == 0 && navigations == 0,
                    "M1-PUSH empty: explicit feedback precedes target resolution; no navigation or stale success");
                check();
            }
        }

        foreach (bool throwResolver in new[] { false, true })
        {
            var (scholar, flashcards) = Fixture();
            using (scholar) using (flashcards)
            {
                var check = PreservedState(flashcards, "unavailable");
                int navigations = 0;
                scholar.OcrResultText = "Valid study term: a sufficiently long definition";
                scholar.FlashcardsPushTargetOverride = () => throwResolver ?
                    throw new InvalidOperationException("PRIVATE CONTENT C:\\private\\paper.pdf") : null;
                scholar.FlashcardsPushNavigationOverride = _ => navigations++;
                scholar.PushToFlashcardsCommand.Execute(null);
                Assert(scholar.LastOperationStatus == unavailable && navigations == 0,
                    "M1-PUSH unavailable: absent/throwing target produces safe status and no navigation");
                check();
            }
        }

        foreach (string text in new[] { "Study term: a sufficiently long definition", "abc", "Unstructured source without a question pair" })
        {
            var (scholar, flashcards) = Fixture();
            using (scholar) using (flashcards)
            {
                var oldDecks = flashcards.Decks.ToArray();
                int navigations = 0;
                scholar.OcrResultText = text;
                // HasLoadedDocument remains false: manually entered text is a real supported source.
                scholar.FlashcardsPushNavigationOverride = route =>
                {
                    navigations++;
                    Assert(route == "Flashcards" && flashcards.Decks.Count == oldDecks.Length + 1 &&
                        flashcards.ActiveDeck is { CardCount: > 0 } created && !oldDecks.Contains(created) &&
                        scholar.LastOperationStatus == success && flashcards.ExportStatus == success,
                        "M1-PUSH success: real nonempty insertion and exact Desktop copy precede legacy route request");
                };
                scholar.PushToFlashcardsCommand.Execute(null);
                Assert(navigations == 1 && scholar.LastOperationStatus == success &&
                    !scholar.LastOperationStatus.Contains("Studio", StringComparison.Ordinal) &&
                    oldDecks.All(flashcards.Decks.Contains),
                    "M1-PUSH success: exact truthful destination, one navigation, prior decks retained");
            }
        }

        foreach (string fault in new[] { "zero-cards", "no-insertion", "insertion-exception", "selection-exception" })
        {
            var (scholar, flashcards) = Fixture();
            using (scholar) using (flashcards)
            {
                var check = PreservedState(flashcards, fault);
                int navigations = 0;
                bool injected = false;
                scholar.OcrResultText = "Study term: a sufficiently long definition";
                scholar.FlashcardsPushNavigationOverride = _ => navigations++;
                flashcards.Decks.CollectionChanged += (_, args) =>
                {
                    if (injected || args.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Add) return;
                    injected = true;
                    var created = (FlashcardDeck)args.NewItems![0]!;
                    if (fault == "zero-cards") created.Cards.Clear();
                    if (fault == "no-insertion") flashcards.Decks.Remove(created);
                    if (fault == "insertion-exception") throw new InvalidOperationException("PRIVATE Scholar content");
                };
                bool selectionFault = false;
                flashcards.PropertyChanged += (_, args) =>
                {
                    if (fault == "selection-exception" && !selectionFault && args.PropertyName == nameof(flashcards.ActiveDeck))
                    {
                        selectionFault = true;
                        throw new InvalidOperationException("PRIVATE Scholar content");
                    }
                };
                scholar.PushToFlashcardsCommand.Execute(null);
                Assert(scholar.LastOperationStatus == (fault.EndsWith("exception", StringComparison.Ordinal) ? failed : noCards) &&
                    navigations == 0 && injected,
                    $"M1-PUSH {fault}: real generator/command guarded; no false success, content leak or navigation");
                check();
            }
        }

        foreach (bool throwNavigation in new[] { false, true })
        {
            var (scholar, flashcards) = Fixture();
            using (scholar) using (flashcards)
            {
                int before = flashcards.Decks.Count;
                scholar.OcrResultText = "Study term: a sufficiently long definition";
                if (throwNavigation) scholar.FlashcardsPushNavigationOverride = _ =>
                    throw new InvalidOperationException("PRIVATE C:\\private\\paper.pdf");
                // The console test has no MainWindow: the null default navigator is also exercised.
                scholar.PushToFlashcardsCommand.Execute(null);
                Assert(scholar.LastOperationStatus == manual && flashcards.ExportStatus == manual &&
                    flashcards.Decks.Count == before + 1 && flashcards.ActiveDeck is { CardCount: > 0 },
                    "M1-PUSH navigation: absent/throwing navigation retains created cards with safe manual guidance");
            }
        }

        Assert(ShellViewModel.PageMap["Flashcards"].PageType == typeof(Axora.Desktop.Views.FlashcardsPage),
            "M1-PUSH route: Flashcards remains the legacy Desktop page, not a Studio handoff");
        Assert(typeof(ScholarKitViewModel).Assembly.GetReferencedAssemblies().All(a => a.Name != "Axora.Studio"),
            "M1-PUSH boundary: legacy command assembly has no Studio reference");
    }

    private static void VerifyR0ContractShape()
    {
        Type[] flashDependencies = typeof(FlashcardsViewModel).GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType).ToArray();
        Assert(flashDependencies.Contains(typeof(IVoiceCoordinator)) &&
               !flashDependencies.Contains(typeof(ISpeechSynthesisService)),
            "R0-A: Flashcards depends on the coordinator and has no low-level synthesis dependency");

        int recognitionStarts = typeof(IVoiceTranscriberService).GetMethods()
            .Count(method => method.Name == nameof(IVoiceTranscriberService.StartDictationAsync));
        Assert(recognitionStarts == 1,
            "R0-B: transcriber exposes one coherent callback/session start contract");

        string[] routerMembers = typeof(IVoiceCommandRouter).GetMembers().Select(member => member.Name).ToArray();
        Assert(!routerMembers.Contains("IsListening") &&
               !routerMembers.Contains("StartListeningAsync") &&
               !routerMembers.Contains("StopListeningAsync"),
            "R0-C: command router no longer claims microphone/listening lifecycle ownership");

        Assert(Enum.GetNames<SpeechPlaybackResult>().SequenceEqual(
                ["Completed", "Canceled", "Unavailable", "Failed"]),
            "R0-D: speech contract contains only the four frozen terminal results");

        Type? activePlayback = typeof(MediaPlayerSpeechPlaybackBackend)
            .GetNestedType("ActivePlayback", BindingFlags.NonPublic);
        string[] ownedProperties = activePlayback?.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name).ToArray() ?? [];
        Assert(ownedProperties.Contains("Stream") && ownedProperties.Contains("Source") &&
               ownedProperties.Contains("Completion") && ownedProperties.Contains("CancellationRegistration"),
            "R0-E: production backend retains stream, source, completion and cancellation ownership");
    }

    private static async Task VerifyFlashcardsRequestOwnershipAsync()
    {
        var voice = new R0FeatureVoiceCoordinator();
        var vm = new FlashcardsViewModel(voice);
        Task speaking = vm.SpeakCurrentCardAsync();
        await voice.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert(vm.IsSpeaking && !speaking.IsCompleted,
            "R0-F: Flashcards IsSpeaking remains true for the pending terminal request");

        vm.CancelSpeechRequest();
        await speaking.WaitAsync(TimeSpan.FromSeconds(2));
        Assert(!vm.IsSpeaking && voice.LastResult == SpeechPlaybackResult.Canceled,
            "R0-G: navigation/request cancellation terminates only the Flashcards request");

        vm.Dispose();
        vm.Dispose();
        Assert(voice.StopSpeechCount == 0 && voice.DisposeCount == 0,
            "R0-H: idempotent Flashcards disposal neither globally stops nor disposes voice services");
    }

    private static async Task VerifyFlashcardsCancellationRacesAsync()
    {
        var voice = new R0FeatureVoiceCoordinator();
        var vm = new FlashcardsViewModel(voice);
        var requests = new List<Task>();
        for (int i = 0; i < 40; i++)
        {
            requests.Add(vm.SpeakCurrentCardAsync());
            if (i % 2 == 0) vm.CancelSpeechRequest();
        }
        vm.CancelSpeechRequest();
        await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(2));
        Assert(!vm.IsSpeaking && voice.StopSpeechCount == 0,
            "R1-F6: rapid Speak replacement and page-style cancellation settle without global stop");

        Task reentered = vm.SpeakCurrentCardAsync();
        vm.CancelSpeechRequest();
        await reentered.WaitAsync(TimeSpan.FromSeconds(2));
        vm.Dispose();
        vm.Dispose();
        Assert(!vm.IsSpeaking && voice.DisposeCount == 0,
            "R1-F6b: navigation re-entry and idempotent VM disposal leave no owned request");

        var disposingVoice = new R0FeatureVoiceCoordinator();
        var disposingVm = new FlashcardsViewModel(disposingVoice);
        Task disposingRequest = disposingVm.SpeakCurrentCardAsync();
        disposingVm.Dispose();
        await disposingRequest.WaitAsync(TimeSpan.FromSeconds(2));
        Assert(!disposingVm.IsSpeaking && disposingVoice.StopSpeechCount == 0,
            "R1-F6c: disposing during active Speak cancels only the owned request");

        var racingVoice = new R0FeatureVoiceCoordinator();
        var racingVm = new FlashcardsViewModel(racingVoice);
        Task racingRequest = racingVm.SpeakCurrentCardAsync();
        await Task.WhenAll(Task.Run(racingVm.CancelSpeechRequest),
            Task.Run(() => racingVoice.CompleteCurrent(SpeechPlaybackResult.Completed)));
        await racingRequest.WaitAsync(TimeSpan.FromSeconds(2));
        Assert(!racingVm.IsSpeaking && racingVoice.StopSpeechCount == 0,
            "R1-F6d: navigation cancellation racing terminal completion does not dispose before Cancel");
        racingVm.Dispose();
    }

    private static async Task VerifyRecognitionStartTruthAsync()
    {
        var transcriber = new R0Transcriber { StartResult = VoiceRecognitionStartResult.Unavailable };
        var speech = new R0ControlledSpeech();
        var fixture = CreateR0Coordinator(transcriber, speech);
        try
        {
            VoiceRecognitionStartResult dictation = await fixture.Coordinator.RequestStartDictationAsync(_ => { });
            Assert(dictation == VoiceRecognitionStartResult.Unavailable &&
                   fixture.Coordinator.CurrentState != VoiceSessionState.Dictating,
                "R0-I: failed recognition startup cannot publish Dictating");

            transcriber.StartResult = VoiceRecognitionStartResult.PermissionDenied;
            VoiceRecognitionStartResult command = await fixture.Coordinator.StartVoiceNavigationAsync();
            Assert(command == VoiceRecognitionStartResult.PermissionDenied &&
                   fixture.Coordinator.CurrentState != VoiceSessionState.ListeningForCommand,
                "R0-J: failed command startup cannot publish ListeningForCommand");
        }
        finally { await DisposeFixtureAsync(fixture); }
    }

    private static async Task VerifyProductionRecognitionAdapterAsync()
    {
        var nativeStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int delivered = 0;
        var ended = new VoiceTranscriberService.RecognitionSession(_ => delivered++);
        Task<bool> start = ended.StartAsync(() => nativeStart.Task);
        ended.Complete();
        nativeStart.SetResult();
        bool resurrected = await start;
        ended.Deliver(new object(), "open dashboard", true);
        Assert(!resurrected && !ended.IsRecording && delivered == 0,
            "R1-F3: native completion while StartAsync waits cannot resurrect capture or its callback");

        var chunks = new List<TranscriptionChunk>();
        var adapter = new VoiceTranscriberService.RecognitionSession(chunks.Add);
        Assert(await adapter.StartAsync(() => Task.CompletedTask) && adapter.IsRecording,
            "R1-F3b: a live native start makes the production session adapter recording");
        object firstNativeResult = new();
        adapter.Deliver(firstNativeResult, "open dashboard", true);
        adapter.Deliver(firstNativeResult, "open dashboard", true);
        adapter.Deliver(new object(), "open dashboard", true);
        Assert(chunks.Count == 3 && chunks[0].SequenceNumber == chunks[1].SequenceNumber &&
               chunks[2].SequenceNumber != chunks[0].SequenceNumber,
            "R1-F2a: production adapter binds duplicate identity to one ID and later identical text to a new ID");
        adapter.Complete();
        Assert(!adapter.IsRecording,
            "R1-F3c: completing the production session adapter clears recording state");

        var nativePending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transcriber = new R0Transcriber { DeferredNativeStart = nativePending };
        var fixture = CreateR0Coordinator(transcriber, new R0ControlledSpeech());
        try
        {
            Task<VoiceRecognitionStartResult> commandStart = fixture.Coordinator.StartVoiceNavigationAsync();
            await WaitUntilAsync(() => transcriber.DeferredSessionStarted);
            transcriber.CompleteDeferredNativeStart();
            nativePending.SetResult();
            VoiceRecognitionStartResult result = await commandStart.WaitAsync(TimeSpan.FromSeconds(2));
            Assert(result != VoiceRecognitionStartResult.Started && !transcriber.IsRecording &&
                   fixture.Coordinator.CurrentState != VoiceSessionState.ListeningForCommand,
                "R1-F3d: pending native completion cannot make the coordinator advertise command capture");
        }
        finally { await DisposeFixtureAsync(fixture); }
    }

    private static async Task VerifyPostNativeStartupPublicationAsync()
    {
        var boundary = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startup = new TaskCompletionSource<VoiceRecognitionStartResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var notifications = new List<bool>();
        int delivered = 0;
        var session = new VoiceTranscriberService.RecognitionSession(_ => delivered++);
        Task operation = session.StartAsync(() => Task.CompletedTask, startup,
            () => notifications.Add(true), async () =>
            {
                boundary.TrySetResult();
                await release.Task;
            });
        await boundary.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert(!startup.Task.IsCompleted && !session.IsRecording,
            "R2-F1a: native start succeeds before the production session's final publication boundary");
        session.Complete(() => notifications.Add(false));
        release.SetResult();
        await operation.WaitAsync(TimeSpan.FromSeconds(2));
        session.Deliver(new object(), "open dashboard", true);
        Assert(await startup.Task != VoiceRecognitionStartResult.Started && !session.IsRecording &&
               delivered == 0 && notifications.SequenceEqual(new[] { false }),
            "R2-F1b: post-native completion wins publication, releases callback, and cannot claim live Started");

        var active = new VoiceTranscriberService.RecognitionSession(_ => delivered++);
        var activated = new TaskCompletionSource<VoiceRecognitionStartResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        notifications.Clear();
        await active.StartAsync(() => Task.CompletedTask, activated, () => notifications.Add(true));
        Assert(activated.Task.IsCompletedSuccessfully && await activated.Task == VoiceRecognitionStartResult.Started && active.IsRecording,
            "R2-F1c: activation publishes the actual public start Task while the production session is live");
        active.Complete(() => notifications.Add(false));
        active.Deliver(new object(), "open dashboard", true);
        Assert(!active.IsRecording && delivered == 0 && notifications.SequenceEqual(new[] { true, false }),
            "R2-F1d: activation first permits only Active to Completed and clears callback ownership");
        bool staleActivation = active.TryActivate(() => delivered++);
        Assert(!staleActivation && delivered == 0,
            "R2-F1d2: a physically completed session rejects activation even before a consumer processes its notification");

        var reentrant = new VoiceTranscriberService.RecognitionSession(_ => delivered++);
        var reentrantStart = new TaskCompletionSource<VoiceRecognitionStartResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        await reentrant.StartAsync(() => Task.CompletedTask, reentrantStart, () => reentrant.Complete());
        Assert(await reentrantStart.Task != VoiceRecognitionStartResult.Started && !reentrant.IsRecording,
            "R2-F1e: synchronous completion during the start notification cannot publish Started afterward");
    }

    private static async Task VerifyCoordinatorActivationHandshakeAsync()
    {
        foreach (bool command in new[] { true, false })
        foreach (bool delayedNotification in new[] { false, true })
        {
            var router = new VoiceCommandRouter();
            int dispatches = 0;
            router.RegisterCommand(new VoiceCommandRegistration(
                "R2.Test", "open dashboard", [], VoiceCommandCategory.Navigation,
                CommandSafetyLevel.Safe, _ => { Interlocked.Increment(ref dispatches); return Task.CompletedTask; }));
            var fixture = CreateR0Coordinator(new R0Transcriber(), new R0ControlledSpeech(), router);
            var states = new List<VoiceSessionState>();
            int dictationChunks = 0;
            fixture.Coordinator.StateChanged += (_, args) => states.Add(args.NewState);
            fixture.Coordinator.BeforeRecognitionActivation = () =>
            {
                // Production has already received Started and read IsRecording.
                if (delayedNotification) fixture.Transcriber.CompletePhysicalSessionWithoutNotification();
                else fixture.Transcriber.EndUnexpectedly();
                fixture.Transcriber.CallbackHistory[0](new TranscriptionChunk("open dashboard", "open dashboard", true, 1));
            };
            try
            {
                VoiceRecognitionStartResult result = command
                    ? await fixture.Coordinator.StartVoiceNavigationAsync()
                    : await fixture.Coordinator.RequestStartDictationAsync(_ => dictationChunks++);
                string? activeMode = typeof(VoiceCoordinator).GetField("_activeRecognitionMode",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(fixture.Coordinator)?.ToString();
                Assert(result != VoiceRecognitionStartResult.Started && !fixture.Transcriber.IsRecording && activeMode == "None" &&
                       !states.Contains(VoiceSessionState.ListeningForCommand) && !states.Contains(VoiceSessionState.Dictating),
                    $"R2-F1f: pending command={command}, delayed-notification={delayedNotification} cannot publish completed capture");
                Assert(!fixture.Coordinator.IsVoiceNavigationDesired && fixture.Coordinator.CurrentState == VoiceSessionState.Idle &&
                       dispatches == 0 && dictationChunks == 0,
                    $"R2-F1g: completed {command} pending generation clears intent and rejects stale transcripts");

                Action<AudioCaptureHealth> oldCompletion = fixture.Transcriber.CompletionNotification!;
                fixture.Coordinator.BeforeRecognitionActivation = null;
                VoiceRecognitionStartResult retry = command
                    ? await fixture.Coordinator.StartVoiceNavigationAsync()
                    : await fixture.Coordinator.RequestStartDictationAsync(_ => dictationChunks++);
                Assert(retry == VoiceRecognitionStartResult.Started && fixture.Transcriber.IsRecording,
                    $"R2-F1h: a newer explicit {command} generation survives reconciliation of the old pending stop");
                oldCompletion(AudioCaptureHealth.Healthy);
                Assert(fixture.Transcriber.IsRecording && fixture.Coordinator.CurrentState ==
                       (command ? VoiceSessionState.ListeningForCommand : VoiceSessionState.Dictating),
                    "R2-F1h2: a delayed completion closure from an old session cannot invalidate its replacement");
                fixture.Transcriber.EndUnexpectedly();
                await WaitUntilAsync(() => fixture.Coordinator.CurrentState == VoiceSessionState.Idle);
                Assert(!fixture.Coordinator.IsVoiceNavigationDesired && !fixture.Transcriber.IsRecording,
                    $"R2-F1i: normally activated {command} generation reconciles a later physical completion");
            }
            finally { await DisposeFixtureAsync(fixture); }
        }
    }

    private static async Task VerifyPersistedNavigationRestartAsync()
    {
        foreach (VoiceRecognitionStartResult initial in new[]
                 {
                     VoiceRecognitionStartResult.Started, VoiceRecognitionStartResult.Unavailable,
                     VoiceRecognitionStartResult.Failed, VoiceRecognitionStartResult.PermissionDenied,
                     VoiceRecognitionStartResult.Canceled
                 })
        {
            string path = Path.Combine(Path.GetTempPath(), "AxoraR2Restart_" + Guid.NewGuid().ToString("N"));
            var saved = new AppSettingsService(customDirectory: path) { IsVoiceNavigationEnabled = true };
            saved.Save();
            var settings = new AppSettingsService(customDirectory: path);
            var transcriber = new R0Transcriber { StartResult = initial };
            var speech = new R0ControlledSpeech();
            var audio = new R0AudioMonitor();
            var coordinator = new VoiceCoordinator(transcriber, speech, new VoiceCommandRouter(), audio,
                new VoiceTextFormatter(), settings) { AcousticDebounceInterval = TimeSpan.Zero };
            var shell = new ShellViewModel(coordinator);
            try
            {
                Assert(settings.IsVoiceNavigationEnabled && !coordinator.IsVoiceNavigationDesired &&
                       coordinator.CurrentState == VoiceSessionState.Idle && transcriber.StartCallCount == 0,
                    $"R2-F2a: persisted-true restart {initial} begins inactive without opening the microphone");
                await shell.ToggleVoiceNavigationAsync();
                Assert(transcriber.StartCallCount == 1 && transcriber.StopCallCount == 0,
                    $"R2-F2b: first persisted-true Shell toggle {initial} chooses Start, never preference-driven Stop");
                if (initial == VoiceRecognitionStartResult.Started)
                {
                    Assert(coordinator.IsVoiceNavigationDesired && coordinator.CurrentState == VoiceSessionState.ListeningForCommand && shell.IsVoiceListening,
                        "R2-F2c: persisted-true first runtime start activates command navigation normally");
                }
                else
                {
                    Assert(!coordinator.IsVoiceNavigationDesired && !shell.IsVoiceListening &&
                           coordinator.CurrentState != VoiceSessionState.ListeningForCommand && settings.IsVoiceNavigationEnabled,
                        $"R2-F2d: {initial} clears runtime desire and listening without rewriting the saved preference");
                    transcriber.StartResult = VoiceRecognitionStartResult.Started;
                    await shell.ToggleVoiceNavigationAsync();
                    Assert(transcriber.StartCallCount == 2 && coordinator.IsVoiceNavigationDesired && shell.IsVoiceListening,
                        $"R2-F2e: {initial} leaves the next explicit Shell start available");
                }
            }
            finally
            {
                shell.Dispose();
                await coordinator.StopAsync();
                coordinator.Dispose();
                speech.Dispose(); transcriber.Dispose(); audio.Dispose();
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
        }
    }

    private static async Task VerifyTerminalSettlementRacesAsync()
    {
        foreach (var race in new[]
                 {
                     ("MediaEnded/cancellation", SpeechPlaybackResult.Completed, SpeechPlaybackResult.Canceled),
                     ("MediaFailed/cancellation", SpeechPlaybackResult.Failed, SpeechPlaybackResult.Canceled),
                     ("Stop/MediaEnded", SpeechPlaybackResult.Canceled, SpeechPlaybackResult.Completed),
                     ("Dispose/MediaEnded", SpeechPlaybackResult.Canceled, SpeechPlaybackResult.Completed)
                 })
        {
            var terminal = new MediaPlayerSpeechPlaybackBackend.TerminalSettlement();
            int cleanup = 0;
            bool Signal(SpeechPlaybackResult result) => terminal.TryComplete(result, true,
                detachHandlers: () => Interlocked.Increment(ref cleanup),
                pause: () => { }, clearSource: () => { }, disposePlayer: () => { },
                unregister: () => { }, disposeSource: () => { }, disposeStream: () => { },
                afterCleanup: _ => { }, reportFailure: _ => { });
            bool[] winners = await Task.WhenAll(
                Task.Run(() => Signal(race.Item2)), Task.Run(() => Signal(race.Item3)));
            SpeechPlaybackResult result = await terminal.Completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert(winners.Count(winner => winner) == 1 && cleanup == 1 &&
                   (result == race.Item2 || result == race.Item3),
                $"R1-F1: {race.Item1} has one terminal winner and one cleanup");
        }

        foreach (string failure in new[] { "clear", "source", "stream", "detach", "pause", "unregister" })
        {
            var terminal = new MediaPlayerSpeechPlaybackBackend.TerminalSettlement();
            int attempts = 0;
            int fallback = 0;
            Action FailIf(string step) => () =>
            {
                Interlocked.Increment(ref attempts);
                if (failure == step) throw new InvalidOperationException(step);
            };
            terminal.TryComplete(SpeechPlaybackResult.Completed, true,
                FailIf("detach"), FailIf("pause"), FailIf("clear"),
                () => Interlocked.Increment(ref fallback), FailIf("unregister"),
                FailIf("source"), FailIf("stream"), _ => { }, _ => { });
            Assert(await terminal.Completion.Task.WaitAsync(TimeSpan.FromSeconds(2)) == SpeechPlaybackResult.Failed &&
                   attempts >= 3 && (failure != "clear" || fallback == 1),
                $"R1-F1b: {failure} cleanup failure still publishes Failed after attempting cleanup");
        }

        {
            var terminal = new MediaPlayerSpeechPlaybackBackend.TerminalSettlement();
            bool sourceDisposed = false;
            bool streamDisposed = false;
            bool retained = false;
            terminal.TryComplete(SpeechPlaybackResult.Completed, false,
                () => { }, () => { },
                () => throw new InvalidOperationException("source clear failed"),
                () => throw new InvalidOperationException("player disposal failed"),
                () => { }, () => sourceDisposed = true, () => streamDisposed = true,
                detached => retained = !detached, _ => { });
            Assert(await terminal.Completion.Task.WaitAsync(TimeSpan.FromSeconds(2)) == SpeechPlaybackResult.Failed &&
                   retained && !sourceDisposed && !streamDisposed,
                "R1-F1d: failed source detach settles Failed and retains media resources safely");
        }

        var generations = new MediaPlayerSpeechPlaybackBackend.TerminalGenerationGate();
        generations.Begin(1);
        bool firstClaimed = generations.TryClaim(1);
        generations.Begin(2);
        bool staleClaimed = generations.TryClaim(1);
        bool currentClaimed = generations.TryClaim(2);
        Assert(firstClaimed && !staleClaimed && currentClaimed && !generations.TryClaim(2),
            "R1-F1c: a late old-generation native event cannot claim newer playback");
    }

    private static async Task VerifyCommandBridgeAndGenerationAsync()
    {
        var transcriber = new R0Transcriber();
        var speech = new R0ControlledSpeech();
        var router = new VoiceCommandRouter();
        int dispatches = 0;
        router.RegisterCommand(new VoiceCommandRegistration(
            "R0.Test", "open dashboard", [], VoiceCommandCategory.Navigation,
            CommandSafetyLevel.Safe, _ => { Interlocked.Increment(ref dispatches); return Task.CompletedTask; }));
        var fixture = CreateR0Coordinator(transcriber, speech, router);
        try
        {
            Assert(await fixture.Coordinator.StartVoiceNavigationAsync() == VoiceRecognitionStartResult.Started,
                "R0-K: command mode starts a real transcriber session");
            var adapter = new VoiceTranscriberService.RecognitionSession(transcriber.EmitCurrent);
            await adapter.StartAsync(() => Task.CompletedTask);
            object firstNativeResult = new();
            adapter.Deliver(firstNativeResult, "open dashboard", true);
            await WaitUntilAsync(() => Volatile.Read(ref dispatches) == 1);
            adapter.Deliver(firstNativeResult, "open dashboard", true);
            await Task.Delay(30);
            Assert(dispatches == 1,
                "R1-F2: duplicate delivery of the same native final result dispatches once");
            adapter.Deliver(new object(), "open dashboard", true);
            await WaitUntilAsync(() => Volatile.Read(ref dispatches) == 2);
            Assert(dispatches == 2,
                "R1-F2b: a later distinct native result with identical text still dispatches");

            Action<TranscriptionChunk> oldCallback = transcriber.CallbackHistory[0];
            await fixture.Coordinator.StopVoiceNavigationAsync();
            await fixture.Coordinator.StartVoiceNavigationAsync();
            oldCallback(new TranscriptionChunk("open dashboard", "open dashboard", true, 2));
            var restartedAdapter = new VoiceTranscriberService.RecognitionSession(transcriber.EmitCurrent);
            await restartedAdapter.StartAsync(() => Task.CompletedTask);
            restartedAdapter.Deliver(new object(), "open dashboard", true);
            await WaitUntilAsync(() => Volatile.Read(ref dispatches) == 3);
            Assert(dispatches == 3,
                "R0-M: old-generation transcripts are ignored while the current final transcript dispatches");
        }
        finally { await DisposeFixtureAsync(fixture); }
    }

    private static async Task VerifyDispatcherFailureIsTerminalAsync()
    {
        Task rejected = DispatcherHelper.RunWithEnqueueAsync(_ => false, () => { });
        bool failed = false;
        try { await rejected.WaitAsync(TimeSpan.FromSeconds(1)); }
        catch (InvalidOperationException) { failed = true; }
        Assert(failed && rejected.IsCompleted,
            "R0-N: dispatcher enqueue rejection faults instead of leaving an incomplete await");
    }

    private static async Task VerifyPlaybackTerminalStateAndResumeAsync()
    {
        var log = new List<string>();
        var transcriber = new R0Transcriber(log);
        var speech = new R0ControlledSpeech(log);
        var fixture = CreateR0Coordinator(transcriber, speech);
        try
        {
            await fixture.Coordinator.StartVoiceNavigationAsync();
            Task<SpeechPlaybackResult> request = fixture.Coordinator.RequestSpeakAsync("terminal playback");
            await speech.WaitForStartsAsync(1);
            await WaitUntilAsync(() => fixture.Coordinator.CurrentState == VoiceSessionState.Synthesizing);
            Assert(!request.IsCompleted && !transcriber.IsRecording &&
                   log.IndexOf("recognition-stop") < log.IndexOf("speech-start:terminal playback"),
                "R0-O: recognition stops before playback and the speech await remains pending");

            speech.CompleteCurrent(SpeechPlaybackResult.Completed);
            Assert(await request.WaitAsync(TimeSpan.FromSeconds(2)) == SpeechPlaybackResult.Completed,
                "R0-P: Completed is returned only after the controlled terminal event");
            Assert(fixture.Coordinator.CurrentState == VoiceSessionState.ListeningForCommand &&
                   transcriber.StartCallCount == 2,
                "R0-Q: matching terminal completion resumes the current desired command mode");
        }
        finally { await DisposeFixtureAsync(fixture); }
    }

    private static async Task VerifyPlaybackResultsAsync()
    {
        foreach (SpeechPlaybackResult expected in new[]
                 { SpeechPlaybackResult.Failed, SpeechPlaybackResult.Unavailable })
        {
            var transcriber = new R0Transcriber();
            var speech = new R0ControlledSpeech { ImmediateResult = expected };
            var fixture = CreateR0Coordinator(transcriber, speech);
            try
            {
                SpeechPlaybackResult actual = await fixture.Coordinator.RequestSpeakAsync("result probe");
                Assert(actual == expected && fixture.Coordinator.CurrentState == VoiceSessionState.Idle,
                    $"R0-R-{expected}: optional playback returns {expected} without failing the host");
            }
            finally { await DisposeFixtureAsync(fixture); }
        }

        var cancelFixture = CreateR0Coordinator(new R0Transcriber(), new R0ControlledSpeech());
        try
        {
            using var cts = new CancellationTokenSource();
            Task<SpeechPlaybackResult> canceled = cancelFixture.Coordinator.RequestSpeakAsync("cancel me", ct: cts.Token);
            await cancelFixture.Speech.WaitForStartsAsync(1);
            cts.Cancel();
            Assert(await canceled.WaitAsync(TimeSpan.FromSeconds(2)) == SpeechPlaybackResult.Canceled,
                "R0-S: caller cancellation returns the terminal Canceled result");
        }
        finally { await DisposeFixtureAsync(cancelFixture); }
    }

    private static async Task VerifySpeechSerializationAndCancellationIsolationAsync()
    {
        var transcriber = new R0Transcriber();
        var speech = new R0ControlledSpeech();
        var fixture = CreateR0Coordinator(transcriber, speech);
        try
        {
            using var firstCancellation = new CancellationTokenSource();
            Task<SpeechPlaybackResult> first = fixture.Coordinator.RequestSpeakAsync("first", ct: firstCancellation.Token);
            Task<SpeechPlaybackResult> second = fixture.Coordinator.RequestSpeakAsync("second");
            await speech.WaitForStartsAsync(1);
            Assert(speech.StartedTexts.SequenceEqual(["first"]) && speech.MaxPhysicalOverlap == 1,
                "R0-T: the second simultaneous request waits behind the first");

            firstCancellation.Cancel();
            Assert(await first.WaitAsync(TimeSpan.FromSeconds(2)) == SpeechPlaybackResult.Canceled,
                "R0-U: canceling the older caller cancels that request");
            await speech.WaitForStartsAsync(2);
            Assert(!second.IsCompleted && speech.StartedTexts.SequenceEqual(["first", "second"]),
                "R0-V: later unrelated speech starts in deterministic order and is not canceled");

            speech.RaiseSpuriousEnded();
            Assert(fixture.Coordinator.CurrentState == VoiceSessionState.Synthesizing,
                "R0-W: stale playback-state completion cannot mutate the active generation");
            speech.CompleteCurrent(SpeechPlaybackResult.Completed);
            Assert(await second.WaitAsync(TimeSpan.FromSeconds(2)) == SpeechPlaybackResult.Completed &&
                   speech.MaxPhysicalOverlap == 1,
                "R0-X: serialized speech requests never physically overlap");
        }
        finally { await DisposeFixtureAsync(fixture); }
    }

    private static async Task VerifyExplicitStopAndDeviceLossAsync()
    {
        var transcriber = new R0Transcriber();
        var speech = new R0ControlledSpeech();
        var fixture = CreateR0Coordinator(transcriber, speech);
        try
        {
            await fixture.Coordinator.StartVoiceNavigationAsync();
            Task<SpeechPlaybackResult> request = fixture.Coordinator.RequestSpeakAsync("do not resume");
            await speech.WaitForStartsAsync(1);
            await fixture.Coordinator.StopVoiceNavigationAsync();
            speech.CompleteCurrent(SpeechPlaybackResult.Completed);
            await request.WaitAsync(TimeSpan.FromSeconds(2));
            Assert(fixture.Coordinator.CurrentState == VoiceSessionState.Idle && transcriber.StartCallCount == 1,
                "R0-Y: explicit navigation stop clears desired mode and prevents stale resume");

            await fixture.Coordinator.StartVoiceNavigationAsync();
            transcriber.EndUnexpectedly();
            await WaitUntilAsync(() => fixture.Coordinator.CurrentState == VoiceSessionState.Idle);
            Assert(!transcriber.IsRecording,
                "R0-Z: unexpected physical recognition completion reconciles advertised coordinator state");

            await fixture.Coordinator.StartVoiceNavigationAsync();
            Action<TranscriptionChunk> stale = transcriber.CurrentCallback!;
            fixture.Audio.SetHealth(AudioCaptureHealth.NoMicrophoneDetected);
            await WaitUntilAsync(() => fixture.Coordinator.CurrentState == VoiceSessionState.Disabled);
            stale(new TranscriptionChunk("open dashboard", "open dashboard", true, 99));
            Assert(!transcriber.IsRecording,
                "R0-AA: device loss terminates capture and invalidates its callback generation");
        }
        finally { await DisposeFixtureAsync(fixture); }
    }

    private static async Task VerifyCrossedRecognitionModesAsync()
    {
        var fixture = CreateR0Coordinator(new R0Transcriber(), new R0ControlledSpeech());
        try
        {
            await fixture.Coordinator.RequestStartDictationAsync(_ => { });
            int stops = fixture.Transcriber.StopCallCount;
            await fixture.Coordinator.StopVoiceNavigationAsync();
            Assert(fixture.Transcriber.IsRecording && fixture.Transcriber.StopCallCount == stops &&
                   fixture.Coordinator.CurrentState == VoiceSessionState.Dictating,
                "R1-F4: stopping command mode cannot stop active dictation");

            await fixture.Coordinator.StartVoiceNavigationAsync();
            stops = fixture.Transcriber.StopCallCount;
            await fixture.Coordinator.RequestStopDictationAsync();
            Assert(fixture.Transcriber.IsRecording && fixture.Transcriber.StopCallCount == stops &&
                   fixture.Coordinator.CurrentState == VoiceSessionState.ListeningForCommand,
                "R1-F4b: stopping dictation cannot stop active command capture");

            Task<SpeechPlaybackResult> commandSpeech = fixture.Coordinator.RequestSpeakAsync("command suspension");
            await fixture.Speech.WaitForStartsAsync(1);
            await fixture.Coordinator.StopVoiceNavigationAsync();
            fixture.Speech.CompleteCurrent(SpeechPlaybackResult.Completed);
            await commandSpeech.WaitAsync(TimeSpan.FromSeconds(2));
            Assert(!fixture.Transcriber.IsRecording && fixture.Transcriber.StartCallCount == 2,
                "R1-F4c: stopping suspended command intent prevents its resume");

            await fixture.Coordinator.RequestStartDictationAsync(_ => { });
            Task<SpeechPlaybackResult> dictationSpeech = fixture.Coordinator.RequestSpeakAsync("dictation suspension");
            await fixture.Speech.WaitForStartsAsync(2);
            await fixture.Coordinator.RequestStopDictationAsync();
            fixture.Speech.CompleteCurrent(SpeechPlaybackResult.Completed);
            await dictationSpeech.WaitAsync(TimeSpan.FromSeconds(2));
            Assert(!fixture.Transcriber.IsRecording && fixture.Transcriber.StartCallCount == 3,
                "R1-F4d: stopping suspended dictation intent prevents its resume");
        }
        finally { await DisposeFixtureAsync(fixture); }
    }

    private static async Task VerifySuspendedDeviceLossAsync()
    {
        var fixture = CreateR0Coordinator(new R0Transcriber(), new R0ControlledSpeech());
        try
        {
            await fixture.Coordinator.StartVoiceNavigationAsync();
            Task<SpeechPlaybackResult> speech = fixture.Coordinator.RequestSpeakAsync("suspend capture");
            await fixture.Speech.WaitForStartsAsync(1);
            fixture.Audio.SetHealth(AudioCaptureHealth.NoMicrophoneDetected);
            await WaitUntilAsync(() => fixture.Coordinator.CurrentState == VoiceSessionState.Disabled);
            fixture.Audio.SetHealth(AudioCaptureHealth.Healthy);
            fixture.Speech.CompleteCurrent(SpeechPlaybackResult.Completed);
            await speech.WaitAsync(TimeSpan.FromSeconds(2));
            Assert(fixture.Transcriber.StartCallCount == 1 && !fixture.Transcriber.IsRecording &&
                   !fixture.Coordinator.IsVoiceNavigationEnabled,
                "R1-F5: device loss during speech suspension invalidates later resume");
            Assert(await fixture.Coordinator.StartVoiceNavigationAsync() == VoiceRecognitionStartResult.Started &&
                   fixture.Transcriber.StartCallCount == 2,
                "R1-F5b: restored capture starts only after an explicit new user action");
        }
        finally { await DisposeFixtureAsync(fixture); }

        var stillLost = CreateR0Coordinator(new R0Transcriber(), new R0ControlledSpeech());
        try
        {
            await stillLost.Coordinator.RequestStartDictationAsync(_ => { });
            Task<SpeechPlaybackResult> speech = stillLost.Coordinator.RequestSpeakAsync("device remains lost");
            await stillLost.Speech.WaitForStartsAsync(1);
            stillLost.Audio.SetHealth(AudioCaptureHealth.NoMicrophoneDetected);
            await WaitUntilAsync(() => stillLost.Coordinator.CurrentState == VoiceSessionState.Disabled);
            stillLost.Speech.CompleteCurrent(SpeechPlaybackResult.Completed);
            await speech.WaitAsync(TimeSpan.FromSeconds(2));
            Assert(stillLost.Coordinator.CurrentState == VoiceSessionState.Disabled &&
                   stillLost.Transcriber.StartCallCount == 1,
                "R1-F5c: absent capture remains Disabled after suspended playback ends");
        }
        finally { await DisposeFixtureAsync(stillLost); }
    }

    private static async Task VerifyBoundedFifoAsync()
    {
        var fixture = CreateR0Coordinator(new R0Transcriber(), new R0ControlledSpeech());
        try
        {
            using var thirdCancellation = new CancellationTokenSource();
            Task<SpeechPlaybackResult>[] requests = Enumerable.Range(1, 8)
                .Select(i => fixture.Coordinator.RequestSpeakAsync($"fifo-{i}",
                    ct: i == 3 ? thirdCancellation.Token : default)).ToArray();
            await fixture.Speech.WaitForStartsAsync(1);
            Task<SpeechPlaybackResult> ninth = fixture.Coordinator.RequestSpeakAsync("fifo-9");
            Assert(await ninth.WaitAsync(TimeSpan.FromSeconds(2)) == SpeechPlaybackResult.Failed &&
                   !requests[0].IsCompleted,
                "R1-M1: ninth request is rejected immediately while eight are admitted");
            thirdCancellation.Cancel();
            Assert(await requests[2].WaitAsync(TimeSpan.FromSeconds(2)) == SpeechPlaybackResult.Canceled,
                "R1-M1b: queued cancellation settles and removes only its own request");
            int started = 1;
            foreach (int i in new[] { 1, 2, 4, 5, 6, 7, 8 })
            {
                await fixture.Speech.WaitForStartsAsync(started);
                fixture.Speech.CompleteCurrent(SpeechPlaybackResult.Completed);
                started++;
            }
            await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(2));
            Assert(fixture.Speech.StartedTexts.SequenceEqual(
                       new[] { 1, 2, 4, 5, 6, 7, 8 }.Select(i => $"fifo-{i}")) &&
                   fixture.Speech.MaxPhysicalOverlap == 1,
                "R1-M1c: admitted non-canceled requests execute FIFO without physical overlap");
        }
        finally { await DisposeFixtureAsync(fixture); }

        var shutdown = CreateR0Coordinator(new R0Transcriber(), new R0ControlledSpeech());
        try
        {
            Task<SpeechPlaybackResult>[] queued = Enumerable.Range(1, 8)
                .Select(i => shutdown.Coordinator.RequestSpeakAsync($"shutdown-{i}")).ToArray();
            await shutdown.Speech.WaitForStartsAsync(1);
            await shutdown.Coordinator.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
            SpeechPlaybackResult[] results = await Task.WhenAll(queued).WaitAsync(TimeSpan.FromSeconds(2));
            Assert(results.All(result => result == SpeechPlaybackResult.Canceled) &&
                   shutdown.Speech.StartedTexts.Count == 1,
                "R1-M1d: shutdown settles active and all queued requests without starting later playback");
        }
        finally { await DisposeFixtureAsync(shutdown); }
    }

    private static async Task VerifyScholarCrossFeatureIsolationAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), "AxoraR1Scholar_" + Guid.NewGuid().ToString("N"));
        var settings = new AppSettingsService(customDirectory: path);
        var voice = new R0FeatureVoiceCoordinator();
        var lowLevel = new W4MockSynthesizer();
        var scholar = new ScholarKitViewModel(new DummyOcrService(), new DummyPdfExtractionService(),
            new DummyDocumentProcessorService(), new DummyVoiceTranscriberService(),
            new DummyDocumentChatService(), lowLevel, new DummyScannerService(), settings,
            voiceCoordinator: voice);
        try
        {
            scholar.OcrResultText = "Scholar request A";
            Task scholarA = scholar.ToggleReadAloudAsync();
            Task<SpeechPlaybackResult> featureB = voice.RequestSpeakAsync("feature B");
            scholar.ClearResultsCommand.Execute(null);
            await scholarA.WaitAsync(TimeSpan.FromSeconds(2));
            Assert(!featureB.IsCompleted && voice.StopSpeechCount == 0,
                "R1-F7: Scholar Clear cancels its own request without stopping a newer feature B");
            voice.CompleteCurrent(SpeechPlaybackResult.Completed);
            Assert(await featureB.WaitAsync(TimeSpan.FromSeconds(2)) == SpeechPlaybackResult.Completed,
                "R1-F7b: feature B survives Scholar cleanup and reaches its own terminal result");

            scholar.OcrResultText = "Scholar owned request";
            Task owned = scholar.ToggleReadAloudAsync();
            await scholar.ToggleReadAloudAsync();
            await owned.WaitAsync(TimeSpan.FromSeconds(2));
            Assert(!scholar.IsSpeaking && voice.StopSpeechCount == 0,
                "R1-F7c: Scholar Stop cancels only its currently owned request");

            scholar.OcrResultText = "Scholar before disposal";
            Task scholarDisposal = scholar.ToggleReadAloudAsync();
            Task<SpeechPlaybackResult> newerFeature = voice.RequestSpeakAsync("newer feature");
            scholar.Dispose();
            await scholarDisposal.WaitAsync(TimeSpan.FromSeconds(2));
            Assert(!newerFeature.IsCompleted && voice.StopSpeechCount == 0,
                "R1-F7d: Scholar disposal cannot stop a newer feature playback");
            voice.CompleteCurrent(SpeechPlaybackResult.Completed);
            await newerFeature.WaitAsync(TimeSpan.FromSeconds(2));

            var fallback = new ScholarKitViewModel(new DummyOcrService(), new DummyPdfExtractionService(),
                new DummyDocumentProcessorService(), new DummyVoiceTranscriberService(),
                new DummyDocumentChatService(), lowLevel, new DummyScannerService(), settings);
            fallback.OcrResultText = "No coordinator";
            await fallback.ToggleReadAloudAsync();
            await fallback.ToggleVoiceDictationAsync();
            Assert(lowLevel.SpeakCallCount == 0 && !fallback.IsSpeaking && !fallback.IsDictating,
                "R1-M3: compatibility construction cannot bypass coordinator ownership");
            fallback.Dispose();
        }
        finally
        {
            scholar.Dispose();
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }

    private static async Task VerifyShellDesiredToggleAsync()
    {
        var voice = new R0FeatureVoiceCoordinator();
        var shell = new ShellViewModel(voice);
        await shell.ToggleVoiceNavigationAsync();
        Assert(voice.IsVoiceNavigationEnabled && voice.NavigationStarts == 1 && !shell.IsVoiceListening,
            "R1-M2: desired command mode can remain on when physical listening is suspended");
        await shell.ToggleVoiceNavigationAsync();
        Assert(!voice.IsVoiceNavigationEnabled && voice.NavigationStarts == 1,
            "R1-M2b: toggle during suspension clears desired mode instead of starting again");
        shell.Dispose();

        var failed = new R0FeatureVoiceCoordinator { StartResult = VoiceRecognitionStartResult.Unavailable };
        var failedShell = new ShellViewModel(failed);
        await failedShell.ToggleVoiceNavigationAsync();
        Assert(!failed.IsVoiceNavigationEnabled && !failedShell.IsVoiceListening,
            "R1-M2c: failed navigation startup leaves desired and actual mode off");
        failedShell.Dispose();

        var fixture = CreateR0Coordinator(new R0Transcriber(), new R0ControlledSpeech());
        var realShell = new ShellViewModel(fixture.Coordinator);
        try
        {
            await realShell.ToggleVoiceNavigationAsync();
            Task<SpeechPlaybackResult> playback = fixture.Coordinator.RequestSpeakAsync("shell suspension");
            await fixture.Speech.WaitForStartsAsync(1);
            await realShell.ToggleVoiceNavigationAsync();
            fixture.Speech.CompleteCurrent(SpeechPlaybackResult.Completed);
            await playback.WaitAsync(TimeSpan.FromSeconds(2));
            Assert(!fixture.Coordinator.IsVoiceNavigationEnabled &&
                   fixture.Transcriber.StartCallCount == 1 && !fixture.Transcriber.IsRecording,
                "R1-M2d: Shell OFF during real coordinator suspension prevents command resume");
        }
        finally { realShell.Dispose(); await DisposeFixtureAsync(fixture); }
    }

    private static async Task VerifySettingsTestSpeechUsesCoordinatorAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), "AxoraR0Settings_" + Guid.NewGuid().ToString("N"));
        try
        {
            var lowLevel = new MockSpeechSynthesisService();
            var voice = new R0FeatureVoiceCoordinator { AutoComplete = true };
            var vm = new SettingsViewModel(new AppSettingsService(customDirectory: path), null,
                lowLevel, null, voice);
            await vm.TestSpeechAsync();
            Assert(voice.SpeakCallCount == 1 && !lowLevel.IsSpeaking,
                "R0-AB: Settings test speech uses the coordinator rather than direct playback");
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    private static async Task VerifyR0ShutdownOwnershipAsync()
    {
        var transcriber = new R0Transcriber();
        var speech = new R0ControlledSpeech();
        var fixture = CreateR0Coordinator(transcriber, speech);
        Task<SpeechPlaybackResult> pending = fixture.Coordinator.RequestSpeakAsync("shutdown");
        await speech.WaitForStartsAsync(1);
        Task stop = fixture.Coordinator.StopAsync();
        await Task.WhenAll(pending, stop).WaitAsync(TimeSpan.FromSeconds(2));
        fixture.Coordinator.Dispose();
        Assert(pending.Result == SpeechPlaybackResult.Canceled && speech.DisposeCount == 0 &&
               transcriber.DisposeCount == 0,
            "R0-AC: shutdown closes admission and drains R0 work without disposing injected services");
        speech.Dispose();
        transcriber.Dispose();
        fixture.Audio.Dispose();
        if (Directory.Exists(fixture.SettingsPath)) Directory.Delete(fixture.SettingsPath, true);
    }

    private static R0Fixture CreateR0Coordinator(
        R0Transcriber transcriber,
        R0ControlledSpeech speech,
        IVoiceCommandRouter? router = null)
    {
        string settingsPath = Path.Combine(Path.GetTempPath(), "AxoraR0_" + Guid.NewGuid().ToString("N"));
        var audio = new R0AudioMonitor();
        var coordinator = new VoiceCoordinator(transcriber, speech, router ?? new VoiceCommandRouter(),
            audio, new VoiceTextFormatter(), new AppSettingsService(customDirectory: settingsPath));
        coordinator.AcousticDebounceInterval = TimeSpan.Zero;
        return new R0Fixture(coordinator, transcriber, speech, audio, settingsPath);
    }

    private static async Task DisposeFixtureAsync(R0Fixture fixture)
    {
        await fixture.Coordinator.StopAsync();
        fixture.Coordinator.Dispose();
        fixture.Speech.Dispose();
        fixture.Transcriber.Dispose();
        fixture.Audio.Dispose();
        if (Directory.Exists(fixture.SettingsPath)) Directory.Delete(fixture.SettingsPath, true);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("R0 deterministic condition did not become true.");
            await Task.Delay(10);
        }
    }

    private sealed record R0Fixture(
        VoiceCoordinator Coordinator,
        R0Transcriber Transcriber,
        R0ControlledSpeech Speech,
        R0AudioMonitor Audio,
        string SettingsPath);

    private sealed class R0Transcriber : IVoiceTranscriberService, IRecognitionActivationSource
    {
        private readonly List<string>? _log;
        private VoiceTranscriberService.RecognitionSession? _deferredSession;
        private VoiceTranscriberService.RecognitionSession? _activationSession;
        public Action<AudioCaptureHealth>? CompletionNotification { get; private set; }
        public Task<VoiceRecognitionStartResult> StartRecognitionAsync(
            Action<TranscriptionChunk> callback, Action<AudioCaptureHealth> completed, CancellationToken ct)
        {
            CompletionNotification = completed;
            return StartDictationAsync(callback, ct);
        }
        public bool TryActivateRecognition(Action activation) => _activationSession?.TryActivate(activation) == true;
        public R0Transcriber(List<string>? log = null) => _log = log;
        public bool IsRecording { get; private set; }
        public AudioCaptureHealth DeviceHealth { get; private set; } = AudioCaptureHealth.Healthy;
        public VoiceRecognitionStartResult StartResult { get; set; } = VoiceRecognitionStartResult.Started;
        public TaskCompletionSource? DeferredNativeStart { get; set; }
        public bool DeferredSessionStarted => _deferredSession != null;
        public void CompleteDeferredNativeStart() => _deferredSession?.Complete(
            () => CompletionNotification?.Invoke(DeviceHealth));
        public int StartCallCount { get; private set; }
        public int StopCallCount { get; private set; }
        public int DisposeCount { get; private set; }
        public Action<TranscriptionChunk>? CurrentCallback { get; private set; }
        public List<Action<TranscriptionChunk>> CallbackHistory { get; } = [];
        public event EventHandler<VoiceTranscriberStateChangedEventArgs>? StateChanged;
        public Task<bool> CheckPrerequisitesAsync(CancellationToken ct = default) => Task.FromResult(true);
        public async Task<VoiceRecognitionStartResult> StartDictationAsync(Action<TranscriptionChunk> callback, CancellationToken ct = default)
        {
            StartCallCount++;
            _log?.Add("recognition-start");
            if (ct.IsCancellationRequested) return VoiceRecognitionStartResult.Canceled;
            if (DeferredNativeStart != null)
            {
                var session = new VoiceTranscriberService.RecognitionSession(callback);
                _deferredSession = session;
                _activationSession = session;
                bool live = await session.StartAsync(() => DeferredNativeStart.Task);
                if (!live) return VoiceRecognitionStartResult.Unavailable;
            }
            if (StartResult != VoiceRecognitionStartResult.Started)
            {
                DeviceHealth = StartResult == VoiceRecognitionStartResult.PermissionDenied
                    ? AudioCaptureHealth.PermissionDenied : AudioCaptureHealth.RecognitionUnavailable;
                return StartResult;
            }
            CurrentCallback = callback;
            DeviceHealth = AudioCaptureHealth.Healthy;
            if (DeferredNativeStart == null)
            {
                _activationSession = new VoiceTranscriberService.RecognitionSession(callback);
                await _activationSession.StartAsync(() => Task.CompletedTask);
            }
            CallbackHistory.Add(callback);
            IsRecording = true;
            StateChanged?.Invoke(this, new VoiceTranscriberStateChangedEventArgs(true, DeviceHealth));
            return VoiceRecognitionStartResult.Started;
        }
        public Task StopDictationAsync()
        {
            _activationSession?.Complete();
            StopCallCount++;
            _log?.Add("recognition-stop");
            IsRecording = false;
            CurrentCallback = null;
            StateChanged?.Invoke(this, new VoiceTranscriberStateChangedEventArgs(false, DeviceHealth));
            return Task.CompletedTask;
        }
        public void EmitCurrent(TranscriptionChunk chunk) => CurrentCallback?.Invoke(chunk);
        public void EndUnexpectedly()
        {
            _activationSession?.Complete(() => CompletionNotification?.Invoke(DeviceHealth));
            IsRecording = false;
            CurrentCallback = null;
            StateChanged?.Invoke(this, new VoiceTranscriberStateChangedEventArgs(false, DeviceHealth));
        }
        public void CompletePhysicalSessionWithoutNotification()
        {
            _activationSession?.Complete();
            IsRecording = false;
            CurrentCallback = null;
        }
        public void Dispose() { DisposeCount++; IsRecording = false; CurrentCallback = null; }
    }

    private sealed class R0ControlledSpeech : ISpeechSynthesisService
    {
        private readonly object _gate = new();
        private readonly List<string>? _log;
        private TaskCompletionSource<SpeechPlaybackResult>? _active;
        private int _physicalActive;
        public R0ControlledSpeech(List<string>? log = null) => _log = log;
        public bool IsSpeaking { get; private set; }
        public VoiceInfo? CurrentVoice => new("r0", "R0 Voice", "en-US", "Neutral", "test", true);
        public IReadOnlyList<VoiceInfo> AvailableVoices => [CurrentVoice!];
        public double SpeechRate { get; set; } = 1;
        public double SpeechPitch { get; set; } = 1;
        public double SpeechVolume { get; set; } = 1;
        public SpeechPlaybackResult? ImmediateResult { get; set; }
        public List<string> StartedTexts { get; } = [];
        public int MaxPhysicalOverlap { get; private set; }
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }
        public event EventHandler<SpeechPlaybackStateChangedEventArgs>? PlaybackStateChanged;
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public async Task<SpeechPlaybackResult> SpeakTextAsync(string text, double pitch = 1, double rate = 1, CancellationToken ct = default)
        {
            if (ImmediateResult is SpeechPlaybackResult immediate)
            {
                ImmediateResult = null;
                return immediate;
            }
            var completion = new TaskCompletionSource<SpeechPlaybackResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                _active = completion;
                StartedTexts.Add(text);
                _log?.Add("speech-start:" + text);
                _physicalActive++;
                MaxPhysicalOverlap = Math.Max(MaxPhysicalOverlap, _physicalActive);
                IsSpeaking = true;
            }
            PlaybackStateChanged?.Invoke(this, new SpeechPlaybackStateChangedEventArgs(true, CurrentVoice?.Id));
            using var registration = ct.Register(() => completion.TrySetResult(SpeechPlaybackResult.Canceled));
            SpeechPlaybackResult result = await completion.Task;
            lock (_gate)
            {
                if (ReferenceEquals(_active, completion)) _active = null;
                _physicalActive--;
                IsSpeaking = false;
            }
            PlaybackStateChanged?.Invoke(this, new SpeechPlaybackStateChangedEventArgs(false, CurrentVoice?.Id));
            return result;
        }
        public void CompleteCurrent(SpeechPlaybackResult result)
        {
            TaskCompletionSource<SpeechPlaybackResult>? active;
            lock (_gate) active = _active;
            active?.TrySetResult(result);
        }
        public void RaiseSpuriousEnded() => PlaybackStateChanged?.Invoke(this,
            new SpeechPlaybackStateChangedEventArgs(false, CurrentVoice?.Id));
        public async Task WaitForStartsAsync(int count) => await WaitUntilAsync(() =>
        {
            lock (_gate) return StartedTexts.Count >= count;
        });
        public void Stop() { StopCount++; CompleteCurrent(SpeechPlaybackResult.Canceled); }
        public void Pause() { }
        public void Resume() { }
        public void SetVoice(string voiceId) { }
        public void Dispose() { DisposeCount++; Stop(); }
    }

    private sealed class R0AudioMonitor : IAudioDeviceMonitor
    {
        public bool HasMicrophone => CurrentHealth == AudioCaptureHealth.Healthy;
        public bool IsPermissionGranted => CurrentHealth != AudioCaptureHealth.PermissionDenied;
        public string? DefaultCaptureDeviceName => HasMicrophone ? "R0 microphone" : null;
        public AudioCaptureHealth CurrentHealth { get; private set; } = AudioCaptureHealth.Healthy;
        public event EventHandler<AudioDeviceStatusChangedEventArgs>? DeviceStatusChanged;
        public Task RefreshStatusAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void SetHealth(AudioCaptureHealth health)
        {
            CurrentHealth = health;
            DeviceStatusChanged?.Invoke(this, new AudioDeviceStatusChangedEventArgs(health));
        }
        public void Dispose() { }
    }

    private sealed class R0FeatureVoiceCoordinator : IVoiceCoordinator
    {
        private TaskCompletionSource<SpeechPlaybackResult>? _pending;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool AutoComplete { get; set; }
        public VoiceRecognitionStartResult StartResult { get; set; } = VoiceRecognitionStartResult.Started;
        public int NavigationStarts { get; private set; }
        public VoiceSessionState CurrentState => VoiceSessionState.Idle;
        public bool IsVoiceNavigationEnabled { get; set; }
        public bool IsVoiceNavigationDesired => IsVoiceNavigationEnabled;
        public AudioCaptureHealth CaptureHealth => AudioCaptureHealth.Healthy;
        public TimeSpan AcousticDebounceInterval { get; set; }
        public int SpeakCallCount { get; private set; }
        public int StopSpeechCount { get; private set; }
        public int DisposeCount { get; private set; }
        public SpeechPlaybackResult LastResult { get; private set; }
        public event EventHandler<VoiceSessionStateChangedEventArgs>? StateChanged;
        public Task<VoiceRecognitionStartResult> RequestStartDictationAsync(Action<string> callback, CancellationToken ct = default) =>
            Task.FromResult(VoiceRecognitionStartResult.Started);
        public Task RequestStopDictationAsync() => Task.CompletedTask;
        public async Task<SpeechPlaybackResult> RequestSpeakAsync(string text, double? pitch = null, double? rate = null, CancellationToken ct = default)
        {
            SpeakCallCount++;
            Started.TrySetResult();
            if (AutoComplete) return LastResult = SpeechPlaybackResult.Completed;
            var pending = new TaskCompletionSource<SpeechPlaybackResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending = pending;
            using var registration = ct.Register(() => pending.TrySetResult(SpeechPlaybackResult.Canceled));
            return LastResult = await pending.Task;
        }
        public void RequestStopSpeech() { StopSpeechCount++; _pending?.TrySetResult(SpeechPlaybackResult.Canceled); }
        public Task<VoiceRecognitionStartResult> StartVoiceNavigationAsync(CancellationToken ct = default)
        {
            NavigationStarts++;
            IsVoiceNavigationEnabled = StartResult == VoiceRecognitionStartResult.Started;
            return Task.FromResult(StartResult);
        }
        public Task StopVoiceNavigationAsync()
        {
            IsVoiceNavigationEnabled = false;
            return Task.CompletedTask;
        }
        public void CompleteCurrent(SpeechPlaybackResult result) => _pending?.TrySetResult(result);
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() => DisposeCount++;
    }
}
