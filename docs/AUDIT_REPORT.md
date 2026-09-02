# AXORA Desktop — Comprehensive Repository & Infrastructure Audit Report

**Date**: 2026-09-02  
**Auditor**: Antigravity Workspace Architect & Principal Engineer  
**Workspace Root**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Git Remote**: `https://github.com/rajghosh06-dev/AXORA-DESKTOP.git` (Private)  

---

## 1. Repository Overview

`AXORA-DESKTOP` is a multi-project desktop workspace providing a privacy-first, zero-trust productivity workstation. It contains two distinct implementations of the AXORA product:

1. **`Axora-Desktop-MaterialUI`**: Web-standard hybrid desktop implementation built with **Tauri v2 + React 18 + Tailwind CSS (Material Design 3 tokens) + Rust Tokio Core**.
2. **`Axora-Desktop-WinUI`**: Pure native Windows 11 desktop implementation built with **.NET 9 + Windows App SDK 1.6 + WinUI 3 XAML + C# 13 + CommunityToolkit.Mvvm**.

The repository is structured to keep application implementations architecturally decoupled while sharing root-level engineering standards, QA contracts, PowerShell automation scripts, and Antigravity `.agents` customizations.

---

## 2. Axora-Desktop-MaterialUI Project Status

- **Technology Stack**:
  - UI: React 18.2.0, TypeScript 5.2.2, Tailwind CSS 3.4.1 (Material Design 3 tokens), Lucide React, Framer Motion.
  - Runtime: Tauri v2 (`@tauri-apps/api` ^2.0.0, `@tauri-apps/cli` ^2.0.0).
  - Backend: Rust 2021 edition (`src-tauri/Cargo.toml`), Tokio async runtime, Axum local server, Argon2id + AES-256-GCM vault, WinRT OCR bindings.
- **Dependency Status**:
  - `package.json` and `package-lock.json` (104 KB) present.
  - `node_modules/` present on disk.
  - `Cargo.lock` (160 KB) and `src-tauri/target/` present on disk.
  - Pre-bundled frontend web assets present in `dist/` (`index.html` + `assets/`).
- **Host Toolchain Status**:
  - Node.js and Rust/Cargo are not installed on the system PATH on this host machine.
  - Compilation of MaterialUI was skipped due to missing host binaries; existing pre-built assets and intact source files are preserved.
- **Verification Confidence**: `SOURCE VERIFIED` (code inspected, manifests intact).

---

## 3. Axora-Desktop-WinUI Project Status

- **Technology Stack**:
  - Framework: .NET 9.0 (`net9.0-windows10.0.26100.0`, `win-x64`), Windows App SDK 1.6.250228001, C# 13.
  - Pattern: Strict MVVM (`CommunityToolkit.Mvvm` 8.4) with Microsoft.Extensions Dependency Injection (19 services + 10 viewmodels registered in `App.xaml.cs`).
  - Windowing: `MainWindow.cs` using `MicaBackdrop` (`Kind = MicaKind.BaseAlt`), tall custom titlebar, and `WM_GETMINMAXINFO` subclassing (1000x620 DIP minimum constraint).
  - Services: `ResumePdfCompilerService` (`PdfSharpCore`), `DirectMlEmbeddingService` (`Microsoft.ML.OnnxRuntime.DirectML`), `StreamingVaultService` (`Argon2` + `AesGcm`), `WiaScannerService`, `P2pSyncService`.
- **Toolchain Status**:
  - .NET SDK: 10.0.400 / 9.0.317 installed and active.
  - Compiler: Visual Studio 2026 Community MSBuild (`v18.9`) and CLI `dotnet build` both functional.
- **Verification Confidence**: `SOURCE VERIFIED` | `BUILD VERIFIED` | `TEST VERIFIED` | `RUNTIME VERIFIED`.

---

## 4. Antigravity Customizations Audit (.agents/)

All configuration in root `.agents/` adheres strictly to the official Antigravity specification:

| Directory / File | Quantity / Type | Status | Assessment |
|---|---|---|---|
| `.agents/rules/` | 6 Markdown rules | **VALID** | Concise, high-signal rules establishing architecture boundaries, 5-tier verification, Git safety, MD3 standards, WinUI compilation invariants, and UI quality. |
| `.agents/skills/` | 6 Skills | **VALID** | All contain valid `SKILL.md` frontmatter, actionable procedures, and correct script paths (`axora-materialui`, `axora-ui-qa`, `axora-winui`, `build-validation`, `regression-testing`, `ui-bug-diagnosis`). |
| `.agents/workflows/` | 7 Workflows | **VALID** | Standard slash commands (`axora-audit`, `axora-build`, `axora-test`, `axora-ui-qa`, `axora-fix`, `axora-verify`, `axora-pr`) with non-recursive, deterministic sequences. |
| `.agents/agents/` | 6 Subagents | **VALID** | Complementary, non-overlapping roles (`build-engineer`, `code-reviewer`, `materialui-specialist`, `qa-engineer`, `ui-reviewer`, `winui-specialist`). |
| `.agents/hooks.json` | 1 Pre-build hook | **VALID** | Invokes `pre-build-clean.ps1` to prevent `MSB3021/MSB3027` locked executable errors before compilation. |
| `.agents/mcp_config.json` | 1 MCP server | **VALID** | Configures `@modelcontextprotocol/server-chrome-devtools` for webview DOM inspection without extraneous servers. |

