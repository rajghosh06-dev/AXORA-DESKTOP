# Phase W5 Product Contract: AXORA Image Studio

**Capability ID**: W5-IMG  
**Official Phase**: W5 — Image Studio  
**Status**: W5-P1 PLANNING CONTRACT; W5-RC1 POST-P3A BASELINE RECONCILED — USER REVIEW REQUIRED  
**Repository**: D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP  
**Canonical Remote**: https://github.com/rajghosh06-dev/AXORA-DESKTOP  
**Target Solution**: Axora-Desktop-WinUI\Axora.Desktop.sln  
**Protected Baseline**: 75ca36ad12f9c64fc5ff88b549d1aece313353c2  

---

## 1. Phase identity and authority

The current Git progression is authoritative:

- W1 — Foundation Hardening
- W1.5 — Extension Manager
- W2 — Universal Converter
- W3 — Scholar Kit, extraction, indexing, and retrieval
- W3-F — Study Synthesis Engine
- W4 — Voice Capabilities
- W5 — Image Studio
- W6 — Integrity Center
- W7 — System Tools / Continuity

Image Studio retains the W5 identity. In the accepted suite direction, deterministic single-image Image Studio belongs to AXORA Studio; Batch Image belongs to AXORA Tools. The existing `Axora.Desktop` host remains the reference until a separately authorized, verified Studio migration. Older documents that reused milestone numbers are historical context and do not redefine this phase.

This contract governs product intent. The Architecture defines the implementation model, the Rule Matrix defines normative invariants, and the Verification Plan defines acceptable evidence. These documents do not authorize W5-P2 or production implementation. W5-P2 remains a separately authorized codec/feasibility proof; production work additionally requires the accepted suite foundation and relevant Studio readiness/V0 gates, W5-P2 acceptance, and an approved ownership/migration scope.

---

## 2. AXORA law: user problem first

No W5 feature enters implementation merely because a library or model exists. Each capability must answer:

1. What user problem does it solve?
2. Who experiences that problem, and how often?
3. What does the user do today?
4. What pain does AXORA remove?
5. Can it work locally?
6. Does it require a model or dependency?
7. Can that dependency be optional?
8. What storage and memory does it consume?
9. What happens without Internet?
10. What happens when the component is unavailable?
11. How is the result verified?
12. Can the user undo or recover?

If those answers are not convincing and testable, the feature does not enter the W5 roadmap.

### 2.1 User problem

Students, researchers, knowledge workers, and desktop users frequently need to inspect and correct one important scan, diagram, photograph, signature image, or document figure. Their current choices are often heavyweight editors with a steep workflow cost or web utilities that require uploading private material.

AXORA already has batch image processing. The missing capability is a focused single-image workspace for careful inspection, reversible edits, privacy-aware export, and predictable local behavior.

### 2.2 W5 product answer

W5 provides a single-image, non-destructive editing session built on shared AXORA raster and codec primitives. It complements, and does not replace, Batch Image or Universal Converter.

### 2.3 W5 admission record

This table records the rationale; the requirement catalogue below remains the normative contract.

| Problem-first question | W5 answer |
|---|---|
| Problem, users, frequency | Students, researchers, knowledge workers, and general desktop users repeatedly need to inspect or correct one scan, figure, photograph, or signature image. |
| Current workaround and pain removed | Heavy editors add workflow overhead; web tools add upload/privacy risk. W5 supplies a focused local workflow inside AXORA. |
| Local and offline behavior | Core edit, inspect, undo/redo, and approved-codec export run locally without Internet. User/OS-controlled clipboard or synced destinations remain explicit actions. |
| Model/dependency and optionality | Core uses existing WIC/Skia infrastructure and no model. Host codecs are optional capabilities exposed only after W5-P2 proof; absence degrades explicitly. |
| Storage and memory | W5 downloads no model and stores state rather than raster history. Admission uses hard dimension/area ceilings plus a declared live-buffer budget. |
| Component unavailable | The capability is hidden, disabled, or returns a structured unavailable/unsupported result; no cloud fallback or silent semantic change occurs. |
| Verification | The Rule Matrix defines 36 observable invariants and the Verification Plan defines deterministic, integration, runtime, UI, accessibility, environment, and regression evidence. |
| Undo and recovery | `MaxUndoDepth = 50`, gesture coalescing, redo rules, source immutability, staged export, destination preservation, and best-effort owned-artifact cleanup provide recovery boundaries. |

