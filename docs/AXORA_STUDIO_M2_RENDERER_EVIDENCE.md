# STUDIO-M2-B1 renderer foundation evidence

**STUDIO-M2-B1 IMPLEMENTATION PASS — READY FOR EXTERNAL REVIEW**

Date: 2026-10-11 (Asia/Calcutta). This is implementation and self-review evidence, not acceptance by an independent external reviewer. No M2-B2 authority is implied.

## Admission and preservation

Baseline: main; HEAD = origin/main = independently queried live GitHub main = 248b44b274388731c6c5c20ae8653e04ee8b9031; ahead/behind 0/0. Initial index/tracked worktree empty; exactly nine approved untracked B1 drafts. The unrelated archive was never opened, hashed, extracted, modified, deleted or staged.

The current brief accepts M2-A as published/frozen, B0 as PASS, native feasibility as PASS, ACL forensics as PASS after follow-up, and the external final security gate as PASS. Those are supplied accepted prerequisites; the feasibility investigation was not repeated. Production-path tests below are new evidence.

Before editing, all nine hashes matched the frozen preflight inventory in the historical B1 brief. Exact copies and original-sha256.json were preserved and verified in persistent user TEMP at C:\Users\rajghosh\AppData\Local\Temp\AXORA-M2B1-PRE-SALVAGE. An initial sandbox TEMP copy was ephemeral; persistent copies were verified before editing.

| Original draft, relative to Axora-Desktop-WinUI | Original SHA-256 |
| --- | --- |
| Axora.Studio/Models/Rendering/RendererModels.cs | 88E34C2BE3589316F69768CADF1528EA21FF84AA8A5B902E0DDF1B9A4DBD1491 |
| Axora.Studio/Models/Rendering/RendererRuntimeManifest.cs | E399BCACD8B7EF14D496392E9A491A9D70DCB5D50FD6F641B0961C27E6016832 |
| Axora.Studio/Services/Rendering/IRendererProcess.cs | 410C5DD3B79D7261021198522F1AA6CFF2F6FFB720B467891A754F8852B24355 |
| Axora.Studio/Services/Rendering/RendererWorkspace.cs | 2F6FD07258805491F872C8EA6571C9C1B16B3C934C29C65F595B7953B0D9E9B3 |
| Axora.Studio/Services/Rendering/TrustedRendererFixture.cs | 7C236D08654F10417748CC5B5ECA2DD91C8F6CB331D14D5AA8F77E31071EF1D1 |
| Axora.Studio/Services/Rendering/TypstRuntimeVerifier.cs | 7E06C5975386AB94DBECB9A08443FA1CD7767C18910B729AD2BD294A1A752F8C |
| Axora.Studio/Services/Rendering/WindowsRendererJob.cs | 8FC883D08E84630FB15D3A11A9D75BFDAECC7FFE5FB2DFEABDDBFD7BC52E7F4E |
| Axora.Studio/Services/Rendering/WindowsRendererProcess.cs | D01C1AE6D2DCAA5FAD676C2800D9E3281C27AF9E79A3F46066E8C3EEC70C6144 |
| Axora.Studio/Services/Rendering/WindowsRendererSandbox.cs | 9A21F100876347D798A07A360BCA92539F00032DF98E98BB21572A7FC229BCAC |

Fresh untouched Debug/x64 Rebuild of Studio and Studio.Tests each exited 1 with real CS1503 at RendererWorkspace.cs:95: invalid path.Contains(':', 2) overload. Logs: red-Axora.Studio.log and red-Axora.Studio.Tests.log in the preservation directory. No manufactured failure.

## Draft hostile review and repairs

Every original draft was read. Valid identity, fixture and Job definitions were retained.

