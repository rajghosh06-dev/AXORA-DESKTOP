# AXORA Studio M2-A Resume evidence

Baseline: main, HEAD/origin/main/live remote main `020afb120f046b74907318a70df07cba1737382e`, parent `a9057a7cbeb3af8c8a6000bac297c502b1021609`, subject `fix(legacy): harden Scholar flashcard fallback`. Starting tracked worktree clean, index empty, ahead/behind 0/0. Remote archive branch and final legacy tag unchanged. The unrelated ZIP was only listed; never opened or hashed.

## Phase 1 foundation

Re-read `Axora.Desktop/Models/ResumeModel.cs` and serialization/restoration methods in `ResumeStudioViewModel.cs`. The legacy observable graph is not used. Every header/contact string, summary, seven entry collections, item ordering, eight visibility flags and numeric formatting selections map to renderer-neutral immutable records. Redundant computed `BulletsLines` is validated and discarded. Missing legacy entry IDs use a deterministic source-hash/collection/index identity; malformed and globally duplicated IDs reject. Dates remain literal user text rather than imposed date formats.

Schema 1 envelope: stable lowercase 32-hex document ID, positive revision, UTC created/modified timestamps, semantic document, optional template identity/version, optional import hash and recovered revision. Collections are immutable arrays with working deserialization. Titles never enter filenames. Section order is a complete permutation of eight typed sections. Managed unknown/future schemas cannot downgrade; unknown properties, duplicate properties, malformed JSON/types, invalid UTF-8 and unpaired UTF-16 surrogates reject.

Limits: 1 MiB source/managed JSON; 100,000 aggregate UTF-16 units; title 200, contact/URL 512, ordinary scalar 2,048, narrative 16,384; 50 entries per collection/200 total; undo 30 snapshots/16 MiB; dashboard 1,000 entries/100 per page; recent 20; verified revision backups 5. Parsing depth 16, semantic depth 8. Numeric font/density/target preferences are known enum values; margins are finite inches within 0.1–3; accent is a six-digit hex color.

ResumeStore is read/enumeration owner and performs no construction-time filesystem work. ResumeSession is the sole active-document/mutation owner. ResumeFilePublisher is the file publication owner. Immutable save capture precedes staging; same-directory staging is flushed, reopened and decoded, checked against the serialized snapshot, and atomically moved or replaced only after destination hash recheck. No delete-first/truncate fallback exists. The Studio writer lease continues to exclude other Studio writers; the final check/replace is not a universal filesystem CAS against deliberate same-user external races.

Import captures a bounded byte array once, then preserves exact verified hash-addressed raw bytes before new managed publication. Changed/disappeared source after capture cannot affect import. Different-byte backup collision fails without overwrite. Identical bytes return typed ExistingImport/open-existing; changed bytes require a separate consented copy. Source filename/private path is not persisted in provenance.

Prior valid revisions use revision-number/hash-addressed filenames, newest first. Five verified backups are retained after publication; normal recovery republishes a verified backup as a new revision. Corrupt prior bytes are quarantined separately; future schemas remain protected. Orphan stages are never promoted. Backup cleanup checks document ID, revision and filename/hash ownership. Post-commit maintenance failure is reported as saved with failed maintenance rather than pretending publication failed.

Semantic dirty comparison covers the whole document; revision/event count alone does not define dirty. Save captures a baseline and later edits remain dirty. Unchanged Save is an idempotent success. Save Copy creates a new managed identity and explicitly makes it active, preserving the original. One mutation is admitted; concurrent mutations return Busy. Save/Discard/Cancel is shared by replacements and departure. Failed Save retains content and blocks departure. Close retains an admitted operation, then resolves remaining dirty content before closing admission. Diagnostics contain operation ID, phase, typed result/schema and exception type only; never raw exception messages/content/private paths.

Foundation hostile ledger: initial sandbox run exposed denied atomic file moves in sandbox temporary directories. No branch was skipped or called passing. Authorized unsandboxed execution used disposable test-owned directories and passed 115 assertions/26 cases with 0 failed/missing/duplicate/unknown/blocked. Subsequent added revision-backup failure coverage and final build/gate are recorded below before Phase 2 starts.

M2-A does NOT provide PDF, faithful paginated preview, or ATS analysis. It does NOT alter legacy Resume files or close Resume migration/cutover. Typst is planned for M2-B only. No renderer/runtime dependency, package, project, manifest or toolchain change is authorized here.

## Acceptance status

Phase 1 PASS (before any UI integration): foundation production ledger 27 cases/118 assertions, 0 failed/missing/duplicate/unknown/blocked; `logs/m2-foundation-final.log`. Debug/x64 Studio and Studio.Tests builds exited 0; foundation incremental test build 0 warnings/0 errors (`logs/m2-foundation-final-build.log`), earlier rebuilt Studio reported its eight existing MVVMTK0045 warnings. No warnings suppressed. Foundation reread found no remaining BLOCKER/HIGH/MEDIUM; required path subset only. Phase 2 then proceeded automatically. At that historical gate overall acceptance was still in progress; final acceptance is recorded at the end of this document.

## Phase 2 implementation and ownership

The dashboard provides bounded managed listings, New, Import Copy, Open, refresh and pagination. Unavailable current files remain visible; retained revision directories make missing documents discoverable. Saved revisions are decoded and identity/hash verified before a recovery choice becomes available. Recovery publishes through the same session/publisher as Save, rather than replacing bytes in a page.

The editor exposes the title, all ten header/contact fields, summary, every scalar in all seven collections, eight visibility flags and all typed formatting preferences. Entries can be added, removed from the draft and reordered; sections can be reordered. Removing an entry is a semantic edit, not managed-file deletion. Dates and bullet narrative remain literal text. Profile opens expanded; other sections expand within a vertically scrolling surface. Preferences explicitly describe later rendering. New/Open/Import/Save/Save Copy/Undo/Back are bound commands. Save Copy creates and activates a separate managed identity; original bytes remain intact. Incomplete typed inputs stay visible, mark the session dirty, block Save and departure, and can be explicitly corrected, undone or discarded.

Both routes resolve one App-owned `Lazy<ResumeViewModel>`/session. Home, Settings, About and Flashcards do not instantiate Resume or its picker and do not enumerate its store. MainWindow supplies owner HWND, dispatch and owned ContentDialog decisions. Shell navigation serializes the asynchronous departure guard and rejects direct route assignments once a guard is configured. Candidate Open is validated before asking to discard the current draft. New, Open, Import, Back, all external routes and window Close use the shared Save/Discard/Cancel policy. Failed Save retains editor content and refuses departure.

The initial WinRT picker returned Cancel immediately in the isolated fixture. Final `ResumeFilePicker` uses an STA-owned `IFileOpenDialog` with a JSON filter, filesystem/existing-file options and DONTADDTORECENT. It holds and releases its COM references explicitly and distinguishes the Windows cancellation HRESULT from errors. There is no production CLI fault switch or test picker substitution. Native dialog automation locates the exact owner HWND; no user's file listing is inspected. The synthetic filename is entered through the native filename control. A desktop-wide UIA enumeration timed out during the modal dialog; this was an observer limitation, repaired by exact HWND/control operations. No production import was counted successful until reviewed managed publication and every-field/hash/restart checks passed.

App shutdown closes Resume admission and retains its admitted operation independently of admitted A2 export. Optional A3 cancellation/deadline remains bounded. Host stop/disposal follows required file settlement. The Program fallback uses the aggregate active integrity-critical file-work property and has no observation timeout while required publication is active. Core publication continuations do not capture an abandoned UI context. Resume observer/diagnostic failures cannot strand operation completion. Close Cancel restores ordinary admission/title. No opportunistic A2/A3 service rewrite was made.

