# Phase W5 Verification Plan: AXORA Image Studio

**Capability ID**: W5-IMG  
**Official Phase**: W5 — Image Studio  
**Status**: W5-P1 VERIFICATION PLAN; W5-RC1 POST-P3A BASELINE RECONCILED — USER REVIEW REQUIRED  
**Repository**: D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP  
**Canonical Remote**: https://github.com/rajghosh06-dev/AXORA-DESKTOP  
**Target Solution**: Axora-Desktop-WinUI\Axora.Desktop.sln  
**Protected Baseline**: 75ca36ad12f9c64fc5ff88b549d1aece313353c2  

---

## 1. Purpose and current authorization

This plan defines the evidence required for W5-P2 through W5-F. It does not create tests, fixtures, packages, production code, XAML, or environment changes during W5-P1.

Evidence is accepted only when it verifies an observable condition or produces a named, reviewable audit artifact. Probe completion alone is not capability proof. ViewModel tests are not UI verification. A skipped conditional case does not prove that the capability works.

Every case below has a stable `V-IMG-*` ID and maps to at least one `PC-IMG-*` requirement and one `R-IMG-*` rule. Future execution records exactly one disposition per registered case:

- `Passed`;
- `Failed`;
- `Skipped/EnvironmentUnavailable`, with the missing prerequisite;
- `Blocked`, with the unresolved gate.

---

## 2. Baseline accounting and suite-completeness guard

**HISTORICAL / PRE-P3A REFERENCE** — the older W1–W4 assertion-execution accounting was:

| Segment | Assertion executions | Expected result |
|---|---:|---|
| Pre-W4 | 1,637 | historically reported all pass |
| W4 | 89 | historically reported all pass |
| Total W1–W4 | 1,726 | historically reported 1,726 pass, 0 fail |

The number 1,726 was an assertion-execution count, not a claim of 1,726 independent tests or a current release guard. That older accounting had known limitations:

- 24 W4 rule entries use unconditional `Assert(true)` checks;
- some environment checks prove only that a probe completed;
- the old runner did not enforce complete group disposition;
- no interactive WinUI, accessibility, or visual smoke suite was included in that count.

The accepted P3A pre-W5 run registered and dispositioned 42/42 groups: 37 Pass, 0 Fail, 3 EnvironmentNotAvailable, 2 SkippedByPolicy, 0 Blocked, and zero missing/duplicate/unknown groups. It executed 1,692 meaningful passing assertions with zero failures and reported `PASS-INCOMPLETE-PHYSICAL-COVERAGE`. Physical capability and interactive UI coverage remain separately qualified; 1,692 is evidence for that tested state, not a permanent expected count.

Completeness of this ledger is necessary, not sufficient, for W5 functional acceptance: a required W5 core behavior still needs its own applicable executing evidence. An unavailable or policy-skipped path cannot close that behavior's acceptance gate merely because the aggregate ledger is complete.

Future W5 work shall therefore:

1. run the current authoritative QA manifest for the tested commit; require every registered mandatory pre-W5 and W5 group to have one valid terminal disposition, with no Fail, Blocked, missing, duplicate, unknown, or failed assertion;
2. register W5 groups explicitly and record actual pre-W5 and W5 group/assertion totals separately, without freezing either historical or P3A counts;
3. maintain a stage-specific case manifest containing every `V-IMG-*` ID expected for the authorized stage and fail completeness on a missing registered W5 group;
4. reject literal `Assert(true)`, forced-pass branches, probe-completed-only checks, non-executing rule statements, and unregistered executable cases as functional verification;
5. report EnvironmentNotAvailable and SkippedByPolicy with specific reasons, not as functional Pass; a Blocked group fails the gate;
6. keep static/manual gaps, physical/environment observations, and executing UI evidence distinct from deterministic/integration functional assertions; a bounded runtime observation proves only its observed path;
7. retain the build log, manifest, full ledger, tested commit/SDK, environment record, and UI/manual checklists together.

No baseline tests are changed by this W5-RC1 documentation reconciliation.

---

## 3. Planned evidence artifacts

The following are future artifacts, not files authorized by this task:

| Artifact | Proposed future location | Purpose |
|---|---|---|
| W5 codec proof harness | `Axora-Desktop-WinUI/Axora.Desktop.Tests/W5CodecFeasibilityTests.cs` | Isolated WIC, Skia, and existing-engine probes |
| Codec fixtures | `Axora-Desktop-WinUI/Axora.Desktop.Tests/TestData/W5CodecProof/` | Synthetic, redistributable positive and negative inputs |
| Fixture manifest | `Axora-Desktop-WinUI/Axora.Desktop.Tests/TestData/W5CodecProof/manifest.json` | SHA-256, provenance/license, expected properties and negative intent |
| Codec decision report | `docs/W5_CODEC_PROOF_MATRIX.md` | Approved per-format classification and backend evidence |
| W5 case manifest | future W5 test/evidence output | Expected IDs, stage, evidence kind and disposition |
| UI/accessibility checklist | future W5-F evidence output | Runtime environment, screenshots and observations |
| Performance/environment record | future W5-F evidence output | Reproducible benchmark and host prerequisites |

Fixture paths and harness changes require separate W5-P2 authorization. Production, XAML, package, and MaterialUI files remain read-only during that proof gate.

---

## 4. Verification catalogue

### 4.1 Static planning and governance — `V-IMG-S-*`

| Verification ID | Product requirements | Rules | Method and acceptance evidence |
|---|---|---|---|
| V-IMG-S-01 | PC-IMG-014, PC-IMG-027 | R-IMG-036 | Parse all four planning documents for defined/referenced IDs; require every product requirement to reach architecture/rule/evidence, every rule to reach evidence, every evidence item to point back, and zero undefined/orphan IDs. Retain the graph/audit output as a phase-gate artifact. |
| V-IMG-S-02 | PC-IMG-001, 002, 026 | R-IMG-001, 002, 035 | Audit W5's AXORA Studio single-image ownership against AXORA Tools Batch Image and future optional AXORA Mind model-driven image work; inspect local design, explicit exclusions, and dependencies. Require no W5 core Win2D, SVG rasterizer, AI/model, cloud, or Python runtime commitment. |
| V-IMG-S-03 | PC-IMG-001, 021, 026 | R-IMG-001, 030, 035 | Inspect the authorized design against `IConversionEngine`, `WicImageConversionEngine`, Batch Image, WIC and Skia. Require a shared low-level strategy and no third unrelated codec stack or opportunistic W2/M4 rewrite. |
| V-IMG-S-04 | PC-IMG-021, 022 | R-IMG-030, 031 | Compare the stage diff with the approved existing-file allowlist and MaterialUI boundary. Any unapproved existing path or any MaterialUI byte change fails. |
| V-IMG-S-05 | PC-IMG-002 | R-IMG-002 | Audit W5 source/dependencies for network clients, endpoints, telemetry/image egress, and hidden cloud fallback. Pair later with bounded runtime socket observation; neither observation nor static audit is overstated. |
| V-IMG-S-06 | PC-IMG-027 | R-IMG-036 | Review pre-implementation gates and debt classification. Preserve verified R4 SDK-selection evidence while its checkpoint is uncommitted; require accepted suite foundation and relevant Studio readiness/V0 gates, separately authorized codec proof, ownership/migration boundary, and explicit non-scope for unrelated debt before W5-A. |
| V-IMG-S-07 | PC-IMG-023 | R-IMG-032 | Audit the current QA registry and stage-specific W5 IDs for complete terminal dispositions, zero failures/blocks/missing/duplicate/unknown groups, separate actual totals, weak-evidence exclusions, and reasoned unavailable/policy-skipped results. Do not enforce a historical assertion count. |
| V-IMG-S-08 | PC-IMG-003, 025 | R-IMG-003, 023 | Review ownership/lease/disposal paths and live-buffer ledger. Require one owner per native resource and declared concurrent buffer classes; working-set-only evidence is insufficient. |

### 4.2 W5-P2 codec and feasibility gate — `V-IMG-P2-*`