| Finding | Original severity | Repair |
| --- | --- | --- |
| Invalid path overload prevents both builds | BLOCKER | Canonical drive-local validation; reject traversal, UNC/device paths, streams, trailing aliases and reparses |
| Existing-resource ACL grants; random profile with incomplete restoration | HIGH | Remove grant/restore design; no ACL setter in renderer production code |
| Pins released before path-based cleanup | HIGH | Atomic directory creation returns identity handles; delete by retained handle, close and verify absence while parent stays pinned |
| Result created before finally completes | HIGH | One native result after tree/readers/handles/security settlement; coordinator then applies workspace cleanup outcome |
| Runtime launch string separated from verification lease | HIGH | RuntimeLease required; OPEN_REPARSE_POINT, single-link file, denied write/delete sharing, ancestor pins, immediate prelaunch hash/security check |
| Unbounded reader joins and reader-owned Job termination | HIGH | Dedicated reader threads, bounded joins/cancellation; supervisor owns termination |
| Partial setup destroys uninitialized attributes or hides cleanup failure | MEDIUM | Initialization flag, staged ownership, typed failed setup cleanup/quarantine |
| Delete-pending/unknown state treated as generic cleanup/absence | MEDIUM | Only FILE_NOT_FOUND/PATH_NOT_FOUND prove absence; typed DeletePending; failed later shutdown |
| Admission/shutdown ownership races | MEDIUM | Publish retained task under lock; one owner; close admission after unresolved state |
| Exit sampled while still active | MEDIUM | Final exit after root wait; STILL_ACTIVE cannot be a settled exit |

Final bounded self hostile reread: **BLOCKER 0, HIGH 0, MEDIUM 0**. LOW debt below. This does not claim protection against administrator compromise or hostile same-user process-memory modification.

## Security model

Authoritative rule: **SECURITY-SEMANTIC RESTORATION / NON-MUTATION GATE**. Preferred production rule: **DO NOT MODIFY PREEXISTING ACLs AT ALL**.

Accepted earlier gate: %TEMP%\AXORA-M2B1-FINAL-SECURITY-GATE\635573a7149341bcb1da8a9e141056bf. Its audit records eleven cases, zero active owned trees, settled readers, AppContainer/zero capabilities, seven equal preexisting raw descriptors, zero residual temporary ACEs and zero delete-pending files. That external test used a temporary test profile. Its handle-coverage limitation remains queryable disk-file handles only, excluding protected/unqueryable processes/handles and mappings without handles.

Production profile: Axora.Studio.Renderer.Typst.V1, per Windows user. Create once with no capabilities; derive SID if already present. Each launch independently verifies TokenIsAppContainer, exact expected TokenAppContainerSid and zero TokenCapabilities before ResumeThread. Operations free their SID pointers but never delete the persistent shared profile.

Creation-time protected DACL: full control for current user and SYSTEM; exactly 0x1200a9 read/execute for this AppContainer SID. No inherited, ALL APPLICATION PACKAGES or network capability ACE is added. Private synthetic sentinel omits the AppContainer ACE. Existing resources require exact DACL bytes, owner, group and protection; incompatibility returns provisioning failure, never ACL repair. No SetNamedSecurityInfo, SetEntriesInAcl, SetAccessControl, grant/restore or preexisting ACL widening exists in renderer code.

Runtime ancestors/file stay pinned against write/reparse/rename/delete substitution as Windows sharing permits. Workspace directories are created and pinned atomically with NtCreateFile. Source uses one creation/write/hash/read/delete handle. Fixture hash and owned security are checked before launch and while supervising. Unexpected entries are rejected and never swept recursively.

Settlement order: root exit and Job zero-active observation; both reader tasks; process/thread/pipe/attribute/environment/Job/security finalization; then identity-bound operation deletion, closing each handle and checking namespace absence while parents remain pinned. Access denied, delete-pending and unknown state fail. Unsettled/failed workspaces retain pins in an in-process quarantine; owning coordinator closes admission and fails shutdown. Deferred cleanup is never success.

Primary native references: [AppContainer profile/launch](https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer), [handle-based disposition](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle), [NtCreateFile](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntcreatefile).