---

## 3. Authoritative requirement catalogue

| Requirement | Normative product requirement | Primary rules |
|---|---|---|
| PC-IMG-001 | W5 is a single-image editing and inspection capability owned by AXORA Studio in the suite direction. Multi-file batch editing remains AXORA Tools' Batch Image capability. | R-IMG-001, R-IMG-035 |
| PC-IMG-002 | Standard editing executes through app-controlled local processing without a cloud-service dependency. Clipboard and user-selected synced folders remain OS/user-controlled destinations. | R-IMG-002 |
| PC-IMG-003 | The decoded canonical source is not modified in place. Edits are represented as state. | R-IMG-003 |
| PC-IMG-004 | The viewport provides pan, bounded zoom, fit-to-view, actual pixels, and a transparency-aware background. | R-IMG-024, R-IMG-033 |
| PC-IMG-005 | Core geometry includes constrained crop, 90-degree rotation, and horizontal/vertical flip. Deskew is conditional on transform proof. | R-IMG-010, R-IMG-011, R-IMG-012 |
| PC-IMG-006 | Parametric adjustments include exposure/brightness, contrast, saturation, temperature, highlights, shadows, sharpness, and blur with defined bounds and formulas. | R-IMG-013 |
| PC-IMG-007 | Presets include document B&W, grayscale, sepia, invert, warm, and cool only after their exact transforms are specified. | R-IMG-013, R-IMG-014 |
| PC-IMG-008 | Inspection includes histogram, metadata display, and before/after comparison without mutating the source. | R-IMG-024, R-IMG-025 |
| PC-IMG-009 | Undo/redo uses `MaxUndoDepth = 50`: the current state plus at most 50 prior committed states. It coalesces one gesture into one entry and clears redo after a new edit. | R-IMG-017, R-IMG-018 |
| PC-IMG-010 | Preview work is asynchronous and latest-revision-wins. An older render may never replace a newer requested revision. | R-IMG-019, R-IMG-020, R-IMG-022 |
| PC-IMG-011 | Oversized input is rejected with a structured error before full-resolution allocation where header inspection permits. W5 core never silently downsamples an oversized import. | R-IMG-004, R-IMG-005, R-IMG-006, R-IMG-007 |
| PC-IMG-012 | The same logical state and coordinate mapping drive preview and full-resolution export. | R-IMG-010, R-IMG-011, R-IMG-016 |
| PC-IMG-013 | Alpha-preserving formats retain alpha where the proven encoder supports it; JPEG uses an explicit matte, default white. | R-IMG-014, R-IMG-015 |
| PC-IMG-014 | Format support is not advertised until W5-P2 proves actual decode, encode, metadata, alpha, and frame behavior. | R-IMG-008, R-IMG-009, R-IMG-036 |
| PC-IMG-015 | Export distinguishes new-file creation from replacement and never silently overwrites the source. | R-IMG-026, R-IMG-027, R-IMG-028 |
| PC-IMG-016 | Export metadata policy is explicit: PreserveBestEffort, StripSensitive, or StripAllSupported. | R-IMG-025 |
| PC-IMG-017 | Decode, processing, and export failures return structured, user-presentable results without crashing the host. | R-IMG-007, R-IMG-028 |
| PC-IMG-018 | Cancellation distinguishes a request from confirmed termination; native calls that cannot cooperate are contained and stale results are discarded. | R-IMG-020, R-IMG-021 |
| PC-IMG-019 | Heavy raster work runs away from the UI thread; presentation state is mutated on the UI dispatcher. | R-IMG-019, R-IMG-022 |
| PC-IMG-020 | W5 voice commands are limited to deterministic, reversible canvas/history actions. Voice never approves a write or overwrite. | R-IMG-029 |
| PC-IMG-021 | W5 may make only named, minimal additive integration changes and must preserve prior behavior. | R-IMG-030 |
| PC-IMG-022 | Axora-Desktop-MaterialUI remains a hard zero-diff boundary. | R-IMG-031 |
| PC-IMG-023 | The current authoritative pre-W5 QA manifest must complete with every registered mandatory group validly dispositioned and no failure, block, missing/duplicate/unknown group, or failed assertion. Environment-unavailable and policy-skipped groups remain justified non-pass evidence; counts are recorded for the tested commit, not frozen as a product requirement. W5 groups and UI/manual/environment evidence are tracked separately. | R-IMG-032 |
| PC-IMG-024 | ViewModel tests are not a substitute for binding, interaction, accessibility, DPI, resize, focus, and visual validation. | R-IMG-033 |
| PC-IMG-025 | Memory, latency, and frame-rate values are measured engineering targets tied to a fixture, backend, hardware, and sample method. | R-IMG-006, R-IMG-023, R-IMG-034 |
| PC-IMG-026 | W5 core adds no Win2D, SVG, AI-model, cloud-generation, segmentation, or super-resolution dependency. Optional model-driven image capabilities belong to a separately admitted future AXORA Mind workstream, not W5 core. | R-IMG-035 |
| PC-IMG-027 | W5-P2 is a mandatory codec and feasibility proof before final format commitments or editor implementation. | R-IMG-008, R-IMG-036 |

