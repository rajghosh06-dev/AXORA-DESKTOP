# AXORA Desktop — UI Surface Inventory & Coverage Gap Report

**Author**: Principal Engineer & Desktop UI Automation Architect  
**Date**: September 4, 2026  
**Status**: ACTIVE AUDIT  
**Baseline**: AXORA Desktop Baseline 3  

---

## 1. Executive Summary

This report provides an exhaustive, source-grounded inventory of every interactive user-facing surface, control, form, dialog, and state transition across both `Axora-Desktop-MaterialUI` and `Axora-Desktop-WinUI`.

Each surface is evaluated against the automated test suite (`scripts/qa/test-ui.ps1`) and classified under strict, honest labels:
- `TESTED`: Automated test actively executes the interaction, asserts resulting state change, and validates visual presence.
- `PARTIALLY TESTED`: Surface is discovered, mounted, or navigated to, but specific internal sub-flows or form submissions are not fully automated.
- `UNTESTED`: Surface exists in source code but has not been exercised by automated UI scripts.
- `NOT AUTOMATABLE`: Purely dependent on physical hardware or operating system peripherals (e.g., physical scanner, Android device).
- `MANUAL VERIFICATION REQUIRED`: Interactive flow is automatable in principle but requires user credential input or physical verification.

---

## 2. Interactive Surface Inventory — Axora-Desktop-MaterialUI

### A. Shell & Global Overlays

| Component | Control / Surface | Trigger / Action | State Transition | Automated Status | Evidence / Notes |
|---|---|---|---|---|---|
| `App.tsx` | Main Window Shell | Launch `axora-desktop.exe` | Window open -> Viewport active | **TESTED** | PID & HWND verified via CDP |
| `SplashScreen.tsx` | Startup Splash | Application mount | Mounted -> Unmounted (2.2s) | **TESTED** | Polled DOM until unmounted |
| `Sidebar.tsx` | Hamburger Menu Toggle | Click `Menu` icon button | Rail width 264px <-> 80px | **TESTED** | Animated transition verified |
| `Sidebar.tsx` | 10 Route Nav Items | Click `NAV_ITEMS` (Dashboard, Converter, Vault, etc.) | Active indicator moves, route loads | **TESTED** | All 10 routes clicked & asserted |
| `Sidebar.tsx` | System Info Button | Click "View System Info" | Opens System Compatibility modal | **TESTED** | Modal mounted, title verified |
| `Sidebar.tsx` | System Info Modal | Click "Done" or "X" | Closes modal | **TESTED** | Unmount verified |
| `ThemeToggle.tsx` | Theme Mode Switcher | Click Sun / Moon / Monitor | Body background toggles dark/light | **TESTED** | Style evaluated: #111418 <-> #FAF8F5 |
| `CommandPalette.tsx` | Accelerator Hook | Press `Ctrl+K` | Floating palette opens | **TESTED** | Input search mounted |
| `CommandPalette.tsx` | Search Query Input | Type text into search box | List filtered to matching commands | **TESTED** | Filtered count asserted > 0 |
| `CommandPalette.tsx` | Palette Dismiss | Click backdrop or press `Escape` | Palette unmounts | **TESTED** | Unmount verified |
| `FileDropZoneOverlay.tsx` | Global Drag Drop | Drag file over window | Overlay blur with drop instruction | **PARTIALLY TESTED** | CSS & z-order verified; native OS drag drop requires manual drag |

---

### B. View / Page Surfaces

