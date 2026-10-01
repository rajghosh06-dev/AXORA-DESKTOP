# AXORA STUDIO-R0 Voice + Flashcards Evidence

## 1. Scope and baseline

- Remediation scope: `V0-FLS-001`, `V0-W4-001`, and `V0-W4-002` only.
- Branch: `main`.
- Verified `HEAD`: `a518f04ba7188a93ec7d7334879c194699db59b9`.
- Verified `origin/main`: `a518f04ba7188a93ec7d7334879c194699db59b9`.
- The tracked worktree and index were clean before implementation.
- `Axora-Desktop-WinUI.zip` was already untracked and was not opened, hashed, extracted, modified, deleted, or staged.
- No Studio host was created. No branch, stage, commit, push, merge, rebase, reset, restore, clean, stash, or amend operation was performed.

## 2. Pre-change flows and findings

### FLS-001

`FlashcardsViewModel` directly consumed the process-wide `ISpeechSynthesisService`. Its await represented synthesis/play initiation rather than terminal playback, and its lifecycle could call the shared service's global stop operation.

### W4-001

The shell voice-navigation control toggled router state, but the router did not own a physical recognizer. Production did not provide one coherent path from a current, final recognizer transcript through command routing and UI dispatch.

### W4-002

Speech playback had no typed terminal result and did not retain a complete per-operation lifetime until media end/failure/cancellation. Recognition suspension/resume, resource ownership, stale callbacks, overlapping requests, and caller-scoped cancellation were therefore not represented truthfully.

## 3. Final contracts

- `VoiceRecognitionStartResult`: `Started`, `Unavailable`, `PermissionDenied`, `Canceled`, `Failed`.
- `SpeechPlaybackResult`: `Completed`, `Canceled`, `Unavailable`, `Failed`.
- `IVoiceTranscriberService` exposes one typed recognition-start operation with one admitted transcript callback.
- `IVoiceCommandRouter` owns text processing/dispatch only; misleading listening-lifecycle members were removed.
- `ISpeechSynthesisService` returns the terminal `SpeechPlaybackResult`.
- `ISpeechPlaybackBackend` is the narrow media seam used by `SpeechSynthesisService`.
- `TranscriptionChunk.SequenceNumber` supports exactly-once filtering within a recognition generation.

No synthetic `Busy` status was added because production does not distinguish it reliably.

## 4. Recognition ownership and command path

Ownership is now:

1. `VoiceCoordinator` owns desired mode, actual state, recognition/playback exclusion, generation identity, and suspend/resume policy.
2. `VoiceTranscriberService` owns `SpeechRecognizer` creation, compilation, physical capture, callback wiring, restart, stop, and disposal.
3. `VoiceCommandRouter` normalizes, matches, safety-checks, and dispatches current final text.

The production command path is:

`ShellViewModel` → `IVoiceCoordinator.StartVoiceNavigationAsync` → `VoiceTranscriberService` → final current-generation `TranscriptionChunk` → `VoiceCommandRouter.ProcessCommandAsync` → dispatcher-backed action.

Coordinator state becomes `Dictating` or `ListeningForCommand` only after a `Started` result. Optional capability failure leaves actual state non-listening and does not terminate the host.

Command dispatch is guarded by recognition generation and sequence identity. Duplicate final callbacks are ignored; callbacks from stopped or superseded generations cannot dispatch. A WinUI dispatcher enqueue rejection now faults the awaited operation rather than leaving it incomplete.

The coordinator also observes the transcriber's physical state. If Windows ends a live recognition session outside a coordinator-requested stop, the matching generation is invalidated, the desired mode is cleared, and advertised state is reconciled to `Idle` or `Disabled`; the coordinator cannot remain falsely `Listening` after capture has ended.

## 5. Playback ownership and terminal lifetime

The high-level flow is:

`Feature ViewModel` → `VoiceCoordinator` → `SpeechSynthesisService` → `MediaPlayerSpeechPlaybackBackend`.

