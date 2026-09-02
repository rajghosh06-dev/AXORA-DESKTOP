---
description: Execute runtime process smoke launch and verify startup diagnostics for AXORA Desktop.
---

# Workflow: /axora-smoke

## Objective
Verify that the compiled desktop application launches as an active OS process, stays alive, completes all initialization phases in `startup.log` without unhandled exceptions, and terminates cleanly.

## Execution Steps

1. **Clean Process Locks**:
   - Run `.\scripts\qa\pre-build-clean.ps1` to ensure no stale instances are running.

2. **Run Smoke Test**:
   ```powershell
   # For WinUI (default):
   .\scripts\qa\smoke-test.ps1 -Target WinUI

   # For MaterialUI (requires host toolchain):
   .\scripts\qa\smoke-test.ps1 -Target MaterialUI
   ```

3. **Verify Exit Codes & Diagnostics**:
   - **Exit 0 (`PASS`)**: Process launched successfully, PID stayed active, `startup.log` recorded complete initialization, terminated gracefully.
   - **Exit 1 (`FAIL`)**: Process crashed on startup or unhandled exception found in diagnostic log.
   - **Exit 2 (`BLOCKED`)**: Target binary not found or toolchain not available on host.

4. **Verify Startup Diagnostic Phases (WinUI)**:
   - `Program.Main started`
   - `ComWrappersSupport initialized`
   - `Application.Start callback invoked`
   - `App constructor started`
   - `App.InitializeComponent completed`
   - `AppHost built (19 services + 10 viewmodels)`
   - `MainWindow instantiated & activated`
   - `System Tray service initialized`
   - `P2P background sync service auto-started`
