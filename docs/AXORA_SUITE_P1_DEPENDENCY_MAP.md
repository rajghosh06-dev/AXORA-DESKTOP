# AXORA suite — SUITE-P1 exact dependency, ownership and migration map

**Status:** planning-only evidence map for user review, 2026-09-28. This document does not authorize decomposition, source repair, V0 acceptance, W5-P2, or SUITE-P2. The current executable remains `Axora.Desktop`. Ownership below is a *future target*, not a statement that a new app exists.

**Evidence boundary.** Inspected repository source, project/solution files, QA scripts, the accepted [SUITE-P0 portfolio](AXORA_SUITE_PRODUCT_PORTFOLIO.md), [V0 remediation plan](V0_REMEDIATION_EXECUTION_PLAN.md), and all four `W5_IMAGE_STUDIO_*.md` plans. Current Git baseline is branch `main`, `75ca36ad12f9c64fc5ff88b549d1aece313353c2`. V0-R4's root `global.json` is untracked and nested WinUI `global.json` is deleted/unstaged; P0, V0 and four W5 plans are untracked; `Axora-Desktop-WinUI.zip` is untracked with unresolved provenance. This map does not inspect the ZIP as size/product evidence or change any of these items. Existing source proves wiring and intended behavior, not successful end-to-end execution. The broad test runner previously lacked a final summary; historical assertion counts are **not** current acceptance evidence.

**Notation.** Paths in the file map are relative to `Axora-Desktop-WinUI/Axora.Desktop/`: `S/` = `Services/`, `C/` = `Services/Contracts/`, `VM/` = `ViewModels/`, `V/` = `Views/`, `M/` = `Models/`, `H/` = `Helpers/`, `Ctl/` = `Controls/`. `—` means no identified direct data/network/process obligation, **not** proof that all transitive calls are side-effect-free. `F` is V0 finding or wave dependency. `Data` and `I/O` columns name relevant persistent state and network/process boundaries. Files grouped in a row are a coherent family; critical services, interfaces, pages and cross-product seams are named individually. `Shared` labels are admission candidates until a second real consumer and stable contract are demonstrated.

## 1. Repository and solution inventory

| Unit | Current fact | Future decision |
|---|---|---|
| `Axora-Desktop-WinUI/Axora.Desktop.sln` | One application project, `Axora.Desktop.csproj`; tests are not listed in the solution. | Retain legacy solution during parity; new solution/project topology requires separate authorization. |
| `Axora.Desktop.csproj` | Single C# 13 / .NET 9 WinUI 3 host, x64, `win-x64`, unpackaged (`WindowsPackageType=None`, `EnableMsixTooling=false`), Windows App SDK self-contained. | LEGACY/MIGRATION, ultimately replace with independently runnable Studio, Tools and Mind hosts. |
| `Axora.Desktop.Tests/Axora.Desktop.Tests.csproj` | Separate console test project referencing the app; `Program.cs`, `ScholarSynthesisEngineTests.cs`, `W4_VoiceSubsystemTests.cs`. | LEGACY/MIGRATION plus feature tests transferred by owner; no test-count inheritance without a complete manifest. |
| `scripts/qa/build-all.ps1` | Builds WinUI app and test project separately; does not rely on the solution including tests. | SUITE QA; later explicit per-host matrix. |
| `scripts/qa/run-tests.ps1`, `pipeline-full-qa.ps1`, `audit-workspace.ps1`, `doctor.ps1`, `pre-build-clean.ps1`, `security-scan.ps1`, `smoke-test.ps1` | Current validation/orchestration scripts; smoke test launches and force-stops rather than proving graceful shutdown. | SUITE QA; R1A/R3 must define truthful completeness and lifecycle evidence. |
| `scripts/qa/test-winui-*.ps1`, `test-ui.ps1`, `test-adversarial-winui.ps1`, `test-adversarial-ui.mjs`, `test-qa-mutations.mjs` | UI/user-journey/negative-test scripts; `test-winui-ui.ps1` contains a forced-pass expression `($hasFormatCombo -or $true)`. | Split by feature/app and preserve a suite-level runner; no forced pass counted. |
| `scripts/qa/test-materialui-*.mjs` | Tests for the separate MaterialUI implementation. | Out of P1/P2 WinUI decomposition scope; do not mutate. |
| `Assets/`, `app.manifest`, `Package.appxmanifest`, `priconfig.xml`, `Program.cs`, `Properties/launchSettings.json` | Host identity, resources and activation. | Mostly per-app copies/adaptations after identity and packaging decisions; not a shared executable. |

There is no existing `Axora.Studio`, `Axora.Tools`, `Axora.Mind`, or shared-library project. No app `ProjectReference` exists. The legacy test project references the legacy app. There is no bundled `model.onnx`; only `Assets/Models/all-MiniLM-L6-v2/vocab.txt` is in source. Scholar's DirectML path is optional and has lexical fallback. A future Mind model manager must not silently depend on this Studio-specific implementation.

## 2. Project, package, native and platform dependency map

Versions are from the current application project, not recommendations to upgrade. “Optional” means future architecture can avoid loading/shipping it for unrelated hosts; current monolithic package references remain present.

| Dependency/version | Current consumers and OS/native payload | Future owner; optionality; duplication/extraction decision |
|---|---|---|
| `Microsoft.WindowsAppSDK` **1.6.250228001**; `Microsoft.Windows.SDK.BuildTools` **10.0.26100.1742** | App/XAML/WinUI and Windows API projections; native XAML/runtime payload. | All three Windows UI hosts individually; unavoidable host baseline. Coordinate version and release measurement, not singleton process state. |
| `CommunityToolkit.Mvvm` **8.4.0** | All VMs, some observable models; managed. | Studio and Tools; Mind may choose same after its own design. Package can be common without shared VM code. |
| `Microsoft.Extensions.DependencyInjection`, `.Hosting`, `.Logging.Debug` **9.0.0** | `App.xaml.cs` host, DI, logging. | Per-host composition. Do not create one cross-app service locator or duplicate hosted background startup. |
| `SkiaSharp`, `SkiaSharp.Views.WinUI` **3.116.1** | WIC/PDF/format/image paths, Scholar raster extraction/OCR and POC; `libSkiaSharp.dll`. | Tools raster/format use, Studio Scholar/W5 only after codec proof; `Views.WinUI` only where a UI control needs it. High duplicate-native-payload risk; a shared raster library is not automatic. |
| `PdfSharpCore` **1.3.65** | Converter, compression, document/Markdown/PDF extraction, Resume compiler, annotations and PDF POC. | Both Studio and Tools, but separate feature adapters; avoid sharing a giant “PDF service.” Payload/format truth per feature. |
| `Microsoft.ML.OnnxRuntime.DirectML` **1.21.0** | `DirectMlEmbeddingEngine`, `DirectMlEmbeddingService`, Dashboard diagnostic; `onnxruntime.dll`/`DirectML.dll`, GPU/driver dependence. | Studio Scholar optional capability only; Mind model/provider system must be independently specified. Do not package into Tools or require on Intel Iris Xe. |
| `Konscious.Security.Cryptography.Argon2` **1.3.1** | `StreamingVaultService` KDF. | Tools Vault only. Crypto implementation/format not Shared.Core; preserve old vault readability. |
| `QRCoder` **1.8.0** | `H/QrCodeHelper.cs`, Mobile Link pairing display. | Tools only; no reason for Studio/Mind payload. |
| `Windows.Media.Ocr`, `Windows.Graphics.Imaging`, `Windows.Data.Pdf`, Windows speech/media/device APIs | OCR, WIC raster, PDF raster, dictation/playback, audio monitor; OS capability/permission dependencies. | Studio OCR/Scholar/voice and Tools WIC/Batch/Converter as separate adapters. Small platform contracts only if a second genuine consumer appears; test unavailable OS/device behavior. |
| WIA/COM scanner and shell/registry assets | `WiaScannerService`, `Assets/ShellExtensions/*`, process/shell integration. | Scanner Studio Scholar; shell verbs require per-app ownership/activation identity review. Do not clone old registry commands into every host. |

The test project separately references Windows App SDK/BuildTools/PdfSharpCore/MVVM at the matching app versions and `Microsoft.Extensions.Logging.Abstractions` 9.0.0. `RuntimeIdentifier=win-x64`, `Platforms=x64`, and `TargetFramework=net9.0-windows10.0.26100.0` with `TargetPlatformMinVersion=10.0.17763.0` constrain all current Windows behavior. A second app referencing the same package does not imply its service state is shared across processes.

## 3. File/type ownership map

**Difficulty** measures extraction and parity risk, not code length. “MOVE AFTER V0 REPAIR” in later graphs applies where a row lists a blocking finding. Shared candidate labels require section 9 admission. Path pairs `*.xaml{,.cs}` explicitly include view and code-behind.