`MediaPlayerSpeechPlaybackBackend` retains the synthesized stream, media source, player relationship, terminal handlers, cancellation registration, completion source, and generation identity until exactly one terminal result wins. Media ended, media failed, caller cancellation, operational stop, and shutdown converge on one terminal completion.

`SpeechSynthesisService` serializes the complete physical playback lifetime, not merely the call to `MediaPlayer.Play()`. It raises active playback only after the backend accepts playback and awaits the terminal result. Injected DI-owned backends are not disposed by the service; a privately created fallback backend is owned by the service.

`VoiceCoordinator` bounds admitted pending speech work to eight requests and serializes physical playback FIFO. Each request retains its own cancellation. Canceling an older feature request does not stop a newer unrelated request. Recognition is stopped before physical playback and resumes only when the matching terminal generation completes and the same desired recognition mode is still current. Explicit mode stop and device loss invalidate the applicable recognition generation.

## 6. Flashcards migration

- `FlashcardsViewModel` now depends on `IVoiceCoordinator`, not `ISpeechSynthesisService`.
- It owns only its current request `CancellationTokenSource`.
- `SpeakCurrentCardAsync` awaits the coordinator's terminal result, and `IsSpeaking` covers the request/playback lifetime.
- Navigation away cancels only that request.
- Idempotent disposal neither stops nor disposes shared voice services.
- Normal deck navigation, answer reveal, scoring, and seeded deck content were not changed.

## 7. Settings test-speech audit

`SettingsViewModel` was confirmed as a feature-level direct-speech bypass. Only its test-speech operation was migrated to `IVoiceCoordinator`. It still uses `ISpeechSynthesisService` for low-level voice catalog and selected-voice configuration, which are not playback bypasses. No settings schema, persistence, or UI layout was changed.

No other unexpected production feature-level playback bypass was found. `ScholarKitViewModel` retains its pre-existing compilation fallback under the explicit Scholar freeze; normal production DI supplies the coordinator.

## 8. Scholar compatibility

`ScholarKitViewModel.PushToFlashcards`, `App.TryGetService<FlashcardsViewModel>`, navigation, handoff, and persistence were not redesigned. Only contract/constructor compilation adaptation was made. Existing Scholar → Flashcards behavior remains covered by the full regression suite.

## 9. Changed paths and finding map

### FLS-001

- `Axora-Desktop-WinUI/Axora.Desktop/ViewModels/FlashcardsViewModel.cs`
- `Axora-Desktop-WinUI/Axora.Desktop/Views/FlashcardsPage.xaml.cs`

### W4-001

- `Axora-Desktop-WinUI/Axora.Desktop/Helpers/DispatcherHelper.cs`
- `Axora-Desktop-WinUI/Axora.Desktop/Models/VoiceModels.cs`
- `Axora-Desktop-WinUI/Axora.Desktop/Services/Contracts/IVoiceCommandRouter.cs`
- `Axora-Desktop-WinUI/Axora.Desktop/Services/Contracts/IVoiceCoordinator.cs`
- `Axora-Desktop-WinUI/Axora.Desktop/Services/Contracts/IVoiceTranscriberService.cs`
- `Axora-Desktop-WinUI/Axora.Desktop/Services/VoiceCommandRouter.cs`
- `Axora-Desktop-WinUI/Axora.Desktop/Services/VoiceCoordinator.cs`
- `Axora-Desktop-WinUI/Axora.Desktop/Services/VoiceTranscriberService.cs`
- `Axora-Desktop-WinUI/Axora.Desktop/ViewModels/ShellViewModel.cs`

### W4-002

- `Axora-Desktop-WinUI/Axora.Desktop/App.xaml.cs`
- `Axora-Desktop-WinUI/Axora.Desktop/Services/Contracts/ISpeechPlaybackBackend.cs`
- `Axora-Desktop-WinUI/Axora.Desktop/Services/Contracts/ISpeechSynthesisService.cs`
- `Axora-Desktop-WinUI/Axora.Desktop/Services/MediaPlayerSpeechPlaybackBackend.cs`
- `Axora-Desktop-WinUI/Axora.Desktop/Services/SpeechSynthesisService.cs`
- `Axora-Desktop-WinUI/Axora.Desktop/Services/VoiceCoordinator.cs`
- `Axora-Desktop-WinUI/Axora.Desktop/ViewModels/SettingsViewModel.cs`

