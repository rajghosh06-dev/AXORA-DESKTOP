# AXORA Desktop V0 Remediation Execution Plan

Status: planning only  
Baseline branch: main  
Baseline HEAD: 75ca36ad12f9c64fc5ff88b549d1aece313353c2  
Plan identifier: V0-RP  
Implementation authorization: not granted

## 1. Purpose and non-negotiable boundary

This document turns the historical V0 audit into an implementation-ready sequence. It does not authorize any repair. During V0-RP no production code, tests, XAML, dependency, SDK pin, W5 document, or MaterialUI file may change. Every future wave requires explicit user authorization, an understood Git baseline, and its own pre-fix evidence.

The plan applies the AXORA “user problem first” law: every correction must remove a demonstrated user risk, preserve local/offline behavior where applicable, expose unavailable capabilities honestly, avoid unnecessary dependencies, verify its result, and leave a recovery path.

No section below claims perfection, universal atomicity, zero regressions, hardware-backed security, or complete format support.

## 2. Repository baseline

At planning start:

- Branch: main.
- HEAD: 75ca36ad12f9c64fc5ff88b549d1aece313353c2.
- Staged files: none.
- Tracked modifications: none.
- MaterialUI: clean and out of scope.
- Pre-existing untracked files: the four W5-P1 planning documents.
- V0-RP output: this document only, left untracked and unstaged.

The four protected W5 files are:

- docs/W5_IMAGE_STUDIO_ARCHITECTURE.md
- docs/W5_IMAGE_STUDIO_PRODUCT_CONTRACT.md
- docs/W5_IMAGE_STUDIO_RULE_MATRIX.md
- docs/W5_IMAGE_STUDIO_VERIFICATION_PLAN.md

## 3. Revalidated finding ledger

Classification meanings:

- PATCH: a narrow correction with no architectural ownership change.
- HARDEN: retain the component but add mandatory safety/validation.
- LOCAL REFACTOR: change ownership or flow inside a bounded subsystem.
- COMPONENT REPLACEMENT: replace an unsafe internal mechanism behind a stable or additive contract.
- DEFER: real but non-blocking work, excluded from V0 completion unless separately authorized.
- DOCUMENTATION CORRECTION: correct claims or user wording without pretending capability exists.
- FALSE POSITIVE: no change; evidence disproves the finding.

| Finding | Revalidation | Strategy | Wave | Evidence/root issue |
|---|---|---|---|---|
| V0-TCH-001 | CONFIRMED | PATCH | V0-R4 | Nested global.json uses a non-SDK feature-band version and root commands escape the pin. |
| V0-TEST-001 | CONFIRMED | COMPONENT REPLACEMENT + HARDEN | V0-R1A, V0-R3 | The console harness has no execution ledger; literal Assert(true) and forced-pass UI branches are counted as evidence. |
| V0-FND-001 | CONFIRMED | PATCH + HARDEN | V0-R1B | initialization can log and continue; the global handler marks unexpected failures handled. |
| V0-FND-002 | CONFIRMED | LOCAL REFACTOR | V0-R1B | App, host, VoiceCoordinator, TrayService, and injected services have overlapping disposal paths. |
| V0-W15-001 | CONFIRMED | COMPONENT REPLACEMENT + HARDEN | V0-R1C | downloaded executable installation has no mandatory, version-bound trust gate. |
| V0-W15-002 | CONFIRMED | COMPONENT REPLACEMENT + LOCAL REFACTOR | V0-R1C | repair/reinstall can remove the known-good install first; process I/O and timeout handling are unsafe. |
| V0-P2P-001 | CONFIRMED and broader than first stated | COMPONENT REPLACEMENT | V0-R1D | outbound broadcast passes an empty key, receive assumes one message, the plaintext LAN beacon discloses the reusable pairing token, peer keys lack private session ownership, and UI completion/disconnect/device state is fictitious or incomplete. |
| V0-W2-001 | CONFIRMED | COMPONENT REPLACEMENT | V0-R2A | overwrite deletes the destination before committing validated staged output. |
| V0-CMP-001 | CONFIRMED | COMPONENT REPLACEMENT | V0-R2A | compressor output publication has the same destination-safety class and should use the shared primitive. |
| V0-VLT-001 | CONFIRMED | LOCAL REFACTOR + HARDEN + DOCUMENTATION CORRECTION | V0-R1E | selected KDF settings are ignored, direct destination creation is unsafe, and TPM/shred wording overstates guarantees. |
| V0-BAT-001 | CONFIRMED | LOCAL REFACTOR + HARDEN | V0-R2B | WIC fallback can silently omit requested quality, target-size, watermark, or metadata behavior. |
| V0-BAT-002 | CONFIRMED | COMPONENT REPLACEMENT + HARDEN | V0-R2B | collisions, decoded-memory concurrency, and external-process lifecycle lack bounded contracts. |
| V0-W3-001 | CONFIRMED | COMPONENT REPLACEMENT | V0-R2C | two-file publication is not one commit, rebuild deletes the last index before replacement, and the manifest hash is not sufficiently bound to the mapped payload. |
| V0-FLS-001 | CONFIRMED | LOCAL REFACTOR | V0-R2D | Flashcards invokes speech directly and bypasses shared recognition/playback coordination. |
| V0-W4-001 | CONFIRMED | LOCAL REFACTOR | V0-R2D | command mode does not own a production transcriber-to-router lifecycle. |
| V0-W4-002 | CONFIRMED | COMPONENT REPLACEMENT + HARDEN | V0-R2D, V0-R2E | playback completes too early, media lifetime is unsafe, work is fire-and-forget, and desired settings are not synchronized with actual state. |
| V0-NAV-001 | CONFIRMED and broader than first stated | PATCH + LOCAL REFACTOR | V0-R2E | seven palette actions are dead, Universal Converter is missing, and the palette advertises Up/Down/Enter behavior that is not wired. |
| V0-SET-001 | CONFIRMED | COMPONENT REPLACEMENT + HARDEN | V0-R2E | settings use direct unversioned writes; recovery/validation and W4 controls are incomplete; voice enumeration and device updates are not safely initialized/dispatched. |
| V0-W2-002 | RECLASSIFIED: non-blocking | DEFER | V0-R5 or later W5 proof work | metadata, multi-frame TIFF, alpha, and deeper reopen coverage are capability-depth work, not a reason to destabilize the V0 publisher fix. |
| V0-W3-002 | CONFIRMED companion hardening | HARDEN | V0-R2C | background/transient indexing is discarded or can replace live state before completion. It is not independently release-blocking, but the same Scholar lifecycle wave must not preserve it. |
| V0-W3F-001 | CONFIRMED, non-blocking | DOCUMENTATION CORRECTION | V0-R5 | current feature should be described as local extractive study synthesis until a model-backed capability exists. |
| V0-W4-003 | RECLASSIFIED: dormant | DEFER | V0-R5/future | confirmation-broker infrastructure is unnecessary until a registered command actually requires confirmation. |
| V0-UI-001 | CONFIRMED verification gap | HARDEN | V0-R3, V0-RV | visual/manual evidence must complement, not replace, semantic automation. |
| V0-BLD-001 | RECLASSIFIED: non-blocking | DEFER | V0-R5 | four obsolete Skia warnings and one unused Scholar field are debt, not current correctness blockers. |

No accepted FIX BEFORE W5 item is classified as a false positive or left without a wave.

## 4. Root-cause map

| Root cause | Findings | Corrective principle |
|---|---|---|
| Failure is converted to apparent success | V0-FND-001, V0-BAT-001, V0-TEST-001 | fail closed at contract boundaries; distinguish failures, observations, and gaps. |
| Ownership is implicit | V0-FND-002, V0-W4-001, V0-W4-002, V0-FLS-001, V0-W3-002 | one lifecycle owner per resource; DI owns injected disposable services. |
| Multi-step mutation lacks a commit point | V0-W15-002, V0-W2-001, V0-CMP-001, V0-VLT-001, V0-W3-001, V0-SET-001 | stage, validate, flush, commit, and recover; never destroy known-good state first. |
| Trust is inferred from transport or filename | V0-W15-001 | bind catalog metadata to exact bytes and verified publisher identity before execution. |
| Protocol state is not represented as a session | V0-P2P-001 | private session object owns directional keys, socket, counters, locks, limits, transfer acknowledgements, and cancellation; discovery never carries a pairing secret. |
| Requested capability is not negotiated | V0-BAT-001, V0-BAT-002 | explicit preflight result; unsupported work fails actionably rather than silently degrading. |
| Persistence has no schema/recovery contract | V0-SET-001, V0-VLT-001, V0-W3-001, V0-W3-002 | version records, retain legacy readers, preserve live state until replacement verifies, quarantine corruption, and migrate only after verified success. |
| Navigation/action catalogs drift | V0-NAV-001 | derive palette navigation from one typed route catalog; hide incomplete actions. |
| Environment/test entry points drift | V0-TCH-001, V0-TEST-001, V0-UI-001 | one repository SDK pin, one explicit test manifest, semantic UI assertions. |

## 5. Dependency graph and optimal order

The wave numbering describes subject areas, not execution order. The safe dependency order is:

    V0-R4 toolchain pin
      -> V0-R1A trustworthy test harness
         -> V0-R1B lifecycle/exception ownership
         -> V0-R1C extension trust + process runner
         -> V0-R2A single-file publication + Converter + Compressor
              -> V0-R1D P2P protocol/session repair
              -> V0-R1E Vault
              -> V0-R2C Scholar generation protocol
              -> V0-R2E settings publication
         -> V0-R2D voice/speech/Flashcards
              -> V0-R2E navigation/settings UI
         V0-R1C + V0-R2A -> V0-R2B Batch
      all mandatory repairs -> V0-R3 evidence/UI hardening
      V0-R3 -> V0-RV historical re-verification
      V0-RV -> V0-R5 optional cleanup, only if separately selected

Linear execution:

1. V0-R4.
2. V0-R1A.
3. V0-R1B.
4. V0-R1C.
5. V0-R2A.
6. V0-R1D.
7. V0-R1E.
8. V0-R2B.
9. V0-R2C.
10. V0-R2D.
11. V0-R2E.
12. V0-R3.
13. V0-RV.
14. V0-R5 only after V0 acceptance and separate authorization.

Parallel implementation is not recommended in this repository because the shared test Program.cs and App.xaml.cs are convergence points. Independent analysis may run in parallel, but each implementation wave should start from the accepted prior-wave baseline.