| Path | Current responsibility | Direct dependencies | Current consumers | Future owner | Difficulty | F | Data | I/O | Migration note |
|---|---|---|---|---|---|---|---|---|---|---|
| `App.xaml{,.cs}` | DI, launch, theme, tray, P2P, shutdown | Host, all registered services/VMs | Whole app | LEGACY/MIGRATION | VERY HIGH | FND-001/002, P2P-001 | settings | LAN/startup | Replace with three per-host compositions only after legacy lifecycle repair; never copy all registrations. |
| `MainWindow.cs` | Mica shell, titlebar, key accelerators, drop | `ShellView`, palette | Whole app | LEGACY/MIGRATION | HIGH | NAV-001 | — | file drop | Each app owns window/identity/route catalog; Mica styling may be repeated. |
| `V/ShellView.xaml{,.cs}`; `VM/ShellViewModel.cs` | Navigation, route map, voice route exposure | all pages, voice coordinator | Whole app | LEGACY/MIGRATION | VERY HIGH | NAV-001, W4-001 | settings | — | Split route catalog and navigation by app; do not share one shell. |
| `Ctl/CommandPaletteDialog.xaml{,.cs}` | Search/action presentation and execution | shell route map, hardcoded entries | Shell | LEGACY/MIGRATION | HIGH | NAV-001 | — | — | Seven advertised actions do not execute; missing Converter; repair truth before replicating. UI pattern may later qualify SHARED.UI, catalogs stay local. |
| `Ctl/DependencyStatusControl.xaml{,.cs}` | Extension status | dependency model | Download Manager | TOOLS | LOW | W15-001/002 | extension state | download/install | No Studio shared control need. |
| `Ctl/FileDropZoneOverlay.xaml{,.cs}` | Drag/drop affordance | WinUI drag/drop | Shell, file features | REVIEW/UNDECIDED | MODERATE | NAV-001 | user files | file drop | Share visual shell only after two consumers and path-admission semantics agree. |
| `Ctl/FloatingDropWidget.xaml{,.cs}` | QuickDrop UI | P2P | Mobile Link | TOOLS | MODERATE | P2P-001 | transfer files | LAN | Never auto-start in other apps. |
| `Converters/BoolToVisibilityConverter.cs`; `H/DispatcherHelper.cs` | Generic binding/thread helpers | WinUI dispatcher | multiple views/VMs | SHARED.UI candidate | LOW | UI-001 | — | — | Only promote a small stateless helper after actual second use; copy is acceptable. |
| `H/NativeFilePickerHelper.cs` | Windows picker | WinUI/window handle | multiple file features | SHARED.PLATFORM candidate | MODERATE | UI-001 | chosen paths | picker | Do not hide product-specific file validation. |
| `H/CryptographyHelper.cs` | Cryptographic helper | crypto APIs | P2P/Vault | REVIEW/UNDECIDED | HIGH | P2P-001, VLT-001 | keys | LAN/vault | Audit primitive and key ownership before sharing; protocol and vault formats remain Tools-specific. |
| `H/QrCodeHelper.cs` | QR image | QRCoder | Mobile Link | TOOLS | LOW | P2P-001 | token presentation | — | Remove from non-Tools runtime. |
| `H/ResumeStorageHelper.cs` | Resume JSON paths/operations | filesystem/JSON | Resume pages/VM | STUDIO | HIGH | SET-001 adjacency | Documents resumes | filesystem | Version/recover/backup before any path cutover. |
| `H/SimdVectorHelper.cs`; `H/WordPieceTokenizer.cs` | Scholar vector/tokenizer support | numerics, vocab/model path | embedding/index | STUDIO | MODERATE | W3-001/002 | model vocab/index | — | Not Mind's tokenizer contract. |
| `M/ExtractionContracts.cs`; `M/ExtractionExceptions.cs`; `M/IndexModels.cs`; `M/ScholarKitModels.cs`; `M/ScholarSearchModels.cs`; `M/StudySynthesisModels.cs` | Scholar documents/chunks/index/search/study results | Scholar pipeline | Scholar services/VM | STUDIO | HIGH | W3-001/002, W3F-001 | Scholar library/index | — | Keep schema/read compatibility and truthful extractive naming. |
| `M/FlashcardDeck.cs`; `M/ResumeModel.cs`; `M/PdfAnnotationModels.cs` | Study and Resume records | feature services | Studio pages/VMs | STUDIO | MODERATE | FLS-001 for deck workflow | resume/export | — | Flashcards have no proven durable deck store; Resume does. |
| `M/VoiceModels.cs` | Speech/recognition state | voice services | Shell/Scholar/Flashcards/settings | STUDIO | HIGH | W4-001/002 | speech settings | mic/audio | Separate actual versus desired state; no global cross-app voice owner. |
| `M/ConversionJob.cs`, `ConversionJobState.cs`, `ConversionJobStatus.cs`, `ConversionJobUiModel.cs`, `ConversionProfile.cs`, `ConversionResult.cs`, `ConversionTelemetry.cs`, `EngineExecutionAffinity.cs`, `EngineResourceProfile.cs`, `QueueProgressReport.cs`, `CollisionPolicy.cs` | Converter job/queue/policy family | engine contracts | Converter VM/orchestrator | TOOLS | HIGH | W2-001/002 | outputs | filesystem | Do not expose mutable job internals as suite contracts. |
| `M/BatchImageJob.cs`, `CompressionJob.cs`, `FormatOptimizationCapabilities.cs`, `MetadataHandling.cs`, `OptimizationPreset.cs`, `OptimizationPresetCatalog.cs`, `OptimizationPresetId.cs`, `OptimizationValidation.cs` | Batch/compression/format policy family | WIC/Skia/PDF | Tools services/VMs | TOOLS | HIGH | BAT-001/002, CMP-001 | outputs | ImageMagick process | W5 may reuse *verified* codec facts, not Tools job models. |
| `M/ExtensionModel.cs`, `ExtensionStateChangedEventArgs.cs`, `ExtensionStatus.cs`, `ExtensionValidationResult.cs`, `DependencyUiState.cs`, `UpdateCheckStatus.cs` | Extension catalog/install/update state | extension services | Download Manager | TOOLS | MODERATE | W15-001/002 | extension cache | network/process | Not Mind model metadata. |
| `M/AxoraDevice.cs`; `M/QuickDropItem.cs` | LAN peer/transfer state | P2P service | Mobile Link/Dashboard | TOOLS | HIGH | P2P-001 | QuickDrop | LAN | State must reflect actual authenticated peer/ACK. |
| `M/TpmSecurityModels.cs` | Vault security profile | TPM/DPAPI service | Vault | TOOLS | HIGH | VLT-001 | sealed key | OS crypto | No portability guarantee implied by name. |
| `M/SystemTelemetry.cs` | host/system observation | Windows metrics | Dashboard | REVIEW/UNDECIDED | MODERATE | UI-001 | — | OS APIs | Current Dashboard is Tools-coupled; metric contracts should be app-local unless reused. |
| `C/IAppSettingsService.cs`; `S/AppSettingsService.cs` | one unversioned suite-sized settings file | JSON/filesystem | all hosts through monolith | LEGACY/MIGRATION | VERY HIGH | SET-001 | `%APPDATA%/Axora/settings.json` | file | Split property ownership; legacy read-only compatibility/migration plan before dual writers. |
| `C/INotificationService.cs`; `S/NotificationService.cs` | in-app notification | WinUI/dispatcher | multiple VMs | SHARED.UI candidate | MODERATE | UI-001 | — | — | Visual pattern can be shared; each process owns its own queue. |
| `C/IThemeService.cs`; `S/ThemeService.cs` | theme/accent application | settings, WinUI resources | app/settings | SHARED.UI candidate | MODERATE | SET-001 | theme settings | — | Tokens may share; runtime theme service is app-owned. |
| `C/ITrayService.cs`; `S/TrayService.cs` | legacy tray lifetime | Windows notification area | App | LEGACY/MIGRATION | HIGH | FND-002 | — | OS shell | Decide explicitly which app, if any, needs background tray. |
| `C/IDocumentProcessorService.cs`; `S/DocumentProcessorService.cs`; `S/MarkdownProcessor.cs` | document/PDF/Markdown utilities | PdfSharpCore | Scholar/Converter-adjacent | REVIEW/UNDECIDED | HIGH | W2-001 adjacency | user output | file | Split use-case methods; no all-purpose shared document engine. |
| `C/IOcrService.cs`; `S/WinRtOcrService.cs`; `C/IOcrEngine.cs`; `S/WindowsMediaOcrEngine.cs`; `C/IOcrCapabilityStateProvider.cs`; `S/WindowsOcrCapabilityStateProvider.cs` | OCR capability and Windows adapter | Windows OCR/WIC/Skia | Scholar extraction | STUDIO | HIGH | W3-002 | extracted text | OS OCR | Keep offline OCR degradation explicit. |
| `C/IPdfExtractionService.cs`; `S/PdfExtractionService.cs`; `S/PdfTextExtractor.cs`; `C/IPdfAnnotationService.cs`; `S/PdfAnnotationService.cs` | PDF text/annotations | PdfSharpCore, file | Scholar | STUDIO | HIGH | W3-001/002 | user PDF/annotations | file | Do not confuse Tools PDF conversion with Studio research semantics. |
| `C/IScannerService.cs`; `S/WiaScannerService.cs` | WIA scanner acquisition | COM/WIA | Scholar | STUDIO | MODERATE | UI-001 | scanned document | device | Hardware optional, no Mind/Tools autoload. |
| `C/IDocumentFormatDetector.cs`; `S/DocumentFormatDetector.cs`; `C/IDocumentPageBuilder.cs`; `S/DocumentPageBuilder.cs` | Scholar import format/page normalization | extractor contracts | Scholar orchestrator | STUDIO | MODERATE | W3-002 | library | file | Product-specific page semantics. |
| `C/IDocumentExtractorEngine.cs`; `C/IPdfDocumentExtractorEngine.cs`; `S/PlainTextExtractorEngine.cs`; `S/DelimitedTextExtractorEngine.cs`; `S/MarkdownExtractorEngine.cs`; `S/LocalHtmlExtractorEngine.cs` | text-like Scholar extraction | format detector/normalizer | Scholar orchestrator | STUDIO | MODERATE | W3-002 | library | file | Do not reuse as Mind attachment ingestion without a separate trust contract. |
| `S/PdfDocumentExtractorEngine.cs`; `S/DocxDocumentExtractorEngine.cs`; `S/RasterImageDocumentExtractorEngine.cs`; `S/TiffDocumentExtractorEngine.cs`; `C/IPdfPageRasterizer.cs`; `S/WindowsPdfPageRasterizer.cs` | binary/PDF/OCR Scholar import | PdfSharpCore, Windows PDF/WIC, Skia | Scholar orchestrator | STUDIO | HIGH | W3-001/002 | library/index | file/OS decode | Codec security and size limits stay explicit. |
| `C/ITextNormalizer.cs`; `S/TextNormalizer.cs`; `C/IPassageChunker.cs`; `S/PassageChunker.cs`; `C/IBoundedContextWindowBuilder.cs`; `S/BoundedContextWindowBuilder.cs` | Scholar canonical text/chunks/context | Scholar models | extraction/search/synthesis | STUDIO | MODERATE | W3-002 | index | — | Can move after tests; not Mind prompt infrastructure by default. |
| `C/IScholarExtractionOrchestrator.cs`; `S/ScholarExtractionOrchestrator.cs` | import/revision coordination | all extractors, library | Scholar VM | STUDIO | VERY HIGH | W3-001/002 | documents/sessions/index | file | Preserve generation ownership and rollback. |
| `C/IScholarLibraryService.cs`; `S/ScholarLibraryService.cs`; `C/IScholarPersistenceMigrator.cs`; `S/ScholarPersistenceMigrator.cs` | Scholar durable library and migration | JSON/filesystem | extraction/search/VM | STUDIO | VERY HIGH | W3-001/002 | `%APPDATA%/Axora/Scholar` | file | Single writer, backups, revisioned reader; no live dual-host writes. |
| `C/IEmbeddingEngine.cs`; `C/IEmbeddingCapabilityStateProvider.cs`; `S/DirectMlEmbeddingEngine.cs`; `C/IWindowsAiService.cs`; `S/DirectMlEmbeddingService.cs` | optional local embeddings and capability | ONNX DirectML/model | Scholar index/DocumentChat | STUDIO | HIGH | W3-002 | model/vocab | GPU/native | Constructor may initialize native session; host-lazy and optional. |
| `S/ScholarVectorIndexWriter.cs`; `S/ScholarVectorIndexReader.cs`; `C/IScholarIndexService.cs`; `S/ScholarIndexService.cs` | vector generation/read/rebuild | embeddings, binary manifest | Scholar search | STUDIO | VERY HIGH | W3-001/002 | multi-file index | file | Repair atomic generation protocol before extraction. |
| `C/IScholarSearchService.cs`; `S/ScholarSearchService.cs`; `S/CrossDocumentConflictDetector.cs`; `S/SixLayerGroundingVerifier.cs` | local grounded retrieval | library/index | synthesis/Scholar VM | STUDIO | HIGH | W3-001/002 | library/index | — | Keep offline lexical fallback; not Mind chat backend. |
| `C/IScholarSynthesisEngine.cs`; `S/ScholarSynthesisEngine.cs`; `C/IScholarSlmModelDriver.cs`; `S/NullScholarSlmModelDriver.cs` | extractive study synthesis | search, optional null SLM | Scholar VM | STUDIO | HIGH | W3F-001 | library/index | — | Label as extractive until a verified model driver exists. |
| `C/IDocumentChatService.cs`; `S/DocumentChatService.cs` | Scholar document question/answer | local embedding service | Scholar VM | STUDIO | HIGH | W3-002 | in-memory passages | GPU optional | Explicit **non-reuse** by Mind; matching name is not a product contract. |
| `C/ISpeechSynthesisService.cs`; `S/SpeechSynthesisService.cs`; `C/IVoiceTranscriberService.cs`; `S/VoiceTranscriberService.cs` | playback and recognition | Windows speech/media | Scholar/Flashcards/Shell | STUDIO | HIGH | W4-001/002, FLS-001 | selected voice | mic/audio | Repair cancellation/completion/ownership before extraction. |
| `C/IVoiceCoordinator.cs`; `S/VoiceCoordinator.cs`; `C/IVoiceCommandRouter.cs`; `S/VoiceCommandRouter.cs`; `C/IVoiceTextFormatter.cs`; `S/VoiceTextFormatter.cs` | voice mode, routing and dictated text | speech services, Shell routes | Studio features | STUDIO | VERY HIGH | W4-001/002, FLS-001 | voice settings | mic/audio | Router is product route-specific; destructive commands never voice-approve. |
| `C/IAudioDeviceMonitor.cs`; `S/AudioDeviceMonitor.cs` | device change watcher | Windows DeviceWatcher | Settings/voice | STUDIO | MODERATE | SET-001, W4-002 | selected device | device watcher | Constructor starts watcher; no other app registration by default. |
| `C/IResumePdfCompilerService.cs`; `S/ResumePdfCompilerService.cs`; `C/IAtsOptimizerService.cs`; `S/AtsOptimizerService.cs` | Resume export/ATS analysis | PdfSharpCore, Resume model | Resume VM | STUDIO | HIGH | W2-001 adjacency | resume/output | file | Preserve deterministic export and truthful ATS claims. |
| `C/IConversionEngine.cs`; `C/IConversionOrchestrator.cs`; `S/ConversionOrchestrator.cs`; `S/ConversionOutputValidator.cs` | converter routing, queue and output validation | engines/jobs | Converter VM | TOOLS | VERY HIGH | W2-001/002 | output | file | Repair safe publication before feature cutover. |
| `S/WicImageConversionEngine.cs`; `S/PdfDocumentConversionEngine.cs`; `S/TextMarkdownConversionEngine.cs`; `S/WindowsPdfRendererConversionEngine.cs`; `S/PdfRendererPocService.cs` | image/PDF/text conversion and PDF-image POC | WIC, Skia, PdfSharpCore, Windows PDF | converter orchestrator/tests | TOOLS | HIGH | W2-001/002, BLD-001 | output | file/OS codec | W5 must prove its own codec assumptions; no wholesale shared raster engine. |
| `C/IBatchImageProcessorService.cs`; `S/BatchImageProcessorService.cs`; `S/ExifOrientationNormalizer.cs` | batch transforms, metadata/orientation | WIC/Skia/ImageMagick optional | Batch VM | TOOLS | VERY HIGH | BAT-001/002 | output | file/process | Preflight requested capabilities; bound memory/process and reserve collision paths. |
| `C/IIntelligentCompressorService.cs`; `S/IntelligentCompressorService.cs` | compression | WIC/PDF/Skia | Compressor VM | TOOLS | HIGH | CMP-001 | output | file | Use repaired publication contract before migration. |
| `C/ISecurityVaultService.cs`; `S/StreamingVaultService.cs`; `C/ITpmSecurityProfileService.cs`; `S/TpmSecurityProfileService.cs` | encrypted file stream/security state | Argon2, Windows DPAPI/TPM | Vault VM | TOOLS | VERY HIGH | VLT-001 | sealed key/user vault | file/OS crypto | Preserve legacy decrypt fixtures, correct claims, never migrate key by copying. |
| `C/IP2pSyncService.cs`; `S/P2pSyncService.cs`; `C/IDownloadManagerService.cs`; `S/DownloadManagerService.cs` | LAN transfer and download/QuickDrop list | TCP/UDP, settings, file | Mobile Link/Dashboard | TOOLS | VERY HIGH | P2P-001 | QuickDrop/downloads | LAN/listener | No shared broker/autostart; repair session protocol and truthful status. |
| `C/IExtensionRegistry.cs`; `S/ExtensionRegistry.cs`; `C/IVersionDetector.cs`; `S/VersionDetector.cs`; `C/IExtensionValidator.cs`; `S/ExtensionValidator.cs` | catalog/current version/trust validation | HTTP/process/signatures | dependency manager | TOOLS | HIGH | W15-001/002 | catalog/cache | network/process | Exact-byte publisher/version binding required before execution. |
| `C/IExtensionDownloader.cs`; `S/ExtensionDownloader.cs`; `C/IExtensionInstaller.cs`; `S/ExtensionInstaller.cs`; `C/IExtensionRepairService.cs`; `S/ExtensionRepairService.cs`; `C/IExtensionCacheService.cs`; `S/ExtensionCacheService.cs`; `C/IDependencyManager.cs`; `S/DependencyManager.cs` | download/cache/install/repair orchestration | HTTP/process/filesystem | Download Manager/Batch | TOOLS | VERY HIGH | W15-001/002, BAT-002 | local extension dirs | network/process | Retain known-good install until verified replacement; not Mind model manager. |
| `VM/DashboardViewModel.cs`; `V/DashboardPage.xaml{,.cs}` | combined Tools transfer/system dashboard | P2P/download/settings/DirectML diagnostic | Shell | TOOLS variant | HIGH | P2P-001, NAV-001 | settings | LAN | Build separate Studio dashboard; do not lift this one wholesale. |
| `VM/ScholarKitViewModel.cs`; `V/ScholarKitPage.xaml{,.cs}` | Scholar UI/coordinator | OCR/PDF/voice/library/search | Shell | STUDIO | VERY HIGH | W3-001/002, W4-001/002 | Scholar | file/mic | Split only after lifecycle and index repair. |
| `VM/ResumeStudioViewModel.cs`; `V/ResumeStudioDashboardPage.xaml{,.cs}`; `V/ResumeStudioPage.xaml{,.cs}` | Resume dashboard/editor | storage, compiler, ATS | Shell | STUDIO | HIGH | SET-001 adjacency | Documents resumes | file/shell open | Both routes one vertical slice; retain old readers and recovery. |
| `VM/FlashcardsViewModel.cs`; `V/FlashcardsPage.xaml{,.cs}` | deck study, speech, exports | speech synthesis | Shell | STUDIO | MODERATE | FLS-001, W4-002 | export only | file/audio | Best first slice only after voice repair; no proven durable deck migration. |
| `VM/UniversalConverterViewModel.cs`; `V/UniversalConverterPage.xaml{,.cs}` | converter queue/UI | orchestrator/settings/notification | Shell | TOOLS | VERY HIGH | W2-001, NAV-001 | output | file | Require safe overwrite and real-format parity. |
| `VM/BatchImageViewModel.cs`; `V/BatchImagePage.xaml{,.cs}` | batch UI | batch processor/settings | Shell | TOOLS | HIGH | BAT-001/002 | output | process/file | Distinct from Studio single-image W5. |
| `VM/CompressorViewModel.cs`; `V/CompressorPage.xaml{,.cs}` | compressor UI | compressor/settings | Shell | TOOLS | HIGH | CMP-001 | output | file | Preserve profile/collision parity. |
| `VM/VaultViewModel.cs`; `V/VaultPage.xaml{,.cs}` | Vault UX | Vault/TPM | Shell | TOOLS | VERY HIGH | VLT-001 | keys/vault files | crypto/file | Not first slice. |
| `VM/MobileLinkViewModel.cs`; `V/MobileLinkPage.xaml{,.cs}` | peers/transfer UI | P2P/download/settings | Shell | TOOLS | VERY HIGH | P2P-001 | transfers | LAN/listener | VM constructor starts server; must become explicit lifecycle. |
| `VM/DownloadManagerViewModel.cs`; `V/DownloadManagerPage.xaml{,.cs}` | extension/dependency UI | dependency manager/cache/notifications | Shell | TOOLS | HIGH | W15-001/002 | extension cache | HTTP/process | Product-specific store/catalog, not universal model manager. |
| `VM/SettingsViewModel.cs`; `V/SettingsPage.xaml{,.cs}` | mixed suite controls | settings/theme/speech/device monitor | Shell | LEGACY/MIGRATION | VERY HIGH | SET-001, W4-002 | settings.json | audio watcher | Split into app-owned settings pages; minimum suite preference below. |

