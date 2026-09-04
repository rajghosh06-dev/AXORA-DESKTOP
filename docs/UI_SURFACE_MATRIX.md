# AXORA Desktop — Comprehensive UI Surface Matrix

This document provides the authoritative inventory and status tracking for all real user-facing UI surfaces, controls, navigation flows, dialogs, and interactive states across both desktop implementations of AXORA Desktop:

1. **Axora-Desktop-WinUI** (.NET 9 + Windows App SDK 1.6 + WinUI 3 + XAML)
2. **Axora-Desktop-MaterialUI** (Tauri v2 + React 18 + Tailwind CSS + Rust)

---

## 1. Classification & Legend

- **Implementation**: `WinUI` or `MaterialUI`.
- **Location**: Specific source file path.
- **Control Type**: Window, Page, NavigationItem, Button, ComboBox, Dialog, DropZone, Toggle, Slider, ListView, AutoSuggestBox.
- **Automation Feasibility**:
  - `CDP Automation`: Automated via Chrome DevTools Protocol WebSocket on WebView2 (`--remote-debugging-port=9222`).
  - `Windows UIA`: Automated via `System.Windows.Automation.AutomationElement` WinUI Visual Tree traversal.
  - `Manual Only`: Requires manual physical peripherals (e.g., physical WIA scanner hardware, real Android device pairing).
- **Status Codes**:
  - `PASS`: Behavior and visual transition verified with runtime evidence.
  - `FAIL`: Defect observed at runtime.
  - `BLOCKED`: Precondition or hardware dependency prevents runtime execution.
  - `NOT VERIFIED`: Not yet exercised in automated or manual pass.
  - `MANUAL VERIFICATION REQUIRED`: Interactive flow verified feasible, pending end-user physical session.

---

## 2. Shell & Window Infrastructure

| Implementation | Surface / Component | Location / File | Control Type | Expected Action | Expected Visual Result | State Transition | Automation Feasibility | Status |
|---|---|---|---|---|---|---|---|---|
| **WinUI** | Main Window | `MainWindow.cs` | Win32 Window Subclass | Launch application | Window opens at 1200x800 with `MicaKind.BaseAlt` and tall titlebar | Unmapped -> Active Window | Windows UIA | `PASS` |
| **WinUI** | Min/Max Constraints | `MainWindow.cs` | `WM_GETMINMAXINFO` Hook | Drag window border smaller than 1000x620 | Window resizing stops at 1000px width, 620px height | Resizing -> Clamped | Windows UIA | `PASS` |
| **WinUI** | Navigation Shell | `Views/ShellView.xaml` | `NavigationView` | Click menu items | Active indicator moves to selected item, updates header title | Page index update | Windows UIA | `PASS` |
| **WinUI** | Command Palette | `Controls/CommandPaletteDialog.xaml` | Overlay `UserControl` | Press `Ctrl+K` | Semi-transparent acrylic backdrop darkens screen, palette centers at top | Collapsed -> Visible | Windows UIA | `PASS` |
| **WinUI** | File Drop Zone | `Controls/FileDropZoneOverlay.xaml` | Drag/Drop Overlay | Drag file over window | Dashed drop boundary with accent glow appears | Inactive -> DragOver | Windows UIA | `PASS` |
| **MaterialUI** | Main Window | `src-tauri/tauri.conf.json` | Tauri / Wry Window | Launch application | Native window opens with 120ms splash guard, min size 960x600 | Closed -> Active Window | CDP Automation | `PASS` |
| **MaterialUI** | Splash Screen | `src/components/SplashScreen.tsx` | Animated Overlay | App mounting | Displays Axora logo, brand title, and loading progress, fades out | Mounted -> Unmounted | CDP Automation | `PASS` |
| **MaterialUI** | Navigation Rail | `src/components/Sidebar.tsx` | Collapsible `motion.aside` | Click hamburger toggle | Width transitions between 264px (expanded) and 80px (collapsed) | Expanded <-> Collapsed | CDP Automation | `PASS` |
| **MaterialUI** | Navigation Items | `src/components/Sidebar.tsx` | MD3 Ripple Items | Click navigation item | Active pill shifts with MD3 spring motion; main content changes | Route state update | CDP Automation | `PASS` |
| **MaterialUI** | Theme Toggle | `src/components/ThemeToggle.tsx` | Icon Button | Click sun/moon icon | Global CSS variables switch between MD3 dark and light tokens | Dark <-> Light | CDP Automation | `PASS` |
| **MaterialUI** | Command Palette | `src/components/CommandPalette.tsx` | Framer Motion Modal | Press `Ctrl+K` | Centered floating modal opens with search input and action list | Closed -> Open | CDP Automation | `PASS` |
| **MaterialUI** | Drop Zone Overlay | `src/components/FileDropZoneOverlay.tsx` | Full-screen Overlay | Drag file over window | Semi-transparent blur overlay with drop instruction appears | Hidden -> Visible | CDP Automation | `PASS` |
| **MaterialUI** | Global Toasts | `src/components/ToastNotification.tsx` | Toast Container | Dispatch toast event | Animated slide-in toast (success/warning/error) with auto-dismiss | Idle -> Toasting -> Idle | CDP Automation | `PASS` |