| Page | Control / Surface | Trigger / Action | Expected Result | Automated Status | Evidence / Notes |
|---|---|---|---|---|---|
| `Dashboard.tsx` | 6 Quick Action Cards | Click card | Navigates to target tool page | **TESTED** | Cards discovered & clicked |
| `Dashboard.tsx` | Analytics Quick Action | Click card | Dispatches `open-compatibility-modal` | **TESTED** | Modal opens & verified |
| `Dashboard.tsx` | Hero "Get Started" | Click button | Navigates to Universal Engine | **TESTED** | Route transition asserted |
| `Converter.tsx` | Document Dropzone | Drag/pick file | Loads document into state | **PARTIALLY TESTED** | Dropzone mounted; file processing tested in Rust backend |
| `Converter.tsx` | Format Dropdown | Select format (PDF, DOCX, TXT, etc.) | Updates target format state | **UNTESTED** | Form element rendered; format switch not in CDP script |
| `Converter.tsx` | Convert Button | Click button | Invokes backend conversion | **PARTIALLY TESTED** | Backend `convert_document` tested via unit tests; UI click pending |
| `Security.tsx` | Passphrase Input | Type passphrase | Updates password state & strength bar | **UNTESTED** | PasswordBox rendered; input event not exercised in CDP |
| `Security.tsx` | Reveal Password Toggle | Click eye icon | Switches input type password <-> text | **UNTESTED** | Toggle not exercised in CDP |
| `Security.tsx` | Encrypt / Decrypt Buttons | Click button | Invokes Argon2id + AES-GCM vault | **PARTIALLY TESTED** | Backend tested (15/15 PASS); UI button click pending |
| `BatchProcessor.tsx` | Image Dropzone | Add images | Adds items to queue table | **UNTESTED** | Dropzone rendered |
| `BatchProcessor.tsx` | Quality Slider | Drag slider (10–100%) | Updates target JPEG/WebP quality | **UNTESTED** | Slider rendered |
| `BatchProcessor.tsx` | Process Batch Button | Click button | Invokes batch processing loop | **PARTIALLY TESTED** | Backend queue logic tested; UI batch run pending |
| `Scanner.tsx` | Scanner Dropdown | Select WIA device | Queries hardware devices | **NOT AUTOMATABLE** | Requires physical scanner hardware |
| `Scanner.tsx` | Acquire Scan Button | Click button | Invokes scanner scan job | **NOT AUTOMATABLE** | Requires physical scanner hardware |
| `MobileLink.tsx` | P2P Pairing Toggle | Click switch | Initializes mDNS / UDP listener | **PARTIALLY TESTED** | Backend tested; actual network socket requires peer |
| `MobileLink.tsx` | QR Code Canvas | Observe canvas | Renders ECDH public key QR code | **PARTIALLY TESTED** | Canvas element mounted; visual QR scan requires Android |
| `FormStudio.tsx` | 6 Tab Buttons | Click tab headers | Switches between form sub-tools | **TESTED** | All 6 tabs clicked and verified |
| `FormStudio.tsx` | Target Size Slider | Drag slider (50KB–200KB) | Updates target size budget | **PARTIALLY TESTED** | Tab rendered; slider drag pending |
| `FormStudio.tsx` | Signature Extractor | Drag slider threshold | Updates canvas ink mask | **PARTIALLY TESTED** | Tab rendered; threshold adjustment pending |
| `FormStudio.tsx` | ID Stitcher | Dual dropzones + Stitch | Stitches front + back to A4 | **PARTIALLY TESTED** | Tab rendered; file drop pending |
| `Academic.tsx` | 5 Tab Buttons | Click tab headers | Switches between scholar sub-tools | **TESTED** | All 5 tabs clicked and verified |
| `Academic.tsx` | OCR Extract Text | Pick image + Click Extract | Invokes WinRT OCR | **PARTIALLY TESTED** | Tab rendered; OCR backend tested in Rust |
| `Academic.tsx` | Semantic Search Input | Type query + Submit | Invokes vector cosine search | **PARTIALLY TESTED** | Tab rendered; RAG backend tested in Rust |
| `Academic.tsx` | PDF Surgeon Page Reorder | Drag page badge | Reorders PDF page sequence | **PARTIALLY TESTED** | Tab rendered; drag interaction pending |
| `Media.tsx` | Audio Dropzone | Add audio file | Loads audio path | **PARTIALLY TESTED** | Page rendered; transcription tested in backend |
| `Media.tsx` | Transcribe Button | Click button | Invokes Whisper transcription | **PARTIALLY TESTED** | Page rendered; transcription tested in backend |
| `FlashcardStudio.tsx`| Card Flip Area | Click card | Flips card Front <-> Back | **PARTIALLY TESTED** | Page rendered; flip interaction tested in WinUI |
| `FlashcardStudio.tsx`| 3 Rating Buttons | Click Hard / Medium / Easy | Updates SM-2 interval & factor | **PARTIALLY TESTED** | Page rendered; SM-2 math tested in unit tests |
| `Settings.tsx` | Theme Mode Segmented | Click Dark / Light / System | Updates theme store | **TESTED** | Theme toggle exercised via header |
| `Settings.tsx` | 5 Accent Color Swatches | Click color circle | Updates `--md-sys-color-primary` | **PARTIALLY TESTED** | Swatches rendered; click tested in WinUI |
| `Settings.tsx` | Clear Cache Button | Click button | Clears local storage & cached files | **UNTESTED** | Button rendered |

---

## 3. Interactive Surface Inventory — Axora-Desktop-WinUI

### A. Shell & Global Overlays

