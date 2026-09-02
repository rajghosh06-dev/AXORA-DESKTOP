---
description: Comprehensive pre-completion verification pipeline covering source correctness, build, tests, runtime logs, and Git diff.
---

# Workflow: /axora-verify

## Objective
Provide exhaustive verification across all 5 verification tiers before declaring a development task complete.

## Execution Steps

1. **Static Analysis & Linting**:
   - Verify all modified files have valid syntax and zero compiler warnings.

2. **Clean Compilation**:
   ```powershell
   .\scripts\qa\build-all.ps1
   ```
   - Must return `0` errors.

3. **Automated Test Suite**:
   ```powershell
   .\scripts\qa\run-tests.ps1
   ```
   - Must return `0` failures across all 59 assertions.

4. **Runtime Launch & Log Verification**:
   ```powershell
   .\scripts\qa\smoke-test.ps1
   ```
   - Must verify process launches, stays alive, and records clean startup logs without unhandled exceptions.

5. **Git Diff & Cleanliness Audit**:
   - Run `git status` and `git diff`.
   - Ensure no unintended files, secrets, or temporary logs are modified or staged.
