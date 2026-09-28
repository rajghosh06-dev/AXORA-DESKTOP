# AXORA suite product portfolio — SUITE-P0

**Status:** planning proposal for user review, 2026-09-28. **No decomposition or feature implementation is authorized by this document.** The existing `Axora.Desktop` WinUI executable remains the only current native host. This plan does not accept V0-R4, begin V0-R1A, begin W5-P2, or change the four protected W5 documents.

## 1. Evidence and decision rule

The current source and executable evidence outrank old phase labels. The SUITE-P0 authorization and the referenced product conversation supply candidate requirements, not proof that code exists. Source anchors are `Axora-Desktop-WinUI/Axora.Desktop`, `docs/V0_REMEDIATION_EXECUTION_PLAN.md`, `docs/AXORA_PRODUCT_PHILOSOPHY.md`, `docs/AXORA_FUTURE_CAPABILITY_ROADMAP.md`, and the four protected `docs/W5_IMAGE_STUDIO_*.md` plans. Here **present** means source exists, not that the capability has passed V0 remediation or release QA; **planned** means no corresponding complete WinUI feature was found. Detailed ownership and dependency proof remains a SUITE-P1 deliverable.

At planning time, `main` is `75ca36ad12f9c64fc5ff88b549d1aece313353c2`. The root `global.json` is untracked and the nested WinUI `global.json` is deleted but unstaged. The five protected planning documents are untracked; `Axora-Desktop-WinUI.zip` is an additional untracked archive of unknown provenance. This is the reported, **unaccepted** V0-R4 state; preserve it. The prior build and bounded launch/close succeeded under SDK 9.0.318, but the old console suite had no final summary after 1,685 passing assertion lines and was interrupted. Neither that partial run nor historical 1,726/1,726 claims can serve as current acceptance evidence.

## 2. Umbrella vision and the User Problem First law

AXORA is a local-first, offline-capable, modular and user-controlled Windows productivity brand. Its products answer different user questions without requiring a universal process or an AI model to be running:

| Product | User question | Promise |
|---|---|---|
| AXORA Studio | What am I creating, studying, analyzing, or submitting? | Human-facing document, study, image-editing and project-review work. |
| AXORA Tools | What must I convert, protect, transfer, inspect, or recover? | Explicit file, device and system utility operations. |
| AXORA Mind | Which model or provider should help me converse, reason, or generate? | Optional local/cloud AI with visible provider and data boundaries. |

Before **any** new capability enters an implementation roadmap, its owner must record: the concrete problem and user; estimated frequency (or a research hypothesis when unmeasured); current workaround and pain removed; whether its core works locally/offline; required and optional models/dependencies; qualitative and eventually measured storage/RAM/startup cost; behavior when offline or unavailable; reproducible verification; and undo/recovery. A missing convincing answer means **defer or reject**, not “add AI.” This expands the six-point gate in `docs/AXORA_PRODUCT_PHILOSOPHY.md`; frequency, unavailability and recovery are explicit here. “Frequent/occasional/rare” below are hypotheses, not user-research findings.

## 3. App count, identities and naming

**Recommend three independently runnable WinUI applications in one repository, introduced incrementally.** Two would place provider credentials and optional model runtimes inside either the human-work or machine-utility product. Four or more would split closely related workflows and increase installation, activation and data-coordination costs before demand proves the need. Current one-host coupling makes extraction nontrivial, but no repository dependency requires a permanent single process: the feature ownership seams are intelligible if shared primitives remain narrow and migration is tested. The recommendation is conditional on later package-size, startup, working-set and migration evidence; three executables do **not** automatically mean three lighter total installations. The current project is unpackaged, x64, Windows App SDK self-contained and directly references ONNX Runtime DirectML (`Axora.Desktop.csproj`); duplicated runtimes could increase total disk use.

| Name | Clarity and fit | Caveat | Recommendation |
|---|---|---|---|
| **AXORA Studio** | Easy to say; creation, learning and review fit. | Broad name needs a “Study • Create • Review” subtitle. | Keep. |
| **AXORA Tools** | Immediately signals practical utilities. | Generic, but clear with “Convert • Protect • Transfer • Recover.” | Keep. |
| **AXORA Mind** | Short and memorable for chat/models/generation. | Abstract; always label “AI Chat & Models” in discovery/UI. | Keep provisionally. |

This is not trademark clearance. Keep historical `W*` and `V0-R*` identifiers in their documents, but assign stable capability IDs independent of owner and execution order: `CAP-SCHOLAR`, `CAP-IMAGE-EDIT`, `CAP-IMAGE-BATCH`, `CAP-PROJECT-INTEGRITY`, `CAP-FILE-INTEGRITY`, `CAP-AI-CHAT`, `CAP-MODELS`, `CAP-RECOVERY`. The owner is metadata, not part of the stable ID; phase names remain scheduling metadata.

## 4. Complete capability ownership matrix

Legend: `Present` = code/surface found, not accepted; `Partial` = narrower or known-defective code; `Plan` = product concept only; `Dormant` = source without a reachable workflow. Source abbreviations: `V` = `Axora.Desktop/Views`, `VM` = `Axora.Desktop/ViewModels`, `S` = `Axora.Desktop/Services`, `H` = `Axora.Desktop/Helpers`, all under `Axora-Desktop-WinUI`; `D` = `docs`; `P0` = this authorization. `None` under Network means no network is required for the proposed core, not a packet-capture assertion. Storage `small/user/index/large` is qualitative. Sensitivity `H` includes privacy or destructive-operation risk; `M` requires guarded I/O; `L` is lower risk. Migration `L/M/H` is relative source/data/coupling complexity. `Core` in priority means an existing feature to retain, **not** permission to migrate it now.

