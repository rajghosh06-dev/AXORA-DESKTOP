# AXORA SUITE-P3A — Test Foundation and R4 Evidence

Dates: 2026-09-28–29 (Asia/Kolkata). Scope: test/QA infrastructure only. This is an implementation and evidence record, **not** R4 acceptance or permission to start P3B.

## Authority and pre-change state

Checked branch `main`, HEAD `75ca36ad12f9c64fc5ff88b549d1aece313353c2`. Before P3A edits, the root SDK pin, deleted nested `Axora-Desktop-WinUI/global.json`, and P0/P1/P2/V0/W5 planning artifacts were already unstaged/untracked. The formerly described `Axora-Desktop-WinUI.zip` was **absent** at this check; it was neither restored nor touched. No additional unexpected source changes were found. No staging, commit, push, PR, or history operation was performed.

## Implementation

- `Axora.Desktop.Tests/TestExecutionLedger.cs`: explicit 42-group manifest and terminal ledger. Each entry has ID, display name, delegate, workstream category, evidence type, environment requirement, and timeout. A metadata-only manifest mode and single-group mode let the outer runner derive its group list from this one registry. The ledger checks duplicate registered IDs, executed duplicates, missing IDs, unknown IDs, assertion failures, and group timeout/exception. Every registered group receives one terminal disposition: `Pass`, `Fail`, `EnvironmentNotAvailable`, `SkippedByPolicy`, or `Blocked`.
- `Axora.Desktop.Tests/Program.cs`: entry point uses the manifest; assertions are attributed to the active group. Four literal forced passes were removed/reclassified: two no-exception observations and two OCR-unavailable pseudo-passes. Direct test execution without temp-isolated APPDATA is blocked. W1.5 manager/validator integration uses a deterministic managed-file fixture detector instead of asking production `VersionDetector` to execute text named `.exe`. The real positive executable/version probe is an **explicit unverified group**, not a borrowed pass.
- `Axora.Desktop.Tests/W4_VoiceSubsystemTests.cs`: W4 is split into six manifest groups. Tier 3 records host observations and unavailable prerequisites instead of forced passes. Twenty-four static rule statements become gaps, not functional assertions. T4-01/T4-02/T4-03 no longer claim that weak socket/file-count/non-injected-canary checks prove privacy properties. T4-04 has 15 numbered iterations, 10-second start and stop waits, a 10-second dispose wait, and stage output. The start stage covers both WinRT compile and start-session internally; test-only code cannot distinguish those two native internals. A timeout is **not** evidence of recognizer correctness.
- `scripts/qa/run-winui-bounded.ps1`: each group runs in its own test child process, so a stuck native operation cannot silently prevent later groups from executing. The wrapper enforces a 45-minute overall deadline and each child timeout plus 20-second process grace, captures per-group stdout/stderr, verifies every child ledger, and aggregates one full-suite ledger. It uses isolated temp `APPDATA`/`LOCALAPPDATA`, terminates only the exact test PID when required, never kills an Axora app, and limits cleanup to the two exact test-owned settings directories. Incomplete settings cleanup makes the wrapper fail. Logs are retained. `-GroupIds` yields a separately labeled **targeted** result, never full-suite evidence.
- `scripts/qa/run-tests.ps1`: WinUI branch delegates to the bounded runner and no longer treats exit 0 without a ledger as a pass. MaterialUI branch was not changed. If a build is needed, it avoids the old pre-build cleaner, which kills all Axora processes by name.

Exit codes: test child `0` only for a complete no-fail/no-block child ledger (physical coverage explicitly incomplete), `1` for failed/blocked/missing/duplicate/unknown group, `2` for harness isolation/manifest/argument failure. Outer runner `0` only when every registered group has a valid terminal ledger, no fail/block/missing/duplicate/unknown condition, and verified settings cleanup; `1` for incomplete/crashed/timed-out/failed or cleanup-failed run; `2` when the executable is unavailable. A missing child summary never becomes success. Environment-unavailable and skipped-by-policy are allowed for the **deterministic-suite** exit only with incomplete physical coverage stated in the summary.

