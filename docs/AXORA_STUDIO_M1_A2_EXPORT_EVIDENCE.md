# AXORA Studio M1 A2-I1 deterministic engine evidence

Status: **I1-R1 repaired and verified; ready for targeted independent re-audit. Initial I1 audit BLOCKED progression.**

Chronology: initial I1 implementation and green tests -> independent I1 audit **BLOCKED** (HIGH ownership, MEDIUM ordering, LOW reporting) -> narrowly authorized R1 repair -> focused and full R1 verification. The initial implementation/results below remain historical evidence; the explicit R1 section records the correction and current results. This is not independent re-audit acceptance and does not authorize A2-I2.

This is **not complete A2 acceptance**. No export functionality is exposed to users. Native Save As, replacement consent UI, coordinator, composition, export UI, application shutdown ownership and Program settlement are deferred to separately authorized A2-I2. No physical Anki import or native picker/UI verification was performed.

## Authorization and baseline

Authority: the user-reviewed STUDIO-M1-A2-P0 contract and STUDIO-M1-A2-I1 implementation authorization supplied in `c1c98112-e8ad-460a-b8be-76f2c443e44c/pasted-text-1.txt`.

Before edits: branch `main`; HEAD and local `origin/main` both `430d781b67ee8f3b63d7bdb609c573774acf7db0`; tracked worktree clean; index empty; sole untracked item `Axora-Desktop-WinUI.zip`. The ZIP was not opened, hashed, extracted, modified, deleted or staged. Read-only `git ls-remote --heads origin refs/heads/main` subsequently confirmed that same published SHA.

Implementation/verification took place on 2026-10-03 Asia/Calcutta (verification logs use 2026-10-02 UTC). Repository SDK resolution was `9.0.318`, from the existing global.json patch-roll-forward policy. No project/package/toolchain changes were made.

## Exact I1 authored inventory

Seven new files:

1. `Axora-Desktop-WinUI/Axora.Studio/Models/FlashcardExportModels.cs`
2. `Axora-Desktop-WinUI/Axora.Studio/Services/Contracts/IExportFilePublisher.cs`
3. `Axora-Desktop-WinUI/Axora.Studio/Services/ExportFilePublisher.cs`
4. `Axora-Desktop-WinUI/Axora.Studio/Services/ExportFileNameSanitizer.cs`
5. `Axora-Desktop-WinUI/Axora.Studio/Services/FlashcardExportCodec.cs`
6. `Axora-Desktop-WinUI/Axora.Studio.Tests/FlashcardExportTests.cs`
7. `docs/AXORA_STUDIO_M1_A2_EXPORT_EVIDENCE.md`

Three modified files:

1. `Axora-Desktop-WinUI/Axora.Studio/ViewModels/FlashcardsViewModel.cs`: additive synchronous snapshot method only; original constructor/study/review/generation behavior preserved.
2. `Axora-Desktop-WinUI/Axora.Studio.Tests/Program.cs`: explicit third-group dispatch and required-case ledger registration.
3. `Axora-Desktop-WinUI/Axora.Studio.Tests/run-m1.ps1`: three-group manifest/ledger validation; assertion counts remain unfrozen.

Build outputs and diagnostic logs remain ignored. All authored work remains unstaged.

## Snapshot and suggestion contract

`CaptureExportSnapshot()` synchronously copies the current active deck into value-only records, with one clock observation for exportedAtUtc. The deck DTO owns a bounded read-only card collection. Strings are immutable; no FlashcardDeck, FlashCard, ObservableCollection or ViewModel reference is retained. It copies all authorized IDs, text, difficulty, count/ease/interval and nullable UTC history, preserving order. It invents no colorTag or review history.

Constructors validate nonzero N-format IDs, unique card membership, Unicode/scalar limits, UTC timestamps, A1 bounds, finite ease and coherent known review date pairs. Positive review counts with both dates unknown remain null, consistent with A1. Capture leaves active selection, lastStudied, reviews, current index, flip and Status unchanged. No active deck produces typed Rejected/NoActiveDeck without reading the clock. Empty decks are available. Capture is intended for the owning UI thread before any future await/dialog; cross-thread mutation of the session ViewModel is not introduced.

The pure suggestion helper validates UTF-16, normalizes NFC, replaces invalid/control runs with `_`, trims edge spaces/dots, prefixes reserved DOS stems including superscript COM/LPT variants, removes duplicate selected-format extensions and truncates on scalar boundaries. The complete suggestion is at most 120 UTF-16 units including exactly `.csv`, `.txt` or `.json`. The original title is never changed. Suggestions are not final-path authorization.

## Formats and independent validation

| Format | Frozen encoding and grammar | Exported values |
|---|---|---|
| CSV | Strict UTF-8 with BOM; exact `Front,Back,Difficulty,IntervalDays` header; CRLF records; every data field quoted; doubled quotes | Exact front/back, canonical difficulty, invariant integer interval |
| Anki text | Strict UTF-8 without BOM; LF headers/records; exact `#separator:Tab`, `#html:false`, `#columns:Front` + actual TAB + `Back`; two quoted TAB-separated fields | Exact front/back only, with embedded tabs/newlines and literal HTML preserved |
| AXORA JSON | Strict UTF-8 without BOM; stable camelCase property order; explicit schema v1; seven-fraction UTC `Z` timestamps and explicit nulls | Complete immutable deck/card snapshot |

CSV formula-like prefixes are preserved. Future I2 wording must explain importing Front/Back as text columns; I1 makes no spreadsheet execution-control claim. Empty CSV/Anki decks produce headers only. Anki is a documented text format, not an `.apkg` or verified physical import; future UI must retain the unverified-import wording required by P0.

JSON envelope: `format="axora.flashcards"`, integer `schemaVersion=1`, `exportedAtUtc`, `deck`. Deck properties: `deckId`, `title`, `description`, `lastStudiedUtc`, `cards`. Card properties: `cardId`, `front`, `back`, `difficulty`, `reviewCount`, `easeFactor`, `intervalDays`, `lastReviewedUtc`, `nextReviewUtc`. No extra metadata or colorTag.

Writers stream to caller-owned streams. CSV/Anki use a 64 KiB text buffer; JSON flushes after each bounded card. Independent readers parse quote/delimiter/newline grammar and compare actual field values to the snapshot. JSON uses an incremental Utf8JsonReader with a fixed 128 KiB token buffer and maximum depth 8; it never loads a whole production JSON DOM. Property sets reject duplicate, unknown and missing names at each level; types, integer version, IDs, timestamps, finite/bounded values, card order and full snapshot equality are checked. Reader expectations are not manufactured by calling the writer.

Both writers/readers enforce a 64 MiB byte ceiling. Existing replacement files have that same inspection ceiling. A 500-card artifact with both fields containing 4096 supplementary Unicode scalars measured **49,243,714 bytes**, and streamed reopening/validation passed. This is a size-bound observation, not a latency or peak-working-set benchmark. Per-file hashing buffers are 64 KiB; validation owns at most one card/token plus fixed buffers. No giant complete artifact string or full production JSON DOM exists.

## Destination support and protections

Supported: validated ordinary absolute drive paths on local fixed NTFS volumes, with existing parent directories and a matching format extension. Final path validation is independent of suggestions. It rejects unsupported/device/UNC/relative forms, ADS syntax, invalid components/reserved names, trailing spaces/dots, directory targets, traversed or final reparse points, hard-linked files, read-only targets, offline/recall-required attributes and failed identity/permission inspection. It enforces component lengths and reserves native full-path space for generated sibling stage/recovery names.

Volume type/filesystem are inspected through Windows APIs. Every parent ancestor is opened without following reparse points and checked. Handle-derived canonical paths and volume/file identities bind plans to the inspected parent and target. Canonical protected roots include the entire Studio operational root, current application base and current executable directory; comparison uses directory boundaries, not naive string prefixes. Protected-root aliases are resolved through handles, including existing final-root redirection. Known operation-owned stage/recovery identities are rejected. Parent/recovery directory handles pin their identities during publication.

Target attributes are inspected before requesting content access; OPEN_REPARSE_POINT/OPEN_NO_RECALL are used, and unsafe attributes are rejected before hashing. Only native ERROR_FILE_NOT_FOUND establishes absence; access denied, sharing violations, ERROR_PATH_NOT_FOUND or failed inspection never establish absence.

Runtime fixture volume was C: fixed NTFS; the machine also reports D: fixed NTFS. No network/removable/non-NTFS volume was available, so those rejection branches have source/API evidence but no mounted-volume runtime PASS. Actual offline-attribute, ACL denial, hardlink, protected-root short-name alias and sharing cases ran. Symbolic-link fixture creation returned Windows privilege error 1314; symlink/final-reparse and traversed-link runtime cases are **ENVIRONMENT-NOT-AVAILABLE**, not runtime PASS. Their rejection logic remains present; the user permits environment-dependent hardlink/reparse coverage.

Fully resident ordinary files inside a synchronized local folder receive only local, point-in-time guarantees. No cloud synchronization/version guarantee is made. Path-based File.Move/File.Replace cannot provide universal compare-and-swap protection against external/malicious same-user writers; backup verification detects tested final-window mismatches and reports them truthfully.

## Initial I1 publication, results and ownership (historical; corrected by R1 below)

`Prepare` is read-only and returns an immutable plan with format, canonical destination, parent identity and either definite absence or exact existing fingerprint (canonical path, volume/file ID, length, last-write and streamed SHA-256). Existing plans start unapproved. `ApproveReplacement()` preserves the exact fingerprint; I1 deliberately supplies no user-consent UI. Unapproved replacement is rejected without staging.

`PublishAsync` returns/owns the complete worker task through verification, recovery metadata, cleanup and handle release. It does not pass cancellation to Task.Run in a way that would skip settlement. The constructor performs no destination/stage/recovery work; production fault callbacks default to null. This engine is not registered in the host and is not called from UI.

New-file path: revalidate plan; unpredictable sibling CreateNew stage; stream with byte ceiling; flush writer and FileStream.Flush(true); close; reopen and independently check grammar, snapshot, identity, length and hash; recheck parent/protection/absence and stage; admit commit; File.Move(stage,destination,overwrite:false); reopen and verify destination against the validated artifact. An appearing destination is never reinterpreted as overwrite permission.

Replacement path: exact approved fingerprint required; stage/validation as above; exclusively create an unpredictable sibling recovery directory using CreateDirectoryW; choose an absent `displaced.original` child; pin/revalidate ownership and final target fingerprint; File.Replace(stage,destination,uniqueBackup,ignoreMetadataErrors:false). No truncation, delete-before-move, copy-overwrite fallback, metadata-error suppression or downgrade is used. Stage, destination and recovery are siblings on the validated volume.

After replacement, inspect the backup and require it to match the displaced approved identity/content/time/length before ordinary success. Independently verify the new destination against the stage fingerprint and snapshot. Success deletes only the exact verified backup and exact owned stage by identity-bound handle disposition; owned directories are removed only when empty. There are no sweeps, recursive production deletes or automatic restoration over a modified destination.

Ambiguity/partial publication/post-verification failures retain relevant recovery artifacts and expose locations in the result for the future UI. Retained replacement recovery includes a CreateNew `recovery.json` note capped at 4 KiB, with operation ID, format, terminal state/reason and artifact basenames, excluding card text, deck title/description and hashes. No production content/path logger or telemetry was added. Cleanup failure after proven success remains Published with warnings; changed cleanup identities are never deleted.

