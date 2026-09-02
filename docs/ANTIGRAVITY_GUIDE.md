# Axora Desktop — Antigravity Agent Customization Guide

This guide explains how to use the Antigravity development, QA, and customization infrastructure configured for `AXORA-DESKTOP`.

---

## 1. Customization Architecture

```
AXORA-DESKTOP/
├── .agents/
│   ├── rules/                           # Persistent engineering rules
│   │   ├── 01-axora-architecture.md     # Dual-implementation separation rules
│   │   ├── 02-engineering-discipline.md # 5-tier verification hierarchy
│   │   ├── 03-git-and-security.md       # Git safety & secret prevention
│   │   ├── 04-materialui-standards.md   # Tauri v2 + React 18 standards
│   │   ├── 05-winui-standards.md        # WinUI 3 XAML invariants & MVVM
│   │   └── 06-ui-ux-quality.md          # Visual quality guidelines
│   ├── skills/                          # Reusable agent capabilities
│   │   ├── axora-ui-qa/                 # UI validation & inspection protocol
│   │   ├── axora-materialui/            # MaterialUI development & IPC guide
│   │   ├── axora-winui/                 # WinUI development & XAML guide
│   │   ├── regression-testing/          # Regression suite execution
│   │   ├── ui-bug-diagnosis/            # 9-step diagnostic sequence
│   │   └── build-validation/            # MSBuild & Cargo build validator
│   ├── workflows/                       # Slash command workflow definitions
│   │   ├── axora-audit.md               # /axora-audit: Health audit
│   │   ├── axora-build.md               # /axora-build: Clean compilation
│   │   ├── axora-test.md                # /axora-test: Automated test runner
│   │   ├── axora-ui-qa.md               # /axora-ui-qa: Runtime UI inspection
│   │   ├── axora-fix.md                 # /axora-fix: Bug reproduction & fix
│   │   ├── axora-verify.md              # /axora-verify: Full verification
│   │   └── axora-pr.md                  # /axora-pr: Pull request preparation
│   ├── agents/                          # Specialized subagent definitions
│   │   ├── ui-reviewer.md               # UI/UX consistency reviewer
│   │   ├── qa-engineer.md               # QA & automated test engineer
│   │   ├── build-engineer.md            # Toolchain & build specialist
│   │   ├── code-reviewer.md             # Security & Git diff reviewer
│   │   ├── materialui-specialist.md     # React 18 / Tauri v2 specialist
│   │   └── winui-specialist.md          # .NET 9 / WinUI 3 specialist
│   ├── hooks.json                       # Pre-build process lock terminator
│   └── mcp_config.json                  # Chrome DevTools MCP configuration
```

---

## 2. Using Workflows (Slash Commands)

Execute workflows to automate key development cycles:
- `/axora-audit`: Fast diagnostic health check of the workspace and toolchains.
- `/axora-build`: Compiles the target application cleanly with zero errors.
- `/axora-test`: Runs the automated test runner and reports pass/fail counts.
- `/axora-ui-qa`: Launches runtime smoke tests and validates UI layout.
- `/axora-fix`: Guides minimal-change bugfixes with regression testing.
- `/axora-verify`: Complete verification gate across source, build, tests, and diff.
- `/axora-pr`: Formats conventional commit messages and verifies diff cleanliness.

---

## 3. Automation Scripts Layer (`scripts/qa/`)

You can run the QA scripts directly from PowerShell:
```powershell
# Build WinUI application:
.\scripts\qa\build-all.ps1 -Target WinUI

# Run all automated tests:
.\scripts\qa\run-tests.ps1

# Perform runtime smoke launch and log validation:
.\scripts\qa\smoke-test.ps1 -Target WinUI

# Fast workspace health audit:
.\scripts\qa\audit-workspace.ps1

# Terminate running process locks:
.\scripts\qa\pre-build-clean.ps1
```
