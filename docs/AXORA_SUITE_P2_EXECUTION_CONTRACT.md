# AXORA suite — SUITE-P2 foundation resolution and migration execution contract

**Status:** planning and bounded read-only diagnosis, 2026-09-28. SUITE-P0 and [SUITE-P1](AXORA_SUITE_P1_DEPENDENCY_MAP.md) are accepted architectural direction; this document does **not** accept V0-R4, repair source/tests, create apps/projects, migrate data, start W5-P2, or authorize Git writes. [V0-RP](V0_REMEDIATION_EXECUTION_PLAN.md) remains the accepted historical remediation plan; scheduling changes proposed below need separate user authorization. Source and current checkout are authority for implementation facts; historical logs are labeled as historical evidence.

## 1. Frozen portfolio and product decisions

| Boundary | P2 decision |
|---|---|
| Umbrella | AXORA monorepo; independently runnable **AXORA Studio**, **AXORA Tools**, **AXORA Mind**, introduced incrementally. |
| Process identity targets | `Axora.Studio.exe`, `Axora.Tools.exe`, `Axora.Mind.exe`. Current `Axora.Desktop.exe` stays the reference/migration host until each feature's parity and cutover are verified. No rename or new executable in P2. |
| Settings | App-owned under conceptual `%APPDATA%\Axora\Studio`, `...\Tools`, `...\Mind`; roots **not created**. Theme/accent may be independently imported. No live suite settings database; no automatic transfer of telemetry/privacy consent as three-product consent. |
| LAN | Future Tools P2P listener **off by default**, persistent enablement only by explicit configuration with visible actual status. Studio and Mind never inherit Tools startup. Current legacy default remains unchanged in P2. |
| Deployment | Preserve present unpackaged/Windows App SDK assumptions sufficiently for functional parity. Packaged versus unpackaged redesign deferred until real hosts and measurements; Debug size is not installed size. |
| Shared projects | Zero initially, or at most `Axora.Shared.Core` **after** one stable primitive has two proven consumers. No pre-created `Shared.Platform`, `Shared.UI`, `AI.Contracts`, or shared product/runtime monolith. |
| User Problem First | New functionality must establish affected user, frequency/workaround, pain removed, local/offline/optional-dependency behavior, storage, unavailable state, verification and recovery. Decomposition is not an excuse to admit speculative features. |

## 2. Current exceptional Git state and evidence boundary

Verified `main` at `75ca36ad12f9c64fc5ff88b549d1aece313353c2`; zero staged changes. Pre-existing unstaged deletion: `Axora-Desktop-WinUI/global.json`. Pre-existing untracked items: root `global.json`, `Axora-Desktop-WinUI.zip`, `docs/AXORA_SUITE_PRODUCT_PORTFOLIO.md`, `docs/AXORA_SUITE_P1_DEPENDENCY_MAP.md`, `docs/V0_REMEDIATION_EXECUTION_PLAN.md`, and four `docs/W5_IMAGE_STUDIO_*.md`. No additional unexpected path was observed. The SDK-pin attempt is **not accepted** simply because it exists; none of these items is restored, deleted, staged or normalized here. The ZIP is not an installation-size measurement. P0/P1/V0/W5 and MaterialUI remain protected from P2 edits.

Evidence in this contract has four levels: **current static source/current CLI**, **historical execution log**, **inference from a trace**, and **unverified future gate**. A historical green run, a partial assertion count, a successful build or a readable ZIP central directory each proves only its own narrow observation. The AXORA build/regression skill files' older “59 assertions” number is stale against current source; it is not used as an acceptance target.

## 3. V0-R4 classification: toolchain mechanism versus acceptance

The attempted root `global.json` contains SDK `9.0.300`, `rollForward: latestPatch`, `allowPrerelease: false`; the nested WinUI `global.json` is absent in the worktree. Current `dotnet --version` from repository root and WinUI directory both report **9.0.318**; installed SDKs are 9.0.318 and 10.0.401. These observations support the intended .NET 9 feature-band selection, including root entry points. Prior recorded build and bounded app launch/close under 9.0.318 succeeded. P2 did **not** rebuild or rerun the app, so current compiler/runtime parity is historical, not newly proven.

**Classification: TECHNICALLY VALID BUT ACCEPTANCE BLOCKED BY TEST INFRASTRUCTURE.** No observed evidence makes the SDK pin itself defective. The later R4 broad-run log has no final summary; a separate unknown ZIP appeared, so the R4 acceptance protocol was not completed. This is *not* an instruction to undo the pin or declare R4 accepted. Future acceptance requires a trustworthy bounded R1A ledger, root/nested/Visual Studio build entry-point evidence, a completed meaningful suite with environment-dependent dispositions, bounded launch/graceful-close, and an explicit review of the exceptional Git state. The ZIP may remain excluded/untouched if its provenance remains unknown; it must not be silently normalized into the baseline.

## 4. Bounded broad-test investigation and confidence classification

