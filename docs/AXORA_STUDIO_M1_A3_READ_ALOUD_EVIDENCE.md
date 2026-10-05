# AXORA Studio M1-A3 — Flashcards Read Aloud evidence

## Authority and baseline

Authorized two-phase implementation: local Windows TTS for the visible Flashcard side; exact 13-path allowlist; no staging, commit, push, M1-B, recognition, microphone, cloud voice, package, project, manifest or toolchain changes.

Starting baseline verified from the live worktree and actual remote: main/HEAD/origin/main/remote main `c5a043fdcf01d8310ef23bc7450574020e9e85a0`, subject `feat(studio): add safe flashcard exports`, parent `430d781b67ee8f3b63d7bdb609c573774acf7db0`, ahead/behind 0/0, tracked worktree clean, index empty. The unrelated untracked ZIP is never opened, hashed, extracted, modified, deleted, staged or committed.

## Product and implementation contract

- Read aloud captures the currently visible question/answer, operation ID and study-context generation before asynchronous work. Stop reading is a separate sibling button outside the card flip control.
- Plain text, valid UTF-16, nonblank, maximum 4,096 Unicode scalars, with no truncation. No empty-card placeholder synthesis.
- One native owner and one latest pending intent. Replacement waits for prior resource release; a third request supersedes the pending second request. Playback admission, progress and UI terminal updates are generation fenced; UI checks execute inside dispatcher callbacks. Terminal UI settlement also fences queued same-request progress.
- Typed Completed/Canceled/Replaced/Unavailable/InvalidText/SynthesisFailed/PlaybackFailed outcomes with safe reason codes and Released/Deferred cleanup disposition. Speech status is independent of notes/export status.
- Retained App-owned lazy StudioReadAloudSession; resolving the DI factory does not create a service/backend/native object. The backend uses installed OS-default Windows SpeechSynthesizer plus nonvisual MediaPlayer/MediaSource.CreateFromStream. System media transport integration is disabled. No temporary audio file.
- Per-request raw synthesis operation, synthesizer, stream, source, player, events and cancellation resources. Cancel requests the raw operation and still observes its settlement/late stream. Player release precedes source/stream/synthesizer disposal. Failed release retains exact resources and closes new admission; the retained driver retries cleanup.
- Native work uses an owned background task/MTA-compatible path; no native call under service state locks or UI synchronous wait. Callbacks only signal managed state and marshal UI progress.
- Flip, navigation, rating, deck changes, newly selected decks, route departure and export start cancel/fence speech without waiting for native settlement. Route return never resumes speech.
- Export snapshot capture/picker/publication are not delayed by speech cancellation. Read is denied while export is busy. Existing A2 engine/picker and production Program remain unchanged.
- App starts permanent speech cancellation and existing export/H0 shutdown independently. Admitted file publication remains retained through verification/cleanup. Five-second audio cancellation observation returns Unavailable/CleanupDeferred when needed; it never kills the process or abandons export. Late cleanup has no Host/UI dependency and cannot resurrect speech or UI.
- No text, deck titles, raw media failure descriptions or raw exception messages in diagnostics. Only operation/side/context, phase, safe outcome/cleanup, type/HRESULT and elapsed timing.

## Exact authored inventory

Five new:

1. Axora-Desktop-WinUI/Axora.Studio/Services/Contracts/IFlashcardReadAloudService.cs
2. Axora-Desktop-WinUI/Axora.Studio/Services/FlashcardReadAloudService.cs
3. Axora-Desktop-WinUI/Axora.Studio/Services/WindowsFlashcardReadAloudBackend.cs
4. Axora-Desktop-WinUI/Axora.Studio.Tests/FlashcardReadAloudTests.cs
5. docs/AXORA_STUDIO_M1_A3_READ_ALOUD_EVIDENCE.md

Eight modified:

6. Axora-Desktop-WinUI/Axora.Studio/ViewModels/FlashcardsViewModel.cs
7. Axora-Desktop-WinUI/Axora.Studio/Views/FlashcardsPage.xaml
8. Axora-Desktop-WinUI/Axora.Studio/StudioBootstrap.cs
9. Axora-Desktop-WinUI/Axora.Studio/App.xaml.cs
10. Axora-Desktop-WinUI/Axora.Studio/MainWindow.xaml.cs
11. Axora-Desktop-WinUI/Axora.Studio/Views/HomePage.xaml
12. Axora-Desktop-WinUI/Axora.Studio.Tests/Program.cs
13. Axora-Desktop-WinUI/Axora.Studio.Tests/run-m1.ps1

## Initial verification disposition (superseded by the later gates below)