| Capability | Current state | Current source area | Future app | Shared dependency | Network requirement | Optional model requirement | Storage impact | Security/destructive sensitivity | Migration complexity | Roadmap priority | Reason for ownership |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Shell, Dashboard, theme and telemetry | Present; mixed host | `App.xaml.cs`, `MainWindow.cs`, `V/DashboardPage`, `S/ThemeService`, `S/SystemTelemetry` | Each app; hardware diagnostics in Tools | UI tokens only after proof | None | None | small | L | H | Foundation | Each executable owns its own shell; machine diagnostics fit Tools. |
| Notifications and tray | Present; one host | `S/NotificationService`, `S/TrayService`, `App.xaml.cs` | Each app as needed | Small conventions/UI primitive | None | None | small | M | M | Foundation | No mandatory suite daemon or shared notification state. |
| Command palette/navigation | Partial; V0-NAV-001 | `VM/ShellViewModel`, `Controls/CommandPaletteDialog`, `MainWindow.cs` | Each app | None | None | None | small | M | H | Repair gate | Routes and commands belong to the owning product. |
| Settings | Partial; one mixed JSON | `S/AppSettingsService`, `V/SettingsPage` | Each app; minimal suite preference by decision | Schema/publication primitive after proof | None | None | small | H | H | Repair gate | Separate owners, no giant global settings object. |
| Resume editor and PDF export | Present | `V/ResumeStudio*`, `VM/ResumeStudioViewModel`, `S/ResumePdfCompilerService` | Studio | Safe publication if genuinely reused | None | None | user | M | M | Core | Creation and application documents. |
| Resume ATS assistance and local resumes | Present | `S/AtsOptimizerService`, `H/ResumeStorageHelper` | Studio | None | None | None | user | M | M | Core | Applicant workflow; user documents stay Studio-owned. |
| Flashcards, review and export | Present; seeded/in-memory decks | `V/FlashcardsPage`, `VM/FlashcardsViewModel`, `Models/Flashcard*` | Studio | None | None | None | small/user | M | M | Core/repair | Learning workflow; persistence is not proven. |
| Scholar import, document library and scanner | Present | `V/ScholarKitPage`, `VM/ScholarKitViewModel`, `S/ScholarLibraryService`, `S/WiaScannerService` | Studio | Picker wrapper only if reused | None | None | user/index | H | H | Core/repair | Personal study corpus and acquisition. |
| PDF/DOCX/text/HTML/CSV/image/TIFF extraction | Present in Scholar | `S/*ExtractorEngine`, `S/WinRtOcrService` | Studio | Narrow codec/extraction helpers only after second use | None | Optional OCR language capability | index | M | M | Core | Turns study material into reviewable text; not a general converter host. |
| Scholar normalization and passage chunking | Present | `S/*Normalization*`, `S/*Chunk*` | Studio | None | None | None | index | M | M | Core | Scholar-specific text semantics. |
| Scholar embeddings, indexes and search | Present; V0-W3-001/002 | `S/DirectMlEmbeddingEngine`, `S/ScholarIndexService`, `S/ScholarSearchService` | Studio | Model capability descriptor only | None | Optional embedding model; lexical fallback | index/model | H | H | Core/repair | Offline knowledge retrieval must not require Mind. |
| Scholar document Q&A | Present as retrieved context, not general AI chat | `S/DocumentChatService` | Studio | None | None | Optional embedding | index | M | M | Core | Answers grounded in the local Scholar corpus; do not relabel as Mind chat. |
| Extractive Study Synthesis | Present; wording needs V0-W3F-001 correction | `S/ScholarSynthesisEngine`, `S/NullScholarSlmModelDriver` | Studio | None | None | Future optional model only | index | M | M | Core/wording | Summary/concept/quiz/comparison from study material; not proven generative LLM. |
| Dictation, read-aloud and safe voice navigation | Partial; V0-W4-001/002 | `S/VoiceCoordinator`, `S/VoiceTranscriberService`, `S/SpeechSynthesisService`, `S/VoiceCommandRouter` | Studio | OS/audio adapter only if reused | None | Optional future speech models | small/optional model | H | H | Repair gate | Productivity input/accessibility; no destructive voice approval. |
| Long-form lecture/meeting transcription, speech hygiene and contextual formatting | Historical plan; no verified complete workflow | `D/AXORA_FUTURE_CAPABILITY_ROADMAP.md` W4 | Studio if admitted | None until contract | None in local mode | Optional speech model/OS capability | audio/transcripts, potentially large | H | H | Defer/discovery | A human study/notes workflow, distinct from current bounded dictation. |
| PDF annotation service | Dormant; no verified VM consumer | `S/PdfAnnotationService` | Studio if admitted | None | None | None | user | M | L | Defer | Do not advertise a non-reachable workflow. |
| Bureaucrat/Form Studio | Plan; no WinUI page | Historical handoff, P0 | Studio | Document primitives only if reused | None | None | user | H | M | Later | Repetitive human document work; must pass problem gate. |
| Interactive single-image Image Studio | Protected W5-P1 plan only | `D/W5_IMAGE_STUDIO_*.md` | Studio | Narrow raster codec after W5-P2 proof | None | **None in core** | user | H | H | Planned/high | Edit an existing image non-destructively, distinct from batch and generation. |
| Project/ZIP/folder/Git analysis | Plan | P0, `D/AXORA_FUTURE_CAPABILITY_ROADMAP.md` | Studio | Bounded archive/path/hash primitives | Optional explicit GitHub/URL | Optional semantic layer only | user/index | H | H | Discovery/high | Human project review, not system integrity. |
| Exact, near, code-structural similarity | Plan | P0, future roadmap | Studio | Hash/token primitives only | Optional explicit source discovery | Optional semantic model only | index | H | H | Discovery/high | Reproducible overlap evidence for human judgment. |
| Attribution, license and citation audit | Plan | P0, future roadmap | Studio | None | Optional explicit link/source check | None in core | index | H | M | Discovery/high | Sources and credit are contextual academic/project work. |
| Scholarly source resolution (for example arXiv/Crossref) | Historical W8 optional plan | `D/AXORA_FUTURE_CAPABILITY_ROADMAP.md` W8 | Studio | None | Explicit external lookup | None | small/cache | H | M | Later/optional | Research and citation context belongs with Scholar/Citation Auditor, not a suite-wide background plugin. |
| Project Doctor and rubric/submission inspector | Plan | P0 | Studio | Bounded file inspection primitives | Optional explicit link validation | Optional explanation only | user/index | H | H | Discovery/high | Finds actionable project/submission defects without a magic score. |
| Secret/privacy scan and submission packager | Plan | P0 | Studio | Safe publication/hash/path primitives | None in core | None | user output | H | H | Discovery/high | Review before sharing; never delete source. |
| Universal Converter routing, queue, profiles | Present; V0-W2-001 | `V/UniversalConverterPage`, `VM/UniversalConverterViewModel`, `S/ConversionOrchestrator` | Tools | Safe publication | None | None | user output | H | H | Core/repair | File-format transformation. |
| WIC raster image conversion | Present | `S/WicImageConversionEngine` | Tools | Proven narrow raster codec candidate | None | None | user output | M | M | Core | Conversion utility, not interactive editing. |
| Markdown/text and image sequence to PDF | Present | `S/TextMarkdown*`, `S/*Pdf*` converter engines | Tools | Safe publication; PDF primitives only if reused | None | None | user output | M | M | Core | Format conversion. |
| PDF text and PDF image rendering | Present | `S/WindowsPdfRenderer*`, `S/*Pdf*` converter engines | Tools | Safe publication | None | None | user output | M | M | Core | Format extraction/rendering for export. |
| Structured CSV/JSON/XML conversion | Historical phase label; no registered engine found | W2-B2 handoff, `D/W2F_FORMAT_MATRIX.md`, `App.xaml.cs` registrations | Tools if proved/admitted | None | None | None | user output | M | M | Verify/defer | Do not infer shipped support from historical label. |
| Batch Image | Present; V0-BAT-001/002 | `V/BatchImagePage`, `VM/BatchImageViewModel`, `S/BatchImageProcessorService` | Tools | Proven raster codec, safe publication/process runner | None | None | user output | H | H | Core/repair | Many-file utility, separate from W5 one-image editor. |
| Compressor | Present; V0-CMP-001 | `V/CompressorPage`, `VM/CompressorViewModel`, `S/IntelligentCompressorService` | Tools | Safe publication | None | None | user output | H | M | Core/repair | File/archive size and format utility. |
| Vault encryption/decryption | Present; V0-VLT-001 | `V/VaultPage`, `VM/VaultViewModel`, `S/StreamingVaultService`, `S/TpmSecurityProfileService` | Tools | Safe publication where contract fits | None | None | user vault | **H** | H | Core/repair | Explicit protected-file operations, not academic integrity. |
| Mobile Link, P2P and QuickDrop | Partial; V0-P2P-001 | `V/MobileLinkPage`, `VM/MobileLinkViewModel`, `S/P2pSyncService`, `Controls/FloatingDropWidget` | Tools | Cryptographic/path primitives only | Explicit LAN | None | user transfer | **H** | H | Core/repair | Device/file transfer and connection management. |
| Transfer activity feed | Present, in memory | `S/DownloadManagerService` | Tools | None | Explicit LAN | None | small | M | L | Core | QuickDrop progress, not extension download management. |
| Extension install/repair/version detection | Present; V0-W15-001/002 | `S/Extension*`, `S/VersionDetector`, `S/DependencyManager` | Tools | Bounded process/trust primitives if reused | Explicit download | None | optional binaries | **H** | H | Core/repair | Executable dependency management; not Mind model management. |
| Extension Download Manager UI | Present | `V/DownloadManagerPage`, `VM/DownloadManagerViewModel` | Tools | None | Explicit download | None | optional binaries | H | M | Core | User-controlled utility extension acquisition. |
| File/system integrity checks | Plan | P0 | Tools | Hash/path primitives | None for local checks | None | manifests | H | M | Later | Verify files/backups/system artifacts, not project originality. |
| Recovery architecture and planner (SR-01, SR-06) | Strategic plan | P0, historical roadmap | Tools | None until proof | Optional catalog/update only | None | plans/snapshots | **H** | H | Later/research | High-stakes system recovery requires a separate safety contract. |
| Software/app snapshot and restore (SR-02, SR-03) | Strategic plan | P0 | Tools | Safe publication only if applicable | Optional explicit downloads | None | potentially large | **H** | H | Later/research | Restore installed application state only with reversible proof. |
| Driver backup/DISM restore (SR-04) | Strategic plan | P0 | Tools | None until proof | None for local backup | None | potentially large | **H** | H | Later/research | Privileged hardware recovery; narrow approval and verification. |
| Environment/SDK restore (SR-05) | Strategic plan | P0 | Tools | None until proof | Optional explicit installers | None | potentially large | **H** | H | Later/research | Developer-machine recovery, not Studio or Mind. |
| New-PC migration and verification (SR-07, SR-08) | Strategic plan | P0 | Tools | Hash/manifest primitives | Optional explicit transfer | None | potentially large | **H** | H | Later/research | Cross-device state preservation with explicit integrity proof. |
| Media/audio/video and deeper hardware utilities | Broad historical idea only | Historical handoff, future roadmap | Tools if admitted | None | Undecided | None | potentially large | H | H | Defer | No concrete bounded problem/codec contract yet. |
| General chat, conversations and attachments | Plan; no Mind host | P0 | Mind | Small content/attachment handoff contract only | Cloud optional | Local model optional | user history | H | H | High after foundation | Provider-neutral AI interaction, isolated from non-AI apps. |
| Cloud provider adapters and BYOK | Plan | P0 | Mind | Windows-protected secret adapter | Explicit cloud | Cloud model | small/history | **H** | H | High after foundation | Credentials and outbound user prompts need one clear owner. |
| Local LLM runtime adapters | Plan | P0 | Mind | Capability descriptor only | None for loopback runtime; install may need network | Local LLM optional | potentially large | H | H | High/optional | Model runtime should not inflate Studio/Tools base. |
| Model/Capability Manager | Plan; distinct from W1.5 | P0, `D/AXORA_MODULAR_CAPABILITY_CONTRACT.md` | Mind | Narrow descriptor/trust concepts only | Explicit optional downloads | Optional task models | potentially large | **H** | H | High/architecture | First-class ownership of model availability and storage. |
| Mind document/RAG bridge | Plan | P0 | Mind, with explicit Studio handoff | Versioned handoff format | Local or explicit cloud | Optional embedding/chat model | user/index | H | H | Later | No silent sharing of Scholar corpus. |
| Vision and embedding provider adapters | Plan | P0 | Mind | Capability descriptor only | Explicit cloud or local | Optional models | potentially large | H | M | Later | AI tasks, not core editing/conversion. |
| AI image generation and image-to-image | Plan | P0, historical future roadmap | Mind | Explicit file handoff to Studio | Explicit cloud or local | Optional heavy model | potentially large | H | H | Later/optional | Creating a new image differs from editing an existing one. |
| AI background removal and super-resolution | Plan, excluded from W5 core | P0, protected W5 contract | Mind | Explicit result handoff | Explicit cloud or local | Optional vision model | potentially large | H | H | Later/optional | Model-driven transformation is optional AI, not W5 baseline. |
| Voice prompt/read-response | Plan | P0 | Mind | OS audio convention only if reused | Follows selected provider | Optional speech model | small/optional model | H | M | Later | AI conversation modality, independent of Studio voice service. |
| Optional agents and executable tools | Idea only | P0 | Mind if admitted | Bounded process/action contracts | Depends on tool | Optional model | variable | **H** | H | Defer | Autonomous actions lack a justified, safe initial contract. |
| Explicit cross-app activation/file handoff | Plan; current links are same-process | P0 | Suite protocol, each app endpoint | Tiny versioned handoff contract | None unless user chooses it | None | temporary user data | H | M | Later | Optional interoperability must not create a mandatory broker. |

