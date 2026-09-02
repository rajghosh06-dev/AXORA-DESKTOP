---
name: axora-materialui
description: Development, architecture, IPC command integration, and component conventions for the Axora MaterialUI desktop application (Tauri v2 + React 18 + Tailwind CSS + Rust). Use when adding features, modifying UI pages, or writing Rust backend commands in Axora-Desktop-MaterialUI.
---

# Axora MaterialUI Development Guide

This skill documents the architecture, directory layout, IPC conventions, and styling rules for `Axora-Desktop-MaterialUI`.

## Architecture Overview
- **Shell**: Tauri v2 (`@tauri-apps/api` ^2.0.0, `@tauri-apps/cli` ^2.0.0).
- **Frontend**: React 18.2.0 + TypeScript 5.2.2 + Vite 5.1.0.
- **Styling**: Tailwind CSS 3.4 + Material Design 3 CSS custom properties (`src/styles/index.css`).
- **Backend Core**: Rust 2021 edition located in `src-tauri/`.

## Key Directory Layout
- `src/App.tsx`: Main route table and page transition manager (`AnimatePresence`).
- `src/pages/`: Application screens (`Dashboard.tsx`, `Converter.tsx`, `Security.tsx`, `Academic.tsx`, `FormStudio.tsx`, `Media.tsx`, `FlashcardStudio.tsx`, `MobileLink.tsx`, `Scanner.tsx`, `Settings.tsx`).
- `src/components/`: Reusable MD3 components (`Sidebar.tsx`, `CommandPalette.tsx`, `FileDropZoneOverlay.tsx`, `ToastNotification.tsx`, `MdRipple.tsx`).
- `src/store/`: Zustand stores (`themeStore.ts`, `toastStore.ts`, `useQuickDropStore.ts`).
- `src-tauri/src/main.rs`: Tauri startup, command registration, and system tray menu.
- `src-tauri/src/commands/`: Command modules (`academic.rs`, `anki.rs`, `bureaucrat.rs`, `media.rs`, `rag.rs`, `sandbox.rs`).
- `src-tauri/src/vault.rs`: Argon2id KDF and AES-256-GCM file encryption.
- `src-tauri/src/network/`: Axum local HTTP/WebSocket server and mDNS pairing service.

## IPC Command Pipeline
When creating or modifying features:
1. **Define Rust Command** in `src-tauri/src/commands/<module>.rs`:
   ```rust
   #[tauri::command]
   pub async fn my_command(payload: String) -> Result<String, String> {
       // logic
       Ok("Success".to_string())
   }
   ```
2. **Register Command** in `src-tauri/src/main.rs` inside `tauri::generate_handler![]`.
3. **Invoke from React Frontend**:
   ```typescript
   import { invoke } from "@tauri-apps/api/core";
   const result = await invoke<string>("my_command", { payload: "value" });
   ```
4. **Toast Feedback**: Always wrap invocations with `useToast()` to display success or error notifications.

## Development Commands
- Compile Frontend: `npm run build` (within `Axora-Desktop-MaterialUI`)
- Run Rust Unit Tests: `cargo test --manifest-path src-tauri/Cargo.toml -- --nocapture`
- Start Dev Server: `npm run dev`