The preserved R4 log at `C:\Users\rajghosh\AppData\Local\Temp\axora-v0-r4-232123c4cf8746eca59653479e652646\r4-tests-final.log` has **1,685 `[PASS]` lines, zero `[FAIL]` lines, two `[NOT-AVAILABLE]` lines and no `TEST RUN SUMMARY`**. The last recorded assertion is **`T4-03: Sensitive dictated text is strictly redacted and absent from application ILogger outputs`** in `W4_VoiceSubsystemTests.cs`. The next source block is **T4-04**, the 15-iteration rapid real `VoiceTranscriberService.StartDictationAsync` / `StopDictationAsync` WinRT-handle test; its assertion is printed only after all iterations. There is no iteration marker in the old log, so the exact iteration and awaited native operation cannot be recovered. No `Axora.Desktop.Tests` process from that attempt remains live now.

**Call chain and boundedness:** `Program.Main` → `RunW4VoiceSubsystemTests` → `RunW4Tier4_DiagnosticsAndPrivacyTests` → T4-04 loop → `VoiceTranscriberService.StartDictationAsync(Action<string>)` → `_startStopLock.WaitAsync(ct)` → new `Windows.Media.SpeechRecognition.SpeechRecognizer` → `CompileConstraintsAsync()` → `ContinuousRecognitionSession.StartAsync()`; each iteration then calls `StopDictationAsync()` → lock wait → `ContinuousRecognitionSession.StopAsync()` / recognizer disposal. T4-04 passes no cancellation token, applies no per-await or whole-test timeout, and emits no progress inside the loop. The test harness's main awaits groups sequentially without an overall timeout. Therefore any WinRT recognition call that never completes can prevent the final summary. The semaphore itself is not proven deadlocked: source releases it in `finally`; native asynchronous start/compile/stop or synchronous disposal remain possible wait sites. The T4-03 logging test had already completed one real start/stop cycle; T4-04 stresses repeated cycles.

**Classification:** blocking **locus HIGH-CONFIDENCE** (T4-04 real speech-recognizer lifecycle); exact native root cause **UNKNOWN** without a trace/instrumented bounded rerun. Audio/microphone/Windows speech runtime involvement is high-confidence; an SDK-pin defect is not. This overlaps V0-W4-002 media/recognition lifetime and V0-FND-002 resource ownership, and strengthens R1A's need for bounded environment tests; it does not prove a specific V0-W4 fix. `SpeechRecognizer` prerequisite probe in the same log reported **Dictation Available: False** while the microphone monitor reported present/healthy; these are different conditions, so the test's unconditional “probe completed” assertion is not proof of recognition availability.

**APPDATA and older evidence:** the R4 command set `APPDATA` to a dedicated temporary `test-appdata` directory before running; W1.7 settings assertions did not fail in that run. An earlier restricted execution printed W1.7a/b persistence failures, plausibly because its settings path was not writable; another historical execution on 2026-09-16 produced a complete `1726/1726` summary. These observations show environment sensitivity and historical intermittency, not a reliable current pass or proof that APPDATA caused the T4-04 stall. No new invocation was made in P2: the exact historical last assertion and next test are available from preserved evidence, while replaying the broad runner would activate microphone/speech and lacks a built-in timeout. P3A should add per-operation timeouts/instrumentation and an explicit environment-capability disposition **before** any newly authorized bounded replay, using isolated settings and no user audio/data mutation beyond the separately approved runtime test contract.

## 5. Unknown ZIP forensics — metadata only

| Read-only observation | Result |
|---|---|
| Path/size | Repository root `Axora-Desktop-WinUI.zip`; **494,366,020 bytes**. |
| Filesystem timestamps (UTC) | Created **2026-09-28 12:41:18**, last written **12:41:44**. Timestamps do not establish author or generating tool. |
| SHA-256 | `9405B73D8B96F8468D2CE228ED069B557BB5343F069C3987873F172197743746`. |
| ZIP parse | Central directory opens successfully and reports **5,989 entries**; compressed entry lengths total **492,372,886 bytes**, uncompressed lengths **1,446,243,021 bytes**. This is structural validity only; every payload/CRC was **not** expanded or checked. |
| Top-level and composition | Every entry is under `Axora-Desktop-WinUI/`: `Axora.Desktop` 4,036 entries, `Axora.Desktop.Tests` 1,777, `.agents` 137, `.vs` 27, plus root project/build/QA text/log entries. About 5,558 paths contain `/bin/` or `/obj/`; no `.git/` entry and no nested `global.json` entry. Includes current-looking source and compiled/test artifacts, not a clean source-only repository snapshot or a release package. |
| Producer search | No repository script/source command referencing this exact ZIP name/path or a project-archive creation command was found in the scoped `scripts/` and WinUI source search. Generic app ZIP handling is unrelated. External/manual creation is possible but unproven. |

**Provenance: UNKNOWN.** Metadata supports “project-directory archive including build/QA outputs” as a description of contents, **not** proof of who or what created it or why. It could be a manual/QA snapshot; it is not safe to classify as a distributable. No entry content was executed, no archive was extracted, and the file was not changed. Keep it untracked and out of builds, releases, size claims and deletion plans pending a separate user decision. An optional later forensic step could compare a small allowlisted set of source hashes *in an isolated read-only stream*, but that still would not prove authorship and is not needed for the migration contract.

## 6. Revised V0 remediation ownership and ordering