## 6. Shared infrastructure decisions

### 6.1 Safe single-file publication

Create a small, internal IFilePublicationService used initially by Converter and Compressor, then P2P received files, Vault, Batch, Scholar’s single active-pointer file, and settings in dependency order. It is not a general transaction framework and must not be used to describe Scholar’s binary-plus-manifest pair as atomic.

Required contract:

1. Normalize source and destination; reject source equals destination.
2. Allocate an unpredictable sibling staging file in the destination directory so the final move is same-volume.
3. Let the producer write only the staging path.
4. Close the producer, flush durable file content, and run format/domain validation before commit.
5. Re-check collision state immediately before commit.
6. CreateNew commits with a non-overwriting move.
7. ReplaceExisting prefers File.Replace with a recovery backup when the platform/filesystem supports it.
8. A fallback backup/move/restore sequence reports its weaker semantics explicitly and must never be marketed as universally atomic.
9. Cancellation is honored before commit. After the commit point the result reports Published even if cancellation arrives; it must not claim cancellation while leaving a published file.
10. Post-commit validation may restore the backup if the consumer requires it.
11. Owned staging and backup files are cleaned; unrelated user files are never swept.
12. Results identify Published, Collision, ValidationFailed, Restored, RecoveryRequired, or Failed, with no false success.

Interface changes are additive. Existing public orchestrator calls may delegate internally until consumers can migrate without a flag day.

### 6.2 External process execution

Create IExternalProcessRunner and ExternalProcessRunner. It must use ProcessStartInfo.ArgumentList, asynchronously drain stdout and stderr, combine caller cancellation with an explicit timeout, kill the process tree on timeout/cancellation, wait for exit, cap retained diagnostic output, and return a typed result containing exit code, timeout/cancellation state, duration, and output. It must not construct a shell command or rely on reading one redirected stream before WaitForExit.

VersionDetector, extension installation/validation, and Batch ImageMagick execution become consumers. A five-minute operation timeout is a starting policy, not an unbounded default; fast probes use a shorter explicit timeout.

### 6.3 Extension artifact trust

HTTPS is transport protection, not artifact identity. Installation requires version-specific trusted metadata established from an authoritative vendor release:

- expected SHA-256 for the exact artifact;
- expected architecture and package kind;
- for executable installers, a successful Windows Authenticode/WinVerifyTrust result and exact expected publisher identity;
- a verified immutable artifact descriptor passed to the installer.

No hash or publisher may be invented. If authoritative metadata cannot be established, installation is disabled with an actionable message while detection of an already installed dependency remains available.

The pipeline is: download to .part, flush, hash and signature/architecture validate, promote to verified cache, install/extract to a new stage, validate installed stage, swap while retaining a known-good backup, then clean up. Reinstall and repair must never uninstall the working version before replacement validation. Prefer portable/staged distribution where officially available; any system-wide installer path must be explicit and recoverable.

### 6.4 DI and shutdown ownership

Microsoft.Extensions.Hosting owns and disposes registered singleton/transient services. App coordinates shutdown but does not dispose injected services independently.

- App may request operational shutdown: stop accepting P2P work, stop recognition/playback, remove the tray icon, then stop/dispose the host.
- VoiceCoordinator disposes only private state it creates; it unsubscribes from injected service events but does not dispose those services.
- TrayService releases its native resources in its own Dispose; App calls an idempotent Remove only when needed for visible state.
- InitializeComponent failure is logged and rethrown.
- the global unexpected-exception handler logs context and does not blanket-set Handled. Recoverable user-operation failures are handled at their local boundary.

### 6.5 Voice coordination

VoiceCoordinator is the single high-level owner of dictation mode, command-listening mode, and speech sequencing. VoiceTranscriber owns recognition resources. VoiceCommandRouter owns command metadata, matching, safety classification, and dispatch only. SpeechSynthesisService owns one playback operation.

Only one recognition mode may be active. Speaking suspends recognition, completes on MediaEnded/failure/cancellation, then resumes the desired mode after a short debounce. A per-session generation and CancellationTokenSource rejects stale callbacks. UI actions marshal through DispatcherHelper; a failed TryEnqueue must fault/complete the returned task instead of hanging.

### 6.6 P2P protocol

Retain the stronger intended encrypted design and make the implementation satisfy it. A private P2pPeerSession owns WebSocket, directional 32-byte AES keys, send lock, cancellation, session identifier, send/receive sequence counters, connection task, and active transfer state. AxoraDevice contains secret-free display metadata only.

The pairing QR becomes a versioned, finite-lived offer containing an offer ID, ephemeral server public key, and 32 random secret bytes. The secret is atomically claimable once and rotated after success, expiry, or explicit regeneration. The existing JSON UDP transmission is honestly called an AXORA LAN discovery beacon unless standards-conformant DNS-SD is implemented; it may advertise service/version/endpoint/offer ID but never the pairing secret. The client proves possession with an HMAC over the bounded handshake rather than transmitting the secret. HKDF context derives distinct client-to-server and server-to-client keys.

A frame has authenticated header fields for magic, protocol version, message type, flags, session ID, sequence, nonce, and ciphertext length; the header is AES-GCM additional authenticated data. Nonces are unique per directional key and sequence. Missing-key, wrong-session, non-contiguous/replayed sequence, bad-tag, unknown-required-type, and over-limit frames are rejected; no raw fallback exists.

Limits:

- pairing/handshake payload: 4 KiB;
- metadata/control payload: 64 KiB;
- encrypted file chunk: 256 KiB;
- file default maximum: 4 GiB, configurable downward and checked against free disk space.

Handshake, upgrade, and receive loops assemble fragments until EndOfMessage with bounded length. File transfer uses Offer, Accept/Reject, ordered FileChunk, Complete, verified publication, and authenticated ACK carrying transfer ID, sanitized UTF-8 filename, total size, offsets, and final SHA-256. Receive writes an owned sibling .part file, flushes it, verifies length and hash, then collision-safely publishes it. Failure/cancellation removes only that transfer’s partial. Outgoing success requires at least one real selected peer and its authenticated ACK; zero peers is failure. BroadcastAsync must not remain a raw public file transport; add typed SendFileAsync, DisconnectAsync, and typed control-message methods.

Start options must consume the validated configured port, download directory, automatic-start, and background QuickDrop preference; the UI displays the actual bound endpoint. BackgroundQuickDropListen=false rejects incoming file offers without fabricating a connection failure. Remove the hard-coded Pixel device, make Disconnect close the real session, and replace the unsafe timed SemaphoreSlim count getter with an immutable/interlocked session snapshot. If Axora Mobile compatibility cannot be confirmed, the wave stops before claiming interoperable release.

### 6.7 Scholar generation publication

Use immutable generation directories:

    indexes/<document-id>/generations/<generation-id>/vectors.bin
    indexes/<document-id>/generations/<generation-id>/manifest.json
    indexes/<document-id>/active.json

The writer completely produces and verifies a generation, including document/model/schema/count/dimension and payload SHA-256, before committing active.json as one small safe-publication pointer. Rebuild never deletes the current generation first. The reader snapshots active.json, validates identifiers and the manifest-to-binary hash/count/dimension binding, and rejects/quarantines inconsistency. This is a generation protocol, not a claim that two files are atomically replaced together.

Legacy schema-v1 indexes remain readable and immediately gain the missing manifest-to-binary hash comparison. The next successful rebuild writes v2 and moves the active pointer only after verification. Retain the prior active generation until readers release mappings; perform bounded owned-generation cleanup later. Transient DocumentChat indexing builds a local immutable passage snapshot and publishes it only after every embedding succeeds; tracked/debounced indexing is awaited or exposed as a real indexing state, and failure/cancellation preserves the prior live snapshot.

### 6.8 Test completeness

The console suite uses an explicit ordered manifest of test-group IDs and delegates plus a TestRunLedger. A successful run proves every registered group started and completed exactly once, no duplicate ID exists, assertion deltas were recorded, and no unregistered legacy invocation remains.

Report three separate quantities:

- deterministic/integration assertions;
- environment observations;
- manual or unavailable evidence gaps.

Literal Assert(true), unconditional pass records, and UI “or true” constructs are forbidden as functional evidence. Historical counts (1,726 total, 1,637 deterministic, 89 environment-dependent) are baseline metadata, not hardcoded success requirements.

## 7. Wave mini-contracts

Every contract below inherits these universal boundaries:

- MaterialUI MUST NOT CHANGE.
- The four W5-P1 files MUST NOT CHANGE.
- No new dependency may be added unless the wave stops and the user separately approves a demonstrated need.
- No wave may stage, commit, push, or open a PR without explicit user authorization.

### V0-R4 — Toolchain reproducibility

- **Wave ID:** V0-R4.
- **Objective:** make every repository entry point select the same supported .NET 9 SDK feature band before repair evidence is generated.
- **Accepted findings addressed:** V0-TCH-001.
- **Exact intended production files:** none.
- **Exact intended test/build files:** Axora-Desktop-WinUI/global.json (remove/move); new repository-root global.json; scripts/qa/build-all.ps1 only if it bypasses repository discovery.
- **New files:** /global.json with version 9.0.300, rollForward latestPatch, allowPrerelease false.
- **Behavior before:** repository-root dotnet resolves 10.0.401; WinUI-directory resolution falls back to installed 9.0.318 from an invalid/non-feature-band 9.0.0 request.
- **Behavior after:** root and nested invocations resolve an installed 9.0.3xx SDK, presently 9.0.318, while allowing security patches in that feature band.
- **Invariants:** target framework and Windows App SDK versions stay unchanged; Visual Studio MSBuild remains supported.
- **Failure behavior:** if SDK resolution differs by working directory or required SDK is missing, stop; do not install or mutate workloads automatically.
- **Compatibility requirements:** command-line and authoritative Visual Studio MSBuild builds must agree; MaterialUI tooling is unaffected.
- **Security implications:** prevents accidental builds with an unreviewed major SDK; no network/install action.
- **Persistence/migration implications:** none.
- **Concurrency implications:** none.
- **UI implications:** none.
- **Test plan:** assert dotnet --version and dotnet --info at root and Axora-Desktop-WinUI; run clean Debug x64 build.
- **Runtime verification:** launch packaged/unpackaged app only after build succeeds.
- **Full regression requirement:** authoritative WinUI build and existing suite under the resolved SDK.
- **Rollback/recovery:** restore the nested pin and remove root pin as one Git reversal if VS/MSBuild incompatibility is proven.
- **Explicit exclusions:** SDK installation, target-framework upgrade, Windows App SDK upgrade.
- **MUST CHANGE:** SDK pin location and valid feature-band value.
- **MAY CHANGE IF REQUIRED:** build script working directory or diagnostic output.
- **MUST NOT CHANGE:** product code, tests other than build-selection checks, XAML, MaterialUI, W5 files.
- **Stop condition:** root/nested SDK disagreement, missing compatible SDK, or authoritative build regression.

