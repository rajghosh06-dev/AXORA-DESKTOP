---
description: Audit Git tracking, ignored artifacts, upstream remote, and repository hygiene.
---

# Workflow: /axora-git

## Objective
Audit the Git repository state to guarantee zero tracked build outputs, zero tracked logs, clean remote tracking, and verified exclusion rules in `.gitignore`.

## Execution Steps

1. **Verify Remote & Upstream**:
   ```powershell
   git remote -v
   git branch -vv
   ```
   - Assert origin is `https://github.com/rajghosh06-dev/AXORA-DESKTOP.git`.
   - Assert repository remains PRIVATE.

2. **Audit Tracked Files**:
   ```powershell
   # Verify no executable binaries are tracked
   git ls-files | Select-String "\.(exe|dll|pdb|binlog|so|dylib|lib|a)$"

   # Verify no build outputs or logs are tracked
   git ls-files | Select-String "(^|/)(bin|obj|target|node_modules|dist|\.vs)/"
   ```
   - Assert 0 matches for both queries.

3. **Audit Ignored Files**:
   ```powershell
   git status --ignored
   ```
   - Verify `bin/`, `obj/`, `target/`, `dist/`, `node_modules/`, `*.log`, `*.binlog`, and `Axora-Desktop-WinUI/.agents/` are ignored.
   - Verify root `.agents/` is actively tracked.

4. **Verify Working Tree Cleanliness**:
   ```powershell
   git status
   ```
