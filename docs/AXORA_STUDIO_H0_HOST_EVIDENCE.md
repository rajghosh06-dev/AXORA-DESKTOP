# AXORA STUDIO-H0-I1 host implementation evidence

Verified 2026-10-01. Scope: minimal independent Studio host only; no feature migration, staging, commit, push, or STUDIO-M1.

## A. Baseline and authority

The accepted STUDIO-H0-P0 response and the user's STUDIO-H0-I1 implementation authorization govern this work. The older SUITE-P0 attachment is historical, not permission to restart portfolio restructuring. The referenced planning conversation was consulted for sequencing, not treated as an independent grant of write authority.

Repository: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`; branch `main`. HEAD and local `origin/main` both remained `7a880f4432a80c87ae73fbac1d9d44d7e345f46d`. Frozen `archive/legacy-monolith` remained `a518f04ba7188a93ec7d7334879c194699db59b9`. Origin remained `https://github.com/rajghosh06-dev/AXORA-DESKTOP.git`. No fetch/push was needed or performed; this is local-ref verification, not a fresh remote-server audit.

At entry the tracked worktree/index were clean and the known ZIP was the only untracked path. The ZIP was not opened, hashed, extracted, modified, deleted, staged, or committed. Existing suite, P3B, R0, W5, toolchain, MaterialUI, and legacy implementation files were not edited.

Architecture inputs preserved: `AXORA_SUITE_PRODUCT_PORTFOLIO.md`, `AXORA_SUITE_P1_DEPENDENCY_MAP.md`, `AXORA_SUITE_P2_EXECUTION_CONTRACT.md`, `AXORA_SUITE_P3A_TEST_BASELINE.md`, `AXORA_SUITE_P3B_LIFECYCLE_EVIDENCE.md`, and `AXORA_STUDIO_R0_VOICE_FLASHCARDS_EVIDENCE.md`.

## B–D. Topology, identity, and properties

`Axora-Desktop-WinUI/Axora.Studio.sln` contains exactly two new projects: `Axora.Studio` and `Axora.Studio.Tests`, with Debug/Release x64 mappings. The legacy solution is unchanged. Studio has no ProjectReference; Tests references only Studio. No Shared, Tools, or Mind project was created.

Production identity is `Axora.Studio.exe`, assembly/root namespace `Axora.Studio`, title exactly `AXORA Studio`, and role `Study • Create • Analyze • Review`. About obtains the informational version from actual assembly metadata; the observed build was `1.0.0+7a880f4432a80c87ae73fbac1d9d44d7e345f46d`. That inherited Git hash identifies the starting checkpoint, not a committed H0 implementation or release tag.

App properties: WinExe; `net9.0-windows10.0.26100.0`; minimum `10.0.17763.0`; x64; `win-x64`; UseWinUI=true; WindowsPackageType=None; EnableMsixTooling=false; WindowsAppSDKSelfContained=true; Nullable/ImplicitUsings enabled; owned `app.manifest`; explicit STA Program.Main with `DISABLE_XAML_GENERATED_MAIN`. Required generated-XAML compilation remains enabled. No unsafe switch, kill target, packaging identity, installer, shell registration, or feature diagnostics were added.

The owned manifest uses asInvoker/uiAccess=false, Windows compatibility declaration, PerMonitorV2 DPI awareness, and long-path awareness. This does not prove operation on every declared Windows version.

The only copied asset is `Axora.Desktop/Assets/AppIcon.ico` → `Axora.Studio/Assets/AppIcon.ico`, a physical independent copy. Both SHA256 values were `DC2BD2902A9DD0CC57D82FFCBF6152B9EC79ACFD02C0E24CEB744D09599AFFA1`. Runtime icon lookup uses Studio's own output, not the legacy path.

## E–F. Dependency and output audit

| Direct application package | Pinned version |
| --- | --- |
| Microsoft.WindowsAppSDK | 1.6.250228001 |
| Microsoft.Windows.SDK.BuildTools | 10.0.26100.1742 |
| Microsoft.Extensions.Hosting | 9.0.0 |
| CommunityToolkit.Mvvm | 8.4.0 |

Tests directly references only the matching WindowsAppSDK and BuildTools in addition to its Studio ProjectReference. No third-party test framework was added. No package expansion or upgrade occurred. Hosting supplies DI and Debug logging transitively.

The restored app `obj/project.assets.json` contained 32 libraries. Besides the four direct packages, these are Microsoft.Extensions Configuration/Abstractions/Binder/CommandLine/EnvironmentVariables/FileExtensions/Json/UserSecrets, DependencyInjection/Abstractions, Diagnostics/Abstractions, FileProviders/Abstractions/Physical, FileSystemGlobbing, Hosting.Abstractions, Logging/Abstractions/Configuration/Console/Debug/EventLog/EventSource, Options/ConfigurationExtensions, Primitives (all 9.0.0), System.Diagnostics.EventLog 9.0.0, and Microsoft.Web.WebView2 1.0.2651.64. Console/EventLog/configuration assemblies being transitively present does not mean their providers or ambient configuration are enabled: the composition uses HostBuilder, clears logging providers, and adds only Debug logging.