The app's `Assets/AppIcon.ico`, logo PNGs, splash image, `App.xaml` resources and `Package.appxmanifest` are legacy-brand/host assets; assign app identities during product design, not a shared host. `Assets/ShellExtensions/RegisterAxoraShellVerbs.reg` and `Windows11ContextMenuManifest.xml` need explicit verb-to-app routing and uninstall behavior before any new installer. `Program.cs`, `app.manifest`, `priconfig.xml`, and `launchSettings.json` are host scaffolding. These are included in the ownership map by this paragraph because their extraction decision is per-app identity, not feature logic.

## 4. Type/service dependency graph and classification

```text
Legacy App -> one DI host -> MainWindow -> ShellView/ShellViewModel
  -> Studio: ScholarKit -> OCR/PDF/scan + voice + Scholar library
       Scholar import -> extractors -> normalized pages/chunks -> library
       index -> optional DirectML embeddings -> vector writer/reader -> search
       search + library + null SLM driver -> extractive synthesis
       DocumentChat -> local embedding/passages (not Mind)
       Resume -> storage + ATS + PDF compiler
       Flashcards -> speech synthesis (currently bypasses coordinator)
  -> Tools: Converter -> orchestrator -> WIC/PDF/text/Windows PDF engines
       Batch -> WIC/Skia + optional ImageMagick process
       Compressor -> raster/PDF output
       Vault -> Argon2 + user-bound sealed key
       Mobile Link -> P2P TCP listener + UDP beacon + downloads
       Download Manager -> catalog -> version/trust -> HTTP -> process install
  -> mixed Settings + Dashboard + Palette + Tray
```