| Verification ID | Product requirements | Rules | Method and acceptance evidence |
|---|---|---|---|
| V-IMG-P2-01 | PC-IMG-011, 014, 027 | R-IMG-004, 008 | Validate manifest provenance and SHA-256; sniff container independently of extension; record dimensions/orientation/pixel format/alpha before decode where possible for every fixture. |
| V-IMG-P2-02 | PC-IMG-013, 014, 016, 027 | R-IMG-008, 015, 025 | PNG: independently probe WIC and Skia decode/encode, RGBA/palette/interlaced pixels, alpha, DPI/ICC/EXIF/XMP enumeration, each metadata policy, output magic, and cross-backend reopen. |
| V-IMG-P2-03 | PC-IMG-013, 014, 016, 027 | R-IMG-008, 015, 025 | JPEG: baseline/progressive/orientation-once decode, proved quality parameter range/default and output response, lossy MAE/PSNR tolerance, explicit matte, metadata policies, output validation, and cross-backend reopen. Never use bit identity for lossy output. |
| V-IMG-P2-04 | PC-IMG-013, PC-IMG-014, PC-IMG-016, PC-IMG-027 | R-IMG-008, R-IMG-009, R-IMG-015, R-IMG-025 | WebP: static lossy/lossless decode/encode, alpha and objective metrics, metadata capability, animated frame enumeration, and explicit static/animated classification. |
| V-IMG-P2-05 | PC-IMG-013, PC-IMG-014, PC-IMG-016, PC-IMG-027 | R-IMG-008, R-IMG-015, R-IMG-025 | BMP: 24-bit, 32-bit/bitfields and top-down behavior; row orientation, actual alpha/matte behavior, encode/reopen, magic, and metadata limitations. |
| V-IMG-P2-06 | PC-IMG-013, 014, 016, 027 | R-IMG-008, 009, 015, 025 | TIFF: little/big-endian, uncompressed/LZW, alpha, metadata and multipage enumeration. Parse tag 259 after encode; do not infer LZW from successful decode or extension. |
| V-IMG-P2-07 | PC-IMG-014, PC-IMG-016, PC-IMG-027 | R-IMG-008, R-IMG-009, R-IMG-025 | GIF: static transparency and animated frame duration/disposal/count; record metadata read/write/preserve capability and determine whether deterministic disclosed read-only frame selection is acceptable. No core encoder promise. |
| V-IMG-P2-08 | PC-IMG-014, PC-IMG-016, PC-IMG-027 | R-IMG-008, R-IMG-009, R-IMG-025 | ICO: enumerate embedded BMP/PNG entries, record metadata read/write/preserve capability, and prove deterministic resolution/bit-depth selection. No core encoder promise. |
| V-IMG-P2-09 | PC-IMG-014, 016, 027 | R-IMG-008, 025 | HEIC/HEIF: record OS build, installed extension/codec, WIC and Skia results, orientation/metadata, unavailable-path behavior and read-only classification. Never report universal host support. |
| V-IMG-P2-10 | PC-IMG-002, 014, 026, 027 | R-IMG-002, 008, 035 | SVG: prove container sniffing, confirm no current rasterizer/dependency, and exercise external-reference/entity fixtures with bounded network/file-access observation. Expected W5-P1 disposition is deferred/unsupported unless separately reviewed evidence changes it. |
| V-IMG-P2-11 | PC-IMG-011, 017, 027 | R-IMG-004, 005, 006, 007, 008 | Run zero-byte, mismatch, truncated, corrupt-offset/chunk/IFD, malformed metadata and oversized-header probes in a child process with timeout. Record exception/HRESULT, crash/hang, elapsed time, peak resource use, source hash, destination/staging state, and proof that full-pixel decode was not entered for oversized headers. |
| V-IMG-P2-12 | PC-IMG-014, 027 | R-IMG-008, 009, 036 | Produce the signed-off matrix with one of `CORE SUPPORTED`, `READ ONLY`, `CONDITIONAL`, `DEFERRED`, or `UNSUPPORTED` per format/backend, plus unavailable-path degradation. No format reaches Core merely because it works on one configured host. |

### 4.3 Deterministic domain, state, and pixel logic — `V-IMG-D-*`