Resolved package graph, app `.deps.json`, and recursive output filenames were separately checked: zero prohibited matches for ONNX DirectML, Argon2, PdfSharpCore, QRCoder, SkiaSharp/Views.WinUI, ImageMagick, tokenizer/vocabulary/model files, or Axora.Desktop. Source inspection confirms no extension executable or legacy feature asset copy.

Observed Debug app output: 294 files, 97,341,356 bytes. This is a measured raw Debug build folder, not installer size or a release footprint promise. It contains the Windows App SDK native XAML/runtime/PRI/localization payload and WebView2 loader inherited from WindowsAppSDK. No WebView or browser capability is registered by H0. WindowsAppSDKSelfContained is **not .NET self-contained**: runtimeconfig requests Microsoft.NETCore.App 9.0.0 and no coreclr.dll is included.

Final runtime-tested app fingerprints, unchanged after the last test-only build:

```text
Axora.Studio.dll SHA256 8556094596AFF2A7FEB00A58017997FA2A41C89FECDAC36E7F109655C5BF2FA9
Axora.Studio.exe SHA256 D002304EE918FC65CC18911E7D1E5C9357F94B828CFE83B739F909157B2B1572
```

## G–H. Startup and shutdown ownership

Program owns entry diagnostics, COM wrappers, Application.Start, dispatcher synchronization context, App creation, and the 15-second fatal fallback. App owns its host and external operational coordinator. The production DI graph validates scopes/on-build and registers only paths, writer lease, settings publisher/service, and shell/settings ViewModels plus the inert WinUI host lifetime. Lifecycle is deliberately outside its own DI disposal graph. There is no static locator, cross-app singleton, hosted feature, network listener, model, microphone, tray, or broker.

Operational startup: IHost.StartAsync → resolve the injected absolute Studio path service → create root → acquire live lease → load settings → construct shell/window → apply theme → verify native handle/cancellation → activate. Absolute path calculation itself is side-effect-free configuration performed when the path service is constructed for entry diagnostics; filesystem acquisition/settings/UI follow host start. Mandatory errors propagate to fail-closed cleanup and nonzero exit; activation is not performed after failed initialization or a close-winning startup race.

ShutdownAsync publishes one cached Task before teardown. It closes startup admission, cancels/settles startup, closes settings-operation admission and joins owned operations, awaits IHost.StopAsync, and disposes IHost once. No separate ServiceProvider disposal or ViewModel disposal of injected services exists. The first AppWindow.Closing is canceled; dispatcher remains alive while teardown settles; subsequent requests join/retain the same pending close and eventual normal close proceeds. Normal runtime logs establish owned work stopped → host stop settled → host disposed → shutdown completed → window closed → Program fallback settled.

Named asynchronous shutdown budget: 10 seconds. Timeout/failure is not clean success; unsettled owned work retains its host and faults shutdown. This is an engineering asynchronous deadline, not a universal ability to interrupt synchronous filesystem/Dispose calls. Deterministic tests prove a real DI-created probe disposes exactly once, ordered stop precedes disposal, repeated task identity is stable, and stop/log failure cannot silently become success. No forced process termination was used.

## I–K. Data, writer lease, settings

One injected StudioPathService resolves an absolute APPDATA base (or ApplicationData equivalent) and owns only its `Axora/Studio` child. Settings, backup, lease, and diagnostics derive from it. No CWD/executable/legacy-settings fallback exists. Startup does not write settings defaults.

The writer lease is FileStream OpenOrCreate/ReadWrite/FileShare.None; ownership is the live exclusive handle, not `.writer.lock` existence. It is DI-owned and releases at final host disposal. Per-process `startup.<pid>.log` files prevent the rejected second process from appending to the first process's mutable log. Settings remain the single-writer store; diagnostic files are distinct process-owned best-effort records. Appending stops when the pre-write length reaches 512 KiB; the threshold can be exceeded by one record (wording corrected by R1; implementation unchanged).

Minimal settings schema is `{ "schemaVersion": 1, "theme": "System" }`, with exactly System/Light/Dark spellings. Numeric enum strings are rejected. Read size is bounded to 64 KiB. Missing file uses defaults without publication. Malformed primary is preserved byte-for-byte and a validated backup/default is selected with warning. Unsupported future schema is read-only, never downgraded. An oversized file whose schema cannot be safely established is likewise preserved read-only with a size-specific warning.

