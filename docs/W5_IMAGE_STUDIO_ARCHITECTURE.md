# Phase W5 Technical Architecture: AXORA Image Studio

**Capability ID**: W5-IMG  
**Official Phase**: W5 — Image Studio  
**Status**: W5-P1 ARCHITECTURE; W5-RC1 POST-P3A BASELINE RECONCILED — USER REVIEW REQUIRED  
**Repository**: D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP  
**Canonical Remote**: https://github.com/rajghosh06-dev/AXORA-DESKTOP  
**Target Solution**: Axora-Desktop-WinUI\Axora.Desktop.sln  
**Protected Baseline**: 75ca36ad12f9c64fc5ff88b549d1aece313353c2  

---

## 1. Architecture decisions

1. W5 is a single-document editing session owned by AXORA Studio in the suite direction, not another batch processor; Batch Image remains with AXORA Tools. The legacy `Axora.Desktop` host remains a reference until separately authorized migration.
2. The canonical decoded source is owned by the document session and is not publicly mutable.
3. Editing is represented by immutable state and bounded committed history.
4. Preview and export use the same coordinate mapping and operation order.
5. Preview rendering is background, revisioned, and latest-revision-wins.
6. Existing WIC/Skia infrastructure is reused or extracted; W5 does not create an unrelated third codec stack.
7. W5 core has no new Win2D, SVG, Python, cloud, or image-model dependency. Optional model-driven image generation/removal/super-resolution belongs to future AXORA Mind admission, not this W5 core.
8. GPU rendering is not assumed. The current image pipeline is CPU-backed.
9. Export uses staged same-directory commit behavior where feasible, not a universal atomicity claim.
10. Metadata policy is a required encoder input.
11. Determinism means a documented CPU reference path and test tolerances. Cross-codec, cross-driver, or cross-backend bit identity is not promised.
12. Environment-dependent capabilities are reported as observations, not universal support.

---

## 2. Existing infrastructure and reuse boundary

### 2.1 Authoritative current state

Relevant existing abstractions include:

- IConversionEngine and IConversionOrchestrator;
- ConversionJob, ConversionProfile, ConversionResult, and EngineResourceProfile;
- ConversionOutputValidator;
- WicImageConversionEngine;
- BatchImageProcessorService and BatchImageViewModel;
- WindowsPdfPageRasterizer, RasterImageDocumentExtractorEngine, TiffDocumentExtractorEngine, and WindowsMediaOcrEngine.

There is no dedicated IWicImageConversionEngine contract in the current source. WicImageConversionEngine implements the general IConversionEngine contract. W5 planning must not invent that existing interface.

Current WicImageConversionEngine behavior is approximately:

- Skia-first decode for PNG, JPEG, WebP, and BMP;
- WIC fallback and TIFF handling;
- Skia encoding for JPEG, PNG, and WebP;
- WIC encoding for BMP and TIFF;
- CPU-backed SKBitmap and SKCanvas processing;
- no generic metadata round-trip pipeline;
- no explicit TIFF LZW selection;
- staged file handling whose replacement semantics are not universally atomic.

### 2.2 Shared-raster strategy

W5-P2 must decide which primitives can be extracted without changing behavior. The intended W5-A direction is one shared raster codec boundary used by interactive W5 code and, only after separate regression proof, adaptable by existing converter/batch consumers.

The shared boundary should provide:

- header/container inspection;
- checked dimension and frame metadata;
- structured decode results;
- canonical pixel ownership;
- proxy creation;
- explicit encode capabilities;
- metadata capability reporting;
- per-format alpha/frame behavior;
- structured failures and cancellation observations.

The interactive session must not directly reuse BatchImageProcessorService orchestration, naming, parallelism, or overwrite behavior. Reuse codec primitives, not unsafe orchestration.

---

## 3. Proposed component boundaries

Names remain planning proposals until W5-A.

### 3.1 Presentation