### Authored inventory: exactly 27

Paths below are repository-relative. Generated bin/obj/logs and disposable fixtures are excluded from authored scope. The SHA-256 inventory is generated at `Axora-Desktop-WinUI/Axora.Studio.Tests/logs/m2-authored-sha256.json` after the final evidence text and audit settle; it includes this document without a circular self-hash embedded here.

| Kind | Path |
|---|---|
| New | Axora-Desktop-WinUI/Axora.Studio/Models/ResumeDocument.cs |
| New | Axora-Desktop-WinUI/Axora.Studio/Models/ResumeFile.cs |
| New | Axora-Desktop-WinUI/Axora.Studio/Models/ResumeLimits.cs |
| New | Axora-Desktop-WinUI/Axora.Studio/Services/ResumeCodec.cs |
| New | Axora-Desktop-WinUI/Axora.Studio/Services/ResumeStore.cs |
| New | Axora-Desktop-WinUI/Axora.Studio/Services/ResumeSession.cs |
| New | Axora-Desktop-WinUI/Axora.Studio/Services/ResumeFilePublisher.cs |
| New | Axora-Desktop-WinUI/Axora.Studio/Services/ResumeFilePicker.cs |
| New | Axora-Desktop-WinUI/Axora.Studio/Services/Contracts/IResumeFilePublisher.cs |
| New | Axora-Desktop-WinUI/Axora.Studio/Services/Contracts/IResumeFilePicker.cs |
| New | Axora-Desktop-WinUI/Axora.Studio/ViewModels/ResumeViewModel.cs |
| New | Axora-Desktop-WinUI/Axora.Studio/Views/ResumeDashboardPage.xaml |
| New | Axora-Desktop-WinUI/Axora.Studio/Views/ResumeDashboardPage.xaml.cs |
| New | Axora-Desktop-WinUI/Axora.Studio/Views/ResumeEditorPage.xaml |
| New | Axora-Desktop-WinUI/Axora.Studio/Views/ResumeEditorPage.xaml.cs |
| New | Axora-Desktop-WinUI/Axora.Studio.Tests/ResumeTests.cs |
| New | Axora-Desktop-WinUI/Axora.Studio.Tests/run-m2.ps1 |
| New | docs/AXORA_STUDIO_M2_RESUME_EVIDENCE.md |
| Modified | Axora-Desktop-WinUI/Axora.Studio/StudioBootstrap.cs |
| Modified | Axora-Desktop-WinUI/Axora.Studio/App.xaml.cs |
| Modified | Axora-Desktop-WinUI/Axora.Studio/Program.cs |
| Modified | Axora-Desktop-WinUI/Axora.Studio/MainWindow.xaml.cs |
| Modified | Axora-Desktop-WinUI/Axora.Studio/Models/StudioRoute.cs |
| Modified | Axora-Desktop-WinUI/Axora.Studio/ViewModels/ShellViewModel.cs |
| Modified | Axora-Desktop-WinUI/Axora.Studio.Tests/Program.cs |
| Modified | Axora-Desktop-WinUI/Axora.Studio.Tests/HostTests.cs |
| Modified | Axora-Desktop-WinUI/Axora.Studio.Tests/run-m1.ps1 |

### Test architecture and historical verification

Resume's 34 mandatory cases exercise production codec, store, publisher, session and editor VM; faults/barriers occur at actual production phase boundaries. Final semantic acceptance invokes Resume plus all four original groups deliberately. `run-m1.ps1` validates the five-group manifest but still dispatches exactly its original four groups. Existing mandatory H0 cases remain; six discriminating Resume lifecycle cases extend them. H0 covers A2-only, Resume-only, both and neither with the real export publisher held at CommitAdmitted, plus bounded/deferred A3 audio during Resume publication. A captive synchronization-context case proves Resume settlement is independent of the lost UI loop.

Before the picker repair, `logs/m2-final-runner.log` completed all five groups with child exit 0 and Pass/complete ledgers: Resume 141 assertions, H0 202, Flashcards 172, Export 289, Read-Aloud 156. Every group reported failed/missing/duplicate/unknown/blocked = 0, registered/executed = 1 and full manifest = 5. `logs/m2-final-m1-runner.log` independently dispatched only the original four groups and exited 0. Unknown group, empty group, group plus trailing text and unknown option each returned exit 2 and zero ledgers. Final post-picker builds and regression are recorded in the freeze section below; historical evidence is not substituted for the final build.

### Native evidence chronology

All content is synthetic, with a distinctive PRIVATE_SENTINEL marker. Fixture root: `Axora.Studio.Tests/logs/native-fb2b81abdcc0444984071f943a940ef4`. APPDATA and LOCALAPPDATA point inside that root. `journeys.log` contains individual successful observations; production diagnostics are separate `AppData/Axora/Studio/startup.<pid>.log` files.

1. Actual Studio.exe PID 14488: fresh Home had no Resume directory and no Resume construction. Home route diagnostic explicitly reported resumeCreated=False. New filled every practical text field across the profile and seven collections plus preferences; managed bytes were decoded and inspected. Normal close exited 0.
2. Actual Studio.exe PID 31336: restart reopened the saved title and nested certification credential. Normal close exited 0. These journeys preceded the picker-only repair; later actual Studio launches use the repaired picker binary.
3. Actual Studio.exe PID 17176: owned Win32 JSON dialog selected only the synthetic legacy fixture; owned review consent preceded import. `m2-native-picker-completion.log` includes a failed UIA observer attempt; successful exact Win32 filename/Open operation and owned review were followed by `m2-native-import-validated.log`. The latter verifies all header fields, every field of all seven collections, all formatting values, every visibility flag, Unicode summary, exact unchanged source bytes and exact backup hash. PowerShell 5's default ANSI read initially caused a false Unicode comparison; explicit UTF-8 observer decoding passed. Process closed normally with exit 0. Actual Studio PID 14736 reopened the imported copy after restart.
4. Actual Studio PID 14736: Cancel/Discard/Save navigation, second Open Cancel/Discard to another document, New Cancel/Discard to blank draft, and window Close Cancel were verified. An observer requested a second action before dialog/close continuation settlement; final observer waits for dismissal, and the supplemental `m2-native-close-guard.log` verifies Close Save publication and normal exit 0. This was not a production guard failure. Failed observer attempts remain in logs; they are not described as passing scripts.
5. Tests.exe PID 10188 hosted the production App with only a publisher stage fault/barrier seam. `m2-native-failure-held-causal.log` proves previous file bytes and editor text survive controlled failure and a failed departure Save keeps the dirty editor active. The observer waits for the distinct second production failure settlement before resetting the fault; an earlier observer matched a stale failure status too soon, so its claimed departure check was superseded. Retry subsequently saved through the same owner. This distinction is retained rather than calling the earlier observer script passing.
6. Same production App child: named events held Save at the actual stage boundary after admission. After the causal Window close requested log, process remained alive and Host disposed was absent. Releasing the barrier yielded Save settlement before host disposal, normal exit 0 and valid managed bytes. Actual Studio.exe PID 4700 reopened the held-save result. Normal Studio has no injected fault mode. `m2-native-failure-held-causal.log` exited 0.

