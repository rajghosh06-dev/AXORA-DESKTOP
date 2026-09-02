# Rule 04: MaterialUI Application Standards

## Technology Stack Conventions
- **Framework**: Tauri v2 (`@tauri-apps/api` ^2.0.0, `@tauri-apps/cli` ^2.0.0) + React 18.2.0 + TypeScript 5.2.2 + Vite 5.1.0.
- **Design System**: Material Design 3 (MD3) custom tokens declared in `src/styles/index.css` using CSS custom properties (`--md-sys-color-*`, `--md-elevation-*`).
- **State Management**: Lightweight Zustand stores in `src/store/` (`themeStore.ts`, `toastStore.ts`, `useQuickDropStore.ts`).
- **Backend Rust Core**: Rust 2021 edition located in `src-tauri/src/`. All IPC commands must be registered in `src-tauri/src/main.rs` via `tauri::generate_handler![]`.

## Quality Guidelines
- **IPC Safety**: Always handle Tauri command rejection in the frontend with try/catch and user-facing toast notifications via `useToast()`.
- **Navigation Integrity**: When adding or renaming pages in `src/pages/`, update the route switch in `src/App.tsx`, the navigation rail in `src/components/Sidebar.tsx`, and the `CommandPalette.tsx` mappings.
- **Window Startup**: Preserve the 120ms hidden window initialization delay in `main.rs` to allow React to mount the `SplashScreen` and eliminate white window flashing.
- **Backend Compilation**: Always test Rust backend changes with `cargo test --manifest-path src-tauri/Cargo.toml` and frontend build with `npm run build`.