### Manifest

All groups have five-minute self-timeout unless noted. The **whole suite** has a 45-minute deadline; each child has an additional 20-second exit grace before the wrapper terminates that child PID. The seven-minute W4-T4 cap permits 15 cycles at up to 10 seconds for each start and stop plus setup/disposal. W4-T3 has a two-minute cap. A timed-out group is `Blocked`; subsequent groups run in fresh processes. If the overall deadline is reached, unvisited registered groups are explicitly `Blocked`, not silently omitted.

| Evidence class | Stable group IDs | Requirement / qualification |
|---|---|---|
| Deterministic | M3-PDF, M4-FLASH, M4-IMAGE, W1, W2-A, W2-F1, W3-C1, W3-C2, W3-C6-1, W3-C6-2, W3-C6-3, W3-C7-3, W4-T1 | No declared physical prerequisite. |
| Integration | W1.5, W2-B1, W2-B2, W2-D, W2-E, W2-F2, W2-F3, W2-F4, W2-F5, W3-B, W3-C3, W3-C4, W3-C5-RASTER, W3-C5-TIFF, W3-C6-4, W3-C7-2, W3-D, W3-E, W3-F, W4-T2, W4-INT | W1.5 uses a labeled managed-file fixture detector, not production native process probing. WIC/codec/PDF requirements are declared on relevant entries; W3-D's optional DirectML branches become `EnvironmentNotAvailable` when absent. |
| Environment runtime | W2-C, W2-E1, W3-C5-OCR, W3-C5-PDF, W4-T3 (2 min), W4-T4 (7 min) | Windows PDF/runtime, OCR pack, speech pack/microphone as declared per entry. OCR-unavailable live checks no longer count as pass. |
| Static conformance | W1.5-NATIVE, W4-RULES | Real positive executable-version probing needs a trusted bounded fixture; 24 voice rule statements need independent functional/manual evidence. These 25 gaps never count as passes. |

The executable manifest is the source of truth; this is its final per-group snapshot. Every row has a delegate, category/evidence class, prerequisite declaration, cap, and terminal result. Assertion counts are completed executions, not group counts.