Final visual review found editor recovery choices leaking into dashboard layout, which could crowd out document rows. Refresh now clears those choices, and explicit recovery review has a bounded scrolling panel. Both projects were rebuilt and both runners rerun after that repair. Actual Studio PID 29800 verified the final dashboard, editor and clean exit; final rebuilt fresh Home PID 28800 used `native-13561d799c4e45f2850827fa629c9f3b` and again proved no Resume construction/root. Native ListView virtualization means only visible Open controls are realized; the observer therefore waits for the settled three-document status and a visible row, rather than requiring three simultaneous native controls. Additional actual Studio PIDs 4812 and 25996 verified steady visible rows and a visible title input, with scoped client screenshots and clean exit 0. Final screenshots are `editor-final.png` and `dashboard-final.png` in the main fixture. DPI-aware client geometry excludes other application pixels. Default dark appearance, scroll access, title input, visible document row and action toolbar were visually inspected. No owned Studio process remains.

### Hostile internal source audit: 30 required attacks

The final source was re-read independently of assertion narrative: immutable model, DTO/codec, store, publisher, session, picker, VM/pages, route integration, App/Program retention, and dispatch/runners. This is an internal acceptance review, not the requested subsequent independent audit. “None” means no remaining correctness gap found in the authorized contract; it does not assert universal filesystem or platform coverage. A publication linearization point is the successful atomic move/replace; session admission separately establishes ownership and retention.

| # / Attack | Owner | Linearization / decisive boundary | Expected result | Deterministic coverage / remaining gap, severity |
|---|---|---|---|---|
| 1 Malformed legacy JSON | Codec / Session | bounded capture then strict decode before consent/publication | Reject; current content and source survive | invalid-input; None |
| 2 Future schema | Codec / Publisher | schema classification and final destination check | Open/recovery refuse downgrade, including observed future prefix with malformed tail | future-protection; None |
| 3 Same title | Session / Publisher | GUID identity and CreateNew/no-overwrite move | Distinct documents, titles never paths | same-title; None |
| 4 Managed-ID collision | Publisher | initial existence check and no-overwrite move | Fail without altering collided file | identity-collision; None |
| 5 Backup failure | Publisher / Session | verified import/prior backup before destination commit | No publication; preserve previous bytes and dirty editor | import-backup-failure, import-backup-collision, revision-backup-failure; None |
| 6 Save staging failure | Publisher | owned stage creation/write/flush | Fail without replacing destination | staging-failure, stage-collision-ownership; None |
| 7 Destination changes | Publisher | SHA recheck immediately before atomic replace | Reject observed change; external bytes survive | destination-change; deliberate same-user post-check race is LOW |
| 8 Reopen validation fails | Publisher | strict staged decode and byte equality before backup/commit | Fail, retain destination/editor | reopen-failure; None |
| 9 Edits after snapshot | Session | immutable capture, then captured baseline promotion | Persist snapshot only; later edits remain dirty | concurrent-save; None |
| 10 Double save | Session | locked TCS operation admission | Second result Busy, no queued duplicate | concurrent-save; None |
| 11 Open while dirty | Session / VM | candidate validation then shared guard before activation | Save/Discard/Cancel, no silent loss | departure-guard, resume-routes-guard and native journey 4; None |
| 12 Navigation while dirty | Shell / Session | serialized guarded route commit | Cancel stays; Discard restores; Save settles before departure | resume-routes-guard all external routes and native journey 4; None |
| 13 Close while dirty | App / Session | PrepareClose guard before final shutdown | Cancel keeps admission/window; failed Save refuses close | departure-guard and native journey 4; None |
| 14 Close during save | Session / App | operation TCS admitted before close, awaited before disposal | Retain publication and settle before host disposal | shutdown-retention, resume-active-shutdown, fallback-context and native journey 6; None |
| 15 A2 export + Resume | App / separate owners | both owners' admitted completion tasks | Both retained; host awaits both; no coupled cancellation | resume-export-coexistence four combinations; None |
| 16 A3 audio + Resume | App / audio deadline | bounded audio result plus independent Resume TCS | Optional late audio never abandons Resume or indefinitely gates host | resume-audio-independence; real audio hardware concurrency not exercised, LOW platform coverage |
| 17 Source disappears before capture | Store / VM | opening bounded read handle before snapshot | Read failure, no managed publication, current retained | import-preservation actual FileNotFoundException plus VM catch/capture contract; None |
| 18 Source changes after capture | Session / Publisher | owned immutable byte snapshot | Publish captured semantic data and exact captured backup | import-preservation changes/deletes source after capture; None |
| 19 Identical re-import | Session / Store | existing provenance scan / stable source-derived identity | Typed ExistingImport opens existing; unreadable collision cannot create duplicate | import-idempotence; None |
| 20 Changed re-import | Session | decode/review consent then independent identity | Reviewed new copy; earlier copy untouched | import-idempotence; None |
| 21 Huge document | Codec / Store / Session | byte/text/entry/depth/history/listing ceilings | Reject out-of-bounds input without partial activation/publication | bounds, lazy-store, undo-recent; None |
| 22 Invalid Unicode | Codec | strict UTF-8 / escaped surrogate validation | Reject malformed scalars; valid Unicode preserved | unicode, native import Unicode; None |
| 23 Malformed IDs | Codec | per-entry canonical/nonzero/global uniqueness validation | Reject paths/duplicates; missing legacy IDs deterministic | invalid-input, legacy-all-fields; None |
| 24 Log privacy leak | Session / App diagnostics | structured operation/phase/type output only | No content, source path or exception message | privacy, observer-failure; 15 native production logs scanned, zero matches; None |
| 25 Page bypasses publisher | Pages / VM / Session | command-to-session-to-publisher call chain | Pages contain no file writer | final source reread and source search; None |
| 26 Page bypasses session | VM / Session | validated semantic Edit and guarded replacements | No separate mutable authoritative document | editor-all-fields, invalid-editor-draft and reread; None |
| 27 Eager startup Resume | App / DI / MainWindow | Lazy resolution only on Resume route | No Home construction/scan/files/picker | resume-lazy, resume-inactive-shutdown, native journey 1; None |
| 28 Runner weakens M1 | Program / scripts | exact manifest and strict child ledger checks | Original four remain mandatory and M1 dispatch stays four | actual M1 exit 0; unknown/malformed exit 2; None |
| 29 Renderer introduced | Model / composition | source/dependency boundary | Semantic data only, no renderer/runtime | final authored production source search zero matches, no project/package edits; None |
| 30 Legacy source modified | Store / Publisher | Studio-owned paths plus read-only source capture | Original bytes identical; no Desktop edits | import-preservation, redirected-root NTFS junction, native import source hash; None |

Additional repairs found during internal review: dashboard recovery for missing/corrupt current documents, redirected ownership paths, idempotence with unreadable prior imports, future-prefix protection, invalid editor draft retention/Save Copy, truthful later-edit Save status, observer exceptions and cancelled-close status. These were fixed within the 27 paths and discriminating coverage added. No accepted checkpoint is being inferred from tests alone.

### Limits and accepted LOW debt

The final destination check and atomic replacement are not a universal CAS against a deliberate same-user external writer between those operations. Studio's normal single-writer lease and redirected-path rejection remain in place; cross-process synchronization is explicitly outside M2-A. Revision/raw import artifacts are bounded per operation/document but there is no global storage-quota/retention policy for all historical imports/quarantine; permanent deletion/cutover is outside M2-A. Strictly unsupported/corrupt/oversized files cannot be silently repaired by Save.