| Terminal state | Meaning |
|---|---|
| Published | New destination and applicable displaced backup verified; cleanup may carry warnings |
| Canceled | Cancellation accepted before admission; publisher does not publish |
| Rejected | Unapproved replacement or unavailable/invalid snapshot |
| InvalidDestination | Unsupported/protected/denied/unknown inspection |
| DestinationChanged | Precommit baseline mismatch; no publisher publication |
| SerializationFailed | Stage write/serializer/durable-flush boundary failed |
| StageValidationFailed | Stage grammar/content/size/identity check failed |
| PublicationFailed | Exception inspection proves no committed change |
| PublishedButVerificationFailed | Publication is known to have happened, but completion verification failed |
| Indeterminate | Late displaced mismatch, partial state or inspection ambiguity prevents a safe conclusion |

Results separately record commit admission and actual native attempt, safe reason codes, observed artifact states, recovery paths and cleanup warnings. A Move/Replace exception is inspected against known destination/stage/backup state; it does not by itself prove that nothing changed. Unknown inspection is never compared as definite absence. Tests exercise genuine native sharing exceptions and actual partial file moves; they do not claim physical disk failure or Windows error 1177 was naturally reproduced.

## Cancellation and fault seams

Before admission, cancellation checks run through writing, hashing, validation and final rechecks. An instance-local Interlocked gate arbitrates the cancellation callback against commit admission. Once admission wins, publication/inspection/recovery/cleanup use no caller cancellation; the complete task remains live until settlement. A deterministic barrier proves cancellation does not prematurely complete an admitted worker awaiting verification. No App/Program timeout or shutdown changes are included in I1.

The dormant instance callback receives only a typed boundary and operation paths. Boundaries: StageOpened, AfterStageWrite, BeforeDurableFlush, BeforeStageValidation, AfterStageValidation, BeforeDestinationRecheck, CommitAdmitted, AfterNativePublication, BeforeFinalVerification, Cleanup. Tests use these to throw boundary I/O faults, modify/move test-owned files or coordinate barriers. No global static switches, environment-variable switches, watchers, sleeps, replacement API fakes or forced-success paths exist. Real native APIs remain the only publishers.

## Initial I1 deterministic and regression verification (historical)

`STUDIO-M1-EXPORT` registers 39 named required cases. Manifest/ledger required-case sets detect missing, duplicate or unknown cases/groups; assertion counts are observations, not pass gates. H0/A1 tests and required-case sets were not modified. Unknown group and malformed multi-argument calls both returned exit 2 and printed that no tests ran. Manifest lists exactly H0, Flashcards and Export.

Final command: `Axora-Desktop-WinUI/Axora.Studio.Tests/run-m1.ps1`.

Final combined log: `Axora-Desktop-WinUI/Axora.Studio.Tests/logs/a2-i1-final-regression.log`.

| Group | Required cases | Passed assertions observed | Failed/missing/duplicate/unknown/blocked | Child exit |
|---|---:|---:|---|---:|
| STUDIO-H0 | 21 | 160 | all zero | 0 |
| STUDIO-M1-FLASHCARDS | 22 | 172 | all zero | 0 |
| STUDIO-M1-EXPORT | 39 | 175 | all zero | 0 |

Each final child has registeredGroups=1, executedGroups=1, totalRegisteredGroups=3, complete=true and disposition=Pass. Runner exit 0. Final per-group logs use `m1-20261002T191705699-{group}.stdout.log` and `.stderr.log` in that logs directory. No forced termination occurred. A subsequent process observation found zero Studio/Test processes.

Coverage mapping:

| Area | Named cases / concrete evidence |
|---|---|
| Snapshot | snapshot-unavailable, snapshot-values, snapshot-isolation, snapshot-validation: empty/order/one clock/no mutation/full copying and post-capture session changes |
| Filename | filename: invalid/control runs, empty/unsafe title, DOS/superscripts, duplicate suffix, NFC, Unicode/scalar truncation |
| CSV | csv-roundtrip, csv-reject: exact independent multiline grammar, BOM/header, commas/quotes/tabs/Unicode/empty/formula fields, malformed rows |
| Anki | anki-roundtrip, anki-reject: exact headers/two fields, quoted tabs/newlines, literal HTML/Unicode/empty and malformed input |
| JSON | json-schema, json-reject: explicit full schema/order/timestamps/null history, duplicate/unknown/missing properties, types/version/IDs/history/nonfinite/bounds |
| Limits | codec-size-memory, oversized-stage-target: near-maximum valid artifact, streaming 64 MiB reader limit, oversized stage and replacement rejection |
| Success/consent | publish-new, publish-empty-formats, publish-existing, replacement-unapproved |
| Destination changes | destination-appears, destination-changes, destination-disappears: appearance, changed identity/hash with same length/time, no implicit overwrite/downgrade |
| Final window | final-window-backup: actual late-writer content retained; truthful Indeterminate result and bounded recovery note |
| Permissions/write | locked-readonly-denied, write-flush-fault: actual sharing/read-only/ACL denial and narrow write/flush fault boundaries |
| Stage identity | stage-validation-fault, stage-identity-swap: malformed/changed staged bytes and identity; outsider stage preserved |
| Exception state | publication-no-change, native-publication-exception, publication-partial, publication-ambiguous: before-change seams, actual Move/Replace sharing failures, real partial moves, unknown locked post-state and post-native exception inspection |
| Post verification | post-verify-fault: new and replacement output failure, applicable original retained |
| Cancellation | cancel-precommit, cancel-postadmission: deterministic boundaries, original unchanged before admission, admitted task retention and full settlement |
| Cleanup | cleanup-canary, cleanup-warning, cleanup-identity-swap: exact cleanup, unrelated canary, verified backup removal, retained backup/warnings, identity-bound refusal |
| Policy | protected-boundaries, unsupported-paths, hardlink-reparse: canonical roots/aliases/sibling boundary, device/UNC/ADS/components/extensions/missing parents/directory/offline/hardlinks; symbolic-link privilege limitation above |
| Idle | publisher-idle: constructor creates no operational root/artifacts and invokes no fault callback; unchanged host composition/A1 laziness tests |

Initial restricted test runs could not inspect Windows ancestor directories and failed with access denied. Authorized filesystem tests were rerun outside that sandbox through automatic approval review. This is an execution-environment limitation, not a policy fallback. Early iterations also revealed and fixed InvalidDataException catch coverage, test-fixture ACL restoration and input trailing-space validation. Only final logs above establish the final green result; failed iteration logs remain ignored diagnostics.

## Initial I1 builds (historical)

Commands, SDK 9.0.318, Debug/x64, existing restored dependencies:

```powershell
dotnet build Axora.Studio/Axora.Studio.csproj --no-restore -c Debug -p:Platform=x64 -v:minimal
dotnet build Axora.Studio.Tests/Axora.Studio.Tests.csproj --no-restore -c Debug -p:Platform=x64 -v:minimal
```

Final Studio build: exit 0, **0 errors, 8 MVVMTK0045 warnings**, 12.40 seconds. Those existing field-backed ObservableProperty warnings occur in ShellViewModel (1), SettingsViewModel (4), FlashcardsViewModel (3); no new observable fields were added. Log: `Axora-Desktop-WinUI/Axora.Studio.Tests/logs/a2-i1-studio-final-build.log`.

Final Tests build: exit 0, **0 errors, 0 warnings** in that incremental invocation, 3.76 seconds; Studio was already built. Log: `Axora-Desktop-WinUI/Axora.Studio.Tests/logs/a2-i1-tests-final-build.log`. This does not claim the Studio warnings disappeared or release/AOT validation occurred.

## Protected-area and Git audit

Only the exact seven-new/three-modified inventory is authored. Production App, Program, MainWindow, StudioLifecycle, HomePage, FlashcardsPage, StudioBootstrap and HostTests are unchanged. A1 models/review/generator/test suite and `docs/AXORA_STUDIO_M1_FLASHCARDS_EVIDENCE.md` are unchanged. Legacy Axora.Desktop, MaterialUI, Tools/Mind/W5, project/package/toolchain files are unchanged. No picker interface, COM dialog, coordinator, UI controls or shutdown integration was created.

Final HEAD/local origin/main remain `430d781b67ee8f3b63d7bdb609c573774acf7db0` on main. Index empty; changes unstaged; ZIP still the unrelated untracked item. No git add, commit, push, merge, rebase, reset, restore, clean, stash, amend, branch or tag operation occurred. `git diff --check` passes; native line-ending normalization warnings are configuration notices, not whitespace errors.

## Requirement completion map

| I1 authorization items | Evidence |
|---|---|
| 1 baseline | Pre-edit Git baseline and read-only actual remote verification |
| 2 authoritative contract | Snapshot/formats/safe publication remain within accepted P0 deterministic engine scope |
| 3-5 inventory/exclusions | Exact seven-new/three-modified inventory; protected-area diff audit |
| 6-7 immutable/consistent snapshot | DTO constructors, synchronous method, four snapshot cases |
| 8 results | Typed format/result/admission/attempt/observation/recovery models |
| 9 suggestions | Pure sanitizer and filename case |
| 10-13 codecs | Streaming writer/independent reader source, roundtrip/schema/rejection/maximum cases |
| 14 destination | Canonical local fixed NTFS policy, protected/unsupported/permission/identity tests and explicit environment limits |
| 15-16 publication | CreateNew stage, durable flush, independent stage/output checks, no-overwrite Move and fingerprint-bound Replace tests |
| 17 recovery | Exclusive container, verified displaced backup, exact handle cleanup, retained artifacts/note and warning cases |
| 18 partial outcomes | Native exception inspection and real partial/ambiguous fixture cases |
| 19 cancellation | Atomic gate, pre/post-admission cases and pending-worker barrier |
| 20 fault seams | Typed instance-scoped dormant callbacks; genuine filesystem mutations and native publishers |
| 21-23 test registration/regression | Three complete ledgers, H0/A1 unchanged, negative dispatch checks; no native UI verification |
| 24 builds | Two zero-error final SDK 9.0.318 Debug/x64 logs with warnings recorded |
| 25 evidence | This I1-only document; historical A1 evidence untouched |
| 26-28 protections/Git/stops | No excluded path or feature added; no Git mutation or scope expansion required |
| 29 final report | A-Z report returned in the calling chat; I2 not started |

## I1-R1 independent-audit findings and safety repair

Repair authority: STUDIO-M1-A2-I1-R1, attachment `fe5e5a87-83ef-441e-8a71-4a2a0887b06a/pasted-text-1.txt`, 2026-10-03. The independent I1 audit **BLOCKED** progression despite the initial green suite; those original tests missed the unsafe paths. R1 changes only ExportFilePublisher.cs, FlashcardExportTests.cs and this evidence file. No public model/interface, test Program/runner, project/package, UI, picker, coordinator, host composition or lifecycle change was required.

### Accepted findings and exact corrections

- **HIGH: backup ownership.** Initial Finish cleanup could delete a file at the reserved backup path using the approved original's identity/fingerprint even when native publication had never run. Another actor moving the original into that path before the destination recheck could therefore lose the last original. Matching content/identity was incorrectly sufficient for cleanup.
- **MEDIUM: final recheck ordering.** The initial final destination check preceded another full stage validation/hash/grammar pass. A destination mutation at that validation's completion could reach publication instead of being rejected precommit.
- **LOW: unavailable accounting.** The symbolic-link privilege failure printed an unavailable observation but also used `That(true)` to increment passing assertions. R1 removes only that assertion; the branch remains informational and explicitly unverified. The actual hardlink assertion remains separate. No shared runner work was needed.