### Compilation-only compatibility

- `Axora-Desktop-WinUI/Axora.Desktop/ViewModels/ScholarKitViewModel.cs`

### Test support

- `Axora-Desktop-WinUI/Axora.Desktop.Tests/LifecycleOwnershipTests.cs`
- `Axora-Desktop-WinUI/Axora.Desktop.Tests/Program.cs`
- `Axora-Desktop-WinUI/Axora.Desktop.Tests/StudioR0VoiceFlashcardsTests.cs`
- `Axora-Desktop-WinUI/Axora.Desktop.Tests/TestExecutionLedger.cs`
- `Axora-Desktop-WinUI/Axora.Desktop.Tests/W4_VoiceSubsystemTests.cs`

### Evidence

- `docs/AXORA_STUDIO_R0_VOICE_FLASHCARDS_EVIDENCE.md`

## 10. Deterministic and focused verification

The test executable was run with `--group=<ID>` for each focused group.

| Group | Result | Evidence |
|---|---:|---|
| `R0-VOICE` | Pass | 30/30 assertions |
| `M4-FLASH` | Pass | 31/31 assertions |
| `W4-T1` | Pass | 20/20 assertions |
| `W4-T2` | Pass | 22/22 assertions |
| `W4-INT` | Pass | 15/15 assertions |
| `P3B-LIFECYCLE` | Pass | 42/42 assertions |
| `W4-T3` | EnvironmentNotAvailable | Speech synthesis activation `0x800455A0`; speech-recognition prerequisite false; no microphone endpoint |
| `W4-T4` | EnvironmentNotAvailable | Speech-recognition prerequisite unavailable; microphone was not opened |
| `W4-RULES` | SkippedByPolicy | Legacy broad rule-matrix audit reported 24 static-evidence gaps; no deterministic failure was converted to pass |

The new R0 group covers direct-dependency removal, request-scoped cancellation/disposal, truthful recognition startup, one callback/session contract, current final transcript routing, duplicate and stale transcript rejection, dispatcher rejection completion, recognition suspension, pending playback, all four terminal results, stale terminal events, conditional recognition resume, explicit-stop behavior, unexpected physical-session completion, FIFO/no-overlap playback, unrelated-request isolation, device loss, Settings test speech, and P3B published-task shutdown/drain ownership.

## 11. Build evidence

- SDK: `9.0.318`.
- Command: `.\scripts\qa\build-all.ps1 -Target WinUI -Configuration Debug`.
- Final aggregate build: exit success, 0 errors; its incremental pass emitted 0 warnings.
- A direct no-restore rebuild of the same final source emitted 362 existing compiler/analyzer warnings: 339 from `Axora.Desktop` and 23 from `Axora.Desktop.Tests`; 0 errors.
- Aggregate result: `ALL EXECUTED TARGETS COMPILED CLEANLY (0 ERRORS)`.

Warnings were recorded, not cleaned or reclassified as part of R0.

## 12. Full bounded regression

After production and test source was frozen, the authoritative test executable was run once without a group filter.

- Expected/executed groups: 44/44.
- Dispositions: 39 Pass, 3 EnvironmentNotAvailable, 2 SkippedByPolicy.
- Fail: 0.
- Blocked: 0.
- Missing: 0.
- Duplicate: 0.
- Unknown: 0.
- Assertions: 1,764 passed, 0 failed.
- Environment observations: 4.
- Static gaps: 25.
- Evidence-kind totals: 484 deterministic, 1,156 integration, 124 environment-runtime.
- Elapsed: 38.0 seconds.

