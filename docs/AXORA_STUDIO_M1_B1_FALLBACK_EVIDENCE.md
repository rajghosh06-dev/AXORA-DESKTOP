# AXORA Studio M1-B1 — Scholar availability and manual fallback

## Authority and baseline

Authority: user-supplied B1/B2 P0 clarification and the accepted B1 implementation contract (`2957b269-98e8-4176-ae1b-335bc4bf54ed/pasted-text-1.txt`). B1 is truthful availability/manual fallback guidance; B2 is a bounded process-local bridge from a real producer. These names are user-supplied clarification, not verbatim repository definitions.

Before editing, live main/HEAD/origin/main and independently queried actual remote main were `75809b300837cab33ed21da8654e98cfad4a0ac9`, subject `feat(studio): add local flashcard read aloud`, ahead/behind 0/0. Tracked worktree was clean and index empty. `Axora-Desktop-WinUI.zip` was the sole unrelated untracked file, observed by Git filename only; never opened, hashed, extracted, modified, deleted or staged.

## Product problem and dependency evidence

Home already states that Scholar integration is absent, but the Flashcards page needs that information beside its existing manual notes workflow. Users must not infer that a deck generated in legacy AXORA Desktop appears in the separate Studio process.

Current Scholar classification: **LEGACY ONLY**. Studio's solution contains Studio and Studio.Tests; `StudioBootstrap.cs` registers no Scholar producer, extraction, retrieval, index or document-session source. `HomePage.xaml` explicitly excludes Scholar integration. The actual legacy `ScholarKitViewModel.PushToFlashcards` synchronously passes `OcrResultText`/`ImportedFileName` to a globally resolved legacy Flashcards VM and navigates the legacy shell. It does not transfer decks to Studio.

Repository planning: `docs/AXORA_SUITE_P2_EXECUTION_CONTRACT.md` section 8 requires a consented bounded handoff or clearly marked temporary fallback and preserves the legacy route until parity. **B1 does not satisfy the eventual Scholar bridge requirement.** It is not full M1 parity, cutover, legacy retirement or release acceptance. Legacy fallback-route/button wording repair remains a separate future gate; this slice does not alter legacy code.

**B2 = DEFERRED** because no Studio-local producer exists. Any future B2 requires a separately accepted real producer contract. No provider, bridge, DTO, IPC, source capture, OCR, index, model, cloud AI or STT work is introduced.

## Exact authored inventory and implementation

- MODIFIED: `Axora-Desktop-WinUI/Axora.Studio/Views/FlashcardsPage.xaml`
- NEW: `docs/AXORA_STUDIO_M1_B1_FALLBACK_EVIDENCE.md`

Exactly two authored paths. One passive TextBlock is a direct sibling immediately before the existing structured-notes Expander, outside its collapsed content. Three Runs with two LineBreaks preserve the frozen copy. TextWrapping=Wrap, default body typography and existing TextFillColorSecondaryBrush make the notice compact, neutral and theme-dependent. No fixed height/width, error styling, action, binding or code-behind is added.

Accessible identity: **Scholar availability and manual alternative**, on the single TextBlock; inline Runs have no duplicate accessible names. No live-region announcement is requested for this static information.

Frozen copy:

> Scholar integration is not available in this Studio build.
>
> Paste notes under “Create cards from structured notes” to create rule-based cards for this session.
>
> Legacy Scholar’s “Push to Flashcards” creates decks in AXORA Desktop. Those decks are not transferred to Studio.

All existing study, notes, generation, export, read-aloud, status and keyboard controls remain in their original order with their original attributes and bindings. No ViewModel, service, DI, lifecycle or storage change is authorized.

## Verification chronology

### Build and deterministic verification (2026-10-05 UTC)