Validation here targets Windows Debug/x64. Release/AOT, alternate filesystems, inaccessible external shares and real audio hardware coexistence were not exercised; deterministic failure/deadline boundaries test their owned behavior. Seven remaining MVVMTK0045 warnings belong to existing Flashcards/Settings generated properties. Shell's former generated field was replaced to enforce the asynchronous route guard, removing its warning. No warning suppression or unrelated conversion was made. Native screenshots validate the owned default window only; broader DPI/theme/accessibility matrix is later coverage.

Three LOW findings remain, each with a defined boundary: (1) deliberate same-user external mutation in the final-check/atomic-replace interval, outside Studio's single-writer/cross-process contract; (2) Debug/x64/default-window validation does not establish Release/AOT, alternate-filesystem, full DPI/theme or real-audio-hardware compatibility, including the existing seven generator warnings; (3) an extra sidebar selection marker was visible during programmatic UIA SelectionItem navigation, while authoritative route/header/content and departure decisions were correct. The last is a cosmetic sidebar finding in the six-route catalog; pointer-path navigation was not exercised. No data loss, publication bypass or guard bypass was found. BLOCKER=0, HIGH=0, MEDIUM=0, LOW=3. Storage quota/cutover/deletion are explicit scope exclusions, not implemented claims.

M2-A does NOT provide PDF. M2-A does NOT provide faithful paginated preview. M2-A does NOT provide ATS analysis. M2-A does NOT alter legacy Resume files. M2-A does NOT close Resume migration/cutover. Typst is planned for M2-B only.

## Final freeze

Final fresh Debug/x64 Rebuilds after the final stage-ownership repair: Studio exit 0, 7 warnings/0 errors, elapsed 1:27.25; Studio.Tests exit 0, 7 warnings/0 errors, elapsed 1:55.70. Evidence: `logs/m2-freeze-studio-build.log`, `logs/m2-freeze-tests-build.log`. An earlier rebuild overlapped an owned still-open window and emitted file-lock cleanup warnings; it is superseded by these clean final rebuilds. No package/project/manifest/toolchain changes or warning suppression.

Final `logs/m2-freeze-runner.log` outer exit 0, all five children exit 0:

| Semantic group | Passed assertions | Failed / missing / duplicate / unknown / blocked | Complete |
|---|---:|---|---|
| STUDIO-M2-RESUME (34 cases) | 143 | 0 / 0 / 0 / 0 / 0 | true |
| STUDIO-H0 (original cases plus six Resume cases) | 202 | 0 / 0 / 0 / 0 / 0 | true |
| STUDIO-M1-FLASHCARDS | 172 | 0 / 0 / 0 / 0 / 0 | true |
| STUDIO-M1-EXPORT | 289 | 0 / 0 / 0 / 0 / 0 | true |
| STUDIO-M1-READALOUD | 156 | 0 / 0 / 0 / 0 / 0 | true |

Each ledger registered/executed one selected group, full manifest five, disposition Pass. Final `logs/m2-freeze-m1-runner.log` outer exit 0 and exactly the original four child groups, no Resume dispatch. Final malformed probes (`logs/m2-freeze-dispatch.json`): unknown group, empty group, unknown option and actual two arguments all exit 2, zero ledgers.

Final privacy/source/path audit (`logs/m2-freeze-audit.json`): 15 production startup logs, zero sentinel/content/private-fixture-path matches; zero renderer terms in authored production sources; zero file writer/publisher calls in the four Resume page files. Exact authored status 18 new/9 modified/27 total; index empty. Protected Desktop/Desktop.Tests/MaterialUI/Tools/Mind/W5, earlier evidence, global.json, project/package/manifest/solution files are unchanged. Generated logs, binaries and runtime fixtures remain outside authored scope. No legacy source was scanned by startup or modified by Resume.

Synthetic legacy source before SHA-256 = after SHA-256 = exact raw import backup SHA-256:
`39EA4352AEA7D7729F4A0ABFB83B7E4F7F05D03A272B531CA26F7D507F9DE7C5`.

Final HEAD = origin/main = live remote main = `020afb120f046b74907318a70df07cba1737382e`, ahead/behind 0/0, index empty; all work unstaged. Live archive remains `a518f04ba7188a93ec7d7334879c194699db59b9`; final legacy tag object `4ea4271029ed7b368b0d86463df264de70f7518b`, peeled archive SHA. All 27 authored paths, including this final evidence document, have SHA-256 entries in `logs/m2-authored-sha256.json`; hashes are checked again after generation. The unrelated ZIP was only listed, never opened/hashed/extracted/modified/deleted/staged. No staging, commit, push, rendering installation, M2-B/M2-C/STT or next slice occurred.

Phase 1 foundation PASS. Phase 2 implementation/internal acceptance PASS, with the three bounded LOW findings disclosed above. Subsequent independent M2-A audit has not been performed or accepted here.

The last ownership reread found a MEDIUM cleanup edge: failed CreateNew could collide with an existing stage, and cleanup previously inferred ownership from path existence. It is repaired: the created flag is established only after a successful FileStream CreateNew, before fallible write/flush. The new stage-collision-ownership case verifies failure/dirty retention, byte-identical foreign stage and unchanged prior document. This finding is now resolved (NONE). Stage identity injection is dormant and test-instance scoped; normal production uses a new validated random identity.

After this final publisher repair, native failure/held-save checks were repeated with production App child PID 29640, normal Studio reopen PID 27352 and fresh final Home PID 24700 (native-e4293c87ef934c35b5d838a4af236952). All settled and exited 0. Evidence: m2-native-final-owned-publication.log and m2-native-final-lazy-close.log. Final privacy scan includes 15 production logs across every recorded native fixture, including the earlier prototype. All owned test processes are closed. Both fresh builds, both runners, malformed dispatch, exact scope and all 27 final hashes were revalidated after the repair.

**STUDIO-M2-A IMPLEMENTATION PASS — READY FOR INDEPENDENT M2-A AUDIT**

## F1/F2/F3 remediation — 2026-10-09 UTC / 2026-10-10 IST

### Audit provenance and corrected acceptance chronology

The subsequent hostile audit in Codex ran on 2026-10-09, 18:19:16–18:32:26 UTC, in chat `AXORA_WORK-4 (cont. of 3)`. It reproduced F1 HIGH (accepted edits lost during Import/Recovery activation), F2 MEDIUM (committed Save reported Failed after backup maintenance), and F3 MEDIUM (invalid unselected backup poisoning valid selected Recovery). Its verdict blocked checkpoint readiness with BLOCKER 0 / HIGH 1 / MEDIUM 2. The historical implementation/internal-acceptance statements above describe the earlier checkpoint; they were not final acceptance and are superseded on those three correctness claims by that blocking report.

That audit was Codex, NOT Antigravity/Claude Sonnet and NOT an independent-model audit. This remediation and its hostile rereads also ran in Codex. A true independent Antigravity/Claude Sonnet M2-A audit remains REQUIRED after this remediation. No external-model acceptance or commit readiness is inferred here.

### Baseline and exact repair boundary

At admission, branch `main`, HEAD, origin/main and independently queried live remote main were `020afb120f046b74907318a70df07cba1737382e`, parent `a9057a7cbeb3af8c8a6000bac297c502b1021609`, subject `fix(legacy): harden Scholar flashcard fallback`, ahead/behind 0/0 and index empty. All 27 authored bytes matched the earlier frozen inventory. The pre-remediation inventory is retained unchanged as `Axora.Studio.Tests/logs/m2-pre-remediation-authored-sha256.json`.

Exactly six existing authored paths needed different bytes for this repair:

1. `Axora-Desktop-WinUI/Axora.Studio/Services/ResumeSession.cs`
2. `Axora-Desktop-WinUI/Axora.Studio/Services/ResumeFilePublisher.cs`
3. `Axora-Desktop-WinUI/Axora.Studio/ViewModels/ResumeViewModel.cs`
4. `Axora-Desktop-WinUI/Axora.Studio/Views/ResumeEditorPage.xaml`
5. `Axora-Desktop-WinUI/Axora.Studio.Tests/ResumeTests.cs`
6. `docs/AXORA_STUDIO_M2_RESUME_EVIDENCE.md`

The other 21 inventory hashes remain byte-identical to the pre-remediation freeze, including HostTests, App/Program shutdown integration and the runners. No new production file, project, package, route, renderer or toolchain change. The complete authored set remains the original 18 new / 9 modified / 27 total, unstaged. Generated ignored logs, helpers and test-owned fixtures are outside authored scope. The unrelated ZIP was only listed by Git; it was never opened, hashed, extracted, modified, deleted or staged.

### RED proof before production repair

Tests were added first in ResumeTests while the production source remained unchanged. `logs/m2-remediation-red-build.log` built the augmented test executable successfully. `logs/m2-remediation-red.log` exited 1 with **211 passed / 61 failed assertions**, complete=true and zero missing/duplicate/unknown/blocked. All original 34 mandatory cases remained; 18 cases were added, yielding 52 registered mandatory cases.

Seventeen added defect cases failed against the original implementation: four held Import/Recovery success/failure cases, replacement close, six maintenance cases and six unselected-candidate cases. The added duplicate-owned-revision case was a regression control and already passed before repair; it is not represented as a RED defect reproduction. Strict selected-invalid controls also already passed. Ordinary Save/Save Copy UI availability and explicit post-commit witness/fatal-exception controls were strengthened after the initial GREEN; these additional controls are not retroactively claimed as part of the earlier RED run.

| Finding | Preserved pre-repair reproduction |
|---|---|
| F1 HIGH | Real publisher held at stage after replacement admission; session text/structural Edit and invalid-draft mutation, VM valid/invalid field setters, Add/Remove/Reorder and Undo were attempted. Original implementation accepted changes and/or retained mutated old state on failure; success activated the replacement and lost accepted changes. Close also retained no semantic editing freeze. |
| F2 MEDIUM | Actual revision-backup rotation encountered ownership, invalid UTF-8, JSON, type and future-schema damage after the new destination was present. Original result/baseline or subsequent modifying Save disagreed with the committed disk snapshot. The later-edit variant reproduced the stale expected-hash failure. |
| F3 MEDIUM | Selected backup was strictly valid; an older unselected candidate was separately malformed JSON, invalid UTF-8, wrong identity, future schema, locked unreadable or ownership-mismatched. Original Recovery failed instead of publishing the selected valid content. |

### F1: replacement ownership freezes semantic editing

Session admission establishes `_replacement` under the same lock as its operation completion owner. New/Open/Import/Recovery use replacement ownership. Save/Save Copy use ordinary publication ownership. `CanEdit` derives directly from session state, never Busy/status text. Edit, invalid-draft mutation and Undo check the admission gate atomically; a UI callback arriving before the disabled control is rendered still cannot mutate an admitted replacement. Undo's restore/pop is now one locked transition.

The VM exposes CanEdit, and editor XAML binds it to the semantic ScrollViewer and Undo. Status and window-close observation remain available. Activation-version/editor-generation checks reject obsolete fields and structural callbacks after replacement or editor rebuild. Field setters commit before accepting local values; replacement rejection cannot turn into a silently accepted invalid local draft. Failure clears ownership without activation and restores editing of the original state; success activates the captured replacement and rebuilds the editor before its availability becomes true.

The four `f1-import-owned`, `f1-import-failed`, `f1-recovery-owned`, `f1-recovery-failed` cases exercise scalar/structural/invalid draft/session/VM/Undo paths, exact old document/revision/destination preservation on failure, editing after failure, clean replacement on success, stale callback rejection and editing the newly active document. Each additionally holds ordinary Save and Save Copy and verifies accepted later VM edits survive dirty while only the captured snapshot is on disk. `f1-replacement-close` holds both Import and Recovery, checks retained close ownership and valid committed reopen. All use deterministic task barriers, without test sleeps.

### F2: commit and maintenance have separate results

The successful atomic move/replace is the destination publication linearization point. The publisher computes the committed byte hash before this point and marks committed immediately after it. Fallible maintenance then runs in a separate explicit catch boundary. The existing typed `ResumePublished(Sha256, BackupMaintenanceFailed)` is the committed/committed-with-warning result; Session promotes its captured baseline and hash for either result and surfaces `Saved · backup maintenance warning`. Later edits remain live and dirty. Import/Recovery also preserve the committed maintenance warning in their successful status.

The maintenance boundary handles expected IO/access, invalid-data, JSON, argument/decoding, invalid-operation/type, format and overflow failures. It does not swallow OutOfMemoryException, StackOverflowException or AccessViolationException; the admitted Session owner settles even when fatal-type propagation occurs. Suspect backup data is preserved, without a destructive fallback. Pre-commit staging/reopen/revision-backup/destination failures still return Failed and retain old authoritative bytes through the original mandatory cases.

`f2-maintenance-ownership`, `-utf8`, `-json`, `-type`, `-future` damage an actual oldest revision so production rotation fails after commit. Each checks the committed destination snapshot, Saved plus warning, correct dirty baseline, exact suspect bytes, a subsequent modifying Save through the promoted expected hash, and privacy. `f2-maintenance-later-edit` holds the actual production maintenance phase, reads the newly committed destination while held, then edits the live draft before releasing maintenance. It verifies later-edit dirtiness and retry, and includes a controlled OutOfMemoryException-type propagation check without actual memory exhaustion. This is causal filesystem evidence, not merely a result-enum assertion.

### F3: strict selected recovery, isolated unselected candidates

The selected artifact is strictly read/validated before the departure guard and is never skipped or replaced by an alternative. After any guard Save, current authority is captured inside replacement ownership. Revision calculation takes the maximum of the verified selected revision, known validated in-memory same-document revision, valid current managed revision, and individually verified owned backup revisions. Decoded current/other backup lineage must match selected identity/CreatedUtc; owned filenames and hashes are strictly checked by the store. Checked max+1 rejects overflow. The publisher independently rejects stale destination hashes, actual current identity/lineage mismatch, non-monotonic publication and unsupported current future envelopes.

Expected validation/read failures from an unselected artifact exclude that candidate individually, retain its bytes and log only phase plus exception type. Malformed content/filename metadata never supplies revision authority. The result discloses retained invalid saved revisions. A future unselected artifact remains byte-identical; it is never downgraded. Directory ownership and bounded-enumeration failures still fail explicitly when a safe bounded operation cannot be established.

The six `f3-unselected-json`, `-utf8`, `-identity`, `-future`, `-unreadable`, `-ownership` cases require selected-content recovery, a new revision above the trustworthy current maximum, exact invalid-artifact preservation and production-store reopen. The unreadable case uses a real exclusive FileShare.None handle. Every variant also verifies selected malformed/future content fails without destination publication. `f3-duplicate-revision` supplies two valid owned same-revision files with different semantic content: selection wins, new revision remains monotonic and the other candidate survives.

### Phase 1 chronology and acceptance gate

