---
description: Execute release readiness checklist, build verification, and package staging for AXORA Desktop.
---

# Workflow: /axora-release

## Objective
Validate that the repository is in a release-ready state: clean compilation across all targets, 100% test pass rate, verified security posture, updated documentation, and clean Git state.

## Execution Steps

1. **Pre-Release Clean**:
   ```powershell
   .\scripts\qa\pre-build-clean.ps1
   ```

2. **Full Compilation**:
   ```powershell
   .\scripts\qa\build-all.ps1 -Configuration Release -Target All
   ```

3. **Adversarial Test Suite**:
   ```powershell
   .\scripts\qa\run-tests.ps1 -Target All
   ```

4. **Security & Secrets Scan**:
   ```powershell
   .\scripts\qa\security-scan.ps1
   ```

5. **Runtime Smoke Validation**:
   ```powershell
   .\scripts\qa\smoke-test.ps1 -Target WinUI
   ```

6. **Documentation & Changelog Audit**:
   - Verify `docs/KNOWN_ISSUES.md` lists zero blocking P0/P1 defects.
   - Verify `docs/UI_QA_CONTRACT.md` standards are satisfied.
   - Verify package versions match in `Package.appxmanifest` and `tauri.conf.json`.

7. **Git Release Tagging**:
   - Ensure working tree is clean.
   - Tag release: `git tag -a v<major>.<minor>.<patch> -m "Release v<version>"`.
