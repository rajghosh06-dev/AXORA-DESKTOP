# Axora Desktop — System Architecture & Implementation Guide

## 1. System Vision & Dual Implementation Strategy

**Axora Desktop** is a privacy-first, local-first workstation productivity suite providing document intelligence, vector PDF compilation, offline OCR, spaced repetition study systems, hardware scanning, zero-trust encrypted file vaults, and local P2P mobile synchronization.

The workspace `AXORA-DESKTOP` contains two distinct, parallel desktop implementations:

```
AXORA-DESKTOP/
├── Axora-Desktop-MaterialUI/     # Tauri v2 + React 18 + Tailwind MD3 + Rust Core
└── Axora-Desktop-WinUI/          # .NET 9 + Windows App SDK 1.6 / WinUI 3 + XAML + C# 13
```

### Architectural Separation Principle
The two applications share the overall product domain and feature concepts, but intentionally use different technology stacks and design systems:
- **MaterialUI**: Built with web standards (React 18 + TypeScript + Tailwind CSS MD3 design tokens) encapsulated within a Tauri v2 native Rust shell.
- **WinUI**: Built with pure native Windows technologies (.NET 9 + Windows App SDK 1.6 + WinUI 3 XAML controls + Mica Alt backdrop + DirectML AI acceleration).

---

## 2. Axora-Desktop-MaterialUI Architecture

```
User Action (React 18 UI)
  │
  ├── React Components / Zustand Stores (themeStore, toastStore, useQuickDropStore)
  │     │
  │     ▼ (tauri-apps/api invoke)
  └── Tauri IPC Bridge (JSON serialization over named pipe)
        │
        ▼ (Tauri Command Handlers in src-tauri/src/commands/)
      Rust Core Services (Tokio Multi-threaded Runtime)
        ├── AxoraVault (Argon2id + AES-256-GCM AEAD) ──► Encrypted .axora containers
        ├── Axum HTTP & WebSocket Server ──────────────► Android P2P Wi-Fi Sync
        ├── Windows 11 Native OCR (WinRT FFI) ─────────► Extracted text stream
        └── Spaced Repetition SM-2 Engine ─────────────► Anki .txt / JSON exports
```

---

## 3. Axora-Desktop-WinUI Architecture

```
User Action (WinUI 3 XAML View)
  │
  ├── Data Binding ({Binding ...}) / XAML Event Handlers
  │     │
  │     ▼ (RelayCommand / [ObservableProperty])
  └── CommunityToolkit.Mvvm ViewModels (Registered in Microsoft.Extensions DI)
        │
        ▼ (Injected Service Contracts: Axora.Desktop.Services.Contracts.I*Service)
      C# Domain Services (.NET 9 Task-based Async / ThreadPool)
        ├── ResumePdfCompilerService (PdfSharpCore) ──► Multi-page vector PDF documents
        ├── DirectMlEmbeddingService (ONNX DirectML) ─► GPU/NPU vector embeddings
        ├── StreamingVaultService (Argon2 + AesGcm) ──► Encrypted streaming files
        ├── P2pSyncService (System.Net.Sockets) ──────► Mobile P2P Network
        └── WiaScannerService (COM Interop) ──────────► WIA 2.0 Scanner Data
```

---

## 4. Key Subsystem Capabilities

| Subsystem | MaterialUI Implementation | WinUI Implementation |
|---|---|---|
| **Encrypted Vault** | `src-tauri/src/vault.rs` (Argon2id + AES-256-GCM) | `StreamingVaultService.cs` + TPM 2.0 sealing |
| **Spaced Repetition** | `anki.rs` (SM-2 Easiness Factor formula) | `FlashcardsViewModel.cs` + Windows Speech TTS |
| **Document OCR** | `academic.rs` (`windows` crate `Media_Ocr`) | `WinRtOcrService.cs` (`Windows.Media.Ocr`) |
| **Semantic Search** | `rag.rs` (384-dim ONNX embeddings) | `DirectMlEmbeddingService.cs` (DirectML GPU) |
| **Mobile P2P Sync** | `network/` (Axum server + mDNS + QR) | `P2pSyncService.cs` (TCP sockets + QR) |
| **Hardware Scanning** | `scanner.rs` (WIA COM FFI) | `WiaScannerService.cs` (WIA 2.0 COM Interop) |
| **Batch Image Ops** | `processor.rs` (multi-threaded resizing) | `BatchImageProcessorService.cs` (SkiaSharp) |