This is a proposed scheduling refinement to the accepted V0-RP, **not** a modification of that plan or an authorization to repair. It separates foundation from product-specific gates. Preserve each finding's verification and rollback contract; all 24 P1 finding dispositions remain tracked. Do not force Tools security repairs to precede creating a Studio host that cannot invoke those features; equally, do not copy unsafe feature code into a new host for later cleanup.

| Lane | Required sequence / cutoff | Owner and gate |
|---|---|---|
| SUITE foundation | R4 acceptance closure + R1A truthful test manifest/hang disposition → R1B fatal-start and single-disposer lifecycle | Before **any** new host. A historical green count cannot substitute for current bounded completeness. |
| STUDIO voice/Flashcards | R2D (`FLS-001`, `W4-001`, core `W4-002`) → narrow Studio voice/settings/route contract and executing UI evidence → Studio H0/M1 | Before Flashcards cutover. Full legacy R2E is **not** required for the first Studio host if the new host uses an independent recoverable settings store, and old settings remain sole-written by legacy. This is a proposed amendment to P1's broad A-before-extraction timing, requiring approval. |
| STUDIO Resume | Verified backup/read/single-writer contract + safe export publication + both routes + ATS/PDF truth | Before M2. Old Documents files remain intact until new writer is accepted. |
| STUDIO Scholar | R2C (`W3-001/002`) generation/path/task repair; R2D for any retained voice; R2E settings aspects when imported | Before M3. No index writer copied before repair. |
| TOOLS publication | R2A (`W2-001`, `CMP-001`) repaired in legacy first; format-specific truth review for first feature | Before Tools publisher canary or any Converter/Compressor cutover. Publisher may start product-local; Shared.Core only after proven second-app consumer. |
| TOOLS executable/process | R1C (`W15-001/002`) → R2B (`BAT-001/002`) where Batch uses ImageMagick | Before Extensions or Batch cutover, not before Studio H0. R1C is not a technical dependency of Studio R2D. |
| TOOLS security/data | R1D (`P2P-001`) before LAN; R1E (`VLT-001`) before Vault | Keep both features unmigrated until their own full security/compatibility gates. Future Tools LAN default is off even after repair. |
| Legacy closure / suite proof | Full R2E (`SET-001`, `NAV-001`, remaining `W4-002`) and R3 (`TEST-001`, `UI-001`) → RV historical re-verification | Required before claiming V0 remediation complete or W5 implementation baseline. Per-slice executing UI evidence is required **earlier**, at each cutover; do not wait for a final all-app R3 to verify the first slice. |
| Deferred | R5 selections (`W2-002`, `W3F-001`, `W4-003`, `BLD-001`) | Non-blocking only with truthful capability/wording and explicit separate scope. |

The old linear list R4→R1A→R1B→R1C→R2A→R1D→R1E→R2B→R2C→R2D→R2E→R3→RV is valid as a conservative *single-host remediation* order. For incremental three-host migration, independent Studio and Tools lanes may proceed after the shared foundation, with per-feature cutover gates. `App.xaml.cs` and test `Program.cs` are convergence points: implementation waves touching them still need serialized integration, not overlapping edits. R2A should remain a bounded publication repair, not an excuse to absorb W5 codecs or Scholar multi-file index logic.

## 7. Minimum pre-decomposition foundation

Before creating any first new `.csproj`, require: (1) documented acceptance decision on the SDK pin from root/nested/Visual Studio evidence; (2) R1A's group manifest, bounded environment tests, exit-code/summary ledger and missing-group detection; (3) R1B's fatal startup and one-owner disposal contract proven in the legacy host; (4) frozen app identity and per-app data roots; (5) an explicit first-feature scope that excludes all unresolved high-risk services; (6) per-slice parity and rollback tests with a single-writer rule; (7) a cleanly enumerated exceptional Git state, leaving the unknown ZIP alone. The test-hang root need not be fully fixed to create a no-audio host, but R1A must prevent a silent indefinite run and report unavailability/failure honestly. A Flashcards cutover additionally needs the Studio-specific gates in section 8.

This foundation **does not** require R1C/R1D/R1E/R2B/R2C, all of R2E or all of R3 before the Studio shell is created. It **does** prohibit copying their unsafe feature implementations. W5 remains downstream of V0-RV and W5-P2 codec proof; no W5 source creation is implied by suite host creation.

## 8. First Studio slice — conditional freeze of Flashcards

**Decision:** retain Flashcards as the *first intended Studio feature slice*, but **not** as an unconditional low-coupling move. Current `FlashcardsViewModel` directly injects `ISpeechSynthesisService` and calls it in `SpeakCurrentCardAsync`; R2D must route playback through the corrected coordinator and await terminal state. Current exports use direct writes to user-selected CSV/Anki/JSON paths; new-host export must preserve prior destination on failure, validate result and report overwrite/cancel truth. The seeded deck text also includes unverified “AI generation,” “neural” speech and Mobile pairing/WebSocket claims; parity may intentionally correct misleading wording, with the correction documented rather than silently asserting the old claim.