| Seam | Classification | Why / gate |
|---|---|---|
| `App.xaml.cs`, Shell, Dashboard, Settings, Palette | **REPLACE DURING MIGRATION** | Hardcoded mixed-product composition/routes; per-host lifecycle and truthful catalogs needed. Repair legacy FND/NAV/SET before copying any behavior. |
| Resume models/ATS/PDF after storage proof | **CAN MOVE AS-IS** only for pure portions | UI/storage shell path must be adapted; preserve JSON/PDF parity. No blanket claim for direct writes. |
| Flashcards view/deck logic | **MOVE AFTER V0 REPAIR** | Direct speech call violates voice coordinator, exports require collision/result proof. |
| Scholar library/index/import/search/synthesis | **MOVE AFTER V0 REPAIR** | W3 generation and task ownership first; single writer and legacy read contract. |
| Converter/Compressor/Batch/Vault/P2P/Extensions | **MOVE AFTER V0 REPAIR** | Each has named data-safety, security, truth or process defect. No new-host cutover before corresponding wave. |
| Safe single-file publication, bounded process runner, typed results/path checks | **EXTRACT SHARED PRIMITIVE FIRST** *only after repaired contract and second use* | Small stateless interface/implementation; feature-specific semantics stay in owners. Do not pre-create large Shared library. |
| Voice, Scholar DocumentChat/synthesis, extension manager, P2P, Vault, app shells | **KEEP PRODUCT-SPECIFIC** | Lifecycle/security/data semantics differ; names alone do not justify reuse. |
| Current notification/theme runtime | **KEEP PRODUCT-SPECIFIC**, perhaps share tokens/control | Each process owns state; sharing WinUI visual tokens is not sharing a service instance. |

## 5. DI, startup and shutdown ownership

`App.xaml.cs` registers singletons. Singleton here means *within this legacy process*; moving registrations does not create a suite-wide singleton. DI generally constructs lazily, but resolving a VM/service can trigger constructor side effects. The following is the complete major registration grouping rather than a proposal to copy its `ConfigureServices` block.

| Current DI group | Registrations / construction chain | Future host and lifecycle rule |
|---|---|---|
| Cross-cutting legacy | `IAppSettingsService/AppSettingsService`, `INotificationService/NotificationService`, `IThemeService/ThemeService`, `ITrayService/TrayService` | New hosts own distinct settings, notification queue and theme instance. Tray assigned explicitly only after user/background need; legacy shutdown repaired first. |
| Tools utility | `IDownloadManagerService/DownloadManagerService`, `IBatchImageProcessorService/BatchImageProcessorService`, `IIntelligentCompressorService/IntelligentCompressorService`, `IP2pSyncService/P2pSyncService`, `ISecurityVaultService/StreamingVaultService`, `ITpmSecurityProfileService/TpmSecurityProfileService` | Tools only; P2P never registered/started in Studio or Mind. Download service creates configured directory when constructed. |
| Tools extension | `IExtensionCacheService/ExtensionCacheService`, `IExtensionRegistry/ExtensionRegistry`, `IVersionDetector/VersionDetector`, `IExtensionValidator/ExtensionValidator`, `IExtensionDownloader/ExtensionDownloader`, `IExtensionInstaller/ExtensionInstaller`, `IExtensionRepairService/ExtensionRepairService`, `IDependencyManager/DependencyManager` | Tools only, lazy; downloader uses HTTP and installer/version detector may execute processes. Do not confuse with Mind model capabilities. |
| Tools conversion | Four concrete engines `WicImageConversionEngine`, `PdfDocumentConversionEngine`, `TextMarkdownConversionEngine`, `WindowsPdfRendererConversionEngine` also as `IConversionEngine`; factory `IConversionOrchestrator/ConversionOrchestrator` | Tools only, bounded queue; no converter startup work in other hosts. |
| Studio voice | `ISpeechSynthesisService/SpeechSynthesisService`, `IVoiceTranscriberService/VoiceTranscriberService`, `IVoiceTextFormatter/VoiceTextFormatter`, `IVoiceCommandRouter/VoiceCommandRouter`, `IAudioDeviceMonitor/AudioDeviceMonitor`, `IVoiceCoordinator/VoiceCoordinator` | Studio process-owned. AudioDeviceMonitor constructor starts DeviceWatcher; transcription/microphone acquisition must be explicit and permission-aware. |
| Studio document/OCR/Resume | `IDocumentProcessorService/DocumentProcessorService`, `IDocumentChatService/DocumentChatService`, `IPdfAnnotationService/PdfAnnotationService`, `IWindowsAiService/DirectMlEmbeddingService`, `IOcrCapabilityStateProvider/WindowsOcrCapabilityStateProvider`, `IOcrEngine/WindowsMediaOcrEngine`, `IOcrService/WinRtOcrService`, `IPdfExtractionService/PdfExtractionService`, `IScannerService/WiaScannerService`, `IResumePdfCompilerService/ResumePdfCompilerService`, `IAtsOptimizerService/AtsOptimizerService` | Studio-only adapters; OCR, scanner, GPU/model and audio are capability-checked, not unconditional host-start dependencies. |
| Studio Scholar extraction | `IScholarPersistenceMigrator/ScholarPersistenceMigrator`, `IScholarLibraryService/ScholarLibraryService`, `IDocumentPageBuilder/DocumentPageBuilder`, `IDocumentFormatDetector/DocumentFormatDetector`, text/CSV/Markdown/HTML/PDF/DOCX/raster/TIFF `IDocumentExtractorEngine`s, `IPdfPageRasterizer/WindowsPdfPageRasterizer`, `ITextNormalizer/TextNormalizer`, `IPassageChunker/PassageChunker`, `IBoundedContextWindowBuilder/BoundedContextWindowBuilder`, factory `IScholarExtractionOrchestrator/ScholarExtractionOrchestrator` | Studio only; no index/migration work in Mind. Preserve first-run and format failure semantics. |
| Studio Scholar retrieval | `DirectMlEmbeddingEngine` as `IEmbeddingEngine` and `IEmbeddingCapabilityStateProvider`; `ScholarVectorIndexWriter/Reader`; `IScholarIndexService/ScholarIndexService`, `IScholarSearchService/ScholarSearchService`, `IScholarSlmModelDriver/NullScholarSlmModelDriver`, `IScholarSynthesisEngine/ScholarSynthesisEngine` | Studio only, model optional. Resolving DirectML engine can initialize native session; avoid eager dashboard-driven GPU load. |
| Legacy VMs | `Shell`, `Dashboard`, `ScholarKit`, `ResumeStudio`, `BatchImage`, `Compressor`, `UniversalConverter`, `Vault`, `Flashcards`, `MobileLink`, `DownloadManager`, `Settings` VMs all singleton | Each new host registers only its feature VMs. `MobileLinkViewModel` constructor calls `EnsureServerStartedAsync`; remove implicit listener start at eventual extraction. |

