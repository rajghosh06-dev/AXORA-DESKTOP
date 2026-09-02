# Axora Desktop: Comprehensive Architectural & Technical Analysis Report

**Project Location**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\Axora-Desktop`  
**Target Platform**: Windows 10/11, macOS, Linux  
**Core Framework**: Tauri v2 + Rust Core Backend + Vite + React 18 (TypeScript) + Tailwind CSS + Zustand  
**Status**: Active Production Rewrite (Phase 1-7 Implemented)

---

## 1. Executive Overview & Vision

### What is Axora Desktop?
**Axora Desktop** is a high-performance, secure, local-first desktop productivity suite and device sync hub. It combines document processing (conversion, compression, editing, page reordering, redaction), image batch utilities, digital signature extraction, target KB file resizing, security vaults (AES-256-GCM encryption), physical flatbed/ADF scanner integration, and local high-speed Wi-Fi synchronization with **Axora Mobile**.

### Why the Rewrite? Rationale for Tauri v2 + Rust Architecture
The legacy prototype of Axora was constructed using **PyQt6**, wrapping a Chromium WebView (`QWebEngineView`), driven by a local **Python Flask** server and a C# launcher (`Launcher.cs`). 

Architectural audits of the legacy desktop app uncovered critical vulnerabilities and severe bottlenecks:
1. **Critical Remote Code Execution (RCE)** (`DT-SEC-01`, `DT-SEC-02`): Flask bound to `0.0.0.0:49152` with wildcard CORS (`*`), exposing an unauthenticated `/api/sandbox/run-python` endpoint executing raw strings via `subprocess.run()`.
2. **Arbitrary File Operations** (`DT-SEC-03`): Unrestricted file write endpoint `/api/ui/write-file` allowing writing arbitrary bytes anywhere on host filesystems.
3. **Synchronous UI Freezes**: Heavy PDF processing, OCR, and image conversions executed synchronously on Flask request handlers, causing UI freezing and browser crashes.
4. **Installer Bloat & Hardcoded Paths**: Shipping Python runtimes, C++ bindings, and heavy sidecars resulted in ~150MB+ bundle sizes and fragile launching scripts (e.g. `C:\MyEnv\Scripts\python.exe`).

### The Modern Tauri v2 Architecture
The production-grade rewrite addresses all legacy issues:
* **Tauri v2 Shell**: Replaces PyQt6/Chromium wrappers with OS-native WebViews (WebView2 on Windows, WebKitGTK on Linux, WKWebView on macOS), shrinking application bundle size from ~150MB to <15MB.
* **Rust Backend**: Eliminates Flask and Python runtime dependencies, delivering native speed, memory safety, and direct C-FFI hardware hooks.
* **Tokio Async Engine**: Prevents UI freezing by routing CPU/IO bound tasks through a multi-threaded Tokio worker queue.
* **React + TypeScript + Zustand SPA**: Modern, clean single-page architecture replacing legacy monolithic 189KB single-file JavaScript scripts (`app.js`).

---

## 2. Core Tech Stack Decision Matrix

| Layer | Technology | Purpose & Rationale |
|---|---|---|
| **App Shell** | Tauri v2 Core | Memory-safe, minimal footprint (<15MB installer), strict security permission boundaries. |
| **Backend Core** | Rust 2021 Edition | High performance, no garbage collection pauses, direct C-FFI bindings to hardware/OS APIs. |
| **Async Runtime** | Tokio `1.x` + Rayon | Bounded thread allocation, asynchronous non-blocking task channels for heavy file workloads. |
| **HTTP / Server** | Axum `0.7.x` | High-speed local network web server binding dynamically for local mobile pairing over Wi-Fi. |
| **mDNS Service** | `mdns-sd` crate | Zero-config local network discovery (`_Axora._tcp.local`) for seamless mobile connection. |
| **Cryptography** | `aes-gcm` + `argon2` + `zeroize` | AES-256-GCM authenticated encryption, Argon2id key derivation, memory zeroing for sensitive keys. |
| **Frontend Framework** | React 18 (TypeScript 5) | Strict type safety, component-driven UI architecture. |
| **Styling & UI** | Tailwind CSS 3.4 + Framer Motion | Liquid glass visual language, dark mode by default, smooth micro-animations. |
| **State Management** | Zustand 4.5 | Lightweight, atomic global state management. |

---

## 3. Detailed Component Architecture

### 3.1 Rust Backend Directory Structure (`src-tauri/src/`)
```
src-tauri/src/
├── main.rs                 # Tauri app setup, system tray, window lifecycle, IPC command registry
├── processor.rs            # Core async batch processor & file converter engine
├── vault.rs                # AES-256-GCM file encrypter/decrypter with Argon2id & Zeroize
├── scanner.rs              # Physical hardware scanner interface (WIA/TWAIN via PowerShell COM)
├── settings.rs             # System info queries, theme preferences & autostart handling
├── network/                # Local network pairing & synchronization hub
│   ├── mod.rs              # Axum server, WebSocket listener, pairing endpoint
│   ├── auth.rs             # Session auth token management & HMAC signatures
│   └── mdns_service.rs     # mDNS service advertisement daemon
└── commands/               # Feature domain command handlers
    ├── bureaucrat.rs       # Form Studio: KB resizing, signature extraction, ID card PDF stitching
    ├── academic.rs         # Scholar Kit: Windows Native OCR, PDF redaction, page reordering
    ├── media.rs            # Media Forge: Audio extraction, snippet storage
    ├── settings.rs         # Settings handlers
    └── (stubs: compress.rs, convert.rs, image_batch.rs, scanner.rs, security.rs)