**Newly confirmed cross-feature seam omitted from P1's simple-VM dependency view:** `ScholarKitViewModel.PushToFlashcards()` resolves `FlashcardsViewModel` through `App.TryGetService`, calls `GenerateCardsFromText(OcrResultText, ImportedFileName)`, then navigates the legacy shell to `Flashcards`. The source Scholar page exposes this as a user action. Therefore M1 cannot claim full parity if Studio Flashcards only shows seeded decks while the legacy Scholar button becomes dead or silently opens old Flashcards. A separately authorized M1 contract must provide an explicit, consented, versioned, bounded *legacy Scholar → Studio Flashcards* handoff, with validation and cleanup, or keep the old route as a clearly marked fallback until a verified handoff is ready; final feature cutover waits for the bridge. No raw academic text on a command line, silent cloud call, permanent broker or direct shared VM. A temporary fallback is migration behavior, not permanent dual ownership.

**Minimum prerequisites:** accepted R4/R1A/R1B; R2D voice/Flashcards repair including real-mic timeout disposition; Studio-local versioned/recoverable settings for voice/theme with no write to legacy `settings.json`; a typed route containing only functional Studio pages/actions; no Tools P2P/extension/ONNX eager registration; safe export result and source integrity tests; actual keyboard/accessibility and voice-unavailable evidence; Scholar handoff/fallback parity; explicit rollback to repaired legacy Flashcards. **Not a prerequisite:** all legacy R2E Tools settings, full Converter palette repair, R1C/R1D/R1E/R2B/R2C or final suite-wide R3/RV. The narrow R2E-style Studio settings/route contract is a scheduling split that must be explicitly authorized, not an assertion that V0-R2E is already fixed. Flashcards has no proven durable deck database; unsaved in-memory review state is not migratable and the cutover UX must say so.

If the user declines the Scholar bridge/fallback or the bounded voice remediation, **do not cut over Flashcards**; reconsider the first feature with a new authority decision rather than quietly reducing parity.

## 9. Probable second Studio slice — Resume

Resume remains the probable M2 because it is Studio-owned and does not require Scholar indexing or LAN. It is **not** low-risk: `Documents\Axora\Resumes` contains user-authored JSON; `ResumeStorageHelper.RenameResumeAsync` writes a new file then attempts to delete the old, while pages/VM also perform direct writes, duplicate/delete and export. M2 requires inventory by actual path and ID/title, immutable backup, read-compatibility of old JSON, dry-run conversion, deterministic duplicate/rename rules, validated save/export with existing destination preserved on failure, and one writer before new-host edits. Both `ResumeStudio` dashboard and internal `ResumeStudioEditor` routes must work; New/Edit/Rename/Duplicate/Delete, templates/presets, PDF/TXT/JSON export/import and open-folder behavior need parity/intentional correction evidence. ATS optimizer/PDF compiler outputs and unsupported conditions must be truthful, not merely a document emitted. Keep old files readable and a bounded rollback window; do not let old and new hosts edit the same resume concurrently.

## 10. First Tools starting point and subsequent feature choice

**Recommend exactly one first Tools host slice: `TOOLS-H0` — an independently runnable Tools shell with an app-owned, safe-publication contract canary and no user-file transformation cutover.** It has `Axora.Tools.exe` identity, Tools-local versioned settings (future LAN false), a truthful minimal route/status page, no P2P listener, no extension installer or model runtime startup, and an isolated test fixture exercising the R2A-repaired publisher. The canary writes only test-owned disposable files and is not exposed as a fake product feature. This proves host identity, DI/resource ownership, output safety and absence of background behavior before transferring risky processors. It follows R4/R1A/R1B and R2A; R1C is not required for the shell/canary. No Shared.Core is created just for this one consumer.

The **next user-facing Tools feature candidate is Compressor**, one feature after H0, not bundled with Converter. It has fewer conversion engines/queue-routing dependencies than Converter, but R2A only repairs publication. Static source shows an additional capability-truth gap: `CompressImageInternalAsync` always selects `BitmapEncoder.JpegEncoderId` while preserving the input extension in the `_compressed{ext}` output path, so PNG/BMP/TIFF input can be labeled with a non-JPEG extension despite JPEG bytes. Office/ZIP recompression also streams untrusted entries without an explicit expansion budget. These are **P2-discovered review gates**, not claims that V0-CMP-001 already covers them. Before a Compressor M1 cutover, separately authorize format/extension/content and archive-resource validation, failure/collision/telemetry parity, and real output reopen. Converter still requires repaired overwrite plus engine/queue/format parity; “Converter + Compressor” is too wide for the first Tools feature commit. Extension Manager and Batch require R1C (and Batch R2B); Vault and P2P stay late after R1E/R1D. If a host shell without a feature is considered insufficient as a “slice,” the answer is **no safe Tools feature is ready before these repairs**—not permission to choose an unsafe one.

## 11. Mind first-project start condition

Mind is a new product, not an extraction destination for Scholar's DocumentChat, synthesis, index, DirectML engine, Tools Extension Manager or Vault. Creating `Axora.Mind.csproj` is worthwhile only after **one** independently runnable Studio or Tools host has demonstrated a reproducible multi-host build/run/close pattern and per-app identity/data-root conventions; a Mind product contract has fixed local-vs-cloud labels, provider/credential protection, consent and retention/deletion/export rules; the user has admitted an initial offline-useful thin history/UI flow; and measured packaging/runtime evidence bounds the extra host cost on a 16 GB/Iris Xe class machine. Mind's model/capability manager is a future core capability but no model download or provider is implicit in H0. Mind must not block Studio/Tools migration, V0-RV or W5 codec proof. If those preconditions are absent, keep Mind at product-contract level rather than creating an empty project for symmetry.