### V0-R1A — Trustworthy test manifest

- **Wave ID:** V0-R1A.
- **Objective:** make later remediation evidence complete, non-tautological, and countable.
- **Accepted findings addressed:** V0-TEST-001 foundation; prepares V0-UI-001.
- **Exact intended production files:** none.
- **Exact intended test files:** Axora-Desktop-WinUI/Axora.Desktop.Tests/Program.cs; Axora-Desktop-WinUI/Axora.Desktop.Tests/W4_VoiceSubsystemTests.cs; scripts/qa/run-tests.ps1.
- **New files:** Axora-Desktop-WinUI/Axora.Desktop.Tests/TestInfrastructure/TestGroupDefinition.cs; Axora-Desktop-WinUI/Axora.Desktop.Tests/TestInfrastructure/TestRunLedger.cs.
- **Behavior before:** groups are manually invoked in a monolithic global try; tautological contract assertions can inflate passing counts; missing groups are not detected.
- **Behavior after:** one manifest drives execution and a ledger proves unique start/completion; assertions, observations, and gaps are separate.
- **Invariants:** existing meaningful tests and historical result metadata remain attributable; one group failure does not hide which groups ran.
- **Failure behavior:** duplicate/missing/uncompleted groups, zero expected assertion delta where prohibited, or a test exception fails the run.
- **Compatibility requirements:** test executable remains runnable by current QA scripts; no external framework dependency is introduced.
- **Security implications:** reduces false assurance for security-sensitive repairs.
- **Persistence/migration implications:** none.
- **Concurrency implications:** manifest runs deterministically unless a group explicitly documents isolation; shared global test state is reset per group.
- **UI implications:** none yet.
- **Test plan:** self-test duplicate IDs, thrown groups, skipped completion, observations, and gap accounting; map every existing invocation into the manifest.
- **Runtime verification:** execute the test binary and inspect summary plus process exit code.
- **Full regression requirement:** all meaningful pre-existing groups execute once; changed count is explained rather than forced to match 1,726/1,637/89.
- **Rollback/recovery:** preserve old group delegates during migration; revert the manifest commit if completeness cannot be proven.
- **Explicit exclusions:** production repairs and PowerShell UI semantics (V0-R3).
- **MUST CHANGE:** Program runner/accounting and tautological voice-rule evidence.
- **MAY CHANGE IF REQUIRED:** run-tests.ps1 parsing and test-only helper organization.
- **MUST NOT CHANGE:** production code, XAML, MaterialUI, W5 files.
- **Stop condition:** any legacy group cannot be mapped, the ledger can report success after omission, or baseline failures are unexplained.

### V0-R1B — Initialization, exception, and disposal ownership

- **Wave ID:** V0-R1B.
- **Objective:** surface fatal startup failures and give every disposable resource one owner.
- **Accepted findings addressed:** V0-FND-001, V0-FND-002.
- **Exact intended production files:** Axora-Desktop-WinUI/Axora.Desktop/App.xaml.cs; Axora-Desktop-WinUI/Axora.Desktop/Services/VoiceCoordinator.cs; Axora-Desktop-WinUI/Axora.Desktop/Services/Contracts/IVoiceCoordinator.cs; Axora-Desktop-WinUI/Axora.Desktop/Services/TrayService.cs; Axora-Desktop-WinUI/Axora.Desktop/Services/Contracts/ITrayService.cs.
- **Exact intended test files:** Axora-Desktop-WinUI/Axora.Desktop.Tests/Program.cs; Axora-Desktop-WinUI/Axora.Desktop.Tests/W4_VoiceSubsystemTests.cs.
- **New files:** none expected.
- **Behavior before:** InitializeComponent can be logged and continued; unexpected exceptions can be marked handled; App and coordinator dispose DI-owned objects.
- **Behavior after:** initialization failures rethrow; unexpected global exceptions are logged but not blanket-swallowed; host owns injected disposables and operational stop calls are idempotent.
- **Invariants:** recoverable conversion/download/user-operation errors remain locally handled with user feedback; orderly shutdown still removes visible tray/P2P/voice activity.
- **Failure behavior:** fatal startup/unknown UI-thread failure terminates after durable diagnostics; shutdown aggregates/logs cleanup failures without double-disposal.
- **Compatibility requirements:** service registration and public user flows remain stable.
- **Security implications:** avoids continuing in partially initialized security/network states.
- **Persistence/migration implications:** none.
- **Concurrency implications:** one guarded shutdown path; idempotent stop/remove; no disposal while callbacks are still accepted.
- **UI implications:** fatal failures no longer leave a misleading half-working window.
- **Test plan:** injected fakes verify each operational stop and Dispose occurs once; initialization/global exception policy gets deterministic seam-level tests.
- **Runtime verification:** normal launch/close, tray exit, app-window close, and induced non-destructive startup failure in a test configuration.
- **Full regression requirement:** full suite plus launch/close smoke.
- **Rollback/recovery:** one isolated commit; revert if host shutdown order regresses.
- **Explicit exclusions:** voice functional repair, P2P protocol repair, new crash UI.
- **MUST CHANGE:** ownership and exception policy at identified boundaries.
- **MAY CHANGE IF REQUIRED:** narrowly scoped service stop interface for idempotence.
- **MUST NOT CHANGE:** unrelated services/pages, MaterialUI, W5 files.
- **Stop condition:** an injected service still has two disposing owners or a fatal initialization path can continue.

### V0-R1C — Extension trust, staged replacement, and process runner

- **Wave ID:** V0-R1C.
- **Objective:** prevent unverified executable installation and preserve a known-good dependency through install/repair/update.
- **Accepted findings addressed:** V0-W15-001, V0-W15-002.
- **Exact intended production files:** Axora-Desktop-WinUI/Axora.Desktop/App.xaml.cs; Models/ExtensionModel.cs; Models/ExtensionValidationResult.cs; Services/DependencyManager.cs; Services/ExtensionDownloader.cs; Services/ExtensionInstaller.cs; Services/ExtensionRegistry.cs; Services/ExtensionRepairService.cs; Services/ExtensionValidator.cs; Services/VersionDetector.cs; and their existing interfaces under Services/Contracts.
- **Exact intended test files:** Axora-Desktop-WinUI/Axora.Desktop.Tests/Program.cs.
- **New files:** Axora-Desktop-WinUI/Axora.Desktop/Models/ExtensionArtifactTrustPolicy.cs; Models/VerifiedExtensionArtifact.cs; Models/ProcessExecutionResult.cs; Services/Contracts/IExternalProcessRunner.cs; Services/ExternalProcessRunner.cs.
- **Behavior before:** catalog hash may be null; downloader output can reach installer without a mandatory trust token; repair may uninstall first; process streams/timeouts are unsafe.
- **Behavior after:** only a verified descriptor can reach execution; install is stage/validate/swap with backup; all subprocesses use bounded async execution.
- **Invariants:** already-installed usable ImageMagick remains detectable; offline use does not require a download; no unverified bytes execute.
- **Failure behavior:** missing authoritative metadata disables install; hash/signature/publisher/architecture mismatch quarantines the owned artifact and preserves current install; timeout kills the process tree and reports diagnostics.
- **Compatibility requirements:** retain current extension IDs and user-visible state where truthful; probe existing install/cache without deleting it.
- **Security implications:** exact SHA-256 plus Authenticode publisher verification for executable installers; HTTPS alone is insufficient; cache trust is immutable/version-bound.
- **Persistence/migration implications:** existing cache can be revalidated and promoted only after trust succeeds; no automatic purge of user/current install.
- **Concurrency implications:** per-extension mutation lock; process runner drains both streams; cancellation cannot interrupt the critical swap without recovery.
- **UI implications:** install/repair is disabled with a precise reason when trust metadata is unavailable; progress differentiates download, verify, stage, validate, commit, restore.
- **Test plan:** hash/signature/publisher/architecture mismatch, truncated download, timeout, stderr saturation, cancellation, failed validation, failed swap/restore, concurrent repair, existing-install preservation.
- **Runtime verification:** authoritative metadata provenance review; real signed artifact verification; install/detect/use/restart/repair smoke on a disposable staged location.
- **Full regression requirement:** full suite and download-manager UI smoke.
- **Rollback/recovery:** retain known-good directory until post-swap validation; typed RecoveryRequired state identifies backup path; Git revert remains isolated.
- **Explicit exclusions:** inventing a hash, installing dependencies during planning, upgrading ImageMagick without separate product decision, broad package manager.
- **MUST CHANGE:** trust gate, staged replacement, process execution.
- **MAY CHANGE IF REQUIRED:** download-manager state/control text and additive interfaces.
- **MUST NOT CHANGE:** unrelated converter behavior, MaterialUI, W5 files.
- **Stop condition:** official exact artifact metadata/publisher cannot be established, known-good rollback cannot be demonstrated, or any executable path bypasses VerifiedExtensionArtifact.

### V0-R1D — P2P encrypted session and file protocol