Evaluated NETCoreSdkVersion: **9.0.318**. Direct MSBuild Debug/x64 restore/rebuild of Studio exited 0: **0 errors, 8 existing MVVMTK0045 warnings**, elapsed 67.98 seconds (`Axora.Studio.Tests/logs/b1-studio-build.log`). The warnings concern `_sourceLabel`, `_notesText`, `_status`, `selectedTheme`, `canEdit`, `status`, `canSave`, and `selectedRoute`; none was suppressed. Direct Tests rebuild against that final Studio output (`BuildProjectReferences=false`) exited 0: **0 errors, 0 warnings**, elapsed 29.28 seconds (`b1-tests-build.log`). No package, project or toolchain file changed.

App/test output Studio DLL SHA-256 matched: `1A4EC923E0D2C21C9AF4EF25249CA93B859760DB7DC26DA627AF588A76EA2EAC`. The actual-page run below used this rebuilt Studio output.

Chronology is preserved: the first restricted-shell run failed H0 native settings publication in sandbox temporary fixtures (115 passed assertions, 18 failed; complete ledger, zero missing/duplicate/unknown/blocked). A normal-permission Windows PowerShell 5 wrapper invocation subsequently produced a passing H0 ledger but returned 1 before continuing. These are not accepted regression passes. No test or production source was changed in response.

The authoritative final run used the unchanged `run-m1.ps1` under **PowerShell 7.6.5**, normal Windows permissions, and exited **0**, running all four groups exactly once. Logs: `Axora.Studio.Tests/logs/m1-20261005T174344065-<GROUP>.stdout.log` and corresponding empty stderr logs.

| Group | Mandatory cases | Passed assertions | Failed / missing / duplicate / unknown / blocked |
|---|---:|---:|---|
| STUDIO-H0 | 21 | 160 | 0 / 0 / 0 / 0 / 0 |
| STUDIO-M1-FLASHCARDS | 22 | 172 | 0 / 0 / 0 / 0 / 0 |
| STUDIO-M1-EXPORT | 73 | 289 | 0 / 0 / 0 / 0 / 0 |
| STUDIO-M1-READALOUD | 42 | 156 | 0 / 0 / 0 / 0 / 0 |

Every final ledger reports Pass, complete=true, one selected/executed group and totalRegisteredGroups=4. Counts describe this run, not acceptance constants. Unknown group `--group=UNKNOWN` and malformed `--invalid` both exited **2**, reporting no tests ran.

DETERMINISTICALLY VERIFIED: parsing final and baseline XAML, removing only the added notice, and comparing XML produced **ExistingXmlEquivalent=True**. The notice's next sibling is the structured-notes Expander. The six-line addition contains only TextBlock/Run/LineBreak, static strings, wrapping, one accessible name and one existing theme resource. No existing binding, event, command, control hierarchy or feature behavior changed. `git diff --check` passed.

### Bounded actual-page observations

OBSERVED through scripted Windows UI Automation, with no interactive Computer Use: final `Axora.Studio.exe`, isolated fixture `Axora.Studio.Tests/logs/b1-runtime-system`, PID **438052**, HWND **6425276**. APPDATA and LOCALAPPDATA were test-owned. The existing runtime-probe retained the original Process handle. No native speech or Save As journey was invoked.

