---
description: Perform a complete repository, architecture, and toolchain health audit across both Axora Desktop applications.
---

# Workflow: /axora-audit

## Objective
Run a comprehensive, non-destructive audit of the workspace to inspect Git status, identify stack versions, verify build toolchains, and catalog test coverage gaps.

## Execution Steps

1. **Inspect Workspace Git State**:
   - Run `git status` to verify branch, untracked files, and modified state.
   - Run `git remote -v` to check remote configuration.

2. **Run Workspace Diagnostic Audit Script**:
   ```powershell
   .\scripts\qa\audit-workspace.ps1
   ```

3. **Check Build Toolchains**:
   - Verify .NET SDK: `dotnet --version`
   - Verify Visual Studio MSBuild: Test path `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`
   - Check Node.js and Rust toolchains if available.

4. **Catalog Existing Test Coverage**:
   - WinUI: Run `.\scripts\qa\run-tests.ps1`
   - MaterialUI: Check for existing tests in `src-tauri`.

5. **Generate Audit Summary**:
   - Report status of both applications.
   - List pre-existing defects or missing resources.
   - Do NOT modify any application source files during the audit.