The non-pass dispositions remained explicit: `W1.5-NATIVE` was skipped because its trusted executable fixture was absent; `W3-D`, `W4-T3`, and `W4-T4` were environment-unavailable; `W4-RULES` remained policy-skipped with its static gaps.

## 13. Runtime and physical evidence

Final Debug/x64 build runtime smoke was performed without the repository smoke script because that script force-terminates the app.

| Cycle | Window evidence | Close evidence | Exit |
|---|---|---|---:|
| Fresh launch 1 | PID 10232, real handle 3803606 | `CloseMainWindow()` accepted; graceful wait completed | 0 |
| Fresh launch 2 | PID 25860, real handle 17632546 | `CloseMainWindow()` accepted; graceful wait completed | 0 |

No force-kill was used. Lingering `Axora.Desktop` process count after cycle 2: 0.

Physical microphone recognition and physical read-aloud playback are `EnvironmentNotAvailable` on this host for this run. The platform speech synthesizer reported activation failure `0x800455A0`; the recognition prerequisite was unavailable and no microphone endpoint was present. Therefore no physical Flashcards audio success is claimed. The app's visible launch/close lifecycle and deterministic voice seams are independently verified.

## 14. Optional-capability behavior

Unavailable, denied, canceled, or failed voice startup does not produce a listening state and does not terminate the app. Speech unavailability returns a typed terminal result. Flashcards' non-voice operations remain independent of voice availability. Device loss invalidates active capture and prevents stale command dispatch without broad audio-monitor redesign.

## 15. P3B lifecycle regression

`P3B-LIFECYCLE` passed 42/42 assertions in the authoritative suite and in five consecutive focused race checks. Coordinator speech and stop tasks are published atomically under their ownership gates; speech admission/cancellation closes before potentially blocking native teardown; work can be canceled/drained; and host final disposal remains the only DI-service disposal boundary. No double disposal was introduced.

## 16. STUDIO-H0 readiness gates

- FLS-001 repaired: **satisfied**.
- W4-001 repaired: **satisfied**.
- W4-002 repaired: **satisfied**.
- Flashcards has no direct low-level speech dependency: **satisfied**.
- Real command recognition architecture exists: **satisfied**.
- Playback result is terminal and exactly-once: **satisfied**.
- Optional voice failure leaves Flashcards/app usable: **satisfied**.
- P3B lifecycle remains green: **satisfied**.
- Scholar → Flashcards compatibility preserved: **satisfied**.
- No new legacy `App` singleton dependency introduced: **satisfied**.
- Contracts are suitable for later extraction: **satisfied**.

This is readiness evidence only. Studio-H0 was not begun.

## 17. Remaining non-R0 debt and boundaries

- The legacy `W4-RULES` broad static-evidence matrix still reports 24 gaps and remains a separate evidence-hardening task.
- The existing SDK/compiler/analyzer warning backlog remains outside R0.
- Physical voice availability depends on Windows speech packages, permissions, endpoints, and device state; it remains environment-qualified rather than a deterministic pass.
- Settings persistence/R2E, Scholar redesign, navigation catalog repair, Tools/Mind, P2P, W5, Studio creation, and seeded content review remain out of scope.
- The repository `smoke-test.ps1` still uses forced termination; R0 runtime validation deliberately used graceful close instead. Changing that QA utility was not authorized here.
- Final review found and repaired two additional in-scope edge cases: unexpected physical recognition completion could leave coordinator state falsely listening, and shutdown could release a deferred synthesizer before cancellation became observable. Both now have deterministic coverage; the lifecycle race passed five repeated focused runs.

## 18. Protected-area and git audit

- No XAML markup, `.csproj`, solution, package/dependency, or `global.json` change.
- No change under `archive/legacy-monolith` or `Axora-Desktop-MaterialUI`.
- No W5, Suite P0/P1/P2, V0 remediation, or P3B evidence document change.
- No user-data change.
- `git diff --check`: exit 0, with only Git's LF→CRLF working-copy notices.
- `git diff --cached`: empty.
- All implementation and evidence changes remain unstaged.
- The excluded `Axora-Desktop-WinUI.zip` remains the sole unrelated untracked baseline path and was untouched.

