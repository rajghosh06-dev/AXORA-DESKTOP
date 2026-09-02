---
description: Execute comprehensive security and secret scanning across working tree, git history, and configs.
---

# Workflow: /axora-security

## Objective
Detect and eliminate embedded API keys, private keys, certificates, cloud tokens, hardcoded passwords, and credential-like test fixtures across the working tree, commit history, and configuration files.

## Execution Steps

1. **Run Security Scanner**:
   ```powershell
   .\scripts\qa\security-scan.ps1
   ```

2. **Inspect Classification Outputs**:
   - **`CURRENT TREE RESULT`**: Scans all tracked files and staged content against 9 high-risk credential patterns (Google, GitHub, OpenAI, AWS, Slack, Private Keys, Password assignments). Must be `PASS (0 secrets)`.
   - **`GIT HISTORY RESULT`**: Audits all commits in `git log` to ensure no secret has entered version control. Must be `PASS`.
   - **`TEST FIXTURE RESULT`**: Verifies that any mock values in test files use unambiguous non-secret dummy tokens (e.g. `axora-non-secret-test-dummy`).
   - **`CONFIGURATION RESULT`**: Ensures all manifests (`Package.appxmanifest`, `tauri.conf.json`, `launchSettings.json`) are free of embedded secrets.

3. **Invariants**:
   - Never silently whitelist suspicious values.
   - Replace any credential-like test string with an unmistakably non-secret dummy value.
   - Never commit `.env` or `secrets.json`.