- ImageStudioPage: viewport, tool chrome, crop interaction, comparison affordance, metadata panel, and accessible status/error presentation.
- ImageStudioViewModel: commands and presentation state only; no direct SKBitmap/WIC manipulation.
- ImageViewportController: presentation transforms, pointer/keyboard interaction, and source-to-screen mapping. It does not own the source raster.

### 3.2 Session and scheduling

- IImageStudioSession: current document, committed state, history, lifecycle, and export coordination.
- IImagePreviewScheduler: revision allocation, background dispatch, cancellation request, stale-result suppression, and result publication.
- IImageHistory: bounded committed states and redo behavior.

### 3.3 Shared processing

- ISharedRasterCodec: inspected decode/encode capability over existing WIC/Skia primitives.
- IImageTransformPipeline: one authoritative preview/export transform order.
- IImageMetadataPolicyService: inspection, policy projection, and codec-specific write capability.
- IImageExportCoordinator: destination decisions, staging, validation, commit, and cleanup.

An interface is introduced only when it creates a tested ownership or substitution boundary. W5 must not add one interface per small operation merely to appear layered.

---

## 4. Ownership model

### 4.1 Image document

The document session owns:

- a `DocumentSessionId` that changes on every load/close boundary;
- source file identity and non-secret display metadata;
- immutable canonical source pixels or a private decoder-backed source handle;
- one optional working proxy;
- captured metadata in a codec-neutral representation;
- current committed edit state;
- monotonic requested revision;
- current published preview;
- owned disposable/native handles.

Mutable native raster objects must not be exposed as public settable properties. Borrowed views cannot outlive the owning session. Close/dispose cancels requests, rejects late publications, disposes preview/proxy/source resources in ownership order, and then clears history.

### 4.2 State and revisions

ImageEditState is an immutable record containing only values needed to reproduce the edit:

- discrete rotation;
- flip flags;
- optional deskew angle;
- normalized crop;
- adjustment values;
- preset/effect selection;
- export-independent working options.

Revision identity has three independent parts:

- `DocumentSessionId` changes on load/close;
- `CommittedRevision` increases on commit, Undo, Redo, and Reset and is never reused;
- `PreviewRequestId` increases for every transient or committed render request.

Every preview request captures:

- document session ID;
- committed revision;
- preview request ID;
- immutable state snapshot;
- target proxy/viewport dimensions;
- cancellation token.

Only a result matching the active session, latest preview request, and still-current state snapshot may be published.

### 4.3 Structured results

Load, preview, and export return structured outcomes rather than Boolean success:

- Success;
- CancelRequested;
- CancelConfirmed;
- Superseded;
- UnsupportedFormat;
- EnvironmentCodecUnavailable;
- InvalidContainer;
- CorruptData;
- DimensionLimitExceeded;
- ResourceAdmissionRejected;
- PermissionDenied;
- DestinationCollision;
- SourceReplacementConfirmationRequired;
- EncoderFailure;
- ValidationFailure;
- DestinationStateUnknown when the filesystem postcondition cannot be established safely;
- UnexpectedFailure with redacted diagnostics.

User-facing text is mapped separately from diagnostic data.

---

## 5. Coordinate systems

### 5.1 Container space

Encoded frames and metadata as stored in the file. EXIF orientation is interpreted here.

### 5.2 Source space

Canonical orientation-normalized pixels. Origin is top-left; X increases right; Y increases down. Pixel bounds are half-open: [0, width) by [0, height). The source is immutable for the session.

### 5.3 Document/edit space

The extent after discrete rotation, flip, and optional deskew, before crop. Crop is stored as finite normalized values X, Y, Width, Height in [0, 1].

Crop conversion uses:

- left = floor(X times document width);
- top = floor(Y times document height);
- right = ceiling((X + Width) times document width);
- bottom = ceiling((Y + Height) times document height);
- all edges clamped to the document extent;
- empty or non-finite rectangles rejected.

Aspect-locked interaction uses double precision. The final integer rectangle may differ by at most one pixel from the requested ratio because of edge rounding.

