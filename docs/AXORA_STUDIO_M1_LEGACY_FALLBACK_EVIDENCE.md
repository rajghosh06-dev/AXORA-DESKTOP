# AXORA Studio M1 — Legacy fallback / cutover truthfulness repair

Date: 2026-10-06 (Asia/Calcutta). Two-phase authority: attachment `fa6abc35-4f3d-44a2-8776-eedcf08e350e/pasted-text-1.txt`. P0 was read-only; its bounded repair gate passed before production edits. No staging, commit or push is authorized.

**This repair does not satisfy B2. M1 remains OPEN. Scholar remains LEGACY ONLY. Legacy Flashcards remains available, and no Studio handoff exists.**

## Baseline and scope

Verified branch `main`, HEAD/local `origin/main`/independently queried actual remote `main` all `a9057a7cbeb3af8c8a6000bac297c502b1021609`, subject `feat(studio): add Scholar fallback guidance`, ahead/behind 0/0, clean tracked tree and empty index. The unrelated ZIP was observed only by Git filename and never opened, hashed, extracted, modified, deleted or staged.

Exact implementation allowlist and authored inventory: three paths, two modified and one new:

1. `Axora-Desktop-WinUI/Axora.Desktop/ViewModels/ScholarKitViewModel.cs`
2. `Axora-Desktop-WinUI/Axora.Desktop.Tests/StudioR0VoiceFlashcardsTests.cs`
3. `docs/AXORA_STUDIO_M1_LEGACY_FALLBACK_EVIDENCE.md`

No fourth path was needed. Scratch scripts, logs, build outputs and synthetic runtime fixtures are verification artifacts, not authored product/test changes.

## P0 source discovery and exact destination

Read the Scholar constructor, editor/change-tracking properties, surrounding export commands and complete Push command; the Flashcards generation, insertion, selection and study-state methods; Scholar/Flashcards status bindings; shell route map and `ShellView.NavigateTo`; `App.TryGetService`; existing R0 ViewModel fixtures, friend-assembly access and semantic group registry.

Original Push resolved `App.TryGetService<FlashcardsViewModel>()`, ignored blank text or absent service silently, called `GenerateCardsFromText(OcrResultText, ImportedFileName)`, reported `Generated flashcards in Flashcard Studio!`, then called `App.MainAppWindow?.ShellRoot.NavigateTo("Flashcards")`.

The receiving singleton is **Axora.Desktop.ViewModels.FlashcardsViewModel**, in **Axora.Desktop.exe**. `ShellViewModel.PageMap["Flashcards"]` targets **Axora.Desktop.Views.FlashcardsPage**. The generator inserts at index zero and selects the new deck before navigation. No Studio process, route, service or transfer is involved.

Push does not extract a document anew or inspect a file. It uses the current editable text and display filename. `HasLoadedDocument` is not a validity gate: manually typed text is supported. Nonblank text with no structured pair becomes an excerpt card in the unchanged legacy generator. There is no normal zero-card return, asynchronous wait, cancellation token or separate unsupported-file branch in this command.

## P0 branch matrix and frozen copy

| Branch | Original behavior | Accepted/final behavior |
|---|---|---|
| Valid nonempty editor source, including manually entered text | Generate, ambiguous success, legacy navigation | Existing generator; verify one new selected, inserted nonempty deck; report `Created flashcards in AXORA Desktop.`; request legacy route |
| No source / null / empty / whitespace | Silent no-op | `Scholar content is empty. Enter text first.`; no target resolution, generation or navigation |
| Short/unstructured text | Excerpt fallback | Preserve excerpt algorithm; real nonempty deck counts as success |
| Unsupported file / invalid extraction format | Not a Push branch | N/A: import/extraction is upstream; no new format policy |
| Missing target or target-resolution exception | Silent or uncaught exception | `Flashcards are unavailable in AXORA Desktop.`; no navigation |
| Abnormal zero cards / no retained insertion | No explicit validation | Restore prior study state; `No flashcards were created. Existing decks are unchanged.`; no navigation |
| Generation/insertion/selection exception | Uncaught; notification exception can occur after insertion | Restore the insertion/selection delta; `Couldn't create flashcards. Existing decks are unchanged.`; no navigation |
| Recovery itself cannot settle because a notification throws | No handling | Safe defensive `Couldn't create flashcards. Check your AXORA Desktop decks.`; do not assert recovery succeeded |
| Missing window or thrown navigation after creation | Nullable no-op or exception after success | Keep created deck; `Created cards in AXORA Desktop. Open Flashcards manually.` |
| Silent return from existing void navigator | No verified navigation result | Creation-only success copy; no assertion that navigation succeeded |
| Cancellation | No command cancellation branch | N/A; remains a synchronous command |

