# AXORA Desktop — WinUI Product Contract & Architectural Specification
**Document Version**: 1.0.0 (Phase W0 Baseline)  
**Target Platform**: Windows 11 (x64) · .NET 9.0 (`net9.0-windows10.0.26100.0`) · Windows App SDK 1.6  
**Reference Implementation**: `Axora-Desktop-MaterialUI` (Tauri v2 + React 18 + Tailwind MD3 + Rust)  
**Implementation Project**: `Axora-Desktop-WinUI` (WinUI 3 + XAML + C# 13 + CommunityToolkit.Mvvm)  

---

## 1. Executive Summary & Program Directives

### 1.1 Core Mandate
The MaterialUI implementation serves as the **PRODUCT REFERENCE** for:
- User-facing capabilities
- Expected workflows & interactions
- Information architecture & taxonomy
- Terminology & user mental model
- Functional intent & outcome expectations

It is **NOT a source-code template**. Under no circumstances should web/Tauri paradigms (e.g. DOM event listeners, raw HTML strings, IPC JSON bridges, React hook chains) be ported into WinUI.

WinUI must remain a strictly native Windows desktop implementation:
- **Framework**: .NET 9 + Windows App SDK 1.6.250228001
- **UI Architecture**: WinUI 3 XAML with `MicaBackdrop` (`MicaKind.BaseAlt`), native visual states, and Segoe UI Variable typography
- **State Pattern**: MVVM Toolkit 8.4 (`ObservableObject`, field-backed `[ObservableProperty]`, `[RelayCommand]`)
- **Composition Root**: Microsoft.Extensions.Hosting dependency injection
- **Platform Integration**: Direct Win32 / WinRT APIs (COM interop, Windows.Media, Windows.Security.Cryptography, DirectML/ONNX Runtime, WIA scanner integration)

### 1.2 Baseline Stability
Existing foundation infrastructure in `Axora-Desktop-WinUI`:
- Shell window with custom tall title bar and `WM_GETMINMAXINFO` clamping (1000x620 DIP minimum)
- 19 service contracts and singleton dependency injection
- Native multi-tier file picker helper with WinRT and Win32 fallback
- Vector PDF compilation via PdfSharpCore and ATS optimization engine
- Streaming AES-256-GCM vault encryption with TPM 2.0 / DPAPI sealing
- WIA flatbed scanner COM interop and WinRT OCR engine
- DirectML / SIMD-accelerated semantic RAG embeddings
- 59 adversarial stress assertions in `Axora.Desktop.Tests`
- Automated UI automation runner via Windows UIA in `scripts/qa/`

This infrastructure will be stabilized, integrated, and extended—never casually rewritten.

---

## 2. Comprehensive Feature Parity Matrix (Phase 1)

Every major AXORA capability has been inspected against both codebases. Classifications strictly follow the program taxonomy: `COMPLETE`, `PARTIAL`, `MISSING`, `NOT APPLICABLE`, `BLOCKED`.

| # | Feature / Capability | MaterialUI Reference | WinUI Equivalent | Functional Parity | Visual Parity | State Parity | Persistence Parity | A11y Parity | Automated Test Coverage | Missing Pieces & Strategy | Priority |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **1** | **Dashboard / Workspace Hub** | `src/pages/Dashboard.tsx` (`ping_backend`, 6 quick action cards, system compatibility modal, hero CTA) | `Views/DashboardPage.xaml`, `ViewModels/DashboardViewModel.cs` | `PARTIAL` | `PARTIAL` | `COMPLETE` | `COMPLETE` | `PARTIAL` | `COMPLETE` | WinUI has live telemetry (CPU, RAM, Disk, P2P) and QuickDrop feed, but lacks quick action cards to navigate to other tools. Needs navigation action cards and System Info dialog. | **P1** |
| **2** | **Universal Engine / Converter** | `src/pages/Converter.tsx`, `src-tauri/src/processor.rs` (`convert_files` for PDF, Office docs, Images) | `Services/DocumentProcessorService.cs` (backend only; no dedicated Page or VM) | `MISSING` | `MISSING` | `MISSING` | `MISSING` | `MISSING` | `PARTIAL` (Backend only) | Dedicated `Views/ConverterPage.xaml` and `ViewModels/ConverterViewModel.cs` must be created to expose multi-format conversion to users. | **P0** |
| **3** | **Scholar Kit** | `src/pages/Academic.tsx` (OCR, LaTeX Notes, PDF Compressor, Redactor, PDF Surgeon page reordering) | `Views/ScholarKitPage.xaml`, `ViewModels/ScholarKitViewModel.cs` | `PARTIAL` | `PARTIAL` | `COMPLETE` | `PARTIAL` | `PARTIAL` | `COMPLETE` | WinUI has superior OCR, Speech Lab (dictation & TTS), and RAG chat, but lacks PDF Surgeon (visual page reordering) and PDF Redaction tools in the UI (backend `PdfAnnotationService` exists but is disconnected). | **P1** |
| **4** | **Resume Studio / Form Studio** | `src/pages/FormStudio.tsx` (Bureaucrat tools: Target Resizer, Signature Extractor, ID Card Stitcher, PDF Builder) | `Views/ResumeStudioDashboardPage.xaml`, `Views/ResumeStudioPage.xaml`, `ViewModels/ResumeStudioViewModel.cs` | `PARTIAL` | `COMPLETE` | `COMPLETE` | `COMPLETE` | `PARTIAL` | `COMPLETE` | WinUI has an advanced ATS Resume Studio with vector PDF preview and auto-save library, but lacks government form tools (Target Resizer to exact KB, Signature Extractor, ID Card Stitcher). | **P1** |
| **5** | **Flashcard Studio** | `src/pages/FlashcardStudio.tsx`, `src-tauri/src/commands/anki.rs` (SM-2 review, 3D flip card, retention curve, Anki export) | `Views/FlashcardsPage.xaml`, `ViewModels/FlashcardsViewModel.cs` | `COMPLETE` | `COMPLETE` | `COMPLETE` | `MISSING` | `PARTIAL` | `COMPLETE` | Algorithmic SM-2, card flip, and TTS pronunciation are complete. Decks are hardcoded in constructor; requires disk persistence to `%APPDATA%\Axora\Decks\*.json` and visual retention curve. | **P2** |
| **6** | **Batch Image Studio** | `src/pages/BatchProcessor.tsx` (Quality slider, format dropdown, determinate progress) | `Views/BatchImagePage.xaml`, `ViewModels/BatchImageViewModel.cs`, `Services/BatchImageProcessorService.cs` | `COMPLETE` | `COMPLETE` | `COMPLETE` | `NOT APPLICABLE` | `PARTIAL` | `COMPLETE` | WinUI surpasses MaterialUI (dual WIC/ImageMagick engine, sizing presets, watermark, multi-threaded queue). Missing: graceful fallback when `magick.exe` is absent from PATH. | **P2** |
| **7** | **Intelligent Compressor** | Embedded in `Converter.tsx` and `Academic.tsx` (`compress_pdf_multi_tier`) | `Views/CompressorPage.xaml`, `ViewModels/CompressorViewModel.cs`, `Services/IntelligentCompressorService.cs` | `COMPLETE` | `COMPLETE` | `COMPLETE` | `NOT APPLICABLE` | `PARTIAL` | `COMPLETE` | Multi-profile compression (Low, Medium, High), live telemetry KPI cards, queue management, and output folder reveal are complete. | **P2** |
| **8** | **Encrypted Vault** | `src/pages/Security.tsx`, `src-tauri/src/vault.rs` (Argon2id + AES-256-GCM file encryption) | `Views/VaultPage.xaml`, `ViewModels/VaultViewModel.cs`, `Services/StreamingVaultService.cs` | `COMPLETE` | `COMPLETE` | `COMPLETE` | `COMPLETE` | `PARTIAL` | `COMPLETE` | Streaming 1MB blocks, TPM 2.0 / DPAPI machine sealing, memory wiping, and corrupted ciphertext rejection complete. Missing: password confirmation validation dialog. | **P2** |
| **9** | **Bureaucrat / Document Tools** | `src/pages/FormStudio.tsx` (Resizer, Signature Extractor, ID Card Stitcher, Stamp Isolator, BG Remover) | `Services/DocumentProcessorService.cs` (PDF image packaging only; no UI) | `MISSING` | `MISSING` | `MISSING` | `MISSING` | `MISSING` | `MISSING` | Missing dedicated view and ViewModels for official document preparation (Target Resizer to exact KB, ink signature extractor, ID card A4 stitcher). | **P1** |
| **10** | **Media / Audio & Snippets** | `src/pages/Media.tsx` (Video-to-audio stripper, Whisper transcription, Encrypted Snippet Vault with Alt+Shift+V) | `Services/VoiceTranscriberService.cs` (live dictation only; no media stripper or snippet vault) | `PARTIAL` | `MISSING` | `MISSING` | `MISSING` | `MISSING` | `MISSING` | Missing video-to-audio extraction (Windows Media Foundation), audio file transcription, and persistent code snippet vault with global accelerator overlay. | **P2** |
| **11** | **Scanner (Hardware Capture)** | `src/pages/Scanner.tsx`, `src-tauri/src/scanner.rs` (Dedicated page, device selector, DPI, flatbed preview) | `Services/WiaScannerService.cs` (accessed only as button inside `ScholarKitPage.xaml`) | `PARTIAL` | `PARTIAL` | `PARTIAL` | `NOT APPLICABLE` | `PARTIAL` | `PARTIAL` (Manual only) | WIA COM service is implemented, but WinUI lacks a dedicated Scanner page, scanner device selector dropdown, resolution controls, and scan preview canvas. | **P2** |
| **12** | **Mobile Link / QuickDrop** | `src/pages/MobileLink.tsx`, `src-tauri/src/network/` (mDNS, QR pairing, Axum WebSocket server) | `Views/MobileLinkPage.xaml`, `ViewModels/MobileLinkViewModel.cs`, `Services/P2pSyncService.cs`, `Controls/FloatingDropWidget.xaml` | `COMPLETE` | `COMPLETE` | `COMPLETE` | `COMPLETE` | `PARTIAL` | `COMPLETE` | Complete native ECDH NIST P-256 handshake + AES-256-GCM WebSocket file receiver into `%USERPROFILE%\Downloads\Axora_QuickDrop\`, plus floating desktop widget. | **P2** |
| **13** | **Settings** | `src/pages/Settings.tsx` (Theme switcher, color swatches, download dir, clear cache) | `Views/SettingsPage.xaml`, `ViewModels/SettingsViewModel.cs`, `Services/AppSettingsService.cs` | `COMPLETE` | `COMPLETE` | `COMPLETE` | `COMPLETE` | `PARTIAL` | `COMPLETE` | Persists to `%APPDATA%\Axora\settings.json`. Floating save/revert pill when `IsDirty=true`. Critical Gap: Theme changes are saved to disk but **never propagate to the live UI**. | **P0** |
| **14** | **Command Palette** | `src/components/CommandPalette.tsx` (`Ctrl+K` overlay, instant filtering, keyboard navigation) | `Controls/CommandPaletteDialog.xaml`, `MainWindow.cs` (`Ctrl+K` accelerator) | `COMPLETE` | `COMPLETE` | `COMPLETE` | `NOT APPLICABLE` | `COMPLETE` | `COMPLETE` | AutoSuggestBox filtering 12+ commands and navigation routes, dim acrylic backdrop, Escape dismiss, and keyboard navigation fully functional and verified via UIA. | **P3** |
| **15** | **Global Drag & Drop** | `src/components/FileDropZoneOverlay.tsx` (Full-window drop overlay with blur) | `Controls/FileDropZoneOverlay.xaml`, `MainWindow.cs` (`AllowDrop=true`, drop routing) | `COMPLETE` | `COMPLETE` | `COMPLETE` | `NOT APPLICABLE` | `COMPLETE` | `PARTIAL` | Window-level drop overlay renders dashed border; drops currently route to ScholarKit and Vault. Needs routing expansion to Converter, BatchImage, and Compressor. | **P2** |
| **16** | **Theme Switching & Backdrops** | `src/store/themeStore.ts` (Dynamic CSS custom properties switching MD3 light/dark tokens) | `MainWindow.cs` (`MicaBackdrop`), `Services/AppSettingsService.cs` | `PARTIAL` | `PARTIAL` | `PARTIAL` | `COMPLETE` | `PARTIAL` | `PARTIAL` | Mica Alt backdrop works. But `RequestedTheme` is never modified on `MainWindow.Content` or `ApplicationPageBackgroundThemeBrush` at runtime when settings change. | **P0** |
| **17** | **Keyboard Shortcuts** | `Ctrl+K` (Command Palette), `Alt+Shift+V` (Snippet Vault), `Escape` (Dismiss) | `MainWindow.cs` (`Ctrl+K`, `Ctrl+\`, `Escape`, `Space` for cards, `Ctrl+Z`/`Ctrl+Y` undo) | `PARTIAL` | `COMPLETE` | `COMPLETE` | `NOT APPLICABLE` | `COMPLETE` | `COMPLETE` | `Ctrl+K` works; `Ctrl+\` accelerator is incorrectly bound to `VirtualKey.Back` (Backspace) instead of backslash; `Alt+Shift+V` snippet overlay is missing. | **P1** |
| **18** | **Notifications / Toasts** | `src/components/ToastNotification.tsx`, `src/store/toastStore.ts` (Global toast chip manager) | `Services/TrayService.cs` (Windows balloon notifications only) | `PARTIAL` | `PARTIAL` | `PARTIAL` | `NOT APPLICABLE` | `PARTIAL` | `PARTIAL` | WinUI lacks in-app floating banner toasts or `InfoBar` host in `ShellView.xaml`. User feedback relies on inline text or OS taskbar notifications. | **P1** |
| **19** | **Persistence Model** | Settings in `settings.json`, Theme in `localStorage`, Snippets in encrypted vault | Settings in `%APPDATA%\Axora\settings.json`, Resumes in Documents library | `PARTIAL` | `NOT APPLICABLE` | `PARTIAL` | `PARTIAL` | `NOT APPLICABLE` | `COMPLETE` | Settings and Resumes persist cleanly. Flashcard decks, study intervals, and RAG document indexes are in-memory only and lost on application exit. | **P1** |
| **20** | **Error, Empty, Loading & Success States** | Illustrated empty states, spinner states (`Loader2`), non-destructive error banners | `Views/*.xaml` (`ProgressRing`, `CardBackgroundFillColorDefaultBrush`, inline error texts) | `COMPLETE` | `COMPLETE` | `COMPLETE` | `NOT APPLICABLE` | `PARTIAL` | `COMPLETE` | Empty states in Resume Studio Dashboard and Dashboard QuickDrop; `ProgressRing` on async actions; inline error texts. Needs unified InfoBar overlay. | **P1** |

---

## 3. Actual WinUI Codebase Inspection (Phase 2)

A rigorous audit of `Axora-Desktop-WinUI/` across Views, ViewModels, Services, Models, Helpers, Controls, and Converters reveals the following architectural realities:

### 3.1 Navigation Lifecycle & Frame Hosting
- **Shell Structure**: `MainWindow.cs` hosts a custom non-client titlebar (36 DIP height) and `ShellView.xaml` (starts at Margin `0,36,0,0`).
- **Frame Navigation**: `ShellView.xaml` embeds a WinUI 3 `NavigationView` whose `ContentFrame` navigates to target pages (`ContentFrame.Navigate(pageInfo.PageType)`).
- **Backstack Discipline**: `IsBackButtonVisible` is set to `Collapsed`. Navigation does not leverage or manage `ContentFrame.BackStack`. Pages such as `ResumeStudioPage` attempt to navigate back to `ResumeStudioDashboardPage` by calling `Frame.Navigate(typeof(ResumeStudioDashboardPage))` again, pushing duplicate instances into memory rather than popping the stack.
- **Auto-Pane Collapse**: `ShellView.xaml.cs` dynamically collapses `IsPaneOpen = false` when navigating to `ResumeStudioPage` to maximize editing canvas space, and restores it on return.

### 3.2 ViewModel Ownership & Singleton Memory Behavior
- **DI Registration**: All 10 ViewModels (`ShellViewModel`, `DashboardViewModel`, `ScholarKitViewModel`, `ResumeStudioViewModel`, `BatchImageViewModel`, `CompressorViewModel`, `VaultViewModel`, `FlashcardsViewModel`, `MobileLinkViewModel`, `SettingsViewModel`) are registered as **Singletons** in `App.xaml.cs`.
- **Page Resolution**: Each Page instantiates its `ViewModel` via `App.GetService<TViewModel>()` in its code-behind property initializer.
- **Lifecycle Conflict**: WinUI Pages are transient visual elements recreated on each `Frame.Navigate` call. Because the ViewModels are Singletons, any strong event subscriptions made by the Page (e.g., `ViewModel.PropertyChanged += ...` in `ScholarKitPage` and `ResumeStudioPage`) permanently anchor the discarded Page instances in memory. Discarded pages are never garbage-collected, creating a cumulative leak of XAML visual trees, timer delegates, and text buffers.

### 3.3 Service Boundaries & Infrastructure Isolation
- **Contracts**: 19 interface contracts exist in `Axora.Desktop/Services/Contracts/`. Every concrete service implements its dedicated contract.
- **Unconnected Services**: `IPdfAnnotationService` (PdfSharpCore redactions, vector overlays) is registered in DI as a singleton (`PdfAnnotationService`), but is **injected into zero ViewModels**. Neither ScholarKit nor Compressor utilizes it, leaving powerful redaction and annotation capabilities completely dormant.
- **WIA Scanner Service**: `IScannerService` is registered in DI and injected into `ScholarKitViewModel`, but is wired only to a small flatbed scan button without scanner device selection or preview capabilities.

### 3.4 Persistence Model
- **File Locations**:
  - Preferences: `%APPDATA%\Axora\settings.json` via `System.Text.Json` in `AppSettingsService.cs`.
  - Resumes: `%USERPROFILE%\Documents\Axora\Resumes\*.json` via `ResumeStorageHelper.cs`.
  - Sealed Vault Keys: `%APPDATA%\Axora\vault_sealed.dat` via `StreamingVaultService.cs`.
  - QuickDrop Ingest: `%USERPROFILE%\Downloads\Axora_QuickDrop\` via `P2pSyncService.cs`.
- **Unpersisted Domains**:
  - Flashcard decks and SM-2 retention metrics in `FlashcardsViewModel.cs`.
  - Ingested document text and RAG vector embeddings in `DocumentChatService.cs`.

### 3.5 Threading, Async Operations & Dispatcher Marshalling
- **UI Dispatching**: Services that raise events for the UI (`VoiceTranscriberService`, `P2pSyncService`, `ScholarKitViewModel`) utilize `DispatcherQueue.GetForCurrentThread()` or `DispatcherHelper` to marshal data back to the UI thread.
- **CPU Offloading**: Cryptography (`StreamingVaultService`), vector embeddings (`DirectMlEmbeddingService`), OCR (`WinRtOcrService`), and PDF compilation (`ResumePdfCompilerService`) appropriately execute inside `Task.Run` worker threads with cancellation token support.
- **UI Thread Bottleneck**: In `ResumeStudioDashboardPage.xaml.cs`, `SearchBox_TextChanged` synchronously invokes `Directory.GetFiles` and `File.ReadAllText` on the UI thread for every keystroke.

---

## 4. Concrete Architectural Risks & Findings (Phase 3)

The following 16 architectural risks represent concrete issues identified in the repository:

### 1. Singleton ViewModels Retaining Stale Page Delegates
- **Finding**: In `ScholarKitPage.xaml.cs` (lines 25-26) and `ResumeStudioPage.xaml.cs` (lines 36-37, 61-66), Pages subscribe to `ViewModel.PropertyChanged` and `ViewModel.Document.PropertyChanged` without unsubscribing on `Unloaded` or `OnNavigatedFrom`.
- **Impact**: Every time the user navigates between pages, a new Page is instantiated and hooked into the singleton ViewModel's multicast delegate. The old Page instances remain pinned in memory indefinitely. Keystrokes in `ResumeStudioPage` trigger multiple duplicate auto-save timers (`_autoSaveTimer`), causing race conditions and disk thrashing.
- **Remediation**: Implement strict `Unloaded` / `OnNavigatedFrom` teardown in all Pages, or replace direct delegate hooks with weak event handlers (`WeakEventManager` or MVVM Toolkit `Messenger`).

### 2. AppHost Lifetime & Graceful Process Termination
- **Finding**: In `App.xaml.cs` (line 125), `_ = AppHost.StartAsync();` is dispatched during `OnLaunched`. However, `AppHost.StopAsync()` and `AppHost.Dispose()` are never invoked anywhere in the application lifecycle.
- **Impact**: When the user closes the main window, the process terminates abruptly without giving DI-hosted services an opportunity to flush buffers, release file locks, or gracefully terminate worker loops.
- **Remediation**: Subclass `MainWindow` closing events to await `AppHost.StopAsync()` before disposing the host.

### 3. Background Service Shutdown
- **Finding**: `P2pSyncService.cs` launches background tasks (`AcceptLoopAsync` and `MdnsBroadcastLoopAsync`) linked to `_serverCts`.
- **Impact**: When the window is closed, `P2pSyncService.StopAsync()` is not called. If background threads are executing async socket calls without `IsBackground = true` or cancellation triggers, the process can hang as an invisible zombie in Task Manager.
- **Remediation**: Register `IHostedService` lifecycle hooks in `AppHost` and cancel tokens on application shutdown.

### 4. P2P Startup, Shutdown & Port Binding
- **Finding**: When `AutoStartP2pEngine` is true, `P2pSyncService.StartAsync()` binds to dynamic port 0 or user-specified port 5050.
- **Impact**: If an unhandled exception or abrupt crash terminates the application while active WebSockets are open, the port can linger in `TIME_WAIT`, causing subsequent starts to fail if bound to a fixed port.
- **Remediation**: Ensure `SO_REUSEADDR` / `ExclusiveAddressUse = false` on listener sockets and wrap server lifecycle in robust try/finally cancellation.

### 5. Native File Picker Threading & UI Blocking
- **Finding**: `NativeFilePickerHelper.RunOnUiThreadAsync` forces picker execution onto the UI dispatcher. In Tier 3 fallback (`ShowLegacyOpenFileDialog` / `GetOpenFileName`), Win32 modal dialogs enter synchronous modal loops on the UI thread.
- **Impact**: The UI thread is blocked during legacy modal dialog display, preventing background XAML animations from updating. Additionally, WinRT pickers throw COM exceptions if `*` is passed alongside specific file extensions.
- **Remediation**: Preserve the extension sanitizer (`ExtractExtensionsFromFilter`) and isolate legacy Win32 modal dialog calls to dedicated STA background threads if needed.

### 6. UI-Thread Blocking on Disk I/O
- **Finding**: In `ResumeStudioDashboardPage.xaml.cs` (lines 72-134), typing inside `SearchBox` synchronously reads every saved JSON resume from disk via `File.ReadAllText(path)` directly on the UI dispatcher thread.
- **Impact**: Typing causes severe UI stutter, frame drops, and sluggishness as the user's resume collection grows.
- **Remediation**: Debounce search input (300ms) and offload disk reads and JSON parsing to `Task.Run`, updating the collection via a diffing helper on `DispatcherQueue`.

### 7. Filesystem Access & Path Robustness
- **Finding**: File paths in `ResumeStorageHelper` (`%USERPROFILE%\Documents\Axora\Resumes`) and `AppSettingsService` (`%APPDATA%\Axora`) assume standard local storage.
- **Impact**: In enterprise or roaming Windows setups where Documents is redirected to a network UNC share (`\\server\share`) or OneDrive Known Folder Move with files on-demand, synchronous I/O can hang indefinitely if the network is saturated.
- **Remediation**: Wrap all directory creation and file checks in timeout-bounded async tasks with fallback to local AppData.

### 8. Database Lifecycle & Missing Persistence for RAG and Flashcards
- **Finding**: RAG passage chunks and vector embeddings in `DocumentChatService.cs` (`_passageIndex`) and study decks in `FlashcardsViewModel.cs` are maintained strictly in-memory.
- **Impact**: Indexing a 50-page research paper consumes hundreds of megabytes of RAM, and the index is completely lost when the app closes. Users must re-index every time they launch. Flashcard reviews are similarly reset.
- **Remediation**: Introduce a lightweight local embedded database (SQLite via `Microsoft.Data.Sqlite` or local JSON repository) with schema migrations for persistent decks, SM-2 retention curves, and vector chunk caches.

### 9. DirectML Resource Lifecycle & D3D12 Device Loss
- **Finding**: `DirectMlEmbeddingService.cs` instantiates an ONNX Runtime `InferenceSession` configured with `AppendExecutionProvider_DML(deviceId: 0)`.
- **Impact**: If the user's laptop switches GPUs (e.g. NVIDIA Optimus switching from discrete GPU to integrated graphics) or sleeps/resumes, the DirectML execution provider can encounter `DXGI_ERROR_DEVICE_REMOVED`, permanently breaking embedding generation until app restart.
- **Remediation**: Wrap `_session.Run()` in device-loss exception guards that automatically dispose the invalid session and reinitialize on CPU fallback.

### 10. OCR Resource Lifecycle & WinRT Stream Disposal
- **Finding**: In `WinRtOcrService.cs` (lines 64-65), `using var raStream = imageStream.AsRandomAccessStream();` wraps the caller's input stream. Disposing `raStream` can close the underlying `imageStream`.
- **Impact**: If a caller (such as `ScholarKitViewModel`) passes an open stream intended for secondary processing, the stream is unexpectedly disposed, causing `ObjectDisposedException` down the pipeline.
- **Remediation**: Decouple streams by cloning or copying input bytes into an independent `InMemoryRandomAccessStream` before passing to `BitmapDecoder`.

### 11. WIA Scanner COM Object Leaks
- **Finding**: In `WiaScannerService.cs` (lines 146-156), dynamic COM object `scannerItem = targetDevice.Items[1]` and enumeration property items are not released via `Marshal.ReleaseComObject` in the `finally` block. Only `scannedImage`, `targetDevice`, and `deviceManager` are released.
- **Impact**: The scanner hardware driver remains locked in memory by the COM runtime. Scanning multiple pages consecutively fails with `WIA_ERROR_BUSY` (0x80210006) until the entire application is closed.
- **Remediation**: Add explicit `Marshal.ReleaseComObject(scannerItem)` and property cleanup guards in `WiaScannerService.cs`.

### 12. Process Spawning Security & Missing Executable Handling
- **Finding**: `BatchImageProcessorService.cs` executes `Process.Start("magick", ...)` without verifying whether ImageMagick is installed or accessible in `PATH`.
- **Impact**: If a user selects the ImageMagick engine on a clean system, `Process.Start` throws `Win32Exception` ("The system cannot find the file specified"), causing all batch jobs to fail without explaining how to install ImageMagick or falling back to WIC.
- **Remediation**: Perform a path probe (`which` / file existence check) before spawning, and provide an automated fallback to WIC with an informative UI notification.

### 13. Unhandled Exception Boundaries
- **Finding**: `App.xaml.cs` hooks `UnhandledException` on the WinUI `Application` class, but does not subscribe to `AppDomain.CurrentDomain.UnhandledException` or `TaskScheduler.UnobservedTaskException`.
- **Impact**: Exceptions occurring on background pool threads (e.g. during socket read in `P2pSyncService` or unobserved tasks in `DirectMlEmbeddingService`) crash the application instantly without logging to `startup.log` or displaying user-facing error feedback.
- **Remediation**: Hook `AppDomain.CurrentDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` in `Program.cs` / `App.xaml.cs` to write diagnostic crash dumps.

### 14. Window Lifecycle & System Tray Orphan Leaks
- **Finding**: `TrayService.Initialize(hwnd)` adds the tray icon via `Shell_NotifyIconW(NIM_ADD)`. However, `MainWindow` does not handle `Closed` or `AppWindow.Closing` to invoke `TrayService.Remove()`.
- **Impact**: When the user exits the app, an orphaned icon lingers in the Windows notification area until the user hovers over it with the cursor.
- **Remediation**: Hook `MainWindow.Closed` to guarantee `TrayService.Remove()` execution.

### 15. Navigation Backstack & Route State Inconsistency
- **Finding**: In `ShellView.xaml.cs`, `NavigateTo` checks `!ContentFrame.CanGoBack`. When `ResumeStudioPage` calls `Frame.Navigate(typeof(ResumeStudioDashboardPage))`, `CanGoBack` becomes true, causing subsequent clicks on the "Resume Studio" nav item to fail to switch views.
- **Impact**: Navigation between the resume editor and dashboard becomes desynchronized; nav rail highlights don't match active view.
- **Remediation**: Implement a unified navigation coordinator with explicit backstack popping (`Frame.GoBack()`) when returning to parent views.

### 16. Theme Propagation Disconnect
- **Finding**: `SettingsPage.xaml` binds theme selection to `SettingsViewModel.SelectedThemeIndex` and persists to `AppSettingsService.ThemeIndex`. However, **zero code anywhere in the project** subscribes to `AppSettingsService.PropertyChanged` to apply `RootElement.RequestedTheme = ElementTheme.Dark/Light`.
- **Impact**: Changing the theme in Settings modifies `settings.json` on disk, but has **zero visual effect** on the running window.
- **Remediation**: Implement a dedicated `ThemeService` that subscribes to `AppSettingsService` and sets `MainWindow.Content.RequestedTheme` and dynamic accent color resources on the fly.

---

## 5. Prioritized Native WinUI Implementation Roadmap (Phase 5)

Based on actual repository dependencies, foundational architectural risks, and user-facing value, the implementation order is structured as follows:

```
+-------------------------------------------------------------------------------+
| FOUNDATION PHASE (M0): ARCHITECTURAL INTEGRITY & SHELL LIFECYCLE              |
| - Fix Window.Closed, TrayService.Remove(), AppHost.StopAsync()                |
| - Fix Page Navigation Event Listener Leaks & Auto-Save Timers                 |
| - Implement Live Theme & Accent Propagation (RequestedTheme)                  |
| - Fix Navigation Keyboard Accelerator (Ctrl+\ Backspace mapping)              |
| - Hook AppDomain.UnhandledException and UnobservedTaskException               |
| - Implement Global In-App InfoBar / Toast Notification Host                   |
+-------------------------------------------------------------------------------+
                                        |
                                        v
+-------------------------------------------------------------------------------+
| PHASE M1: CORE ENGINE & FLAGSHIP CONVERTER (P0)                               |
| - Create Views/ConverterPage.xaml and ViewModels/ConverterViewModel.cs        |
| - Connect DocumentProcessorService & BatchImageProcessorService to UI         |
| - Format Grid, Drag/Drop Queue, Determinate Conversion Progress, Output Open  |
+-------------------------------------------------------------------------------+
                                        |
                                        v
+-------------------------------------------------------------------------------+
| PHASE M2: SCHOLAR KIT COMPLETION & ANNOTATION ENGINE (P1)                     |
| - Expose IPdfAnnotationService (Redactions & Vector Overlays) in ScholarKit   |
| - Implement PDF Surgeon (Visual Page Thumbnail Reorder Grid, Rotate, Delete)  |
| - Add Search Query Debouncing and Memory-Safe Stream Handling in OCR          |
+-------------------------------------------------------------------------------+
                                        |
                                        v
+-------------------------------------------------------------------------------+
| PHASE M3: BUREAUCRAT / OFFICIAL FORM STUDIO (P1)                              |
| - Create Views/FormStudioPage.xaml and ViewModels/FormStudioViewModel.cs      |
| - Implement Target KB Resizer (Binary-Search JPEG Compression)                |
| - Implement Signature Extractor (Ink Isolation, Transparent PNG Export)       |
| - Implement ID Card Stitcher (Front/Back onto A4 PDF Canvas)                  |
+-------------------------------------------------------------------------------+
                                        |
                                        v
+-------------------------------------------------------------------------------+
| PHASE M4: DATA PERSISTENCE & EXTENDED STUDIOS (P2)                            |
| - Implement Flashcard Deck Disk Persistence (%APPDATA%\Axora\Decks\*.json)    |
| - Add Visual SVG/Canvas Retention Curve in Flashcard Studio                   |
| - Clean up WIA COM Leaks in WiaScannerService and Add Scanner Device Dialog   |
| - ImageMagick Executable Probe & Graceful WIC Fallback in Batch Image Studio  |
+-------------------------------------------------------------------------------+
                                        |
                                        v
+-------------------------------------------------------------------------------+
| PHASE M5: MEDIA FORGE & SNIPPET VAULT OVERLAY (P2)                            |
| - Implement Windows Media Foundation Audio Stripper (MP4 -> MP3/WAV)          |
| - Implement Encrypted Code Snippet Vault with Alt+Shift+V Global Overlay      |
| - Local Audio File Transcription Integration                                  |
+-------------------------------------------------------------------------------+
```

---

## 6. QA Validation Requirements (Phase 6)

Every future feature merged into `Axora-Desktop-WinUI` must satisfy the strict 5-dimensional QA gate:

1. **Logic Test**: Automated unit/stress assertion in `Axora.Desktop.Tests` verifying data transformations, math, cryptographic boundaries, and state machines with zero UI thread dependencies.
2. **UI Automation Test**: Windows UI Automation script in `scripts/qa/` using `System.Windows.Automation` to locate controls by `AutomationId` / `AutomationProperties.Name`, execute patterns (`InvokePattern`, `SelectionItemPattern`, `TogglePattern`, `ValuePattern`), and assert resulting UI state transitions.
3. **Visual Check**: Adherence to the 25-Point Visual Audit (multiples of 4/8px grid alignment, 16/24px padding, zero text clipping at 1000x620 DIP, dark/light contrast legibility), verified with screenshot evidence in `docs/qa/screenshots/`.
4. **Accessibility (a11y) Check**: All interactive elements have descriptive `AutomationProperties.Name`, keyboard tab order flows logically, minimum 32x32 DIP touch/click bounds, and focus visuals are distinct.
5. **Negative Path Test**: Explicit automated test of error states (empty inputs, non-existent files, 0-byte corrupt files, cancelled operations, disconnected hardware peripherals) proving the application remains stable and presents clear feedback.

---

## 7. Phase W1: Native Foundation Hardening Completion Audit

**Execution Date**: September 4, 2026  
**Status**: **COMPLETED & VERIFIED**  
**Engineering Baseline**: Baseline 4 Native Foundation Hardened  

### 7.1 Scope & Directives Delivered
Phase W1 hardened the native WinUI 3 foundation to guarantee that all subsequent feature implementations (Universal Converter, Scholar Kit enhancements, Form Studio, etc.) inherit a resilient, memory-safe, observable, and theme-reactive runtime.

### 7.2 Remediated Architectural Risks
| # | Foundation Domain | Identified Defect / Risk | Remediated Architectural Solution | Verification Status |
|---|---|---|---|---|
| **1** | **Theme Propagation** | `RequestedTheme` and dynamic accent brushes never updated runtime XAML elements when changed in Settings. | Implemented `ThemeService` implementing `IThemeService`. Injected into `SettingsViewModel`, dynamically applies `ElementTheme` to `MainWindow.Content`, updates `AccentColorBrush` / `AccentColorTertiaryBrush`, and forces visual tree `{ThemeResource}` re-evaluation. | **TESTED** (Automated W1.1a-f, UIA Flow W-01) |
| **2** | **App & Process Lifecycle** | Window closure abandoned background tasks in `P2pSyncService`, leaked tray icons, and omitted `AppHost.StopAsync()` / `Dispose()`. | Replaced leaky shutdown flag with thread-safe singleton `_shutdownTask` pattern. `MainWindow.Closed` and `Program.Main` `finally` coordinate clean cancellation, socket shutdown, `TrayService.Remove()`, and `AppHost.StopAsync()`. | **TESTED** (Smoke test PID termination, W1.4a-d) |
| **3** | **Event & Timer Memory Leaks** | Pages hooked singleton ViewModel events and timers without `Unloaded` cleanup, pinning discarded page visual trees in memory. | Hardened `ResumeStudioDashboardPage` debouncer timer with reusable instance and `Unloaded` teardown. Implemented `HookDocument` / `UnhookDocument` in `ResumeStudioPage` to prevent multicast delegate leaks. | **TESTED** (Adversarial Route Thrashing 16 transitions) |
| **4** | **In-App Notification Infrastructure** | No standard in-app feedback mechanism; relied on OS balloons or silent failure. | Created `INotificationService` and `NotificationService`. Embedded accessible `InfoBar` host inside `ShellView.xaml` with auto-dismiss timer reuse to eliminate delegate leaks. | **TESTED** (Automated W1.2a-f) |
| **5** | **OCR Resource Decoupling** | `WinRtOcrService` wrapped caller input streams directly, risking unintended disposal and stream position offset mutation down the pipeline. | Streams copied to independent `InMemoryRandomAccessStream`; caller seekable streams have original `.Position` offset restored in guaranteed `finally` block. | **TESTED** (Automated W1.3a-c) |
| **6** | **WIA Scanner COM Object Cleanliness** | Scanner item COM handles not explicitly released via `Marshal.ReleaseComObject`, risking `WIA_ERROR_BUSY` (0x80210006). | Wrapped dynamic COM items and property iterators in explicit try/finally blocks calling `Marshal.ReleaseComObject`. | **TESTED** (Automated W1.5 repeated enumeration) |
| **7** | **Process Spawning Security & Fallback** | `BatchImageProcessorService` threw `Win32Exception` if `magick.exe` was absent from PATH without fallback. | Implemented `IsImageMagickAvailable` executable path probe; automatically falls back to native Windows Imaging Component (WIC) engine with observable job status warning. | **TESTED** (Automated W1.6a-f) |
| **8** | **Settings Persistence Observability** | IO exceptions during JSON persistence were logged to file but never surfaced to consumers, and static path fields inhibited container/redirected testing. | Added `LastPersistenceError` property to `IAppSettingsService`. Converted settings paths to instance fields supporting environment variables and custom base directory overrides. | **TESTED** (Automated W1.7a-c) |
| **9** | **DirectML Device Loss Resilience** | ONNX Runtime DML inference sessions threw unhandled exceptions during GPU power state transitions or driver reset. | Implemented device-loss detection in `DirectMlEmbeddingService`; handles `DXGI_ERROR_DEVICE_REMOVED` by falling back safely to CPU SIMD vector execution. | **TESTED** (Automated W1.8a-c) |
| **10** | **Keyboard Accelerator Mapping** | `Ctrl+\` navigation pane toggle accelerator was incorrectly mapped to `VirtualKey.Back` (Backspace). | Replaced incorrect keycode with standard `VirtualKey.None` + scan code `0xDC` (`VK_OEM_5`). | **TESTED** (Automated W1.9a-b) |
| **11** | **Global Exception Observability** | Background thread exceptions crashed process unobserved. | Hooked `AppDomain.CurrentDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` in `Program.cs` and `App.xaml.cs` with formatted HResult and stack logging to `startup.log`. | **TESTED** (Startup log verification) |

### 7.3 Deferred Architectural Risks
The following dependencies require physical peripherals and are intentionally deferred to physical integration stages:
1. **Physical Flatbed Scanner Acquisition**: Requires physical WIA/USB scanner peripheral connected to the host machine. Service logic and COM lifetime verified via software harness.
2. **Physical Multi-Node Mobile Link**: Requires physical Android 16 device running Axora Mobile for Wi-Fi Direct socket negotiation. Cryptographic ECDH NIST P-256 handshake and WebSocket receiver verified via unit tests.
3. **Physical GPU Removal During Inference**: Requires physical hot-unplugging of external PCIe/Thunderbolt GPU during active ONNX inference. Recovery code path verified via CPU fallback logic.

---

## 8. Phase W1.5: Extension / Dependency / Download Manager Foundation

**Execution Date**: September 4, 2026  
**Status**: **COMPLETED & VERIFIED**  
**Engineering Baseline**: Baseline 4 + W1 Hardening + W1.5 Download Manager  

### 8.1 Scope & Purpose
Phase W1.5 established a dedicated, user-controlled Extension / Dependency / Download Manager foundation in `Axora-Desktop-WinUI` before implementing major product features (such as Phase W2 Universal Converter). It ensures external dependencies (e.g. ImageMagick, OCR language packs, future AI models, format converters) are governed under a uniform security, versioning, installation, repair, and UI status contract.

### 8.2 Product Principles & Non-Negotiable Rules
1. **No Silent Updates**: AXORA may detect and notify users that an update exists, but **MUST NEVER silently install or upgrade an external dependency without explicit user approval**. Every install, update, repair, and clean reinstall requires conscious user action and confirmation dialog.
2. **Dedicated Footer Placement**: Download Manager resides strictly in the `NavigationView.FooterMenuItems` (above Settings), completely segregated from primary feature workflow pages.
3. **Non-Destructive Clean Reinstall**: Clean Reinstall removes extension binaries, extension cache, temporary installer artifacts, and extension-specific metadata. It **NEVER touches AXORA user documents, user projects, settings, or unrelated application data**.
4. **Resilient Fallbacks**: For optional extensions (such as ImageMagick), native system fallbacks (such as Windows Imaging Component / WIC GPU processing) remain fully operational when the extension is missing or unready.
5. **Reusable Consumer UI**: Feature pages declare dependencies using `<controls:DependencyStatusControl>`, displaying readiness, missing warnings, or update notices, with a direct 1-click navigation link to Download Manager highlighting the targeted dependency.

### 8.3 Core Architecture & Service Topology
- **Models**:
  - `ExtensionModel`: Defines dependency identity (`Id`, `DisplayName`, `Description`, `InstalledVersion`, `LatestVersion`, `Status`, `RequiredBy`, `IsRequired`, `IsOptional`, `InstallSource`, `ExecutableName`, `ProbeArguments`, `ExpectedChecksumSha256`, `IsBusy`, `IsHighlighted`, etc.).
  - `ExtensionStatus`: State machine (`NotInstalled`, `Installing`, `Installed`, `UpdateAvailable`, `RepairRequired`, `Corrupted`, `Unsupported`, `Failed`).
  - `ExtensionValidationResult`: Reports validity, corruption, and repair requirement flags.
  - `DependencyUiState`: Encapsulates visual state tokens for consumer pages.
  - `ExtensionStateChangedEventArgs`: Thread-safe notification args for status changes.
- **Service Interfaces & Implementations**:
  - `IExtensionRegistry` / `ExtensionRegistry`: In-memory thread-safe registry seeded with ImageMagick Q16-HDRI (`7.1.1-43`).
  - `IDependencyManager` / `DependencyManager`: Orchestrates queries, version checks, installs, updates, repairs, reinstalls, clean reinstalls, and broadcasts state changes.
  - `IVersionDetector` / `VersionDetector`: Resolves installed versions via probe command lines, normalized semantic version comparison, and registry inspection.
  - `IExtensionDownloader` / `ExtensionDownloader`: HTTPS-only downloader with vendor domain whitelisting, SHA-256 verification, and cancellation support.
  - `IExtensionInstaller` / `ExtensionInstaller`: Stages downloads, executes verified installers, and performs post-install verification.
  - `IExtensionRepairService` / `ExtensionRepairService`: Restores corrupted binaries and configurations non-destructively.
  - `IExtensionCacheService` / `ExtensionCacheService`: Manages `%LOCALAPPDATA%\Axora\ExtensionCache\`, calculates size, and purges cache files safely.
  - `IExtensionValidator` / `ExtensionValidator`: Validates binary existence, non-zero file sizes, architecture compatibility, and checksums.
  - `DownloadManagerViewModel`: MVVM presentation layer with filtered collections (`InstalledExtensions`, `AvailableExtensions`, `UpdateExtensions`), action commands, and confirmation dialog orchestration.
  - `DownloadManagerPage.xaml`: Native WinUI 3 interface with Cards, Status Badges, Progress Bars, and Dialogs.
  - `DependencyStatusControl.xaml`: Reusable consumer control with `InfoBar` and deep-linking button.