Explicit Save captures the draft, writes a CreateNew unpredictable sibling stage with WriteThrough, flushes/closes, validates the staged record, then commits. First creation uses non-overwriting File.Move; replacement uses File.Replace with a recoverable backup. There is no delete-destination fallback. A malformed primary receives a uniquely named corrupt-recovery file rather than overwriting a valid `.bak`. Failed publication retains previous bytes; exact owned stage cleanup is attempted, with cleanup failure logged rather than a broad temp sweep. Cancellation after the commit point reports the published truth, not a false canceled save.

## L–M. Theme and real navigation

Native WinUI XamlControlsResources/ThemeResources supply colors, accent, typography, card resources, and contrast-aware defaults. System maps to ElementTheme.Default; Light/Dark map explicitly. No accent editor, legacy import, live suite synchronization, or feature settings exist. Physical high-contrast and multi-monitor/scaling matrix validation was not performed; only the present machine's viewport and actual System/dark appearance → saved Light/restarted Light were visually inspected. All three themes have deterministic persistence coverage.

One typed immutable route catalog drives the three visible native NavigationView items and real HomePage/SettingsPage/AboutPage resolution. Native Home/Setting/Help icons make compact mode readable. Invalid Navigate and direct SelectedRoute assignments throw without changing the active route. Home states that features have not migrated; Settings has the real theme selector, awaited Save, progress, and truthful status; About shows actual identity/version and H0-only status. No fake feature tiles, unsupported shortcuts, palette, intake, or migrated feature placeholder exists.

Draft selection does not publish or apply before Save. Editing is disabled while Save runs. A post-commit theme-render callback failure truthfully reports 'saved, but applying failed', rather than claiming publication failed.

## N–O. Deterministic test ledger

One registered/executed semantic group: STUDIO-H0. Fifteen named required cases cover identity/composition, mandatory startup failure, close-during-startup, cached shutdown/DI ownership, timeout, root/defaults/legacy canary, three-theme roundtrip, corruption recovery, future/oversized schema, staged interruption, replacement failure, cancellation, lease, routes, and Settings ViewModel/save/admission.

Tests use production components and narrow publication/host seams, including a real Windows File.Replace failure while destination sharing denies deletion, actual DI ownership, actual SaveCommand, and byte comparison of synthetic legacy settings. They do not instantiate a fake UI and call that runtime navigation. The independent native runtime evidence below covers actual pages.

Final run `Axora.Studio.Tests/run-h0.ps1`, log `Axora.Studio.Tests/logs/h0-20261001T115334980.stdout.log`:

```json
{"group":"STUDIO-H0","registeredGroups":1,"executedGroups":1,"disposition":"Pass","passedAssertions":72,"failedAssertions":0,"missing":0,"duplicate":0,"unknown":0,"blocked":0,"complete":true}
```

Child and runner exits were 0. Counts are observed, not frozen pass thresholds. Manifest reports exactly STUDIO-H0 once with a 120-second group budget. Unknown group explicitly ran no tests and exited 2. The external runner defaults to 135 seconds; timeout reports Blocked/PID/logs without forced termination. Synthetic test temp roots are retained for inspection rather than broadly deleted.

## P. Build evidence

Direct MSBuild executable: `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`, observed 18.10.1-1.26427.6+3cd27c13e. Root and nested SDK selection both returned 9.0.318. Commands used each owned project path with `-restore -p:Configuration=Debug -p:Platform=x64 -v:minimal -m`; no QA kill helper was run.

| Build | Result | Retained ignored log |
| --- | --- | --- |
| Final Studio Debug/x64 compile | 0 errors; 5 MVVMTK0045 warnings | Axora.Studio/h0-build.log |
| Final Studio.Tests Debug/x64 incremental build | 0 errors; 0 warnings emitted; referenced app unchanged | Axora.Studio.Tests/h0-build.log |
| Authorized legacy Desktop Debug/x64 | 0 errors; 332 existing warnings | Axora.Studio.Tests/legacy-build.log |

The five app warnings concern field-backed ObservableProperty WinRT/AOT compatibility; they were not suppressed. AOT publication is not accepted by this H0 verification. Legacy warnings were not repaired under this authorization. Before the legacy build, no Desktop process was running, so its preexisting kill target had no application to terminate. No full 1,851-assertion legacy suite rerun occurred.

## Q–S. Final Studio runtime, restart, second writer

Visible native QA used the computer-use skill's supported @oai/sky API; process launch/lifecycle diagnostics used the owned runtime-probe.ps1 and normal CloseMainWindow. The probe retains the original Process object, not a late Get-Process attachment, to observe real exit codes. Test APPDATA/LOCALAPPDATA live under `Axora.Studio.Tests/logs/runtime-3302f0596d734a4d9d8e9bef9564c233/{roaming,local}`. Real user settings were not used.