| Verification ID | Product requirements | Rules | Method and acceptance evidence |
|---|---|---|---|
| V-IMG-D-01 | PC-IMG-003 | R-IMG-003 | Compare canonical source pixels/state before and after edits, undo/redo and render requests. Mutation attempts cannot escape the session boundary; history contains state only. |
| V-IMG-D-02 | PC-IMG-011, 025 | R-IMG-004, 005, 006 | Boundary/property cases for zero, negative/overflowing dimensions, 16,384 edges, 100,000,000 area, stride and live-buffer arithmetic. Validate exact structured disposition without unsafe allocation. |
| V-IMG-D-03 | PC-IMG-011, 017 | R-IMG-007 | Map known validation/codec/filesystem/native errors to stable structured results and redacted user/diagnostic views; unknown errors remain contained and observable. |
| V-IMG-D-04 | PC-IMG-005, 012 | R-IMG-010, 016 | Round-trip normalized crop through odd full/proxy dimensions; cover finite checks, half-open floor/ceiling, clamping, one-pixel minimum, aspect rounding, and viewport exclusion from edit state. |
| V-IMG-D-05 | PC-IMG-005, 012 | R-IMG-011, 012 | Use asymmetric labeled/corner pixels to verify orientation, every quarter-turn, horizontal/vertical flips, crop, transform order, crop reset on a later geometry change, its single undoable transaction, and conditional deskew rules. Output, not internal flags, is the oracle. |
| V-IMG-D-06 | PC-IMG-006, 007 | R-IMG-013 | Golden/reference vectors and boundary values for every adjustment/preset, including non-finite rejection, clamping, formula version, kernel/edge policy and operation order. Unspecified transforms remain blocked. |
| V-IMG-D-07 | PC-IMG-007, 013 | R-IMG-014, 015 | Pixel math for alpha preservation, premultiply/unpremultiply, transparent colored edges, matte composition, sRGB working limitation, and exact default-white JPEG matte. |
| V-IMG-D-08 | PC-IMG-009 | R-IMG-017, 018 | Prove 50 undo transitions, oldest-state eviction, one gesture/one commit, transient update exclusion, no-op/cancel behavior, redo clearing, reset, and load/close isolation. |
| V-IMG-D-09 | PC-IMG-010, 018 | R-IMG-020, 021 | Deterministic scheduler model with monotonic session/request IDs, deliberately reordered completions, stale suppression, and distinct requested/confirmed cancellation states. |
| V-IMG-D-10 | PC-IMG-008, 016 | R-IMG-025 | Verify metadata-field categorization and policy projection, entire GPS group, known location/device/serial/owner/creator identifiers, maker-note/opaque-block handling and orientation normalization. |
| V-IMG-D-11 | PC-IMG-004, 008 | R-IMG-024 | Verify zoom bounds, fit/actual-pixel math, viewport-state exclusion, histogram bins/revision identity and before/after reference selection without document mutation. |

### 4.4 Codec, render, metadata, and filesystem integration — `V-IMG-I-*`

| Verification ID | Product requirements | Rules | Method and acceptance evidence |
|---|---|---|---|
| V-IMG-I-01 | PC-IMG-003, 011, 014 | R-IMG-003, 004, 008 | Decode each approved positive fixture through the selected shipping adapter; record canonical format and prove source-file and canonical-source hashes remain unchanged. |
| V-IMG-I-02 | PC-IMG-011, 017 | R-IMG-004, 005, 006, 007 | Exercise zero/truncated/mismatch/oversized inputs with instrumentation showing the guard occurs before full decode/allocation when supported and the active document remains coherent. |
| V-IMG-I-03 | PC-IMG-005, 012 | R-IMG-011, 016 | Render odd-size/oriented/cropped fixtures at proxy and full resolution; compare logical geometry exactly and pixels using operation-appropriate tolerances. |
| V-IMG-I-04 | PC-IMG-006, 007, 012, 013 | R-IMG-011, 013, 014 | Compare preview and full renderer against the CPU reference for every approved adjustment/order; record lossless exactness or justified tolerance and alpha-edge behavior. |
| V-IMG-I-05 | PC-IMG-013 | R-IMG-014, 015 | Round-trip transparent fixtures through every approved alpha format and JPEG matte paths; re-decode to check alpha and colored-edge/fringe behavior. |
| V-IMG-I-06 | PC-IMG-008, 016 | R-IMG-025 | For metadata-rich fixtures, enumerate before/after each policy. Record retained/lost/unsupported fields; require prohibited supported fields absent; never label residual-unknown output “PII free.” |
| V-IMG-I-07 | PC-IMG-015, 017 | R-IMG-026, 028 | New destination: unique sibling stage, close/flush, signature/dimension/nonempty/reopen/metadata validation, collision recheck and successful publication. Inject a race-created destination and prove no overwrite. |
| V-IMG-I-08 | PC-IMG-015, 017 | R-IMG-027, 028 | Existing destination and source equality: require path-bound pointer/keyboard authorization, exercise canonical/alias paths and unavailable safe replacement, and prove prior bytes remain on rejection/failure. |
| V-IMG-I-09 | PC-IMG-017, 018 | R-IMG-007, 021, 028 | Inject encoder, validation, permission, lock, disk-full and cancellation failures before/during publication. Before publication, require destination preservation and truthful confirmed cleanup; after commit starts, defer cancellation, verify/report the observed postcondition, and never emit `CancelConfirmed` when preservation is unknown. Cleanup remains limited to operation-owned artifacts. |
| V-IMG-I-10 | PC-IMG-003, 015 | R-IMG-003, 027 | Hash the source file before/after load, edits, failed/successful exports and source-replacement denial/authorization paths; no silent write is permitted. |