| ID | Display name / runner | Category · evidence | Prerequisite | Cap | Disposition | Assertions pass/fail |
|---|---|---|---|---:|---|---:|
| M3-PDF | Resume PDF<br>`RunM3PdfTests` | M3 · Deterministic | none | 300s | PASS | 11/0 |
| M4-FLASH | Flashcards<br>`RunM4FlashcardsTests` | M4 · Deterministic | none | 300s | PASS | 31/0 |
| M4-IMAGE | Batch image<br>`RunM4BatchImageTests` | M4 · Deterministic | none | 300s | PASS | 17/0 |
| W1 | Foundation hardening<br>`RunW1HardeningTests` | W1 · Deterministic | none | 300s | PASS | 34/0 |
| W1.5 | Extension manager<br>`RunW1_5ExtensionManagerTests` | W1.5 · Integration | test-owned managed-file fixtures; no native executable launch | 300s | PASS | 84/0 |
| W1.5-NATIVE | Real executable version-probe gap<br>`RunW1_5NativeProbeGapTests` | W1.5 · StaticConformance | trusted bounded executable fixture not supplied | 300s | SKIPPED-BY-POLICY | 0/0 |
| W2-A | Converter domain<br>`RunW2_ACoreDomainTests` | W2 · Deterministic | none | 300s | PASS | 48/0 |
| W2-B1 | WIC image engine<br>`RunW2_B1WicImageEngineTests` | W2 · Integration | Windows WIC | 300s | PASS | 56/0 |
| W2-B2 | Document engines<br>`RunW2_B2DocumentEngineTests` | W2 · Integration | none | 300s | PASS | 73/0 |
| W2-C | PDF renderer POC<br>`RunW2_CPdfRendererPocTests` | W2 · EnvironmentRuntime | Windows PDF runtime | 300s | PASS | 28/0 |
| W2-D | Conversion orchestrator<br>`RunW2_DConversionOrchestratorTests` | W2 · Integration | none | 300s | PASS | 29/0 |
| W2-E | Converter view model<br>`RunW2_EUniversalConverterViewModelTests` | W2 · Integration | none | 300s | PASS | 27/0 |
| W2-E1 | Converter runtime<br>`RunW2_E1RealRuntimeInteractionTests` | W2 · EnvironmentRuntime | Windows runtime | 300s | PASS | 15/0 |
| W2-F1 | Optimization domain<br>`RunW2_F1OptimizationDomainModelTests` | W2 · Deterministic | none | 300s | PASS | 31/0 |
| W2-F2 | Image enhancement<br>`RunW2_F2ImageEngineEnhancementsTests` | W2 · Integration | Windows WIC | 300s | PASS | 28/0 |
| W2-F3 | Optimization UI model<br>`RunW2_F3OptimizationUiIntegrationTests` | W2 · Integration | none | 300s | PASS | 10/0 |
| W2-F4 | Queue telemetry<br>`RunW2_F4QueueTelemetryTests` | W2 · Integration | none | 300s | PASS | 19/0 |
| W2-F5 | Format capability<br>`RunW2_F5FormatCapabilityTests` | W2 · Integration | installed codecs | 300s | PASS | 15/0 |
| W3-B | Scholar persistence<br>`RunW3_BScholarPersistenceTests` | W3 · Integration | none | 300s | PASS | 74/0 |
| W3-C1 | Extraction contracts<br>`RunW3_C1ExtractionContractsTests` | W3 · Deterministic | none | 300s | PASS | 81/0 |
| W3-C2 | Text extraction<br>`RunW3_C2DeterministicExtractionTests` | W3 · Deterministic | none | 300s | PASS | 85/0 |
| W3-C3 | PDF extraction<br>`RunW3_C3PdfExtractionTests` | W3 · Integration | Windows PDF runtime | 300s | PASS | 51/0 |
| W3-C4 | DOCX extraction<br>`RunW3_C4DocxExtractionTests` | W3 · Integration | none | 300s | PASS | 68/0 |
| W3-C5-OCR | OCR<br>`RunW3_C5OcrTests` | W3 · EnvironmentRuntime | Windows OCR language pack | 300s | PASS | 38/0 |
| W3-C5-RASTER | Raster extraction<br>`RunW3_C5RasterImageExtractionTests` | W3 · Integration | none | 300s | PASS | 37/0 |
| W3-C5-PDF | Hybrid PDF OCR<br>`RunW3_C5PdfHybridOcrTests` | W3 · EnvironmentRuntime | Windows OCR language pack | 300s | PASS | 43/0 |
| W3-C5-TIFF | TIFF extraction<br>`RunW3_C5TiffExtractionTests` | W3 · Integration | none | 300s | PASS | 39/0 |
| W3-C6-1 | Normalization foundation<br>`RunW3_C6_1NormalizationFoundationTests` | W3 · Deterministic | none | 300s | PASS | 14/0 |
| W3-C6-2 | Unicode sanitization<br>`RunW3_C6_2UnicodeAndSanitizationTests` | W3 · Deterministic | none | 300s | PASS | 20/0 |
| W3-C6-3 | Structural normalization<br>`RunW3_C6_3StructuralNormalizationTests` | W3 · Deterministic | none | 300s | PASS | 43/0 |
| W3-C6-4 | Normalization integration<br>`RunW3_C6_4NormalizationIntegrationTests` | W3 · Integration | none | 300s | PASS | 79/0 |
| W3-C7-2 | Passage chunking<br>`RunW3_C7_2PassageChunkingIntegrationTests` | W3 · Integration | none | 300s | PASS | 48/0 |
| W3-C7-3 | Context windows<br>`RunW3_C7_3BoundedContextWindowBuilderTests` | W3 · Deterministic | none | 300s | PASS | 49/0 |
| W3-D | Index service<br>`RunW3_DIndexServiceTests` | W3 · Integration | optional DirectML GPU and neural model; lexical fallback available | 300s | ENV-NOT-AVAILABLE | 48/0 |
| W3-E | Search service<br>`RunW3_ESearchServiceTests` | W3 · Integration | none | 300s | PASS | 143/0 |
| W3-F | Study synthesis<br>`RunW3_FStudySynthesisEngineTests` | W3 · Integration | none | 300s | PASS | 119/0 |
| W4-T1 | Voice deterministic<br>`RunW4Tier1_DeterministicLogicTests` | W4 · Deterministic | none | 300s | PASS | 20/0 |
| W4-T2 | Voice mocked resilience<br>`RunW4Tier2_MockedCoordinatorResilienceTests` | W4 · Integration | none | 300s | PASS | 22/0 |
| W4-T3 | Voice host probes<br>`RunW4Tier3_EnvironmentDependentRuntimeTests` | W4 · EnvironmentRuntime | Windows voice and microphone | 120s | ENV-NOT-AVAILABLE | 0/0 |
| W4-T4 | Physical voice diagnostics<br>`RunW4Tier4_DiagnosticsAndPrivacyTests` | W4 · EnvironmentRuntime | speech pack and microphone; --physical-voice | 420s | ENV-NOT-AVAILABLE | 0/0 |
| W4-INT | Voice view-model integration<br>`RunW4_IntegrationTests` | W4 · Integration | none | 300s | PASS | 15/0 |
| W4-RULES | Voice rule matrix gaps<br>`RunW4_RuleMatrixConformanceAudit` | W4 · StaticConformance | none | 300s | SKIPPED-BY-POLICY | 0/0 |