## 5. Existing and planned inventories: important corrections

The existing WinUI host has Studio-like Resume, Scholar, Flashcards and voice code; Tools-like Converter, Batch, Compressor, Vault, P2P/QuickDrop and Extension code; and a single shared shell. It does **not** contain a separate Mind host, general multi-conversation provider chat, a complete Project Integrity Lab, Form Studio, interactive W5 Image Studio, or a recovery engine. Scholar's `DocumentChatService` is retrieval/context, and `ScholarSynthesisEngine` is currently extractive; neither proves a cloud/local chatbot. `DirectMlEmbeddingEngine` belongs to Scholar's current offline path, with a lexical fallback; do not move Scholar's core into Mind. Flashcards have in-memory seeded decks, not proven durable deck persistence. `PdfAnnotationService` is registered but not a proved reachable feature. `DownloadManagerService` is the transfer feed; `DownloadManagerViewModel` is the extension UI. The historical W2-B2 “Structured Data Engine” label and older format matrix do not establish a registered structured-data converter.

The planned inventory is the remainder of the matrix: Studio's Image Studio, Form Studio and Project & Integrity Lab; Tools' file/system integrity and SR-01–SR-08 recovery family; Mind's provider chat/model center and optional generative capabilities; plus optional cross-app handoff. Historical phase names are retained as aliases only. The old future roadmap's W5 generative additions are **not** part of protected W5-P1's deterministic editing core, and its W6 “Integrity Center” must split into project/academic evidence (Studio) and file/system integrity (Tools).