---

## 4. Core functional scope

### 4.1 Canvas and viewport

- Load one image document.
- Pan by pointer, mouse, keyboard where accessible, and supported touch input.
- Zoom within implementation-defined safe bounds.
- Fit to view and actual-pixel view.
- Checkerboard, light, and dark presentation backgrounds.
- Pixel grid may appear at high zoom if it remains accurate and accessible.
- Zoom to selection is optional and must not block core delivery.

### 4.2 Geometry

- Free and constrained crop ratios: 1:1, 4:3, 16:9, 3:2, 9:16, and A-series paper ratio where defined.
- 90-degree clockwise and counter-clockwise rotation.
- Horizontal and vertical flip.
- Fine deskew is a conditional W5 capability. It enters core only after its bounds, edge policy, crop interaction, resampling, and preview/export parity are proven.

### 4.3 Parametric adjustments and presets

- Exposure/brightness, contrast, saturation, temperature, highlights, and shadows.
- Sharpness and blur.
- Document B&W, grayscale, sepia, invert, warm, and cool presets.

W5-A must define the exact numeric domains, clamping, formulas, processing order, edge handling, and preset composition. Marketing names do not substitute for transform specifications.

### 4.4 Inspection

- RGB/luminance histogram derived from the currently displayed revision.
- Source metadata inspection with unsupported fields clearly identified.
- Hold-to-compare and/or split before/after comparison.

### 4.5 Export

- Explicit file picker or destination selection.
- JPEG quality control is deferred from the W5-P1 core acceptance set. W5-P2 records encoder behavior; a later stage may add a user-facing control only through an explicit requirement/rule/evidence amendment.
- Explicit metadata policy.
- Explicit alpha/matte behavior.
- Copy-to-clipboard is an optional follow-up, not a W5 core acceptance requirement, unless a later stage adds an explicit requirement/rule/evidence path.
- No automatic upload or app-controlled cloud transfer.

---

## 5. Format status before W5-P2

The table below is a planning classification, not a support claim.

| Format | W5-P1 classification | Intended role | Commitment before W5-P2 |
|---|---|---|---|
| PNG | Candidate Core | Read/write, lossless, alpha | Not yet advertised as proven |
| JPEG | Candidate Core | Read/write, lossy, no alpha | Not yet advertised as proven |
| WebP | Candidate Core | Read/write, lossy/lossless, possible alpha | Not yet advertised as proven |
| BMP | Candidate Core | Read/write baseline raster | Not yet advertised as proven |
| TIFF | Conditional | Single-page read/write if codec behavior is acceptable | No LZW or metadata promise |
| GIF | Read-Only Candidate | First frame only | Animation remains out of scope |
| ICO | Read-Only Candidate | Deterministically selected embedded image | Selection rule requires proof |
| HEIC/HEIF | Conditional | Host-codec-dependent read only | Never a universal guarantee |
| SVG | Deferred | None in W5 core | No parser/rasterizer dependency exists |