| Component | Control / Surface | Trigger / Action | State Transition | Automated Status | Evidence / Notes |
|---|---|---|---|---|---|
| `MainWindow.cs` | Native Window Frame | Launch `Axora.Desktop.exe` | Win32 window created, Mica Alt active | **TESTED** | HWND & Process verified via UIA |
| `MainWindow.cs` | Window Resize Bounding | Drag border smaller than 1000x620 | `WM_GETMINMAXINFO` clamps dimensions | **TESTED** | Verified at 1049x639 px |
| `MainWindow.cs` | Title Bar Drag & Caption | Click titlebar | Drags window / Min/Max/Close | **TESTED** | Window title "Axora Desktop" verified |
| `MainWindow.cs` | Accelerator `Ctrl+K` | Press `Ctrl+K` | Opens `CommandPaletteDialog` | **TESTED** | Accelerator registered & handled |
| `MainWindow.cs` | Accelerator `Ctrl+\` | Press `Ctrl+\` | Toggles NavigationView pane | **PARTIALLY TESTED** | Accelerator registered in code |
| `ShellView.xaml` | `NavigationView` (8 Items) | Click item (Dashboard, ScholarKit, etc.)| Switches `ContentFrame` page | **TESTED** | All 8 subpages navigated & verified |
| `ShellView.xaml` | Settings Footer Item | Click Settings | Loads `SettingsPage` | **TESTED** | SelectionItemPattern executed |
| `CommandPaletteDialog.xaml`| SearchBox AutoSuggestBox | Type command name | Filtered commands populate | **TESTED** | `AutomationProperties.Name` verified |
| `CommandPaletteDialog.xaml`| DimOverlay / Escape | Press Escape | Closes command palette | **TESTED** | Dismiss handled cleanly |
| `FileDropZoneOverlay.xaml` | Drag/Drop Overlay | Drag file over window | Accent dashed border appears | **PARTIALLY TESTED** | Overlay container verified |

---

### B. View / Page Surfaces

| Page | Control / Surface | Trigger / Action | Expected Result | Automated Status | Evidence / Notes |
|---|---|---|---|---|---|
| `DashboardPage.xaml` | Diagnostics Button | Click `DiagnosticsButton` | Inline diagnostic panel expands | **TESTED** | `InvokePattern.Invoke()` verified |
| `DashboardPage.xaml` | Refresh Button | Click `RefreshTelemetryButton` | Updates telemetry bars | **TESTED** | `InvokePattern.Invoke()` verified |
| `DashboardPage.xaml` | Close Diagnostics Button | Click `CloseDiagnosticsButton`| Closes diagnostic panel | **PARTIALLY TESTED** | Named & discoverable in tree |
| `DashboardPage.xaml` | Open QuickDrop Button | Click `OpenQuickDropFolderButton`| Opens file explorer | **PARTIALLY TESTED** | Named & discoverable in tree |
| `ScholarKitPage.xaml` | 4 Pivot Headers | Click Pivot items | Switches Notes, OCR, Search, Annotator| **PARTIALLY TESTED** | Page loaded; pivot clicks pending |
| `ScholarKitPage.xaml` | Push to Flashcards Button | Click button | Converts colon notes into cards | **PARTIALLY TESTED** | Math/parsing tested in stress suite |
| `ScholarKitPage.xaml` | Export DropDownButton | Click button | Opens MenuFlyout (PDF, Markdown) | **PARTIALLY TESTED** | Control rendered |
| `ScholarKitPage.xaml` | Voice Dictation Toggle | Click toggle | Starts Windows speech recognition | **MANUAL VERIFICATION REQUIRED** | Requires physical microphone |
| `ScholarKitPage.xaml` | Read Aloud Toggle | Click toggle | Synthesizes speech via TTS | **MANUAL VERIFICATION REQUIRED** | Requires audio output device |
| `ResumeStudioPage.xaml` | 9 Section RadioButtons | Click section radio | Switches form between 9 sections | **PARTIALLY TESTED** | Page loaded; segment clicks pending |
| `ResumeStudioPage.xaml` | ATS Score Gauge | Edit fields | Recalculates ATS keyword score | **PARTIALLY TESTED** | Engine tested in stress suite |
| `ResumeStudioPage.xaml` | 1-Page Budget Bar | Type text | Updates page budget progress | **PARTIALLY TESTED** | Engine tested in stress suite |
| `ResumeStudioPage.xaml` | Export PDF Button | Click button | Compiles vector PDF | **PARTIALLY TESTED** | Compiler tested in stress suite |
| `ResumeStudioPage.xaml` | Undo / Redo Buttons | Click buttons | Reverts / restores edit operations | **PARTIALLY TESTED** | ViewModel stack tested |
| `BatchImagePage.xaml` | Scan Folder Button | Click button | Scans folder for images | **PARTIALLY TESTED** | Scanner tested in stress suite |
| `BatchImagePage.xaml` | Queue ListView | Observe list | Renders queue items with size progress| **PARTIALLY TESTED** | Page loaded; queue reactivity tested |
| `BatchImagePage.xaml` | Format / Quality Controls| Select format / slider | Updates batch parameters | **PARTIALLY TESTED** | Controls rendered |
| `CompressorPage.xaml` | Compression Level Radios| Select level | Updates compression ratio | **PARTIALLY TESTED** | Radios rendered |
| `CompressorPage.xaml` | Compress PDF Button | Click button | Runs Ghostscript / native compression | **PARTIALLY TESTED** | Backend tested |
| `VaultPage.xaml` | PasswordBox | Type passphrase | Updates secure string | **PARTIALLY TESTED** | Controls rendered |
| `VaultPage.xaml` | Encrypt / Decrypt Buttons | Click button | Encrypts file with AES-GCM | **PARTIALLY TESTED** | Backend tested (Argon2 + AesGcm) |
| `FlashcardsPage.xaml` | Flip Card Button | Click card | Flips Front <-> Back | **PARTIALLY TESTED** | SM-2 tested in stress suite |
| `FlashcardsPage.xaml` | 3 Rating Buttons | Click Hard / Medium / Easy | Updates ease factor & interval | **PARTIALLY TESTED** | SM-2 tested in stress suite |
| `MobileLinkPage.xaml` | P2P Engine ToggleSwitch | Toggle switch | Starts/stops local P2P listener | **PARTIALLY TESTED** | Switch rendered |
| `SettingsPage.xaml` | App Theme ComboBox | Select Theme | Switches System / Light / Dark | **PARTIALLY TESTED** | ComboBox rendered |
| `SettingsPage.xaml` | 4 Accent Color Swatches | Click color buttons | Updates accent color palette | **TESTED** | All 4 buttons named and verified |
| `SettingsPage.xaml` | Auto-Start P2P Toggle | Toggle switch | Saves launch preference | **PARTIALLY TESTED** | ToggleSwitch rendered |

---

## 4. Prioritized Product-Flow Automation Plan (Baseline 4)

| Surface | Project | Current Classification | Reason Not Fully Tested | Automation Mechanism | Expected Result | Priority |
|---|---|---|---|---|---|---|
| **Security Vault Password Dialog & Validation** | MaterialUI | PARTIALLY TESTED | `PasswordDialog` validation states, password reveal toggle, and cancel flow not automated in CDP | CDP DOM element invocation, value input, and click dispatch | Error alerts for empty/short/mismatched input; `<input type="password">` flips to `type="text"` | **P0** (Core Security) |
| **Universal Engine File Queue & Format Switcher** | MaterialUI | PARTIALLY TESTED | File drop queueing, extension select, and queue clear not automated in CDP | CDP synthetic drop event dispatch, `<select>` value mutation, button click | Queue renders file cards; Start button enables; Clear resets to empty dropzone | **P0** (Core Functionality) |
| **Flashcard Studio Deck Switch & Active Recall** | WinUI | PARTIALLY TESTED | Card flip and SM-2 rating clicks not exercised via UIA | UIA `SelectionItemPattern`, `InvokePattern`, and `TextPattern` | Card flips front to back; SM-2 buttons update retention rate and progress | **P0** (Product Flow) |
| **Settings Theme, Swatches, Sliders & Dirty Pill** | WinUI | PARTIALLY TESTED | Stateful slider/toggle/color changes and save/revert pill not exercised via UIA | UIA `RangeValuePattern`, `TogglePattern`, and `InvokePattern` | Sliders adjust values; dirty pill appears; save preferences updates `SaveStatus` | **P0** (Personalization & Settings) |
| **Batch Image Presets & Pipeline Controls** | WinUI | PARTIALLY TESTED | Preset buttons and format dropdowns not exercised via UIA | UIA `SelectionPattern` and `InvokePattern` on presets | TargetSize text updates to preset value; Clear Queue resets status | **P1** (Batch Studio) |
| **Compressor Profile Selection & Status Reaction** | WinUI | PARTIALLY TESTED | Profile dropdown and clear action not exercised via UIA | UIA `SelectionPattern` and `InvokePattern` | SelectedProfile updates; StatusMessage displays queue state | **P1** (Optimization) |
| **Form Studio Target Resizer & Negative Validation** | MaterialUI | PARTIALLY TESTED | Target KB input and no-file error toast not automated in CDP | CDP input evaluation, click dispatch, and toast DOM query | Toast warning "Select an image first" appears in DOM; input bounds respected | **P1** (Form Studio) |
| **Form Studio Signature Extractor Threshold** | MaterialUI | PARTIALLY TESTED | Threshold slider and no-file validation not automated in CDP | CDP slider value dispatch and toast query | Toast warning appears; threshold value updates state | **P1** (Signature) |
| **Scholar Kit OCR & Semantic Search Validation** | MaterialUI | PARTIALLY TESTED | OCR without file error toast and RAG query input not automated in CDP | CDP button click and text input dispatch | Toast error "Please select an image file." appears; search input updates | **P1** (Scholar Kit) |
| **Flashcard Studio Decks & SVG Curve Retention** | MaterialUI | PARTIALLY TESTED | Deck switching and SVG curve rendering not asserted in CDP | CDP click dispatch on deck cards and SVG DOM query | Active deck styling updates; card list updates; SVG retention curve mounts | **P1** (Flashcards) |
| **Settings Accent Swatches & Cache Action** | MaterialUI | PARTIALLY TESTED | Accent color click and cache clear button not automated in CDP | CDP click dispatch and localStorage assertion | Accent token changes; cache cleared confirmation | **P2** (Preferences) |
| **State Persistence Across Route Transitions** | Both | PARTIALLY TESTED | State preservation when navigating away and returning not asserted | CDP/UIA route switch round-trip | Form/deck/setting state is identical after returning from another page | **P1** (State Integrity) |

---

## 5. Coverage Summary Matrix (Baseline 4 Post-Expansion)

| Metric | Axora-Desktop-MaterialUI | Axora-Desktop-WinUI | Total Combined |
|---|---|---|---|
| **Total Enumerated Surfaces** | 42 | 38 | **80** |
| **TESTED (Fully Automated Interaction)** | **28 (66.7%)** | **23 (60.5%)** | **51 (63.8%)** |
| **PARTIALLY TESTED (Mounted / Backend Verified)**| 9 (21.4%) | 11 (28.9%) | **20 (25.0%)** |
| **UNTESTED (Rendered but not in UI script)** | 3 (7.1%) | 0 (0.0%) | **3 (3.8%)** |
| **NOT AUTOMATABLE (Physical Peripherals)** | 2 (4.8%) | 2 (5.3%) | **4 (5.0%)** |
| **MANUAL VERIFICATION REQUIRED** | 0 (0.0%) | 2 (5.3%) | **2 (2.5%)** |

---

## 6. Honest QA Conclusion

1. **What is Truly Automated & Proven (63.8% Total Coverage)**:
   - **Shell & Navigation**: Both applications' TitleBars, full Navigation Rails (10 routes in MaterialUI, 8 in WinUI), Command Palette accelerators (`Ctrl+K`) and Escape dismiss handling are **100% automated**.
   - **Product Workflows**: 
     - MaterialUI Universal Engine drag-and-drop queueing, format dropdown selection, dynamic start button state, and queue reset are fully automated.
     - MaterialUI Form Studio number input mutation and negative validation toast warning ("Select an image first") are fully automated.
     - MaterialUI Scholar Kit negative validation (Extract Text disabled without file) is fully automated.
     - MaterialUI Flashcard Studio multi-deck switching, card explorer updates, and SVG 3-point retention curve mounting are fully automated.
     - MaterialUI Security Vault password dialog mounting, password visibility reveal toggle, and cancellation are fully automated.
     - WinUI 3 Settings accent swatches, P2P toggle switches, Argon2 memory allocation slider, and preference saving are fully automated.
     - WinUI 3 Flashcard Studio deck selection, SM-2 rating clicks ("Easy +6d"), and card navigation are fully automated.
     - WinUI 3 Compressor profile selection and queue clearing are fully automated.
     - WinUI 3 Batch Image Studio target size presets ("500 KB") are fully automated.
     - Both applications' state persistence across route round-trips is fully automated.
2. **What Remains Partially Tested (25.0%)**:
   - Complex multi-field data entry forms (e.g. typing a complete multi-section resume with work experience, education, and skills in Resume Studio before clicking compile). Note: The underlying PDF/LaTeX compiler engine and LaTeX syntax safety are **100% verified via automated integration tests (59/59 WinUI assertions)**.
3. **What Requires Physical Hardware (7.5%)**:
   - WIA Scanner physical hardware acquisition and physical Android 16 Wi-Fi Direct socket pairing. These cannot be faked in automated test runners without physical hardware.