---

## 3. View / Page Inventory

### A. Dashboard / Workspace Hub

| Implementation | Control / Section | Location | Control Type | Expected Action | Expected Visual Result | Automation Feasibility | Status |
|---|---|---|---|---|---|---|---|
| **WinUI** | System Status Badge | `Views/DashboardPage.xaml` | Border / Grid | Displays health | Green check badge with "System Nominal" text | Windows UIA | `PASS` |
| **WinUI** | Diagnostics Button | `Views/DashboardPage.xaml` | Accent Button | Click button | Opens inline hardware & runtime diagnostic card with CPU/RAM details | Windows UIA | `PASS` |
| **WinUI** | Refresh Button | `Views/DashboardPage.xaml` | Standard Button | Click button | Updates CPU, RAM, storage, and connection progress bars | Windows UIA | `PASS` |
| **WinUI** | CPU Telemetry Card | `Views/DashboardPage.xaml` | Metric Card | Observe value | Live CPU % with animated `ProgressBar` | Windows UIA | `PASS` |
| **WinUI** | RAM Telemetry Card | `Views/DashboardPage.xaml` | Metric Card | Observe value | Live RAM usage (e.g. "5.2 GB / 15.8 GB") with bar | Windows UIA | `PASS` |
| **WinUI** | Storage Telemetry Card | `Views/DashboardPage.xaml` | Metric Card | Observe value | Primary disk usage with percentage and detail label | Windows UIA | `PASS` |
| **WinUI** | Connections Card | `Views/DashboardPage.xaml` | Metric Card | Observe value | Shows active P2P mesh connections count | Windows UIA | `PASS` |
| **WinUI** | QuickDrop Feed | `Views/DashboardPage.xaml` | ListView / EmptyState | Displays items | Empty illustration if 0 drops; list of cards with "Show in Folder" | Windows UIA | `PASS` |
| **MaterialUI** | Status Pill | `src/pages/Dashboard.tsx` | Animated Badge | Observe pulse | Glowing dot with "Backend Online" status chip | CDP Automation | `PASS` |
| **MaterialUI** | Quick Action Cards (6) | `src/pages/Dashboard.tsx` | Ripple Cards | Click card | Navigates to target page (Universal Engine, Vault, Bulk Canvas, etc.) | CDP Automation | `PASS` |
| **MaterialUI** | Analytics Quick Action | `src/pages/Dashboard.tsx` | Ripple Card | Click card | Dispatches `open-compatibility-modal` event, opening System Info dialog | CDP Automation | `PASS` |
| **MaterialUI** | Hero "Get Started" | `src/pages/Dashboard.tsx` | Primary Button | Click button | Navigates directly to "Universal Engine" | CDP Automation | `PASS` |

---

### B. Scholar Kit / Academic Toolkit