### 4.5 Concurrency, lifecycle, and resources — `V-IMG-C-*`

| Verification ID | Product requirements | Rules | Method and acceptance evidence |
|---|---|---|---|
| V-IMG-C-01 | PC-IMG-010, 019 | R-IMG-019 | Block/instrument a heavy codec/render worker while a UI heartbeat continues; require bounded scheduler activity and no synchronous UI-thread raster work. Timing is reported, not universalized. |
| V-IMG-C-02 | PC-IMG-010, 019 | R-IMG-022 | Invoke completion from a worker and require preview/status/property publication on the captured DispatcherQueue; detect direct off-thread mutation. |
| V-IMG-C-03 | PC-IMG-010, 018 | R-IMG-020 | Complete old and new renders out of order, then switch/close documents. Only the latest valid session/request publishes and all late rasters are disposed. |
| V-IMG-C-04 | PC-IMG-018 | R-IMG-021 | Cover cancellation before start, cooperative cancellation, non-cooperative native completion, supersession and close. Require truthful requested/confirmed states and no canceled result publication. |
| V-IMG-C-05 | PC-IMG-010, 025 | R-IMG-006, 019, 023 | Instrument one-active/one-latest-pending previews, export single-flight, proxy/export exclusion where budget requires, and declared concurrent buffer classes. Reject over-budget admission. |
| V-IMG-C-06 | PC-IMG-025 | R-IMG-006, 023 | Track owned native handles/leases/streams/staging across repeated load/render/export/close and failure paths. Pair exact ownership counters with working-set/private-byte observations. |

### 4.6 ViewModel and application contracts — `V-IMG-VM-*`

| Verification ID | Product requirements | Rules | Method and acceptance evidence |
|---|---|---|---|
| V-IMG-VM-01 | PC-IMG-004, 008, 009 | R-IMG-017, 018, 024 | Verify command enablement/state for load, viewport, compare, undo/redo, gesture commit/cancel, history reset and revision-bound inspection results. |
| V-IMG-VM-02 | PC-IMG-011, 017 | R-IMG-007 | Feed every structured load/render/export outcome and verify stable busy/progress/error state, non-modal presentation intent and retention of the last coherent document. |
| V-IMG-VM-03 | PC-IMG-015, 020 | R-IMG-027 | Verify new/existing/source-equal destination gating and path-bound confirmation. No command or voice route may bypass the picker/confirmation boundary. |
| V-IMG-VM-04 | PC-IMG-020 | R-IMG-029 | Register only safe W5 commands, normalize/collision-check aliases, verify enabled/disabled session lifetime, action routing and prohibited write/confirmation commands. Record that router proof is not speech-recognition proof. |

### 4.7 Executing WinUI automation/runtime — `V-IMG-UIA-*`