If quarter-turn rotation, either flip, or deskew changes while a non-full crop exists, the crop resets to the full new post-geometry extent as part of the same undoable committed transaction. W5 does not silently reinterpret or approximately reproject an old normalized crop through a changed geometry transform.

### 5.4 Proxy space

The document/edit space scaled to a bounded proxy. Mapping is derived from actual proxy dimensions, not an assumed uniform constant. Normalized crop state is shared; it is not independently rounded and stored for the proxy.

### 5.5 Viewport space

Presentation-only pan/zoom transform from rendered preview to WinUI coordinates. Viewport pan, zoom, checkerboard, selection handles, and pixel grid never change ImageEditState.

---

## 6. Authoritative processing order

Preview and export use this logical order:

1. inspect container, frame, dimensions, and format;
2. decode selected frame and normalize EXIF orientation into source space;
3. apply 90-degree rotation;
4. apply horizontal and vertical flips in document space;
5. apply deskew, if the deskew capability passed its proof gate;
6. clamp and apply crop in the post-geometry document extent;
7. resolve the selected preset into its defined base effect;
8. apply exposure, temperature, highlights, shadows, contrast, and saturation;
9. apply the selected grayscale, sepia, invert, or document-B&W terminal effect;
10. apply blur;
11. apply unsharp-mask sharpness;
12. scale to the requested preview or export size;
13. preserve alpha or apply the selected matte;
14. encode pixels and apply the selected metadata policy.

Optimized implementations may fuse passes only if deterministic reference fixtures remain within the documented tolerance and alpha behavior is unchanged.

### 6.1 Adjustment reference semantics

W5-A must implement and test a CPU reference specification before any optimized path. Planning defaults are:

- channel calculations use normalized 0 to 1 values and clamp after each defined pass;
- exposure value E in [-100, 100] uses multiplier 2 raised to E/100;
- contrast value C uses (channel - 0.5) times (1 + C/100) + 0.5;
- saturation value S uses luma + (channel - luma) times (1 + S/100);
- luma uses 0.299R + 0.587G + 0.114B in the W5 core sRGB code-value working space;
- document B&W uses that luma with default threshold 0.5;
- grayscale writes luma to R, G, and B;
- invert writes 1 minus channel;
- sepia uses the documented standard 3 by 3 coefficients and clamps;
- warm/cool presets are fixed, versioned parameter/effect bundles rather than undocumented artistic filters;
- brightness (if presented separately from exposure), temperature, highlight, shadow, blur-kernel, edge-mode, and unsharp-mask formulas must be frozen in the W5-A contract before implementation acceptance.

The last item is an explicit design gate: those operations remain in product scope, but implementation cannot begin with unspecified pixel mathematics.

---

## 7. History and gesture model

- `MaxUndoDepth` is 50 prior committed states plus the current state, allowing at most 50 Undo transitions.
- The initial state is the history root and is not counted as an edit; when capacity is exceeded, the oldest prior state is evicted.
- Begin gesture captures the pre-gesture committed state.
- Intermediate pointer/keyboard values update transient preview state and may request throttled renders.
- End gesture commits at most one new state if it differs from the prior committed state.
- Cancel gesture restores the pre-gesture state without a history entry.
- Undo/redo operates only on committed states.
- A new commit after undo clears redo.
- Reset is one committed transition when it changes state.
- History never stores full-resolution raster snapshots.

---

## 8. Preview scheduling and threading

### 8.1 Latest-revision-wins

1. A state/viewport request captures the current `DocumentSessionId` and `CommittedRevision`, then obtains the next `PreviewRequestId`.
2. The previous request receives cancellation request where possible.
3. Background processing captures only immutable inputs.
4. Completion first checks session ID, preview request ID, committed state identity, and session lifetime.
5. A stale or closed-session result is disposed and reported as Superseded; it is never published.
6. A current result is dispatched to the UI thread for presentation-state mutation.

### 8.2 Cancellation semantics