## 12. Per-domain data cutover protocol

For every persistent domain, an implementation authorization must name the **source and destination paths, schema versions, owner, writer**, and exact reversible procedure. The invariant sequence is:

1. **INVENTORY:** enumerate actual files/versions/identifiers and all path variants, without assuming a single `%APPDATA%` root.
2. **BACKUP:** preserve original bytes and a manifest/hash in a user-controlled or test-owned recoverable location; prove the backup can be read.
3. **READ COMPATIBILITY:** new reader accepts supported old data without rewriting it; unsupported/corrupt data is reported, never silently defaulted over.
4. **DRY RUN:** transform copies, report conflicts/space needs and proposed changes; no live mutation.
5. **VALIDATION:** reopen transformed output, verify schema/content/links/counts and failure injection; compare source hash where applicable.
6. **SINGLE WRITER:** explicitly stop/disable writes in old host for that domain before enabling new writer. No two live hosts write one legacy store absent a designed multiwriter protocol.
7. **CUTOVER:** user-visible owner switch with atomic or recovery-safe publication, clear success/partial/failure result and no hidden deletion.
8. **ROLLBACK WINDOW:** keep old data and executable reader available for a documented period/condition; old host is read-only or becomes sole writer again only through an explicit reverse transition.
9. **FINAL OWNERSHIP:** record new canonical path/version, import provenance, backup disposition and which host can write. Retire legacy path only with separate authority.

| Domain | Specific inventory/compatibility/validation | Single-writer and rollback requirement |
|---|---|---|
| Mixed legacy `settings.json` | Read `%APPDATA%\Axora\settings.json` including an `APPDATA` override/fallback; parse v1 fields separately; validate theme/accent, voice, download path, KDF and port. Do **not** import legacy telemetry `true` as consent for all apps. | Legacy remains sole writer of old file. Studio/Tools/Mind create independent stores only after their own schema/backup gates. Initial theme/accent copy may be one-time; optional later sync needs a separate protocol. Preserve old bytes. |
| Resume JSON and exports | Inventory `Documents\Axora\Resumes` including user titles/IDs and direct-written files. Dry-run duplicate/rename conflicts; reopen PDF/TXT/JSON exports and validate protected existing destination. | Studio one writer at cutover. Preserve source files and old reader; no concurrent Resume editing in two apps; rollback reassigns writer explicitly. |
| Scholar documents, sessions, indexes, metadata, quarantine | Inventory both library `APPDATA`-override root and `SpecialFolder.ApplicationData` paths used by vector/model helpers; detect duplicate/divergent generations. R2C generation protocol and corpus/search/restart tests first. | Studio index/library writer only after verified complete generation. Keep previous verified generation and legacy reader; never copy half an index or delete before replacement. |
| Optional Scholar model assets | Inspect actual model/vocab paths, checksum/license/version/space and whether model exists; no model bundled by assumption. | Studio optional ownership. Never make Tools/Mind installation depend on it; rollback removes only exact newly copied asset after reference check. |
| Flashcards | No durable deck store is proven. Inventory only user exports and in-memory state at the moment of cutover; seeded decks are code, not migrated records. | No hidden disk migration. Warn that unsaved session/decks do not cross process; verified Scholar handoff must preserve user-initiated generation. Legacy fallback remains until parity. |
| Vault sealed key and `.axvault` outputs | User-bound DPAPI seal at `%APPDATA%\Axora\vault_sealed.dat`; inspect version/KDF and encrypted-file fixtures without exporting secrets. R1E old/new decrypt/tamper/round-trip proof before any path move. | Tools sole key writer. Preserve old seal and ciphertext; rollback must restore decrypt ability in original user context. A copied seal is not portable to a new PC. |
| Extension cache and installed dependencies | `%LOCALAPPDATA%\Axora\ExtensionCache` and `Extensions`, executable identity/version/hash/publisher/known-good state, fallback paths. R1C trust validation before reuse. | Tools sole installer/cache writer. Never remove known-good before verified replacement; retain recovery artifact and offline installed-version detection. |
| QuickDrop/download locations | Compare configured `DownloadDirectory` with P2P's fixed `Downloads\Axora_QuickDrop`; inventory actual user files and partials without treating in-memory `Transfers` as history. R1D authenticated transfer/ACK truth first. | Tools sole transfer writer and LAN owner; future listening off by default. Do not relocate user Downloads silently; rollback stops new listener and preserves files. |
| Future Mind conversations, attachments, credentials, model packages | No legacy data. Define schema, local/cloud provenance, deletion/export, encrypted credential storage, optional large cache root and model license/hash before first write. | Mind only; no legacy migration or borrowed Scholar/Vault writer. |

For user-selected outputs across features, distinguish *input/source integrity* from *store migration*: a safe output publisher must never overwrite a source or destroy an existing destination on failure. The user must be able to inspect what remains after cancellation. Data backup does not authorize deleting the source.

## 13. General vertical-slice parity template