| Final binary run | PID / handle | Actual routes | Close / exit |
| --- | --- | --- | --- |
| Standalone cycle 1 | 23736 / 788668 | Home, Settings, About rendered | CloseMainWindow accepted; exit 0 |
| Standalone cycle 2/restart | 8048 / 396464 | Home, Settings, About rendered | CloseMainWindow accepted; exit 0 |
| Fresh post-release writer/side-by-side | 22520 / 330766 | Home, Settings, About rendered | CloseMainWindow accepted; exit 0 |

All titles were exactly AXORA Studio. In cycle 1 the native Settings UI selected Light; settings.json was confirmed absent before Save. Explicit UI Save created schemaVersion=1/theme=Light, displayed Saved status and visually applied Light. Cycle 2 loaded/applied Light in startup logs and displayed Light in the native ComboBox after restart. Synthetic legacy settings remained byte-identical in each probe.

While cycle 2 remained healthy, second process 11520 targeted the same root. It exited 1 boundedly, handle 0/title empty, with Mandatory startup failed: IOException and no activation record. PID 8048 remained alive/responding with its original window. After 8048 exited, PID 22520 acquired the lease and activated despite the retained `.writer.lock`. This proves both rejection and later recovery through actual processes, not just in-process lease tests.

Retained per-process logs contain ordered startup/shutdown events. Final standalone cycle shutdowns completed normally without a lingering Studio process; the final combined process inventory was zero.

## T–V. Legacy smoke and side-by-side acceptance

Legacy smoke PID 20736, handle 265086, title Axora Desktop: actual dashboard observed, normal CloseMainWindow accepted, exit 0, no lingering Desktop. Its synthetic settings explicitly disabled AutoStartP2pEngine, BackgroundQuickDropListen, voice navigation, and telemetry preference. DownloadDirectory='.' resolves to the test-owned probe working directory, not the user's Downloads. Unopened legacy feature stores are not covered by this smoke.

Separate ordered acceptance launched Desktop first: PID 21292 / handle 265388, then Studio PID 22520 / handle 330766. Both remained alive/responding simultaneously. Studio's actual Home/Settings/About worked. Closing Studio accepted normally and exited 0; Desktop stayed alive/responding. A native click toggled Desktop's navigation button from Close Navigation to Open Navigation after Studio exited, proving retained usability, not just process existence. Desktop then accepted normal close and exited 0. Final process inventory: zero Axora.Studio and zero Axora.Desktop.

Both runtimes used separate test-owned APPDATA fixtures. All Studio legacy-parent canaries and Desktop synthetic settings stayed byte-identical. There is no shared mutable settings writer/path in production Studio. Legacy startup.log (ignored build-output diagnostic) recorded voice stop → P2P stop → tray removal → host stop → final disposal → normal close. No microphone, LAN, transfer, feature store, or destructive functionality was exercised.

## W–Y. File inventory, protected scope, Git

35 new unstaged/untracked implementation/documentation files:

```text
Axora-Desktop-WinUI/Axora.Studio.sln
Axora-Desktop-WinUI/Axora.Studio/
  Axora.Studio.csproj
  Program.cs
  App.xaml
  App.xaml.cs
  StudioBootstrap.cs
  StudioLifecycle.cs
  MainWindow.xaml
  MainWindow.xaml.cs
  app.manifest
  Assets/AppIcon.ico
  Properties/launchSettings.json
  Models/StudioSettings.cs
  Models/StudioRoute.cs
  Services/StudioPathService.cs
  Services/StudioWriterLease.cs
  Services/StudioSettingsService.cs
  Services/StudioDiagnostics.cs
  Services/SettingsFilePublisher.cs
  Services/Contracts/ISettingsFilePublisher.cs
  ViewModels/ShellViewModel.cs
  ViewModels/SettingsViewModel.cs
  Views/HomePage.xaml
  Views/HomePage.xaml.cs
  Views/SettingsPage.xaml
  Views/SettingsPage.xaml.cs
  Views/AboutPage.xaml
  Views/AboutPage.xaml.cs
Axora-Desktop-WinUI/Axora.Studio.Tests/
  Axora.Studio.Tests.csproj
  Program.cs
  HostTests.cs
  run-h0.ps1
  runtime-probe.ps1
  Fixtures/legacy-runtime-settings.json
docs/AXORA_STUDIO_H0_HOST_EVIDENCE.md
```

Ignored generated bin/obj/build logs/runtime fixtures are not staged deliverables. Requested status, diff --name-status, diff --stat, diff --check, diff, and diff --cached were inspected. Tracked diff/index remain empty; ordinary git diff does not display these untracked new files, so their contents and text whitespace were separately reviewed. Every final new deliverable is allowlisted. No legacy/source/test/solution, scripts, props/global.json, MaterialUI, historical planning/evidence, or Git ref was changed. No Git write/publication action was used.