- **Wave ID:** V0-R1D.
- **Objective:** make every outbound/inbound message satisfy a bounded, authenticated, replay-resistant session contract.
- **Accepted findings addressed:** V0-P2P-001.
- **Exact intended production files:** Axora-Desktop-WinUI/Axora.Desktop/Services/P2pSyncService.cs; Services/Contracts/IP2pSyncService.cs; Helpers/CryptographyHelper.cs; ViewModels/MobileLinkViewModel.cs; Models/AxoraDevice.cs; Models/QuickDropItem.cs; Views/MobileLinkPage.xaml and .xaml.cs; App.xaml.cs only where V0-R1B owns awaited startup/shutdown.
- **Exact intended test files:** Axora-Desktop-WinUI/Axora.Desktop.Tests/Program.cs; new V0R1D_P2pProtocolTests.cs.
- **New files:** Axora-Desktop-WinUI/Axora.Desktop/Services/P2pPeerSession.cs; Services/P2pProtocolCodec.cs; Services/QuickDropTransferSession.cs; Models/P2pProtocolModels.cs; Axora-Desktop-WinUI/Axora.Desktop.Tests/V0R1D_P2pProtocolTests.cs.
- **Behavior before:** BroadcastAsync can construct a frame with an empty key; fragmented reads assume one message; the reusable pairing token is sent in a plaintext JSON LAN beacon; public device models hold keys; timed count locking is invalid; settings are ignored; a fake Pixel appears; disconnect is UI-only; whole files are loaded in memory and marked complete with zero peers/no ACK.
- **Behavior after:** peer-private sessions encrypt/authenticate every typed frame with directional keys, enforce sequence/size limits, assemble fragments, honor validated settings, stream verified chunked files to .part, publish safely, and report completion only after a real peer ACK.
- **Invariants:** P-256 ECDH and HKDF remain acceptable cryptographic building blocks; raw keys never appear on public device models or logs; local-only operation remains possible.
- **Failure behavior:** protocol/version/auth/replay/length/hash/disk-space/ACK errors close or reject only the affected session/transfer, remove owned partials, and emit actionable status; zero peers cannot return success.
- **Compatibility requirements:** protocol version negotiation is explicit; if the current Axora Mobile implementation cannot negotiate the new version, release is blocked rather than silently falling back to plaintext.
- **Security implications:** one-time finite-window pairing offer; no secret in discovery or logs; HMAC possession proof; directional keys; authenticated header; unique nonce/counter; replay rejection; sanitized filenames and destination containment; session key buffers zeroed on close.
- **Persistence/migration implications:** existing QuickDrop user files stay untouched; only AXORA-owned transfer partials may be cleaned.
- **Concurrency implications:** per-peer send lock/counters and tracked connection task; bounded receive assembly; streaming I/O; immutable/interlocked connected-count snapshot; service stop/disconnect cancels and joins the actual session.
- **UI implications:** QR shows current pairing window/version; Mobile Link begins with zero devices, send is disabled until a real peer exists, the actual endpoint is displayed, and status distinguishes paired, incompatible, rejected, unacknowledged, transfer verification failure, and stopped.
- **Test plan:** known frame vectors; two peers with distinct keys; wrong/missing key/tag; nonce/sequence replay; fragmented handshake/frame; oversize frames/files; token absence from beacon; one-time offer race; malicious filename; out-of-order chunk; length/hash mismatch; cancellation; zero-peer send; authenticated ACK; reconnect; configured port/directory/background preference; real disconnect; no fake device; connected-count contention; multi-peer send serialization.
- **Runtime verification:** two-process loopback plus real Axora Mobile compatibility check when available; monitor memory on a multi-gigabyte synthetic sparse/streamed transfer without retaining whole content.
- **Full regression requirement:** full suite, Mobile Link navigation/UI smoke, startup/shutdown lifecycle.
- **Rollback/recovery:** protocol version keeps old and new behavior distinguishable, but no insecure compatibility fallback; revert as one protocol commit if mobile counterpart is not ready.
- **Explicit exclusions:** Internet relay, cloud discovery, background system service, arbitrary broadcast payload API.
- **MUST CHANGE:** session ownership, encrypted framing, secret-free discovery, fragmentation, typed acknowledged chunked file transfer, settings consumption, real disconnect/count state, token lifecycle, and false UI state.
- **MAY CHANGE IF REQUIRED:** minimal Mobile Link status/compatibility XAML.
- **MUST NOT CHANGE:** MaterialUI/mobile repository code not present in this workspace, W5 files, unrelated QuickDrop files.
- **Stop condition:** plaintext outbound/discovery secret remains, nonce uniqueness cannot be proven, bounded fragmentation/ACK fails, a fake UI success/device remains, or counterpart compatibility is unknown at release gate.

### V0-R2A — Safe publication for Converter and Compressor

- **Wave ID:** V0-R2A.
- **Objective:** introduce the smallest correct single-file publication primitive and remove destructive overwrite from Converter and Compressor.
- **Accepted findings addressed:** V0-W2-001, V0-CMP-001.
- **Exact intended production files:** Axora-Desktop-WinUI/Axora.Desktop/App.xaml.cs; Services/ConversionOrchestrator.cs; Services/ConversionOutputValidator.cs; Services/IntelligentCompressorService.cs; Services/Contracts/IConversionOrchestrator.cs and IIntelligentCompressorService.cs only if additive result plumbing is required; ViewModels/UniversalConverterViewModel.cs and CompressorViewModel.cs only for result messaging.
- **Exact intended test files:** Axora-Desktop-WinUI/Axora.Desktop.Tests/Program.cs; scripts/qa/test-winui-universal-converter-real.ps1; scripts/qa/test-winui-universal-converter-user-journey.ps1.
- **New files:** Axora-Desktop-WinUI/Axora.Desktop/Models/FilePublicationModels.cs; Services/Contracts/IFilePublicationService.cs; Services/FilePublicationService.cs.
- **Behavior before:** validated stage can be followed by destination delete then move; compressor publication is not governed by one recovery contract.
- **Behavior after:** producer writes a sibling stage; validation/flush precedes one explicit commit policy; failed replacement leaves or restores the prior destination.
- **Invariants:** source is never destination; source hash protection remains; CreateNew never overwrites; output validation remains format-specific.
- **Failure behavior:** collision, validation failure, commit failure, restore failure, and post-commit cancellation are distinct typed outcomes; no false canceled/success state.
- **Compatibility requirements:** existing conversion profiles, engines, telemetry, naming, and source-protection semantics remain.
- **Security implications:** normalized containment and non-predictable owned staging names; no broad temp cleanup or symlink-like redirection accepted without revalidation.
- **Persistence/migration implications:** none; abandoned owned stages may be cleaned by exact naming/age policy only.
- **Concurrency implications:** commit-time collision recheck; same destination is serialized/reserved; unrelated destinations can proceed concurrently.
- **UI implications:** preserve Skip/AutoRename/Replace semantics and provide recovery-required messaging; no redesign.
- **Test plan:** CreateNew race, ReplaceExisting success, locked destination, injected move/replace failure, backup restoration, cancellation at every phase, validator failure, source=destination, pre-existing source/destination integrity.
- **Runtime verification:** real image/document conversions and compression with collision modes; reopen output; compare original destination bytes after induced failure.
- **Full regression requirement:** full suite and real/user-journey converter scripts.
- **Rollback/recovery:** backup persists when automatic restoration fails and result exposes it; shared service change is isolated before later consumers migrate.
- **Explicit exclusions:** metadata/multi-frame capability expansion (V0-W2-002), Scholar multi-file publication, W5 Image Studio.
- **MUST CHANGE:** publisher, Converter commit, Compressor commit.
- **MAY CHANGE IF REQUIRED:** additive result models and narrow view-model messages.
- **MUST NOT CHANGE:** conversion engine capability matrix beyond bug-required publication, MaterialUI, W5 files.
- **Stop condition:** prior destination can be lost on any injected pre-commit failure or service callers can bypass required validation.

### V0-R1E — Vault format, destination safety, and truthful security

- **Wave ID:** V0-R1E.
- **Objective:** make new vault files self-describing, honor bounded KDF settings, publish safely, and align claims with actual Windows/storage behavior.
- **Accepted findings addressed:** V0-VLT-001.
- **Exact intended production files:** Axora-Desktop-WinUI/Axora.Desktop/Services/StreamingVaultService.cs; Services/Contracts/ISecurityVaultService.cs; Services/TpmSecurityProfileService.cs; Services/Contracts/ITpmSecurityProfileService.cs; Helpers/CryptographyHelper.cs; Models/TpmSecurityModels.cs; ViewModels/VaultViewModel.cs; Views/VaultPage.xaml and .xaml.cs; App.xaml.cs only for DI additions already introduced by V0-R2A.
- **Exact intended test files:** Axora-Desktop-WinUI/Axora.Desktop.Tests/Program.cs.
- **New files:** Axora-Desktop-WinUI/Axora.Desktop/Models/VaultFileFormat.cs; Models/VaultEncryptionOptions.cs.
- **Behavior before:** unversioned v1 assumes Argon2 64 MiB/3 passes; selected settings can be ignored; destination is directly created; “TPM” and “DoD shred” wording overclaims.
- **Behavior after:** AXVLT002 header records schema/KDF/block/salt/nonce parameters; bounded selected settings are honored; v1 decrypt remains; stage/authenticate/publish protects the destination.
- **Invariants:** authenticated encryption and streaming remain; legacy no-magic v1 files decrypt with historical 64 MiB/3 behavior; sealed DPAPI data/path remains readable.
- **Failure behavior:** authentication, cancellation, invalid header/KDF bounds, I/O, or publish failure leaves existing destination unchanged and removes owned stage; sealing returns typed failure, never false success.
- **Compatibility requirements:** same user can unseal existing DPAPI-protected key; no destructive migration; v2 is additive.
- **Security implications:** describe DPAPI as “Windows account-protected key”; hardware backing is not guaranteed. Describe deletion as “Delete original after verified encryption” with SSD/filesystem recovery warning and per-run confirmation. Zero derived byte buffers where possible; do not claim managed password strings can be reliably zeroed.
- **Persistence/migration implications:** old vault files and sealed-key blobs remain; new files are self-describing; no bulk rewrite.
- **Concurrency implications:** one operation per destination; cancellation before publication; original deletion only after verified publication and explicit user choice.
- **UI implications:** Replace/AutoRename/Cancel collision choice; KDF bounds/errors visible; corrected labels and warning.
- **Test plan:** v1 fixture decrypt, v2 parameter round-trip, invalid/excessive KDF rejection, wrong password/tamper, cancellation, collision modes, existing-destination preservation, DPAPI legacy blob, seal failure, original-delete ordering.
- **Runtime verification:** encrypt/decrypt representative large file under memory monitoring; compare SHA-256; test restart/unseal; test locked destination recovery.
- **Full regression requirement:** full suite plus Vault UI smoke.
- **Rollback/recovery:** reader remains dual-version; revert writer to v1 only if no v2 files have shipped, otherwise retain v2 reader permanently.
- **Explicit exclusions:** guaranteed secure erase, hardware TPM attestation, credential manager redesign, cloud key recovery.
- **MUST CHANGE:** format/version options, publication, result semantics, security wording.
- **MAY CHANGE IF REQUIRED:** type names while preserving serialized data/path compatibility.
- **MUST NOT CHANGE:** existing user vault or sealed-key data, MaterialUI, W5 files.
- **Stop condition:** v1 fixture compatibility fails, selected KDF is not encoded/read, or a failed decrypt/encrypt can damage source/destination.

