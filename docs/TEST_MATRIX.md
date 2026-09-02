# Axora Desktop — Comprehensive Test Matrix

This document defines the testing coverage across both desktop implementations, categorizing test capabilities into **Automated**, **Semi-Automated**, and **Manual**.

---

## 1. WinUI Application Test Matrix

| Category | Test Scenario | Automation Level | Test Location / Method |
|---|---|---|---|
| **Build & Toolchain** | Unpackaged x64 Debug & Release Compilation | **Automated** | `.\scripts\qa\build-all.ps1 -Target WinUI` |
| **Startup & Mica** | Process launch, DI build, Mica Alt activation, NotifyIcon | **Automated** | `.\scripts\qa\smoke-test.ps1 -Target WinUI` (inspects `startup.log`) |
| **Vector PDF Compiler** | Empty resume, 5000+ words stress, markdown sanitization | **Automated** | `Axora.Desktop.Tests/Program.cs` (M3.1 - M3.4) |
| **PDF Layout Matrix** | 75 font family, margin & line spacing combinations | **Automated** | `Axora.Desktop.Tests/Program.cs` (M3.5) |
| **9-Section CV Compiler** | Full vector document compilation with headers & footers | **Automated** | `Axora.Desktop.Tests/Program.cs` (M3.6) |
| **Flashcard Deck Reactivity** | ObservableProperty notifications for CardCount & RetentionRate | **Automated** | `Axora.Desktop.Tests/Program.cs` (M4.1 - M4.2) |
| **SM-2 Algorithm Stress** | 1,000-iteration exponential rating & DateTime overflow defense | **Automated** | `Axora.Desktop.Tests/Program.cs` (M4.3) |
| **Empty Deck Resilience** | Flip, Next, Prev, Rate actions on empty decks without crash | **Automated** | `Axora.Desktop.Tests/Program.cs` (M4.4) |
| **Text Note Parser** | Automatic flashcard generation from colon-separated lines | **Automated** | `Axora.Desktop.Tests/Program.cs` (M4.6) |
| **Batch Image Queue** | Observable property notifications and size string formatting | **Automated** | `Axora.Desktop.Tests/Program.cs` (M4.7) |
| **Corrupted File Defense** | 0-byte corrupted image files defensive handling | **Automated** | `Axora.Desktop.Tests/Program.cs` (M4.8) |
| **Concurrency Stress** | 50 rapid concurrent failure callbacks without thread deadlock | **Automated** | `Axora.Desktop.Tests/Program.cs` (M4.9) |
| **Navigation Rail** | Switching between 10 navigation views without frame duplication | **Semi-Automated** | UIA3 / FlaUI automation or manual click-through |
| **Command Palette** | Ctrl+K hotkey activation, fuzzy search, and item invocation | **Semi-Automated** | Keyboard event injection (`Ctrl+K`) |
| **Theme Switching** | Dark / Light theme transition and dynamic brush updates | **Manual** | Visual inspection against UI Quality Checklist |
| **Window Subclassing** | `WM_GETMINMAXINFO` clamping window size to 1000x620 DIP | **Manual** | Drag-resizing window to minimum bounds |

---

## 2. MaterialUI Application Test Matrix

| Category | Test Scenario | Automation Level | Test Location / Method |
|---|---|---|---|
| **Frontend Build** | TypeScript compilation (`tsc`) and Vite asset bundling | **Automated** | `npm run build` |
| **Backend Unit Tests** | Rust Tokio core compilation and 8 unit test assertions | **Automated** | `cargo test --manifest-path src-tauri/Cargo.toml` |
| **RAG Cosine Vectors** | Identical (1.0), Orthogonal (0.0), Opposite (-1.0) vectors | **Automated** | `src-tauri/src/commands/rag.rs` (`test_cosine_*`) |
| **RAG Chunking Engine** | 512-character window and 50-character overlap assertions | **Automated** | `src-tauri/src/commands/rag.rs` (`test_chunking`) |
| **Cryptographic Roundtrip** | Argon2id KDF + AES-256-GCM encryption & decryption | **Automated** | `src-tauri/src/vault.rs` (`test_encrypt_decrypt_roundtrip`) |
| **Tampered Ciphertext** | AEAD authentication tag rejects modified ciphertext bytes | **Automated** | `src-tauri/src/vault.rs` (`test_tampered_ciphertext`) |
| **Sandbox Policy** | MXC execution containment policy rules | **Automated** | `src-tauri/src/commands/sandbox.rs` (`test_mxc_validation`) |
| **Splash Screen Unhide** | 120ms window unhide delay eliminating white flash | **Automated** | `src-tauri/src/main.rs` (verified by inspection) |
| **DOM & Layout** | Material Design 3 surface containers and token hierarchy | **Semi-Automated** | Playwright / Chrome DevTools MCP inspection |
| **Toast Notifications** | Zustand `toastStore` message queue push and auto-dismiss | **Semi-Automated** | Webview2 console event verification |
| **Mobile Link QR** | SVG QR code rasterization of pairing payload | **Manual** | Visual scanning with Android device |