## Z–AB. Limitations, next gate, and unexpected findings

- H0 proves only the independent host. No migration, feature parity, release, installer, external capability, or whole-suite completion is claimed. Independent host audit is next; STUDIO-M1 is not authorized by this result.
- Native high contrast, arbitrary scaling/monitors, release/AOT, unsupported OS versions, power-loss/filesystem durability, and external tampering outside the cooperative lease protocol remain outside demonstrated coverage. File.Replace/flush provide recoverability, not a universal storage guarantee.
- Per-process diagnostics stop appending at a pre-write 512-KiB threshold, which can be exceeded by one record; no historical-log retention policy is added in H0. Interrupted stage cleanup can retain only its owned file; no broad recovery cleaner exists.
- Legacy still carries preexisting dashboard readiness text and legacy dependencies; observing its UI is not verification of its claimed AI/P2P readiness. These excluded areas were not repaired.
- An early build was interrupted before outputs existed. A later compile found generic inference error CS0411 in SaveAsync; explicit SettingsSaveResult type arguments fixed it. Final builds/tests/runtimes above supersede those attempts.
- Native QA initially failed with sandbox/helper setup errors; reset/reinitialization recovered. One later coordinate-geometry failure was recovered by selecting/activating a fresh returned window and refreshing state. Failed input was not recorded as Pass. Native observations can momentarily lag the click; settled snapshots and actual page Loaded logs were checked.
- Exploratory late Get-Process attachment could not return the historical process exit code; that preliminary run is not counted among the two final cycles. The owned probe retained launch Process objects for all accepted runtime exits.
- An agent-created root `Axora.Studio-placeholder.txt` containing only 'temporary' was accidentally added outside the source allowlist. Its exact content/identity was checked and only that owned file was removed; no user/project data was deleted. The transient scope mistake is disclosed, and no stray file remains. Final deliverable scope audit passes.

## AC. Verdict

Final source compiles; STUDIO-H0 executes completely with 72 passed / 0 failed assertions; actual three-route runtime, explicit UI Save/restart, second-writer rejection/reacquisition, legacy build/smoke, and ordered side-by-side acceptance passed. All accepted processes closed normally; no forced kill, staging, commit, push, or STUDIO-M1 occurred.

STUDIO-H0 PASS — READY FOR INDEPENDENT HOST AUDIT

## STUDIO-H0-R1 SETTINGS SAFETY REPAIR

2026-10-01. This section supersedes the earlier settings-safety acceptance and 72-assertion verification for the repaired source, without replacing the historical H0-I1 record. Independent H0-CA blocked checkpoint acceptance on R1-F1 (HIGH: unestablished schemas could be writable) and identified R1-F2 (LOW: the cancellation test label misstated chronology). R1 is a narrow corrective implementation, not M1 or permission to checkpoint.

### Baseline and exact repair scope

Branch `main`, HEAD/local `origin/main` `7a880f4432a80c87ae73fbac1d9d44d7e345f46d`, frozen archive `a518f04ba7188a93ec7d7334879c194699db59b9`. Initial tracked worktree/index empty; 35 H0 deliverables plus the excluded ZIP. Baseline SHA256 fingerprints of all 35 H0 files were captured without touching the ZIP.

Only these existing H0 files are edited by R1:

- `Axora-Desktop-WinUI/Axora.Studio/Services/StudioSettingsService.cs`
- `Axora-Desktop-WinUI/Axora.Studio.Tests/HostTests.cs`
- `docs/AXORA_STUDIO_H0_HOST_EVIDENCE.md`

No publisher, settings model/contract, project, package, shell, lifecycle, lease, route, legacy, MaterialUI, shared/toolchain, or protected historical document is changed. No staging, commit, push, branch/tag/ref mutation, dependency installation, or M1 work.

### Classification and write admission

The private `SettingsReadState` replaces the collapsing Exists/Valid/Future booleans:

| State | Load behavior | Write admission |
| --- | --- | --- |
| Missing | Defaults, no startup publication | Allowed |
| ValidCurrent | Validated v1 theme | Allowed |
| ConfirmedCorrupt | Readable invalid JSON/settings; validated backup or warned defaults; original bytes untouched | Explicit repair allowed, established original retained in a unique corrupt-recovery file |
| UnsupportedSchema | Warned defaults, original read-only | Refused |
| Unreadable | Read/access/sharing failure; truthful unavailable/read-only warning, not a corruption claim | Refused |
| Unclassified | Oversize or ambiguous duplicate schema fields; original read-only | Refused |