## 6. Admission register for proposed features

These are product hypotheses to validate before implementation. “Offline” means the core can function without Internet **if its stated optional local dependency is already present**. A feature not yet able to meet its verification/recovery test remains in discovery, even if it sounds valuable. Each row records the User Problem First questions; this register is not implementation approval.

| Candidate | User, frequency hypothesis, workaround and pain removed | Local/offline, dependency and storage | Missing/offline behavior | Verification and undo/recovery | Decision |
|---|---|---|---|---|---|
| Interactive Image Studio | People editing scans/photos; frequent; manual editor switches and accidental original overwrite. | Local deterministic raster core; no model; user output/proxies. | Unsupported codec/action is explicit; source intact. | Pixel/metadata/reopen and UI-state tests; undo/redo plus safe export. | High after W5-P2. |
| Form Studio | Applicants/admin users; occasional repetitive forms; copy/paste across tools. | Local templates; document storage, no model in core. | Unsupported forms stay manual; no hidden submission. | Field mapping, preview/export round-trip; preserve source and editable draft. | Discovery until workflow/risk contract. |
| Project similarity & attribution | Students/educators/reviewers; assignment-cycle frequency; manual diffs/web search and false accusations. | Local hashes/tokens/corpus; optional explicit web/semantic; index/storage scale with input. | Local comparison still works; no fabricated web coverage. | Labeled exact/near/code fixtures and side-by-side provenance; delete derived index, never source. | High discovery; evidence, not verdict. |
| Project Doctor/rubric inspection | Students/maintainers; submission-cycle frequency; manual checklist and missed files. | Static local scan; optional explicit link check, no model; small report. | Unchecked rules shown as gaps, not passes. | Seeded defects/false positives; advice only, no source mutation. | High discovery. |
| Citation auditor | Researchers/students; occasional; manual bibliography reconciliation. | Local markers; optional explicit DOI/link lookup; small index. | “Not checked online” state. | Citation fixtures and source links; review/ignore finding, never auto-edit citations. | High discovery. |
| Scholarly network lookups | Researchers/students; occasional; manual source-resolution visits. | Local citation work without network; optional explicit arXiv/Crossref-style adapter and small cache. | No Internet/provider: local citation findings remain, online verification labeled absent. | Mocked provider/consent/metadata matching; revoke cache, never rewrite source. | Later optional, after privacy contract. |
| Submission packager/secret scan | Students/sharers; submission-cycle frequency; manual ZIP and accidental leaks. | Local scan/package; output can be large; no model. | Unsafe/unknown files halt or require explicit review. | Manifest/hash/reopen/secret fixtures; source unchanged, delete only new package by user choice. | High discovery after safe publication. |
| File/system Integrity Center | File custodians; occasional; manual checksums/archives. | Local hashes/validation; manifests; no model. | Unreadable files reported, never treated as verified. | Corruption/mismatch fixtures; read-only check, preserve originals. | Later after Tools safety. |
| SR-01 recovery architecture | PC owners; rare but severe; fragmented backup practices. | Local planning; potential snapshots large; no model. | Cannot promise recovery without tested restore path. | Threat model and rehearsal fixtures; non-destructive plan. | Research only. |
| SR-02 software/app snapshot | PC owners; rare/periodic; manual inventories; missed app/config state. | Local capture; potentially large, no model. | Partial snapshot clearly labeled. | Manifest, checksum, restore drill; retain old snapshot. | Later, needs restore proof. |
| SR-03 application restore | PC owners; rare/high stakes; reinstall manually. | Local artifacts or explicit downloads; potentially large. | Missing installers block a restore step safely. | Isolated restore/rollback matrix; no overwrite without confirmation. | Later, high risk. |
| SR-04 driver backup/DISM restore | PC owners; rare/high stakes; vendor tools/DISM manually. | Local backup; possibly large; privileges, no model. | No elevation/hardware match means no action. | Hardware/version match, backup/restore rehearsal and rollback. | Later, explicit high-risk approval. |
| SR-05 environment/SDK restore | Developers; occasional after migration; manual package inventories. | Local manifests/artifacts, optional explicit installers; large possible. | Missing package marked unresolved, not silently installed. | Recreate in disposable environment; retain original configuration. | Later. |
| SR-06 recovery planner | PC owners; rare but stressful; scattered notes/checklists. | Local plan, no model. | Unknown facts displayed as unknown. | Scenario rehearsal/checklist coverage; editable plan. | Later, perhaps first recovery slice. |
| SR-07 new-PC migration | PC owners; rare; ad hoc copy/reinstall. | Local or explicit transfer; potentially large; no model. | Partial migration with resumable manifest. | Source/destination manifest reconciliation; source untouched. | Later/high risk. |
| SR-08 recovery verification | PC owners; after restore; manual spot checks. | Local checks, no model. | Unverifiable items remain open. | Hash/service/launch checks with evidence; rollback guidance. | Required with any restore capability. |
| Mind provider chat/BYOK | People needing model assistance; potentially frequent; browser-switching and copied context. | Local UI/history, optional explicit cloud; keys protected; history variable. | No key/network: choose ready local model or clear unavailable state. | Mock-provider streaming/cancel/privacy tests; delete/export conversation and revoke key. | High, after host/security foundation. |
| Local LLM/model manager | Offline/privacy users; potentially frequent; separate runtime tools and opaque hardware costs. | User-selected local runtime/model; storage potentially very large. | No model: Mind shell still usable for history/settings, no fake answer. | Capability/readiness/checksum/license/resource tests; uninstall only with reviewed deletion/recovery. | High architecture, optional payload. |
| Mind document/RAG bridge | People querying a chosen document; occasional; manual excerpts. | Local or explicit cloud; optional embeddings; user/index storage. | No model/Studio: standalone attachment path or unavailable state. | Consent/provenance/grounding tests; revoke handoff and purge derived copy. | Later. |
| AI image generation/image-to-image | Creators; occasional; separate generator apps. | Explicit cloud or optional heavy local weights; large possible. | Unsupported GPU/offline cloud: no generation, no hidden download. | Prompt/provider/result provenance, cost and export checks; user can discard output. | Optional later, not Studio core. |
| AI background removal/upscaling/vision | Image users; occasional; specialist apps. | Explicit cloud or optional local model; large possible. | Core Studio edit remains available; missing AI task shown. | Reference-image quality/fallback checks; preserve original and result version. | Optional later. |
| Mind voice prompt/read response | Accessibility/conversation users; optional frequency; typing/manual read aloud. | OS voice or optional model; local/cloud follows provider. | No microphone/voice: text chat still works. | Device/permission/cancel tests; no destructive spoken approval. | Later. |
| Lecture/meeting transcription and speech cleanup | Students/meeting participants; occasional; manual notes or separate recorder/transcriber. | Local only with a proven speech capability; recordings/transcripts can be large and sensitive. | No device/model: no fake transcript; existing text notes stay usable. | Timestamp/word/error/privacy tests with consent; editable transcript and user-controlled recording deletion. | Defer until consent, resource and problem proof. |
| Optional agents/tools | Unclear target problem/frequency; manual workflows currently safer. | Potential network/process/file side effects and variable storage. | No capability: no autonomous action. | No bounded permission/recovery contract yet. | **Defer**. |
| Broad media/hardware utility expansion | Problem and usage not narrowed; existing specialist tools suffice for now. | Codec/driver footprint unknown. | Keep current Tools usable. | No verifiable acceptance contract yet. | **Defer**. |
| System-wide dictation across all apps | Cross-app user need unvalidated; OS-level focus/permission complexity. | Local speech may be possible but process boundary is unsettled. | Studio dictation remains independent. | No safe focus/undo proof yet. | **Defer**, not implied by current W4. |