- Navigation to Flashcards rendered the final notice. One ControlType.Text exposes **Scholar availability and manual alternative**; its TextPattern returns the complete exact copy, not only its accessible name.
- At **1400 x 900 physical pixels**, System theme, the Expander was **Collapsed** and the notice was onscreen: bounds `221,869,1050,84`; three visible text-line rectangles. At **900 x 900**, it remained onscreen and Collapsed: bounds `147,812,789,140`; five visible text-line rectangles. All reported line rectangles fit within the notice/window bounds. This establishes actual line wrapping and text exposure using the accessibility layout surface, not a screenshot-based visual opinion. Scrolling to the lower-page notice was used; it is not claimed visible without scrolling from the page top.
- UIA Expand opened the existing notes workflow. Setting the generic test-owned note `Gravity: A force that attracts objects with mass.`, moving focus to commit the existing input binding, and invoking the existing command produced **Created 1 rule-based card(s); ignored 0 line(s). Session only.** The generated current-card accessible text included **Question. Gravity. Press Space or Enter to flip.** No B1 handler or generation path was involved.
- Button inventory contains **Read aloud**, **Stop reading**, and all three existing export controls. Scholar action/button count is **0**. Collapsed content remains operable; notice does not depend on opening it.
- Existing Settings UI applied **Light** in the isolated fixture; runtime diagnostics confirm `Theme applied: Light; requested=Light`. Returning to Flashcards recreated a collapsed Expander and exposed the same exact notice with three text lines. System and Light were observed through runtime state/UIA. No real user settings were changed.
- Normal CloseMainWindow was requested. The original runtime-probe confirmed **exit 0**, unchanged legacy-settings canary, and ordered owned-work stop, host stop/disposal, window close and Program fallback settlement. No forced termination; the owned PID was absent afterward.

QA observations are retained in ignored logs: `b1-wide-system.json`, `b1-narrow-system.json`, `b1-wide-light.json`, `b1-manual-journey.json`, `b1-button-inventory.json`, and the fixture's `roaming/Axora/Studio/startup.438052.log`. These are bounded UI observations, not production content logging.

### Limitations and Phase-1 gate

NOT-RUN: reliable app-only pixel screenshot/contrast inspection. The screen-copy attempt did not capture the owned Studio surface; invalid captures were discarded and are not acceptance evidence. UIA text and line geometry establish wrapping/accessibility exposure, but do not establish pixel color contrast. Dark, high contrast, screen-reader narration, DPI/multi-monitor and full accessibility certification were not verified. No broad native export/speech rerun or full M1 parity is claimed.

LOW/unverified debt: pixel contrast and the optional visual observations above; inherited MVVMTK0045 warnings; restricted-shell publication and Windows PowerShell 5 wrapper limitations observed in this verification chronology. No B1 source defect was established by those environment-specific runs; the authoritative final normal-permission PowerShell 7 run passes unchanged tests.

Harsh self-audit: collapsed visibility and exact text were observed; absence of integration/transfer claims was source-reviewed; manual generation succeeded; no new dependency/side effect is introduced; A2/A3 code and bindings are unchanged; session-only wording persists; Git inventory contains only the two authorized B1 authored paths. Phase-1 severity: **BLOCKER 0, HIGH 0, MEDIUM 0**.

**STUDIO-M1-B1 IMPLEMENTATION PASS.** Next phase is the required read-only hostile audit. This implementation gate is not that audit's verdict and grants no staging/commit/push authority. Final audit findings and freeze hashes belong in the task report; this document will not be edited during Phase 2.

## Privacy, exclusions and Git authority

The added product markup is static text and theme-resource lookup only: zero content capture, logging, network, file operations, task, thread, cancellation source, service or DI registration. Existing host/feature behavior is not attributed to B1.

No staging, commit, push, B2 or STT is authorized. Everything remains unstaged. Build products, regression logs and isolated runtime fixtures are verification artifacts, not additional authored product/test paths.

At the Phase-1 gate, HEAD and origin/main remain the published A3 SHA, ahead/behind 0/0 and index empty. Git status is one modified XAML, the new untracked B1 evidence document, and the unrelated untracked ZIP. No third authored path exists. All other tracked paths, including legacy, MaterialUI, services/models, test source, A1/A2/A3 evidence, Home, DI and lifecycle code, are unchanged.

## Final hostile audit and checkpoint readiness

This section closes the already-completed Phase-2 hostile audit in the chronology. That audit was entirely read-only: neither authored B1 path was edited during it. The implementation gate, restricted-shell failures, PowerShell 5 wrapper observation and NOT-RUN visual limitations above remain historical and unchanged. No build, regression, UI/theme/manual-notes, native speech, Save As or Computer Use rerun was performed for this evidence-only closeout.

### Accepted audit findings

