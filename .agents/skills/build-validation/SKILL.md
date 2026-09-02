---
name: build-validation
description: Comprehensive build validation, packaging inspection, and compiler log diagnostic protocol for Axora Desktop. Use when performing clean builds, verifying toolchain paths, or debugging compilation errors.
---

# Axora Build Validation Skill

This skill teaches the agent how to validate clean builds, detect toolchain anomalies, and interpret MSBuild and Cargo compiler output.

## Build Verification Matrix

### 1. WinUI Build Validation
- **Target Project**: `Axora-Desktop-WinUI\Axora.Desktop\Axora.Desktop.csproj`
- **Compiler Path**: `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`
- **Build Execution**:
  ```powershell
  & "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" Axora.Desktop\Axora.Desktop.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -m
  ```
- **Validation Criteria**:
  - Exit code must equal `0`.
  - Output binary `Axora.Desktop.exe` and `Axora.Desktop.dll` exist in `bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\`.
  - Output contains 0 Errors.

### 2. WinUI Test Suite Build & Execution
- **Target Project**: `Axora-Desktop-WinUI\Axora.Desktop.Tests\Axora.Desktop.Tests.csproj`
- **Run Execution**:
  ```powershell
  .\Axora-Desktop-WinUI\Axora.Desktop.Tests\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.Tests.exe
  ```
- **Validation Criteria**:
  - Exit code must equal `0`.
  - Summary reports `Total: 59 | Passed: 59 | Failed: 0`.

### 3. MaterialUI Build Validation
- **Frontend Target**: `Axora-Desktop-MaterialUI\package.json`
- **Frontend Execution**: `npm run build` (runs `tsc && vite build`)
- **Backend Target**: `Axora-Desktop-MaterialUI\src-tauri\Cargo.toml`
- **Backend Execution**: `cargo test --manifest-path src-tauri/Cargo.toml -- --nocapture`
- **Validation Criteria**:
  - `dist/index.html` and `dist/assets/` generated without TypeScript errors.
  - Rust backend compiles and passes 8 unit tests.

### 4. Central Build Script
Run the automated unified build pipeline:
```powershell
.\scripts\qa\build-all.ps1
```