IN PROGRESS. Initial Debug builds: Studio zero errors/eight existing MVVMTK0045 warnings; Tests zero errors/zero warnings. Initial Read Aloud group: 40 cases, 135 passed assertions, zero failures/missing/duplicate/unknown/blocked. Self-review then strengthened terminal UI fencing and cleanup-failure admission; these revisions require fresh verification. Phase 2 has not started. No native PASS is claimed.

Build/test logs are generated ignored artifacts under Axora.Studio.Tests/logs, not additional authored source paths.

## Phase 1 completed gate (2026-10-05 UTC)

STUDIO-M1-A3 IMPLEMENTATION GATE PASS. Self-review corrections were rebuilt and the full runner repeated against final source. Studio direct Debug/x64 rebuild: exit 0, zero errors, eight existing MVVMTK0045 warnings; elapsed 28.52 seconds. Tests direct Debug/x64 rebuild using that Studio assembly: exit 0, zero errors/warnings; elapsed 8.42 seconds. SDK 9.0.318; no restore/dependency/project changes.

| Group | Required cases | Passed assertions | Failed/missing/duplicate/unknown/blocked |
| --- | ---: | ---: | --- |
| STUDIO-H0 | 21 | 160 | 0/0/0/0/0 |
| STUDIO-M1-FLASHCARDS | 22 | 172 | 0/0/0/0/0 |
| STUDIO-M1-EXPORT | 73 | 289 | 0/0/0/0/0 |
| STUDIO-M1-READALOUD | 41 | 140 | 0/0/0/0/0 |

Runner exit 0. Each group selected/executed once in its own child, manifest four groups, complete semantic ledgers. Evidence: `Axora-Desktop-WinUI/Axora.Studio.Tests/logs/a3-full-regression.log` and `m1-20261005T133958239-*.stdout.log`. Unknown and malformed dispatch both exit 2 without running tests.

Build warning inventory: ShellViewModel selectedRoute; FlashcardsViewModel _notesText, _sourceLabel, _status; SettingsViewModel selectedTheme, status, canEdit, canSave. All eight are MVVMTK0045 and were not suppressed. Full logs: `a3-studio-build.log`, `a3-tests-build.log` in the same generated log directory.

The 41 Read Aloud cases cover lazy factory/session/native construction; scalar/UTF-16 bounds and visible capture; normal completion; replacement/latest-only/late synthesis/events/UI; explicit Stop and terminal races; every study-context cancellation; route return; unchanged metadata; unavailable/unsupported text; synthesis/stream/source/playback/handler/player failures; shutdown identity/admission/deadline/late cleanup; real A2 admitted publication coexistence; source UI/keyboard/status/privacy fences; and unknown backend failure retaining a truthful deferred owner. Fake native objects drive the production Windows backend cleanup path and service, with explicit barriers/injected deadline and no real audio or five-second sleep.

Static red-team: no speech recognizer/capture/AudioGraph/network/cloud/download/process-launch/temp-audio API, global native singleton, new async-void handler or unowned task in the A3 production additions. The two Task.Run paths are respectively session-retained driver and returned/awaited backend work. Existing notes/export exception-message handling and existing route validation discard are unchanged, outside speech diagnostics. No callback/native operation runs under the service state gate. Source/diff whitespace check passed. All protected candidate paths have empty diffs; exact authored inventory is 5 new/8 modified, index empty.

Self-review strengthened: same-request queued progress is fenced after terminal UI settlement; failed native release immediately denies new admission and starts bounded observation; unknown backend settlement preserves passive active/deferred ownership; unavailable Windows APIs/unsupported text map truthfully. No unresolved BLOCKER/HIGH/MEDIUM identified at the Phase-1 gate. Phase 2 begins only after this gate; native findings remain pending.

## Phase 2 hostile review repair and repeated gate

The first native iteration used isolated process 7016, with successful question/answer reading, Stop/reuse, navigation, route return, picker cancellation, keyboard and visual observations. The subsequent hostile source review found a MEDIUM UI race: native settlement can precede its queued terminal callback. A study action in that window fenced the callback but left the previous Preparing/Reading phase active. Stop could then leave stale busy status despite no native owner.

Repair remains inside the existing thirteen-path envelope: `FlashcardsViewModel.CancelReadAloudContext` now clears stale busy state to Idle / Read aloud stopped when native work has already settled. The new mandatory `settled-context-ui` case holds UI dispatch while production service/backend settlement completes, then exercises Stop, flip, next, previous, rating, deck selection, starter creation and generated-deck selection. It asserts both immediate UI clearing and rejection of old queued callbacks. Against the pre-repair Studio assembly, the new test produced sixteen failed assertions (runner exit 1, complete ledger, no missing/duplicate/unknown/blocked). This establishes that the regression detects the actual defect.