Some groups mix deterministic and environment branches; the ledger classifies assertions by *group*, so the evidence-class totals are coarse, not proof that every assertion in a mixed group is deterministic. UI/manual interaction is not covered by the headless manifest. `scripts/qa/test-winui-ui.ps1` still contains `-or $true` patterns outside the narrow P3A runner/manifest scope; none of its results enter this ledger. This remains a manual/UI evidence gap.

## SDK and build evidence

Root `dotnet --version`: `9.0.318`. Inside `Axora-Desktop-WinUI`: `9.0.318`. Installed SDKs: `9.0.318`, `10.0.401`. No SDK pin was changed. Initial app build with restore failed (exit 1, 0 warnings, 1 error) because sandbox access to `C:\Users\rajghosh\AppData\Roaming\NuGet\NuGet.Config` was denied. This is not a compiler failure. Direct supported builds using existing restore assets:

| Working directory | Command | Exit | Warnings | Errors |
|---|---|---:|---:|---:|
| Repository root | `dotnet build Axora-Desktop-WinUI/Axora.Desktop/Axora.Desktop.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal --no-restore` | 0 | 0 | 0 |
| Repository root | `dotnet build Axora-Desktop-WinUI/Axora.Desktop.Tests/Axora.Desktop.Tests.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal --no-restore` | 0 | 21 | 0 |

The 21 test warnings are mostly nullable dereference warnings in existing test bodies, plus one unused-event and one inexact-read warning. The final test source was built with `-v:quiet --no-restore`: exit 0, 21 warnings, 0 errors. The app was independently rebuilt after the test changes: exit 0, 0 warnings, 0 errors. The final compiled test binary was exercised by the full 42-group run below.

## Complete bounded full-suite run

Final command: `& scripts/qa/run-winui-bounded.ps1` from the repository root, without `-PhysicalVoice`. The isolated APPDATA root was `C:\Users\rajghosh\AppData\Local\Temp\axora-p3a-cbc57ab40819487ca5594cd2349cff89\appdata`. The exact test-owned APPDATA/LOCALAPPDATA directories were removed after exit (`cleanup-failed=False`); per-group logs remain under the run root. The outer watchdog did not fire, no test child remains live, and no physical microphone loop ran.

| Metric | Final observed |
|---|---:|
| Manifest / dispositioned groups | 42 / 42 |
| Pass / Fail / EnvironmentNotAvailable / SkippedByPolicy / Blocked | 37 / 0 / 3 / 2 / 0 |
| Missing / duplicate / unknown | 0 / 0 / 0 |
| Completed assertion executions passed / failed | 1,692 / 0 |
| Environment observations / static gaps | 4 / 25 |
| Elapsed / wrapper exit | 52.5 seconds / 0 |
| Structured exit | `mode=full`, `PASS-INCOMPLETE-PHYSICAL-COVERAGE` |