### V0-R2B — Batch capability truth, memory, collision, and process safety

- **Wave ID:** V0-R2B.
- **Objective:** execute only operations the selected engine can honor and keep batch resource use/collisions bounded.
- **Accepted findings addressed:** V0-BAT-001, V0-BAT-002.
- **Exact intended production files:** Axora-Desktop-WinUI/Axora.Desktop/Services/BatchImageProcessorService.cs; Services/Contracts/IBatchImageProcessorService.cs; Models/BatchImageJob.cs; ViewModels/BatchImageViewModel.cs; Views/BatchImagePage.xaml and .xaml.cs.
- **Exact intended test files:** Axora-Desktop-WinUI/Axora.Desktop.Tests/Program.cs.
- **New files:** Axora-Desktop-WinUI/Axora.Desktop/Models/BatchCapabilityResult.cs; Services/DecodedImageMemoryGate.cs.
- **Behavior before:** WIC fallback can claim success while ignoring requested semantics; deterministic names overwrite; worker count can reach 16 without decoded-memory accounting; ImageMagick command handling is brittle.
- **Behavior after:** preflight binds every requested option to an engine implementation or fails actionably; output uses reserved collision policy and shared publication; decoded memory and workers are bounded; ImageMagick uses the process runner.
- **Invariants:** resize/core format conversion remains locally available with WIC; Strip metadata true remains truthful when output is freshly encoded; requested preserve/watermark/target-size behavior cannot silently disappear.
- **Failure behavior:** unsupported combination fails before output; per-item failures do not masquerade as success; timeout/cancellation kills process tree and preserves existing destination.
- **Compatibility requirements:** current jobs/presets load; default collision is AutoRename, with explicit Skip/Overwrite options; naming stays recognizable.
- **Security implications:** ArgumentList prevents shell interpolation; filenames remain data; output path normalization/reservation prevents cross-item overwrite.
- **Persistence/migration implications:** no job-store schema change unless current persisted model requires an additive collision field with default AutoRename.
- **Concurrency implications:** maximum workers min(ProcessorCount, 8); decoded-byte estimate width × height × 4 enters a gate with budget min(512 MiB, max(128 MiB, available-memory/8)); an oversized item runs exclusively or fails before decode.
- **UI implications:** engine/capability preflight lists why an option is unavailable; collision policy is explicit; progress differentiates skipped, unsupported, failed, and published.
- **Test plan:** capability matrix, JPEG quality, bounded target-size search (maximum six attempts with declared tolerance), watermark without ImageMagick, preserve-metadata request, case-insensitive collision, reservation race, memory gate, cancellation, stderr saturation, process timeout, destination preservation.
- **Runtime verification:** representative mixed batch on the user’s 16 GB/Iris Xe machine while recording peak working set, worker count, output dimensions/format/size, and collisions.
- **Full regression requirement:** full suite plus Batch UI smoke and affected Converter/Compressor publisher tests.
- **Rollback/recovery:** per-item stages/backups remain recoverable; process runner and publisher are already accepted dependencies and must not be forked locally.
- **Explicit exclusions:** GPU-specific pipeline, new codec dependency, silent target-size approximation for unsupported formats.
- **MUST CHANGE:** preflight, collision policy, safe publication, memory gate, process execution.
- **MAY CHANGE IF REQUIRED:** minimal XAML for capability/collision feedback.
- **MUST NOT CHANGE:** unrelated conversion engines, MaterialUI, W5 files.
- **Stop condition:** any requested option can still yield success without being applied, or peak concurrency bypasses the memory gate.

### V0-R2C — Scholar generation consistency

- **Wave ID:** V0-R2C.
- **Objective:** give Scholar index readers one validated generation snapshot without claiming multi-file atomicity.
- **Accepted findings addressed:** V0-W3-001 and the directly coupled V0-W3-002 lifecycle hardening.
- **Exact intended production files:** Axora-Desktop-WinUI/Axora.Desktop/Services/ScholarVectorIndexWriter.cs; Services/ScholarVectorIndexReader.cs; Services/ScholarIndexService.cs; Services/Contracts/IScholarIndexService.cs; Services/DocumentChatService.cs; Services/Contracts/IDocumentChatService.cs; ViewModels/ScholarKitViewModel.cs; Models/IndexModels.cs; Models/ScholarKitModels.cs only if generation metadata belongs there.
- **Exact intended test files:** Axora-Desktop-WinUI/Axora.Desktop.Tests/Program.cs; new V0R2C_ScholarIndexConsistencyTests.cs; scripts/qa/test-winui-scholar-persistence-runtime.ps1.
- **New files:** Axora-Desktop-WinUI/Axora.Desktop.Tests/V0R2C_ScholarIndexConsistencyTests.cs; a narrowly scoped ScholarIndexGenerationModels.cs is permitted only if existing model files would mix responsibilities.
- **Behavior before:** vectors and manifest are moved separately; the reader does not robustly bind active manifest/hash to mapped payload; RebuildIndexAsync deletes the current index first; transient chat clears/replaces mutable live state and callers can discard indexing tasks.
- **Behavior after:** immutable verified generation is published first and one active pointer commits visibility; reader binds pointer, IDs, metadata, and payload hash; rebuild preserves the prior active index until commit; transient indexing publishes one immutable complete snapshot from tracked work.
- **Invariants:** existing documents/library records remain; legacy v1 index remains readable; search never observes a half-generated v2.
- **Failure behavior:** corrupt/missing/mismatched generation is rejected and quarantined/logged; previous valid active generation or transient chat snapshot remains; rebuild is offered; indexing failure/cancellation is observed rather than discarded.
- **Compatibility requirements:** lazy migration on successful rebuild; no forced deletion or eager rewrite.
- **Security implications:** validate path IDs/containment and bounded sizes before mapping/allocating; treat persisted index as untrusted local input.
- **Persistence/migration implications:** schema v2 immutable generations plus active.json; retain prior generation until readers release memory maps, then bounded cleanup of owned generations.
- **Concurrency implications:** readers snapshot the pointer; writer uses a per-document mutation lock; active switch occurs after all streams/mappings are closed and verified; dictation/static-import indexing is tracked, debounced/superseded by cancellation, and queries wait for the newest requested revision or report indexing.
- **UI implications:** existing rebuild/error UX may show “index inconsistent; rebuild available”; a minimal real indexing indicator/disabled query state may be added, but no Scholar redesign.
- **Test plan:** v1 fixture with manifest-to-binary hash check; v2 build/read; torn generation before pointer; corrupt pointer/manifest/binary; swapped valid-looking binary; count/dimension/hash/generation mismatch; failed rebuild preserves prior index; concurrent reader/rebuild; cleanup with open reader; crash at every phase; failed/canceled transient replacement preserves prior chat snapshot; discarded-task scan.
- **Runtime verification:** real document index/restart/search plus fault-injected staging; run persistence script.
- **Full regression requirement:** full suite and Scholar persistence/runtime smoke.
- **Rollback/recovery:** old active generation is retained; pointer can be restored; v1 reader cannot be removed.
- **Explicit exclusions:** embedding model change, synthesis quality expansion, Class-B/model implementation.
- **MUST CHANGE:** writer/reader generation binding, pointer commit, delete-before-rebuild behavior, and directly coupled indexing-task/live-snapshot ownership.
- **MAY CHANGE IF REQUIRED:** narrow generation model file and minimal indexing/rebuild status UI.
- **MUST NOT CHANGE:** Scholar product scope, MaterialUI, W5 files.
- **Stop condition:** a reader can observe mixed generations, failed replacement destroys prior persistent/transient state, legacy fixtures fail, an indexing task is orphaned, or cleanup can delete a mapped/active generation.

### V0-R2D — Voice, speech, and Flashcards coordination

- **Wave ID:** V0-R2D.
- **Objective:** complete the production command-recognition chain and serialize recognition/speech through one coordinator.
- **Accepted findings addressed:** V0-FLS-001, V0-W4-001, core V0-W4-002.
- **Exact intended production files:** Axora-Desktop-WinUI/Axora.Desktop/Services/VoiceCoordinator.cs; VoiceCommandRouter.cs; VoiceTranscriberService.cs; SpeechSynthesisService.cs; AudioDeviceMonitor.cs only if status sequencing requires it; corresponding interfaces under Services/Contracts; Models/VoiceModels.cs; Helpers/DispatcherHelper.cs; ViewModels/FlashcardsViewModel.cs; ViewModels/ScholarKitViewModel.cs only for existing dictation/read-aloud integration; ViewModels/ShellViewModel.cs only for safe action dispatch; App.xaml.cs for DI registration only.
- **Exact intended test files:** Axora-Desktop-WinUI/Axora.Desktop.Tests/W4_VoiceSubsystemTests.cs; Program.cs; new V0R2D_VoiceIntegrationTests.cs.
- **New files:** Axora-Desktop-WinUI/Axora.Desktop/Services/Contracts/ISpeechPlaybackBackend.cs; Services/MediaPlayerSpeechPlaybackBackend.cs; Axora-Desktop-WinUI/Axora.Desktop.Tests/V0R2D_VoiceIntegrationTests.cs.
- **Behavior before:** router “listening” is a flag without production recognition ownership; the transcriber swallows start failure so the coordinator can still report Dictating; string/chunk callbacks can remain simultaneously active; speech returns after Play begins and may dispose its stream; direct consumers bypass the coordinator; UI dispatch can hang if enqueue fails.
- **Behavior after:** coordinator starts one typed-result recognition session and enters a listening state only after actual capture succeeds; one callback/mode exists per generation; final text routes to dictation or commands; awaited playback owns media lifetime through completion and restores desired recognition state; Flashcards uses coordinator.
- **Invariants:** command router remains deny-by-default for ConfirmationRequired; no command executes on ambiguous match; dictation punctuation remains local/configurable.
- **Failure behavior:** recognition compilation/permission/start failure returns a typed unavailable/failed result and never reports listening; stale/duplicate callbacks are cleared/ignored; media/recognizer/device failure transitions to explicit state and completes waiters exactly once; failed dispatcher enqueue faults promptly.
- **Compatibility requirements:** current commands and Scholar/Flashcards flows remain; public interface additions are additive where possible.
- **Security implications:** command safety metadata enforced before dispatch; no hidden destructive command; recognized text not logged by default.
- **Persistence/migration implications:** none in this wave; desired settings integration completes in V0-R2E.
- **Concurrency implications:** one recognition mode/callback, one playback, generation token/CTS, bounded sequential command processing, no state lock held for full synthesis/playback, event subscriptions balanced.
- **UI implications:** state is observable for later shell/settings controls; no broad XAML here.
- **Test plan:** command final-result path; recognition start/constraint/permission failure never enters Listening/Dictating; string/chunk callback mutual exclusion; dictation path; ambiguous/unknown/confirmation-required denial; speak suspends/resumes; pending playback proves no early completion; sequential speech; MediaEnded/failure/stop/cancel/disposal; rapid mode changes; stale callbacks; direct consumer elimination; failed dispatcher enqueue; shutdown.
- **Runtime verification:** microphone command navigation, dictation, Flashcards/Scholar read-aloud, device removal, repeated start/stop/speak on real hardware.
- **Full regression requirement:** full suite plus W4 tests and voice-related navigation smoke.
- **Rollback/recovery:** single coordinator contract keeps previous router matcher reusable; revert as one subsystem change if native lifecycle proves unstable.
- **Explicit exclusions:** cloud speech, wake word, new AI model, confirmation UI broker, recording archive.
- **MUST CHANGE:** recognition ownership, playback completion/lifetime, coordinator consumers, dispatcher failure.
- **MAY CHANGE IF REQUIRED:** additive typed start/router/playback results and state/events in voice contracts.
- **MUST NOT CHANGE:** unrelated pages, MaterialUI, W5 files.
- **Stop condition:** more than one recognition owner/callback exists, coordinator can claim listening without active capture, SpeakTextAsync completes before terminal playback, stale callbacks can execute actions, or a direct speech consumer remains in scoped flows.