`OnLaunched` creates/activates the window, initializes theme, starts `AppHost.StartAsync()` with a continuation rather than an awaited readiness gate, initializes tray, and starts P2P automatically when `AutoStartP2pEngine` (default true). Shutdown explicitly removes/disposes tray, stops P2P, disposes voice coordinator, then stops/disposes host; these paths overlap DI ownership (V0-FND-002). Future host startup must be awaited, fail closed, and have one disposer per resource (V0-FND-001/002). **Prohibited cross-host startup:** Studio/Mind must not inherit P2P listener, extension downloader/installer, QuickDrop folder creation, Tools tray assumptions, or eager DirectML/audio watcher unless their own feature is active.

## 6. UI and navigation ownership

| Current route/tag | Current page/VM | Target | Notes |
|---|---|---|---|
| `Dashboard` | `DashboardPage`/`DashboardViewModel` | TOOLS variant; new STUDIO/MIND dashboards | Current P2P/download/status and DirectML diagnostics are mixed. No shared Dashboard VM. |
| `ScholarKit` | `ScholarKitPage`/VM | STUDIO | Local research workflow. |
| `ResumeStudio`, `ResumeStudioEditor` | `ResumeStudioDashboardPage`, `ResumeStudioPage`/VM | STUDIO | Two routes are one Resume feature. |
| `Flashcards` | `FlashcardsPage`/VM | STUDIO | Speech coordination gate. |
| `UniversalConverter` | `UniversalConverterPage`/VM | TOOLS | Route exists; palette omission is V0-NAV-001. |
| `BatchImage` | `BatchImagePage`/VM | TOOLS | Not W5 Image Studio. |
| `Compressor` | `CompressorPage`/VM | TOOLS | Output safety gate. |
| `Vault` | `VaultPage`/VM | TOOLS | Security/data gate. |
| `MobileLink` | `MobileLinkPage`/VM | TOOLS | LAN listener/QuickDrop. |
| `DownloadManager` | `DownloadManagerPage`/VM | TOOLS | Extension/dependency manager, not Mind model manager. |
| `Settings` | `SettingsPage`/VM | Each host | UI and persistence split by actual app field owner. |

`ShellViewModel.PageMap` has 12 tags above; voice navigation is generated from the page map plus shell/playback controls, so route separation must also separate safe voice intents. `CommandPaletteDialog` has 17 visible entries: 10 navigation entries and seven non-navigation entries that do not execute; Converter is absent. Its advertised Up/Down/Enter behavior is not wired. Per-app route/action catalogs must drive navigation, palette, deep-link allowlists and accessible command names from one tested source. Reasonable eventual `SHARED.UI` candidates are theme tokens, notification visual, a file-drop *visual* and small accessible controls only; route registrations, quick actions, settings panes and window/tray lifetime stay product-specific.

## 7. Settings ownership: every current property

The implementation stores one direct, unversioned `%APPDATA%/Axora/settings.json` (environment `APPDATA` override or executable-directory fallback) and exposes `LastPersistenceError`. Malformed-load recovery can write defaults. These are facts about legacy behavior, **not** a future three-writer schema. “Suite preference” is intentionally minimal: an optional visual theme/accent preference only if users explicitly want synchronized appearance and a conflict/override rule is designed. Initially per-app is safer.

| `IAppSettingsService` member | Current default | Future owner | Migration/validation rule |
|---|---|---|---|
| `ThemeIndex` | `0` | STUDIO/TOOLS/MIND app preference; optional SUITE visual preference later | Copy as initial value at most; each app can override, no live multiwriter file. |
| `AccentColor` | `#5B7DE8` | Same | Validate color/token; don't force Mind style to match. |
| `IsTelemetryEnabled` | `true` | DEPRECATED/REVIEW then each app privacy decision | Current single switch is not consent for three products or AI providers; no implicit migration as opt-in. |
| `AutoStartP2pEngine` | `true` | TOOLS | Explicit listener consent/default review; never carried to Studio/Mind. |
| `BackgroundQuickDropListen` | `true` | TOOLS | Bound to actual background behavior; no hidden suite daemon. |
| `P2pPort` | `5050` | TOOLS | Current listener binds ephemeral TCP port `0`; reconcile semantics in V0-R1D. |
| `DownloadDirectory` | `UserProfile/Downloads/Axora_QuickDrop` | TOOLS | Download service uses it; P2P currently uses a fixed QuickDrop path; resolve before migration. |
| `Argon2MemoryMb` | `64` | TOOLS/Vault | Current Vault ignores selected KDF settings (V0-VLT-001); encode effective parameters in versioned format. |
| `Argon2Iterations` | `3` | TOOLS/Vault | Same. |
| `SelectedVoiceId` | `null` | STUDIO | Resolve available voice/device safely; runtime state may differ from desired value. |
| `SpeechRate` | `1` | STUDIO | Clamp/validate; keep local speech effect truthful. |
| `SpeechPitch` | `1` | STUDIO | Same. |
| `IsVoiceNavigationEnabled` | `false` | STUDIO | Permission and actual recognizer state, no false listening indicator. |
| `IsAutoPunctuationEnabled` | `true` | STUDIO | Dictation setting only. |
| `LastPersistenceError` | computed | LEGACY/MIGRATION -> per-app diagnostics | Not a persisted suite preference. |

No current member maps to Mind model selection, provider credentials, chat retention or context ingestion. Those require a new Mind contract, private per-app storage and explicit user admission. Before extracting Settings, V0-R2E must provide versioned/recoverable legacy reads; later migration must be one-time, idempotent, backed up, and never leave two active writers at the old path.

## 8. Persistence, data and single-writer map

| State | Actual legacy path/logic | Future owner | Risk / single writer / compatibility / rollback |
|---|---|---|---|
| Settings | `%APPDATA%/Axora/settings.json`; `APPDATA` env override, fallback executable base | Per-app settings; old file LEGACY/MIGRATION | HIGH. One legacy writer until cutover; preserve original bytes and recovery fixture, do not make three apps edit it. |
| Resume data | `Documents/Axora/Resumes` JSON via `ResumeStorageHelper` and page direct writes/deletes | STUDIO | HIGH despite simple schema: user-authored data. Backup before copy, versioned read, idempotent import, old host read-only during final cutover, rollback via saved legacy files. |
| Resume exports | User-selected PDF/other paths, direct write/open | STUDIO | MODERATE/HIGH collision risk. Existing destinations remain recoverable; explicit publish/overwrite tests. |
| Scholar library | `%APPDATA%/Axora/Scholar/{documents,sessions,indexes,metadata,quarantine}` via `ScholarLibraryService`; env `APPDATA` override | STUDIO | VERY HIGH. One writer; generation, corruption/quarantine, revision and backup protocol before cutover; old reader retained until validated. |
| Scholar sessions/indexes | Same tree, but vector reader/writer use `Environment.SpecialFolder.ApplicationData` directly | STUDIO | VERY HIGH path inconsistency under `APPDATA` override; multi-file manifest/bin not atomic. V0-R2C then migration inventory by *actual* discovered path, no assumed single root. |
| Scholar model/vocab | Embedded vocab; optional `model.onnx` under `%APPDATA%/Axora/Capabilities/Models/all-MiniLM-L6-v2` or app assets; tokenizer/embedding use special folder API in places | STUDIO | MODERATE. Model optional; don't duplicate into Tools or equate with future Mind model catalog. Verify license/provenance before redistribution. |
| Flashcards | Seeded in-memory decks; CSV/Anki/JSON export to user-chosen files; no durable deck store proven | STUDIO | LOW persistent migration, MODERATE export/speech parity. Never promise import of unsaved runtime state; close old session only after warning. |
| Vault sealed key | `%APPDATA%/Axora/vault_sealed.dat`; user-bound Windows `DataProtectionProvider("LOCAL=user")` | TOOLS | VERY HIGH. Not portable by file copy. Keep old decrypt path, fixtures and user-context checks; backup encrypted inputs, no irreversible key rewrite. |
| Vault encrypted outputs | Beside source or user-selected `.axvault` | TOOLS | VERY HIGH. Preserve old format/version and collision semantics; backup, verify full decrypt before replacing any output. |
| Extension cache/install | `%LOCALAPPDATA%/Axora/ExtensionCache` and `Extensions`, fallback `AppContext.BaseDirectory/Data`; registry seeded in memory (ImageMagick), not a proven durable catalog | TOOLS | HIGH executable trust. One installer writer, known-good retained until verified; avoid reusing cache path from multiple app processes. |
| QuickDrop/downloads | P2P fixed `UserProfile/Downloads/Axora_QuickDrop`; download service uses configured `DownloadDirectory`; `Transfers` in memory | TOOLS | HIGH discrepancy and LAN security. Reconcile path/identity/protocol in R1D; copy only verified files, not fictitious transfer state. |
| Logs | `startup.log` in executable base used by app/converter paths | Each host diagnostic location | MODERATE. Unpackaged install may be non-writable; define privacy/rotation before three hosts. |
| Future Mind conversations, attachments, provider credentials, models | **No current Mind store** | MIND | New versioned storage/security contract; no Scholar or Vault path reuse by implication. Local/offline behavior and delete/export must be designed before user data exists. |