CancelRequested means the caller requested cancellation. CancelConfirmed means all controlled work has stopped and owned staging/intermediate resources were cleaned up.

Native codec or encoder calls may not be cooperatively interruptible. Such calls may complete in containment, but their result is discarded if superseded or canceled. No universal 500 ms cancellation promise is made.

### 8.3 UI thread boundary

Header reading, decode, resampling, convolution, histogram calculation, full-resolution rendering, metadata parsing, and encoding do not execute synchronously on the UI thread. Observable properties, dialog state, error presentation, and preview publication are mutated through DispatcherQueue.

The architecture does not require indiscriminate Task.Run wrapping. The implementation chooses asynchronous APIs or controlled background workers and tests actual UI responsiveness.

---

## 9. Resource and buffer architecture

### 9.1 Admission

Before full decode where the codec permits, inspection validates:

- recognized container/magic;
- selected frame;
- positive dimensions;
- checked width times height and stride arithmetic;
- width and height no greater than 16,384;
- total area no greater than 100,000,000 pixels;
- frame/page policy;
- projected working-set class and current resource availability.

Exceeding a hard ceiling rejects the import. It does not trigger automatic full decode or silent downscale.

### 9.2 Live topology

The intended maximum classes of simultaneously live data are:

- one canonical source;
- one proxy;
- one currently presented preview;
- one active preview revision and its minimum required intermediates;
- during export, explicitly owned full-resolution intermediates and encoded stream/staging file.

The scheduler disposes superseded results promptly. Pipelines should process in tiles or reuse buffers where correctness permits. Numeric memory targets are established only from measured fixtures.

### 9.3 Proxy

The proxy limit is a planning default of 2,560 pixels on the longest side. W5-P2/W5-B may change it based on measured quality and memory. Proxy generation never changes the source or bypasses the hard input ceiling.

Full-resolution work is reserved for export and explicit high-detail viewport needs. A fixed 200 ms full-resolution rerender is not required; viewport tiles or another measured strategy may be selected.

---

## 10. Codec proof and frame policy

W5-P2 records, per format:

- signature/container detection;
- decode and encode;
- pixel format and alpha;
- metadata read/write;
- orientation behavior;
- animation or multipage behavior;
- WIC versus Skia path;
- Windows codec dependency;
- malformed/truncated behavior;
- representative fixture and hash;
- environmental prerequisites.

Until that proof:

- PNG, JPEG, WebP, and BMP are Candidate Core;
- TIFF is Conditional;
- GIF and ICO are Read-Only Candidates;
- HEIC/HEIF is Conditional and environment-dependent;
- SVG is Deferred.

Frame behavior must be explicit. Candidate W5 behavior is first-frame-only for GIF, deterministic best-frame selection for ICO, and first-page-only for TIFF only if W5-P2 accepts those limitations. Animated or multipage editing remains out of scope.

---

## 11. Color and alpha

- Core reference processing uses an 8-bit sRGB code-value working representation.
- ICC profile interpretation/preservation is conditional on W5-P2 evidence.
- Wide-gamut and linear-light fidelity are not W5 core guarantees.
- RGB adjustments do not change alpha.
- Resampling/convolution uses premultiplied-alpha-safe handling to avoid dark fringes, with conversion back to encoder-required representation.
- PNG/WebP alpha preservation requires fixture proof.
- JPEG applies an explicit matte, default #FFFFFF.
- BMP/TIFF alpha behavior is conditional.
- Lossless outputs use exact pixel comparison where meaningful; lossy outputs use documented tolerances, never bit identity.

---

## 12. Export architecture

### 12.1 New destination

1. Validate destination path and supported encoder.
2. Initially confirm that the destination does not exist.
3. Encode to a uniquely named AXORA sibling staging file.
4. Flush and close.
5. Validate container, dimensions, and non-empty output.
6. Recheck immediately before publication that the destination is still absent.
7. Move to the new destination using the applicable no-overwrite filesystem operation.
8. Return structured success or preserve/clean up on failure.