Each future slice receives a concrete matrix of fixtures, commands and expected results, with **legacy observation**, **new-host observation**, **intentional correction**, **evidence type**, **owner**, **rollback action** and **status** per row. Neither compilation nor a synthetic VM assertion covers the whole row.

| Required parity row | Minimum acceptance evidence |
|---|---|
| Route/action reachable | From a fresh process, navigate by rail/palette/activation as applicable; verify the intended rendered page/control and truthful command action, not just a VM call. |
| Normal user journey | Same result as repaired legacy behavior or a documented product-approved correction; compare outputs/metadata, not only status text. |
| Failure/negative behavior | Invalid/corrupt input, denied permission, cancellation, collision, locked destination, unavailable device/dependency, bounded resource exhaustion; no false success. |
| Offline/unavailable | App starts with network/model/hardware absent; feature reports actionable unavailable state; no silent download, socket or model initialization. |
| Persistence compatibility | Old data reads; backup/dry-run/validation works; schema and actual path variants handled; single writer after cutover. Mark N/A with evidence, not an empty cell. |
| Source/destination integrity | Hash selected source and pre-existing destination before/after success/failure as appropriate; verify atomic/recoverable publication and output reopen. |
| Keyboard/accessibility | Focus order, shortcut/Enter/Escape, accessible names/status, 100–200% scale, high contrast and at least one executing UI automation plus manual visual check where needed. |
| Startup/shutdown | Fresh/cold launch, repeated open/close, cancellation and exactly-once resource disposal; no orphan process, device handle, temp file or listener. |
| Background side effects | Observe network listeners/traffic, child processes, model/device initialization, CPU and file writes at idle and feature use. Non-owning hosts remain quiet. |
| Performance/footprint | Record published/installed bytes, native duplication, cold/warm startup, idle/feature/peak working set on the named hardware class; regressions explicitly reviewed. |
| Rollback | Stop new writer, restore prior data/route, reopen in legacy host, show preserved backup and define failure escalation. Do not delete new user output to disguise a failed migration. |
| Git/scope | Exact changed files and approved phase; no protected MaterialUI/W5/global/ZIP mutations unless separately authorized; staged/commit/push state reported accurately. |

Every test result is one of PASS, FAIL, ENVIRONMENT-NOT-AVAILABLE, NOT-RUN or BLOCKED with reason. A constant `Assert(true)`, UI “or true” branch, or `1726/1726` historical label is not evidence for a feature-specific gate. The test manifest must reject missing groups and bound native waits. Per-slice parity can be accepted before full V0-RV, but a release/V0-complete claim cannot.

## 14. Exact new-host creation gate

**Studio-H0 may create `Axora.Studio.csproj` only when:** R4 is explicitly accepted after P3A evidence; R1A produces a current, bounded and complete baseline disposition; R1B startup/disposal is accepted; `Axora.Studio.exe` identity, app-owned settings root and no-LAN/no-Tools-service list are frozen; R2D voice/Flashcards prerequisites have a verified plan and, before *feature cutover*, accepted repair evidence; the Scholar→Flashcards handoff/fallback and export parity contract is approved; rollback and exact Git file scope are authorized. A shell-only H0 can precede completion of R2D if it registers **no** voice/Flashcards service and cannot be mistaken for feature parity; M1 cannot.

**Tools-H0 may create `Axora.Tools.csproj` only when:** the common R4/R1A/R1B gate is accepted; identity and Tools-local settings root are frozen with LAN **off**; the H0 scope explicitly excludes P2P/extension/Vault/Converter/Compressor feature routes until repaired; R2A publisher is accepted before the publication canary is enabled; isolated canary/rollback/idle-no-listener tests and exact file scope are approved. A bare shell without the canary could be created after common foundation but would not satisfy the recommended first Tools slice. The old `Axora.Desktop.exe` remains runnable/reference; new host builds must not inherit a broad force-kill target.

**Mind-H0 has its additional section 11 gate** and is not prerequisite for either host. No host creation is authorized by this P2 document. A future host must not import the entire `App.xaml.cs` registration block, a shared settings singleton, or the old `KillRunningAppBeforeBuild` target without per-host lifecycle and identity review.

## 15. Refined phase roadmap and dependency checkpoints

Identifiers below are **proposed execution contracts**, not a renumbering of historical W phases or an automatic authorization. Each implementation phase needs an approved scope, baseline, test/rollback plan and separate Git instruction. Studio and Tools tracks can interleave after common foundation, but conflicting edits to legacy `App.xaml.cs` and test `Program.cs` are serialized.