Success linearization is verified nonempty deck insertion/selection, **not** method return or attempted navigation. Success does not promise durable save, Studio transfer or successful route completion. The destination's existing `ExportStatus` also carries the creation message, so it remains visible after leaving Scholar.

For rejected/no-op generation the command snapshots deck references, active deck/card, index, flip, progress/statistics and active deck's LastStudied value. It restores the insertion/selection delta without removing prior decks or changing their cards/review arithmetic. Work is synchronous on the existing UI command thread. No transaction spanning unrelated concurrent actors is claimed. A repeatedly throwing external notification handler can defeat restoration; the defensive recovery message explicitly avoids claiming unchanged state in that case.

## Architecture gate and rollback

P0 disposition: **PASS; BLOCKER 0, HIGH 0, MEDIUM 0 architecture findings**. This is a command-local truthfulness/state-recovery repair. The generation algorithm and shell routing architecture are untouched.

Two internal instance-local target/navigation delegates permit deterministic production-command tests; both are null in the application. Existing friend-assembly access is reused. There is no generator fake, global switch, new locator, service/framework, bridge DTO, source-transfer contract or dependency change. The default target resolution and `Flashcards` destination remain the existing legacy calls.

Rollback is removal of these command/test/evidence edits after separately authorized Git action; no schema, migration or user-file conversion exists. Published Studio B1 remains unchanged and accurate. No route is hidden, retired or redirected.

## Deterministic verification

Added tests run inside the existing `R0-VOICE` semantic group and execute `PushToFlashcardsCommand.Execute(null)`, with the real legacy generator and Flashcards ViewModel. Existing fake voice/document dependencies keep microphone, models and file import out of these tests.

Thirty new assertions cover:

- Four null/empty/whitespace cases: explicit feedback, no target resolution/navigation, full prior state retained.
- Missing/throwing target: safe status and prior state retained.
- Structured, short and unstructured manual text: actual nonempty insertion before exact `AXORA Desktop` status and `Flashcards` route request; existing decks retained.
- Actual collection/selection notifications inject zero-card, removed-insertion, insertion-exception and selection-exception outcomes. These execute the production generator and recovery path, then verify deck identity/order, selected card, index, flip, progress/statistics, LastStudied, review count and prior export feedback.
- Missing/throwing navigator: created cards retained, safe manual guidance; private exception sentinel never appears in status.
- Legacy route maps to the legacy page; the legacy assembly has no Studio reference.

The old ambiguous success string, blank-input silence, lack of insertion validation and absent exception recovery would fail these behavioral assertions. No test calls a copied command algorithm, forces success or uses timing sleeps.

Current rebuilt targeted runner result:

| Semantic group | Passed assertions | Failed |
|---|---:|---:|
| M4-FLASH | 31 | 0 |
| R0-VOICE | 147 | 0 |
| W4-INT | 15 | 0 |
| Total | **193** | **0** |

Manifest total 44; requested/executed groups 3/3; missing/duplicate/unknown groups zero; cleanup-failed=False; wrapper exit 0, `TARGETED-PASS`. This is not a full legacy-suite claim.

Retained authoritative run root: `C:\Users\rajghosh\AppData\Local\Temp\axora-p3a-77e3f05f2e404f06807f841ebe3b74f7`.

An earlier wrapper started while a repeated rebuild was replacing x64 outputs and selected a stale September non-x64 binary. That binary ignored the manifest argument; the wrapper timed out and terminated only its owned test PID. The failed attempt is not counted. After the build completed, the wrapper selected the verified current x64 executable and passed above. No application was force-killed.

## Build evidence

Pinned SDK resolved to **9.0.318**. `dotnet build Axora.Desktop.Tests/Axora.Desktop.Tests.csproj -c Debug -p:Platform=x64 -t:Rebuild -v:minimal` rebuilt the legacy app and tests through their existing project reference.

Final retained build: exit 0, **0 errors; 366 warning emissions**, elapsed 1:24.40. An initial identical rebuild also completed with zero errors. No project/package/toolchain file changed and no warning was suppressed. Warning inventory is preserved in the build log: MVVMTK0045, CS8602, CS0618, CS0067, CS0436, CS0649, CS1998, CA2022 and NU1900 package-vulnerability-metadata access warnings. Distinct diagnostic lines number 359; emission totals include repetition. Existing field-backed properties and the existing R0 fake's unused event remain warnings; this repair adds no annotated property, package or framework.