- Exact inventory: one modified `Axora-Desktop-WinUI/Axora.Studio/Views/FlashcardsPage.xaml` and one new `docs/AXORA_STUDIO_M1_B1_FALLBACK_EVIDENCE.md`; no third authored path. The index remained empty, and the unrelated ZIP remained untouched and untracked. Ignored logs/build outputs/runtime fixtures were not authored feature changes.
- Structure: the notice is the structured-notes Expander's immediate preceding sibling, outside its collapsed content, with no visibility dependency on opening the Expander.
- Wording: every sentence establishes unavailable Studio Scholar integration, the existing manual notes alternative, rule-based generation, session-only cards, legacy Push targeting AXORA Desktop, and legacy decks not being transferred into Studio. No B2 capability or automatic transfer is claimed.
- Accessibility: one TextBlock identity named **Scholar availability and manual alternative**. Actual UIA exposed the complete text while the Expander was collapsed. No duplicate inline accessible names or live-region announcement was added. This is bounded accessibility evidence, not certification.
- Layout: actual UIA line geometry established wrapping at 1400x900 and 900x900, with the notice onscreen after scrolling and the Expander collapsed. There are no fixed notice dimensions or hard-coded foreground/background colors; the existing theme resource is used. Pixel-level screenshot/contrast inspection remains **NOT-RUN**.
- Zero behavior: the added markup introduces no event, command, binding, provider, service, task/thread, network access, file operation, logger, cancellation source or storage. Only passive text, line breaks, wrapping, one accessibility identity and an existing theme resource were added.
- Existing-feature integrity: removing only the notice from final XML yielded baseline-equivalent existing XML. Structured notes, study, export and read-aloud controls, bindings, events and hierarchy remained intact. The accepted actual-page manual-generation journey and unchanged four-group regression results remain the verification evidence; they were not repeated for this append.
- Dependency boundary: **Scholar = LEGACY ONLY; B2 = DEFERRED** because Studio has no real Studio-local producer. A future B2 requires a separately accepted real producer contract. No placeholder/provider/bridge path was introduced.
- Protected areas: ViewModel, page code-behind, Home, MainWindow, App, Program, bootstrap/DI, services/models, all test source, A1/A2/A3 evidence, legacy, MaterialUI, W5, Tools/Mind and project/package/manifest/toolchain files were unchanged.

### Accepted LOW and unverified debt

Reliable pixel screenshot/contrast inspection was unavailable; invalid captures were discarded. Dark and high contrast were not observed. Screen-reader narration, DPI/multi-monitor behavior and full accessibility certification were not verified. The eight existing MVVMTK0045 warnings remain unsuppressed. Restricted-shell publication failures and the PowerShell 5 wrapper observation remain historical environment-specific verification limitations, not established B1 source defects; the accepted final unchanged PowerShell 7 regression run passed.

### Final audit verdict and user acceptance

Final hostile-audit severity: **BLOCKER 0; HIGH 0; MEDIUM 0**.

**STUDIO-M1-B1 AUDIT PASS — READY FOR USER ACCEPTANCE AND COMMIT AUTHORIZATION**.

The user accepted the exact audited two-path B1 feature and its disclosed LOW/unverified debt for exactly one local checkpoint, under the evidence-closeout/local-checkpoint contract (`83213857-72c0-4a7d-b2ab-b53b475491ca/pasted-text-1.txt`). No commit or push had occurred when this closeout was written. This new authority permits exact two-path staging and one local commit only after the closeout/refreeze gate passes; it does not authorize publication.

The accepted XAML SHA-256 remains `AE9F0D4CB7752FA0A6960F251FD7334B23446FE378025E6195997B1387AE4880`. The evidence SHA-256 legitimately changes because of this append; its final freeze value is reported outside this document to avoid a self-referential hash.

B1 does not complete M1 parity or the eventual Scholar bridge requirement. No legacy retirement, legacy button repair, deck transfer, cross-process handoff, B2 or STT implementation is claimed or authorized by this checkpoint.