### V0-R2E — Settings schema, W4 controls, and command palette truth

- **Wave ID:** V0-R2E.
- **Objective:** make settings recoverable/versioned, synchronize desired voice state with actual state, and expose only working palette actions.
- **Accepted findings addressed:** V0-SET-001, V0-NAV-001, settings/UI portion of V0-W4-002.
- **Exact intended production files:** Axora-Desktop-WinUI/Axora.Desktop/Services/AppSettingsService.cs; Services/Contracts/IAppSettingsService.cs; ViewModels/SettingsViewModel.cs; Views/SettingsPage.xaml and .xaml.cs; ViewModels/ShellViewModel.cs; Views/ShellView.xaml and .xaml.cs; Controls/CommandPaletteDialog.xaml and .xaml.cs; App.xaml.cs; MainWindow.cs only for post-load desired-state activation.
- **Exact intended test files:** Axora-Desktop-WinUI/Axora.Desktop.Tests/Program.cs; W4_VoiceSubsystemTests.cs; new V0R2E_NavigationSettingsTests.cs; scripts/qa/test-winui-ui.ps1.
- **New files:** Axora-Desktop-WinUI/Axora.Desktop/Models/CommandPaletteEntry.cs; Models/SettingsSchema.cs only if the schema is not cleanly internal to AppSettingsService; Axora-Desktop-WinUI/Axora.Desktop.Tests/V0R2E_NavigationSettingsTests.cs.
- **Behavior before:** direct settings write can truncate; there is no schema/recovery; five W4 settings are not fully surfaced; voice catalog is read before synthesis initialization and device callbacks may mutate UI state off-thread; seven dead palette actions coexist with a missing Converter route; advertised Up/Down/Enter execution is not implemented and AutoSuggestBox has no suggestion source.
- **Behavior after:** schema-v2 settings use validated safe publication/backups and quarantine malformed primary data; initialized voice catalog and dispatcher-safe device events feed truthful controls; actual coordinator state is synchronized; palette navigation derives from typed routes, incomplete actions are absent, and keyboard behavior executes the selected real route.
- **Invariants:** existing JSON field names load; missing schema is v1; unknown fields are ignored; Resume internal editor route stays internal.
- **Failure behavior:** malformed primary becomes settings.corrupt.<timestamp>.json, then .bak is attempted, then defaults; save failure keeps previous settings; voice initialization/device failure is marshalled to explicit unavailable status instead of an empty catalog, cross-thread exception, or false enabled state.
- **Compatibility requirements:** preserve theme/accent/download/KDF/voice values after clamp; no user settings deletion.
- **Security implications:** validate enum/hex/path/port/KDF/speech bounds. Replace “Zero-Cloud Guarantee” with “Local-first processing”: “Document analysis, OCR, study synthesis, and encryption run locally. Extension downloads and Mobile Link use network access only when you invoke or enable them.”
- **Persistence/migration implications:** schema v2; sibling stage/flush/safe replace/backup through IFilePublicationService; v1 loads and writes v2 on next successful save.
- **Concurrency implications:** serialize load/save; use immutable snapshot or lock; async Save awaits coordinator transition without blocking UI thread; synthesis initialization is awaited before enumeration; named device-event handlers marshal through the captured dispatcher and unsubscribe under DI-owned disposal.
- **UI implications:** a compact “Voice & Dictation” card contains voice selector, rate/pitch sliders, voice navigation, auto punctuation, microphone/actual-state status, and Test Voice. Shell gets a minimal global toggle/status. Palette adds Universal Converter and removes Vault Encrypt/Decrypt, Mobile Start/Stop, Flashcards New Deck, Scholar Browse, and Settings Reset until contextual implementations exist. Ctrl+K focuses search; Up/Down changes the filtered-list selection; Enter executes the selected or first result; Escape closes; close restores prior focus. Typed entries raise a Shell navigation request instead of parsing display strings or directly reaching App.MainAppWindow.
- **Test plan:** v1 migration; valid v2; corrupt primary/valid backup; both corrupt; clamps; interrupted/concurrent saves; awaited voice enumeration; dispatcher-safe device change; startup desired-enabled success/failure; Save/Reset applies runtime; control bindings; test speech coordination; typed route-set completeness across Shell/palette/voice; no dead actions; internal ResumeStudioEditor exclusion; Ctrl+K/filter/Up/Down/Enter/Escape/focus restoration; Converter navigation and rendered destination.
- **Runtime verification:** modify/restart settings; corrupt a disposable test settings file; exercise actual Ctrl+K/filter/keyboard navigation and page render; light/dark/high-contrast/readability; 100/125/150/200% scale; screen-reader names; voice enable/test/disable and unavailable-host path.
- **Full regression requirement:** full suite plus semantic UI script and voice tests.
- **Rollback/recovery:** .bak and quarantined original are preserved; route catalog can be reverted without data migration.
- **Explicit exclusions:** implementing the seven removed actions, broad navigation redesign, cloud settings sync.
- **MUST CHANGE:** settings store/schema, voice catalog/event/UI synchronization, palette route/action catalog and advertised keyboard behavior.
- **MAY CHANGE IF REQUIRED:** minimal Shell/MainWindow activation hook and accessible control text.
- **MUST NOT CHANGE:** feature pages behind removed actions, MaterialUI, W5 files.
- **Stop condition:** save can destroy last-known-good settings, enabled UI can disagree silently with coordinator, voice catalog/device events are unsafely initialized/dispatched, or any displayed/keyboard-selected palette entry lacks a verified execution path.

### V0-R3 — Final test-evidence and UI-test hardening

- **Wave ID:** V0-R3.
- **Objective:** remove every known forced-pass path and make automated UI evidence semantic and auditable.
- **Accepted findings addressed:** remainder of V0-TEST-001, V0-UI-001.
- **Exact intended production files:** none.
- **Exact intended test files:** Axora-Desktop-WinUI/Axora.Desktop.Tests/Program.cs; W4_VoiceSubsystemTests.cs; scripts/qa/test-winui-ui.ps1; scripts/qa/test-winui-product-flows.ps1 and test-adversarial-winui.ps1 only where the same forbidden evidence pattern exists.
- **New files:** scripts/qa/verify-test-evidence.ps1.
- **Behavior before:** literal true assertions, “-or $true”, unconditional Record-Test success, or contract-only text checks can report functional success.
- **Behavior after:** scanner rejects forbidden patterns; UI tests require element discovery, invoke/select behavior, navigation result, rendered expected control/page, and binding/state change.
- **Invariants:** screenshots/logs are supplemental; environment gaps are visible and not assertions; no test is weakened to stabilize count.
- **Failure behavior:** unavailable automation is an explicit gap/nonzero gate according to RV policy, not a pass; missing element/action/result fails with diagnostic context.
- **Compatibility requirements:** scripts retain documented entry points and safe cleanup.
- **Security implications:** security-sensitive UI and operations no longer inherit fabricated evidence.
- **Persistence/migration implications:** none.
- **Concurrency implications:** UI automation serializes against one app instance and uses bounded readiness waits, not arbitrary forced success.
- **UI implications:** no production UI change; semantic tests cover actual interaction, focus, disabled states, and accessibility names.
- **Test plan:** seed known forbidden fixture/pattern to prove scanner failure; run console manifest; validate representative positive and negative UI flow.
- **Runtime verification:** full WinUI semantic automation on the target Windows desktop with screenshots/logs retained as artifacts.
- **Full regression requirement:** complete test executable, every applicable QA script, authoritative build, no unexplained warning increase.
- **Rollback/recovery:** script/test-only commit; retain diagnostics if reverted.
- **Explicit exclusions:** production behavior changes, pixel-perfect screenshot assertions as sole oracle.
- **MUST CHANGE:** forced-pass constructs and completeness scanner.
- **MAY CHANGE IF REQUIRED:** adjacent WinUI scripts with verified same pattern.
- **MUST NOT CHANGE:** production code/XAML, MaterialUI, W5 files.
- **Stop condition:** any literal/conditional forced pass remains or UI navigation success can be recorded without validating the destination state.

### V0-RV — Historical final verification