| Phase | Bounded objective | Exit gate and dependency |
|---|---|---|
| **SUITE-P3A / R1A bootstrap + R4 closure** | Test-only manifest/group ledger, bounded native W4 environment operations, honest unavailable vs pass, isolated settings, R4 root/nested/VS recheck. Do not alter SDK pin or production. | No indefinite broad run; complete current summary/disposition; user explicitly accepts or rejects R4. **Next recommended authorization.** |
| **SUITE-P3B / R1B** | Legacy fatal startup, host/resource ownership and graceful shutdown repair. | Failure cannot continue as healthy; exactly one disposer; no surprise background startup. |
| **STUDIO-R0 / R2D** | Legacy voice/transcriber/playback/Flashcards coordination; bounded real-hardware verification. | No fake listening, early completion, stale callback or direct Flashcards speech path. No new host yet. |
| **STUDIO-H0** | Create only Studio shell/identity, app-owned settings and minimal truthful route; no Scholar/Tools startup. | Independent launch/close, no listener/model eager load; exact host gate. |
| **STUDIO-M1** | Flashcards page/deck/speech/export migration plus consented Scholar handoff or explicit temporary fallback. | Full section 13 parity and rollback; old route is not stranded. |
| **STUDIO-M2** | Resume dashboard/editor, validated data handoff and exports. | Backup/read/dry-run/single-writer/cutover/rollback; two-route UI and ATS/PDF truth. |
| **TOOLS-R0 / R2A** | Legacy Converter+Compressor safe single-file publication; independently review Compressor format/archive gap. | Prior destination survives failures, output reopens, capability labels truthful. R1C is independent unless a shared process adapter is explicitly introduced. |
| **TOOLS-H0** | Tools shell/settings with LAN off and an isolated repaired-publisher canary; no unsafe feature cutover. | Per-host run/close, no P2P/installer startup; section 14 gate. |
| **TOOLS-M1** | Compressor **only after** approved format/resource review and R2A. | Format/extension match, old output preserved, mixed-format/negative/UI parity. |
| **TOOLS-M2** | Converter orchestrator/engines/queue after R2A and real-format parity. | Collision/source integrity, cancellation, metadata/truth matrix. |
| **TOOLS-R1 / R1C→R2B** | Extension trust/process repair, then Batch capability/memory/collision repair. | No unverified executable; known-good survives; bounded ImageMagick/memory. Migrations follow each gate. |
| **STUDIO-R1 / R2C→M3** | Scholar generation/path/background-task repair, then staged Scholar migration. | Verified prior index generation and one writer; full import/search/voice/OCR fallback. |
| **TOOLS-R2 / R1E and R1D** | Vault compatibility/safe output; separately P2P private session/authenticated transfer. | Only then Vault/Mobile Link migrations; Tools LAN opt-in/off-by-default remains enforced. |
| **Legacy R2E + suite R3/RV** | Finish mixed legacy settings/palette repairs, semantic UI/test evidence and historical re-verification across mandatory findings. | V0 may be called accepted only on complete evidence; no unfinished product-specific defect hidden by host split. |
| **W5-P2 then W5-A–F, separately** | Studio single-image codec feasibility after V0-RV and Studio host readiness. | Protected W5 plan gates; Tools Batch is distinct; no implementation in P2. |
| **MIND-H0, separately** | Thin Mind only after section 11 gate. | Independent offline/privacy/credential/footprint contract; no Scholar service move. |

P3/P4/P5/P6/P7 measurement gates from P0/P1 remain: published and installed bytes, native duplication, cold/warm start, idle/feature/peak working set, background CPU/network and graceful shutdown on a representative 16 GB integrated-GPU device. Observe absent optional model/dependency states. Do not multiply the old 166.6 MiB Debug directory to predict distribution cost.

## 16. Future local commit boundaries — none authorized now

Each future phase begins with a reviewed dirty-worktree inventory and explicit user authorization for files and local commit. Suggested one-purpose boundaries:

1. Toolchain-pin acceptance record (and any separately authorized correction) **separate** from test edits.
2. R1A test manifest/timeout/disposition tests, with no production, host or data migration bundled.
3. R1B legacy startup/shutdown ownership, with focused lifecycle tests.
4. R2D voice/Flashcards repair, with real/mocked voice evidence; no Studio host in same commit.
5. Studio H0 identity/bootstrap/settings skeleton; then M1 Flashcards; then its legacy Scholar handoff integration as an explicit reviewed sub-boundary if needed.
6. Resume schema/backup/read migration tooling on copies **separate** from Resume UI/feature cutover.
7. R2A safe publisher and legacy Converter/Compressor integration **separate** from Tools H0 and Compressor migration.
8. R1C executable trust, R1D P2P protocol and R1E Vault security **separate from each other** and from any new host/feature/data migration.
9. R2C Scholar generation repair, Scholar data dry-run/import and Scholar UI cutover as separate reversible boundaries.
10. Per-slice parity/evidence documentation after executing tests, with the exact accepted code state referenced; release/packaging changes only after measured hosts.

Do not stage, commit, push, PR, merge or rebase from this contract. An existing untracked artifact never becomes accepted or committed by implication. Do not rewrite history to conceal the R4 attempt or the unknown ZIP.

## 17. Risk register and stop conditions