The first repaired build failed WMC0011 because StackPanel is not a Control and cannot bind IsEnabled. `logs/m2-remediation-targeted-build.log` retains that failed attempt. The binding moved to the existing ScrollViewer; `m2-remediation-targeted-build-2.log` passed with seven inherited warnings plus a new nullable warning. The nullable warning was corrected. Initial GREEN (`m2-remediation-green-initial.log`) was 272/0. Later controls explicitly covered ordinary Save/Save Copy UI availability, the already-committed destination witness and fatal-type propagation; no passing assertion was weakened.

After final repair/coverage refinement, targeted `m2-remediation-phase1-resume.log` passed **291/0 across 52 mandatory cases**; `m2-remediation-phase1-h0.log` passed **202/0**. Both complete=true with zero missing/duplicate/unknown/blocked. The required twelve-attack hostile reread found BLOCKER 0 / HIGH 0 / MEDIUM 0. Phase 1 PASS was established before proceeding to full Phase 2 regression.

### Phase 2 fresh builds and final runners

Repository `global.json` pins 9.0.300 with latestPatch; actual SDK was **9.0.318**. Fresh Debug/x64 MSBuild Rebuilds completed after all production/test edits: `logs/m2-remediation-final-studio-build.log`, exit 0, **7 warnings / 0 errors**, 00:00:27.88; `logs/m2-remediation-final-tests-build.log`, exit 0, **0 warnings / 0 errors**, 00:00:10.57. The Tests rebuild used BuildProjectReferences=false because Studio had just been freshly rebuilt separately. Seven Studio warnings remain the inherited Flashcards/Settings MVVMTK0045 warnings. No warning suppression, package or toolchain edits.

Final intended `run-m2.ps1` outer exit 0 (`logs/m2-remediation-final-runner.log`), all five child exits 0:

| Semantic group | Passed assertions | Failed / missing / duplicate / unknown / blocked | Complete |
|---|---:|---|---|
| STUDIO-M2-RESUME (52 mandatory cases) | 291 | 0 / 0 / 0 / 0 / 0 | true |
| STUDIO-H0 | 202 | 0 / 0 / 0 / 0 / 0 | true |
| STUDIO-M1-FLASHCARDS | 172 | 0 / 0 / 0 / 0 / 0 | true |
| STUDIO-M1-EXPORT | 289 | 0 / 0 / 0 / 0 / 0 | true |
| STUDIO-M1-READALOUD | 156 | 0 / 0 / 0 / 0 / 0 | true |

Total **1,110 passed assertions**, every ledger disposition Pass, selected registered/executed 1/1 and full manifest 5. Child stdout files use prefix `m2-20261009T185103696-`. Final M1 runner (`logs/m2-remediation-final-m1-runner.log`, outer exit 0) still dispatched exactly H0/Flashcards/Export/ReadAloud, no Resume; all four children/ledgers passed. Dispatch probes (`logs/m2-remediation-dispatch.json`) for unknown group, empty group, unknown option and an actual two-argument invocation all returned exit 2 with zero ledgers.

A2/A3/App/Program/HostTests bytes are unchanged from the prior freeze. Final H0/Export/ReadAloud results preserve independent integrity-critical file retention, bounded optional audio and fallback coverage. No broad native A2/A3 replay was needed or claimed.

### Targeted final native replacement and close evidence

One bounded owned Tests.exe process, PID **18352**, hosted the production App using only the existing test-instance publisher barrier/fault seam. Normal Studio has no fault CLI. Ignored fixture: `Axora.Studio.Tests/logs/native-864b4ef7fa57471e85ee26a58a531e77`; content is synthetic. `logs/m2-remediation-native-attempt1.log` exited 0 on its first attempt.

The fixture begins at current revision 2 with a verified selected backup revision 1. Held Recovery failure: actual title-input UIA IsEnabled=false and Undo IsEnabled=false, while status remains enabled; old visible content stays. Failure release restores enabled controls and exact original editor/disk bytes. Held Recovery success likewise disables controls; release re-enables them with correct selected content and new revision 3. No unsafe native edit was attempted. This is native control-state evidence, not a new screenshot/theme/accessibility-matrix claim.

A third held Recovery selected the original current content. An actual owned-window CloseMainWindow request was causally observed while replacement remained held; the process stayed alive and Host disposed was absent. Release settled Recovery before Host disposal, produced selected content at revision 4, and exited normally with code 0. No owned process remains. Deterministic Session tests separately cover held Import as well as Recovery close. All six historical native journeys were not replayed.

New production-log privacy scan covers the one newly produced native startup log: **zero PRIVATE_SENTINEL/content/private-fixture-path/email matches**. New maintenance/recovery diagnostics emit safe phase/result/exception type, not exception messages, Resume text or backup paths. Test-local diagnostic privacy assertions also passed. The earlier 15-log scan remains historical evidence, not a substitute for this new-log check.

### Final hostile internal re-audit

The final production repair was reread after the final builds/tests; all twelve required attacks were applied again alongside the F1/F2/F3 causal cases. This is an internal Codex review, bounded to the repaired contract.

| Attack | Decisive protection / evidence | Remaining severity |
|---|---|---|
| 1 Edit before UI disable catches up | Session gate admits replacement and rejects Edit under one lock; held Import/Recovery session tests | None |
| 2 Programmatic bypass | Edit, invalid draft and Undo enforce the same gate independently of XAML | None |
| 3 Failure permanently disables editing | Finally clears ownership; four success/failure cases and native failed Recovery re-enable | None |
| 4 Success edits stale document/VM | Activation version plus editor generation reject stale fields/actions; rebuilt editor accepts new-document edits | None |
| 5 Ordinary Save freezes editor | Save/Save Copy remain non-replacement operations; held VM edits accepted and remain dirty | None |
| 6 Commit precedes fallible hash capture | Hash computed before atomic publication; committed flag set immediately afterwards | None |
| 7 Warning converted to Failed | Typed published warning returns Saved and promotes baseline/hash; real rotation damage cases | None |
| 8 Warning clears later edits | Immutable captured baseline only; causal maintenance hold + later live edit + modifying retry | None |
| 9 Corrupt unselected backup deleted | Candidate exclusion never deletes; byte-for-byte preserved artifacts after recovery and retry | None |
| 10 Invalid selected silently ignored | Strict selected validation before guard/publication; malformed/future negatives | None |
| 11 Identity-mismatched metadata trusted | Store ownership/hash checks, identity and lineage validation; isolated mismatch cases | None |
| 12 Duplicate/non-monotonic publication | Maximum trustworthy revision + checked increment and independent publisher comparison; duplicate control | None |

F1 additionally covers text/structural/invalid input rejection, failure/edit-again, success/edit-new-document, ordinary Save later edits and close retention. F2 covers every required artifact class, commit-before-warning, later edit, immediate modifying Save and original pre-commit failure controls. F3 covers selected-valid/bad-unselected, selected-invalid/good-alternatives, duplicate revisions, identity/future/unreadable candidates and exact preservation/reopen. No remaining BLOCKER/HIGH/MEDIUM was found within this scope.

Accepted debt is unchanged: **LOW-1** deliberate same-user final-check/atomic-replace race; **LOW-2** unestablished Release/AOT/platform/full accessibility matrix, including inherited generator warnings; **LOW-3** cosmetic UIA sidebar selection marker. This remediation did not change their assumptions or repair them. BLOCKER 0 / HIGH 0 / MEDIUM 0 / LOW 3. PDF/Typst/LaTeX/ATS/autosave/deletion/cutover/M2-B/M2-C/STT remain outside this slice.

### Unstaged freeze and required next audit