### 12.2 Existing destination

1. Require an explicit confirmation token bound to the canonical destination identity.
2. Source-equals-destination requires the stronger source-replacement confirmation.
3. Encode, close, and validate a sibling staging file.
4. Use the supported replacement operation and report its actual semantics.
5. Preserve the existing destination when encoding or validation fails.

Same-directory staging improves same-volume behavior but is not a universal atomic guarantee across filesystems, interruptions, locks, sync providers, or power failure.

Cancellation is confirmed only before publication when staging cleanup has completed and the destination is known unchanged. Once move/replacement publication begins, cancellation is deferred until the filesystem operation resolves. The coordinator then validates and reports the observed final state; it never reports `CancelConfirmed` when preservation is unknown, and may return `DestinationStateUnknown` when the postcondition cannot be established.

### 12.3 Temporary ownership

Staging names use an AXORA-specific prefix and random ID. Cleanup deletes only artifacts created and recorded by the current operation. Cancellation, disk-full, ACL denial, destination lock, collision race, encoder failure, validation failure, and interruption recovery are verification cases.

---

## 13. Metadata architecture

The decoder captures a codec-neutral metadata snapshot plus raw capability diagnostics. The export request always contains one MetadataPolicy:

- PreserveBestEffort;
- StripSensitive;
- StripAllSupported.

The encoder and metadata service jointly report which fields were written, omitted, unsupported, or failed. Orientation metadata is reconciled with already-oriented output pixels so it is not applied twice.

StripSensitive removes or refuses to copy the entire GPS IFD, known location fields, owner/body/lens serial identifiers, ImageUniqueID, selected creator/contact fields, and opaque maker notes that cannot be safely sanitized. It does not claim removal of every possible PII field.

StripAllSupported produces a fresh container without intentionally copying source metadata beyond required technical fields.

---

## 14. WinUI, MVVM, and accessibility

- ViewModel commands call session abstractions and expose structured status.
- Heavy operations are asynchronous commands with cancellation state.
- New observable properties must follow the current CommunityToolkit/WinRT analyzer-compatible partial-property pattern; W5 must not add to MVVMTK0045 debt.
- Every XAML binding is checked against the generated member name.
- Crop handles expose keyboard operation and accessible names/roles where WinUI permits.
- Focus order, high contrast, DPI, resize, minimum window size, pointer capture, touch/mouse/keyboard parity, and error InfoBars require runtime evidence.
- ViewModel tests are separate from UI Automation and manual visual/accessibility evidence.

---

## 15. Voice integration

W5 registers only exact, non-ambiguous, reversible commands accepted by the existing router:

- zoom in/out;
- fit to view;
- actual pixels;
- reset zoom if unambiguous;
- undo/redo;
- optional before/after toggle.

End-to-end spoken execution cannot be claimed until the existing recognized-text-to-router path is verified. W5 does not depend on the absent confirmation broker. Voice never confirms export, chooses overwrite, or replaces the source.

---

## 16. Explicit integration allowlist

The following is the original legacy-host integration allowlist. It is not authorization to implement W5 inside `Axora.Desktop` or to copy its shell into AXORA Studio. A separately authorized Studio stage must approve a stage-specific allowlist and migration boundary; any legacy-host bridge must be justified and scoped independently. Within that boundary, an implementation plan may request minimal additive edits to:

- Axora.Desktop\App.xaml.cs — DI registration;
- Axora.Desktop\ViewModels\ShellViewModel.cs — route map;
- Axora.Desktop\Views\ShellView.xaml — navigation item;
- Axora.Desktop\Views\ShellView.xaml.cs — navigation/drop integration if required;
- Axora.Desktop\Controls\CommandPaletteDialog.xaml.cs — Image Studio route only if command-palette parity is in scope;
- Axora.Desktop\Services\Contracts\IAppSettingsService.cs — additive W5 defaults only if approved;
- Axora.Desktop\Services\AppSettingsService.cs — matching persistence only if approved;
- Axora.Desktop\ViewModels\SettingsViewModel.cs — matching W5 settings only if approved;
- Axora.Desktop\Views\SettingsPage.xaml — matching controls only if approved;
- Axora.Desktop\Services\WicImageConversionEngine.cs — only an approved shared-primitive extraction/delegation that preserves `IConversionEngine` behavior;
- Axora.Desktop\Services\ConversionOutputValidator.cs — only approved additive shared validation;
- Axora.Desktop.Tests\Program.cs — manifest registration/completeness integration when W5 tests are authorized, with W5 groups explicitly identified and no frozen historical assertion count.