The preexisting app build target has force-stop behavior. No Desktop process was running before either build, so it had no application to terminate. No Studio build/native suite was rerun: Studio has no legacy ProjectReference and no shared source was modified.

Build/runtime artifacts: `C:\Users\rajghosh\AppData\Local\Temp\AXORA-M1-legacy-fallback-20261006`.

## Bounded actual UI Automation

Programmatic Windows UI Automation used the current `bin/x64/Debug/.../Axora.Desktop.exe`, synthetic typed editor text and isolated APPDATA/LOCALAPPDATA settings. P2P autostart, QuickDrop listening, voice navigation and telemetry preference were disabled. No import, Save Session, extraction, microphone or model feature was invoked.

Scholar's legacy index constructor still uses the OS special-folder path rather than APPDATA. Read-only preflight established that its directory already existed and no user model was present. This journey did not index or save a document; no isolation fix was added to the repair. No real user source/document was opened for verification.

Final owned process **PID 8900**, actual title **Axora Desktop**:

| Check | Observed result |
|---|---|
| Empty editor → Push | Exact empty-source status, IsOffscreen=False, bounds `1347,427,327,23`; Scholar Push remains present |
| Synthetic text → Push | Exact `Created flashcards in AXORA Desktop.` status visibly exposed on destination, IsOffscreen=False, bounds `607,1009,310,22` |
| Destination | Legacy Previous control present; Scholar Push absent; actual legacy Flashcards page |
| Studio boundary | Zero Studio PIDs at the observation; source contains no launch/handoff |
| Close | CloseMainWindow requested; normal exit **0**, no owned Desktop remaining |

Interrupted harness attempts were corrected without production changes: a guessed PowerShell path launched nothing; UIA initially selected an inner TextBlock instead of its Button; an Invoke without focus did not settle the editor binding; focus was then applied only to the actionable Button. A stale Scholar status element became offscreen after navigation; final evidence reacquired the visibly exposed destination element. Interrupted attempts are not accepted PASS evidence. All launched apps closed normally. Computer Use, coordinate clicks and broad native regression were unnecessary.

Final `ui-observations.json`, `ui-close.json`, script and log remain in the artifact root. No pixel/contrast, screen-reader, narrow layout or full accessibility certification is claimed. This task changes no XAML.

## Privacy, hostile audit and limitations

All new user messages are fixed safe strings. New Debug diagnostics contain only operation phase and exception type. No text, filename, private path or raw exception message is logged by this repair. The bounded startup-log search found zero matches for the synthetic source term/answer. That is bounded privacy evidence, not an audit of all historical Scholar logging outside this command.

Final source/diff review challenged premature success, zero/no-op generation, silence, state loss, navigation after failure, Studio implication, retirement, transfer and content leakage. Each normal/synthetic fault branch has production-command evidence above. Existing generation/parser/arithmetic, route map, composition and lifecycle source are unchanged.

Disclosed LOW/unverified boundaries for user review: existing build warnings and Release/AOT debt; no broad visual/accessibility/platform certification; physical navigation failure and abnormal generator/recovery faults use deterministic evidence rather than induced native failures; persistent hostile notification handlers are not an atomic rollback domain, and failed recovery is disclosed instead of false unchanged-state feedback. Existing void navigation can silently return, so success copy intentionally claims creation only.

Final hostile internal audit: **BLOCKER 0; HIGH 0; MEDIUM 0** within this bounded repair. No B2, Scholar producer/migration, persistence, STT, IPC, process launch, file handoff, network operation or new generator is introduced.

## Git/protected-area disposition

Final gate independently confirmed actual remote main, local HEAD and origin/main all remain `a9057a7cbeb3af8c8a6000bac297c502b1021609`, ahead/behind 0/0. Everything remains unstaged, index empty and diff whitespace check passes. ZIP remains untouched/untracked. Studio, MaterialUI, W5, Tools/Mind, legacy Flashcards generation/page, shell route, App/Program/lifecycle, projects/packages/manifests/toolchain, test registry and A1/A2/A3/B1 evidence have no authored changes.

**STUDIO-M1 LEGACY FALLBACK REPAIR PASS — READY FOR USER ACCEPTANCE AND COMMIT AUTHORIZATION**

This is readiness for acceptance of this three-path repair, not M1 closeout, B2 completion, release certification or permission to stage/commit/push.
