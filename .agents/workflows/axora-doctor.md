---
description: Run comprehensive system, toolchain, git repository, and Antigravity workspace health diagnostics.
---

# Workflow: /axora-doctor

## Objective
Quickly audit the entire development workstation environment: verify OS build, Git tracking, .NET SDK, Visual Studio MSBuild, Node.js/npm, Rust/cargo, Antigravity workspace integrity, and active process locks.

## Execution Steps

1. **Run Doctor Script**:
   ```powershell
   .\scripts\qa\doctor.ps1
   ```

2. **Diagnose Output Categories**:
   - **OS & Architecture**: Verifies Windows 11 64-bit build.
   - **Git Repository**: Verifies binary, branch `main`, and private remote URL.
   - **.NET Toolchain**: Verifies modern .NET SDK (9.0/10.0) for WinUI.
   - **Visual Studio MSBuild**: Verifies presence of MSBuild.exe for Pass 2 XAML compilation.
   - **Web & Rust Toolchains**: Checks Node.js/npm and Cargo/Rust availability for MaterialUI. Reports `[BLOCKED]` if not found.
   - **Antigravity Customizations**: Verifies all 6 rules, 6 skills, workflows, subagents, and config files in `.agents/`.
   - **Process Locks**: Checks for stale running instances of `Axora.Desktop.exe`.

3. **Interpreting Results**:
   - **Exit 0 (`PASS`)**: Full toolchain available across both WinUI and MaterialUI.
   - **Exit 2 (`PASS WITH LIMITATIONS`)**: WinUI ready; MaterialUI toolchains missing on host.
   - **Exit 1 (`FAIL`)**: Critical toolchain missing (e.g. .NET SDK or Git missing).
