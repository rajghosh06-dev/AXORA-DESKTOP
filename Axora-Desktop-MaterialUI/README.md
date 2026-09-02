# Axora Desktop — Universal Document Processing & Offline AI Studio

**Axora Desktop** is a high-performance desktop application built with **Tauri v2 (Rust 2021)** and **React 18 (TypeScript + Tailwind CSS + Framer Motion)**. It provides zero-cloud privacy, Argon2id per-file random salt cryptography, native Windows 11 OCR, an SM-2 Spaced Repetition Flashcard Studio, a Media Forge suite, and a local 384-dimensional ONNX Vector RAG engine.

---

## 🔑 Key Features & Subsystems

1. **AxoraVault (Zero-Trust Security)**
   - Per-file cryptographically secure 16-byte random salt (`rand::thread_rng()`).
   - Argon2id Key Derivation Function (KDF) + AES-256-GCM authenticated encryption.
   - Zero hardcoded salts, protected headers, and zero-knowledge memory handling.

2. **Scholar Kit & Windows 11 Native OCR**
   - Offline text extraction using `windows::Media::Ocr` (Windows 10/11 native API).
   - Cross-platform target abstraction (`#[cfg(target_os = "windows")]`) with fallback error stubs for macOS and Linux.
   - Vector RAG search overlay: natural language concept search with similarity score ranking (`% Match`).

3. **Spaced Repetition Studio (SM-2 Engine)**
   - SuperMemo-2 (SM-2) algorithm calculating optimal review intervals ($I$) and easiness factors ($EF$).
   - Anki `.apkg` export integration via Zip compression.
   - Interactive Framer Motion SVG memory retention decay curves.

4. **Global Command Palette (`Ctrl+K` / `Cmd+K`)**
   - Fuzzy search command index with keyboard navigation (`ArrowUp`/`ArrowDown`/`Enter`).
   - 1-tap quick action triggers ("Open Flashcard Studio", "Toggle Quick Drop", "Switch Theme", "Open Vault").

5. **Quick Drop Drawer & Shared Clipboard**
   - Zero-trust P2P local Wi-Fi handshake over encrypted WebSockets.
   - Real-time text, link, and file snippet sync between Axora Mobile and Axora Desktop.

---

## 🛠️ Architecture & Tech Stack

- **Desktop Framework**: Tauri v2, Rust 2021 (Tokio, Axum 0.7.x)
- **Frontend SPA**: React 18, TypeScript, Tailwind CSS, Framer Motion, Zustand
- **Cryptography**: Argon2id, AES-256-GCM (`ring` / `aes-gcm`), `rand`
- **Vector RAG Engine**: 384-dimensional ONNX normalized float vector projection & Cosine Similarity search

---

## 🚀 Building & Running

### Prerequisites
- [Rust & Cargo](https://rustup.rs/) (edition 2021)
- [Node.js](https://nodejs.org/) v18+ & npm

### Development Server
```bash
# Install Node dependencies
npm install

# Run Tauri dev mode (Frontend + Rust Backend)
npm run tauri dev
```

### Production Build
```bash
# Verify TypeScript build
npm run build

# Compile Tauri production binaries (.exe / .msi)
npm run tauri build
```