- **Wave ID:** V0-RV.
- **Objective:** independently prove every mandatory finding’s acceptance criterion on the integrated baseline.
- **Accepted findings addressed:** all confirmed FIX BEFORE W5 items and V0-UI-001.
- **Exact intended production files:** none.
- **Exact intended test files:** no test changes are allowed while the gate is running; evidence report may be separately authorized as docs/V0_REMEDIATION_VERIFICATION_REPORT.md.
- **New files:** only the verification report if explicitly authorized.
- **Behavior before:** repaired waves have targeted evidence but not one integrated release gate.
- **Behavior after:** each finding has build, deterministic/integration, runtime/UI, compatibility, and Git evidence or an explicit user-approved reclassification.
- **Invariants:** no test or product fix is made inside the verification run; a failure returns to the owning wave.
- **Failure behavior:** any unexplained build warning, missing manifest group, forced-pass hit, compatibility break, runtime failure, dirty protected area, or orphan finding blocks V0 completion.
- **Compatibility requirements:** v1 settings/index/vault fixtures; existing extension install; existing QuickDrop files; mobile protocol version outcome.
- **Security implications:** rerun extension trust, Vault tamper, P2P auth/replay, path/collision, and privacy-wording checks.
- **Persistence/migration implications:** execute backups/quarantine/rollback fixtures in isolated test locations; never mutate the user’s real data for fault injection.
- **Concurrency implications:** stress publisher collisions, batch memory gate, P2P sessions, Scholar reader/rebuild, settings saves, and voice state changes.
- **UI implications:** keyboard, accessibility names, theme/scale, navigation, disabled/error/progress states, and representative real operations.
- **Test plan:** traceability matrix below is the checklist; capture exact commands, versions, exit codes, and artifacts.
- **Runtime verification:** real WinUI app on the target Dell Inspiron 14 5430; hardware-specific observations are labeled observations, not universal assertions.
- **Full regression requirement:** authoritative clean build, full manifest suite, semantic UI scripts, real converter/Scholar flows, and security scanner.
- **Rollback/recovery:** failed gate identifies last known-good wave commit and exact recovery; no history rewriting.
- **Explicit exclusions:** new features, opportunistic cleanup, W5 implementation.
- **MUST CHANGE:** nothing during verification; only separately authorized evidence report.
- **MAY CHANGE IF REQUIRED:** return to an owning repair wave under new authorization.
- **MUST NOT CHANGE:** production/tests during gate, MaterialUI, W5 files.
- **Stop condition:** all mandatory acceptance criteria pass and Git scope is clean, or immediately on any failed criterion pending repair authorization.

### V0-R5 — Optional non-blocking cleanup

- **Wave ID:** V0-R5.
- **Objective:** address separately selected, non-blocking debt without reopening the V0 architecture.
- **Accepted findings addressed:** selectable V0-W2-002, V0-W3F-001, V0-W4-003, V0-BLD-001; never all by default. V0-W3-002 is already coupled to V0-R2C and is not deferred here.
- **Exact intended production files:** for W2-002, WicImageConversionEngine.cs, ConversionOutputValidator.cs, related conversion models; for W3F-001, ScholarKitPage.xaml, ScholarKitViewModel.cs, NullScholarSlmModelDriver.cs, FlashcardsViewModel.cs user-facing capability text only; for W4-003, VoiceCommandRouter.cs/VoiceModels.cs only when a real command demands it; for BLD-001, only the four files producing obsolete Skia calls and the file containing the unused Scholar field, re-resolved at wave start.
- **Exact intended test files:** Program.cs, ScholarSynthesisEngineTests.cs, W4_VoiceSubsystemTests.cs, and the relevant existing runtime/UI script for the selected subset.
- **New files:** none presumed; exact scope must be frozen after the user selects a subset.
- **Behavior before:** real but non-blocking depth/debt remains documented.
- **Behavior after:** only the selected item has stronger evidence/wording/lifecycle or warning-free API use.
- **Invariants:** V0-RV remains green; no optional cleanup changes published capability without product approval.
- **Failure behavior:** revert the selected isolated change; do not hold V0 completion hostage.
- **Compatibility requirements:** all V0 migration readers and public behaviors remain.
- **Security implications:** no confirmation broker is added speculatively; no capability/privacy wording overclaims.
- **Persistence/migration implications:** none unless separately planned for a selected item.
- **Concurrency implications:** each selected subset documents its own; no speculative background service or broker is introduced.
- **UI implications:** W3F wording may change; capability additions require their own UI proof.
- **Test plan:** targeted pre-fix evidence and regression for selected subset.
- **Runtime verification:** relevant real format/index/voice flow only.
- **Full regression requirement:** full suite and V0-RV critical subset.
- **Rollback/recovery:** one commit per selected cleanup.
- **Explicit exclusions:** W5 feature implementation, broad codec expansion, speculative abstractions.
- **MUST CHANGE:** only the explicitly authorized subset.
- **MAY CHANGE IF REQUIRED:** exact adjacent files approved in the frozen subset contract.
- **MUST NOT CHANGE:** all unselected debt, MaterialUI, W5 files.
- **Stop condition:** scope touches an unselected item, changes a product promise, or regresses V0-RV.

## 8. Traceability and acceptance matrix

| Finding | Root cause | Wave/owner | Regression evidence | Runtime evidence | Acceptance criterion |
|---|---|---|---|---|---|
| V0-TCH-001 | inconsistent/invalid SDK discovery | R4/build | root+nested resolver checks; clean build | launch with recorded SDK/MSBuild | all entry points use approved .NET 9 feature band |
| V0-TEST-001 | no completeness ledger; fabricated evidence | R1A+R3/test harness | manifest self-tests; forbidden-pattern scan | full executable/script exits | every group accounted; zero forced passes counted |
| V0-FND-001 | fatal failure swallowed | R1B/App | injected initialization/global-handler tests | induced safe startup failure | fatal path cannot continue as healthy |
| V0-FND-002 | double disposal | R1B/host lifecycle | fake disposal-count tests | launch/close/tray exit | each resource disposed by one owner exactly once |
| V0-W15-001 | no mandatory identity gate | R1C/extensions | hash/signature/publisher negative matrix | verified official artifact path | unverified bytes cannot reach execution |
| V0-W15-002 | destructive replacement/process hazards | R1C/extensions | failure/timeout/restore matrix | install/repair/restart | known-good install survives failed replacement |
| V0-P2P-001 | no private session; plaintext secret discovery; no bounded/acknowledged transfer | R1D/P2P | auth/replay/fragment/beacon/settings/ACK tests | loopback + mobile version check | no plaintext/raw/secret-discovery path; real peer ACKs verified streamed transfer |
| V0-W2-001 | delete-before-commit | R2A/publisher | injected commit failures | real overwrite conversion | prior destination remains on failure |
| V0-CMP-001 | unsafe direct publication | R2A/publisher | shared publisher compressor tests | real collision compression | same safety/result contract as Converter |
| V0-VLT-001 | unversioned settings mismatch/unsafe output/claims | R1E/Vault | v1/v2/tamper/collision/DPAPI fixtures | large-file round trip | settings honored and encoded; old data works; no false claims |
| V0-BAT-001 | silent fallback omission | R2B/Batch | capability/output property matrix | mixed real batch | unsupported request fails before success output |
| V0-BAT-002 | unbounded memory/collision/process | R2B/Batch | memory/reservation/timeout tests | 16 GB host observation | bounded peak work and collision-safe outputs |
| V0-W3-001 | two-file visibility/delete-before-rebuild without generation binding | R2C/Scholar | crash/corruption/rebuild/concurrency fixtures | index/restart/search | reader sees one verified generation or rejects it; failed rebuild preserves prior index |
| V0-W3-002 | discarded/background work and mutable live replacement | R2C/Scholar | cancellation/failure/revision/task-ownership tests | import/dictation/index/query states | complete immutable snapshot publishes from tracked work; prior snapshot survives failure |
| V0-FLS-001 | direct speech consumer | R2D/voice | coordinator fake/call-path test | Flashcards speak | all scoped playback passes coordinator |
| V0-W4-001 | router flag without recognizer; swallowed start failure | R2D/voice | final-recognition-to-command and failed-start state tests | microphone command navigation | production phrase reaches safe dispatch; listening state requires active capture |
| V0-W4-002 | duplicate callbacks, early playback completion, state drift | R2D+R2E | callback/media lifecycle/state/settings tests | speak/interrupt/restart UI | exactly one callback/playback/mode and truthful desired/actual state |
| V0-NAV-001 | drifting manual action list and false keyboard affordance | R2E/navigation | route completeness/dead-action/keyboard tests | Ctrl+K/filter/select/execute/render | Converter present; seven dead actions absent; every displayed entry executes |
| V0-SET-001 | direct unversioned persistence and unsafe voice initialization/events | R2E/settings | migration/corruption/interruption/catalog/dispatcher tests | edit/restart/recover/voice unavailable | previous valid data survives failed save; W4 fields and runtime state are truthful |
| V0-UI-001 | non-semantic/manual-only proof | R3+RV/QA | semantic automation and scanner | theme/scale/a11y/manual matrix | applicable UI contract has behavioral evidence or explicit gap |

## 9. Data compatibility and migration

- **Settings:** missing schema means v1. Validate/clamp theme 0–2, accent hex, port 1–65535, non-empty normalized download path, Argon2 memory 16–512 MiB and iterations 1–10, and speech rate/pitch bounds. Preserve malformed primary as timestamped quarantine, try .bak, then defaults. Successful next save writes schema v2.
- **Vault:** retain legacy no-magic v1 decrypt at historical parameters and the current DPAPI sealed-key file/blob. New v2 files are self-describing. Never bulk-rewrite user vaults.
- **Scholar:** retain v1 reader. A successful rebuild creates v2 generation and commits active pointer; only then may bounded old-generation cleanup occur.
- **Extensions:** detect/probe existing installation and cache; never delete merely because new trust metadata is unavailable. Revalidate a cached artifact before promoting it to verified cache.
- **P2P/QuickDrop:** existing user files are untouched. Only transfer-owned .part files are managed. Protocol versions negotiate; no plaintext compatibility fallback.
- **Converter/Compressor/Batch:** profile/job formats remain unless an additive collision field is needed; default it to AutoRename.
- **Resume/persistence outside these findings:** unchanged.

Migration tests require stable checked-in synthetic fixtures with no personal data. A future schema reader must be retained after a new schema ships; rollback must not strand data produced by the newer version.

## 10. Security and privacy wording corrections

Use exact truthful language:

