# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

This is a Tauri v2 application with React/TypeScript frontend and Rust backend:

- `npm run dev` - Start development server (Vite + Tauri)
- `npm run build` - Build for production (TypeScript compile + Vite build)
- `npm run preview` - Preview production build locally
- `npm run tauri` - Direct Tauri CLI access (use `npm run tauri dev` for dev, `npm run tauri build` for production build)

Common development workflow:
1. Start development: `npm run dev`
2. Make changes to frontend (src/) or backend (src-tauri/src/)
3. Changes hot-reload in development mode
4. For production builds: `npm run build` followed by `npm run tauri build`

## Code Architecture & Structure

### High-Level Architecture (Tauri v2)

The application follows a split architecture documented in ARCHITECTURE.md:

```
TAURI APP WRAPPER
├── React Frontend (TypeScript, Tailwind) 
│   ├── Minimalist UI with Zustand state management
│   ├── Framer Motion for animations
│   └── Virtual Lists for large datasets
├── IPC Communication (JSON serialization)
└── Rust Backend (Tauri Core)
    ├── Request Handlers (Tauri commands)
    ├── Scoped Filesystem Guards
    ├── Tokio Runtime for async operations
    └── Security Manager
    
Rust Backend spawns/controls:
└── Rust Task Queue (Worker Threads)
    ├── ImageMagick FFI for image processing
    ├── LibreOffice Sidecar for document conversion
    ├── AES-256-GCM encryption (ring crate)
    ├── TWAIN/SANE Drivers for scanner integration
    └── Local mDNS Server for device discovery
```

### Frontend Structure (src/)
- `src/App.tsx` - Main application component
- `src/main.tsx` - React entry point
- `src/pages/` - Individual page views (Dashboard, Converter, Scanner, etc.)
- `src/components/` - Reusable UI components (Sidebar, ThemeToggle, ToastNotification, etc.)
- `src/store/` - Zustand state stores (themeStore.ts, toastStore.ts)
- `src/styles/` - TailwindCSS configuration (index.css)
- `src/assets/` - Static assets (SVG, images)

### Backend Structure (src-tauri/src/)
- `src-tauri/src/main.rs` - Tauri application setup and event loop
- `src-tauri/src/commands/` - Tauri IPC command handlers (conversion, compression, security, scanner, etc.)
- `src-tauri/src/services/` - Core business logic (file_manager.rs, state_manager.rs)
- `src-tauri/src/hardware/` - Scanner & printer bindings (TWAIN/WIA, SANE, macOS ImageCapture)
- `src-tauri/src/network/` - Local Axum server, WebSocket handler, mDNS advertiser
- `src-tauri/src/crypto/` - AES-256-GCM & Argon2id encryption wrapper

### Key Technologies
- **Frontend**: React 18, TypeScript, TailwindCSS, Framer Motion, Zustand, Lucide icons
- **Build**: Vite, TypeScript compiler
- **Backend**: Rust, Tauri v2, Tokio async runtime
- **Communication**: Tauri IPC (Commands for request/response, Events for pub/sub)
- **Security**: CSP headers, scoped filesystem access, Argon2id key derivation, AES-256-GCM encryption
- **Hardware Integration**: TWAIN/WIA (Windows), SANE (Linux), ImageCaptureCore (macOS) for scanners

### Common Development Patterns
1. **State Management**: Zustand stores in `src/store/` - use `useStore()` hook to access state
2. **Styling**: TailwindCSS utility classes - modify `tailwind.config.js` for theme changes
3. **IPC Communication**: 
   - Frontend → Backend: Use `invoke()` from `@tauri-apps/api`
   - Backend → Frontend: Use `app_handle.emit()` for events or return values from commands
4. **Adding New Features**:
   - Add Tauri command in `src-tauri/src/commands/` 
   - Register command in `src-tauri/src/main.rs`
   - Call command from frontend using `invoke('command_name', { payload })`
   - Handle events with `listen()` or `once()` from `@tauri-apps/api/event`

### Security Considerations (Built-in)
- Tauri defaults to secure WebView (no network exposure)
- Filesystem access restricted to user-selected directories and app data directories
- Content Security Policy prevents inline script execution
- Local network server binds only to private interfaces with origin validation
- Sensitive data protected with zeroize crate and secure memory handling

### Frequently Accessed Directories
- App data directories (configurable via Tauri):
  - Windows: `%APPDATA%\Axora`
  - macOS: `~/Library/Application Support/Axora`
  - Linux: `~/.config/Axora`
- Temporary processing: System temp directories with automatic cleanup

## CUSTOM INSTRUCTIONS/NOTES [KEEP IN MIND]:
- Maintain a **<log.md>** which you need to everytime refer before starting working inorder to check what you performed and what all are done!
- **Stability & Baseline Status:** This repository is the stable, reference implementation of Axora. Treat existing architecture, data schemas, and business logic as established ground truth.
- **Read-Heavy Reference:** Prioritize reading and analyzing existing code structures over restructuring them. Do not perform large-scale refactoring unless explicitly requested.
- **Maintain Compatibility:** Ensure any new tweaks or bug fixes do not break existing features, IPC protocols, or data models that the mobile client relies upon.
- **Code Style & Patterns:** Strictly follow the established coding patterns, directory structures, and naming conventions already present across the codebase.
- **Documentation Parity:** When updating APIs, data models, or core logic, update comments and documentation so the mobile development context stays aligned.
- **Verification:** Run all standard tests and verification scripts after making any changes to guarantee project stability.
- Maintain and update the **<log.md>** after every action with the format -> <[DATE,TIME] (TASK/ACTION SMALL SUMMARY): TASK/ACTION Detailed Information (atmost 2-3 lines)>. And you need to everytime refer before starting working inorder to check what you performed and what all are done! Also make sure to **append all the latest logs at bottom**, so that you need not replace the entire log.md!
- Use your creativity, intelligence, productivity  and innovation and implement the best!!