```

### 3.2 Registered Tauri IPC Command Registry
The application exposes the following high-performance IPC functions to the React frontend in `main.rs`:

1. **System & Settings Suite**:
   - `settings::get_download_dir`, `settings::save_settings`, `settings::load_settings`, `settings::update_theme_settings`, `settings::get_system_info`, `settings::get_autostart_enabled`, `settings::set_autostart_enabled`
2. **Security & Vault Suite**:
   - `vault::encrypt_file`: Encrypts arbitrary files using password-derived AES-256-GCM key.
   - `vault::decrypt_file`: Authenticates and decrypts `.Axora` ciphertext containers.
3. **Core Processing Suite**:
   - `processor::batch_process_images`: Async queue processing mass image batches (PNG, JPG, WebP, TIFF).
   - `processor::convert_files`: Document format conversions.
4. **Hardware Scanner Suite**:
   - `scanner::list_scanners`: Enumerates connected WIA/TWAIN physical scanners.
   - `scanner::scan_document`: Triggers flatbed/ADF hardware scan acquisition via PowerShell WIA COM bridge.
5. **Network & Mobile Sync Suite**:
   - `toggle_sync_server`: Starts/stops local Axum server & mDNS advertisement.
   - `get_server_info`: Retrieves current IP, port, token, and public key.
   - `generate_pairing_qr`: Renders compact JSON pairing data into a clean SVG Data URL.
   - `ping_backend`: Health check ping returning backend status.
6. **Form Studio Suite (Bureaucrat)**:
   - `commands::bureaucrat::resize_to_target_kb`: Resizes images down to exact KB boundaries (e.g. 50KB-100KB for government/exam portal uploads via binary search over JPEG quality).
   - `commands::bureaucrat::extract_signature`: Isolates handwritten signatures from scanned documents using threshold contouring.
   - `commands::bureaucrat::stitch_id_card_pdf`: Combines front and back ID card images onto a single A4 PDF.
   - `commands::bureaucrat::compile_ordered_pdf`: Merges ordered image arrays into a single document.
7. **Scholar Kit Suite (Academic)**:
   - `commands::academic::ocr_image_windows`: High-accuracy OCR using Windows Native Media OCR API (`windows::Media::Ocr`).
   - `commands::academic::redact_pdf`: Permanently redacts specified PDF text areas or coordinate bounding boxes.
   - `commands::academic::get_pdf_page_count`, `reorder_pdf_pages`, `rotate_pdf_pages`, `extract_pdf_pages`.
8. **Media Forge Suite (Media)**:
   - `commands::media::extract_audio`: Extracts MP3/AAC/WAV audio streams from video files.
   - `commands::media::save_snippet`, `load_snippets`, `delete_snippet`.

### 3.3 React Frontend Architecture (`src/`)
The frontend is structured into 12 primary view pages and modular UI components:
* `src/pages/Dashboard.tsx`: System overview, quick action cards, workspace telemetry.
* `src/pages/Converter.tsx`: Multi-format document converter UI with drag-and-drop dropzones.
* `src/pages/BatchProcessor.tsx`: Mass batch image processing engine (up to 3,000 images) with live ETA and progress meters.
* `src/pages/Security.tsx`: AES-256-GCM Vault encrypter/decrypter UI.
* `src/pages/Scanner.tsx`: Physical desktop scanner controller (DPI, color mode, flatbed vs ADF).
* `src/pages/MobileLink.tsx`: Link-to-Mobile pairing hub with live SVG QR code rendering and PIN verification.
* `src/pages/EcosystemSync.tsx`: Real-time active connection monitor and file transfer list.
* `src/pages/FormStudio.tsx`: Bureaucrat utility suite (KB resizer, signature extractor, ID card stitcher).
* `src/pages/Academic.tsx`: Scholar Kit OCR engine, PDF page re-orderer, and redaction editor.
* `src/pages/Media.tsx`: Audio extractor and snippet manager.
* `src/pages/Settings.tsx`: App theme customization (Accent colors, Light/Dark/System), autostart, system resource profile allocation.
* `src/components/Sidebar.tsx`: Navigation bar with liquid glass styling and collapse support.
* `src/components/SplashScreen.tsx`: Phased Canvas splash screen animation preventing white/transparent window flash.
* `src/components/MdRipple.tsx`, `ThemeToggle.tsx`, `ToastNotification.tsx`.

---

## 4. Implemented Features vs. Work-In-Progress

### Fully Implemented & Functional:
1. **System Tray & Window Lifecycle**: Native system tray icon with "Open", "Toggle Clipboard Sync", and "Exit Application" context menu options; intercepting close button to minimize to tray instead of quitting.
2. **Modern Security Vault**: Full AES-256-GCM encryption with 16-byte random salt, 12-byte nonce, 16-byte authentication tag, Argon2id KDF, and zeroization of secret keys in Rust memory.
3. **Form Studio (Bureaucrat)**: Target KB image resizing algorithm, signature contour extraction, ID card front/back PDF layout stitcher.
4. **Scholar Kit (Academic)**: Windows Native OCR engine (`windows::Media::Ocr`), PDF page rotation, extraction, reordering, and redaction.
5. **Media Forge**: Fast audio stream extractor and media snippet manager.
6. **Local Network Axum Server**: Dynamic port binding, ECDH session key exchange, mDNS discovery (`_Axora._tcp.local`), and real-time SVG QR code generation for pairing.
7. **Mass Image Batch Engine**: Multi-threaded process queue for large batch processing.

### Planned & Work In Progress (Cross-referencing `ROADMAP.md` & `FEATURES_SPEC.md`):
1. **LibreOffice Headless Sidecar Integration** (Phase 2-3): While image-to-PDF is native, high-fidelity DOCX/PPTX to PDF conversion relies on LibreOffice CLI sidecar. The background monitor and process timeout wrapper (60s/180s limits) need final packaging.
2. **Ghostscript Vector Compression Profiles** (Phase 3): Low (95% quality), Medium (150 DPI / 75% quality), High (72 DPI / 50% quality) PDF compression using Ghostscript CLI.
3. **Cross-Platform Hardware Scanner Drivers** (Phase 6): Windows WIA/TWAIN driver implementation is active in `scanner.rs` via PowerShell COM; SANE (`sane-sys` for Linux) and ImageCaptureCore (macOS Objective-C FFI) driver wrappers are scheduled for cross-platform releases.
4. **Extended AI Sidecar Roadmap** (Phase 11-14):
   - **Local AI Chat & QA**: `llama-server` sidecar process + Qwen 1.5B GGUF.
   - **PDF RAG Indexer**: ONNX MiniLM vector embeddings (`ort` crate) + SQLite Vector (`sqlx`).
   - **Voice Transcriber**: Native Rust static wrapper around `whisper.cpp`.
   - **Math Solver & Background Remover**: SymPy parsing and U2Net ONNX background removal.

---

## 5. Ecosystem Alignment with Axora Mobile

Axora Desktop pairs directly with **Axora Mobile** over local Wi-Fi without cloud dependencies:
* **Zero-Config Discovery**: Desktop advertises `_Axora._tcp.local` via mDNS. Mobile discovers desktop automatically on the local network.
* **Cryptographic Handshake**: Desktop displays a QR code containing `{ ip, port, token, pubkey }`. Mobile scans QR, initiating an ECDH (Elliptic Curve Diffie-Hellman) exchange to derive a shared session key.
* **HMAC Message Authentication**: All incoming local HTTP requests from mobile require an `X-Client-Signature` header calculated using HMAC-SHA256.
* **Streamlined Transfer Protocol**: Replaces Base64-in-JSON strings with high-speed **Multipart HTTP Uploads** over Axum endpoints, preventing memory crashes on large files.

---

## 6. Identified Issues, Code Smells & Architectural Vulnerabilities

| Issue ID / Location | Severity | Description | Impact & Resolution Strategy |
|---|---|---|---|
| **Hardcoded Argon2id Salt** (`vault.rs`) | **Critical (Security)** | `vault.rs` uses a hardcoded salt string `b"AxoraSalt!1"`. | Generate a cryptographically random 16-byte salt per file using `rand`, prepend it to the file header, and read it during decryption. |
| **Rayon Heap Allocation Spike Risk** (`processor.rs`) | **High (Performance)** | Batch engine currently uses `rayon` parallel iterators rather than a bounded `tokio::sync::mpsc` channel. | Replace `rayon` with a bounded channel with backpressure to prevent system heap exhaustion on 3,000+ image batches. |
| **PowerShell COM Dependency** (`scanner.rs`) | **Medium (Architecture)** | Hardware scanner listing writes temporary `.ps1` PowerShell scripts to execute WIA COM calls. | Migrate from PowerShell script generation to native C-FFI or direct Windows API bindings to support clean execution on Linux/macOS. |
| **Command Stub Files** (`src-tauri/src/commands/convert.rs`, `compress.rs`, `image_batch.rs`, `security.rs`, `scanner.rs`) | **Medium (Code Smell)** | 30-byte stub files created during early refactoring while logic lives in `processor.rs`, `vault.rs`, `scanner.rs`. | Route command calls through `src-tauri/src/commands/` for clean modular separation. |
| **Windows-Specific OCR API** (`academic.rs`) | **Medium (Portability)** | Uses `windows::Media::Ocr` directly without non-Windows fallback. | Wrap Windows OCR in `#[cfg(target_os = "windows")]` and provide Tesseract / ONNX fallbacks for Linux & macOS. |