- Settings heading: **Local-first processing**.
- Settings description: **Document analysis, OCR, study synthesis, and encryption run locally. Extension downloads and Mobile Link use network access only when you invoke or enable them.**
- Vault label: **Windows account-protected key (DPAPI)**.
- Vault help: **Protects the saved key for the current Windows user; hardware-backed protection is not guaranteed.**
- Vault deletion option: **Delete original after verified encryption**.
- Vault warning: **Deletion is not guaranteed secure erasure, especially on SSDs, journaling filesystems, backups, or synced storage.**
- Scholar synthesis: **Local Study Synthesis (extractive)** until a model-backed capability exists.

Security results must distinguish confidentiality/integrity, transport/authentication, data-at-rest protection, and best-effort deletion. No “zero cloud,” “TPM-backed,” “DoD shred,” “atomic everywhere,” or “encrypted broadcast” claim is allowed unless the exact shipped path proves it.

## 11. UI and accessibility verification strategy

Production UI changes are deliberately small: trust/status feedback, explicit collision/capability choices, Vault wording, a Voice & Dictation card, a shell voice state, and a truthful palette catalog.

For each touched view:

1. Verify initial, loading, success, empty, unavailable, canceled, error, recovery-required, and disabled states that apply.
2. Verify keyboard-only access, logical tab order, Enter/Space/Escape behavior, focus restoration, and visible focus.
3. Verify AutomationProperties.Name/help text for icon-only/status controls and screen-reader announcement of changing progress/error state.
4. Verify light, dark, and high-contrast themes.
5. Verify 100%, 125%, 150%, and 200% scale without clipped controls; also narrow-window behavior.
6. Verify controls change bound state and that resulting operation/navigation actually occurs.
7. Treat screenshots as supplemental evidence, never the sole oracle.

The target Dell 1920×1200-class display and Iris Xe device is useful runtime evidence, not a substitute for scale/theme/accessibility coverage.

## 12. Test-evidence protocol

Each repair wave follows:

1. Record clean baseline and exact finding.
2. Add or identify a targeted test that fails for the historical defect when practical.
3. Implement only the authorized wave.
4. Run the targeted group.
5. Run affected-consumer tests for shared infrastructure.
6. Run the full manifest suite.
7. Run authoritative build and relevant semantic runtime/UI script.
8. Record command, SDK/MSBuild version, exit code, assertion/observation/gap counts, warnings, and artifacts.
9. Audit forbidden evidence patterns and Git scope.

Environment-dependent facts are recorded with Observe and may gate release when required, but they never increase deterministic assertion totals. Manual gaps remain visible until closed or explicitly accepted by the user.

## 13. Toolchain conclusion

The correct pin is a repository-root global.json using:

    {
      "sdk": {
        "version": "9.0.300",
        "rollForward": "latestPatch",
        "allowPrerelease": false
      }
    }

This expresses the .NET 9.0.3xx feature band and selects the installed 9.0.318 patch while permitting later approved patches in the same band. The current nested 9.0.0 value is not a valid installed SDK feature-band pin and does not govern repository-root commands. Exact 9.0.318 with disable would be unnecessarily brittle. The change still requires V0-R4 authorization and root/nested/Visual Studio revalidation before acceptance.

## 14. Exact proposed file scope by wave

Paths are repository-relative. “Inspect” is not permission to edit.

| Wave | Must-change scope | Conditional scope/new files |
|---|---|---|
| R4 | /global.json; remove Axora-Desktop-WinUI/global.json | scripts/qa/build-all.ps1 |
| R1A | Axora.Desktop.Tests/Program.cs; W4_VoiceSubsystemTests.cs | new TestInfrastructure/TestGroupDefinition.cs and TestRunLedger.cs; run-tests.ps1 |
| R1B | App.xaml.cs; VoiceCoordinator.cs; IVoiceCoordinator.cs; TrayService.cs/ITrayService.cs | Program.cs; W4_VoiceSubsystemTests.cs |
| R1C | DependencyManager, ExtensionDownloader/Installer/Registry/RepairService/Validator, VersionDetector and contracts; ExtensionModel/ValidationResult; App.xaml.cs | new trust/process models, IExternalProcessRunner, ExternalProcessRunner; Program.cs; Download Manager UI only if status contract requires |
| R1D | P2pSyncService/IP2pSyncService; CryptographyHelper; MobileLinkViewModel; QuickDropItem; AxoraDevice; Mobile Link XAML/code-behind | new P2pPeerSession/P2pProtocolCodec/QuickDropTransferSession/P2pProtocolModels/V0R1D_P2pProtocolTests; Program.cs; App.xaml.cs shutdown hook only |
| R2A | App.xaml.cs; ConversionOrchestrator/OutputValidator; IntelligentCompressorService | new publisher model/interface/service; related contracts/view models; Program and converter scripts |
| R1E | StreamingVaultService/ISecurityVaultService; TpmSecurityProfileService/interface; CryptographyHelper; TpmSecurityModels; Vault VM/XAML | new VaultFileFormat/VaultEncryptionOptions; App.xaml.cs; Program.cs |
| R2B | BatchImageProcessorService/interface; BatchImageJob; Batch VM/XAML | new BatchCapabilityResult/DecodedImageMemoryGate; Program.cs |
| R2C | ScholarVectorIndexWriter/Reader; ScholarIndexService/interface; DocumentChatService/interface; ScholarKitViewModel; IndexModels | ScholarKitModels or new narrow generation model; new V0R2C_ScholarIndexConsistencyTests; Program.cs; Scholar runtime script; Scholar XAML only for real indexing state |
| R2D | VoiceCoordinator, Router, Transcriber, Speech, contracts, VoiceModels, DispatcherHelper; FlashcardsViewModel; App.xaml.cs DI | new speech playback backend/interface and V0R2D_VoiceIntegrationTests; AudioDeviceMonitor, ScholarKitVM/ShellVM only if required; W4 tests/Program |
| R2E | AppSettingsService/interface; Settings VM/XAML; Shell VM/XAML; CommandPaletteDialog; App.xaml.cs | MainWindow.cs; new CommandPaletteEntry/SettingsSchema/V0R2E_NavigationSettingsTests; Program/W4/UI script |
| R3 | Program.cs; W4 tests; test-winui-ui.ps1 | new verify-test-evidence.ps1; only adjacent WinUI QA scripts containing proven forced-pass patterns |
| RV | no product/test edit | separately authorized verification report only |
| R5 | only a user-selected and re-frozen subset | exact adjacent files resolved at that wave; never W5 or MaterialUI |

All Axora.Desktop paths above are under Axora-Desktop-WinUI/Axora.Desktop; test paths are under Axora-Desktop-WinUI/Axora.Desktop.Tests.

## 15. Proposed future commit boundaries

Commits remain local unless the user separately authorizes Git publication. Recommended one coherent commit after each accepted wave:

1. chore(toolchain): pin repository .NET 9 feature band
2. test(harness): add complete non-tautological execution ledger
3. fix(lifecycle): make startup failures fatal and DI ownership singular
4. fix(extensions): enforce artifact trust and staged recovery
5. fix(p2p): implement authenticated bounded peer sessions
6. fix(storage): add safe publisher for converter and compressor
7. fix(vault): version format and correct security/publication behavior
8. fix(batch): enforce capability, memory, collision, and process contracts
9. fix(scholar): publish immutable verified index generations
10. fix(voice): coordinate recognition, speech, and Flashcards
11. fix(settings): version persistence and make navigation/voice UI truthful
12. test(qa): remove forced passes and require semantic UI evidence
13. docs(verification): record V0-RV only if explicitly authorized

Do not squash different migration/security protocols together before review. Do not commit current W5 documents as part of V0.

## 16. Risks and rollback strategy

| Risk | Prevention | Recovery |
|---|---|---|
| shared publisher overclaims filesystem guarantees | typed result and documented fallback semantics | preserve backup and return RecoveryRequired |
| extension metadata is unavailable or changes | authoritative version-bound provenance; fail closed | keep detected working install; disable new install |
| P2P breaks mobile compatibility | explicit version negotiation and counterpart gate | do not ship protocol claim; no plaintext fallback |
| new Vault/settings/Scholar schema strands data | permanent legacy reader plus fixtures | retain old data/generation/backup and revert writer only |
| voice native state deadlocks/races | one coordinator, no long lock, generation tokens | cancel/join session and return explicit unavailable state |
| Batch exceeds 16 GB device memory | decoded-byte gate plus worker cap | cancel queued work; process one oversized item exclusively |
| test count changes are mistaken for regression | ledger and categorized accounting | compare group coverage/evidence, not magic totals |
| UI changes hurt accessibility/scale | semantic/a11y/theme/scale matrix | revert the isolated UI wave while retaining service fix if contract permits |
| convergence-file merge errors | sequential waves and narrow commits | return to prior accepted commit without resetting user work |

No destructive Git reset is part of recovery. Data fault injection uses disposable directories and synthetic fixtures.

## 17. W5 impact

After V0-RV passes, W5 may safely assume:

- repository commands share a reproducible .NET 9 feature band;
- build/test evidence has a complete manifest and no counted forced passes;
- host/DI lifecycle and unexpected-failure behavior are explicit;
- external processes are bounded and drained;
- external executable artifacts require verified identity;
- a tested single-file publication service exists for W5 outputs where one-file commit semantics fit;
- settings have schema, recovery, and safe publication;
- Vault, P2P, Scholar, Batch, Converter, Compressor, voice, and palette contracts no longer carry the identified blockers;
- MaterialUI and the four W5-P1 plans remain untouched.

W5 must still prove its own codec/capability matrix, metadata/multi-frame behavior, GPU/CPU/memory budgets, model/dependency optionality, undo/recovery, and output validation. It must not inherit unverified “all formats,” metadata preservation, universal atomicity, AI, or hardware-acceleration claims from V0.

## 18. V0 completion gate

V0 is complete only when:

- every accepted FIX BEFORE W5 finding is fixed and verified, or reclassified with evidence and explicit user approval;
- the authoritative build succeeds with no new unexplained warning;
- meaningful deterministic/integration tests pass and suite completeness is demonstrated;
- no tautological/forced-pass check is counted as functional evidence;
- required runtime and semantic UI smoke checks pass;
- legacy settings/Vault/Scholar data compatibility is demonstrated;
- extension trust, P2P authentication, destination preservation, and failure recovery tests pass;
- MaterialUI remains untouched;
- the four W5-P1 documents remain intact;
- Git state and all untracked artifacts are explicitly understood.

Passing this plan is not implementation authorization. The next action, if authorized by the user, is V0-R4—not V0-R1 and not W5-P2.
