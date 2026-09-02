---
description: Systematic bug diagnosis and minimal fix workflow for reported UI, runtime, or build defects.
---

# Workflow: /axora-fix

## Objective
Investigate and resolve reported bugs through the strict 9-step diagnostic sequence without causing regressions.

## Execution Steps

1. **Observe & Reproduce**:
   - Understand the reported failure symptom.
   - Run the application or test suite to trigger the exact issue.

2. **Localize & Root Cause Analysis**:
   - Check error logs, stack traces, and relevant XAML/TSX files.
   - Determine whether the bug is in View (binding/resource), ViewModel (command/state), or Service (business logic).

3. **Apply Minimal Fix**:
   - Modify only the specific lines responsible for the defect.
   - Preserve existing architecture and naming conventions.

4. **Rebuild & Retest**:
   - Run `.\scripts\qa\build-all.ps1`.
   - Run `.\scripts\qa\run-tests.ps1`.

5. **Execute Regression Verification**:
   - Run `.\scripts\qa\smoke-test.ps1`.
   - Verify adjacent features and navigation continue working seamlessly.

6. **Report**:
   - Summarize the bug cause, file diff, build verification, and regression test outcome.
