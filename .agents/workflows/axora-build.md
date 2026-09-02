---
description: Build the specified Axora Desktop application (WinUI, MaterialUI, or Both) and verify compiler output with zero errors.
---

# Workflow: /axora-build

## Objective
Execute a clean, deterministic build of the target Axora application and diagnose any compilation errors.

## Execution Steps

1. **Terminate Running Instances**:
   - Run `.\scripts\qa\pre-build-clean.ps1` to stop running instances of `Axora.Desktop` or `axora-desktop` and release file locks.

2. **Execute Targeted Build**:
   ```powershell
   # To build WinUI:
   .\scripts\qa\build-all.ps1 -Target WinUI

   # To build MaterialUI:
   .\scripts\qa\build-all.ps1 -Target MaterialUI

   # To build Both:
   .\scripts\qa\build-all.ps1 -Target All
   ```

3. **Verify Build Outputs**:
   - WinUI: Ensure `Axora-Desktop-WinUI\Axora.Desktop\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.exe` is updated.
   - MaterialUI: Ensure `Axora-Desktop-MaterialUI\dist\index.html` exists.

4. **Report Status**:
   - `BUILD VERIFIED`: All target projects compiled with 0 errors.
   - `FAIL`: Build errors encountered (extract and display error codes).