The repair was then rebuilt and the entire semantic runner repeated before launching a fresh final native instance. Final direct Debug/x64 Studio rebuild: exit 0, zero errors, the same eight unsuppressed MVVMTK0045 warnings, 48.99 seconds. Tests rebuild against that Studio output: exit 0, zero warnings/errors, 13.68 seconds. SDK remains 9.0.318.

| Final group | Required cases | Passed assertions | Failed/missing/duplicate/unknown/blocked |
| --- | ---: | ---: | --- |
| STUDIO-H0 | 21 | 160 | 0/0/0/0/0 |
| STUDIO-M1-FLASHCARDS | 22 | 172 | 0/0/0/0/0 |
| STUDIO-M1-EXPORT | 73 | 289 | 0/0/0/0/0 |
| STUDIO-M1-READALOUD | 42 | 156 | 0/0/0/0/0 |

Final runner exit 0; each group executed exactly once. Unknown group and extra malformed argument again returned exit 2 with no tests. No assertion total is frozen in the runner.

Generated ignored logs: `a3-race-pre-repair-build.log`, `a3-race-pre-repair.log`, `a3-final-studio-build.log`, `a3-final-tests-build.log`, `a3-final-full-regression.log`, and `m1-20261005T142809959-*.stdout.log` under `Axora-Desktop-WinUI/Axora.Studio.Tests/logs`.

## Final executable native observations

Windows reported Microsoft Windows NT 10.0.26300.0. The actual rebuilt Debug/x64 executable was launched with test-owned APPDATA/LOCALAPPDATA in:

`C:\Users\rajghosh\AppData\Local\Temp\AXORA-A3-final-10339e613568441099d6219337ce6925`

Owned final process: 311176. Its safe runtime log is `Roaming\Axora\Studio\startup.311176.log`. Native harnesses, JSON observations and screenshots are temporary runtime fixtures, outside authored repository source. No real user documents or settings were used. Only the owned Studio window/dialog was operated; no force kill was used.

Final Studio assembly SHA256: `D3AA27D89BAF83BF92F1BA2F7F9744CCD790728BE06BA05EB3AFACCC23056C09`; the Tests output copy is identical. Apphost executable SHA256: `1D5FB593DF0AA6B9637715C9481DCAB79D57D453F526A39D6142B19F48CFEDCA`. The assembly fingerprint matters because an unchanged apphost alone does not identify managed implementation changes.

| Observation | Actual result |
| --- | --- |
| Unused Home / Flashcards | `readAloudCreated=False`, no NativeCreated phase; TCP 0, UDP 0, Studio child processes 0. Home working set 163,254,272 bytes; unused Flashcards 178,446,336 bytes. Owned files only startup log and writer lease. |
| Question read | Operation `5a622d68a0f14607906613d57e66fac5`: Question captured, synthesis/stream produced, actual MediaPlayer Playing event, Reading question, Completed / Released, Read aloud finished. |
| Answer read | Corrected operation `572a9435e9234154a506ab4afecf2915`: Answer captured after observing the visible-side change, Reading answer, actual Playing, Completed / Released, finished UI. No claim that speaker audibility or pronunciation was verified. |
| Stop and reuse | Operation `61cc25a949894b6c85a8db18443edbf3` reached Playing, then Canceled / Released. Stopped UI observed; later Read completed normally. Stop observation 169.21 ms. Transient Stopping has deterministic state coverage; no separate screenshot of that brief transition is claimed. |
| Next | Card progress changed; Next invocation observed at 36.65 ms; old request Canceled / Released and stopped status. No rating was invoked; review count remained zero in bounded UI observations and metadata tests. |
| Route departure / return | Departure invocation 50.02 ms, Canceled / Released; returning added no Read capture and Stop was disabled. |
| Export | Operation `bfa4b90deda1484d8906c932a7a4eed4` Canceled / Released before the owned native Save Flashcards picker. Picker observed within 1,953.03 ms of invocation, including automation/dialog time. Speech status unavailable while exporting; Read disabled. Normal dialog dismissal produced export-canceled status, no destination output, and Read re-enabled. |
| Keyboard | Enter (`c6a94b2328524fc68a548f728f43ff02`) and Space (`8a929596e7264b69add221c9f3881170`) each produced one capture. Card side/text and card progress unchanged; focus reported ReadAloudButton. Space on Stop canceled each request, with stopped UI. Foreground PID was checked before injecting keys. |
| Normal playback close | Operation `5f3c86e110cb4108b26803a576ad473e` reached actual Playing, then normal WM_CLOSE entered Studio's existing close handler. Canceled / Released, H0 owned work/host stop/disposal completed, Window closed, Program fallback settled. Retained native process handle verified exit code 0. Close observation 794.21 ms; lingering owned process 0. |

