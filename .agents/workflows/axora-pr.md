---
description: Prepare clean, verified changes for Git/GitHub pull requests without automatically pushing.
---

# Workflow: /axora-pr

## Objective
Inspect the current Git branch, verify that all builds and tests pass, review the full diff for stray files or secrets, and generate a professional pull request summary.

## Execution Steps

1. **Verify Git State**:
   - Run `git status` to verify modified and untracked files.
   - Assert that no `.env`, `*.log`, `bin/`, `obj/`, `target/`, or `node_modules/` files are tracked.

2. **Run Full Verification Gate**:
   - Build: `.\scripts\qa\build-all.ps1` (Exit code 0)
   - Tests: `.\scripts\qa\run-tests.ps1` (Exit code 0)

3. **Inspect Diff**:
   - Run `git diff` and verify every changed line is intentional and relevant.

4. **Prepare Commit Message & PR Summary**:
   - Write a structured Conventional Commit message (e.g. `feat(winui): ...`, `fix(materialui): ...`, `test(qa): ...`).
   - Present the summary to the user for review.
   - **DO NOT** execute `git push` unless explicitly commanded by the user.