The current version identifier must be JSON's integer encoding `1`. Every other numeric version is unsupported, including 0, negatives, 2, beyond-Int32/Int64 numbers, fractional forms, and huge exponents. Non-integer encodings `1.0`/`1e0` are conservatively unsupported, not numerically coerced into the current identifier. Missing/non-numeric version fields in readable content follow confirmed-corrupt policy; duplicate version fields are ambiguous/read-only, not last-value-wins.

Opening the file, rather than a suppressing `File.Exists` precheck, establishes missing versus unreadable. FileNotFound/DirectoryNotFound mean missing; IOException/UnauthorizedAccessException/SecurityException mean unreadable. Successfully read JSON/settings parse/validation failures remain confirmed-corrupt. No ACL mutation was used to test access errors.

Save always obtains a fresh classification before creating a stage, regardless of stale Load/CanSave. It obtains another classification after stage validation immediately before the commit region. Unsafe states refuse publication and report no Saved success; an initially missing destination that appears by the second read is rejected for review/retry. A later direct service retry reclassifies safely rather than permanently trusting the earlier unavailable result.

TOCTOU boundary: the live writer lease serializes cooperating Studio writers; the final recheck narrows exposure to outside changes and rejects observed unsupported/unreadable destinations. This is not a generalized transaction or an atomic compare-and-replace against arbitrary external writers after the final read. That preexisting external-tampering limitation remains explicit.

Publication remains CreateNew/unpredictable sibling stage -> write -> flush/close -> validate -> File.Move(overwrite:false) or File.Replace with recovery. No delete-before-move, truncation, fallback publisher, or broad cleanup was added. Readable corruption still preserves byte-identical originals in recovery and does not overwrite a valid backup with corrupt bytes.

### Regressions and cancellation chronology

Twenty required cases now execute in the single registered STUDIO-H0 group. New production-service checks cover the exact 2147483648 input; other unsupported numbers including 9223372036854775808 and 1e10000; ambiguous schema fields; real FileShare.None obstruction of a v1 and an unsupported file; refusal without staging/corrupt-recovery; unchanged original bytes; safe retry after release; unsupported-schema refusal after release; and destination changes after an absent or valid Load.

Existing corruption/validated-backup, v2, oversize, replacement failure, lease, lifecycle, routes, and ViewModel tests remain. Additional readable malformed/missing/non-numeric-version and invalid-theme cases prove the repair did not make every invalid document unavailable/read-only.

The earlier cancellation-before-publisher case is now `commit-region-cancellation` and labeled accordingly. A distinct `post-commit-cancellation` uses the narrow publisher seam to physically commit with the real SettingsFilePublisher, verify the destination is newly Dark and the stage is gone while the token is not yet canceled, then cancel before returning to Save. Assertions establish published/Saved truth, Current=Dark, production-reader-validated v1/Dark disk content, and no remaining stage. Production cancellation behavior was not weakened.

### Final-source build and test evidence

Direct MSBuild `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe` used each owned csproj with `-restore -p:Configuration=Debug -p:Platform=x64 -v:minimal -m` and normal file logging. Root and nested SDK invocations both selected 9.0.318.

| Verification | Result | Retained ignored evidence |
| --- | --- | --- |
| Studio Debug/x64 | Exit 0, 0 errors, 5 existing MVVMTK0045 warnings | `Axora.Studio/h0-r1-build.log` |
| Studio.Tests Debug/x64 | Exit 0, 0 errors, 0 warnings emitted | `Axora.Studio.Tests/h0-r1-build.log` |
| Complete STUDIO-H0 | Runner/child exit 0; 140 passed assertions; 0 failed/blocked/missing/duplicate/unknown; complete=true | `Axora.Studio.Tests/logs/h0-20261001T124638521.stdout.log` and empty stderr |
| Manifest | Exactly STUDIO-H0 once, group budget 120 seconds | Live final-binary output |
| Unknown group | No tests ran; exit 2 | Live final-binary output |

The historical count was not preserved as an acceptance threshold. No warning suppression or legacy test/build helper was used.

Repaired app SHA256:

```text
Axora.Studio.dll DB0B20AAF0BD66B0DCB0304D75E1F969402BB6D37B19BD72139A0E7A83BBAEC2
Axora.Studio.exe D002304EE918FC65CC18911E7D1E5C9357F94B828CFE83B739F909157B2B1572
```

### Final-source runtime verification

Final-source native checks used the supported computer-use `@oai/sky` API and the unchanged owned runtime-probe. The probe retained its original Process objects and reported actual exit codes; the driver validated the launched PID's executable path before requesting normal CloseMainWindow. No accepted process was force-killed.