| Verification ID | Product requirements | Rules | Method and acceptance evidence |
|---|---|---|---|
| V-IMG-UIA-01 | PC-IMG-019, 021, 024 | R-IMG-022, 033 | Launch the normal WinUI build, navigate through the real Shell route, instantiate the page, exercise bindings/commands, and require no XAML binding or `MarkupCompilePass2` failure. |
| V-IMG-UIA-02 | PC-IMG-015, 024 | R-IMG-027, 033 | Exercise file-picker success/cancel/error, new/existing/source-equal destination dialogs, collision, confirmation and non-destructive cancellation through the UI. |
| V-IMG-UIA-03 | PC-IMG-004, 005, 008, 009, 024 | R-IMG-010, 012, 017, 024, 033 | Use pointer and keyboard to pan/zoom/fit/actual pixels, crop with ratios, commit/cancel one gesture, undo/redo and hold/toggle before/after. Verify focus remains usable. |
| V-IMG-UIA-04 | PC-IMG-010, 017, 018, 019, 024 | R-IMG-019, 022, 033 | Exercise progress, supersession, cancel-requested/confirmed, InfoBar/error presentation and UI heartbeat during real background work. |
| V-IMG-UIA-05 | PC-IMG-020, 024 | R-IMG-029, 033 | Inspect UI Automation names, roles, enabled states and supported patterns; invoke safe W5 actions through the application boundary and prove no voice/automation overwrite path exists. |

### 4.8 Manual visual and accessibility — `V-IMG-MVA-*`

| Verification ID | Product requirements | Rules | Method and acceptance evidence |
|---|---|---|---|
| V-IMG-MVA-01 | PC-IMG-024 | R-IMG-033 | Retained checklist/screenshots at 100%, 150% and 200% DPI; minimum window, resize/maximize; light, dark and high-contrast themes; focus order/visuals, text clipping and hit targets. |
| V-IMG-MVA-02 | PC-IMG-004, 007, 008, 013, 024 | R-IMG-014, 024, 033 | Inspect checkerboard/alpha, default/custom JPEG matte, histogram, before/after alignment, image clarity and state feedback using reference fixtures. |
| V-IMG-MVA-03 | PC-IMG-005, 024 | R-IMG-012, 033 | Inspect crop handles, aspect labels, keyboard cues, pointer/touch hit targets, rotation/flip result and conditional deskew disclosure at supported scale factors. |

### 4.9 Environment and performance observations — `V-IMG-ENV-*`

| Verification ID | Product requirements | Rules | Method and acceptance evidence |
|---|---|---|---|
| V-IMG-ENV-01 | PC-IMG-014, 025, 027 | R-IMG-008, 034, 036 | Record OS build, installed codecs/extensions, WIC/Skia versions and both available/unavailable paths. Host-dependent success remains Conditional. |
| V-IMG-ENV-02 | PC-IMG-025 | R-IMG-034 | Benchmark decode, proxy, representative adjustments, histogram, full render, export and cancellation observation. Record hardware, OS/driver, fixture hash, dimensions/frame/pixel format, backend, operation, warm/cold state, sample count, median/P95, and managed/native/private/working-set peaks. |
| V-IMG-ENV-03 | PC-IMG-002, 025 | R-IMG-002, 034 | Perform bounded socket/process observation during representative local workflows and record the Dell Inspiron 14 5430, 16 GB RAM and Intel Iris Xe reference environment. Report observations, not “zero cloud” or universal hardware guarantees. |

### 4.10 Regression and release governance — `V-IMG-REG-*`

| Verification ID | Product requirements | Rules | Method and acceptance evidence |
|---|---|---|---|
| V-IMG-REG-01 | PC-IMG-023 | R-IMG-032 | Run the then-current authoritative pre-W5 QA manifest for the tested commit. Require every mandatory registered group validly dispositioned, zero Fail/Blocked/missing/duplicate/unknown and zero failed assertions; report actual counts and justified EnvironmentNotAvailable/SkippedByPolicy without promoting them to Pass. |
| V-IMG-REG-02 | PC-IMG-023 | R-IMG-032 | Register and run authorized W5 groups in the current manifest or a linked stage-specific manifest. Every expected W5 group must have one valid disposition; missing groups fail completeness. Report W5 assertions/cases separately from pre-W5, manual/static, physical/environment and executing UI evidence; invent no final W5 count in planning. |
| V-IMG-REG-03 | PC-IMG-019, 024 | R-IMG-019, 033 | Perform a normal clean WinUI build with XAML `MarkupCompilePass2`; retain compiler/analyzer log and reject any new W5 build warning/error debt. |
| V-IMG-REG-04 | PC-IMG-001, 020, 021 | R-IMG-001, 029, 030 | Execute focused W1–W4 behavioral regression across shared conversion, Batch Image, shell navigation/command palette and voice registration. Do not silently repair unrelated known defects. |
| V-IMG-REG-05 | PC-IMG-022 | R-IMG-031 | Compare MaterialUI against the protected baseline and require a completely empty diff/status attributable to W5. |
| V-IMG-REG-06 | PC-IMG-014, 021, 027 | R-IMG-036 | Re-run the trace/orphan audit, codec decision gate, toolchain/ownership sign-off, allowlist and evidence inventory before authorizing W5-A or declaring W5-F complete. |