## 7. Project & Integrity Lab contract (Studio)

The purpose is *reviewable evidence for learning, attribution and submission quality*, not an automated misconduct verdict. Inputs: user-selected ZIP, folder, local Git repository, explicit GitHub URL, explicit deployed URL, PDF/DOCX/Markdown/text documentation, or multiple supplied projects. Acquisition is read-only and bounded by file count, uncompressed bytes, depth, time and supported type; do not run untrusted project code, scripts, package managers, macros or websites merely to inspect it. Reject traversal, unsafe links and archive bombs; treat nested archives and Git submodules/LFS as separate opt-in scope. Remote acquisition is an explicit network action with source, destination, requested data and limits visible first. A deployed URL is not a proof of its source repository.

The default deterministic pipeline records input hashes, parser/version, included/excluded file manifest and comparison denominator. Classify generated/vendor/dependency/build content (`node_modules`, `bin`, `obj`, `dist`, `build`, `venv`, lock files, minified assets, vendored code) separately rather than silently attributing it to an author. Layers may include exact hashes, normalized text, token shingles, MinHash/LSH or SimHash candidate retrieval, normalized code tokens, language-aware AST/structure only where a trustworthy parser exists, comments, license notices and citation metadata. Candidate retrieval is **not** a match verdict; final displayed overlaps require reproducible pairwise evidence. Optional semantic similarity and external source discovery are separately labeled, user-triggered and never required for local analysis.

Report exact/near/structural overlaps, likely source candidates, already-attributed third-party material, common boilerplate and potential attribution gaps with side-by-side passages, file/line anchors, method, thresholds, excluded corpus and uncertainty. An “unmatched” share means *no match in the analyzed corpus*, not originality. Do not emit “plagiarized X%,” a guilt score or a sole-model accusation. A human reviewer resolves context, license, permitted collaboration and intent. Version and retain the report only by user choice; derived caches can be removed without altering source projects.

### Project Doctor, Citation Auditor and Submission Packager

Project Doctor checks README/setup/build declarations, dependencies, links, missing files/datasets/attribution, path portability, generated junk, debug leftovers, secret indicators, documentation/code consistency and rubric evidence as **individual findings** with source links, severity, confidence and actionable suggestions. Static inspection is the default. “Builds successfully” must never be claimed from merely finding a build script; execution of untrusted projects requires a future isolated-run contract and explicit user consent. Citation Auditor reconciles in-text markers and bibliography, with optional separately consented online validation; unreachable is not automatically invalid citation.

Submission Packager shows a complete proposed inclusion/exclusion manifest and estimated output size before creation. It highlights `.env`, credentials, user databases, personal paths, build/IDE caches, vendored dependencies and large generated files. High-risk secret findings stop default packaging until reviewed. It stages a new package, validates its manifest/ZIP, and publishes to a user-selected destination under a safe collision policy. It **never deletes or modifies the source project**, and never silently uploads a submission.

## 8. AXORA Mind product and provider contract

Mind begins as an independently runnable chat shell: conversation list, explicit local/cloud model selector, attachments, history export/deletion and clear readiness/error states. Actual inference still needs a **user-configured cloud provider or an available local model**; with neither, Mind must show setup and unavailable states, not pretend it can answer. It is not a clone of a particular vendor's UI or a hard-coded provider client. The current Scholar `DocumentChatService` remains Studio-owned; a future “Ask in Mind” bridge must pass a user-selected, previewed slice of content, not mount the entire Scholar library.

