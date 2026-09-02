---
name: regression-testing
description: Comprehensive regression testing protocol, baseline verification, and test matrix execution for Axora Desktop. Use after modifying core services, shared models, navigation routing, or data converters.
---

# Axora Regression Testing Skill

This skill guides the agent in preventing accidental regressions and verifying that changes to one feature do not damage neighboring functionality.

## Regression Verification Pipeline

```
1. ESTABLISH BASELINE
   └── Check existing test suite status (59 WinUI assertions / 8 Rust tests).

2. TEST CHANGED COMPONENT
   └── Execute unit tests targeting the modified service or view model.

3. TEST ADJACENT FLOWS
   ├── WinUI: Run Resume PDF compiler tests + Flashcard deck tests + Image queue tests.
   └── MaterialUI: Run Rust RAG tests + Vault roundtrip encryption tests.

4. TEST SHELL & NAVIGATION
   ├── Verify Command Palette opens via Ctrl+K.
   ├── Verify Navigation rail switches between all 10 views without freezing or duplicate frame pushes.
   └── Verify startup.log contains 0 UnhandledExceptions.

5. VERIFY RECOVERY & ERROR HANDLING
   ├── Test with 0-byte corrupted input files.
   ├── Test cancelled file pickers.
   └── Test invalid password entries.

6. REPORT OUTCOME
   └── Distinguish NEW FAILURES from PRE-EXISTING FAILURES.
```

## Central Test Runner Script
Run the workspace-wide automated regression suite:
```powershell
.\scripts\qa\run-tests.ps1
```
This runs all available automated test projects and outputs a structured pass/fail report with exit code 0 on success.