Evidence-class assertion accounting from the same final ledger: deterministic **484 pass / 0 fail**; integration **1,084 pass / 0 fail**; environment-runtime **124 pass / 0 fail**. These are group-level evidence classifications; a mixed group may contain narrower subtests of another kind.

The 37 pass dispositions cover M3/M4, W1/W1.5 fixture-backed manager behavior, W2, most W3, and W4 deterministic/mocked/integration groups. Three groups are **environment-not-available**: W3-D's DirectML hardware/model-only branches (the same group completed 48 other assertions), W4-T3's Windows speech synthesizer activation (`0x800455A0`), recognition prerequisite and capture endpoint, and W4-T4's unavailable speech-recognition prerequisite. W4-T4 never opened a microphone. The two **skipped-by-policy/static-gap** groups are W1.5-NATIVE (no trusted executable fixture for positive production version probing) and W4-RULES (24 non-executing rule statements). Per-group stdout/stderr and the metadata manifest are retained in the run root; the console `SUITE-LEDGER` line is the aggregate record.

The first in-process diagnostic run had 41/41 dispositions but W1.5 timed out and caused 37 blocked groups. A later per-group diagnostic run ran all 41 groups and isolated two issues: W1.5's text-as-`.exe` status/version call timed out, and W4-T3 threw the host speech activation COM error. The final test-only fixes are reflected in the 42-group full run above. Targeted W1.5/W4-T3 checks also completed, but are not substituted for that final full result. The W1.5 timeout establishes a **test-fixture/probe interaction**, not the exact production native root cause.

An intermediate attempt to redirect all system-temp traffic under a run-owned root left an empty `Intel` directory that normal sandbox cleanup could not remove. That experiment was rolled back; APPDATA/LOCALAPPDATA isolation remains. The exact empty diagnostic directory was removed with scoped elevated access, and the two W1.5 fake-fixture directories left by earlier timed-out runs were removed after validating their names and contents. No unrelated temp directories or user files were deleted. The final run's settings cleanup succeeded without escalation.

### Historical assertion-count reconciliation

Historical `1,726` assertion executions (`1,637` pre-W4, `89` W4) are reference metadata, not an enforced target. The former R4 run emitted `1,685` pass lines, no final summary, and stopped around W4 T4-03/T4-04; it was **41 below** the historical count but never finished. The final P3A run completed **1,692** meaningful assertion executions, **34 below** the historical total and **7 above** the incomplete R4 pass-line count. These are different evidence regimes: four Program.cs forced passes, three W4 Tier-3 pseudo-passes and 24 W4 rule pseudo-passes were removed/reclassified; the physical Tier-4 assertions were unavailable and not run; the W1.5 positive native probe is now an explicit gap while its manager/validator fixtures remain executable tests. Conditional host paths and renamed/replaced checks prevent an exact one-to-one subtraction from the historical aggregate, which has no per-group ledger. The 34/7 numeric differences are observed, not labeled regressions.

## Recommendation and next gate

**R4 STILL BLOCKED.** SDK 9.0.318 resolves correctly; app and test projects compile; and the P3A deterministic/integration baseline now finishes with an honest complete ledger. However, P2's separate R4 acceptance gate also calls for a Visual Studio build entry-point check and a bounded fresh launch/graceful-close observation; neither is established by the P3A tests or this report. The positive production executable/version probe remains an explicit gap, and this host could not exercise physical voice. These gaps do not invalidate the SDK selection, but they prevent this report from recommending unconditional R4 acceptance. The user remains the acceptance authority. Do not proceed to P3B, new suite apps, or W5-P2 on this report alone.

Protected-scope check: no P3A edits to production C#, XAML, csproj, root/nested `global.json`, P0/P1/P2/V0/W5 documents, MaterialUI, or ZIP. Existing pre-change Git exceptions remain untouched. P3A files remain unstaged.