Final Git verification retains HEAD = origin/main = independently queried actual remote main = `020afb120f046b74907318a70df07cba1737382e`, main, ahead/behind 0/0, index empty and exactly the original 18 new / 9 modified / 27 authored paths unstaged. Archive branch remains `a518f04ba7188a93ec7d7334879c194699db59b9`; legacy tag object `4ea4271029ed7b368b0d86463df264de70f7518b`, peeled archive SHA. Diff whitespace check passes; Git reports existing LF-to-CRLF normalization notices separately. Protected legacy/MaterialUI/Tools/Mind/W5 paths, prior evidence and all project/package/toolchain files have no authored changes.

The ignored `logs/m2-authored-sha256.json` is regenerated for all 27 final paths after this evidence write, then every entry is rehashed and checked. `logs/m2-remediation-freeze-audit.json` records exact scope, six changed hashes against the retained pre-remediation inventory, privacy and final baseline verification. This evidence contains no circular self-hash. No stage/commit/push or next-slice work occurred.

A true independent Antigravity/Claude Sonnet M2-A audit remains REQUIRED. The repaired checkpoint is ready for that audit; acceptance by that model has not occurred here.

**STUDIO-M2-A REMEDIATION PASS — READY FOR TRUE INDEPENDENT M2-A AUDIT**

## ChatGPT external-review X1/X2 remediation — 2026-10-10 UTC

### Provenance and baseline

The supplied external-review instructions report a separate source review in **ChatGPT / GPT-5.6 Sol / High reasoning**, reading the flattened post-remediation source/test/evidence/log bundle. That review found BLOCKER 0 / HIGH 0 / MEDIUM 2: X1 per-file InvalidDataException isolation, and X2 preserved corrupt revisions poisoning recovery bounds. This was not a Claude/Sonnet audit. Antigravity Sonnet was unavailable due quota. The prior Codex F1/F2/F3 remediation remains historical, and its outstanding external acceptance was not silently converted into a pass. This section records Codex repairs in response to the supplied ChatGPT review; updated external review remains pending.

Admission baseline was branch main, HEAD = origin/main = independently queried live remote main = `020afb120f046b74907318a70df07cba1737382e`, ahead/behind 0/0, index empty. All 27 current authored hashes matched the prior frozen inventory before tests changed. The untouched pre-X1/X2 inventory is retained as `logs/m2-pre-x1x2-authored-sha256.json`. Git listed exactly 18 new / 9 modified / 27 authored files; no unrelated archive was opened or otherwise accessed.

Exactly five existing authored paths are repaired: `Axora-Desktop-WinUI/Axora.Studio/Services/ResumeStore.cs`, `Axora-Desktop-WinUI/Axora.Studio/Services/ResumeFilePublisher.cs`, `Axora-Desktop-WinUI/Axora.Studio/Models/ResumeLimits.cs`, `Axora-Desktop-WinUI/Axora.Studio.Tests/ResumeTests.cs`, and this evidence file. No other authored bytes change from the pre-X1/X2 freeze. In particular Session, VM, editor XAML, App/Program, HostTests, routes and runners remain byte-identical. No package, project, toolchain, protected legacy/MaterialUI/Tools/Mind/W5 or earlier evidence changes.

### X1 MEDIUM: per-candidate invalid-data isolation

Codec Decode intentionally throws InvalidDataException for unsupported schema and envelope/semantic validation. Store listing/provenance catches omitted that exception, so valid JSON with invalid semantic/envelope content escaped candidate isolation. Invalid/noncanonical managed filenames likewise failed DocumentPath validation outside the existing exception families. The older malformed-JSON lazy-store case only exercised JsonException.

The repair adds **only InvalidDataException** to the two existing candidate catch filters. Root ownership checks, library enumeration, per-library document/folder ceilings and page bounds remain outside those catches. Candidate isolation cannot swallow those whole-library failures. Dashboard returns an unavailable row for the bad candidate while preserving healthy rows. FindImport excludes the candidate and continues its bounded scan. Invalid/future candidates are never rewritten, downgraded, deleted or automatically quarantined by those read paths. Deterministic import publication still refuses an occupied invalid/unreadable target rather than overwriting or creating another identity.

Six mandatory new cases (`x1-future`, `x1-semantic`, `x1-filename`, `x1-import-future`, `x1-import-semantic`, `x1-import-collision`) cover future-schema, invalid Schema-1 envelope identity, invalid filename, provenance continuation, successful unrelated reviewed import, exact excluded bytes, unreadable and invalid deterministic target collisions with no duplicate/overwrite, and both library-wide ceilings. Every case failed before production repair; all pass after it.

### X2 MEDIUM: preservation without poisoning active recovery

The original production sequence was reproduced physically: five backups; damage oldest; first modifying Save commits and warns while preserving damage; second modifying Save commits, producing seven active candidates; Backups throws because its six-artifact ceiling is exceeded. Maintenance simultaneously refused a seven-file set and never reduced it. This trapped recovery behind preservation and allowed subsequent publication to grow the active directory.

The final contract keeps **RevisionBackups = 5** as the normal verified active retention target. **RevisionArtifacts = 32** is a separate finite degraded-state scan/admission budget, not a larger retention target. Store reads at most 33 directory entries and explicitly fails if more than 32 are observed. Within that budget it exposes the bounded candidates for strict selection, so temporary maintenance failure at six/seven does not hide every valid revision. Publisher refuses creation of a new prior-revision backup at an already-full 32-artifact budget **before destination commit**. It does not silently abandon an admitted commit or continue unbounded growth.

When active candidates exceed five, maintenance classifies the bounded snapshot individually in descending order. Only strictly decoded same-document/same-CreatedUtc/owned filename-and-hash revisions count toward the five retained valid revisions. Invalid high-sorting filenames cannot displace the valid retention set. Extra verified valid revisions may be pruned under the existing retention policy. Invalid artifacts are never treated as authoritative revision metadata.

An invalid candidate whose bounded bytes can be captured is preserved at owned `Quarantine/<documentId>/revision_<SHA256>.json` using CreateNew or exact-byte idempotent verification. Different existing bytes at that identity are never overwritten. Preservation is reopened and verified; quarantine ownership and bytes are checked again; the original active candidate is re-read and must still match the captured bytes. Only then is that preserved suspect removed from active Revisions. Future-schema and wrong-ID bytes are preserved exactly without decode/rewrite at the quarantine boundary. There is no delete-before-preserve or delete-first replacement fallback.

Unreadable/oversized/redirected candidates which cannot be safely captured remain active and generate a maintenance warning. Preservation, collision, revalidation or removal failures also warn, retain the original where deletion has not safely completed, and do not abort independent safe pruning of other extra candidates. A single blocked suspect therefore stabilizes at five valid active revisions plus that retained suspect, rather than growing with every successful Save. When the obstruction clears, later maintenance preserves evidence and restores five active valid revisions. Persistent widespread obstructions stop new growth at the explicit finite admission budget; this does not claim successful cleanup or unlimited storage.

Encountering a suspect reports committed-with-maintenance-warning even when quarantine succeeds. Destination hash/baseline promotion remains the existing F2 committed result contract. No new content/path/filename/exception-message diagnostic is emitted. Fatal exception types remain outside the explicit expected artifact catch family. Existing stage ownership, verified backup-before-replace, destination recheck, atomic publication and captured-hash boundaries are unchanged.