| Final repaired binary run | PID / native handle | Observation | Actual exit |
| --- | --- | --- | --- |
| Fresh isolated launch | 24396 / 527916 | AXORA Studio Home/Settings; draft Light left settings absent; real UI Save produced v1/Light, Saved status and Light appearance | 0; normal close accepted |
| Same-root restart | 7796 / 7736728 | AXORA Studio Home/Settings; startup log loaded Light/writable=True; native ComboBox displayed Light and appearance was Light | 0; normal close accepted |
| Unsupported numeric fixture | 26984 / 1182936 | Startup loaded defaults/writable=False; actual Settings displayed unsupported/read-only warning and disabled Save; clicking the disabled control did not publish | 0; normal close accepted |

Fresh/restart fixture: `Axora.Studio.Tests/logs/r1-runtime-20261001-7d83845c/{roaming,local}`. Unsupported fixture: `Axora.Studio.Tests/logs/r1-unsupported-20261001-2495e961/{roaming,local}`. Both APPDATA and LOCALAPPDATA were test-owned. All three probe results reported legacyCanaryUnchanged=true. Per-PID startup logs are retained under each fixture's `roaming/Axora/Studio` and establish settings/theme, real page Loaded, ordered normal shutdown, host disposal, window close, and Program fallback settlement. Initial fresh-root Host started file logging is best-effort before root creation; its absence from that first log is not a claimed startup failure.

Unsupported settings were exactly `{"schemaVersion":2147483648,"theme":"System"}` with the fixture newline. Before launch and after disabled Save/normal close, SHA256 was identical: `33CF443527071E78B849B0B023B7D93DCBF66796550EF7079E113B920A4B3258`. The root contained only settings.json, the stale writer-lock file, and its per-PID diagnostic log: no .bak, staging file, or corrupt-recovery file. Final Studio process inventory was zero. User settings were never used.

Unexpected QA events: initial input returned SendInput/GetLastError 87 and then the user-input safety guard; a later capture showed another foreground app despite Studio accessibility text. UI actions were paused until the user explicitly answered Ready for Studio UI verification. Fresh selection/state and supported screenshot-coordinate input recovered the actual Settings workflow. A later lost JavaScript `sky` binding was recovered by initialization and fresh window selection. No failed input or mismatched capture was counted as Pass.

### Final scope self-audit

All requested status/diff/name-status/stat/check/cached/untracked inspections were performed. Tracked diff/index remain empty. Because H0 is untracked, before/after SHA256 comparison of all 35 deliverables separately established that exactly the three authorized paths changed, with no missing/new deliverable. No ZIP content access occurred. Generated build/test/runtime data are ignored; the unsupported JSON fixture is ignored as well. Placeholder remains absent, protected refs unchanged, and resolved assets contain zero prohibited feature packages. Changed untracked text was checked separately for trailing whitespace; none found.

### Remaining debt and next gate

Deferred unchanged: stopped-dispatcher/Task.Yield fatal fallback; pre-write diagnostics threshold overshoot; historical-log retention; runtime-probe diagnostic-only acceptance semantics; retained test temp roots/timestamp log collisions; Release/AOT; high contrast/DPI/multi-monitor; installer/MSIX; unsupported OS and power-loss guarantees.

Legacy build/full 1,851 suite/side-by-side were not repeated: the repair changes no legacy/shared/toolchain/runtime-shell file, and the accepted H0 legacy observations remain historical, not newly verified.

R1-F1 and R1-F2 are addressed with final-source builds, the complete 140-assertion group, real native Save/restart and unsupported-schema preservation, and exact protected-scope reconciliation. This is implementation/self-verification evidence, not independent re-audit acceptance or commit authorization. Stop for targeted re-audit; do not checkpoint or begin M1.

STUDIO-H0-R1 PASS — READY FOR TARGETED RE-AUDIT

## STUDIO-H0-R1-T2 FINAL-RECHECK DETERMINISTIC COVERAGE

T1 correctly stopped: the existing publisher seam executes after the final destination classification and cannot synchronize the missing during-Save regression. T2 explicitly authorizes the minimal seam, regression and this appendix; the production guard was not shown defective.

### Scope and production inertness

Baseline: main; HEAD/local origin/main `7a880f4432a80c87ae73fbac1d9d44d7e345f46d`; empty index/tracked diff; 35 untracked H0 deliverables plus excluded ZIP. T2 changes only `StudioSettingsService.cs`, `HostTests.cs` and this evidence document. No deliverable/package/feature was added; ZIP content was not accessed.

`BeforeFinalRecheckForTest` is a nullable, instance-scoped, init-only `Func<CancellationToken, Task>` property. It is neither static nor persisted, and has no DI registration. The existing three-argument constructor and normal composition are unchanged. The sole conditional invocation is after successful staged-settings validation and immediately before the final destination `ReadAsync`; the supplied delegate is awaited with the existing operation token. With no delegate, the previous Save path is unchanged. A production-DI assertion proves the resolved property's value is null. Reconstructing each edited source without its T2 additions reproduces its pre-T2 SHA256 exactly: existing production logic and prior test source were preserved byte-for-byte.

