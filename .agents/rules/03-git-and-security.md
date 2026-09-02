# Rule 03: Git Safety & Secret Prevention

## Repository Safety Standards
- **Private Repository Target**: The workspace connects to private repository `https://github.com/rajghosh06-dev/AXORA-DESKTOP.git`.
- **Destructive Commands Prohibited**:
  - NEVER execute `git reset --hard` without explicit human authorization.
  - NEVER execute `git push --force` or force push overrides.
  - NEVER delete or recreate the `.git` directory.
- **Strict Secret Protection**:
  - Never commit private keys (`*.key`, `*.pem`, `*.pfx`, `*.p12`), certificate files (`*.cer`, `*.crt`), `.env` files, API tokens, passwords, or personal credentials.
  - Never print unmasked sensitive tokens or passwords into logs or markdown artifacts.
- **Clean Diff Discipline**:
  - Always run `git status` and `git diff` before preparing commits.
  - Never stage build outputs (`bin/`, `obj/`, `target/`, `dist/`, `node_modules/`, `*.binlog`, `*.log`).
  - Do NOT automatically push commits to remote unless explicitly requested by the user.
