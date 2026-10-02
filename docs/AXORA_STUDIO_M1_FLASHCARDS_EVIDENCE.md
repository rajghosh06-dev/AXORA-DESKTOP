# AXORA Studio M1-A1 — Core Flashcards evidence

Verification completed 2026-10-02. This is the core, session-only migration slice, not feature parity, final cutover, or release acceptance.

## A. Baseline and authority

Repository: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`.
Initial preflight independently established `main`, HEAD and local `origin/main` at `770fb3ba703dec6819953a0cee3497fde2337e72`, clean tracked files, empty index, and the unrelated untracked `Axora-Desktop-WinUI.zip`. The ZIP was not opened, hashed, extracted, modified, deleted, or staged.

Authority is the original M1-A1 implementation authorization plus the continuation expanding scope to App's typed-factory wiring and Home's obsolete status text only. No source outside the 16-path allowlist was necessary. All A1 changes remain unstaged.

## B. Exact authored path inventory

New files (9):

- `Axora-Desktop-WinUI/Axora.Studio/Models/FlashcardDeck.cs`
- `Axora-Desktop-WinUI/Axora.Studio/ViewModels/FlashcardsViewModel.cs`
- `Axora-Desktop-WinUI/Axora.Studio/Views/FlashcardsPage.xaml`
- `Axora-Desktop-WinUI/Axora.Studio/Views/FlashcardsPage.xaml.cs`
- `Axora-Desktop-WinUI/Axora.Studio/Services/FlashcardReviewPolicy.cs`
- `Axora-Desktop-WinUI/Axora.Studio/Services/FlashcardTextGenerator.cs`
- `Axora-Desktop-WinUI/Axora.Studio.Tests/FlashcardsTests.cs`
- `Axora-Desktop-WinUI/Axora.Studio.Tests/run-m1.ps1`
- `docs/AXORA_STUDIO_M1_FLASHCARDS_EVIDENCE.md`

Modified existing files (7):

- `Axora-Desktop-WinUI/Axora.Studio/Models/StudioRoute.cs`
- `Axora-Desktop-WinUI/Axora.Studio/StudioBootstrap.cs`
- `Axora-Desktop-WinUI/Axora.Studio/MainWindow.xaml.cs`
- `Axora-Desktop-WinUI/Axora.Studio.Tests/Program.cs`
- `Axora-Desktop-WinUI/Axora.Studio.Tests/HostTests.cs`
- `Axora-Desktop-WinUI/Axora.Studio/App.xaml.cs`
- `Axora-Desktop-WinUI/Axora.Studio/Views/HomePage.xaml`

Ignored build outputs, compiler logs, runner logs, and isolated runtime fixtures are verification artifacts, not additional authored production paths.

## C. Studio-owned domain model

`FlashcardDeck`, `FlashCard`, and typed `CardDifficulty` live in Studio. Identity, card text, and deck membership are immutable; only review/study metadata changes. IDs are unique nonzero N-format GUIDs, with duplicate membership and session IDs rejected. Fields reject malformed UTF-16 and enforce Unicode-scalar limits through strict UTF-8 validation.

Review invariants are finite ease 1.3–3.0, interval 1–36,500 days, nonnegative count, and nondefault UTC review timestamps when a review exists. Newly constructed cards have no fabricated review dates. Complete review state is validated before assignment and before property notifications; count/date overflow cannot partially mutate a review. Deck Easy-share notifications follow difficulty changes.

## D. Custom review policy

The pure production policy preserves the accepted custom arithmetic:

| Rating | New ease | New interval |
| --- | --- | --- |
| Easy | `min(3.0, old + 0.15)` | `min(36500, max(2, floor(old interval * new ease)))` |
| Medium | unchanged | `min(36500, max(1, floor(old interval * 1.2)))` |
| Hard | `max(1.3, old - 0.2)` | 1 day |

Unknown enum values are rejected. One injected `TimeProvider.GetUtcNow()` observation supplies both review dates; count increments once and the card advances only after a valid update. This is not exact SM-2, and no fixed +1/+3/+6-day schedule is claimed.

## E–F. Truthful statistics and examples

The UI says **Cards marked Easy**, not retention. This is the current share of cards whose latest difficulty is Easy; empty decks display `—`, not 100%. Generic examples cover study techniques and Windows concepts, with two cards per deck, initially Medium. They assert no Vault cryptography, mobile pairing, Mica implementation, installed models, or AI capability.

Home now says: “Session-only Flashcards study is available. Exports, read-aloud, Scholar integration, and persistence are not included.” Only the obsolete H0 sentence changed; no dashboard shortcuts or placeholder capabilities were added.

## G–I. Lifetime, composition, and route

Studio registers singleton review policy, generator, ViewModel, and `TimeProvider.System`. A singleton `Func<FlashcardsViewModel>` captures resolution through the existing host. Resolving that delegate constructs no feature. App passes it as the fourth, typed MainWindow argument; MainWindow wraps it in `Lazy<FlashcardsViewModel>` and invokes it only for the Flashcards route.

App.xaml.cs was necessary because H0 constructs MainWindow explicitly at the composition root. Its only change is forwarding that factory. No startup/shutdown, settings ownership, lease, tracker, disposal, or Program.Main logic changed.

The existing explicit page switch constructs `FlashcardsPage(retainedViewModel)` on entry. Pages can be recreated; the DI-owned ViewModel is retained for the process. No service provider enters MainWindow/page, no static service locator exists, and no generic resolver, second container, or navigation framework was introduced. The public narrow `ResolveFlashcards` production seam is exercised by tests without constructing a native Window.

Route values remain Home=0, Settings=1, About=2; Flashcards=3 is appended and displayed after Home. Home remains startup. All four catalog entries resolve real pages.

Deterministic composition counters prove zero VM/policy/generator constructions at bootstrap, delegate resolution, Home, Settings, and About; exactly one of each on first Flashcards resolution; and no reconstruction after navigation. Native PID 29992 logs also show `flashcardsCreated=False` for Home/Settings/About before first entry. Final PID 5300 records False on Home and True only after Flashcards, with subsequent entries retaining True.

## J–L. Selection, navigation, keyboard, and starter creation

The page uses one two-way deck-selection binding, without a duplicate selection-reset handler. Null selection clears current card/progress and disables actions. Repeating the same selection does not reset. A new selection resets index/flip exactly once. Previous/Next wrap for multiple cards, remain safe for single cards, and are no-ops for empty decks; navigation resets the answer side. All cards are practised, with no due-only queue implied.

Selection now validates one clock observation before changing state. Insertion reuses that prevalidated observation, eliminating a second clock read that could previously fail after insertion. A deterministic failing-clock case proves deck count, active deck, index, and flip state are preserved on rejection.

Space/Enter flip; Left/A and Right/D navigate; 1/2/3 and numpad equivalents rate. Already-handled events, modified chords, editing controls, and selectors are guarded. Buttons own Space/Enter, preventing double activation or accidental nested-control flips. The card contains text only, not nested interactive controls.

New Study Deck adds one educational starter card and selects it. The UI explicitly says this is not a deck editor. The create command disables at 100 decks; every insertion also enforces identity and text-budget bounds.

## M. Structured-note generator

The production generator has no AI/model, filesystem, network, or external-process dependency. The optional source label is display metadata only, strips directory components, and is never opened as a path. Parsing is deterministic:

- Trimmed lines shorter than 10 UTF-16 code units are skipped.
- A colon at index 0–39 splits a front/back pair; empty sides are rejected and counted as ignored.
- Otherwise a following trimmed line longer than 5 code units forms an adjacent question/answer pair.
- If no pair results, a document excerpt takes at most 200 Unicode scalars, adding an ellipsis only when more scalars exist. This is not a semantic summary.

CR, LF, and CRLF forms are supported. Scanning is bounded by the strict 1 MiB UTF-8 input limit and checks cancellation periodically; parsing checks cancellation per line. Output is limited to 500 cards, each field to 4,096 Unicode scalars, the session to 100 decks, and aggregate front/back card text to 16 MiB. Invalid input, metadata, capacity, or cancellation fails before deck insertion. No partial generated deck is published.

The actual page provides local text entry and Create cards from structured notes, with inline status/error feedback. It exposes no file picker, import, Scholar handoff, export, speech, or AI control. The final UI limit copy distinguishes UTF-8 bytes and Unicode scalars.

## N–O. Deterministic tests and complete H0 regression

Final `run-m1.ps1` execution exited 0. Evidence under `Axora-Desktop-WinUI/Axora.Studio.Tests/logs/`:

- `m1-20261001T185844328-STUDIO-H0.stdout.log`: **160 passed**, 0 failed.
- `m1-20261001T185844328-STUDIO-M1-FLASHCARDS.stdout.log`: **172 passed**, 0 failed.
- Corresponding stderr logs are empty.

Both ledgers report complete=True, one selected/executed group, total registered groups=2, and zero missing, duplicate, unknown, or blocked cases. H0's original required-case list and production lifecycle/settings code are preserved. The route/inventory expectations reflect the real fourth route, not a frozen assertion total. The existing `run-h0.ps1` also passed (160 assertions; log `h0-20261001T184718014.stdout.log`).

The M1 group has 22 mandatory semantic cases covering defaults/IDs, invalid values, notifications, selection/null/repeated selection, atomic clock failure, empty/single/multiple navigation, flip, automatic advance, exact review arithmetic, boundary/overflow cases, one-clock review timestamps, Easy share, starter/session limits, all parsing branches, line boundaries/newlines/metadata, Unicode safety, input/card/field limits, cancellation/no insertion, lazy composition/retention, and keyboard routing. Tests call production code, not copied implementations.

The final no-argument executable run on 2026-10-02 ran both groups exactly once, again 160/172 passing and exit 0. `--manifest` reports the two groups; `--group=UNKNOWN` exits 2 with “No tests ran” and no success ledger. Production DLL SHA256 values in app and test output match. No assertion count is an acceptance constant. Runner timeouts are bounded and do not force-kill an unsettled process.

## P. Builds and toolchain

MSBuild: `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`.
Evaluated `NETCoreSdkVersion`: **9.0.318**. Both projects were directly restored/rebuilt for Debug/x64 using `-restore -t:Rebuild -p:Configuration=Debug -p:Platform=x64 -v:minimal -m`.

| Build | Exit | Errors | Warnings | Log |
| --- | --- | --- | --- | --- |
| Axora.Studio | 0 | 0 | 8 | `Axora-Desktop-WinUI/Axora.Studio/m1-a1-verified-build.log` |
| Axora.Studio.Tests including referenced Studio rebuild | 0 | 0 | 8 from Studio; no test compiler warnings | `Axora-Desktop-WinUI/Axora.Studio.Tests/m1-a1-verified-build.log` |

All eight are MVVMTK0045 warnings: five existing H0 properties and three new field-backed notes/source/status properties. They were not suppressed. Debug/native operation is verified; release/NativeAOT acceptance is not claimed. The initial test nullable warning was repaired. Project files, packages, SDK policy, references, XAML compiler passes, and generated metadata mechanisms were not altered.

## Q–R. Native UI and session retention

Real executable: `Axora-Desktop-WinUI/Axora.Studio/bin/x64/Debug/net9.0-windows10.0.26100.0/win-x64/Axora.Studio.exe`.
Each launch used process-scoped APPDATA/LOCALAPPDATA under the owned ignored fixture:
`Axora-Desktop-WinUI/Axora.Studio.Tests/logs/m1-a1-native-489f4f6c560c4af986069facb07f768e/{roaming,local}`.

Final rebuilt native run: **PID 5300**, 2026-10-02 17:00–17:09 UTC. Computer-use accessibility observations and directly inspected screenshots establish:

- A real native Home window and all four routes; truthful Home exclusions.
- Two initial example decks, no restoration of the previous run's created decks/reviews.
- Starter creation selected New Study Deck 3 with one card and truthful singular statistics.
- Deck selection, click flip, one Space flip, one Enter flip, Left/Right/A/D, and multi-card wrap/reset.
- Number1/2/3 on the starter yielded review counts 1/2/3, intervals 2/2/1 and ease 2.65/2.65/2.45.
- Actual Previous/Next and Easy/Medium/Hard buttons worked and advanced multi-card study.
- Local structured notes created a one-card Retrieval practice deck; Enter inserted a newline and Number1 inserted literal `1` without changing study state. Enter on the focused generation button created the deck, not an unrelated flip.
- Home → Flashcards preserved four decks, active Structured notes, its current card, answer “Try answering before checking your notes.”, Easy share 100%, count 1, interval 2, ease 2.65, and status. Earlier PID 29992 also demonstrated retention at non-first card 2/2.
- Narrow stacked/scrollable layout and wide two-column layout were visually observed. System theme/native accent and focus indicators were used. A comprehensive light/dark, high-contrast, DPI, screen-reader, and maximum-sized-content matrix is not claimed.
- No export, read-aloud, Scholar import, or persistence controls/claims were present.
- Close through the native X control completed normal shutdown. The retained original Process probe reported **exit 0**; a subsequent process inventory reported **zero lingering Studio processes**. Logs show admission closed, work stopped, host stopped/disposed, window closed, and Program fallback settled. No forced termination was used.

Native journey screenshots/accessibility were inspected in the tool transcript rather than re-decoded or saved. Durable lifecycle evidence is `roaming/Axora/Studio/startup.5300.log` within the fixture. PID 29992's earlier full journey also closed via X with exit 0. PID 9056 was interrupted before Flashcards entry and is not credited as a feature journey.

## S. Idle side effects

Final PID 5300 was observed on Home before feature activation, and again about 70 seconds after entering Flashcards with no study actions. Both snapshots found **0 owned TCP connections/listeners, 0 UDP endpoints, 0 child processes, and 0 loaded module-name matches for ONNX/DirectML/speech/Whisper/llama**. The isolated root contained only H0 `.writer.lock` and PID diagnostic logs, before and after study/close; no settings save, Flashcards export, or persistence file appeared.

These are bounded observations, not a full OS event trace. Source/DI inspection supplies the complementary evidence: the entire feature graph has no microphone/recognition/speech API, socket/listener/network operation, model/download dependency, process-launch call, filesystem operation, or legacy-settings reference. H0's physical legacy-settings byte-preservation test remains green; its path/lease/lifecycle code is unchanged. No OS privacy/security setting was inspected or changed, and no microphone permission was requested.

## T. Bounded performance observations

| Observation | H0 baseline | Final A1 |
| --- | --- | --- |
| Fresh Debug output files | 294 | 295 |
| Fresh Debug output bytes | 97,343,704 | 97,433,395 |
| First nonzero native HWND probe | 2,253 ms | 511 ms on final resumed launch; 1,472 ms on earlier final build launch |
| Working set roughly 5 seconds after launch | 172,015,616 bytes | 165,826,560 bytes |
| Home idle snapshot | not separately sampled | 169,418,752 bytes |
| First Flashcards render log | not applicable | 201 ms |
| Flashcards idle, before study actions | not applicable | 183,508,992 bytes |

Output increased by one file and 89,691 bytes. No heavy/model payload filenames matched the audited output. Baseline probe/build evidence preceded A1 edits; the original baseline window was not targetable by the UI helper and was closed normally with exit 0. Later desktop launches were targetable.

These are individual Debug observations on this machine, affected by caches, desktop launch context, and sampling time. They are not release/install sizes, benchmarks, performance thresholds, or claims of statistically significant speed/memory improvement. First HWND is not equivalent to complete first-page rendering; the Flashcards timing measures route creation through Loaded, not user-perceived latency under every workload.

## U–W. Evidence, protected areas, and Git

This file is the requested evidence artifact. Final scope checks establish no changed authored path outside the allowlist (excluding the untouched pre-existing ZIP), empty index, and no tracked diffs in MaterialUI, legacy Axora.Desktop, scripts, agent configuration, shared infrastructure, or protected W5 work. No package/project-file changes or legacy assembly reference was introduced. `git diff --check` passes; Git only emits existing LF-to-CRLF conversion notices.

HEAD and local `origin/main` remain `770fb3ba703dec6819953a0cee3497fde2337e72` on main. No add, commit, push, merge, rebase, reset, restore, clean, stash, amendment, branch, or tag operation was performed for A1. Build/test/runtime evidence remains local and ignored. The earlier H0 publication verification is separate from this unstaged feature work.

## X–Y. Deferred work, limits, and unexpected findings

M1-A2/A3/B remain separately gated: CSV/Anki/JSON exports, save picker/publisher, read-aloud/speech synthesis, microphone/recognition, voice coordinator migration, Scholar bridge/fallback edits, persistence/import/database, Shared.Core, legacy retirement, and Tools/Mind/W5. No subsequent slice began.

Known limits: session-only data is intentionally lost on exit; no full editor; heuristic output must be reviewed by the user; excerpt truncation preserves Unicode scalars, not whole grapheme clusters; custom review math is not a validated learning-outcome metric; study generation is bounded synchronous work; release/AOT and exhaustive accessibility/theme/device testing are outside the verified Debug acceptance.

Unexpected findings handled within scope: initial NuGet-config sandbox access required approved build execution; an initial baseline desktop visibility problem was resolved through authorized desktop launch; changing window bounds/offscreen automation targets required fresh observations, not blind input retries; physical Escape stopped one final UI attempt, and further input waited for the user's explicit readiness reply. The final resumed native journey passed. The source audit additionally caught and repaired the second selection-clock-read edge case, with a mandatory production regression test.

## Z. Verdict

STUDIO-M1-A1 PASS — READY FOR INDEPENDENT CORE-FEATURE AUDIT

This verdict accepts the authorized core slice only. All work remains unstaged. Do not begin M1-A2 without separate authority.