Nine new mandatory cases (`x2-json`, `x2-future`, `x2-identity`, `x2-ownership`, `x2-preserve-failure`, `x2-identical`, `x2-collision`, `x2-degraded`, `x2-bound`) exercise real filesystem corruption, first commit/warning, modifying second Save, eight more Saves, exact preservation, recovery content/monotonicity, future and wrong-ID preservation, injected quarantine preservation failure, identical/different-byte collisions, recovery under warning, later obstruction removal, seven-artifact degraded enumeration, high-sorting invalid names, admission at 32 and scan rejection at 33. No sleeps. Seams are publisher-instance scoped and dormant in normal composition.

Existing `f2-maintenance-*` assertions now require exact suspect bytes at their verified quarantine identity, active removal only after preservation, correct warning/baseline and modifying retry with bounded recovery. This updates their location expectation for the authorized X2 preservation contract; it does not drop their integrity assertions. F1 replacement ownership and ordinary Save/Save Copy behavior, F3 unselected/selected strictness and duplicate-revision controls remain mandatory and green. F3 non-excess unselected artifacts are still left in place; maintenance is only triggered when retention is needed.

### RED, refinement and Phase-1 GREEN chronology

Tests were authored first; `logs/m2-x1x2-red-build.log` built them against unchanged production. `logs/m2-x1x2-red.log` exited 1: **320 passed / 22 failed**, complete=true, zero missing/duplicate/unknown/blocked. All **15 new cases** failed specifically on X1/X2; the existing 52 cases remained green. The actual X2 second Save and ensuing Backups exceptions are preserved, not inferred from source counts.

Initial repair build (`m2-x1x2-targeted-build.log`) succeeded; initial GREEN (`m2-x1x2-green-initial.log`) passed 477/0. Hostile reread added verified-valid counting so invalid high-sorting names could not displace valid retention, with explicit high-name and 32/33-bound controls. The first refinement build (`m2-x1x2-refined-build.log`) failed CS0173 for an untyped conditional empty collection; explicit Enumerable.Empty<string>() corrected it (`m2-x1x2-refined-build-2.log`). These failed/superseded attempts remain recorded.

Final targeted Phase-1 gate (`m2-x1x2-phase1-green.log`) passed **484/0**, **67 mandatory cases**, complete=true, all ledger defect counts zero. The final reread attacked future/semantic-invalid managed files, invalid filenames, provenance/collision isolation, six/seven revisions, repeated Saves, preservation failure/collision, future/wrong-ID revisions, warning recovery, duplicate revisions and invalid selected backups. No remaining BLOCKER/HIGH/MEDIUM was found in X1/X2 or affected invariants. Phase 1 PASS preceded full regression.

### Phase 2 final acceptance evidence

Repository SDK pin remains 9.0.300/latestPatch; actual SDK **9.0.318**. Fresh Debug/x64 Rebuilds after the final source/test edit: Studio (`logs/m2-x1x2-final-studio-build.log`) exit 0, **7 inherited MVVMTK0045 warnings / 0 errors**, 00:00:21.94; Tests (`logs/m2-x1x2-final-tests-build.log`) exit 0, **0 warnings / 0 errors**, 00:00:09.17. Tests rebuilt with BuildProjectReferences=false after the separate fresh Studio rebuild. No suppression or toolchain changes.

Final intended M2 runner (`logs/m2-x1x2-final-runner.log`), outer exit 0 and all five child exits 0:

| Group | Passed assertions | Failed / missing / duplicate / unknown / blocked | Complete |
|---|---:|---|---|
| STUDIO-M2-RESUME (67 cases) | 484 | 0 / 0 / 0 / 0 / 0 | true |
| STUDIO-H0 | 202 | 0 / 0 / 0 / 0 / 0 | true |
| STUDIO-M1-FLASHCARDS | 172 | 0 / 0 / 0 / 0 / 0 | true |
| STUDIO-M1-EXPORT | 289 | 0 / 0 / 0 / 0 / 0 | true |
| STUDIO-M1-READALOUD | 156 | 0 / 0 / 0 / 0 / 0 | true |

Total **1,303 passed assertions**. Every ledger selected registered/executed 1/1, full manifest 5, disposition Pass. Final M1 runner (`logs/m2-x1x2-final-m1-runner.log`) outer/children exit 0 and exactly its original H0/Flashcards/Export/ReadAloud groups. Dispatch results (`logs/m2-x1x2-dispatch.json`) for unknown group, empty group, unknown option and actual two arguments all exit 2 with zero ledgers. No broad native replay: production-path deterministic tests physically establish quarantine writes/reopen/byte comparisons, pruning, recovery and admission bounds.

`logs/m2-x1x2-causal-privacy.json` points to the full final runner and its SHA-256, extracts 164 actual passing X2 filesystem/causal assertions and records nine passing production-diagnostic privacy cases. Test-local production diagnostic lists contain neither sentinel Resume content, private paths, filenames nor raw failure messages. Publisher introduces no diagnostic output. No new native startup log was generated or purportedly scanned for this slice; the earlier native scan remains historical. This distinction avoids substituting test-stacktrace logs for production diagnostic privacy evidence.

### Final scope, freeze and repack boundary

The original authored set remains **18 new / 9 modified / 27 total**, index empty, all unstaged. Exactly the five allowed repair hashes differ from the pre-X1/X2 inventory; the other 22 remain unchanged. Whole-root/protected paths and legacy source remain untouched. HEAD = origin/main = live remote main = `020afb120f046b74907318a70df07cba1737382e`, main, ahead/behind 0/0. Archive branch and final legacy tag refs remain unchanged. The unrelated archive was never opened, hashed, copied, extracted, modified, deleted or staged. No commit, push, renderer installation or next-slice work.

All 27 hashes are regenerated in ignored `logs/m2-authored-sha256.json` after this final evidence write and rechecked. `logs/m2-x1x2-freeze-audit.json` records the final scope/hash/privacy gate. The ZIP and flattened UTF-8 review text are regenerated only after these acceptance gates, outside the repository, at the previously specified dedicated TEMP names. Their manifests explicitly distinguish historical F1/F2/F3 totals from final X1/X2 totals; they include all 27 current files, nine baseline copies, full diff, updated evidence and relevant logs. No circular evidence self-hash or external-review acceptance claim is embedded here.

Remaining accepted LOW debt is unchanged: LOW-1 external same-user mutation after final ownership/hash check; LOW-2 unverified Release/AOT/platform/full accessibility matrix and inherited generator warnings; LOW-3 cosmetic UIA sidebar marker. Hash-addressed quarantine is bounded per captured artifact/operation, but global historical storage quota/deletion/cutover remains an explicit exclusion. BLOCKER 0 / HIGH 0 / MEDIUM 0 / LOW 3 within the audited contract. Updated external review is still required; this Codex remediation is not that verdict.

**STUDIO-M2-A EXTERNAL-AUDIT REMEDIATION PASS — UPDATED CHATGPT REVIEW PACK READY**

## User acceptance and local checkpoint authorization — 2026-10-10

The user explicitly accepted M2-A and authorized the local checkpoint commit. This acceptance follows the frozen X1/X2 remediation and evidence above; it does not assert that another independent external review ran. Earlier review requirements and freeze statements are retained as historical chronology.

The checkpoint contains the same 27 authored paths (18 new, 9 modified). Only this evidence closeout changes after the final freeze; all other 26 file hashes remain identical. The existing final fresh Debug/x64 builds and 1,303 passing regression assertions therefore remain the validation for the accepted implementation; no build or test rerun is claimed for this documentation-only closeout. The three recorded LOW items and excluded runtime/platform coverage remain unchanged.

Authorization is for one local checkpoint commit only. No push or subsequent implementation slice is authorized by this acceptance.