| Implementation | Control / Section | Location | Control Type | Expected Action | Expected Visual Result | Automation Feasibility | Status |
|---|---|---|---|---|---|---|---|
| **WinUI** | Voice Dictation | `Views/ScholarKitPage.xaml` | ToggleButton | Click toggle | Activates microphone dictation; displays recording badge | Windows UIA | `MANUAL VERIFICATION REQUIRED` |
| **WinUI** | Read Aloud | `Views/ScholarKitPage.xaml` | ToggleButton | Click toggle | Speaks current notes via Windows Speech Synthesis | Windows UIA | `MANUAL VERIFICATION REQUIRED` |
| **WinUI** | Push to Flashcards | `Views/ScholarKitPage.xaml` | Accent Button | Click button | Parses colon notes into flashcards and sends to Flashcard Studio | Windows UIA | `PASS` |
| **WinUI** | Export Dropdown | `Views/ScholarKitPage.xaml` | DropDownButton | Click button | Opens MenuFlyout (Copy, Save as PDF, Save as Markdown) | Windows UIA | `PASS` |
| **WinUI** | 4-Pivot Container | `Views/ScholarKitPage.xaml` | Pivot | Click tab headers | Switches between OCR, RAG Semantic Search, PDF Annotator, Notes | Windows UIA | `PASS` |
| **MaterialUI** | 5-Tab Bar | `src/pages/Academic.tsx` | MD3 Tab Pill Row | Click tab | Switches between OCR, LaTeX Notes, PDF Compressor, Redactor, Surgeon | CDP Automation | `PASS` |
| **MaterialUI** | OCR File Picker | `src/pages/Academic.tsx` | File Picker / DropZone | Select image | Loads image path; enables "Extract Text" button | CDP Automation | `PASS` |
| **MaterialUI** | Extract Text Button | `src/pages/Academic.tsx` | Filled Button | Click button | Invokes `ocr_image_windows`, displays text in textarea | CDP Automation | `PASS` |
| **MaterialUI** | Semantic Search Query | `src/pages/Academic.tsx` | Search Input | Enter query + Enter | Invokes Rust `semantic_search_docs`, displays top-3 chunk cards | CDP Automation | `PASS` |
| **MaterialUI** | Copy Text Button | `src/pages/Academic.tsx` | Outlined Button | Click button | Copies extracted text to clipboard, triggers success toast | CDP Automation | `PASS` |
| **MaterialUI** | PDF Surgeon Drag Grid | `src/pages/Academic.tsx` | Reorder Grid | Drag page badge | Reorders PDF pages with Framer Motion spring feedback | CDP Automation | `PASS` |

---

### C. Resume Studio / Form Studio

| Implementation | Control / Section | Location | Control Type | Expected Action | Expected Visual Result | Automation Feasibility | Status |
|---|---|---|---|---|---|---|---|
| **WinUI** | ATS Score Gauge | `Views/ResumeStudioPage.xaml` | Custom Visual Ring | Change content | Dynamic ATS score (0–100%) recalculates with color grading | Windows UIA | `PASS` |
| **WinUI** | Section Navigation | `Views/ResumeStudioPage.xaml` | SegmentRadioButton | Click section | Switches editor between Contact, Education, Experience, Skills, etc. | Windows UIA | `PASS` |
| **WinUI** | 1-Page Budget Bar | `Views/ResumeStudioPage.xaml` | ProgressBar | Type text | Progress fill updates; turns amber/red if exceeding single page budget | Windows UIA | `PASS` |
| **WinUI** | Vector PDF Preview | `Views/ResumeStudioPage.xaml` | Canvas / Image | Edit fields | PDF layout updates in real time with headers, bullets, margins | Windows UIA | `PASS` |
| **WinUI** | Export PDF Button | `Views/ResumeStudioPage.xaml` | Accent Button | Click button | Compiles vector PDF to selected destination | Windows UIA | `PASS` |
| **MaterialUI** | Form Studio Tabs (6) | `src/pages/FormStudio.tsx` | MD3 Tab Buttons | Click tab | Switches between Resizer, Signature, BG Remover, Stamp, ID Stitcher, PDF Builder | CDP Automation | `PASS` |
| **MaterialUI** | KB Target Resizer | `src/pages/FormStudio.tsx` | Slider + NumberInput | Drag slider | Sets target file size (e.g. 50 KB - 200 KB) for JPEG binary search | CDP Automation | `PASS` |
| **MaterialUI** | Signature Isolator | `src/pages/FormStudio.tsx` | Threshold Slider | Adjust threshold | Displays real-time canvas preview of extracted transparent ink | CDP Automation | `PASS` |
| **MaterialUI** | ID Card Stitcher | `src/pages/FormStudio.tsx` | Dual Dropzones | Add front + back | Renders 2-up composite A4 preview ready for 1-click export | CDP Automation | `PASS` |

---

### D. Spaced Repetition / Flashcard Studio