`IConversionEngine`, `BatchImageProcessorService`, project/package files, and voice-router internals are not initially allowlisted. New W5 files will be named in the stage plan. Any existing file outside this allowlist requires an explicit scope amendment. MaterialUI is never allowlisted.

---

## 17. Pre-existing findings and debt classification

### 17.1 Foundation and implementation gates

- At W5-RC1 (2026-09-29), the uncommitted R4 change removes `Axora-Desktop-WinUI/global.json` and places `global.json` at the repository root with SDK `9.0.300`, `rollForward: latestPatch`, and `allowPrerelease: false`. Root and WinUI commands resolved installed SDK `9.0.318`; Visual Studio/MSBuild WinUI build and bounded fresh launch/normal close succeeded. R4 verification evidence is complete for checkpoint review, but the Git checkpoint was not yet committed at this observation. W5 neither changes this policy nor reopens the superseded root-.NET-10/nested-.NET-9 diagnosis as a current blocker.
- A separately authorized W5-P2 codec proof, accepted suite foundation and relevant Studio readiness/V0 gates, and a shared ownership/migration decision block final format commitments and W5-A production implementation. A planning commit does not grant stage authorization.

### 17.2 W5 integration concerns

- duplicated navigation definitions and command-palette drift;
- broad global exception handling, requiring W5 to translate failures locally;
- incomplete recognized-text-to-router execution, limiting end-to-end W5 voice claims;
- voice/settings state drift;
- BatchImage weaknesses when extracting shared codec primitives;
- current metadata capability claims that exceed actual round-trip behavior.

### 17.3 Non-blocking deferred debt

- possible VoiceCoordinator/DI double disposal; W5 must not own or dispose W4 services;
- absent W4 confirmation broker; W5 avoids confirmation-required voice actions;
- fire-and-forget speech paths; W5 adds no new speech playback;
- existing MVVM analyzer warning debt; W5 adds no new occurrences.

These findings are not silently repaired during W5.

---

## 18. Toolchain and optional developer environments

W5 is a native C#/WinUI feature and has no Python or Conda runtime dependency. The developer reports Miniconda environments base, AI, GENERAL, and ML, with GENERAL available if a future isolated tool is justified. Any future Python use requires a problem-first justification, version discovery, deployment analysis, and explicit phase authorization. It is not part of W5-P1 or the default W5-P2 design.

---

## 19. Requirement-to-architecture traceability