### WinUI Subproject Legacy `.agents` Inventory
- **Location**: `Axora-Desktop-WinUI/.agents/`
- **Contents**: 107 files across 29 directories (historical worker dispatches from `auditor_1`, `challenger_1..3`, `explorer_*`, `orchestrator_1`, `reviewer_1..3`, `suborch_*`, `worker_1..2`, and `rules/winui3_xaml_invariants.md`).
- **Classification**: Historical ephemeral logs. No code or script in the solution references these files.
- **Action Taken**: In accordance with user directives, **0 files were deleted**. The folder is safely ignored by Git via root `.gitignore` to keep the version control history clean while preserving all files intact on local disk.

---

## 5. QA Automation Scripts Audit (`scripts/qa/`)

All scripts in `scripts/qa/` were audited, tested, and hardened for 100% compatibility across both Windows PowerShell 5.1 and PowerShell 7 (pwsh):

1. **`build-all.ps1`**:
   - Resolves MSBuild automatically via candidate locations and `vswhere.exe`.
   - Added automatic fallback to `dotnet build` CLI if standalone MSBuild is not located.
   - Fixed Unicode character encoding to ASCII-safe hyphenation.
2. **`run-tests.ps1`**:
   - Replaced hardcoded assertion counts with dynamic regex parsing of `TEST RUN SUMMARY: Total: X | Passed: Y | Failed: Z`.
   - Added multi-path resolution for test executables (`bin\x64\Debug\` and `bin\Debug\`).
   - Cleanly reports skipped targets when toolchains are unavailable.
3. **`smoke-test.ps1`**:
   - Fixed string termination error in Windows PowerShell 5.1 caused by multi-byte em-dash character encoding.
   - Enhanced runtime verification to assert PID liveness and validate `startup.log` phases.
4. **`pre-build-clean.ps1`**:
   - Safely terminates active instances of `Axora.Desktop`, `axora-desktop`, and `Axora.Desktop.Tests` to release file locks.
5. **`audit-workspace.ps1`**:
   - Performs rapid environment diagnostics covering Git status, .NET SDK, MSBuild, Node.js, Cargo, and `.agents` customization counts.

---

## 6. Real Build, Test & Runtime Validation Results

### A. Build Validation
- **WinUI MSBuild**: `PASS` (0 Errors, 0 Warnings from MSBuild runner).
- **WinUI `dotnet build`**: `PASS` (0 Errors, 222 AOT compatibility warnings from WinRT CsWinRT generators).
- **MaterialUI Build**: `SKIPPED` (Node.js/Cargo not in current host PATH; pre-built `dist/` verified).

### B. Test Suite Execution
- **Command Executed**: `.\Axora-Desktop-WinUI\Axora.Desktop.Tests\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.Tests.exe`
- **Result**: `PASS`
- **Metrics**:
  - **Total Discovered**: 59
  - **Executed**: 59
  - **Passed**: 59 (100%)
  - **Failed**: 0
  - **Coverage Areas**:
    - Resume PDF Vector Compiler Stress Testing (M3.1–M3.6): empty resume handling, 5,000+ word multi-page documents, oversized continuous tokens, consecutive newlines/markdown sanitization, 75 font/margin matrix combinations, 9-section full CV.
    - Flashcards SM-2 & Deck Reactivity (M4.1–M4.6): property change notifications, exponential rating stress, division-by-zero defense on empty decks, text-to-card parsing.
    - Batch Image Queue Reactivity (M4.7–M4.10): size formatting, 0-byte corrupted image defense, 50 rapid concurrent failure callbacks, folder scanner.

### C. Runtime Smoke Test Execution
- **Command Executed**: `.\scripts\qa\smoke-test.ps1 -Target WinUI`
- **Result**: `PASS`
- **PID Launched**: Active and verified alive during startup phase.
- **Log Verification (`startup.log`)**:
  - `Program.Main started`
  - `ComWrappersSupport initialized`
  - `Application.Start callback invoked`
  - `App constructor started`
  - `App.InitializeComponent() completed`
  - `Building AppHost with DI services`
  - `0 UnhandledException entries`

---

## 7. UI QA Capability & Limitations

- **Layer 1 (Static)**: Automated via MSBuild / `dotnet build`.
- **Layer 2 (Functional Tests)**: Fully automated (59 stress assertions in `Axora.Desktop.Tests.exe`).
- **Layer 3 (Runtime Smoke)**: Automated via `smoke-test.ps1` verifying process launch and startup diagnostics.
- **Layers 4 & 5 (Interactive & Visual UI QA)**:
  - Automated screenshot diffing / desktop visual test harness is **not currently configured** on this host.
  - Manual verification protocol defined in `docs/UI_QA_CONTRACT.md` and `docs/UI_QUALITY_CHECKLIST.md`.
  - Visual validation must be conducted through interactive execution or Chrome DevTools MCP (for Tauri webview).

---

## 8. Security & Secrets Audit

- **Automated Regex Scan**: Scanned all source code, manifests, configs, and documentation for API keys, private keys, certificates, passwords, AWS tokens, GitHub tokens, and bearer tokens.
- **Findings**:
  - `0` real credentials, tokens, or private keys discovered.
  - Match in `Axora-Desktop-MaterialUI/src-tauri/src/vault.rs` was verified to be a unit test dummy string (`"SecretMasterPassword2026!"`).
- **Secret Protection**: `.gitignore` strictly blocks all `*.pfx`, `*.p12`, `*.key`, `*.pem`, `*.cer`, `*.crt`, `.env`, `.env.*`, `secrets.json`, and machine credentials.

---

## 9. .gitignore & Git Hygiene Findings

- **Root `.gitignore`**:
  - Cleanly excludes all build outputs (`bin/`, `obj/`, `target/`, `dist/`, `node_modules/`).
  - Cleanly excludes all diagnostic logs and binlogs (`*.binlog`, `*.log`, `build_log.txt`, `diagbuild.log`, `fresh_build.log`).
  - Cleanly excludes Visual Studio user state (`.vs/`, `*.user`, `*.suo`).
  - Cleanly excludes transient QA evidence (`docs/qa/`, `qa-evidence/`, `TestResults/`, `screenshots/`, `*.dump`).
  - Safely ignores legacy subproject agent logs (`Axora-Desktop-WinUI/.agents/`) without deleting them from disk.
- **Preserved Files**: All source code, project files, solutions, assets, scripts, documentation, and root `.agents/` are recognized as tracked/staged candidates.

---

## 10. Summary of Changes Made

1. **`Axora-Desktop-WinUI/Directory.Build.props`**: Added multi-drive detection for `<AppxMSBuildToolsPath>` (`C:\Program Files` and `D:\Program Files`), fixing `dotnet build` MSB4062 error.
2. **`Axora-Desktop-WinUI/Axora.Desktop/App.xaml`**: Resolved Issue W-02 by declaring `<ThemeShadow x:Key="Elevation16Shadow" />` in global application resources, preventing runtime `XamlParseException` on `SettingsPage`.
3. **`Axora-Desktop-MaterialUI/src-tauri/tauri.conf.json`**: Added `minWidth: 960` and `minHeight: 600` window constraints to guarantee responsive minimum layout integrity matching WinUI's `WM_GETMINMAXINFO` subclassing.
4. **`Axora-Desktop-MaterialUI/src/components/Sidebar.tsx`**: Added `Spaced Repetition` (`BookOpen` icon) to `NAV_ITEMS` for 1-click access to Flashcard Studio, establishing direct feature parity with WinUI. Clarified Issue M-01 event bus functionality.
5. **`scripts/qa/smoke-test.ps1`**: Standardized ASCII encoding, fixed PowerShell 5.1 string parsing, added multi-path binary resolution, and added exit code 2 (`BLOCKED`) when target executables are unavailable.
6. **`scripts/qa/run-tests.ps1`**: Dynamic regex parsing of test output, multi-path binary discovery, and explicit exit code 2 (`BLOCKED`) when 0 tests execute due to missing host toolchains.
7. **`scripts/qa/build-all.ps1`**: Added execution tracking, fallback to `dotnet build`, clean ASCII headers, and explicit exit code 2 (`BLOCKED`) when toolchains are missing.
8. **`scripts/qa/audit-workspace.ps1`**: Standardized ASCII encoding, added Node.js and Rust/Cargo environment diagnostics.
9. **`.gitignore`**: Excluded transient QA evidence (`docs/qa/`, `TestResults/`, etc.) and legacy subproject agent logs (`Axora-Desktop-WinUI/.agents/`).
10. **`docs/UI_QA_CONTRACT.md` [NEW]**: Established authoritative 6-layer UI QA contract and universal 15-point PASS criteria.
11. **`docs/UI_TEST_STRATEGY.md` [NEW]**: Formulated practical UI testing boundaries, decision standards, and the 9-stage Vibe Coding Quality Gate.
12. **`.agents/workflows/axora-ui-qa.md`**: Encoded the 9-stage Vibe Coding Quality Gate directly into the Antigravity workflow.

---

## 11. Remaining Limitations & Next Steps

1. **MaterialUI Host Toolchain**: To compile and run `Axora-Desktop-MaterialUI` from scratch on this machine, Node.js (v18+) and the Rust toolchain (`rustup` / `cargo`) must be installed.
2. **Desktop Visual UI Automation**: Automated UI tree inspection (e.g. via FlaUI / Windows App Driver) can be added in a future milestone to automate Layer 4 and Layer 5 testing.
3. **First Commit**: Proceed with clean staged-file review and first git commit.