Current data paths are evidence of behavior, not guaranteed canonical locations. `APPDATA` environment handling differs from direct `SpecialFolder.ApplicationData` calls in Scholar components; migration must inspect both actual roots with provenance and conflicts. Rollback means ability to reopen the *old* valid generation/file, not merely copying files back while both processes may still write. No migration is performed in P1.

## 9. Network, process, device and privileged-boundary ownership

| Boundary and exact entry point | Current behavior | Future owner and non-negotiable gate |
|---|---|---|
| `S/P2pSyncService.cs` | `TcpListener(_localIp, 0)` chooses an ephemeral TCP port; `UdpClient` emits LAN discovery beacon, currently including reusable pairing token; receive/peer state deficiencies are V0-P2P-001. | TOOLS only. No listener in Studio/Mind, no always-running suite broker. Authenticated session/acknowledged bounded streaming and user-visible network consent before cutover. |
| `App.xaml.cs` and `VM/MobileLinkViewModel.cs` | `AutoStartP2pEngine` defaults true at launch; MobileLink VM constructor also calls server start. | TOOLS explicit lifecycle owner, one start/stop path, real bound-port/status presentation. Opening Settings/Dashboard must not accidentally duplicate start. |
| `S/ExtensionDownloader.cs` | `HttpClient` downloads executable artifact. | TOOLS only. Artifact hash, version and publisher bound to trusted metadata before installer sees bytes. |
| `S/VersionDetector.cs`, `S/ExtensionInstaller.cs`, `S/ExtensionRepairService.cs` | Local version command and installer process execution; repair can affect known-good installed binary. | TOOLS only. V0-R1C bounded stdout/stderr, timeout, cancellation, staged replacement, rollback. A shared process *contract* only if Batch and extension use the same safety semantics. |
| `S/BatchImageProcessorService.cs` | Optional external ImageMagick process; WIC/Skia fallback. | TOOLS only. Capability preflight, decoded-memory budget and safe process/exit behavior in V0-R2B. |
| Resume, Converter and other UI open-location actions | `Process.Start` with shell activation for selected user paths in some workflows. | Owning app only; validate path/source and preserve user intent. Not equivalent to arbitrary command execution. |
| `S/DirectMlEmbeddingEngine.cs` | May initialize ONNX DirectML session in constructor after resolve. | STUDIO Scholar only, optional/lazy, CPU/lexical fallback tested. Neither Tools nor Mind startup should resolve it. |
| `S/AudioDeviceMonitor.cs`, `S/VoiceTranscriberService.cs`, `S/SpeechSynthesisService.cs` | Device watcher starts in monitor constructor; speech recognition/playback uses Windows audio APIs. | STUDIO, permission/device/error aware. Mind could later define separate voice input, not borrow this instance implicitly. |
| `S/WiaScannerService.cs`, OCR and WIC/PDF raster adapters | OS hardware/media codecs and COM capabilities can be absent or fail. | STUDIO scanner/Scholar; Tools codec adapters stay separate. Capability state and failure truth in each app. |
| `Assets/ShellExtensions/*` and `S/TrayService.cs` | Registry/shell activation and notification-area lifetime tied to old executable identity. | REVIEW until host/installer ownership is chosen. Explicit install/uninstall and no competing verbs/tray icons. |

File decoders and PDFs are also hostile-input boundaries even without a network socket. Each destination app must carry size, path and metadata validation. Future cross-app handoffs are untrusted inputs; an AXORA-branded file is not automatically safe or authorized.

## 10. Shared-code admission decisions

Admission requires **two real consuming apps**, stable minimal contract, no hidden process-wide state, and tests proving failure semantics. A shared project is not justified by using the same NuGet package. Intentional duplication of two small adapters or UI controls is preferable to a shared dependency that makes every app carry Skia/ONNX, network startup or Vault state.

| Candidate | Second-app demand? | Stable/no lifecycle coupling? | Decision now |
|---|---|---|---|
| Typed operation result, cancellation/error category, path/filename validation | Plausible Studio+Tools and future Mind attachments | Stable only when exact semantics written and negative-tested | **SHARED.CORE candidate**, introduce with second proven use; no general “utility bag.” |
| Versioned JSON/serialization envelope and safe single-file publication | Settings (all hosts), Tools Converter/Compressor, Studio Resume/W5 | Potentially stable after V0-R2A/R2E; filesystem atomicity varies by target | **SHARED.CORE or SHARED.PLATFORM candidate** after V0 repair, with transaction/collision policy supplied by feature. Do not make Scholar multi-file index use it as if one-file commit solved generation consistency. |
| Bounded external-process execution | Tools Extensions and Batch both need it | Stable after R1C/R2B error/timeout model | Prefer **Tools-internal first**; promote to SHARED.PLATFORM only when a separate app actually needs subprocess execution. |
| Hashing and signature verification | Tools extension trust; future Studio/Mind integrity may need hashes | Primitive stable; trust roots/publisher policy not stable across products | Pure hash/path primitive possible; **trust policy stays owner-specific**. |
| Windows file picker/window-handle helper | Studio and Tools need picking | Small, app-window parameterized | SHARED.PLATFORM candidate; local copies may be simpler. |
| Theme tokens and accessible small controls | Three WinUI hosts likely need coherent identity | Visual contract may stabilize after first two hosts | SHARED.UI candidate **only for tokens/control**, not navigation tree, dashboards, command catalogs or runtime queues. |
| Diagnostic convention and telemetry event shape | Multiple apps | Privacy/retention differs | Share naming/typed severity at most; opt-in, sink and user consent per app. |
| AI capability descriptors | Studio optional embedding and future Mind models | Not yet stable; same word “model” hides different guarantees | **AI.CONTRACTS deferred** until Mind + Studio or another real second producer/consumer. Do not move DirectML engine. |
| PdfSharpCore/WIC/Skia codec adapters | Both Studio and Tools use format libraries | Feature capability and metadata semantics differ | No shared “universal document/image engine” now. Reconsider a narrow proven raster decoder only after W5-P2 codec matrix and Tools parity. |

**Explicitly excluded from Shared:** `App.xaml.cs` service host, `ShellViewModel`, Dashboard, route/palette list, `AppSettingsService` mutable singleton, Scholar library/index/DocumentChat/synthesis, Flashcards, voice coordinator/router, Converter queue, Batch job policy, Vault key/format, P2P session, extension catalog/trust/install, and future Mind provider credentials/conversation store. “Shared contract only” in DI never means a shared running service across three processes.

## 11. Coupling and risk map

| Coupling seam | Mechanism and observed hazard | Migration control |
|---|---|---|
| Monolithic composition/lifecycle | One App registers all features, mixed singleton VMs and startup/shutdown; P2P auto-start, possible double dispose and swallowed fatal initialization. | Repair R1B, then per-host registration inventories and startup negative tests (“does *not* start listener/model/device”). |
| Shared legacy `settings.json` | Settings mixes P2P, Vault, Studio voice and visual fields; unversioned direct writes. | R2E version/recovery, then read/backup/one-time per-app import; no simultaneous writers. |
| Scholar generation/path split | Library respects `APPDATA` override while writer/reader/model helpers use SpecialFolder in places; index is multi-file. | R2C consistent generation and path discovery; conflict/rollback fixture before Studio cutover. |
| Tools output publication | Converter deletes destination before verified replacement; Compressor analogous; Batch collision/process issues. | R2A/R2B first; negative fault-injection and real-file parity in new host. |
| Security/irreversible data | Vault key user-bound; extension binary trust absent; P2P beacon/session unsafe. | No first-slice use; R1C/R1D/R1E and old-data compatibility before new host. |
| UI catalog drift | Hardcoded Shell, palette and voice routes disagree; seven palette actions dead. | R2E route/action truth; build per-app catalogs with executing UI tests. |
| Native payload and integrated GPU | Self-contained Windows App SDK plus DirectML/ONNX and Skia can bloat three hosts and trigger feature work on 16 GB/Iris Xe machine. | Keep optional per-owner packages, lazy activation, measure published/installed and feature working set on target-class hardware. |
| Test/build evidence | Tests absent from sln, custom scripts, blocked broad runner, forced-pass UI expression, build target force-kills process. | R4 acceptance then R1A/R3 explicit ledger; separate app identity/build targets and graceful-close tests. |

The consequence is **vertical, guarded feature migration**, not moving directories en masse. Each slice leaves the legacy host usable until new-host parity and rollback are proven, then establishes exactly one writer/owner. A “build succeeded” result alone is insufficient.

## 12. V0-before/during/after-extraction map — all 24 findings