---

## 5. W5-P2 fixture matrix

All positive fixtures should be synthetic AXORA-owned patterns or otherwise explicitly redistributable. Do not use personal photos or real PII. The manifest records filename, SHA-256, origin/generation method, license, expected container, dimensions, pixel format, alpha values, orientation, metadata inventory, frame/page count, and negative-test intent.

| Family | Required controlled fixtures | Required proof focus |
|---|---|---|
| PNG | `png-rgba8-alpha-grid.png`, `png-palette-trns.png`, `png-interlaced-rgb.png`, `png-metadata-icc-xmp-exif.png` | lossless canonical pixels, alpha 0/128/255, palette/interlace, DPI/ICC/metadata |
| JPEG | `jpeg-baseline-rgb.jpg`, `jpeg-progressive-rgb.jpg`, `jpeg-exif-orientation6-gps-serial-icc.jpg` | orientation exactly once, lossy metrics, matte, sensitive metadata |
| WebP | `webp-lossless-rgba.webp`, `webp-lossy-rgb.webp`, `webp-animated-2frame.webp` | lossless/lossy, alpha, metadata capability, frame behavior |
| BMP | `bmp-rgb24.bmp`, `bmp-bitfields-bgra32-alpha.bmp`, `bmp-topdown-rgb24.bmp` | row direction, actual alpha, encode/reopen, metadata limits |
| TIFF | `tiff-le-uncompressed-rgba.tiff`, `tiff-be-lzw-rgb.tiff`, `tiff-multipage-2page.tiff`, `tiff-exif-icc.tiff` | endian/compression tag 259, alpha, metadata, page enumeration |
| GIF | `gif-static-transparent.gif`, `gif-animated-2frame.gif` | transparency, count, duration/disposal, disclosed frame policy |
| ICO | `ico-multisize-16-32-256.ico` | embedded entry enumeration and deterministic selection |
| HEIF/HEIC | `heic-still-8bit.heic`, `heif-metadata-orientation.heif` | host extension, decode, orientation, metadata, unavailable path |
| SVG | `svg-basic-self-contained.svg`, `svg-transparent-gradient.svg`, `svg-external-reference.svg`, `svg-doctype-entity.svg` | no current renderer assumption; no external file/network resolution |
| Cross-format reference | `alpha-colored-fringe.png`, `color-chart-srgb.png` | alpha-safe resampling/matte and documented color limitation |
| Negative/adversarial | `invalid-zero.bin`, valid PNG/JPEG with wrong extensions, per-format truncated header/body, invalid chunk/offset/IFD, malformed metadata, and per-format header-only oversized variants | structured failure, timeout containment, no unsafe allocation/write |

For every applicable format/backend, the proof report records:

- detected container versus claimed extension;
- dimensions, orientation, pixel format, alpha and frame/page count;
- WIC decode/encode and Skia decode/encode separately;
- the existing `WicImageConversionEngine` file-to-file path separately;
- output magic, nonzero length, close/flush and cross-backend reopen;
- normalized pixel equality for lossless data or objective error metrics for lossy data;
- metadata read, `PreserveBestEffort`, `StripSensitive`, and `StripAllSupported` results;
- source hash before/after;
- malformed/crash/hang/HRESULT behavior and peak resource observation;
- Windows codec/extension, WIC and Skia dependencies;
- environment and final classification with rationale.

Core classification requires guarded detection/dimensions, decode and encode through shipping dependencies, validated/reopened output, declared alpha/matte behavior, structured malformed-input failure, and applicable metadata evidence. Host-extension reliance is Conditional. Decode-only is Read Only. A new dependency/design is Deferred. Unsafe or unavailable behavior is Unsupported.

