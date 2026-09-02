# Axora Desktop — Local-First Workstation Productivity Suite

Welcome to the **AXORA-DESKTOP** workspace. Axora Desktop is a privacy-focused, zero-trust local desktop productivity suite providing vector PDF compilation, offline OCR, spaced repetition study systems, hardware scanning, and local mobile synchronization.

---

## 1. Repository Structure

```
AXORA-DESKTOP/
├── Axora-Desktop-MaterialUI/     # Tauri v2 + React 18 + Tailwind MD3 + Rust Core
├── Axora-Desktop-WinUI/          # .NET 9 + Windows App SDK 1.6 / WinUI 3 + XAML + C# 13
├── .agents/                      # Antigravity Rules, Skills, Workflows & Subagents
├── scripts/qa/                   # Automated QA, Build, Test & Smoke Test Scripts
└── docs/                         # Architecture, Test Matrix & UI Quality Checklist
```

### Dual Implementation Model
- **Axora-Desktop-MaterialUI**: A cross-platform hybrid desktop implementation using **Tauri v2 + React 18 + Material Design 3**.
- **Axora-Desktop-WinUI**: A native Windows 11 desktop implementation using **.NET 9 + WinUI 3 + XAML + Mica Alt**.

---

## 2. Quick Start & QA Commands

Run the central QA automation scripts from PowerShell:

```powershell
# 1. Audit Workspace Health & Toolchains
.\scripts\qa\audit-workspace.ps1

# 2. Build WinUI Application Cleanly
.\scripts\qa\build-all.ps1 -Target WinUI

# 3. Run Automated Adversarial Stress Tests (59 Assertions)
.\scripts\qa\run-tests.ps1

# 4. Perform Runtime Smoke Launch & Verify Logs
.\scripts\qa\smoke-test.ps1 -Target WinUI

# 5. Clean Process Locks
.\scripts\qa\pre-build-clean.ps1
```

---

## 3. Antigravity Agent Workflows

When pair-programming with Antigravity, the following slash commands are available:
- `/axora-audit`: Fast repository health and toolchain audit.
- `/axora-build`: Targeted clean compilation of WinUI, MaterialUI, or Both.
- `/axora-test`: Automated test suite runner with structured pass/fail reporting.
- `/axora-ui-qa`: Runtime UI inspection and layout validation.
- `/axora-fix`: Systematic 9-step bug reproduction, root-cause diagnosis, and minimal fix cycle.
- `/axora-verify`: Complete pre-completion verification gate.
- `/axora-pr`: Clean Git diff audit and pull request preparation.

---

## 4. Documentation Links
- [System Architecture](docs/ARCHITECTURE.md)
- [Comprehensive Test Matrix](docs/TEST_MATRIX.md)
- [UI Quality Assurance Contract](docs/UI_QA_CONTRACT.md)
- [UI Testing Strategy & Quality Gate](docs/UI_TEST_STRATEGY.md)
- [25-Point UI Quality Checklist](docs/UI_QUALITY_CHECKLIST.md)
- [Antigravity Customization Guide](docs/ANTIGRAVITY_GUIDE.md)
- [Repository & Infrastructure Audit Report](docs/AUDIT_REPORT.md)
- [Catalog of Known Issues](docs/KNOWN_ISSUES.md)