| Implementation | Control / Section | Location | Control Type | Expected Action | Expected Visual Result | Automation Feasibility | Status |
|---|---|---|---|---|---|---|---|
| **WinUI** | Deck ListView | `Views/FlashcardsPage.xaml` | ListView | Click deck | Selects deck; loads cards, updates retention rate, sets active card | Windows UIA | `PASS` |
| **WinUI** | Create Deck Button | `Views/FlashcardsPage.xaml` | Button | Click button | Opens dialog or generates new deck item in list | Windows UIA | `PASS` |
| **WinUI** | Export Deck Dropdown | `Views/FlashcardsPage.xaml` | DropDownButton | Click button | Flyout with CSV, Anki TXT, and JSON export options | Windows UIA | `PASS` |
| **WinUI** | Flashcard Canvas | `Views/FlashcardsPage.xaml` | Border / Tap Target | Click or press Space | Flips card between Question/Concept and Answer/Details | Windows UIA | `PASS` |
| **WinUI** | Rating Buttons (3) | `Views/FlashcardsPage.xaml` | Button Cluster | Click Hard/Med/Easy | Recalculates SM-2 interval; advances to next card; updates retention % | Windows UIA | `PASS` |
| **WinUI** | Prev / Next Buttons | `Views/FlashcardsPage.xaml` | Navigation Buttons | Click button | Steps through deck cards; resets card flip state to Front | Windows UIA | `PASS` |
| **MaterialUI** | Study Deck Grid | `src/pages/FlashcardStudio.tsx` | Deck Cards | Click deck | Loads deck session with animated card presentation | CDP Automation | `PASS` |
| **MaterialUI** | 3D Flip Card | `src/pages/FlashcardStudio.tsx` | 3D Transform Card | Click card | 180° smooth flip animation revealing back definition | CDP Automation | `PASS` |
| **MaterialUI** | SM-2 Recall Buttons | `src/pages/FlashcardStudio.tsx` | Rating Buttons | Click rating | Updates review interval, updates deck statistics, advances card | CDP Automation | `PASS` |
| **MaterialUI** | Export to Anki | `src/pages/FlashcardStudio.tsx` | Outlined Button | Click button | Exports deck to Anki-compatible format with toast confirmation | CDP Automation | `PASS` |

---

### E. Bulk Canvas / Batch Image Processor

| Implementation | Control / Section | Location | Control Type | Expected Action | Expected Visual Result | Automation Feasibility | Status |
|---|---|---|---|---|---|---|---|
| **WinUI** | Dropzone Area | `Views/BatchImagePage.xaml` | Drag/Drop Border | Drop files or folders | Populates Queue ListView with file thumbnails, names, sizes | Windows UIA | `PASS` |
| **WinUI** | Browse Files Button | `Views/BatchImagePage.xaml` | Button | Click button | Opens Windows OpenFileDialog for image files | Windows UIA | `PASS` |
| **WinUI** | Processing Engine Box | `Views/BatchImagePage.xaml` | ComboBox | Select item | Switches between WIC (Hardware) and ImageMagick (Extended) | Windows UIA | `PASS` |
| **WinUI** | Queue ListView | `Views/BatchImagePage.xaml` | ListView | Observe items | Renders file status pills (Pending, Processing, Done, Failed) | Windows UIA | `PASS` |
| **WinUI** | Start Batch Button | `Views/BatchImagePage.xaml` | Accent Button | Click button | Launches multithreaded queue worker; updates live progress | Windows UIA | `PASS` |
| **MaterialUI** | Dropzone Canvas | `src/pages/BatchProcessor.tsx` | Drop Target | Drop files | Adds images to queue table with thumbnail previews | CDP Automation | `PASS` |
| **MaterialUI** | Quality Slider | `src/pages/BatchProcessor.tsx` | MD3 Slider | Drag slider | Updates compression percentage (1%–100%) | CDP Automation | `PASS` |
| **MaterialUI** | Format Dropdown | `src/pages/BatchProcessor.tsx` | Select / ComboBox | Choose format | Selects output format: WEBP, JPEG, PNG, AVIF | CDP Automation | `PASS` |
| **MaterialUI** | Execute Batch Button | `src/pages/BatchProcessor.tsx` | Filled Button | Click button | Starts parallel processing; displays determinate progress bar | CDP Automation | `PASS` |

---

### F. Universal Engine / Compressor