The publisher now has private, per-operation backup provenance, separate from fingerprint equality: no backup observed; unexpected before native invocation; replacement attempted; observed after native success; ambiguous after publication exception; successful replacement verified. Successful cleanup requires File.Replace to have returned success, admission and actual attempt, Published destination verification, the successful replacement provenance, the exact owned/canonical pinned recovery container and child location, and the approved original fingerprint/identity. Cleanup revalidates those location/provenance conditions after its fault seam and pins the recovery container through DeleteOwned. DeleteOwned itself is unchanged: exact handle identity, unsafe-attribute/link checks, optional exact fingerprint, synchronous handle disposition, empty-only directory deletion and no sweeping.

Every precommit exit checks backup presence without caller cancellation before settlement. A present or not-definitely-absent unexpected backup is retained/disclosed regardless of identity/hash; its owned container is retained with a bounded recovery note, while the exclusively created stage is cleaned by its exact identity. Missing original produces DestinationChanged; occupied backup with unchanged destination produces InvalidDestination; cancellation delivered at the precheck seam produces Canceled. None admits or attempts native publication. An exception-path backup is never promoted to cleanable provenance through equality; destination, stage and backup inspections determine PublicationFailed, PublishedButVerificationFailed or Indeterminate with applicable retained recovery. No automatic restoration occurs.

Current order: exclusive stage creation/write/flush/close -> full reopen/fingerprint/grammar/snapshot validation, including the final repeat validation -> recovery-container exclusive preparation -> final destination canonical/protection/volume/parent/target fingerprint recheck and owned-container/definite-backup-absence check -> cancellation/admission CAS -> synchronous native Move/Replace -> post-inspection/verification/recovery/cleanup. No full stage validation/hash/parse follows the final destination recheck. The new dormant, instance-scoped finalStageValidated completion seam lives only inside ExportFilePublisher and runs before recovery preparation/final destination recheck; it replaces no filesystem API and introduces no global/model switch. The existing CommitAdmitted seam remains the specifically positioned final-window race seam. A short container/backup-absence check immediately after that seam also prevents passing a known newly occupied backup to File.Replace. It performs no full hash/grammar work and honors post-admission settlement without cancellation.

The cancellation gate is unchanged: cancellation wins before admission or commit admission wins and settlement completes without honoring subsequent caller cancellation. Universal external-writer filesystem compare-and-swap is **not promised**. A writer racing after the final inspection remains subject to native Replace semantics, displaced-backup verification, destination verification and truthful ambiguity retention. The existing late-writer mismatch test remains green.

### Deterministic regression evidence

All original 39 mandatory identities remain registered/executed. Five added required identities:

| Case | Deterministic production-path evidence |
|---|---|
| precommit-backup-original | BeforeDestinationRecheck moves the approved original to the reserved backup; DestinationChanged, zero admission/attempt, exact original bytes/SHA-256 preserved, backup disclosed, owned stage cleaned |
| precommit-backup-cancel | Same unexpected moved original plus cancellation at the precheck seam; Canceled, zero admission/attempt, original retained/disclosed, truthful absent destination/present backup, stage cleaned |
| precommit-backup-occupied | Unchanged approved destination plus unrelated backup occupant; InvalidDestination before admission, both byte sets intact, recovery disclosed and stage cleaned |
| final-stage-destination-change | At final validation completion, independently validate the full produced stage and confirm still-approved destination, then change destination; subsequent final recheck returns DestinationChanged with zero admission/attempt, actor bytes untouched and stage/empty recovery cleaned |
| final-window-backup-occupied | Occupy backup at the existing CommitAdmitted seam; immediate absence guard prevents native invocation, Indeterminate retains stage/occupant without changing approved destination |

No sleeps/timing races or native publisher fakes. Before repairing ordering/cleanup, the first four regressions ran against the original engine with only the narrow completion seam added. Log `Axora-Desktop-WinUI/Axora.Studio.Tests/logs/a2-i1-r1-red-export.log` records **exit 1, 8 failed assertions**, including lost original/recovery disclosure and the final-validation mutation reaching publication. Thus the MEDIUM regression actually failed against the pre-repair ordering. This deliberately failing diagnostic is not a completion result; the subsequent final logs establish the repair result.

Focused verification used the existing full STUDIO-M1-EXPORT group, preserving Program/runner contracts. It includes all requested focused paths: normal new/replace and successful backup cleanup, actual native sharing exceptions, partial/ambiguous publication, late backup mismatch, pre/post-admission cancellation and pending-worker settlement, identity-swap/canary cleanup, plus all added regressions. Final focused log: `Axora-Desktop-WinUI/Axora.Studio.Tests/logs/a2-i1-r1-focused-export.log`; **44 required cases, 192 passing assertions, zero failures/missing/duplicate/unknown/blocked, complete=true, exit 0**.

Then the unchanged `run-m1.ps1` ran all three groups. Combined log: `Axora-Desktop-WinUI/Axora.Studio.Tests/logs/a2-i1-r1-full-regression.log`. Per-group logs: `m1-20261003T153221896-{group}.stdout.log` / `.stderr.log` in that logs directory.

| Group | Required cases | Current passed assertions | Failed/missing/duplicate/unknown/blocked | Child exit |
|---|---:|---:|---|---:|
| STUDIO-H0 | 21 | 160 | all zero | 0 |
| STUDIO-M1-FLASHCARDS | 22 | 172 | all zero | 0 |
| STUDIO-M1-EXPORT | 44 | 192 | all zero | 0 |

All final ledgers: registeredGroups=1, executedGroups=1, totalRegisteredGroups=3, complete=true, disposition=Pass. Runner exit 0; all three stderr logs are zero bytes. No forced termination; subsequent process check found no Studio/Test process. Unknown-group and malformed extra-argument dispatch still return **2**, explicitly with no tests run. Assertion totals are observations: the initial 175 included the unavailable-privilege assertion; current totals remove that one and include the new real assertions.

### R1 rebuilds and scope settlement

SDK resolution freshly confirmed **9.0.318**. Explicit rebuild commands use existing restored dependencies only:

```powershell
dotnet build Axora.Studio/Axora.Studio.csproj --no-restore -t:Rebuild -c Debug -p:Platform=x64 -v:minimal
dotnet build Axora.Studio.Tests/Axora.Studio.Tests.csproj --no-restore -t:Rebuild -c Debug -p:Platform=x64 -v:minimal
```

Studio: **exit 0, 0 errors, 8 existing MVVMTK0045 warnings**, 16.59 seconds; `Axora-Desktop-WinUI/Axora.Studio.Tests/logs/a2-i1-r1-studio-build.log`. Tests: **exit 0, 0 errors, 8 existing Studio MVVMTK0045 warnings from rebuilding its dependency**, 22.51 seconds; `Axora-Desktop-WinUI/Axora.Studio.Tests/logs/a2-i1-r1-tests-build.log`. No new warnings/project/package changes; no Release/AOT claim.

Before/after SHA-256 comparison of the exact ten I1 authored paths confirms only the three R1 allowlisted files changed. The other seven, including public export models/interface, codecs/sanitizer, ViewModel, test Program and runner, are unchanged from pre-repair. No new authored path: original seven-new/three-modified inventory above remains exact, plus the excluded unrelated ZIP by Git name only. Production App/Program/MainWindow/Home/FlashcardsPage/Bootstrap/HostTests, legacy Desktop/MaterialUI/W5, A1 historical evidence, package/project/toolchain and lifecycle paths remain outside the authored diff. No A2-I2 file or integration was added.

main; HEAD/local origin/main remain `430d781b67ee8f3b63d7bdb609c573774acf7db0`; index empty; all work unstaged; no Git mutation/publication. Build outputs and logs remain ignored. ZIP was never opened/hashed/extracted/modified/deleted/staged. Whitespace checks pass. No unexpected repair finding or repair blocker remains; targeted independent re-audit is still required.

Remaining environment gaps: symlink privilege error 1314 (explicitly **ENVIRONMENT-NOT-AVAILABLE**, no passing assertion), unavailable mounted network/removable/non-NTFS fixtures, physical Anki import, native Save As/UI/coordinator/lifecycle verification, naturally occurring partial Replace/disk-failure variants. Actual hardlink, native sharing-fault and deterministic partial/publication tests are distinguished from unavailable observations. No universal filesystem CAS or cloud/version-synchronization guarantee was added.

## Remaining A2-I2 and acceptance gates

Separately authorize native path-only Save As with HWND/STA ownership and nonmutation proof, explicit fingerprint-bound replacement consent UI, single-operation coordinator, lazy existing-host composition, export controls/status/recovery wording, Home wording, App pre-H0 settlement gate and Program fallback settlement. Extend tests for those ownership/modal/lifecycle contracts and perform the authorized native UI checks. Retain truthful unverified Anki wording unless a separately authorized physical import succeeds. I1 does not fulfill those gates.

Eventual A2 feature checkpoint still requires I1 + I2, complete verification, independent audit and user acceptance, followed by separate Git authority. Existing A1 behavior remains the published fallback. No user output/recovery artifact is an automatic rollback-deletion target.

## Official technical basis