Timing codes: **A** repair legacy host before extraction; **B** repair while defining a genuinely reused primitive, retaining legacy feature integration; **C** repair in final product *before feature cutover* only where this does not propagate unsafe behavior; **D** deferred/non-blocking with explicit product evidence. An A+B item needs both legacy integration and shared-contract proof; **C never licenses copying a known high-risk defect into a new runnable app**. Existing wave order/authority remains in [V0-RP](V0_REMEDIATION_EXECUTION_PLAN.md); no repair is authorized here.

| Finding (existing wave) | Timing | Owner and exact sequencing reason |
|---|---|---|
| `V0-TCH-001` (R4) | A | SUITE toolchain pin and root/nested entry points must be accepted before reproducible new-host baseline; current attempt remains unaccepted. |
| `V0-TEST-001` (R1A, R3) | A then C | SUITE: trustworthy legacy execution manifest first; per-app suite/semantic UI evidence before each cutover and R3/RV. |
| `V0-FND-001` (R1B) | A | Legacy fatal initialization cannot be copied into three hosts; per-host fatal-path tests later. |
| `V0-FND-002` (R1B) | A | Fix legacy DI/resource disposal ownership, then each host owns only its own resources. |
| `V0-W15-001` (R1C) | A | TOOLS extension executable trust/version binding before any new installer can execute bytes. |
| `V0-W15-002` (R1C) | A; B only if second consumer | TOOLS retain known-good install and bounded process; do not pre-share install semantics. |
| `V0-P2P-001` (R1D) | A | TOOLS encrypted private session/beacon/ACK truth before Mobile Link migration. |
| `V0-W2-001` (R2A) | B with A integration | TOOLS Converter, narrow safe single-file publisher candidate; prior destination preserved on failure in legacy and new host. |
| `V0-CMP-001` (R2A) | B with A integration | TOOLS Compressor second real consumer validates publisher contract. |
| `V0-VLT-001` (R1E) | A | TOOLS old-format decrypt, KDF truth and safe output before moving security-sensitive workflow. |
| `V0-BAT-001` (R2B) | A | TOOLS explicit requested-capability preflight before copying fallback path. |
| `V0-BAT-002` (R2B) | A; B process primitive only if warranted | TOOLS bound collisions/memory/process in legacy, then new-host parity on target hardware. |
| `V0-W3-001` (R2C) | A | STUDIO verified Scholar generation/rollback before moving any index writer. |
| `V0-FLS-001` (R2D) | A | STUDIO Flashcards must use coordinated playback before its recommended first migration slice. |
| `V0-W4-001` (R2D) | A | STUDIO production transcriber-to-router lifecycle; no fake command-listening state. |
| `V0-W4-002` (R2D/R2E) | A then C | STUDIO audio completion/lifetime repaired in legacy; app-owned desired/actual setting sync required before Studio cutover. |
| `V0-NAV-001` (R2E) | A then C | Repair legacy palette/route truth; new per-app route catalog must prove all displayed actions execute before cutover. |
| `V0-SET-001` (R2E) | A then C | Recoverable versioned legacy settings before migration; per-app settings import/new writer gate before cutover. |
| `V0-W2-002` (R5/W5 proof) | D | Deeper Tools metadata/TIFF/alpha and Studio W5 codec evidence; disclose unsupported depth, do not silently declare parity. |
| `V0-W3-002` (R2C) | A | STUDIO coupled Scholar transient/background task hardening in same generation wave, not separate deferral. |
| `V0-W3F-001` (R5) | D | STUDIO correct extractive study wording; no generative claim until capability exists. |
| `V0-W4-003` (R5/future) | D | STUDIO dormant confirmation broker remains out until a real safe command demands it. |
| `V0-UI-001` (R3/RV) | A then C | SUITE manual+executing semantic UI evidence, then each app's navigation/feature UI cutover evidence. |
| `V0-BLD-001` (R5) | D | TOOLS Skia and STUDIO Scholar warning debt; not a reason to broaden mandatory scope. |

**Ordering preservation:** R4 acceptance → R1A trustworthy baseline → R1B host lifecycle; R1C extension/process and R2A publisher establish safety → R1D/R1E/R2B/R2C/R2D/R2E by their stated dependencies → R3/RV final historical verification → only separately selected R5 debt. The V0 plan remains authoritative for wave scopes and approval. SUITE migration may be designed alongside this work, but no feature cutover precedes the relevant repair and a separate user-authorized migration phase. Do not mark old findings “fixed” merely because a new host omits a route.

## 13. Candidate vertical migration slice catalog

Each slice is proposed, **not authorized**. “Parity” means behavioral, negative/failure, offline and accessibility evidence, not visual resemblance or compilation. Rollback includes a non-mutated legacy read path and a clear single-writer transition.

| Slice / future host | Required dependencies and V0 prerequisites | Data/shared primitive; tests and legacy/new parity | Rollback; risk |
|---|---|---|---|
| **Flashcards → Studio** | Deck/page/VM, repaired speech coordinator; R4/R1A/R1B/R2D, relevant R2E UI/settings, R3 evidence | No proven durable deck store; user exports only. No mandatory Shared library. Test deck progression, CSV/Anki/JSON export, speech start/interrupt/completion, unavailable voice, keyboard/screen reader, offline behavior; compare old/new. | Keep legacy Flashcards available until proven. Unsaved in-memory state is not migratable; warn on cutover. **MODERATE**, recommended first. |
| **Resume → Studio** | Resume models/storage/ATS/PDF; R4/R1A/R1B, R2E settings, safe output contract where overwrite applies | Documents JSON + exports. Backup/read compatibility, import idempotency, corruption, file collision, PDF/ATS truth, two-route UI parity. | Preserve original Documents files and old reader; single writer at cutover. **HIGH**. |
| **Scholar → Studio** | Whole extraction/library/index/search/voice/OCR/scan/synthesis; R2C and R2D, then R2E/R3 | Multi-file index, `APPDATA` path inconsistency, optional model. Generation/corruption/crash/cancellation/restart/search/OCR fallback tests; legacy/new corpus and result parity. | Verified old generation and one writer; no in-place destructive migration. **VERY HIGH**. |
| **Voice → Studio** | Speech/transcriber/coordinator/router/settings/device; R2D/R2E | Audio/mic OS state, no data except settings; no shared runtime. Exact callback, cancellation, device unplug, permission denied, speech while dictating and safe route tests. | Disable new-host voice route and return to repaired legacy; settings backup. **HIGH**. |
| **Converter + Compressor → Tools** | Orchestrator/engines/compressor; R2A, R2E and R3 | User-selected outputs; narrow safe publisher candidate. Existing-destination fault injection, real image/PDF/text formats, metadata capability disclosure, queue cancellation and UI parity. | Old outputs preserved; legacy path remains available until cutover. **VERY HIGH** because overwrite defect. |
| **Batch Image → Tools** | Batch engine, WIC/Skia, optional ImageMagick/dependency manager; R1C/R2A/R2B | Many outputs, preset/collision/resource contracts; bounded process primitive only after proof. Mixed-format, unsupported-request, 16 GB peak-memory, process timeout, restart and output property tests. | No partial success claim; untouched source and reserved output rollback. **VERY HIGH**. |
| **Extensions/Download Manager → Tools** | Catalog, validator, downloader, installer, repair, cache, UI; R1C/R3 | Executable cache/install state. Trusted metadata fixture, tampered/signature/version negatives, timeout/partial download, failed replacement recovery, offline inventory parity. | Known-good install and cache backup; never delete first. **VERY HIGH/security**. |
| **Vault → Tools** | Vault/KDF/DPAPI/TPM/profile/UI; R1E/R2E | User-bound sealed key + user vault files. Old v1/v2 compatibility, tamper, collision, large-stream roundtrip, different-user/no-TPM, settings truth tests. | Preserve old key/format/decrypt path and encrypted originals. **VERY HIGH/security**. |
| **Mobile Link/QuickDrop → Tools** | P2P, device/transfer models, download dir, QR/drop UI; R1D/R2E | Fixed/configured path reconciliation and live LAN sessions. Beacon secrecy, replay/fragment/ACK, disconnect, port, unavailable network, firewall/user consent, actual file hash tests. | Stop new listener; legacy only after repaired protocol; no simultaneous port/session writer. **VERY HIGH/security**. |
| **W5 single-image editor → Studio (later)** | W5-P2 codec/architecture/rule/verification gates, V0-RV baseline; no current editor files | New non-destructive single-image state and safe export; Tools Batch remains separate. W5 matrix, metadata, alpha/frame, memory and preview-revision tests. | Original input untouched, output transactional; feature can be withheld. **HIGH/new capability**. |
| **Thin Mind host → Mind (later)** | New product/provider/privacy contracts, measured packaging, suite shell conventions | No existing conversation/model store to migrate and no Scholar Chat move. First-run/offline/optional-provider/privacy/credentials tests; no legacy feature parity claim. | Withhold app; no always-running service. **MODERATE new-product**, not a source migration slice. |

The grouping “Converter + Compressor” is a *candidate coordinated publisher wave*, not permission for a single giant UI move. A migration should cut over one coherent user journey at a time even when its safe publication primitive is shared.

## 14. Exactly one recommended first feature slice

**Recommend Flashcards → Studio, eventually, after the relevant V0 gates.** It exercises an independent WinUI host, page/route registration, per-app DI, speech capability unavailable-state, accessible controls, export paths and truthful parity without touching the Scholar library, user-bound Vault key, LAN protocol, executable installer or many-file conversion queue. Its current decks are seeded in memory and no durable deck database is proven, so there is less data migration than Resume/Scholar. This is not a claim of zero risk: direct speech use is a confirmed defect (`V0-FLS-001`), so V0-R2D coordination and V0-W4-002 playback lifetime must be repaired and proven first. The first cutover must keep speaking/export working, not quietly drop them. If R2D/R2E/R3 are not authorized or accepted, **no slice starts**.