| Risk | Severity and owner | Stop/mitigation trigger |
|---|---|---|
| R4 accepted from partial count | HIGH / SUITE | Stop if suite lacks final group/exit ledger, native operation timeout or user acceptance. CLI SDK resolution alone is narrower evidence. |
| Native voice stress indefinite wait | HIGH / STUDIO/R1A | T4-04 must report per-iteration await and timeout/unavailable state; exact WinRT wait remains unknown. No new uncontrolled microphone replay. |
| Scholar→Flashcards direct global VM call | HIGH / STUDIO | Stop Flashcards cutover if legacy Scholar “Push to Flashcards” loses user content or truth. Scoped handoff/fallback needs explicit consent and tests. |
| Flashcards unsafe direct export/misleading sample text | MODERATE / STUDIO | Stop if prior destination can be truncated on failure or UI continues to claim unproven AI/neural/WebSocket behavior. |
| Compressor wrong extension and unbounded archive work | HIGH / TOOLS | Stop M1 until file signatures/output extension, archive expansion/memory limits and result messages are verified on real fixtures. R2A alone is insufficient. |
| Premature Shared project | MODERATE / SUITE | Stop if a project is justified only by future hypothetical use or pulls native/model/security packages into unrelated hosts. |
| Dual writers to old settings/Resume/Scholar/Vault/cache | VERY HIGH / owning app | Stop cutover without backup, validated dry run, one-writer switch and reversible old reader. |
| P2P autostart copied to Tools/Studio/Mind | VERY HIGH / TOOLS | Future Tools must default off; Studio/Mind must have no listener registration. Verify idle sockets, not just config text. |
| Extension executable or Vault key copied before repair | VERY HIGH / TOOLS | Require R1C/R1E trust/format/compatibility gates before feature migration. |
| Unknown ZIP normalized/deleted/used as release evidence | HIGH / SUITE | Provenance remains unknown; leave untouched/untracked and exclude from artifact/size claims. |
| Three-host native runtime/disk/memory growth | MODERATE / SUITE | Measure real published/installed/startup/working set before packaging redesign or release claim. |
| Incomplete UI/test proof | HIGH / SUITE | No slice cutover on compilation, forced-pass assertion or non-executing palette test; per-slice semantic evidence required. |

## 18. Exact next implementation authorization recommendation

Recommend the user **separately authorize only `SUITE-P3A`**, a test-infrastructure/R4-acceptance bootstrap—not Studio/Tools creation. Proposed scope: read-only baseline first; test project `Program.cs` and `W4_VoiceSubsystemTests.cs` plus narrowly necessary QA runner/manifest files; instrument T4-04 by await/iteration with explicit per-operation and whole-suite timeouts; isolate `APPDATA` to a test-owned directory; distinguish deterministic assertions, physical-hardware observations, unavailable prerequisites and skipped groups; reject forced passes; record exact process exit and full summary. Do **not** change `global.json`, production services, user audio/settings/data, protected plans, MaterialUI or ZIP in that phase. Any real microphone replay needs its own short bounded runtime contract and safe environment/user approval; test-only static manifest work can proceed first. The phase ends with an evidence-backed **user decision** on accepting the attempted R4 pin. No automatic progression to R1B.

This is the smallest next action that resolves the dependency on trustworthy evidence. Starting R2D, host creation, W5-P2 or archive deletion while the runner can silently wait indefinitely would bypass the agreed foundation gate. If a future diagnostic proves a production WinRT defect rather than only a test timeout gap, the authorized R2D scope must incorporate that finding before Flashcards migration.

## 19. Explicit defer and exclusion list

- No new `Axora.Studio`, `Axora.Tools`, `Axora.Mind` project, executable, solution entry, installer, packaging switch or shell verb in P2.
- No V0-R4 acceptance by this document, no R1A/R1B/R2D/R2A implementation, no W5-P2, no R5 debt cleanup, and no MaterialUI synchronization.
- No migration of settings, Resume, Scholar, Vault, extension/cache or QuickDrop data; no deletion or extraction of the unknown ZIP.
- No suite-wide live settings DB, telemetry-consent inheritance, shared app shell, universal document/raster engine, always-running IPC broker or pre-created shared projects.
- No Mind provider/model/runtime, AI image generation, integrity/plagiarism verdict, Project/ZIP Analysis, or SR-01–SR-08 recovery implementation from this plan alone.
- No claim that 1,685 passes, the historical 1,726 pass, a Debug directory size or a central-directory parse establishes current release readiness.

## 20. Open user decisions and P2 exit

1. Accept the proposed **P3A-only** next implementation authorization and the scheduling refinement that allows Studio R2D/H0 to proceed without unrelated Tools repairs or all legacy R2E, once shared foundation is accepted.
2. Approve a scoped, consented legacy Scholar→Studio Flashcards handoff/fallback contract before M1 cutover; if declined, revisit the first Studio feature rather than weakening parity.
3. Confirm `TOOLS-H0` shell + isolated publisher canary as the first Tools host slice and Compressor as the first *later* user-facing Tools feature only after the newly found format/archive gates.
4. Decide whether the unknown ZIP should remain indefinitely excluded or receive a separately authorized deeper forensic/archival decision. Its creator cannot be established from current metadata.
5. Decide the rollback-window duration, backup placement and user messaging for each future persistent domain; no duration is invented here.
6. Later, after functional parity and measurements, choose packaged/unpackaged distribution and optional visual synchronization. Those decisions do not block P3A.

**P2 exit:** Accepted product identities, settings/LAN/shared/packaging decisions are translated into bounded gates; R4 is technically plausible but unaccepted, test-hang locus is high-confidence with unknown exact native await, and ZIP provenance remains unknown but safely bounded. This single document is the only P2 artifact. Stop for user review; do not begin P3 or modify the legacy host.