Use capability-specific contracts rather than a universal “AI provider” object: candidate interfaces `IChatProvider`, `ILocalModelProvider`, `IEmbeddingProvider`, `IVisionProvider`, `IImageGenerationProvider`, plus a small task/readiness descriptor. They should express model identity, streaming/cancellation, supported input types, content limits, errors and provider-specific costs/data handling. No provider implementation, key, download or local runtime is selected in P0. User-supplied cloud credentials belong in a Windows-protected, app-scoped secret store—not `settings.json`, logs, crash reports or handoff files. Decide Credential Locker versus user-scoped DPAPI after a threat-model and roaming/recovery review: Windows Credential Locker is supported for WinUI/desktop apps, but its desktop access/roaming properties are not a magic inter-app isolation boundary. [Microsoft Credential Locker guidance](https://learn.microsoft.com/en-us/windows/apps/develop/security/credential-locker), [Microsoft .NET DPAPI guidance](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection).

Local runtime classes to evaluate later: user-managed loopback OpenAI-compatible servers, Ollama-style service, llama.cpp-compatible service, and only then native ONNX/DirectML where technically suitable. Probe actual endpoint/model capabilities; do not assume every “OpenAI-compatible” endpoint implements the same operations or is local merely because it uses HTTP. The model/capability manager is a **first-class Mind product**, not a renamed Tools Extension Manager. A record should expose provider, ID/version/checksum, task flags (Chat, Embedding, Vision, OCR, Speech, ImageGeneration, Upscaling, Segmentation), license, installed/readiness state, source, storage path/size, context limits and measured/recommended RAM/GPU capability when available. Unknown values remain unknown. The user's 16 GB RAM/Intel Iris Xe machine is a reason to keep heavy models optional and test CPU/DirectML availability, not to promise any model size/performance without measurement.

Cloud prompts/attachments require an explicit destination and data-preview action; visibly label Local versus Cloud, estimated provider cost where available, cancellation and retention semantics. No Studio/Tools background cloud calls. Local models, cloud provider adapters and optional image/vision tasks degrade independently. Future agents/tools are deferred until explicit per-action permissions, bounded side effects, audit trail and recovery can be demonstrated. Mind never silently authorizes file deletion, encryption, installation, repair or transfer.

## 9. Studio and Tools roadmaps

**Studio:** retain and remediate Scholar, Resume, Flashcards and bounded voice; prove W5-P2 codec behavior before an interactive non-destructive Image Studio; validate Form Studio with user workflows; then build Project & Integrity Lab in vertical slices (safe local acquisition → deterministic evidence → project hygiene → citation → reviewed package → optional external discovery). Optional Mind explanation must never replace evidence. Studio's core workflows work with Mind uninstalled.

**Tools:** retain and remediate Converter, Batch Image, Compressor, Vault, Mobile Link/QuickDrop and Extension Manager. Next consider read-only file integrity checks; only after a separate threat/safety/rollback contract should SR-01–SR-08 recovery functions advance. Tools does not install a local LLM for its basic work. Voice is limited to safe navigation unless separately designed. Its high-risk file/device/system actions require explicit UI confirmation and cannot be approved by voice alone.

**Mind:** first a thin local history/UI and provider contract with honest “not configured” states; then BYOK and one tested adapter at a time; then optional local-runtime discovery/capability manager; then consented attachment/RAG bridge; only later vision/image generation and other heavy tasks. Do not use speculative model capabilities to define Studio/Tools release readiness.

## 10. Shared-core and lightweight-dependency policy

One repository is the default, but `src/Axora.Studio`, `Axora.Tools`, `Axora.Mind`, `Axora.Shared.Core`, `Axora.Shared.Platform`, `Axora.Shared.UI` and `Axora.AI.Contracts` are **candidate boundaries, not directories to create now**. Extract only after an actual second consumer and a stable contract exist. `Shared.Core` candidates: typed result/error records, path/hash validation, versioned serialization, safe single-file publication, bounded process-execution *contracts* and cancellation/diagnostic conventions. `Shared.Platform` may hold small picker/WinRT wrappers. `Shared.UI` may hold tested tokens, theme resources and tiny accessible controls. A shared raster library is conditional on W5-P2 and separate from Tools' Converter/Batch orchestration. AI task descriptors may be shared; model installs, providers, chat history and credentials remain Mind-owned.

Keep navigation, dashboards, ViewModels, settings schemas, feature repositories and resource ownership per app. Intentionally duplicate minimal host bootstrap, route registration and product-specific UX rather than forcing every app through a shared runtime. Existing `Axora.Desktop.csproj` is unpackaged and Windows App SDK self-contained, and brings ONNX Runtime DirectML into the monolith; packaging/runtime choice and real disk/working-set/startup measurements are future gates, not assumptions. [Microsoft packaging overview](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/packaging/). Studio's optional embedding and Tools' optional ImageMagick must not silently become Mind base requirements. Models, installer binaries, caches and indexes are optional/user-controlled with size, license, checksum, readiness and uninstall/recovery information shown before acquisition.

Qualitative cost expectations (to measure in SUITE-P1/P3):

| App | Core/optional footprint | Data growth | Background/network/startup boundary |
|---|---|---|---|
| Studio | WinUI, document/PDF/OCR/raster needs; optional embedding and W5 codec paths. | Resumes, Scholar documents/indexes, edited outputs and project reports grow with user input. | No P2P listener or model download; start only owned services. |
| Tools | WinUI, conversion/crypto/transfer; optional ImageMagick and later recovery components. | User outputs, Vault files, QuickDrop, extension cache and potential snapshots. | LAN listener only with explicit persistent opt-in and visible status; network downloads only on action. |
| Mind | Thin WinUI/chat base; cloud adapters and local model runtimes/weights optional. | Chat/attachments/caches; optional models may dominate disk. | No model server/download on unrelated app startup; cloud calls only by explicit prompt/action. |

## 11. Settings and data ownership; no migration yet

Proposed *conceptual* new roots are `%APPDATA%\Axora\Studio`, `%APPDATA%\Axora\Tools`, `%APPDATA%\Axora\Mind`, with large/cache/model data considered separately under `%LOCALAPPDATA%` or a user-selected path. Current code is unpackaged and uses direct file paths, not a guaranteed packaged-app data abstraction. [Microsoft app-data guidance](https://learn.microsoft.com/en-in/windows/apps/design/app-settings/store-and-retrieve-app-data). No existing file moves or schema changes are authorized here.

| Current/future store | Current evidence | Future owner | Later migration rule |
|---|---|---|---|
| Mixed `%APPDATA%\Axora\settings.json` | `S/AppSettingsService` holds theme, P2P/Download, Vault and voice fields. | Split Studio/Tools; Mind own settings; only truly suite-wide theme by decision. | Snapshot and parse/version each field; one writer per new store; retain legacy read/rollback. |
| Resumes in `Documents\Axora\Resumes` | `H/ResumeStorageHelper` | Studio | User documents remain user-owned; preserve paths/IDs and original files. |
| Scholar documents/sessions/indexes/metadata/quarantine in `%APPDATA%\Axora\Scholar` | `S/ScholarLibraryService`, `S/ScholarVectorIndexWriter` | Studio | Consistent generation migration; no dual-index writers or destructive rebuild. The library honors an `APPDATA` override while the index writer uses the special-folder API; inventory actual paths rather than assuming one root. |
| Flashcard deck/review state | Current VM/decks largely seeded and in memory. | Studio | First establish actual persistence contract; do not invent migrated data. |
| `%APPDATA%\Axora\vault_sealed.dat` and user Vault outputs | `S/StreamingVaultService`, `S/TpmSecurityProfileService` | Tools | Current key seal is user-bound; do not promise portability or naïvely copy it to another PC. Preserve ciphertext/key compatibility and require reversible decrypt/round-trip proof. |
| `%LOCALAPPDATA%\Axora\ExtensionCache` and `Extensions` | `S/ExtensionCacheService` | Tools | Preserve verified binaries; revalidate trust/version before reuse; never uninstall known-good first. |
| `Downloads\Axora_QuickDrop` and in-memory transfer feed | `S/P2pSyncService`, `S/DownloadManagerService` | Tools | User files stay put until approved; reconcile configured versus actual directory, no fictitious history migration. |
| Scholar model path under `%APPDATA%\Axora\Capabilities\Models` | `S/DirectMlEmbeddingEngine` | Studio's optional embedding; possible later Mind catalog reference | No forced Mind dependency or silent move; hash/version/space check before any copy. |
| Future Mind conversations, attachments, model state and credentials | No current Mind data | Mind | Separate history/attachment store; secrets in protected store; user-controlled export/deletion. |

Migration later must inventory actual on-disk values, create a recoverable snapshot, define schema/owner/version and test dry-run/rollback on copies. The legacy host remains a read-only migration source or sole writer for a store until cutover; **never two live writers to the same legacy data**. Documents/Downloads/user-selected outputs are not simply `%APPDATA%` contents and cannot be silently relocated.

## 12. Security, privacy and cross-app interaction boundaries

| Boundary | Contract before implementation |
|---|---|
| Mind credentials/history | Protect per-user secrets, redact logs/crashes, scope provider permissions, label Local/Cloud and retention; no plaintext settings; explicit export/delete. |
| Project inputs and external discovery | Read-only bounded acquisition; no executing untrusted ZIP/Git/site code; local evidence by default; explicit network targets/data preview and opt-in source discovery. |
| Studio↔Mind | Only user-selected text/files and named task are handed off; preview/redact before cloud inference; Studio works without Mind. |
| Studio↔Tools | User explicitly opens a copy/path in the other app; no silent Vault/P2P action; validate source identity and destination before writes. |
| Vault/P2P | Tools owns keys, permissions, peer pairing, transfers and truthful status; encrypted/verified sessions and explicit transfer approval precede claims of safety. |
| Extension/model acquisition | Tools owns executable extensions; Mind owns model packages. Reuse a vetted trust/manifest primitive, not a shared installer service. Show source, license, size, hash and rollback. |
| Recovery and voice | Privileged/destructive steps require explicit non-voice confirmation, backup/rollback and post-action verification; voice cannot silently approve them. |

Optional integration starts with versioned file/URI activation or a bounded user-initiated handoff, *after* independent hosts exist. No permanent broker, always-running suite service, shared database or large IPC framework without measured need. Handle absent target app, unsupported version, canceled/expired handoff and sensitive temporary-data cleanup. Each app starts, closes and performs its core workflows alone. Windows App SDK offers activation mechanisms, but the exact registration/deployment design depends on the later packaged/unpackaged decision. [Microsoft app lifecycle and activation overview](https://learn.microsoft.com/en-us/windows/apps/develop/launch/app-lifecycle).

## 13. Current-code coupling and extraction risk

| Source area / observed coupling | Classification | Why and extraction gate |
|---|---|---|
| `App.xaml.cs` DI/startup/shutdown mixes Scholar, Converter, Vault, voice, extension, tray and P2P; P2P autostarts from one flag. | **High-risk extraction** | Split registration and lifecycle per host only after V0-R1B-style ownership proof; avoid copying unconditional background start. |
| `ShellView.xaml(.cs)`, `ShellViewModel`, `MainWindow.cs`, palette and drop routing know all product pages. | **Moderate-to-high extraction** | Per-app typed route catalog and reachable-page tests; cross-app commands become explicit activation, not in-process navigation. |
| `AppSettingsService` mixes theme, voice, Vault/P2P/download settings in one unversioned file. | **High-risk extraction** | Field-level owner/schema map, recovery and one-writer cutover. |
| `NotificationService`, `ThemeService`, picker/dispatcher helpers. | **Should remain shared only as small contracts/UI/platform helpers** | Prove a second consumer; stateful service instance and UI thread belong to each process. |
| `VoiceCoordinator` and Flashcards/Scholar/UI settings; voice route aliases span pages. | **Moderate-to-high extraction** | Studio owns productive voice; Mind later has a separate optional voice interface; Tools only safe navigation. Repair W4 state/lifecycle before reuse. |
| Converter/Compressor/Batch publication and process handling. | **Shared primitive + high-risk product integration** | Prove safe publication and bounded process semantics; do not put whole orchestrators in Shared. |
| WIC/Skia/PDF/Scholar extraction and Batch image paths. | **Moderate; shared raster only after W5-P2** | Codec test and resource limits before extracting a common consumer library; preserve product-specific operations. |
| `ScholarLibraryService`, index writer/reader and embedding engine. | **High-risk Studio-owned extraction** | Preserve index generation and lexical fallback; Mind is optional, not a required host. |
| `P2pSyncService`, Mobile Link, QuickDrop and transfer feed. | **High-risk Tools-owned extraction** | Pairing/session truth, destination safety and file ACK before porting. |
| Vault/Tpm profile/extension install/repair. | **High-risk Tools-owned extraction** | Security/known-good state must be remediated and migration-tested, not cloned. |
| Host startup, app-specific route tables and ViewModels. | **Intentionally duplicated** | Small product-specific composition roots are clearer than shared monolithic host code. |

`Axora.Desktop.csproj` also has a `KillRunningAppBeforeBuild` target that force-stops the current executable. Later multi-host build/QA design must remove cross-app collateral effects under separate authorization; do not change it in P0. Current source auto-starts P2P on launch when settings allow it; reconcile this with the local-first product philosophy's explicit-network rule rather than claiming no background network activity today.

## 14. V0 remediation remap: retain every finding

`docs/V0_REMEDIATION_EXECUTION_PLAN.md` remains authoritative for *current-host findings and dependency ordering*, not for new app ownership. Below, `SUITE` is governance/QA; `SHARED` is a narrow library/convention plus owning consumers; `STUDIO` and `TOOLS` are product owners. There is no V0 finding that requires creating Mind. Every wave requires later separate authorization; this table is not a repair order to execute now.

| Finding | Existing wave | Future owner and disposition |
|---|---|---|
| V0-TCH-001 | R4 | SUITE SDK policy; attempted but not accepted, preserve current state. |
| V0-TEST-001 | R1A, R3 | SUITE QA manifest/ledger, then per-app suites; do not count forced passes. |
| V0-FND-001 | R1B | SHARED failure-handling convention, implemented by each host; repair legacy host first. |
| V0-FND-002 | R1B | SHARED lifetime convention, separate host ownership; repair legacy shutdown first. |
| V0-W15-001 | R1C | TOOLS executable extension trust and version binding. |
| V0-W15-002 | R1C | TOOLS known-good install retention; bounded process primitive only if reused. |
| V0-P2P-001 | R1D | TOOLS encrypted pairing/session, truthful transfer/peer state. |
| V0-W2-001 | R2A | SHARED safe publication primitive + TOOLS Converter integration. |
| V0-CMP-001 | R2A | SHARED safe publication primitive + TOOLS Compressor integration. |
| V0-VLT-001 | R1E | TOOLS Vault format, destination and truthful security repair. |
| V0-BAT-001 | R2B | TOOLS requested-capability truth/fallback repair. |
| V0-BAT-002 | R2B | TOOLS collision, memory and process-boundary repair. |
| V0-W3-001 | R2C | STUDIO Scholar generation/publication consistency. |
| V0-FLS-001 | R2D | STUDIO Flashcards through coordinated speech lifecycle. |
| V0-W4-001 | R2D | STUDIO production transcriber-to-router lifecycle. |
| V0-W4-002 | R2D, R2E | STUDIO speech lifetime and app-owned settings/runtime synchronization. |
| V0-NAV-001 | R2E | Each app's route/action truth; Studio/Tools current-host fixes, Mind future contract. |
| V0-SET-001 | R2E | Each app's versioned settings, with safe publication; no suite settings monolith. |
| V0-W2-002 | R5/defer | TOOLS converter depth and STUDIO W5 codec evidence; separate future scope. |
| V0-W3-002 | R2C | STUDIO Scholar background/transient generation hardening. |
| V0-W3F-001 | R5/defer | STUDIO wording: extractive synthesis until a real model capability exists. |
| V0-W4-003 | R5/defer | STUDIO dormant confirmation broker only if a real command requires it. |
| V0-UI-001 | R3, RV | SUITE QA rule, with executing/manual UI evidence per app. |
| V0-BLD-001 | R5/defer | TOOLS Skia and STUDIO Scholar warning debt, selectable separately. |

Wave ownership: **R4** SUITE; **R1A** SUITE QA; **R1B** legacy host and per-app lifecycle conventions; **R1C** TOOLS; **R2A** SHARED primitive with TOOLS consumers; **R1D/R1E/R2B** TOOLS; **R2C/R2D** STUDIO; **R2E** per-app settings/navigation; **R3** SUITE evidence plus each app's UI tests; **RV** independent suite-wide verification; **R5** only selected owner-specific debt after separate approval. The V0 plan's dependency graph still matters: trustworthy evidence and critical file/executable trust must precede migrating or exposing their consumers. Do **not** place all owner-specific repairs after host migration, which would copy known defects.

## 15. W5 and historical-roadmap remap

The four W5-P1 documents remain protected, untracked and unchanged. Their present contract is a deterministic, non-destructive **single-image** editor for Studio; W5-P2 codec feasibility and shared-raster ownership proof precede W5-A–F. Batch Image remains Tools. Generative image creation, model-based background removal and super-resolution are **optional Mind** concepts, not baseline W5 or a hidden dependency of Studio. A later separately authorized contract reconciliation must update W5's historical exact test-count/SDK references after V0-R1A and a new accepted baseline; P0 does not edit those plans. The historical roadmap's W4 Voice, W5 Image Studio, W6 Integrity, W7 Continuity and W8 capability ecosystem are phase-era names, not durable suite owners. Earlier handoff labels reuse W4–W7 differently; capability IDs above avoid silently renumbering either history.

## 16. Incremental migration and proposed suite sequence

No big-bang rewrite and no immediate split into three Git repositories. Preserve Git history where practical. Keep `Axora.Desktop` buildable as the migration source; move **vertical slices** (UI, service, tests, data compatibility) only after parity gates. A new host's existence does not retire a legacy feature. Future packaging choice, installer size, activation and backup/data migration need explicit experiments. At each slice: establish a source baseline, copy/extract on an authorized branch, run source/new parity tests, dry-run read-only data migration, verify release/rollback, then approve a single-writer cutover. No destructive legacy cleanup in the migration wave.

| Phase | Refined goal and exit gate |
|---|---|
| **SUITE-P0** | This portfolio proposal; user decides app count/names and planning assumptions. Stop here. |
| **SUITE-P1** | Exact dependency, package, UI-route, data-store, startup and feature-owner map; measured current footprint; no source moves. |
| **SUITE-P2** | Re-author V0 finding/wave ownership and disposition of blocked R4/ZIP/test hang; reconcile historical phase aliases, not protected W5 text yet. |
| **SUITE-P3** | Trustworthy test manifest/SDK/build and legacy-host lifecycle gate; separately authorized critical executable-trust/publication work before affected feature copies. Risky existing actions remain gated until safe. |
| **SUITE-P4** | Extract only proven shared Core/Platform/UI primitives with independent contracts and regression tests; W5 raster remains conditional on W5-P2. |
| **SUITE-P5** | Tools host, then Converter/Compressor, Batch, extension, Vault and P2P vertical slices; repair each high-risk behavior **before** cutover, verify data/permissions. |
| **SUITE-P6** | Studio host, then Resume/Flashcards, Scholar/voice and W5 only after their respective safety/codec gates; preserve documents/indexes. |
| **SUITE-P7** | Thin independent Mind host/provider/capability foundation with no mandatory local model; no feature-level generation promises. |
| **SUITE-P8** | Residual owner-specific remediation/debt and independent parity, **not** a catch-all postponement of P5/P6 safety fixes. |
| **SUITE-P9** | Explicit, optional, versioned cross-app activation/handoff only after each host works alone. |
| **SUITE-P10** | New products by individual User Problem First admission: Project Lab, W5 after gates, file integrity, selective Mind AI; recovery only after separate high-risk contract. |
| **SUITE-RV** | Independent build/test/UI/data/absence/rollback/package-size/resource verification for each app and shared primitive, then user acceptance and separately authorized Git publication. |

## 17. Risks, non-goals and open decisions

**Major risks:** accidental acceptance of partial V0-R4 evidence; unknown ZIP provenance; cloning unsafe Converter/Compressor/extension/Vault/P2P/Batch behavior; Scholar index migration loss; mixed legacy settings and dual writers; current build target force-stopping the app; background P2P versus explicit-network promise; total disk growth from multiple self-contained WinUI runtimes; native ONNX/Skia duplication; model/GPU assumptions on 16 GB integrated-graphics machines; secret leakage via AI prompts/project reports; false plagiarism claims; remote-project and archive ingestion abuse; driver/restore actions without rollback; branding and navigation drift. Every phase must own a risk register and a measured acceptance gate rather than treating “new host builds” as parity.

**Explicit non-goals of P0:** code/file moves, new csproj or solution edits, model/provider/runtime selection or installation, cloud or GitHub scan, feature repair, data migration, ZIP deletion/provenance inference, modifying V0-R4 or W5, test-count repair, cross-app IPC implementation, trademark clearance, exact footprint estimates, staging/commit/push/PR/merge/rebase, and starting SUITE-P1.

**User decisions before decomposition authorization:**

1. Accept three independently runnable apps and the provisional Studio/Tools/Mind names (or choose alternatives).
2. Choose packaging/distribution policy and what “lightweight” means in measured installed bytes, startup and idle memory; do not assume three apps save total disk.
3. Decide whether theme/accent should be suite-shared or app-owned, and whether any legacy app remains installed during cutover.
4. Authorize a separate, bounded investigation of the V0-R4 test hang and unknown untracked ZIP; no acceptance or deletion is implied here.
5. Decide opt-in/default policy for Tools' LAN listener and future external source discovery; reconcile it with local-first privacy language.
6. Approve future per-wave remediation ownership and migration/rollback before any source move; choose whether high-risk legacy actions remain unavailable until repaired.
7. Decide Mind history/credential portability and local/cloud disclosure policy before BYOK implementation.
8. Validate Project Lab's audience, review rubric, corpus and false-positive tolerance with real students/educators before scoring or external search.
9. Select one first vertical slice for an eventual SUITE-P1/P3 authorization; P0 itself authorizes none.

**P0 stop condition:** present this proposal for user review. Do not proceed into SUITE-P1, V0-R1A, W5-P2 or restructuring without a new explicit authorization.