**Definitely not first:** Vault (key/format and irreversible trust), Mobile Link (LAN session/secret disclosure), Extensions (executable trust), Scholar (multi-file index and path split), Converter/Compressor (existing-destination loss), Batch (memory/process/collision), or W5 (not implemented and codec proof still pending). Resume is the plausible second low-dependency candidate, but its user-authored Documents data and direct writes make it less safe as the first host proof.

## 15. Proposed eventual solution structure, not created

```text
src/
  Axora.Studio/       independent WinUI host; Scholar, Resume, Flashcards, voice, later W5
  Axora.Tools/        independent WinUI host; Converter, Batch, Compressor, Vault, LAN, Extensions
  Axora.Mind/         new thin WinUI host; new conversations/providers/models by contract
  Axora.Shared.Core/  ONLY admitted stateless results/path/serialization/publication contracts
tests/
  Axora.Studio.Tests/
  Axora.Tools.Tests/
  Axora.Mind.Tests/
  Axora.Shared.Core.Tests/
  Axora.Suite.IntegrationTests/   only real cross-app activation/schema tests
legacy/
  Axora.Desktop/      conceptual temporary compatibility host, not a path move in P1
```

The actual repo does **not** contain this tree. Initially, three apps plus **zero or one** tiny Shared.Core is preferable. Add `Axora.Shared.Platform` only when two apps need the same Windows adapter with a stable contract; add `Axora.Shared.UI` only after design tokens/accessibility controls prove reuse; add `Axora.AI.Contracts` only when Mind and Studio genuinely exchange a capability descriptor. These are future decisions, not mandatory scaffolding. Keep the legacy project/solution and QA executable until cutover evidence for each feature; retire it only under an explicit final migration/release plan. Avoid project-reference cycles and a “shared” package that transitively pulls ONNX, Skia, Argon2, QR, LAN or all feature VMs into every host.

## 16. Build, packaging and identity considerations

The current project is unpackaged, Windows App SDK self-contained, `win-x64`, x64, .NET 9 with one app identity/executable and host assets. Three runnable apps need distinct executable/process names, icons, activation verbs, application data roots, logs, crash identity, accessibility names, installer/update/uninstall entries, and code-signing/release policy. Whether to remain unpackaged or package each host is an **open product/release decision**; P1 does not change it. New hosts may duplicate Windows App SDK/native runtime payload even if code is shared, so inspect *published and installed* output rather than adding current Debug directory sizes. Consider optional deployment of DirectML/ONNX and Skia by feature, not an unproven runtime “deduplication” promise.

`Axora.Desktop.csproj` has a `KillRunningAppBeforeBuild` MSBuild target invoking PowerShell `Get-Process -Name 'Axora.Desktop' | Stop-Process -Force` before build. As written it targets only that exact process name; it will not *automatically* kill differently named Studio/Tools/Mind executables. **The hazard is copying/generalizing this target**, using a common executable name, or force-stopping unsaved old-host state. Future per-host builds should not inherit a broad AXORA process kill. Tests and build scripts need explicit host selections; `Axora.Desktop.sln` currently omits tests, while `build-all.ps1` builds app and test project separately. The unknown ZIP is not an install/publish artifact baseline.

## 17. Minimal future cross-app handoff requirements (no IPC design)

| Direction | Minimum likely handoff | Classification | Boundary |
|---|---|---|---|
| Studio → Tools | User explicitly sends selected exported PDF/image/document for conversion/compression | **PATH/FILE ACTIVATION** | Tools revalidates input/path, never treats Studio's filename as trust. No background broker. |
| Studio → Mind | User opts to ask about a selected excerpt/document or project report | **SMALL VERSIONED HANDOFF FILE** candidate | Explicit preview/redaction/consent; provenance and deletion, especially academic/private material. No automatic Scholar index sharing. If no concrete use case, **NO CURRENT NEED**. |
| Mind → Studio | User explicitly opens saved draft/citation/study artifact | **PATH/FILE ACTIVATION** candidate | Studio validates format/source; no direct access to Scholar store. Could remain **NO CURRENT NEED** until capability admitted. |
| Tools → Studio | User explicitly opens converted image/PDF as a Studio document | **PATH/FILE ACTIVATION** | Studio checks file type/limits, import is user initiated. |

URI activation may become useful after installation identity is settled, but **none is required by present source evidence**. Do not design an always-running broker, shared mutable database, universal clipboard daemon or direct cross-process service locator in P2 without a demonstrated user problem and safety contract.

## 18. Mind reuse and non-reuse boundary

Mind is largely greenfield. Legitimate possible reuse is limited to admitted typed results/path validation, a tiny versioned handoff envelope, accessible visual tokens/control patterns, and later an abstract *capability descriptor* when there is a real second consumer. A credential abstraction may be considered only after provider/storage/privacy semantics are specified; no existing Vault sealed key is Mind's credential store. Mind's model/capability manager is a core architectural capability per the product philosophy, but it must be modular, optional, offline-truthful and storage-measured. This is a future contract, not a reason to relocate `DirectMlEmbeddingEngine` or extension install code.

**Must not move to Mind:** Scholar `DocumentChatService`, local extraction/index/search/synthesis, `NullScholarSlmModelDriver`, Resume/ATS, voice coordinator as-is, Extension/Download Manager, Vault, P2P and QuickDrop. Scholar's “chat” operates on local study passages and optional embeddings; Mind's conversational/provider UX has different privacy, persistence, account, model and output expectations. Generative Image Studio concepts are optional future Mind capabilities; the deterministic single-image W5 editor is Studio, and Tools Batch remains Tools. No existing Mind data or provider implementation was found, so P1 cannot claim Mind feature parity.

## 19. Read-only footprint baseline and measurement gaps

The existing `Axora.Desktop/bin/x64/Debug/net9.0-windows10.0.26100.0/win-x64` directory was already present and measured without rebuild: **422 files, 174,667,081 bytes** (approximately 166.6 MiB). This is a **Debug build-output directory**, not published size, installed size or user download size. It includes PDBs and native dependencies and may include stale outputs. Source `Assets/` currently totals **9 files, 990,939 bytes**; `SplashScreen.png` is 393,259 bytes and embedded `vocab.txt` 231,508 bytes. No bundled `model.onnx` was found.

| Notable existing Debug file | Bytes | Interpretation |
|---|---:|---|
| `Microsoft.Windows.SDK.NET.dll` | 26,341,408 | Windows SDK projection, not a feature-specific model. |
| `DirectML.dll` | 18,527,776 | Optional Studio Scholar native GPU path currently present in monolith. |
| `onnxruntime.dll` | 16,031,776 | Optional embedding runtime currently bundled by package reference. |
| `Microsoft.ui.xaml.dll` | 14,386,248 | WinUI native payload. |
| `libSkiaSharp.dll` | 9,605,176 | Raster/PDF codec payload used across current features. |
| `DirectML.pdb` | 8,622,080 | Debug symbols; should not be equated with release deployment. |
| `Microsoft.WinUI.dll` | 7,270,432 | WinUI managed projection. |

**P3/P5/P6/P7 measurement contract:** record reproducible SDK/commit/configuration and target hardware, then measure each app's published bytes, installed bytes, shared runtime duplication, cold start, warm start, idle working set, feature working set and peak memory (especially Batch/W5 on a 16 GB integrated-GPU machine). Distinguish first-run model download from package payload; measure optional dependencies absent/present and offline startup. Also capture listener count, background CPU/network, handle count and graceful shutdown. Do not extrapolate three-app totals by simply tripling Debug output. No new build/test/performance run was done in P1.

## 20. Open decisions and SUITE-P2 inputs

**Open decisions for user/product authority:**

1. Exact per-app names/branding/IDs, executable process names, shell verb owner, tray/background expectation and install/update strategy.
2. Whether any visual preference is suite-synchronized; default recommendation is per-app settings, with no suite telemetry-consent inheritance.
3. One-time old-path migration mechanics and read-only legacy transition for Resume/Scholar/settings/Vault; backup retention and user-visible rollback window.
4. Definition of supported file formats/metadata and W5 codec proof; no hidden claim that Tools capability is Studio capability.
5. Whether/when Mind provider, local model and voice work solves an admitted user problem, under offline, privacy, dependency, storage and recoverability rules.
6. Unknown ZIP provenance and separate acceptance/investigation of attempted V0-R4/toolchain + broad test hang. P1 does not resolve them.

**Inputs for a separately authorized SUITE-P2:** accepted P0 and this P1 map; confirmed Git baseline; V0 wave authority/status and a trustworthy test ledger; one chosen first slice (Flashcards recommendation, subject to R2D/R2E); explicit host identity/packaging choice; per-app settings/data schema and single-writer transition contract; a parity/rollback matrix; real target-hardware footprint budget. P2 should convert these into bounded implementation authorization and acceptance gates, **not** assume P1 itself approves project creation. The User Problem First law remains the gate for every *new* capability: affected user, frequency/current workaround, pain removed, local/offline/optional dependency, storage, unavailable behavior, verification and undo/recovery must all be answered. Strategic Recovery/Migration, Integrity/Plagiarism, Voice, optional AI image generation, modular model management and Project/ZIP Analysis remain separately admitted future work, not implicit P2 scope.

**P1 exit statement:** This document maps current ownership, risk, sequencing and proposed slice boundaries. It makes no claim that V0-R4, R1A, V0-RV, W5-P2, any new host, any migrated data, or release parity is complete. Stop for user review.