W5-P2 must classify every row as Core Supported, Read Only, Conditional, Deferred, or Unsupported using recorded fixtures and evidence.

---

## 6. Safety and resource contract

### 6.1 Oversized-image policy

The provisional hard ceilings are:

- maximum width or height: 16,384 pixels;
- maximum decoded area: 100,000,000 pixels.

If either ceiling is exceeded, W5 rejects full-resolution import with a structured resource-limit result. It does not silently decode and downscale.

These values are defense-in-depth ceilings, not a complete decompression-bomb guarantee. Validation must also use checked arithmetic, container/frame inspection where available, pixel format, row stride, concurrent buffers, codec behavior, and an implementation-time resource-admission estimate. W5-P2 may recommend lower ceilings; raising them requires a separate review.

### 6.2 Live-buffer topology

Planning permits these buffer classes:

1. immutable canonical source representation;
2. one bounded working proxy;
3. current presented preview;
4. at most the processing buffers required by one active preview revision;
5. full-resolution export intermediates with explicit ownership;
6. encoder stream and same-directory staged file.

History stores state, not full-resolution raster snapshots. Old previews and abandoned revisions must be disposed. No universal 150 MB or 250 MB promise is made.

### 6.3 History policy

- Default `MaxUndoDepth`: 50 prior committed states in addition to the current state, allowing at most 50 Undo transitions.
- Intermediate slider or pointer values update preview state but do not each enter history.
- Gesture completion commits one state.
- Undo moves a committed state to redo.
- A new committed edit after undo clears the redo branch.
- When capacity is exceeded, the oldest prior committed state is evicted.
- Opening or closing a document clears both stacks.
- If future state objects become materially larger, the capacity must be reviewed rather than silently storing raster snapshots.

---

## 7. Export and metadata policy

### 7.1 Destination behavior

New destination and existing destination replacement are different operations.

For both, W5 should encode to an AXORA-owned uniquely named sibling staging file where the filesystem permits, close and flush it, validate the output, and then perform the destination operation.

- A new destination is created only after a collision recheck.
- Replacing an existing destination requires explicit confirmation.
- Source-equals-destination always requires explicit source-replacement confirmation.
- Voice input cannot provide either confirmation.
- Temporary-file write plus rename/replace is described as staged same-directory commit behavior, not universally atomic behavior.
- Disk-full, permission, collision, validation, encoder, and cancellation-before-publication failures must preserve the original destination and trigger best-effort cleanup of AXORA-owned staging files. Once destination publication begins, cancellation is deferred until that filesystem operation resolves; W5 reports the observed final state and never reports `CancelConfirmed` unless destination preservation is known.
- Process interruption may leave an AXORA-owned staging artifact; recovery and cleanup limitations must be documented.

### 7.2 Metadata policies

| Policy | Required behavior |
|---|---|
| PreserveBestEffort | Copy only metadata fields supported and validated for the source/destination codec. Orientation is reconciled with rendered pixels. Perfect round-trip is not promised. |
| StripSensitive | Omit the entire GPS IFD, known location fields, camera owner/body/lens serial identifiers, ImageUniqueID, creator/contact fields selected by the implementation contract, and maker notes that cannot be safely inspected. This does not claim removal of every possible PII field. |
| StripAllSupported | Encode a fresh destination container without intentionally copying source metadata except technical fields required by the format/encoder. |

The export contract must receive the selected metadata policy and captured source metadata. Metadata inspection and export cannot be disconnected paths.

---

## 8. Color and alpha contract

- W5 core targets a documented 8-bit sRGB working representation unless W5-P2 proves a broader color-managed path.
- No wide-gamut or colorimetric-fidelity claim is made before ICC behavior is proven.
- Color operations preserve alpha values.
- Resampling and convolution must use an alpha-safe policy that avoids dark fringes.
- PNG and WebP preserve alpha only where W5-P2 proves the chosen encoder behavior.
- JPEG has no alpha; transparent pixels are composited against a user-visible matte, default #FFFFFF.
- BMP and TIFF alpha behavior remains conditional on codec proof.