---

## 6. Exact W5-P2 action sequence — plan only

1. Capture immutable repository and environment facts: HEAD, branch, status, project dependency versions, OS build, `dotnet --version` from root and WinUI directory, and installed Windows codec evidence.
2. Reconfirm the verified R4 toolchain policy for the tested checkout: repository-root `global.json` requests SDK `9.0.300`, `rollForward: latestPatch`, `allowPrerelease: false`; root and WinUI resolved installed `9.0.318`, and Visual Studio/MSBuild build plus fresh launch/normal close were observed. At W5-RC1 (2026-09-29), the old nested policy deletion and root policy were uncommitted; verification evidence was complete for checkpoint review, not yet Git-checkpointed. Do not edit either `global.json` as part of W5.
3. Create only the approved isolated proof harness, controlled fixtures and manifest; make no production/XAML/package refactor and install no dependency.
4. Probe Skia `SKCodec`/encoding and WIC `BitmapDecoder`/`BitmapEncoder` independently, then the existing `WicImageConversionEngine` path.
5. Sniff bytes and perform checked header/resource admission before decode; normalize successful decodes to one recorded canonical representation.
6. Encode to a new sibling stage, validate/reopen through both available backends, and compare exact/tolerant pixels as applicable.
7. Enumerate metadata and execute each policy without claiming complete PII removal.
8. Enumerate multi-frame/page inputs and record the selected-frame disclosure policy.
9. Run malformed/native probes out of process with a bounded timeout; prove oversized fixtures do not enter full-pixel decode where the container permits header inspection.
10. Produce `docs/W5_CODEC_PROOF_MATRIX.md`, classify every family, decide the shared raster extraction boundary, rerun the protected baseline, verify MaterialUI zero-diff, and obtain user review before W5-A.

W5-P2 is proof-oriented. It does not build the editor.

---

## 7. Tooling note

The user reports Conda environments `base`, `AI`, `GENERAL`, and `ML`, and permits `GENERAL` when genuinely useful. `GENERAL` may be used later as an optional, independent fixture generator or oracle only after its interpreter/packages are recorded. Official W5 proof must not depend on that local environment: generated fixtures are static, hashed, licensed/provenanced, and consumable by the native test harness. Python/Conda is never a product/runtime/CI dependency, and W5-P1/P2 authorizes no package installation.

---

## 8. Bidirectional traceability and orphan gate

The catalogue above is the verification-to-product/rule reverse index. The Product Contract provides product-to-rule links, the Architecture provides product-to-architecture-to-rule links, and the Rule Matrix provides rule-to-verification links.

At each authorized stage, a mechanical audit shall:

1. enumerate definitions of `PC-IMG-001` through `PC-IMG-027`;
2. enumerate definitions of `R-IMG-001` through `R-IMG-036`;
3. enumerate every defined `V-IMG-*` item;
4. reject an undefined reference, duplicate definition, missing forward link, missing reciprocal link, or verification item without both a product and rule source;
5. emit counts and four orphan sets: Product, Architecture, Rule, Verification;
6. require all four orphan sets to be empty.

Deferred capabilities pass only a static scope-boundary/dependency check. They do not receive a fake functional pass. Conditional codecs require both available-path evidence and graceful unavailable-path evidence.

---

## 9. Stage exit gates

| Stage | Exit evidence |
|---|---|
| W5-P1 | Four reconciled planning documents, zero-orphan audit, Git/document-scope safety, user review |
| W5-P2 | Approved codec matrix, fixture manifest, toolchain/ownership decisions, protected baseline and MaterialUI zero-diff |
| W5-A | Deterministic domain/safety/ownership evidence and shared-codec preservation |
| W5-B | State/history/gesture/proxy/scheduler/concurrency evidence |
| W5-C | Full-render, alpha, metadata, staged export and filesystem failure evidence |
| W5-D | ViewModel plus executing WinUI/navigation/binding/interaction evidence |
| W5-E | Safe W4 router integration with known end-to-end recognition limitation disclosed |
| W5-F | Build, current complete pre-W5 and registered W5 manifests with honest dispositions and actual counts, UI/accessibility, performance/environment, diff and zero-orphan evidence |

No stage begins merely because this plan exists; each requires explicit authorization.