## Typst provenance and provisioning

Typst 0.15.1; exact version output typst 0.15.1 (9dfd3a08); AMD64 PE; executable 52,487,680 bytes.

- Executable SHA-256: 081217A463ADB006F8894B44227FE4B9C9E91FC85F5463D5948E8370DB9BB31E.
- Official archive SHA-256: 19CE3551153C2FE7EE9FA2F95208310C8F4D3209FEDB699E0333FAF8913F6736.
- Release commit 9dfd3a08500b7896045f907433cf7b4b02434fad; asset 480303335; archive 22,463,684 bytes; Apache-2.0.
- [Official release asset](https://github.com/typst/typst/releases/download/v0.15.1/typst-x86_64-pc-windows-msvc.zip). Existing authenticated release evidence is included in the review pack; no new download was needed.

Actual test source: %TEMP%\AXORA-M2B1-TYPST-FEASIBILITY\runtime\typst.exe. Runner rehashes it; provisioner authenticates the locked source and destination. No binary/archive/font/PDF enters Git.

Expected production path:
%LOCALAPPDATA%\Axora\Studio\Renderers\typst\0.15.1\win-x64\081217A463ADB006F8894B44227FE4B9C9E91FC85F5463D5948E8370DB9BB31E\typst.exe.

Explicit provisioning must create Renderers and the manifest hierarchy/file with prescribed descriptors. Existing ancestors above Renderers are never re-ACLed. Internal provisioning utility is test/installer support, never invoked at startup. It creates new destinations and refuses replacement/ACL repair. Adjacent JSON is not authority. No PATH search, shell, self-update, download fallback or missing-runtime startup dependency.

Native commands are sandboxed --version followed by compile --format pdf --root OWNED_INPUT --ignore-system-fonts --package-path EMPTY_OWNED_PACKAGES --package-cache-path EMPTY_OWNED_CACHE --diagnostic-format short OWNED_SOURCE -. Environment contains only owned app/cache/temp/profile paths and SystemRoot. Fixtures need no system font discovery, external package or user document.

## Behavior and limits

Request accepts only GUID, trusted fixture enum and budget enum. No arbitrary user source/Resume, executable or command is accepted. Binary stdout stays binary. Public result exposes counts/hash/timings/typed state, never PDF bytes, source, paths or raw stderr.

One active request per coordinator; second request returns Busy. DI factory and session are lazy: composition, startup and unused shutdown create no renderer/native resources. Shutdown immediately closes admission, cancels, retains one settlement task and waits before host disposal. Existing Export/ReadAloud/Resume retention and host cleanup remain intact.

Limits: PDF stdout 16 MiB; combined diagnostics/non-PDF stdout/stderr 128 KiB; Job committed memory 512 MiB; four active processes. Overflow stops tree and discards incomplete output. Job is non-inherited and kill-on-close. Create suspended, assign/verify Job and token, then resume. Version timeout five seconds; compile Preview/Final timeouts 15/60 seconds. Dedicated concurrent readers avoid blocking ThreadPool work. Root exit does not imply descendant/pipe settlement.

## Native tests

Final standalone evidence: %TEMP%\AXORA-M2B1-NATIVE-5779cdcef570421488fc740e580650f5\renderer.stdout.log.
Final regression-native root: %TEMP%\AXORA-M2B1-NATIVE-193b5de08c61451b882c5d5a5607821c.
Runner builds static C++ probes in external TEMP; copies receive protected creation-time ACLs and pinned/hashed identity. Helpers supplement mandatory real authenticated Typst.

**45 required cases, 107 passing renderer assertions**. Complete=true; failed/missing/duplicate/unknown/blocked=0. Mandatory real version, ASCII, Unicode process probe and offline clean-cache compile passed.

Coverage: absent/provisioning/valid runtime/hash/version/architecture/unexecutable; runtime/workspace reparse/substitution; immediate launch substitution attempt; startup/partial setup; exact binary bytes; stdout/stderr/dual flood; timeout/cancel/nonzero; real invalid-handle Job assignment rejection plus injected suspended-child assignment failure; pre-resume exception; pipe-holding descendant; traversal; fixture tamper denial; exact owned ACLs; owned-write denial; private-file ACCESS_DENIED; completed socket WSAEACCES 10013; delete-pending typed failure; Busy; native/supplemental active shutdown; repeated/unused shutdown; admission/shutdown races; cleanup/privacy.

Wrong version and coordinator concurrency use supplemental mocks. Pre-resume exception is a failure-path injection, not a claim to have forced OS ResumeThread itself to fail. Architecture mutation fails authentication before execution; no second architecture is trusted. Production hooks are dormant.

First native run: 93 assertions, three real failures (symbolic-link privilege unavailable in two probes; nonblocking network probe inspected only initial error). Privilege-free junctions and completed select/SO_ERROR inspection repaired the probes. Later production hardening/tests increased final coverage to 107. Earlier failed logs remain available and are not recast as passes.

Fourteen distinct preexisting/stable owner/group/protection/inheritance/DACL descriptor snapshots match before/after: repository roots, external source, test parent, provisioned hierarchy/helper/operations. Coverage is the readable owner/group/DACL descriptor, not a new privileged SACL or universal system-handle audit. Production has no existing-resource security mutation API. Temporary ACEs disappear with operation resources.

Deliberate retained-handle injection returns DeletePending, CleanupComplete=false, no PDF metadata, permanently closed admission and failed later shutdown. Releasing the synthetic handle removes the resources. Final operation-directory inventory is empty; no final delete-pending artifact was observed.

## Builds, regressions and dispatch

VS 18 Community MSBuild 18.10.1, Debug/x64, normal WinUI/XAML pipeline. Both final fresh Rebuilds exited 0 with **0 errors**. Studio has seven existing MVVMTK0045 warnings; Tests adds the existing WindowsAppSDK CS0436 conflict (eight). Protected warning sources were not edited.

Logs under %TEMP%\AXORA-M2B1-IMPLEMENTATION:
final-build-Axora.Studio.log; final-build-Axora.Studio.Tests.log; final-renderer-runner.log; final-regression-m1.log; final-regression-m2.log; final-manifest.json; dispatch-probes.json; final-run-exits.json. M1 and M2 runner exits are both 0.

| Group | Passing assertions | Ledger |
| --- | ---: | --- |
| STUDIO-H0 | 207 | Pass, complete |
| STUDIO-M1-FLASHCARDS | 172 | Pass, complete |
| STUDIO-M1-EXPORT | 289 | Pass, complete |
| STUDIO-M1-READALOUD | 156 | Pass, complete |
| STUDIO-M2-RESUME | 484 | Pass, complete |
| STUDIO-M2-RENDERER | 107 | Pass, complete |
| Final M2 runner total | 1415 | Six groups, once each |

All six: complete=true; failedAssertions/missing/duplicate/unknown/blocked=0. Counts are observations, not hardcoded pass totals. M1 validates six-group manifest and selects its original four groups. M2 adds renderer and requires explicitly prepared external native prerequisites.

Unknown group, H0 extra argument, renderer extra argument, and manifest extra argument each exit 2 with exactly “Unknown arguments/group. No tests ran.” and no test ledger.

## Final reread, privacy and performance

Reattacked verification-to-launch, runtime/workspace/in-place reparse substitution, fixture race, partial sandbox/profile setup, profile collision, assignment/resume failure paths, cancel-vs-exit/deadline, stdout/stderr saturation, descendant-held pipes, binary handling, unexpected entries, cleanup with live ownership, delete-pending, admission/shutdown, offline/package behavior and private diagnostic leakage. Bounded self-review disposition is zero remaining BLOCKER/HIGH/MEDIUM.

Actual authenticated compile results: AppContainer=true, ZeroCapabilities=true, JobMembership=true, CleanupComplete=true. ASCII PDF 13,982 bytes; Unicode probe 16,395 bytes. PDF hashes vary by metadata; no deterministic-byte claim. RENDERER-NATIVE and RENDERER-PROBE preserve measured timings, verification/settlement costs and Job peak committed bytes. These are this-machine observations, not cold-start, working-set, representative-device or production performance claims. Job committed bytes exclude image mappings/full working set. Authentication cost is intentionally retained.

Privacy: B1 reads synthetic source only. No real Resume data is consumed. Public renderer results omit raw diagnostics, user paths and source. Review artifacts contain source and bounded text evidence only. No PDF parser/package was added.

## LOW debt and exclusions

1. Production installer provisioning/update/uninstall and persistent-profile removal remain future work; per-render deletion would collide with the deterministic identity contract.
2. Unknown settlement/cleanup retains pins in quarantine and requires recovery/restart instead of automatic sweeping; it is a failed operation, never successful deferred cleanup.
3. Native lab runner expects recorded VS 18/MSVC 14.51.36231 and SDK 10.0.26100.0 paths. RuntimeSource is configurable; CI/toolchain generalization is future work.

Explicitly: **no PDF-validation claim; no Hindi-layout correctness claim; no Resume UI integration; no production installer completion; no LaTeX/Tectonic**. No ResumeRenderSnapshot, preview, user PDF export, ATS, persistence change or M2-B2.

## Exact scope and freeze

Exactly **20 authored allowlisted paths**, fourteen new and six modified. Nine original drafts are part of the fourteen new files.

New: the two Rendering models; TypstRuntimeVerifier, RendererWorkspace, WindowsRendererProcess, WindowsRendererSandbox, WindowsRendererJob, RendererCoordinator, StudioRendererSession, IRendererProcess and TrustedRendererFixture under Services/Rendering; RendererTests.cs; run-m2-renderer.ps1; this document.

Modified only: Axora.Studio/StudioBootstrap.cs; Axora.Studio/App.xaml.cs; Axora.Studio.Tests/Program.cs; Axora.Studio.Tests/HostTests.cs; Axora.Studio.Tests/run-m1.ps1; Axora.Studio.Tests/run-m2.ps1.

Protected Resume models/services/UI, routes/MainWindow, production Program/StudioLifecycle/StudioPathService, existing Export/ReadAloud, project/package/solution/pin/manifest files, legacy Desktop/tests, MaterialUI, Tools, Mind, W5 and M2-A evidence are untouched. Generated build outputs/existing ignored runner logs are not authored source.

Final source hashes/inventory, full bounded diff, original draft copies/hashes, baseline six modified files and git-freeze.json are in the external review pack. Freeze: main; HEAD = origin/main = independently queried live remote = required baseline; ahead/behind 0/0; index empty; all work unstaged. No staging, commit or push.

External deliverables: %TEMP%\AXORA-M2B1-POST-IMPLEMENTATION-AUDIT.zip and %TEMP%\AXORA-M2B1-CHATGPT-REVIEW.txt. Stop afterward; no M2-B2.

## 2026-10-11 — B1R external-audit remediation chronology

The preceding B1 self-review and counts are historical. **ChatGPT GPT-5.6 Sol High external review returned BLOCKER 0 / HIGH 0 / MEDIUM 1 (X1)**: operation cleanup did not establish a persistent/ambient AppContainer state boundary. That review supersedes the earlier zero-MEDIUM self-review; it was not an accepted checkpoint.

Admission was reconfirmed against `248b44b274388731c6c5c20ae8653e04ee8b9031`: main, HEAD/origin/live remote equal, ahead/behind 0/0, index empty. All twenty authored SHA256 values matched the frozen B1 inventory before experiments and again before the first B1R source edit. No source was edited until the external managed-profile design passed.

### RED proof and alternatives

External experiment root: `%TEMP%\AXORA-M2B1R-BOUNDARY-da115b8b79c74a1f9a7cdc527db0b3de`. `phase1-verdict.txt` records the gate; failed alternatives remain explicit in logs. The full managed lifecycle evidence is the final `phase1-native17.log` and corresponding JSON snapshots, including package-root/AC-subtree and registry owner/group/DACL before and after hardening.

**X1 REPRODUCED — APPCONTAINER HAS PERSISTENT WRITABLE PROFILE STATE.** Under the original deterministic V1 identity, a native helper wrote known synthetic bytes in the official `GetAppContainerFolderPath` location and reopened/read them. The same standard model obtained its native registry location and set/read/deleted a synthetic value. The file was then independently read and removed, with absence verified. Cleanup does not negate the successful RED write. Native `NtQueryKey` established that registry Storage is keyed by the profile moniker, while Mappings is keyed by SID; both are now checked.

A protected external sentinel granted owner/SYSTEM management and AAP read, with no explicit renderer SID ACE. Standard AppContainer could read it; LPAC denied it. LPAC used three native attributes: explicit handle list, zero-capability security capabilities and `PROC_THREAD_ATTRIBUTE_ALL_APPLICATION_PACKAGES_POLICY` with opt-out DWORD 1. Authenticated Typst version, ASCII, Unicode and clean-cache offline compilation worked. However, the required descendant-held-pipe helper's `CreateProcessW` failed with **Win32 5**; an explicitly secured child-object variant also failed. Winsock initialization returned **WSAEPROVIDERFAILEDINIT 10107**, not WSAEACCES. Neither is recast as a passing descendant/network test. No extra capabilities, privileges or system ACL changes were used to force compatibility.

Token information class 46 returned Win32 87 on this machine. Early diagnostic `TOKEN_LPAC` text represented requested configuration, not an observed token flag. `CheckTokenMembershipEx` and a simple `AccessCheck` returned false in both controls and were not used to prove the LPAC distinction. The controlled native AAP file access established the measured behavioral difference. Early failed diagnostic logs that included synthetic PDF base64 are excluded from the review payload; final evidence records byte counts and hashes only.

Valid-length derive-only identities failed native launch with Win32 2 under both LPAC and standard AppContainer. Creating and deleting a unique synthetic profile before LPAC launch also failed with Win32 2. No profile-free launch is claimed. The managed alternative passed all required native functions while denying profile file and registry writes. Disposable experiment profiles were removed with native `DeleteAppContainerProfile`, followed by filesystem and registry absence checks. V1 resources were not repurposed or ACL-repaired.

### Adopted model and lifecycle

**Preference 3: explicitly managed read-only profile with standard AppContainer.** LPAC was not adopted because the complete required native compatibility matrix did not pass. Standard AAP ambient read remains a measured surface for external review; zero explicit TokenCapabilities does not mean zero ambient AAP access. This is a deliberate, disclosed selection rather than a silent downgrade.

Production uses a distinct identity, `Axora.Studio.Renderer.Typst.ReadOnly.V2`. Before first creation, it derives the SID and checks absence of the exact package directory and both profile registry roots. A partial/unregistered collision fails closed. Existing resources follow validation only; they never enter hardening. A successful new profile creation is checked against the derived SID. Windows' just-created renderer-owned package root, AC directory and four empty AC subdirectories receive protected owner/SYSTEM full access and renderer read/execute access. The four renderer registry keys (Mappings SID and its Children; Storage moniker and its Children) receive protected owner/SYSTEM full access and renderer KEY_READ.

Only those freshly created profile resources may have DACLs set. No ownership change, added privilege, inherited user/repository/Windows-directory ACL change or repair of existing resources occurs. Registry keys are opened with `REG_OPTION_OPEN_LINK`; unexpected link values/layout are rejected, so hardening does not follow a registry link to another key. Directory ancestry and the complete declared profile tree are pinned against namespace replacement for each sandbox lifetime. Exact layout, readable owner/group/DACL, registry mapping metadata and absence of file payloads are mandatory. Unsupported OS layout or a profile collision produces a provisioning/isolation failure, without automatic sweeping or repair.

The profile persists as **six empty directories and four read-only registry keys**, with only known mapping metadata. Its byte/semantic state is checked before creation of the child, during supervision and after tree/reader settlement. A boundary failure discards output and reports unsettled security state, causing coordinator admission to close. Per-operation profile deletion is unnecessary and is not used. The trusted host owner retains management rights; this is a renderer boundary, not isolation from a hostile process already running with the host owner's unrestricted authority.

The external fresh-current-production proof copies the final renderer code and changes only the profile-name literal to a unique synthetic name. It exercises the absence/create/harden/reuse branch with authenticated version/ASCII/Unicode and verifies complete native profile cleanup afterward. Its source identity inventory makes that sole variation reviewable.

### Production-path acceptance and retained security coverage

The final renderer matrix has **53 required cases / 157 passing assertions**, complete=true and failed/missing/duplicate/unknown/blocked=0. Existing 107 assertions remain; fifty additional assertions cover the external finding and artifact label.

Real authenticated Typst profile filesystem and relevant registry state are captured before/after version, normal compile, Unicode probe, offline clean-cache compile, cancellation, timeout and shutdown. Every snapshot is byte/semantically unchanged, including owner/group/DACL. Cancellation/shutdown tests cancel an authenticated, assigned, suspended Typst process; this is not a claim that an arbitrarily long active document compilation was interrupted. Timeout uses an actual resumed Typst process. PDF output remains unvalidated fixture output, and Unicode remains a process probe rather than a Hindi-layout verdict.

Native probes deny direct writes in **all six** package/profile directories and deny a writable native registry-location request. Separate probes deny filesystem/registry WRITE_DAC, WRITE_OWNER, DELETE and write access individually; owner rights cannot be used by the renderer to reopen the profile for control changes. Filesystem and registry collision cases fail admission without repair, then remove only their synthetic collision. AAP-only ambient read is positively recorded and its sentinel removed; it is not mislabeled as denied under the selected standard model.

All previous runtime substitution/reparse, workspace substitution/reparse, fixture tamper, Job/pre-resume failure, saturation, timeout/cancel, descendant pipe, partial sandbox, delete-pending, shutdown/race, offline/private/network/owned-write denial and ACL non-mutation cases are retained. Standard-model network denial still requires completed **WSAEACCES 10013**, not a timeout or LPAC provider initialization failure.

`RendererResult.ArtifactStatus` now says `UNVALIDATED FIXTURE PDF` only for Completed with positive PDF bytes and a nonblank hash. Failed, zero-byte or missing/blank-hash results say `NO FIXTURE PDF ARTIFACT`; discriminating assertions cover these branches.

### B1R validation and freeze

Final fresh Debug/x64 Studio and Studio.Tests rebuilds each exited 0 with **0 errors**. Warning counts remain seven existing MVVMTK0045 warnings for Studio and eight for Tests including existing CS0436. No protected warning source was changed. Final evidence is under `%TEMP%\AXORA-M2B1R-IMPLEMENTATION`; builds are `studio-final-build.log` and `tests-final-build.log`, standalone native acceptance is `renderer-final.log`, and full runner/dispatch results are `final-regression-m1.log`, `final-regression-m2.log`, `dispatch-probes.json` and `final-run-exits.json`.

Final full regression, hostile reread, Git freeze and payload hashes are recorded below after completion. Until that record is appended, this section does not claim the post-remediation checkpoint is ready.

Primary API references used for the native model: [AppContainer/LPAC launch](https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer), [official folder path](https://learn.microsoft.com/en-us/windows/win32/api/userenv/nf-userenv-getappcontainerfolderpath), [registry location](https://learn.microsoft.com/en-us/windows/win32/api/userenv/nf-userenv-getappcontainerregistrylocation), [registry link opening](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regopenkeyexw), [registry security capture](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-reggetkeysecurity).

### Completed B1R validation record

M1 runner exit 0; M2 runner exit 0. The final M2 run executed each of the six groups once:

| Group | Passing assertions | Complete | Failed / missing / duplicate / unknown / blocked |
| --- | ---: | --- | --- |
| H0 | 207 | true | 0 / 0 / 0 / 0 / 0 |
| Flashcards | 172 | true | 0 / 0 / 0 / 0 / 0 |
| Export | 289 | true | 0 / 0 / 0 / 0 / 0 |
| ReadAloud | 156 | true | 0 / 0 / 0 / 0 / 0 |
| Resume | 484 | true | 0 / 0 / 0 / 0 / 0 |
| Renderer | 157 | true | 0 / 0 / 0 / 0 / 0 |
| Total | **1465** | six passing groups | all historical floors preserved |

Standalone native root: `%TEMP%\AXORA-M2B1-NATIVE-1ed84c55011e4ad6b44d82ec3c242dd7`. Full regression native root: `%TEMP%\AXORA-M2B1-NATIVE-7f576ed62ecd4009969be487bd924ab4`. Both contain `profile-state.json`, `profile-shutdown.json`, `acl-nonmutation.json` and `aap-ambient.json`. The six malformed-dispatch probes (unknown/empty/wrong-case group and extra arguments for H0, renderer and manifest) each exited 2, emitted only the rejection text, and ran no test ledger.

The fresh-production proofs exited 0, including a direct native write attempt against the old V1 profile by the new renderer identity: **ERROR_ACCESS_DENIED 5**. Both disposable fresh identities were natively removed and their filesystem/registry absence verified. All three managed Phase-1 experiment profiles were also removed, with cleanup recorded in `synthetic-profile-cleanup.json`. The fixed production V2 profile remains intentionally read-only; no automatic deletion or repair of V1 occurred.

The hostile reread covered profile filesystem/registry shape, write/control rights, initialization and collision behavior, registry link handling, standard AAP surface, rejected LPAC construction/compatibility, runtime identity, native Job/token ownership, operation cleanup and shutdown. **Bounded self-review: BLOCKER 0 / HIGH 0 / MEDIUM 0.** Registry link protection was reviewed statically against the native open-link API and validated on ordinary native opens; no privileged registry-link creation is claimed. This is readiness for external post-remediation review, not independent acceptance or a universal Windows-security verdict.

Remaining LOW/deferred work: installer provisioning/update/uninstall and V1 retirement; strict Windows profile-layout portability (unsupported layouts fail closed); existing quarantine/restart recovery after unresolved cleanup; fixed lab toolchain paths. Standard AAP ambient read is explicitly retained/classified under the selected model and remains visible to the external reviewer. There is still no PDF-validation, Hindi-layout, installer-completion, representative-device performance or UI-integration claim.

Final freeze remains exactly **20 existing allowlisted authored paths: 14 new / 6 modified**, index empty and all work unstaged. B1R changes only the existing Sandbox, Process, RendererModels, RendererTests, renderer runner and this evidence document. Final Git/source inventories and security/non-mutation checks are supplied in the regenerated pack; main, HEAD, origin and independently queried remote main remain at the required baseline, ahead/behind 0/0. No stage, commit, push or M2-B2.

New deliverables: `%TEMP%\AXORA-M2B1-POST-REMEDIATION-AUDIT.zip` and `%TEMP%\AXORA-M2B1-CHATGPT-REVIEW-POST-REMEDIATION.txt`. The previous review pack and chronology remain preserved. The new pack includes current bounded source/diff, exact six baseline copies, original draft evidence, X1 RED and rejected alternatives, final profile/AAP/native evidence, both fresh builds, all regression ledgers, privacy/non-mutation records and final hashes.

**STUDIO-M2-B1R PASS — READY FOR EXTERNAL POST-REMEDIATION REVIEW.** Stop here; independent post-remediation acceptance remains pending.