---

## 9. Voice contract

Candidate Safe commands are:

- zoom in;
- zoom out;
- fit to view;
- actual pixels;
- reset zoom where unambiguous;
- undo;
- redo;
- before/after toggle if deterministic.

Aliases must be checked against the existing router for ambiguity. Opening an export dialog may be considered separately only if it performs no write. Voice cannot select a replacement decision, confirm overwrite, replace the source, or bypass the file picker.

W5 does not broaden or repair W4 during core implementation without separate authorization.

---

## 10. Explicit exclusions

W5 core excludes:

- cloud image-generation APIs;
- Stable Diffusion, FLUX, or other generative image models;
- AI background removal or segmentation;
- AI super-resolution;
- Photoshop-style layers and vector authoring;
- animation authoring or video timelines;
- multi-file batch editing;
- SVG rasterization;
- silent source overwrite;
- new Win2D or other rendering dependencies before a separately approved proof;
- Python or Conda runtime dependencies.

The user has identified AI Image Studio as a future optional heavy capability in AXORA Mind and the modular capability/model manager as core infrastructure. Those are future workstreams, not permission to expand W5 core.

---

## 11. Strategic context recorded for future planning

These notes are non-normative for W5 implementation but are intentionally retained for future roadmap work:

- High-value / likely core: Voice and Smart Dictation, Scholar Kit, Integrity/Plagiarism Center.
- High-value / optional heavy: AI Image Studio.
- Infrastructure: Modular Model/Capability Manager.
- Later strategic: Recovery, migration, and driver backup.
- Project/ZIP analysis remains an unassigned future candidate subject to the AXORA problem-first law; this note does not place it in W5 or commit it to another phase.
- System Recovery workstream: SR-01 architecture, SR-02 software/app snapshots, SR-03 application restore, SR-04 driver backup and DISM restore, SR-05 environment/SDK restore, SR-06 recovery planner, SR-07 new-PC migration, and SR-08 recovery verification.

The modular capability manager is a product architecture capability, not merely a download implementation detail.

---

## 12. Integration and preservation

W5 may require minimal additive changes to shell, DI, settings, command-palette, voice-registration, shared-raster, validation, and test-runner integration surfaces. The Architecture's existing-host allowlist is a bounded planning proposal, not permission to implement W5 in the legacy host or copy that host into AXORA Studio. A future authorized Studio stage must approve its own exact integration paths; any additional existing file requires an explicit scope amendment.

The preservation requirement is behavioral compatibility, not a claim that no earlier file can ever be touched. MaterialUI remains zero-diff.

W5-P1 modifies planning documents only. It does not authorize production code, XAML, tests, project files, packages, global.json, or environment changes.

### 12.1 Pre-existing findings / deferred debt boundary

Architecture §17 owns the detailed classification. In summary:

- pre-implementation gates: preserve the verified R4 SDK-selection evidence (verification complete, Git checkpoint not yet committed at W5-RC1), complete a separately authorized W5-P2, satisfy the applicable suite foundation/Studio readiness/V0 gates, and approve the shared ownership and migration boundary before W5 source implementation;
- W5 integration concerns: duplicated navigation/palette definitions, broad global exception handling, incomplete recognized-text routing, voice/settings drift, Batch Image weaknesses, and incomplete metadata round-trip capability;
- non-blocking deferred debt: possible W4/DI double-disposal, the absent confirmation broker, fire-and-forget speech, existing MVVM warning debt, and unrelated command/navigation cleanup.

These findings are not permission to repair unrelated behavior during W5. They are either gates, explicit integration risks, or separately authorized future work.

---

## 13. Planned execution gates

1. W5-P1 — planning reconciliation.
2. W5-P2 — codec and feasibility proof.
3. W5-A — shared raster domain, ownership, validation, and safety.
4. W5-B — non-destructive edit state, bounded history, and preview scheduling.
5. W5-C — full-resolution render, export, and metadata.
6. W5-D — WinUI page, ViewModel, navigation, and settings integration.
7. W5-E — safe W4 voice interoperability.
8. W5-F — verification, performance observations, accessibility, and regression.

No later stage begins without explicit authorization.