Runtime files `launch.json`, `front.json`, `back.json`, `stop.json`, `navigate.json`, `route.json`, `export.json`, `keyboard.json`, `network.json`, `close.json`, `audit.json` retain the observations in the isolated fixture. They are not additional source paths.

### Resource, network and privacy limits

Final safe diagnostic inventory: service created once; sixteen captured requests and sixteen NativeCreated / ResourcesReleased pairs, six Completed / Released and ten Canceled / Released. Timestamp-ordered ownership reconstruction has maximum one logged native owner, zero owners remaining after close. Every admitted native instance in this bounded run reached its release trace. Repeated Read/Stop did not block later requests.

Actual TCP/UDP/child-process snapshots were zero before first Read and after several reads/export, and during a bounded observation initiated after a Playing event. That last multi-command snapshot completed after the utterance ended; it is not continuous packet capture or proof that every socket lifetime was observed. Source independently contains no A3 network/cloud/download API. No universal offline guarantee is claimed.

Owned APPDATA files after the final run were only the writer lease, isolated settings JSON and startup log; no owned audio file appeared. Backend audio remains an in-memory SpeechSynthesisStream / MediaSource path with no audio-file write API. This is not a claim to have exhaustively inspected all OS caches.

The safe log had zero matches for the built-in front/back fields or deck titles exercised in these runs. Speech diagnostics contain only operation/side/context, phase, safe outcome/cleanup, exception type/HRESULT and timings. Raw media failure details and text are never logged. No academic/generated speech content or artifact content hash was emitted to the runtime log.

Direct managed/native heap inspection, event-subscription enumeration and audio-session ETW were not performed. Cleanup order and retained dependency ownership have production-path deterministic fault tests plus bounded real release traces. Working set rose during repeated speech/native dialog use; it does not establish a leak or a universal absence of accumulation.

### Visual and accessibility observations

The first owned native iteration (PID 7016) produced inspected `system-wide.png` (1800×1080), `system-narrow.png` (780×900), and `light-wide.png` (1800×1080), with corresponding `visual.json`, in:

`C:\Users\rajghosh\AppData\Local\Temp\AXORA-A3-native-ba98668e3a83489ea14bcc1f31735db6`

Read/Stop were readable sibling controls, fully reachable through the existing scroll viewport at both sizes. Automation reported their exact names, Button type, Invoke/ScrollItem patterns, visible bounds and expected idle enablement. Read focus was observed in UI Automation; inspected earlier captures showed its focus outline. Dedicated status is XAML-declared Polite and exposes TextPattern. No full screen-reader/accessibility certification is claimed.

The later repair changes only VM settled-context state and tests. UI XAML is byte-identical to the inspected native iteration (SHA256 `B8FF69589F5D850C26A69EF44357BD34AFDB80938B05AE004046B84BCA4982C7`). Final binary keyboard/focus checks passed. A final screenshot repeat could not retain owned foreground during capture and was rejected by the harness, so no final-build screenshot PASS is claimed. System and Light samples above remain observations of the unchanged UI; isolated theme saves affected only test APPDATA.

### Native automation findings and exclusions

Name-based invocation, initially minimized state, asynchronous provider dispatch and a generic Save As title assumption caused interrupted/ambiguous harness attempts. Some early attempts produced replacement captures, or captured Question before the intended flip became observable. These were not accepted as the intended native PASS. The picker actually uses title Save Flashcards and is an owned descendant window; its Cancel wrapper did not expose Invoke, so normal owned dialog WM_CLOSE was used.

The speech controls were subsequently targeted by unique AutomationId, actual side changes were awaited, the log was read with writer-compatible sharing, and unnecessary foreground changes were removed. Actual controlled Enter/Space tests independently verified single activation. The exact cause of every interrupted input attempt was not established; exclusive human input was not guaranteed. Production replacement/cleanup remained safe in the observed supersession traces. Scratch-helper corrections do not constitute additional authored product paths.

Installed Windows speech was available in the exercised environment: no fake native unavailable PASS. Negative API/voice/unsupported-text availability has deterministic coverage. Human-audible output and native close during synthesis were NOT OBSERVED; synthesis was not held through a controllable real-native barrier. Mandatory playback-close was observed. No microphone or recording was introduced to fill these gaps.