---

## 7. Actionable Recommendations & Prioritized Implementation Plan

### Immediate Term (High Priority Fixes):
1. **Fix Vault Cryptography**: Replace the hardcoded Argon2id salt `b"AxoraSalt!1"` in `vault.rs` with a 16-byte random salt generated per file using `rand::thread_rng()`, stored in the `.Axora` file header.
2. **Add Target OS Guard Flags**: Add `#[cfg(target_os = "windows")]` around `commands/academic.rs` Windows Media OCR calls to ensure clean cross-platform compiling on macOS and Linux.
3. **Consolidate Command Modules**: Refactor the stub command files in `src-tauri/src/commands/` so that Tauri handlers are cleanly mapped to their implementation modules.

### Near Term (Next 2-4 Weeks):
1. **Bounded Async Task Queue**: Migrate batch processing in `processor.rs` from `rayon` to a bounded `tokio` channel to enforce strict RAM ceiling limits (e.g. 70% RAM cap).
2. **Automate CLI Dependency Detection**: Implement a dependency checker command in `settings.rs` that checks if `libreoffice` and `ghostscript` are present on system `PATH` and reports status to `Settings.tsx`.

### Long Term (Phases 11-14 Extended AI Suite):
1. **Implement Portable Sidecar Manager**: Create a Rust subprocess manager for spawning `llama-server` and `whisper.cpp` sidecars on demand without locking system memory when unused.