Windows volume/file identity comparison follows [GetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfileinformationbyhandle); identity-bound deletion uses [SetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle). Reparse/no-recall metadata handling follows [CreateFileW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew). Publication exception/recovery interpretation respects [ReplaceFileW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew), without assuming an exception implies unchanged paths. Anki quoting, two-column text and header choices follow the [official Anki text import manual](https://docs.ankiweb.net/importing/text-files.html); actual import remains NOT-RUN.

Historical initial I1 verdict was **STUDIO-M1-A2-I1 PASS — READY FOR INDEPENDENT EXPORT-ENGINE AUDIT**; the subsequent independent audit **BLOCKED** progression. R1 addresses those accepted findings without concealing that history.

**Current repair verdict: STUDIO-M1-A2-I1-R1 PASS — READY FOR TARGETED RE-AUDIT.** A2-I2 remains unstarted and separately authorized.

## I1-R2 reservation hardening and strict gate (2026-10-03)

The R1 targeted independent audit BLOCKED on regression quality: `precommit-backup-occupied` also passes pre-R1. It also identified the residual absent-backup-slot window. This chronology remains intact. The continuation authority is attachment `431c0ffb-0656-4b2c-8a2a-ae4a1280a23f/pasted-text-1.txt`.

The corrected standard is **at least one discriminating regression per defect**. Supplementary invariant tests may pass both implementations. Defect A (unowned moved-original deletion) is discriminated by `precommit-backup-original`; defect B (late stage-validation destination mutation) by `final-stage-destination-change`. Their saved R1 red run has eight failures. `precommit-backup-occupied` remains useful supplementary safety coverage rather than evidence that it distinguishes defect A. R2's new owned-slot invariant is discriminated by `backup-reservation-required`: run against unchanged R1 production, it produced exactly three failures and exit 1 (`a2-i1-r2-red-export.log`).

R2 modifies only ExportFilePublisher.cs, FlashcardExportTests.cs and this document. Fresh before/after SHA-256 comparison confirms the other seven I1 authored paths are identical. HEAD/local origin/main remain 430d781b67ee8f3b63d7bdb609c573774acf7db0 on main; index empty; authored inventory still seven new and three modified. ZIP excluded by name only and untouched.

### Reservation and cleanup state machine

After full stage write/flush/close and independent validation, the publisher exclusively creates an unpredictable recovery directory and an unpredictable `displaced-{guid}.original` child with FileMode.CreateNew. That empty placeholder contains no academic bytes. It records creation provenance and an exact handle-derived identity/fingerprint, even if cancellation just won, then closes the placeholder. Final destination validation precedes final recovery-container/canonical-path/reservation-identity/zero-length/time/attribute/link validation and cancellation/admission CAS. A short metadata-only reservation recheck follows the dormant CommitAdmitted seam; no long artifact work occurs after the final checks before native publication.

The production native File.Replace consumes the existing owned slot with ignoreMetadataErrors:false. Microsoft documents that an existing backup file is replaced, and the production-path test physically verifies this behavior. Native success, placeholder identity and displaced-original fingerprint are separate facts. Successful promotion additionally requires backup identity to differ from the placeholder and match the approved original, stage absence, and full new-destination validation. Fingerprint equality without this native-success provenance never authorizes original-backup deletion.

| Cleanup branch | Production authority and tests |
|---|---|
| Owned stage | Exact handle identity; stage-identity-swap and cleanup-canary preserve outsiders |
| Owned unchanged placeholder; canceled/no native change | Exact recorded identity and empty fingerprint; reservation-cancel and actual native sharing exception paths |
| Unexpected occupant before CreateNew | Creation fails closed; reservation-creation-occupied retains/discloses occupant and cleans owned stage |
| Externally replaced or modified placeholder | Identity/fingerprint fail; reservation-identity-swap, precommit-backup-occupied and final-window-backup-occupied retain outsiders |
| Successful replacement plus verified transformation/output | Exact approved-original identity/fingerprint, native success and verified provenance; backup-reservation-required and publish-existing |
| Native exception with old original at backup | Never promoted; publication-partial and post-native exception retain original |
| Native exception with unknown/ambiguous state | Inspection-driven Indeterminate; publication-ambiguous retains recovery |
| Output verification failure | PublishedButVerificationFailed; post-verify-fault retains recovery |
| Cleanup failure after verified success | Published with warnings/recovery; cleanup-warning and cleanup-identity-swap |

Original mandatory cases remain. Moved-original/partial-move actor fixtures now explicitly replace the owned placeholder; they still exercise actual production cleanup and genuine filesystem moves. The former final-window occupant fixture now modifies the owned placeholder and is detected by its final zero-length/identity check. All callbacks are instance-scoped and null in normal production.

**Residual platform limit:** no universal filesystem CAS is claimed. A deliberate same-user actor can delete/move/replace the placeholder after its final check and before File.Replace. `reservation-postcheck-race` deterministically exercises that exact residual seam: the native call overwrites the injected occupant with the approved original and the verified operation can return Published. This observation is evidence of the limitation, not a safety PASS against such an actor. An ordinary CreateNew collision with an absent random slot is removed because the slot already exists and is operation-owned. The directory/slot must still be protected from deliberate same-user interference during the final pathname operation.

### Verification and fresh adversarial review

SDK 9.0.318; explicit Debug/x64 --no-restore -t:Rebuild for Studio and Tests: both exit 0, zero errors and eight existing MVVMTK0045 warnings each (dependency rebuild included). Logs: `a2-i1-r2-studio-build.log`, `a2-i1-r2-tests-build.log`. Focused export run: 50 mandatory cases, 207 assertions, zero failures, exit 0 (`a2-i1-r2-focused-export.log`); the final rebuilt suite again passes Export 207, H0 160 and A1 172 (`a2-i1-r2-full-regression.log`). All ledgers complete, one execution per group, zero missing/duplicate/unknown/blocked. Unknown group and malformed extra argument both exit 2. Assertion totals remain observations. Symlink privilege remains explicitly ENVIRONMENT-NOT-AVAILABLE without a passing assertion. Mounted unsupported-volume fixtures and natural disk/partial-native failures remain unavailable.

Fresh hostile review traced all ten P1.23 invariants: no content-only cleanup authority; owned slot instead of absence; precheck identity swap blocks admission; verified-native transition required for displaced-backup deletion; exceptions never promote; metadata-only final reservation checks; atomic precommit cancellation; noncancelable post-admission settlement; exclusive handle deletion of exact identity; truthful residual TOCTOU disclosure. No BLOCKER/HIGH/MEDIUM finding remains within this stated threat boundary. Protected files and the other seven authored hashes are unchanged; no I2 change occurred before this gate.

**STUDIO-M1-A2-I1-R2 STRICT GATE PASS.** The same continuation authorizes automatic progression into I2; complete A2 remains subject to implementation, deterministic/native verification and independent full-feature audit. No staging/commit/push.

## I2 native integration, verification and acceptance gap (2026-10-03)

I2 began only after the R2 gate above passed, under the same `431c0ffb-0656-4b2c-8a2a-ae4a1280a23f` continuation. Earlier statements that I2 was unstarted describe their historical checkpoints; they are preserved rather than rewritten. This section records the implemented integration, final rebuilt deterministic suite, actual native observations and the remaining mandatory acceptance gap.

**Current disposition: I2 BLOCKED on incomplete physical close-during-picker verification (P2.30).** Save As cancel/new-file/replacement/destination-change observations succeeded, and the deterministic ownership/lifecycle tests pass. They do not establish the missing native close journey. Computer Use was stopped by the user with the physical Escape key before inspecting the final rebuilt launch. No further app input was issued during evidence reconciliation. The last read-only process check still found isolated Studio PID **32808** running; consequently zero lingering processes is not an acceptance claim. No forced kill was used.

### Final implementation and source ownership

`IStudioSavePicker` is a Studio-only typed contract: Selected, Canceled or Failed; selection, replacement confirmation and active cancellation are separate methods. `StudioSavePicker.cs:12` specifies filesystem-only, NOTESTFILECREATE, PATHMUSTEXIST, NOCHANGEDIR, STRICTFILETYPES and DONTADDTORECENT flags, without FOS_OVERWRITEPROMPT. Settings provide one format-specific filter, matching default extension and sanitized suggestion. The picker has no destination write/create/copy/delete operation; the publisher owns all publication.

The adapter captures the existing UI STA DispatcherQueue only when its lazy factory is invoked. Each operation validates the supplied current HWND using IsWindow and apartment/thread access, queues native creation/Show on that STA, registers its one active dialog and token cancellation, maps HRESULTs, extracts a filesystem path and unregisters/releases before settling its task (`StudioSavePicker.cs:38-115`). RequestCancel queues Close(ERROR_CANCELLED) onto that same STA and checks dialog reference identity so a stale request cannot close a later operation. A failed Close/enqueue retains operation ownership until Show actually returns. Adapter tests cover owning-STA routing, selected/canceled/failed terminals, extraction/release failures, exactly-once release, active registration and stale cancellation. A real OS Close failure was not injected.

Native Save uses the exact FileSaveDialog CLSID **C0B4E2F3-BA21-4773-8DBA-335EC946EB8B** and IFileSaveDialog IID **84BCCD23-5FDE-4CDB-AEA4-AF64B83D78AB**. The implementation directly owns the CoCreateInstance reference; SDK-verified vtable slots are Show 3, SetFileTypes 4, SetFileTypeIndex 5, SetOptions 9, SetFileName 15, SetTitle 17, GetResult 20, SetDefaultExtension 22 and Close 23. IShellItem::GetDisplayName slot 5 uses SIGDN_FILESYSPATH 0x80058000. Path memory is freed with CoTaskMemFree, shell-item and dialog references are released exactly once, and constructor failure releases any created dialog. Signatures/order were compared with Windows SDK 10.0.26100.0 `um/ShObjIdl_core.h` (IFileDialog 19686, IFileSaveDialog 20026, IShellItem 8867; FileSaveDialog coclass 29350). This is SDK/source and bounded runtime evidence, not proof for every OS/COM fault.

Owner-bound replacement uses explicit Replace/Cancel buttons with Cancel as default. A fresh local operation Guid, canonical destination and immutable existing-file fingerprint remain associated with the same coordinator plan; consent is never cached or transferable to a subsequent operation. The final publisher recheck independently rejects a changed destination. TaskDialogIndirect definitions, callback, configuration and Pack=1 structures follow SDK `CommCtrl.h`; cancellation posts the Cancel button on its owning STA. The installed Windows v6 common-controls assembly is activated for that STA call only, then its DLL, activation cookie and context handle are released. No project/manifest/package is changed. Technical basis: [IFileSaveDialog](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ifilesavedialog), [TaskDialogIndirect](https://learn.microsoft.com/en-us/windows/win32/api/commctrl/nf-commctrl-taskdialogindirect), [ACTCTX](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-actctxa), [ActivateActCtx](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-activateactctx).

`FlashcardExportCoordinator.cs:19` publishes one owned task under its gate before synchronous immutable snapshot capture or dialogs. A second request returns Busy without queueing. It owns the captured snapshot, native selection/release, preparation, operation-bound consent, production publication and all inspection/cleanup/recovery settlement. Navigation retains the session ViewModel and does not cancel this task. Publisher exceptions after entry map to Indeterminate, never ordinary success. StopAsync (`:81`) permanently closes admission, requests cancellation/dismissal, retains ownership even if cancellation observers fail and returns the same underlying settlement task on repeated calls.

`StudioExportSession` (`FlashcardExportCoordinator.cs:108`) owns one lazy coordinator. IsCreated/IsActive and unused-session shutdown do not invoke its factory. StudioBootstrap registers typed factories in the existing host; pages/windows receive concrete objects/delegates and never use IServiceProvider or a service locator. MainWindow (`:71-74`) supplies its current native handle at each export invocation. No static HWND is retained. Home and Flashcards pre-export logs show exportCreated=False; native first-export creation logs show count=1.

Flashcards captures values before the first await (`FlashcardsViewModel.cs:74`) and adds separate busy/status/recovery properties. Export does not rate a card or mark the deck studied. Three explicit controls in `FlashcardsPage.xaml:94-96` are **Save CSV (.csv)**, **Save Anki-format text (.txt)** and **Save AXORA JSON (.json)**. Disabled/busy state, polite live status and selectable disclosed recovery paths are independent of existing notes/study status. CSV guidance preserves exact text and asks spreadsheet users to import Front/Back as text. Anki copy explicitly says physical import has not been verified. Home describes available local exports and session-only study state honestly.

### App shutdown, fallback and deadlock review

App owns the export session in the existing host. Its AppWindow Closing handler (`App.xaml.cs:61-77`) initially cancels close, closes admission through the session, displays **Finishing export before closing**, awaits export/native release/production settlement, then H0 stop/disposal, and only then retries window Close. Repeated close requests share the same flow. Unused export shutdown never constructs a picker/coordinator. The one session shutdown task is published before cancellation can reenter.

Program (`Program.cs:35-47`) removes the obsolete UI SynchronizationContext after Application.Start returns. If export-owned work is active, fallback waits for the same session shutdown without the historical 15-second observation timeout. The historical bounded H0 observation remains for the path without active export. No Kill, FailFast, forced Environment.Exit or host disposal beneath admitted publication was added. A permanently stuck native operation retains resources; safety is not traded for a forced exit deadline.

Fresh source attack found and repaired a UI-context hazard during final hardening: the first session shutdown driver used Task.Yield and could capture the UI context before admitted settlement, creating a fallback deadlock if Application.Start had already returned. The final driver is explicitly retained and awaits export and H0 shutdown with ConfigureAwait(false) (`FlashcardExportCoordinator.cs:125-147`). `session-shutdown-context` (`FlashcardExportTests.cs:775`) holds the real publisher immediately after admission, installs a context that records but does not execute posted continuations, requests shutdown twice and requires the same task, zero context Posts and no early host disposal. Releasing the barrier completes publication, verification and exactly one H0 stop/disposal without pumping UI continuations. This new deterministic check is structural regression evidence; no separately executed old-binary red run is claimed for this I2 context fix.

The normal close handler awaits asynchronously while the native dialog's nested loop can dispatch Close on its STA. Coordinator continuations do not require the UI thread. Final publication work is synchronous within its retained worker and honors no caller cancellation after the admission CAS. UI ViewModel continuation can update status while the normal message loop remains alive; file-integrity ownership is independent of that UI continuation. Source and adapter tests support this reasoning, but physical close-during-dialog remains unverified.

### Deterministic verification and final normal-output rebuilds

All 21 mandatory H0 identities and 22 A1 identities remain. All original 44 repaired Export identities and six R2 identities remain; eighteen I2 identities bring Export to **68** mandatory cases. I2 identities: coordinator-lazy, coordinator-busy, coordinator-picker-terminals, coordinator-consent, coordinator-destination-change, coordinator-status, coordinator-navigation, coordinator-stop-picker, coordinator-stop-staging, coordinator-stop-admitted, coordinator-stop-verification, coordinator-recovery, picker-adapter-contracts, picker-adapter-cancel-release, picker-adapter-failure, picker-owner-guard, export-performance and session-shutdown-context. One case may establish multiple contracts; assertion counts are observations rather than a frozen requirement.

Production barriers, instance-scoped and dormant/null in normal execution, hold staging, commit admission and post-publication/pre-verification. Shutdown while held must leave host Stops/Disposals at zero and the production session task incomplete. After release: staging shutdown is Canceled with unchanged sentinel and owned artifacts cleaned; admitted and verification shutdowns are Published with commit admitted, complete verification/cleanup and exactly one subsequent host stop/disposal. These tests exercise the App-owned production session gate and real publisher with a probe host; they do not construct an actual native Window or claim a physical process exit at each barrier.

Final normal-output regression log: `Axora-Desktop-WinUI/Axora.Studio.Tests/logs/a2-i2-final-normal-regression.log`. Per-group ledgers/logs: `m1-20261003T174615732-{group}.stdout.log` and `.stderr.log` in that ignored logs folder.

| Final group | Mandatory cases | Passed assertions | Failed/missing/duplicate/unknown/blocked | Execution |
|---|---:|---:|---|---|
| STUDIO-H0 | 21 | 160 | all zero | exactly once; child exit 0 |
| STUDIO-M1-FLASHCARDS | 22 | 172 | all zero | exactly once; child exit 0 |
| STUDIO-M1-EXPORT | 68 | 259 | all zero | exactly once; child exit 0 |

Each ledger has registeredGroups=1, executedGroups=1, totalRegisteredGroups=3, complete=true and disposition=Pass. Combined runner exit 0. Unknown group and malformed extra argument both exited 2 with no tests run (`a2-i2-verified-unknown.log`, `a2-i2-verified-malformed.log`). Symlink privilege unavailable remains an explicit ENVIRONMENT-NOT-AVAILABLE observation without a passing assertion; hardlinks were independently tested.

The restricted execution token initially caused production NTFS destination metadata inspection to fail with Win32Exception 5, resulting in 50 assertions failing (`a2-i2-consent-adapter-regression.log`). This failed environmental run is retained. Running the same suite outside that restricted token passed; the final 259-assertion normal-output run also used that approved execution context. H0/A1 already passed under the restricted context. No test was weakened or converted to fake PASS.

SDK **9.0.318**, direct existing-dependency Debug/x64 rebuilds:

```powershell
dotnet build Axora.Studio/Axora.Studio.csproj --no-restore -t:Rebuild -c Debug -p:Platform=x64 -v:minimal
dotnet build Axora.Studio.Tests/Axora.Studio.Tests.csproj --no-restore -t:Rebuild -c Debug -p:Platform=x64 -v:minimal
```

| Final normal-output rebuild | Exit/errors/warnings | Elapsed | Log |
|---|---|---|---|
| Studio | 0 / 0 / 8 | 21.28 s | a2-i2-studio-final-normal-rebuild.log |
| Studio.Tests, including Studio dependency | 0 / 0 / 8 | 21.67 s | a2-i2-tests-final-normal-rebuild.log |

All eight warnings are existing MVVMTK0045 WinRT/AOT field-generation warnings: SettingsViewModel selectedTheme (:13), status (:14), canEdit (:15), canSave (:18); ShellViewModel selectedRoute (:11); FlashcardsViewModel notesText (:27), sourceLabel (:28), status (:29). No warning suppression, project/manifest/package/toolchain edits, Release build or AOT compatibility claim. An earlier alternate ignored output rebuild/suite was also green; final evidence above deliberately refers to the normal project output.

Before native UI, and again during final reconciliation, static red-team searched destination writes/truncation, overwrite:true, path deletion/copy overwrite, service locators, window providers/static handles, Kill/FailFast/forced Exit, unowned threads/tasks, inappropriate async void and raw logging. Export uses only owned CreateNew stage/reservation/recovery metadata, no-overwrite Move or approved Replace, and exact-handle cleanup. The existing H0 settings-stage File.Delete is unchanged and unrelated to export destination deletion. async void is limited to required UI handlers. Environment.ExitCode records errors; App.Exit is restricted to startup failure/close failure after awaited settlement. No deferred feature or unexpected risky occurrence remained in the authored source.

### Physical native runtime chronology and artifacts

All academic export paths and mutations used test-owned temporary fixtures, never real user documents. Isolated root:

`C:\Users\rajghosh\AppData\Local\Temp\axora-a2-native-64c888d4e2134943b6147def28be56cb`

APPDATA pointed to `roaming`, LOCALAPPDATA to `local`, and export destinations to `exports` below that root. Actual canonical normal-output Axora.Studio.exe was launched. Runtime diagnostics are the isolated `roaming/Axora/Studio/startup.{pid}.log` files. Computer Use screenshots/accessibility observations were inspected during the native task; no claim of a separately committed screenshot artifact is made.

Failures remain part of the chronology. The initial Save implementation accidentally used the FileOpenDialog CLSID, producing InvalidCastException. A raw COM lifetime change preceded recognition of that GUID error; safe phase diagnostics then located it. The CLSID was corrected against the SDK and actual Save As appeared. The first native replacement consent failed with EntryPointNotFoundException because this unpackaged app lacked a v6 common-controls activation dependency. It left the sentinel untouched. The thread-scoped installed-Windows activation solution above corrected it without changing a manifest or adding a package. Subsequent real Replace/Cancel and destination-change journeys worked. These initial native failures are not counted as successful acceptance observations.

PID **22620** established actual native Save As ownership/modal behavior, CSV (*.csv) filter, safe Study techniques.csv suggestion and matching extension. PID **5732** established corrected native replacement consent, destination-change, layouts/themes/focus and ordinary idle close. The final rebuild adds the session context/fallback hardening described above; picker/consent/publication source had no subsequent change. PID **32808** is the final rebuilt launch, observed in read-only startup logs at Home with exportCreated=False; Computer Use stopped before its renewed UI checks.

| Physical journey | Observed result and independent file evidence |
|---|---|
| New CSV path canceled | Typed test-owned cancel-new.csv; native Cancel produced Canceled. No destination, stage or recovery artifact created |
| New path before Save | Typed new-cards.csv while dialog remained open; Test-Path was false before Save, physically demonstrating no premature output creation |
| New CSV saved | UI success: 2 cards; new-cards.csv 251 bytes. Independent Import-Csv reopened two rows with Front,Back,Difficulty,IntervalDays and exact built-in text values |
| New JSON saved | UI success: 2 cards; new-cards.json 787 bytes. Independent ConvertFrom-Json reopened format axora.flashcards/schemaVersion 1/two cards, valid IDs and reviewCount 0 |
| Existing selected before consent | Sentinel initially 31 bytes, known ASCII bytes and SHA-256 D533C0A60B86184AB42644E550C35AC8F8475AF88B6C419D68B239FDE2359EAB; bytes/hash unchanged after actual Save As selection and before any Studio approval |
| Replacement Cancel | Owner-bound Replace/Cancel prompt, default Cancel; Cancel preserved exact original 31 bytes/hash and left no stage/recovery residue |
| Approved replacement | Fresh selection preserved sentinel before approval; explicit Replace produced independently valid schema-1 JSON, 787 bytes, reviewCount 0; no recovery residue after verified cleanup |
| Destination changed while consent open | changed-sentinel.json was independently changed from before-change to external-change-after-consent-open before Replace. UI DestinationChanged; exact 34 actor bytes survived; no stage/recovery residue |
| Ordinary idle close | PID5732 owner Close after all dialogs settled; retained live process-handle observer confirmed exit code 0. Logs record H0 stop, disposal, Window closed and Program fallback settled. This is not close-during-picker evidence |
| Close while Save As active | Attempts yielded picker cancellation/minimization rather than an observed App Closing event while the modal was active. P2.30 is UNVERIFIED; no claim of dialog-driven app shutdown/exit0/zero lingering processes |

The first independent CSV validation script had an incorrect expected Back value and failed its own assertion. Comparing with the actual built-in deck corrected the expectation; reopened content then matched exactly. This was an external validation-script error, not a product artifact repair or false product PASS.

Fresh read-only artifact reconciliation found exactly four exports: changed-sentinel.json 34 bytes, new-cards.csv 251, new-cards.json 787 and sentinel.json 787. No `.axora*` stage/recovery entry remained. New CSV/JSON and replacement JSON reopened successfully again; the changed sentinel retained exact actor text. The initial sentinel hash belongs only to this test-owned evidence, never to production diagnostics.

No forced termination occurred. Earlier diagnostic launches show ordinary close/shutdown/disposal/fallback completion in logs. A sandbox-desktop launch was inaccessible to interactive Computer Use and closed normally from its owning execution context; its exit code was not captured and is not claimed. Current final PID32808 remains alive and has no Window close requested log. Leaving this test-owned process alive after the user's Computer Use stop is disclosed, not interpreted as an application leak or successful shutdown.

### UI, privacy, idle and performance observations

System dark at 1280x752 and narrow 695x474, plus isolated-settings Light narrow/wide, displayed readable export copy, three explicit vertically arranged controls and separate status/recovery regions. Shift-Tab from the notes expander visited JSON, Anki, then CSV, with visible keyboard focus borders. Automation trees reported all exact export names and busy controls; the screenshot focus evidence was used because the helper's focused_element sometimes reported the containing pane. Controls disabled during modal export and reenabled after terminal settlement; study flip remained usable and reviewCount remained 0. Settings navigation and return retained session export status. Polite LiveSetting is present in source and status changes were visible; no audible Narrator test or full accessibility certification is claimed.

Home/Flashcards before export showed exportCreated=False in runtime counters; no export dialog, stage/recovery or destination work was observed. The first real export constructed the coordinator once. No export-owned child worker or TCP connection was observed in bounded process/socket checks, and source contains no model/speech feature wiring. These are bounded observations plus source evidence, not universal offline/long-duration claims.

Runtime log scan across the isolated startup logs returned **zero matches** for built-in front/back text, academic deck title, full selected export root, initial sentinel bytes or its hash. Safe native failure diagnostics contain phase/type/HRESULT only. Program's Debug diagnostic was hardened to type/HRESULT rather than raw exception text. No card/notes/path-bearing message is intentionally logged by export. Recovery metadata remains an explicitly disclosed user artifact with bounded names and safe operation metadata, not a production log. Privacy claims are limited to inspected source and those observed logs.

| Bounded Debug measurement | Actual observation / limitation |
|---|---|
| Final startup | Launch timestamp 17:47:09.2754Z to Window activated 17:47:10.8072Z, about 1.53 s for one normal-output launch |
| Final Home idle working set | Initially 168,652,800 bytes / peak 168,710,144; latest reconciliation 167,567,360 / peak 169,746,432. Single-process samples, not a budget |
| Native after dialogs/themes | PID5732 working set 293,572,608 / peak 299,630,592 bytes at that observation |
| First coordinator construction | Native count=1; 3.16 ms in corrected-dialog launches (other earlier samples 3.45/3.87 ms) |
| Small production CSV, dialogs excluded | 49.44 ms; 157 test-fixture bytes; peak test process working set 37,867,520 bytes |
| Small production JSON, dialogs excluded | 60.52 ms; 844 test-fixture bytes; peak test process working set 42,438,656 bytes |
| Near-limit deterministic processing | Codec write plus independent validation 579.59 ms; 49,243,714 artifact bytes; peak test process 56,025,088 bytes. Not a full near-limit publisher benchmark |
| Comparable final hardening rebuild delta | Normal Studio Debug output 295 files / 97,560,398 bytes before, 295 / 97,560,478 after: +80 bytes, unchanged file count. This is the final hardening window, not the entire I2-vs-A1 dependency/output delta |

No thresholds are invented. Native artifact sizes differ from deterministic fixture sizes because their card payloads differ. User dialog time is excluded from publisher measurements.

### Requirement reconciliation and internal full-A2 attack

| Requirement | Evidence / disposition |
|---|---|
| P2.1 total paths; P2.2 no dependencies | Exact 21 inventory below; no project/package/manifest/toolchain change |
| P2.3 Save picker; P2.4 nonmutation | Required flags/source/adapter tests plus physical new-path absence and existing sentinel preservation |
| P2.5 COM ownership; P2.6 active cancellation | SDK definitions and actual dialog/release terminals; STA Close adapter tested. Physical app close during dialog remains missing |
| P2.7 coordinator; P2.8 single export | Ownership-before-capture, no queue, Busy and snapshot isolation tests |
| P2.9 laziness | Production typed factories, unused shutdown and native pre-export counters |
| P2.10 UI; P2.11 status; P2.12 Anki; P2.13 Home | Exact controls, separate state/recovery, status tests and observed layouts; honest unverified import/session copy |
| P2.14 consent; P2.15 composition | Same operation Guid/plan/fingerprint, owner-bound prompt, native cancellation/replacement/change; existing host/typed dependencies |
| P2.16 shutdown; P2.17 StopAsync | One session task, permanent closed admission, production barriers before H0 disposal |
| P2.18 fallback; P2.19 deadlock | Retained admitted work, no timeout abandonment, no UI capture; captive-context test and source trace |
| P2.20 I2 tests; P2.21 picker tests | Eighteen new mandatory identities with real publisher barriers and narrow native adapter seams |
| P2.22 regressions; P2.23 builds | 21 H0 / 22 A1 / 68 Export; final 160/172/259; both rebuilds zero errors, eight disclosed warnings |
| P2.24 static attack | Before-native and final searches, inspected exceptions described above |
| P2.25 runtime; P2.26 cancel | Actual executable in isolated runtime; real new/existing cancellation and independent nonmutation |
| P2.27 new export; P2.28 replacement | Physically saved and independently reopened CSV/JSON/replacement; no success residue |
| P2.29 destination change | Native consent-open external mutation produced DestinationChanged; actor bytes intact |
| **P2.30 close during picker** | **UNVERIFIED / acceptance blocker; user stopped Computer Use; final test process remains running** |
| P2.31 precommit; P2.32 admitted close | Deterministic production-session/real-publisher barriers, host retention and settlement. Actual window/process journey not claimed for these test barriers |
| P2.33 idle; P2.34 UI; P2.35 privacy; P2.36 performance | Bounded native observations, source, Automation/focus/layout checks, zero inspected-log matches and measured processing |
| P2.37 chronology; P2.38 inventory; P2.40 no commit | Appended record, exact inventory, empty index, unchanged Git baseline and protected scope |
| P2.39 full-A2 attack | Sixteen claims traced below; native close claim remains insufficient to permit PASS |

| P2.39 claim attacked | Exact source/test/runtime support and limit |
|---|---|
| 1 picker nonmutation | NativeSaveDialog options/GetPath only; picker-adapter-contracts; physical absent new target and unchanged existing sentinel |
| 2 reservation ownership | ExportFilePublisher stage/recovery/reservation sequence and RequireOwnedReservation; backup-reservation-required/identity-swap; deliberate postcheck swap remains an acknowledged platform limit |
| 3 cleanup provenance | Finish/Cleanup/DeleteOwned identity and successful-native transition; partial/ambiguous/mismatch/canary tests; no equality-only authority |
| 4 single admission | Coordinator gate publishes task before capture; coordinator-busy; no queued second operation |
| 5 consent binding | Fresh local operation Guid plus unchanged canonical plan/fingerprint; coordinator-consent and native Replace/Cancel |
| 6 destination change | Prepare/MatchesPlan final recheck; final-stage-destination-change/coordinator-destination-change; physical consent-open mutation |
| 7 precommit cancel | commitGate CAS and no attempt before admission; cancel-precommit/reservation-cancel/coordinator-stop-staging |
| 8 post-admission settlement | Noncancelable native/inspection/verification/cleanup; cancel-postadmission/coordinator-stop-admitted/stop-verification retain host |
| 9 close during dialog | RequestCancel marshals exact active dialog Close to STA; picker-adapter-cancel-release/coordinator-stop-picker; **native genuine app-close journey not established** |
| 10 Program fallback | HasActiveExport does not create subsystem; unconditional active settlement, cleared UI context; session-shutdown-context. Native active-picker fallback not independently established |
| 11 lazy subsystem | StudioBootstrap factory and StudioExportSession Lazy; coordinator-lazy, unused shutdown and runtime exportCreated=False |
| 12 navigation ownership | Shared session ViewModel/coordinator, no page disposal cancellation; coordinator-navigation and native Settings return |
| 13 COM cleanup | GetPath finally, constructor Dispose and Dispatch release-before-completion; adapter fault/release tests plus native repeated dialogs; exhaustive OS-fault guarantee not claimed |
| 14 status truth | Distinct Published/Canceled/Changed/PublishedButVerificationFailed/Indeterminate/warning/recovery mappings; coordinator-status/recovery and observed native statuses |
| 15 privacy | Safe phase/type/HRESULT diagnostics, no selected-path/card log call; zero observed runtime-log matches |
| 16 previous H0/A1 | Original mandatory identities intact, full final 160/172 passing assertions; unchanged protected service/review algorithms |

No unresolved source defect was identified in this final self-attack within the stated threat boundary. The missing P2.30 physical evidence is a **blocking acceptance finding**, so the native lifecycle claim and overall readiness are not accepted. This self-review is not the separately required independent full-feature audit.

### Exact final A2 inventory and protected settlement

Fresh Git status reconciles **21 authored A2 paths: 11 modified, 10 new**, all unstaged. The ten-path I1 inventory gained exactly eleven paths during I2 (three new services/contracts and eight modified existing integration/host-test paths). I2 also legitimately extended the existing A2 ViewModel, export test file and this evidence file. No twenty-second authored path was introduced. Logs/build outputs remain ignored diagnostics.

| # | Repository-relative path | Git disposition |
|---:|---|---|
| 1 | Axora-Desktop-WinUI/Axora.Studio.Tests/HostTests.cs | Modified |
| 2 | Axora-Desktop-WinUI/Axora.Studio.Tests/Program.cs | Modified |
| 3 | Axora-Desktop-WinUI/Axora.Studio.Tests/run-m1.ps1 | Modified |
| 4 | Axora-Desktop-WinUI/Axora.Studio/App.xaml.cs | Modified |
| 5 | Axora-Desktop-WinUI/Axora.Studio/MainWindow.xaml.cs | Modified |
| 6 | Axora-Desktop-WinUI/Axora.Studio/Program.cs | Modified |
| 7 | Axora-Desktop-WinUI/Axora.Studio/StudioBootstrap.cs | Modified |
| 8 | Axora-Desktop-WinUI/Axora.Studio/ViewModels/FlashcardsViewModel.cs | Modified |
| 9 | Axora-Desktop-WinUI/Axora.Studio/Views/FlashcardsPage.xaml | Modified |
| 10 | Axora-Desktop-WinUI/Axora.Studio/Views/FlashcardsPage.xaml.cs | Modified |
| 11 | Axora-Desktop-WinUI/Axora.Studio/Views/HomePage.xaml | Modified |
| 12 | Axora-Desktop-WinUI/Axora.Studio.Tests/FlashcardExportTests.cs | New |
| 13 | Axora-Desktop-WinUI/Axora.Studio/Models/FlashcardExportModels.cs | New |
| 14 | Axora-Desktop-WinUI/Axora.Studio/Services/Contracts/IExportFilePublisher.cs | New |
| 15 | Axora-Desktop-WinUI/Axora.Studio/Services/Contracts/IStudioSavePicker.cs | New |
| 16 | Axora-Desktop-WinUI/Axora.Studio/Services/ExportFileNameSanitizer.cs | New |
| 17 | Axora-Desktop-WinUI/Axora.Studio/Services/ExportFilePublisher.cs | New |
| 18 | Axora-Desktop-WinUI/Axora.Studio/Services/FlashcardExportCodec.cs | New |
| 19 | Axora-Desktop-WinUI/Axora.Studio/Services/FlashcardExportCoordinator.cs | New |
| 20 | Axora-Desktop-WinUI/Axora.Studio/Services/StudioSavePicker.cs | New |
| 21 | docs/AXORA_STUDIO_M1_A2_EXPORT_EVIDENCE.md | New |

main; HEAD and local origin/main still **430d781b67ee8f3b63d7bdb609c573774acf7db0**; index empty. No Git mutation, fetch, staging, commit, push, branch or tag. ZIP appears only by its excluded Git name and was never opened/hashed/extracted/modified/deleted/staged. A1 historical evidence, legacy Desktop/MaterialUI, Tools/Mind/W5, Shared.Core, project/package/manifest/toolchain paths remain unchanged. No A3/B, persistence/import/database or route retirement was introduced. Tracked whitespace check succeeds; Git reports only existing LF-to-CRLF conversion notices.

### Remaining gates and interruption

P2.30 requires a genuine Studio close while native Save As is active, dialog cancellation/release, no target mutation, retained export settlement, H0 shutdown, clean exit code and zero remaining Studio process. Deterministic native adapter tests and ordinary idle exit 0 cannot substitute. The modal owner-X interaction did not produce that journey. Computer Use subsequently reported that the user physically stopped it; its skill guidance says, **"If Computer Use reports that the turn ended or that the user stopped Computer Use, stop issuing app input."** See `C:/Users/rajghosh/.codex/plugins/cache/openai-bundled/computer-use/26.930.31730/skills/computer-use/SKILL.md` and its required `../../docs/guidance.md`. The remaining native action needs an explicit user resumption or equivalent user-controlled native journey; no shell UI automation/custom close driver was used to bypass the stop.

Other disclosed gaps: actual Anki import not run (Anki unavailable); symbolic-link creation privilege unavailable; mounted network/removable/non-NTFS fixtures unavailable; natural disk-full/partial-native-Replace variants not physically induced; full Narrator/accessibility certification and Release/AOT unverified; whole-I2 output delta lacks an A1 binary baseline. Existing deterministic hardlink, partial publication, exception and ownership tests remain distinct from these unavailable environments.

All independent evidence/inventory work is complete at this checkpoint. The mandatory native close observation remains pending, so overall goal completion and independent-audit readiness are not asserted. After that gate is verified, complete A2 still requires its separately specified independent full-feature audit and acceptance before any separately authorized Git checkpoint.

**STUDIO-M1-A2-I2 BLOCKED — FULL A2 AUDIT NOT READY**

## Final gate Phase 1: scripted native completion (2026-10-04)

Authority: attachment `a9bc8b36-db31-4d85-8eb1-cde88fc424a5/pasted-text-1.txt`. It expressly replaces the interactive workflow with a temporary external Windows automation harness and authorizes repair of real I2 defects within the same 21 paths. No interactive Computer Use was invoked in this final-gate task. The earlier Escape interruption and incomplete native-close checkpoint above remain historical facts.

Initial reconciliation found main, HEAD/local origin/main `430d781b67ee8f3b63d7bdb609c573774acf7db0`, empty index and exactly 21 A2 paths (11 modified/10 new). No production source was newer than the previous green 259 run. Rebuilding was initially unnecessary; the subsequently observed native defect and its repair made fresh builds/tests necessary. Only StudioSavePicker.cs, App.xaml.cs, FlashcardExportTests.cs and this existing evidence document changed during final-gate Phase 1. No dependency/project/manifest/toolchain change or path #22.

### External harness and failed native-close chronology

The test-owned harness lives outside the repository at `C:\Users\rajghosh\AppData\Local\Temp\axora-a2-scripted-20261003`. It uses System.Windows.Automation SelectionItem/Invoke patterns, user32 EnumWindows/EnumChildWindows, process/window ownership, bounded polling and bounded SendMessageTimeout for the native filename field. It launches the actual final normal-output Axora.Studio.exe with isolated APPDATA/LOCALAPPDATA and an isolated exports directory. It never invokes a terminal through UI or uses real user Documents/Desktop destinations. Harness scripts and generated diagnostics are temporary external evidence, not additional authored repository paths.

An initial harness executable-path assumption was wrong (19041 versus the verified 26100 output directory) and was corrected before launch. The first synchronous UIA invocation/inspection harness was replaced with a retained task-based UIA invocation and a separate Win32 observer. Windows PowerShell's annotated Get-Content strings initially expanded provider metadata during JSON serialization; subsequent records use plain ReadAllLines strings. These harness errors are not product acceptance results.

**Actual product finding:** PID35552 received a genuine owner close with Save As visible, but automatic dialog dismissal/shutdown did not settle within the 20-second observation budget. Queuing Close behind the DispatcherQueue callback synchronously executing Show was unsafe for this nested modal path. RequestCancel now calls Close directly when already on the owning STA and retains dispatch marshaling for other threads. PID32484 still failed the native journey after that first repair. PID2348's safe diagnostics then proved the normal close handler ran on STA thread 2, requested session shutdown, entered/returned the native Close calls on the owning STA and retained the shutdown task, while Save As remained visible. Direct native Close alone was therefore insufficient in this observed canceled-owner-close context.

The final native Save adapter obtains the exact active dialog HWND through IOleWindow (IID 00000114-0000-0000-C000-000000000046, GetWindow slot 3), releases that QueryInterface reference in finally, requests IFileDialog.Close(ERROR_CANCELLED), and queues ordinary WM_CLOSE to that same still-live dialog if its native owner matches the operation's supplied HWND. It never enumerates or closes guessed windows. Native close is operation-idempotent; raw references remain alive through Show and are released before picker settlement. Failed Close/posting does not release coordinator/host ownership prematurely. Technical basis: [IFileDialog.Close](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifiledialog-close), [IOleWindow.GetWindow](https://learn.microsoft.com/en-us/windows/win32/api/oleidl/nf-oleidl-iolewindow-getwindow), and the FileDialog-specific IOleWindow guidance in [IFileDialogEvents.OnFileOk](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifiledialogevents-onfileok).

The failed launches were cleaned up by a separately documented dialog-only WM_CLOSE after the observation failure. Their already-requested normal Studio shutdown then completed with exit 0. Those cleanup cancellations are **not** successful automatic owner-close observations. No Kill/taskkill/TerminateProcess/FailFast/forced Exit occurred. Failed runtime logs and `failed-close-cleanup.json` records remain in their isolated temporary roots.

### Current real native close proof

The corrected adapter passed first on PID22516, then on final rebuilt PID **33300**. The latter authoritative record is:

`C:\Users\rajghosh\AppData\Local\Temp\axora-a2-scripted-20261003\runtime-b9e2d6c5e4064670b17381b8840eec05\close-result.json`

Its matching `launch-result.json` and `roaming/Axora/Studio/startup.33300.log` establish actual executable/UIA invocation and causal runtime order. These records were reopened after the run. The real native #32770 Save Flashcards HWND **1379654** was visible, owned by actual WinUI Studio HWND **1379528**, whose enabled state was false during modality. Home and Flashcards pre-export logged exportCreated=False; actual export invocation created the coordinator once. Native filename edit contained Study techniques.csv. The observer changed it to test-owned `exports/canceled-new.csv` using bounded WM_SETTEXT and independently confirmed the path absent before close.

Process.CloseMainWindow targeted the actual owner but returned false for the disabled window. WM_SYSCOMMAND/SC_CLOSE was posted but produced no close handler within the bounded five-second observation. WM_CLOSE to that same owner then produced **exactly one effective normal close request**. No dialog-only cancellation was issued by the external observer in this successful journey; cancellation came from the app's repair.

| Required causal observation | Actual evidence |
|---|---|
| Normal owner closing | 18:57:20.2994137Z Window close requested; exactly one such log entry |
| Admission closes / cancellation requested | App calls its production session ShutdownAsync; StopAsync closes admission before cancellation; 18:57:20.3087296Z Picker cancel requested; owningSTA=True |
| Legal native cancellation | Owning STA Close entered/returned at 18:57:20.3098196Z / .3151441Z; source executes IFileDialog.Close and exact-dialog native cancellation, with IOleWindow reference released in finally |
| Dialog Show/release and picker settle | 18:57:20.5330312Z Picker terminal; state=Canceled, no release-failure reason. Dispatch logs this only after unregister and Dispose; its task completes afterward |
| Coordinator settlement before H0 | StudioExportSession awaits that entire export task before invoking H0; 18:57:20.5446649Z H0 Shutdown requested; admission closed |
| Host/resource order | Owned work stopped .5478973Z; Host stop settled .5536871Z; Host disposed .5665727Z; Shutdown completed .5690694Z |
| Window and fallback | Window closed .5731861Z; Program fallback settled .7244542Z |
| Normal process exit | Retained process-handle observer: exit code **0**; zero remaining Studio processes and zero windows for PID33300 |
| New destination / artifacts | Typed new path absent before close and still absent afterward; exports directory contains only the original 29-byte test sentinel; no stage/recovery residue |
| Sentinel integrity | Before/after/reopened SHA-256 **BE6CFD0AA94DE396A7011DE97FD1BEC0887455EBBA6B9F449306D4DE8808220E**, exact test-owned bytes unchanged |
| Privacy | Fresh runtime-log scan: zero matches for deck title, card text, full selected export root or sentinel hash |

Duplicate cancellation requests can reach the adapter because StopAsync requests both token cancellation and dialog dismissal; the per-native-dialog guard permits one native close action. The observer's one effective owner close is separate from those internal cancellation requests. No commit was admitted in this picker-cancel journey. Existing real-publisher barrier tests separately prove staging cancellation and retained post-admission/post-publication settlement.

Earlier final PID32808 is no longer running. Its historical runtime log records ordinary user/window closing, H0 disposal and fallback settlement at 18:19:32Z; no retained exit code is claimed for that historical process. Current zero-Studio-process checks are direct process observations, not an inference from that log.

### Regression correction, current builds and ledgers

New mandatory case `picker-adapter-ui-close` exercises both explicit cancellation and token cancellation on the owning STA without a recursive dispatcher pump. The old queued-only implementation would not close inside either Show callback; the failed native run also directly discriminates its real behavior. Existing cross-thread tests remain. Their fake dispatcher now tracks the actual owning managed thread, rather than a global bool. A subsequent failure revealed that Task.Run.GetResult can execute the queued task inline on the caller's pool thread; the test now uses a retained LongRunning task and explicitly asserts distinct thread IDs before checking that cancellation only posts Close. This strengthens apartment evidence and preserves the original assertion's purpose.

The two intermediate full suites (`a2-finalgate-full-regression.log`, `a2-finalgate-final-regression.log`) each reported Export 262 passed/1 failed because that old cross-thread fixture did not guarantee a separate thread; H0/A1 remained green. These failures are preserved. An intermediate CS9191 in/ref modifier warning was corrected to the actual .NET signature without suppression. The final suite was temporarily not executed because automatic approval review hit an account usage limit; that was a review failure, not an unsafe-action determination. After usage became available, the approved run executed successfully.

| Current verification | Result | Authoritative ignored log |
|---|---|---|
| Direct Studio Debug/x64 --no-restore -t:Rebuild, SDK9.0.318 | exit0, 0 errors, 8 existing warnings; 30.53 s | a2-finalgate-studio-final-rebuild.log |
| Direct Tests Debug/x64 --no-restore -t:Rebuild after distinct-thread test correction | exit0, 0 errors, 8 existing dependency warnings; 41.23 s | a2-finalgate-tests-threadproof-rebuild.log |
| STUDIO-H0 | 21 cases / 160 passed assertions | a2-finalgate-verified-regression.log |
| STUDIO-M1-FLASHCARDS | 22 cases / 172 passed assertions | same combined log |
| STUDIO-M1-EXPORT | **69 cases / 263 passed assertions** | same combined log |
| Unknown/malformed dispatch | each exit2, no tests run | a2-finalgate-unknown.log / a2-finalgate-malformed.log |

Final child logs are `m1-20261004T050310027-{group}.stdout.log` / `.stderr.log`. Each group executes once, complete=true, disposition=Pass, child exit0; combined runner exit0; all failed/missing/duplicate/unknown/blocked counts zero; all three stderr files zero bytes. The eight unchanged MVVMTK0045 fields/warnings are listed in the preceding I2 section. No warnings were suppressed. No production change followed the final rebuilt native observation; the subsequent change was confined to the test's distinct-thread guarantee and is covered by the final rebuilt suite. No Release/AOT or broad performance claim.

### Phase-1 safety attack and gate

| P1.16 claim attacked | Current support |
|---|---|
| 1 nonmutating picker | Native flags/path-only code; real new-path absence and historical existing-selection preservation |
| 2 explicit consent | Operation-local Guid/plan; actual Replace/Cancel history, no native overwrite prompt |
| 3 changed fingerprint blocks overwrite | Final publisher check, deterministic cases and native consent-open external mutation |
| 4 ordinary backup-slot collision removed | CreateNew owned reservation and dedicated R2 discriminator; deliberate postcheck swap limitation remains disclosed |
| 5 cleanup provenance | Successful-native transformation required for backup cleanup; exception/identity/canary tests |
| 6 lazy subsystem | Typed Lazy factory, current pre-export counters, unused shutdown tests |
| 7 one operation | Coordinator lock/task ownership and Busy test; no queue |
| 8 navigation ownership | Shared session, immutable capture and navigation test; historical Settings return |
| 9 native close | Final PID33300 actual owner WM_CLOSE, app cancellation, causal trace, exit0/zero process |
| 10 precommit integrity | Real canceled Save As plus production staging shutdown barrier |
| 11 admitted safety | Production admission/verification barriers retain host until complete inspection/cleanup |
| 12 fallback ownership | No active-export timeout abandonment; captive-context and host-ownership tests |
| 13 native lifetime | STA Close, IOleWindow QueryInterface/finally release, Show/Dispose-before-terminal source plus real terminal trace |
| 14 privacy | Safe type/HRESULT/phase/thread metadata and fresh zero-hit native-log scan |
| 15 scope | Reconciled same 21 paths; empty index; protected files outside diff; ZIP name only |

The observed HIGH native-close settlement defect was repaired and physically verified. No remaining BLOCKER/HIGH/MEDIUM was identified in this Phase-1 completion check. Optional Anki import, symlink privilege, unsupported mounted volumes, natural disk failure, full accessibility certification and Release/AOT observations remain honestly unverified. The deliberate same-user postcheck reservation swap remains the previously accepted platform limitation, not universal CAS.

**STUDIO-M1-A2-I2 FINAL VERIFICATION PASS.** Proceed automatically into the separately required fresh read-only full A2 audit. This gate is not that audit's verdict and grants no staging/commit/push authority.

## Full hostile A2 audit finding and R3 repair (2026-10-04)

The subsequent independent full A2 audit reviewed the complete 21-path feature and recorded **0 BLOCKER, 0 HIGH, 1 MEDIUM**. The sole blocking finding was in `StudioSavePicker.cs` replacement consent: `AllocHGlobal` does not promise zero-filled memory, yet the old `finally` called `DestroyStructure<TaskDialogButton>` for both entries even if marshaling failed before the second entry was initialized. That could interpret an uninitialized pointer and free it. This was a source-established exceptional-path finding, not a physically induced allocation failure or destination-corruption observation. The audit verdict was **STUDIO-M1-A2 AUDIT BLOCKED — REPAIR REQUIRED BEFORE CHECKPOINT**. Earlier I2 native and regression results remain historical, not an initial full-audit PASS.

R3 authority: `b6ed61ee-dc3d-4594-88d1-0cc5250966fa/pasted-text-1.txt`. Pre-repair reconciliation found `main`, HEAD/local `origin/main` both `430d781b67ee8f3b63d7bdb609c573774acf7db0`, empty index, and the same 21 A2 paths (11 modified/10 new). The unrelated ZIP was observed by Git name only. R3 authored changes are limited to this evidence file, `Axora.Studio/Services/StudioSavePicker.cs`, and `Axora.Studio.Tests/FlashcardExportTests.cs`. No package, project, SDK, manifest or protected-area edit was made.

### R3 native ownership and failure matrix

`NativeConfirmation` now uses one ownership model. It checks the packed SDK button size/offset and checked total byte count, allocates the complete native array, then zeroes the entire array before writing either entry. Each non-null UTF-16 label pointer returned by `StringToHGlobalUni` is immediately stored in a two-element owned-pointer array. Packed entries contain only the exact button ID and that pointer. No `StructureToPtr` or `DestroyStructure` operates on the consent button array. The outer `finally` frees each nonzero owned text pointer at most once, retires that pointer, and independently frees the array once. It attempts all other owned frees even when one throws. If both body and cleanup fault, the original body exception remains the typed picker failure; a cleanup-only exception also produces failure. The instance-scoped construction and invocation hooks are null in production; they add no public product API or process-global switch.

| Fault/terminal point | Owned text at cleanup | Array cleanup | TaskDialog attempted |
|---|---|---|---|
| Array allocation fails | none | no array exists | no |
| Zeroing fails / before first text | none | once | no |
| After first text, before its entry | text 0 | once | no |
| First entry ready; before second text | text 0 | once | no |
| After second text, before its entry | texts 0 and 1 | once | no |
| Both entries ready; TaskDialog throws or returns failure | texts 0 and 1 | once | yes |
| Both entries ready; Cancel or Replace | texts 0 and 1 | once | yes |

The TaskDialog owner, operation-local approved-plan/fingerprint binding, IDs 100/101, default Cancel 101, callback, title/copy, activation context and all File Save COM definitions remain as previously audited. Installed Windows SDK `10.0.26100.0` `um/CommCtrl.h` declares `TASKDIALOG_BUTTON` under `pshpack1.h` as a 32-bit ID followed by a pointer; the runtime layout check requires offset 4 and size `4 + IntPtr.Size`. The repair does not touch publication or destination I/O. Earlier actual native Replace/Cancel and close observations were retained; no Computer Use or repeated native journey was performed for this exceptional allocation fix.

### R3 verification before targeted read-only re-audit

Four additional mandatory cases bring Export to **73**. `consent-partial-button-init` fails both before second-text allocation and after the second text is owned but before its entry is written. `consent-first-button-fault` fails before any text and after first-text allocation. `consent-success-control` independently reads both native IDs and UTF-16 pointers, checks Cancel remains default, verifies Replace/Cancel mapping, and checks exact cleanup events. `consent-taskdialog-failure` covers a negative HRESULT and an invocation exception after both entries are ready. Each case reaches the production private consent implementation through the picker adapter; no copied allocator is tested. The old implementation lacks this owned-pointer lifecycle and would fail the expected partial-cleanup event sequences. Fault cases prove the dialog invocation hook is not reached and the test-owned sentinel remains exact. Existing picker cancellation/release, owner, coordinator consent and destination-change cases remain in the full Export group.

| Current Debug/x64 check (SDK 9.0.318) | Result | Ignored log |
|---|---|---|
| Direct Studio `--no-restore -t:Rebuild` | exit 0; 0 errors; 8 existing MVVMTK0045 warnings | `a2-r3-studio-final-rebuild.log` |
| Direct Studio.Tests `--no-restore -t:Rebuild` | exit 0; 0 errors; same 8 dependency warnings | `a2-r3-tests-final-rebuild.log` |
| Focused `STUDIO-M1-EXPORT` group | 73 cases; 289 passed assertions; 0 failed/missing/duplicate/unknown/blocked; exit 0 | `a2-r3-final-focused-export.log` |
| Full `STUDIO-H0` | 21 cases; 160 passed assertions; 0 failed | `a2-r3-final-full-regression.log` |
| Full `STUDIO-M1-FLASHCARDS` | 22 cases; 172 passed assertions; 0 failed | same combined log |
| Full `STUDIO-M1-EXPORT` | 73 cases; 289 passed assertions; 0 failed | same combined log |
| Unknown and malformed group dispatch | each exit 2; no tests run | `a2-r3-unknown.log`, `a2-r3-malformed.log` |

The full runner recorded one complete execution of each group, each child exit 0, and combined exit 0. Logs `m1-20261004T054343792-{group}.stdout.log` and `.stderr.log` retain per-child evidence. No warning was suppressed. Release/AOT, physical Anki import, privileged symlink and unsupported mounted-volume fixtures remain unverified as previously recorded. The deliberate same-user post-final-check reservation swap remains the disclosed platform limitation; no universal CAS is claimed.

**STUDIO-M1-A2-R3 REPAIR VERIFICATION PASS.** The separate targeted Phase-2 audit is required before checkpoint-readiness can be declared. This section grants no staging, commit, push, A3 or B authority.

## R3 final targeted re-audit and checkpoint readiness (2026-10-04)

This entry closes the already-completed targeted independent re-audit in the chronology. The preceding full hostile A2 audit found exactly one MEDIUM defect: replacement-consent cleanup could call `DestroyStructure<TaskDialogButton>` on a partially initialized native button array. Its historical BLOCKED verdict remains unchanged. R3 changed only `Axora.Studio/Services/StudioSavePicker.cs`, `Axora.Studio.Tests/FlashcardExportTests.cs`, and this A2 evidence file.

### Accepted repair and discriminating coverage

The final consent ownership model uses one native button array and separately owned UTF-16 button strings. The entire array is zero-initialized before entries are written. Only successfully allocated, explicitly owned text pointers are freed; each pointer is retired before its cleanup attempt, and the array is freed exactly once. No `DestroyStructure` operates over uninitialized consent entries. Partial initialization is safe, cleanup attempts remain independent, and a primary construction/invocation failure retains precedence over a subsequent cleanup failure.

The added production-path coverage exercises partial button initialization before and after second-text allocation, first-entry failure before and after first-text allocation, normal Replace, normal Cancel, negative TaskDialog HRESULT, invocation failure, and exact cleanup ownership. The success control independently reads the actual native IDs and UTF-16 pointers. Replace remains ID 100, Cancel ID 101 and the default; operation-bound owner/plan/fingerprint consent is preserved. Instance-scoped fault/invocation hooks remain null in production. These are deterministic adapter/production-consent checks, not physically induced native allocation failures.

### Final accepted verification

| Verification reused for this closeout | Accepted result |
|---|---|
| STUDIO-H0 | 21 mandatory cases; 160 passed assertions; 0 failures |
| STUDIO-M1-FLASHCARDS | 22 mandatory cases; 172 passed assertions; 0 failures |
| STUDIO-M1-EXPORT | 73 mandatory cases; 289 passed assertions; 0 failures |
| Unknown/malformed dispatch | Each exit 2; no tests executed |
| Axora.Studio Debug/x64 rebuild, SDK 9.0.318 | 0 errors; 8 existing MVVMTK0045 warnings |
| Axora.Studio.Tests Debug/x64 rebuild, SDK 9.0.318 | 0 errors; the same 8 existing dependency warnings |

The authoritative R3 logs and complete group ledgers are identified in the preceding section. No warning was suppressed. No new build, test, Computer Use, native Save As, replacement or close-during-picker work was run for this evidence-only correction.

### Final targeted independent re-audit

The completed read-only targeted re-audit confirmed the repaired root cause absent and accounted for the native allocation/free inventory, partial-initialization unwinding and original-failure precedence. Checked pointer arithmetic and the packed SDK layout remain consistent: text-pointer offset 4, button stride `4 + IntPtr.Size`, and checked two-button allocation size. Normal consent behavior remains unchanged.

All 21 accepted A2 file hashes were stable throughout that re-audit. The other 18 A2 paths were unchanged from the pre-R3 freeze. The complete authored A2 inventory remained exactly 21 paths, classified as 11 modified and 10 new. Publisher safety, picker/lifecycle ownership, truthful product wording and protected areas remained unchanged. No A1 evidence, legacy Desktop, MaterialUI, unrelated W5/Tools/Mind, project, package, manifest or toolchain change was included.

Final targeted independent re-audit severity: **BLOCKER 0; HIGH 0; MEDIUM 0**.

### Accepted limitations and user acceptance

Accepted LOW/unverified debt remains: physical Anki import **NOT-RUN**; unavailable symlink privilege; unavailable network/removable/non-NTFS mounted fixtures; natural disk/native partial-failure variants not physically induced; the disclosed deliberate same-user post-final-check filesystem race limitation; Release/AOT unverified; full accessibility certification unverified; and the eight existing MVVMTK0045 warnings. The implementation does not provide universal filesystem CAS. No persistence/import, read-aloud or Scholar integration is claimed.

**STUDIO-M1-A2 AUDIT PASS — READY FOR USER ACCEPTANCE AND COMMIT AUTHORIZATION**

The user accepted the complete audited M1-A2 feature for one local checkpoint containing the exact accepted 21 paths. Authority for this evidence-only append and conditional local checkpoint is `8b6e3d3d-ccd2-4529-b56b-51ece5c1afb8/pasted-text-1.txt`. No commit or push had occurred at the time this evidence entry was written. Publication is not authorized by this entry; any push requires separate user authorization.