### Deterministic chronology and result

The new required case `final-recheck-change` starts from writable v1/System and requests Dark. After first admission and stage write/flush/close/validation, the hook signals a RunContinuationsAsynchronously completion source and awaits a separate release signal. The test proves Save is paused, opens the owned stage with exclusive sharing and validates v1/Dark, then writes and rereads these exact UTF-8 destination bytes, without BOM or newline:

```json
{"schemaVersion":2147483648,"theme":"System"}
```

Before release, SHA256 is `717CF63242C031CDF0F061E2DBDCE267E3CB806B830099A6DB160819DFB42683`. The test releases Save; final classification reports unsupported/read-only, `Published=false`, and no Saved success. Hook hits = 1; independently counted publisher Commit calls = 0. Destination bytes/hash remain identical and schema remains 2147483648. No .bak or corrupt-recovery artifact is created; the exact owned stage is removed, while an unrelated stage and unrelated file retain their bytes. Current remains v1/System, not attempted Dark. An unhooked subsequent Save still refuses; a safe retry succeeds only after the fixture explicitly restores valid current bytes.

Boundary entry, hook release and Save completion have 10-second waits; the operation token has a 30-second cancellation budget. Finally releases the hook and joins Stop with a 10-second bound. Timeout/fault fails the case; no sleeps, watchers or scheduling guesses establish chronology. Existing outer group/runner budgets remain 120/135 seconds.

### Final-source verification

Direct Visual Studio MSBuild, SDK 9.0.318, Debug/x64, restore and normal file logging:

| Check | Result | Retained ignored evidence |
| --- | --- | --- |
| Studio build | Exit 0; 0 errors; 5 existing MVVMTK0045 warnings, unsuppressed | `Axora.Studio/h0-r1-t2-build.log` |
| Studio.Tests build, including Studio reference | Exit 0; 0 errors; 0 warnings emitted | `Axora.Studio.Tests/h0-r1-t2-build.log` |
| Complete STUDIO-H0 | Runner/child exit 0; 159 passed assertions; one registered/executed group; complete=true; 0 failed, blocked, missing, duplicate or unknown | `Axora.Studio.Tests/logs/h0-20261001T134545773.stdout.log`; empty stderr |
| Prior coverage | All 140 prior passing assertion occurrences retained; 19 additions, not a frozen acceptance count | Comparison with retained R1 group log |
| Isolated native sanity | PID 31424, handle 11013348; actual AXORA Studio Home observed; System/writable startup; user normal X close; actual exit 0; final Studio process count 0 | `Axora.Studio.Tests/logs/h0-r1-t2-runtime.log` |

Runtime fixture: `Axora.Studio.Tests/logs/r1-t2-runtime-20261001-b2d73a90/{roaming,local}`; both APPDATA and LOCALAPPDATA were isolated. The probe's original Process object confirmed exit 0 and unchanged legacy canary. Per-PID log records close request, work stop, host stop/disposal, shutdown completion, window close and Program fallback settlement. No settings/defaults were written; only the stale lock and PID log remain in the Studio root. No process was force-killed.

Final DLL SHA256: `BBBDA7CD8DD976C33E1064F058B554591221E5C88AD01D6627DE11E37C7DF1AF`; EXE SHA256: `D002304EE918FC65CC18911E7D1E5C9357F94B828CFE83B739F909157B2B1572`.

Unexpected verification events: the sandbox denied the initial NuGet.Config read; the approved-access build retry succeeded without toolchain changes. Initial screenshots showed another foreground application despite Studio accessibility text; those were excluded, and explicit activation recovered the actual Studio view. Two Close inputs failed with SendInput/GetLastError 87; automation input stopped, the user closed Studio normally, and probe/log evidence independently confirmed shutdown. Failed inputs were not counted as Pass.

No current-schema UI Save/restart replay was needed: neither constructor nor composition changed; production hook nullness and ordinary Save paths passed the complete group. Per T2, legacy full suite, side-by-side and second-writer runtime were not repeated. This is bounded host sanity, not a new exhaustive UI/release audit.

### Self-audit and next gate

Final inventory remains 35 H0 files plus excluded ZIP; generated logs/bin/obj are ignored and Placeholder is absent. Fingerprint comparison isolates exactly the three authorized changed paths; all other H0 files, tracked state and protected refs remain unchanged. No staging, commit, push, feature migration or M1 work occurred.

Remaining LOW debt is unchanged: stopped-dispatcher/fatal fallback; diagnostics retention/threshold; runtime-probe diagnostic-only acceptance semantics; retained test roots/log collisions. Release/AOT, accessibility/DPI, packaging and power-loss guarantees remain outside this verification.

STUDIO-H0-R1-T2 PASS — READY FOR FINAL MICRO-AUDIT