| Product requirements | Architecture sections | Rules |
|---|---|---|
| PC-IMG-001 | 1–3 | R-IMG-001, R-IMG-035 |
| PC-IMG-002 | 1, 15 | R-IMG-002 |
| PC-IMG-003 | 4 | R-IMG-003 |
| PC-IMG-004 | 5.5, 14 | R-IMG-024, R-IMG-033 |
| PC-IMG-005 | 5–6 | R-IMG-010, R-IMG-011, R-IMG-012 |
| PC-IMG-006 | 6.1 | R-IMG-013 |
| PC-IMG-007 | 6.1, 11 | R-IMG-013, R-IMG-014 |
| PC-IMG-008 | 13–14 | R-IMG-024, R-IMG-025 |
| PC-IMG-009 | 7 | R-IMG-017, R-IMG-018 |
| PC-IMG-010 | 8 | R-IMG-019, R-IMG-020, R-IMG-022 |
| PC-IMG-011 | 4.3, 9 | R-IMG-004, R-IMG-005, R-IMG-006, R-IMG-007 |
| PC-IMG-012 | 5–6, 9.3 | R-IMG-010, R-IMG-011, R-IMG-016 |
| PC-IMG-013 | 11 | R-IMG-014, R-IMG-015 |
| PC-IMG-014 | 10 | R-IMG-008, R-IMG-009, R-IMG-036 |
| PC-IMG-015 | 12 | R-IMG-026, R-IMG-027, R-IMG-028 |
| PC-IMG-016 | 13 | R-IMG-025 |
| PC-IMG-017 | 4.3, 12 | R-IMG-007, R-IMG-028 |
| PC-IMG-018 | 8.1–8.2 | R-IMG-020, R-IMG-021 |
| PC-IMG-019 | 8.3 | R-IMG-019, R-IMG-022 |
| PC-IMG-020 | 15 | R-IMG-029 |
| PC-IMG-021 | 16–17 | R-IMG-030 |
| PC-IMG-022 | 16 | R-IMG-031 |
| PC-IMG-023 | 17, 20 | R-IMG-032 |
| PC-IMG-024 | 14 | R-IMG-033 |
| PC-IMG-025 | 9, 20 | R-IMG-006, R-IMG-023, R-IMG-034 |
| PC-IMG-026 | 1, 10, 18 | R-IMG-035 |
| PC-IMG-027 | 10, 17, 21 | R-IMG-008, R-IMG-036 |

---

## 20. Verification and performance evidence governance

### 20.1 Protected baseline accounting

**HISTORICAL / PRE-P3A REFERENCE:** The older W1–W4 report counted 1,726 assertion executions (1,637 pre-W4 and 89 W4). That total included weak evidence and is not a current expected-count guard. The accepted P3A run registered and dispositioned 42/42 groups: 37 Pass, 0 Fail, 3 EnvironmentNotAvailable, 2 SkippedByPolicy, 0 Blocked; missing/duplicate/unknown groups were zero, with 1,692 meaningful assertion executions passed and zero failed. Its structured result was `PASS-INCOMPLETE-PHYSICAL-COVERAGE`, not full physical coverage.

At each future W5 gate, the then-current authoritative QA manifest must give every registered mandatory pre-W5 and W5 group one valid terminal disposition, with no Fail, Blocked, missing, duplicate, unknown, or failed assertion. Environment-unavailable and policy-skipped groups need explicit reasons and do not become functional passes. Record actual group/assertion counts for the tested commit; never freeze 1,692 or 1,726 as a permanent target. Register W5 groups explicitly and retain separate deterministic/integration, environment, UI, manual, and static evidence. Literal `Assert(true)`, forced-pass branches, probe-completed-only checks, non-executing rule statements, and ViewModel-only UI checks are not functional proof; a bounded runtime observation proves only its observed path.

### 20.2 Performance evidence contract

Every benchmark records:

- hardware and OS;
- renderer/codec backend;
- image dimensions, frame count, pixel format, and fixture hash;
- operation and state;
- warm or cold run;
- sample count;
- median and P95;
- working set/private bytes and native allocations where measurable;
- success, failure, or environmental prerequisite.

Frame rate, latency, memory, GPU, and cancellation figures are targets or observations only until measured. No universal 60 FPS, 16 ms, fixed-memory, GPU, zero-cloud, or zero-regression claim is permitted.

---

## 21. Planned stages

- W5-P2 — codec and feasibility proof.
- W5-A — shared raster domain, ownership, safety, and reference mathematics.
- W5-B — edit state, `MaxUndoDepth = 50`, gestures, and preview scheduler.
- W5-C — full-resolution render, export, destination safety, and metadata.
- W5-D — WinUI page, ViewModel, navigation, settings, and UI verification hooks.
- W5-E — non-destructive W4 voice interoperability.
- W5-F — deterministic, integration, UI, performance, accessibility, and regression evidence.

No stage is authorized by this document alone.