| Implementation | Control / Section | Location | Control Type | Expected Action | Expected Visual Result | Automation Feasibility | Status |
|---|---|---|---|---|---|---|---|
| **WinUI** | Dropzone Area | `Views/CompressorPage.xaml` | Drag/Drop Border | Drop documents | Adds PDFs and Office files to compression queue | Windows UIA | `PASS` |
| **WinUI** | Profile Selector | `Views/CompressorPage.xaml` | ComboBox | Select profile | Switches between Low (Lossless), Medium (150 DPI), High (72 DPI) | Windows UIA | `PASS` |
| **WinUI** | Start Compression | `Views/CompressorPage.xaml` | Accent Button | Click button | Executes compression; displays saved KB and ratio % | Windows UIA | `PASS` |
| **MaterialUI** | Mode Switcher | `src/pages/Converter.tsx` | Tab Row | Click tab | Switches between Document Conversion and Compression | CDP Automation | `PASS` |
| **MaterialUI** | Format Grid | `src/pages/Converter.tsx` | Radio Tiles | Click tile | Selects target extension (PDF, DOCX, EPUB, TXT, HTML) | CDP Automation | `PASS` |
| **MaterialUI** | Convert Action | `src/pages/Converter.tsx` | Filled Button | Click button | Runs conversion; displays download / reveal in explorer button | CDP Automation | `PASS` |

---

### G. Encrypted Security Vault

| Implementation | Control / Section | Location | Control Type | Expected Action | Expected Visual Result | Automation Feasibility | Status |
|---|---|---|---|---|---|---|---|
| **WinUI** | Passphrase Box | `Views/VaultPage.xaml` | PasswordBox | Enter passphrase | Password masked with eye reveal toggle; evaluates strength | Windows UIA | `PASS` |
| **WinUI** | TPM 2.0 Toggle | `Views/VaultPage.xaml` | ToggleSwitch | Toggle switch | Binds encryption key to platform hardware security module | Windows UIA | `PASS` |
| **WinUI** | Encrypt Files Button | `Views/VaultPage.xaml` | Accent Button | Click button | Streams AES-256-GCM encryption, appends `.axora` extension | Windows UIA | `PASS` |
| **WinUI** | Decrypt Files Button | `Views/VaultPage.xaml` | Button | Click button | Verifies Argon2id tag; recovers original files | Windows UIA | `PASS` |
| **MaterialUI** | Vault Master Key Box | `src/pages/Security.tsx` | Input with Reveal | Enter password | Password strength meter updates with entropy estimation | CDP Automation | `PASS` |
| **MaterialUI** | Encryption Controls | `src/pages/Security.tsx` | Action Buttons | Click Encrypt/Decrypt | Invokes Rust `vault_encrypt_file` / `vault_decrypt_file` | CDP Automation | `PASS` |

---

### H. Mobile Link & Settings

| Implementation | Control / Section | Location | Control Type | Expected Action | Expected Visual Result | Automation Feasibility | Status |
|---|---|---|---|---|---|---|---|
| **WinUI** | High-Contrast QR | `Views/MobileLinkPage.xaml` | Image / Border | Page loaded | Crisp 240x240 QR code with Axora badge rendered in center | Windows UIA | `PASS` |
| **WinUI** | Regenerate Token | `Views/MobileLinkPage.xaml` | Button | Click button | Recreates ephemeral ECDH keypair; refreshes QR bitmap | Windows UIA | `PASS` |
| **WinUI** | Theme ComboBox | `Views/SettingsPage.xaml` | ComboBox | Select theme | Switches between System Default, Light, Dark | Windows UIA | `PASS` |
| **WinUI** | Accent Palette | `Views/SettingsPage.xaml` | Swatch Buttons (4) | Click color circle | Re-keys primary application accent brush dynamically | Windows UIA | `PASS` |
| **MaterialUI** | QR Code Canvas | `src/pages/MobileLink.tsx` | Canvas / SVG | Page loaded | Generates vector QR code for Android Axora companion pairing | CDP Automation | `PASS` |
| **MaterialUI** | Color Palette Theme | `src/pages/Settings.tsx` | MD3 Color Swatches | Click color | Updates dynamic MD3 primary and secondary tonal palettes | CDP Automation | `PASS` |
| **MaterialUI** | Clear Storage Cache | `src/pages/Settings.tsx` | Outlined Button | Click button | Clears temporary conversion and thumbnail caches | CDP Automation | `PASS` |