### Bounded Debug performance

Fresh final process startup to observed Home: 3,788.59 ms (automation-inclusive). First valid Read service construction: 0.95 ms. First native request recorded SynthesisStarted at 320.82 ms, StreamProduced at 364.27 ms, and actual PlaybackStarted at 1,206.25 ms, all backend-relative. Stop 169.21 ms; normal playback-close 794.21 ms. A later speech observation sampled working set 255,356,928 bytes after repeated native use. These are bounded Debug samples under a live desktop workload, with no Release claim, invented threshold or baseline performance delta. The five-second deadline is a contract limit, not a measured latency result.

## Final internal hostile audit

| Property challenged | Source and deterministic evidence | Bounded native evidence |
| --- | --- | --- |
| A/B/C never overlap | Service ReadAsync/DriveAsync promotes the latest pending request only after Released; replacement/latest-only/late-synthesis cases. | Maximum one logged owner; release precedes subsequent NativeCreated. |
| Old work cannot play/stop/dispose new work | Per-request raw operation/player/source/stream; final MayPlay admission; late-events/playback-fence cases. | Independent release traces and later successful Reads. |
| Old UI cannot overwrite new context or finished state | VM DispatchCurrent checks UI/context/id inside dispatched callback; ApplyResult advances terminal UI generation; late-ui and settled-context-ui regressions. | Next/route/keyboard/export terminal observations. |
| Raw WinRT operation and late stream stay owned | Backend retains raw AsTask without managed cancellation wrapper; awaits terminal synthesis before stream/synth release. | Actual synthesis/stream/playback/release traces; hostile late synthesis is deterministic. |
| Player release cannot free dependencies prematurely | Backend detach/pause-clear/player/source/stream/synth order; failed release retains exact objects and retries. handler-cleanup/player-cleanup tests. | All sixteen real native owners released; no heap-level claim. |
| Audio deadline cannot resurrect speech or weaken A2 | Service deadline fences admission/UI and retains native Task; deadline-late-cleanup/export-admitted-deferred with real A2 publisher barrier. | Normal native close settled; real pathological deferred cleanup was not forced. |
| Session stop identity/admission remain permanent | Cached Stop/combined shutdown tasks; shutdown-unused/shutdown-race/host-independent cases. Host owns no native speech instance. | Clean final close, H0 completion and Program fallback. |
| Export cannot be delayed/abandoned by audio | VM export fences without awaiting; combined shutdown initiates A2 independently; unchanged A2 engine, 73 Export cases. | Owned picker cancellation after speech; no output mutation. |
| No UI deadlock or native calls under state lock | Background MTA Task.Run paths retained/awaited; service gate contains state only; callbacks/native cancellation dispatched outside gate; UI state through dispatcher. | Study/route/export/close remained responsive; bounded invocation timings above. |

Final source was re-read after the repair. A3 has no microphone/recognition/cloud provider, SSML, voice chooser, persistence, recording, forced process termination, new dependency or deferred product feature. The only newly found MEDIUM issue was repaired and regression-proved; final unresolved BLOCKER/HIGH/MEDIUM counts are 0/0/0. Environment/observation gaps above remain explicit.

## Final scope and Git state

Final authored inventory remains exactly thirteen paths: five new and eight modified, as listed above. No path fourteen. Index is empty; all work remains unstaged. HEAD/origin/main/actual read-only remote main all remain `c5a043fdcf01d8310ef23bc7450574020e9e85a0`; branch main, ahead/behind 0/0. Diff whitespace check passed. No Git mutation was performed.

Protected production Program.cs, FlashcardsPage.xaml.cs, HostTests.cs, FlashcardExportTests.cs, A1/A2 evidence, project/package/manifest/toolchain files, legacy Desktop, MaterialUI, Shared.Core and deferred Tools/Mind/W5 areas have no authored diff. The unrelated ZIP was seen only by filename in Git status; it was never opened, hashed, extracted, modified, deleted or staged.

Only VM and Read Aloud tests changed after the initial twelve-source-file freeze; the other ten source hashes remained identical. Final VM SHA256 `5471303EA188A0008F17932ABE837CDACAE6820B1F087F326F936F8A39818F3E`; final Read Aloud test SHA256 `0752BD1FB99C5DD4965E304DD1467B63F5219BC2CF60711CD43165596C91413A`. No production/test source changed after the final rebuild/full regression/native iteration.

STUDIO-M1-A3 PASS — READY FOR INDEPENDENT READ-ALOUD AUDIT

This is readiness for an independent audit, subject to the explicit bounded observations and gaps above. No staging, commit, push, M1-B or STT work follows.
