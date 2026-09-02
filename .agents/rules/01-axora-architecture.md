# Rule 01: AXORA Architecture & Workspace Boundaries

## Universal Workspace Principles
- **Dual Implementation Model**: `AXORA-DESKTOP` contains two distinct native desktop applications:
  1. `Axora-Desktop-MaterialUI`: Cross-platform hybrid built on **Tauri v2 + React 18 + Tailwind CSS (Material Design 3 tokens) + Rust 2021**.
  2. `Axora-Desktop-WinUI`: Pure native Windows 11 desktop application built on **.NET 9 + Windows App SDK 1.6 / WinUI 3 + XAML + C# 13**.
- **Architectural Independence**: Never attempt to unify or merge internal code structures, shared class libraries, or identical codebases between the two applications. Each application must preserve its own idiomatic tech stack, design philosophy, and runtime model.
- **Functional Parity Goal**: Keep intended product capabilities (Document processing, OCR, SM-2 Spaced Repetition, AES-GCM Encrypted Vault, Hardware Scanner, Mobile Link) equivalent while respecting platform UI paradigms.
- **Scoped Edits**: When working on a MaterialUI task, do not touch WinUI files, and vice versa. Keep modifications tightly scoped to the target application.