## 19. Evidence verdict

The authorized STUDIO-R0 remediation meets its deterministic, integration, build, bounded-suite, graceful runtime, lifecycle, optional-capability, protected-scope, and git-governance gates. Physical voice behavior remains honestly environment-unavailable on this host and is not represented as a pass.

**STUDIO-R0 PASS — READY FOR INDEPENDENT CODE AUDIT**

## 20. STUDIO-R0-R1 AUDIT REPAIR (2026-09-29)

This section records the subsequent independent-audit corrective repair. It does not replace the historical R0 observations or verdict above. The independent audit blocked R0 acceptance; R1 repaired the cited defects in place without staging or committing.

### Path accounting correction

Section 9 enumerates **24 distinct R0 production, test, and evidence paths** (the VoiceCoordinator path appears in two finding categories but is one path). Git status also contains one unrelated, excluded `Axora-Desktop-WinUI.zip`, for **25 total status paths**. The ZIP is not an authorized R0/R1 path and was not opened, hashed, extracted, deleted, modified, or staged. The earlier “25/25 authorized paths” phrasing conflated these counts.

### Corrective implementation

- **R1-F1 terminal publication:** `MediaPlayerSpeechPlaybackBackend.TerminalGenerationGate` rejects an old media event after a newer generation starts. `TerminalSettlement` claims a terminal signal once, attempts independent cleanup steps, detaches the player/source before disposing media resources, and publishes a result in `finally`. Mandatory cleanup failure yields `Failed`; a source that cannot be detached even by player disposal is retained rather than disposed while the player may still use it, and the backend rejects further playback. Startup failure returns the already-claimed terminal result when cancellation/media completion won first.
- **R1-F2 recognition identity:** Windows `SpeechRecognitionResult` exposes no documented unique result ID. A per-session `ConditionalWeakTable` binds a sequence number to the native result object instance, so duplicate delivery of that instance reuses its ID without retaining every utterance or deduplicating transcript text. Distinct later result instances with identical text receive distinct IDs. This is adapter-object identity, not a claim of a platform-provided durable ID. See [Microsoft's result API](https://learn.microsoft.com/en-us/uwp/api/windows.media.speechrecognition.speechrecognitionresult?view=winrt-26100).
- **R1-F3 startup/completion:** the production `RecognitionSession` begins in Starting, records completion, and changes to Recording only if its native `StartAsync` returns while the same session is still live. Completion during pending startup returns a non-Started result and clears callback ownership.
- **R1-F4 mode stop:** `VoiceCoordinator.StopRecognitionModeAsync` stops physical recognition only when the requested mode is the active physical mode; stopping an unrelated mode preserves the running capture.
- **R1-F5 suspended device loss:** capture loss invalidates desired recognition even while speech has suspended physical capture. Playback completion cannot revive that intent; absent capture remains Disabled, and later recovery requires explicit start.
- **R1-F6 Flashcards CTS:** replacement and cancellation occur under the same request-owner lock that governs completion/disposal. Each request disposes only its own CTS; page unload and VM disposal cancel only that request.
- **R1-F7 Scholar isolation / R1-M3 fallback:** Scholar uses a feature-owned CTS for read-aloud and message speech. Stop, clear, and dispose cancel only Scholar's request. A compatibility-constructed Scholar without a coordinator reports voice unavailable rather than directly starting the transcriber or low-level playback.
- **R1-M1 FIFO:** a linked, capacity-eight admission queue grants the next non-canceled request in insertion order. Request nine returns `Failed` immediately; queued cancellation removes only its node; shutdown cancellation settles active and queued work. No background queue worker was introduced.
- **R1-M2 Shell:** the navigation toggle reads coordinator desired navigation intent, so OFF during speech suspension clears resume intent. Failed startup leaves command intent off.
- **Global-stop audit:** the only production feature-facing `RequestStopSpeech` caller is Shell's explicitly global “stop reading” voice action. Scholar and Flashcards no longer use global stop for feature-owned requests.

### R1 tests, build, bounded suite, and runtime

The `R0-VOICE` group now uses the production recognition-session adapter and terminal-settlement seam for duplicate native-result delivery, pending-start completion, competing terminal signals, injected cleanup failures, and old-generation rejection. It also covers crossed recognition modes, command/dictation suspension stops, suspended device loss/recovery, eight-request FIFO and ninth rejection, queued cancellation, shutdown drain, rapid Flashcards request changes, Scholar-versus-newer-feature cancellation, fallback behavior, and Shell suspended-toggle behavior. These are deterministic seam tests; they are not physical WinRT playback or microphone observations.

Final-source focused bounded run: `M4-FLASH` **31/31**, `R0-VOICE` **72/72**, `W4-T1` **20/20**, `W4-T2` **22/22**, `W4-INT` **15/15**, `P3B-LIFECYCLE` **42/42**. Aggregate **202 assertions passed, 0 failed**, six groups Pass, with no missing, duplicate, unknown, or blocked group.

SDK `9.0.318`; final Debug/x64 application build **0 errors, 339 warnings**; final test-project build **0 errors, 24 warnings**. Warnings include existing MVVM Toolkit WinRT/AOT `MVVMTK0045`, Skia obsolescence `CS0618`, and other pre-existing code warnings. No unrelated warning debt was cleaned.

Final-source authoritative bounded WinUI run: **44/44 registered groups executed; 39 Pass, 3 EnvironmentNotAvailable, 2 SkippedByPolicy, 0 Fail, 0 Blocked, 0 missing, 0 duplicate, 0 unknown; 1,806 assertions passed, 0 failed**. The final ledger reports 484 deterministic, 1,198 integration, and 124 environment-runtime passing assertions, four environment observations, and 25 static gaps from separately policy-skipped groups. The prior 1,764 count was not preserved artificially.

Two fresh launches of the final application binary showed a nonzero real main-window handle. Both used `CloseMainWindow()` (normal window close), exited with code 0 without force-kill, and left zero `Axora.Desktop` processes. `startup.log` records `MainWindow activated`, voice operational stop, AppHost final disposal, and `Program.Main shutdown completed`; no unhandled exception was observed in the two-cycle log window. The legacy `smoke-test.ps1` was not used because it force-terminates the app.

Physical voice remains **EnvironmentNotAvailable**, not Pass: this run observed `SpeechSynthesizer` activation HRESULT `0x800455A0`, unavailable speech-recognition prerequisite/language, and no microphone capture endpoint. No Windows speech configuration was changed.

### Remaining boundaries

The LOW DispatcherHelper synchronous-throw normalization was not expanded in R1. Native media/device cleanup races are covered by the production settlement seam but cannot be physically exercised on this host. The legacy `W4-RULES` 24 static gaps and `W1.5-NATIVE` one gap remain separately policy-skipped, not R1 failures. P3B's published stop-task, bounded drain, and host-disposal-withholding rules remain in place. No Studio-H0 work, protected-area edits, staging, commit, or push occurred.

## 21. STUDIO-R0-R2 FINAL RECOGNITION CLOSURE (2026-10-01)

The final R1 independent re-audit withheld acceptance on two remaining findings: recognition completion racing final start/activation publication (R2-F1, HIGH), and the Shell restart toggle confusing saved preference with runtime desired navigation (R2-F2, MEDIUM). This appended section records their authorized corrective implementation and verification; the historical R0/R1 results above remain unchanged.

### R2-F1: one-way session lifecycle and activation handshake

`VoiceTranscriberService.RecognitionSession` synchronizes Starting -> Active, Starting -> Completed, and Active -> Completed under its session gate. There is no Completed -> Active transition. Native startup returning successfully does not itself publish Active. Final recording notification and settlement of the actual public startup Task occur together under the gate shared with completion. Completion clears transcript and completion callback ownership. If completion wins before this final publication, startup settles `Unavailable` and never publishes a live `Started` result; synchronous/reentrant completion during publication also prevents a later `Started` result. If activation/public startup settlement wins first, a subsequent completion remains a legitimate Active -> Completed event; the settled startup result is an observation at that linearization point, not a guarantee that capture stays live until a caller continuation is scheduled.

The public `StartDictationAsync` returns this published Task directly. Its core startup/cleanup remains serialized by the transcriber start/stop semaphore, so a coordinator stop also waits for any remaining native startup cleanup before host disposal. Native event handling captures the corresponding session, and public completion publication removes only that session under the owner gate.

`VoiceCoordinator` creates a Pending recognition activation record before invoking the transcriber. `OnRecognitionCompleted` synchronously changes that exact generation to Completed and clears its active mode before asynchronous state reconciliation. Final Pending -> Active publication uses the same coordinator generation gate. For the production transcriber, the internal `IRecognitionActivationSource` handshake additionally runs that publication under the physical session gate, in session -> coordinator lock order. This closes the gap where physical completion precedes delivery of its notification. Its completion closure captures the particular coordinator activation record; a delayed completion from an old session cannot complete a replacement. The untagged compatibility `StateChanged` event is not used as a second completion authority for the physical adapter. Compatibility test adapters continue to use the serialized event path. No public transcriber contract, model, or additional production file was changed.

The production coordinator test boundary is after the transcriber returns `Started` and after the physical `IsRecording` read, before final activation. Tests force completion there for both Command and Dictation, with notifications delivered immediately and delayed. Completed generations publish neither ListeningForCommand nor Dictating, leave no active recognition mode, reconcile desired intent, and reject stale transcripts. Tests also verify a newer explicit generation survives the old completion closure and that activation-first/later-completion reconciles normally. The existing duplicate-native-object sequence identity and distinct-object/identical-text behavior remain covered and unchanged.

### R2-F2: saved preference, runtime desired mode, actual state

The additive read-only `IVoiceCoordinator.IsVoiceNavigationDesired` is derived from the volatile runtime desired recognition mode being Command. Shell now uses this property for its toggle. The legacy read/write `IsVoiceNavigationEnabled` and Settings persistence semantics remain compatible; neither is used by Shell as proof of runtime desired capture.

Saved preference may be true at restart while runtime desired mode is None and actual state is Idle. Construction performs no recognition start. The first Shell toggle then chooses Start. Failed startup (Unavailable, Failed, PermissionDenied, or Canceled) clears runtime desired intent and does not advertise listening; a later explicit toggle can retry. Runtime failure does not rewrite the saved settings preference. The existing command-desired -> speech suspension -> Shell OFF -> no resume behavior remains covered. No settings schema, file format, migration, layout, persistence redesign, or app-launch microphone activation was added.

### R2 scope

R2 changed only four production files: `VoiceTranscriberService.cs` (R2-F1), `VoiceCoordinator.cs` (R2-F1/F2), `IVoiceCoordinator.cs` (R2-F2 property), and `ShellViewModel.cs` (R2-F2 toggle). `StudioR0VoiceFlashcardsTests.cs` adds the regression coverage and adapts its controlled transcriber to the production session handshake. `Program.cs` and `LifecycleOwnershipTests.cs` each receive one mock property required to compile the additive coordinator contract. This document is the only R2 evidence edit. No new production/evidence file or new Git status path was introduced.

### Final-source build and focused checks

MSBuild evaluation confirms SDK **9.0.318**. Final Debug/x64 application build: **0 errors, 337 warnings**; test-project build: **0 errors, 24 warnings**. Existing MVVM WinRT/AOT, Skia obsolescence, and other warning debt was not repaired in this phase. Compiler output is retained in ignored `Axora-Desktop-WinUI/studio-r2-app-build.log` and `studio-r2-tests-build.log`.

Final-source focused bounded run: `R0-VOICE` **117/117**, `W4-T1` **20/20**, `W4-T2` **22/22**, `W4-INT` **15/15**, `P3B-LIFECYCLE` **42/42**, and `M4-FLASH` **31/31**. Total **247 passed, 0 failed**; all six groups Pass, zero blocked/missing/duplicate/unknown groups. The final focused logs are retained at `C:\Users\rajghosh\AppData\Local\Temp\axora-p3a-8e3ae78e0089405e8ce907285a628c5e`. The group exercises production session publication and gate operations plus the actual coordinator and Shell; it does not claim physical microphone success.

### Full bounded suite

After compiled source stabilization, the authoritative full bounded suite ran once: **44/44 groups executed, 39 Pass, 3 EnvironmentNotAvailable, 2 SkippedByPolicy; 0 Fail, 0 Blocked, 0 missing, 0 duplicate, 0 unknown; 1,851 assertions passed, 0 failed**. The evidence ledger reports **484 deterministic, 1,243 integration, and 124 environment-runtime passing assertions**, four environment observations, and 25 separately policy-skipped static gaps. This supersedes the R1 total of 1,806 for final R2 source. Runtime was 49.7 seconds with verified child ledgers and successful test-owned settings cleanup. Full logs are retained at `C:\Users\rajghosh\AppData\Local\Temp\axora-p3a-bc994ef48ed74999a778de4b2fbbc4ce`.

### Two final-build runtime cycles and P3B

Two fresh launches of the final executable showed a real main window titled `Axora Desktop`:

| Cycle | PID | Main-window handle | Normal close | Exit | Lingering Axora processes | Force-kill |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 12964 | 4327360 | `CloseMainWindow()` accepted | 0 | 0 | No |
| 2 | 3832 | 6097344 | `CloseMainWindow()` accepted | 0 | 0 | No |

The new 54-line startup-log interval contains two main-window activations, two completed voice operational stops, two completed host final disposals, and two `Program.Main shutdown completed` entries, with zero unhandled/unobserved exception or shutdown-failure entries. Runtime used test-owned APPDATA/LOCALAPPDATA directories under `C:\Users\rajghosh\AppData\Local\Temp\axora-studio-r2-runtime-dab01048debd4710a01dea2df963028b`; existing user settings were not changed. Compiled source/test fingerprints were identical before the full run and after both runtime cycles.

P3B remained **42/42** in both focused and full runs. Coordinator published-stop, closed speech admission, active/queued cancellation, native transcriber stop, callback/task drain, DI ownership, and host-disposal withholding remain in place. R2 recognition completion reconciliation uses the existing owned callback-task registry and stopping gate.

### Physical/environment boundaries and remaining debt

Physical voice remains **EnvironmentNotAvailable**, not Pass. The final host probes report SpeechSynthesizer activation HRESULT `0x800455A0`, unavailable recognition language/prerequisite, and no microphone capture endpoint; physical recognition was not opened. No speech pack, Windows voice configuration, model, dependency, or package was installed or changed. The third environment-unavailable group remains `W3-D`. The LOW DispatcherHelper synchronous enqueue-throw normalization remains deferred. `W4-RULES` (24 static gaps) and `W1.5-NATIVE` (one static gap) remain separately SkippedByPolicy.

### Final scope and Git self-audit

Branch `main` and committed HEAD remain `a518f04ba7188a93ec7d7334879c194699db59b9`. All R0/R1/R2 work remains unstaged; the index is empty. Status retains **24 R0/R1/R2 paths plus the single excluded ZIP**, with no unrelated new path. The ZIP was not opened, hashed, extracted, modified, deleted, or staged. Protected archive, MaterialUI, W5/Suite/V0/P3B evidence, SDK/project/solution/package configuration, XAML markup, and user data were not modified by R2. Final diff review maps R2 production edits solely to its two authorized findings, and `git diff --check` has no patch errors. No branch, stage, commit, push, merge, rebase, reset, restore, clean, stash, or amend was performed. STUDIO-H0 has not begun; final independent re-audit and user acceptance remain the next gates.

**STUDIO-R0-R2 PASS — READY FOR FINAL RE-AUDIT**
