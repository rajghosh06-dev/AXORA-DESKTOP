---
name: ui-bug-diagnosis
description: Systematic root-cause debugging methodology for UI defects, dead buttons, navigation errors, and visual misalignment in Axora Desktop. Use when investigating reported UI bugs or diagnosing exceptions.
---

# Axora UI Bug Diagnosis Skill

This skill outlines the strict 9-step diagnostic sequence for investigating and fixing UI defects without introducing broad regressions or unnecessary rewrites.

## 9-Step Diagnostic Cycle

```
[OBSERVE] ────────► [REPRODUCE] ────────► [LOCALIZE]
       │                    │                    │
       ▼                    ▼                    ▼
[ROOT CAUSE] ─────► [MINIMAL FIX] ─────► [BUILD]
       │                    │                    │
       ▼                    ▼                    ▼
[RETEST] ─────────► [REGRESSION] ───────► [REPORT]
```

### Detailed Steps

1. **OBSERVE**: Inspect the defect description, error message, or log snippet.
2. **REPRODUCE**: Identify the exact user journey that triggers the bug:
   - *Example*: Click Dashboard -> Click Resume Studio -> Editor opens.
3. **LOCALIZE**: Trace the UI element to its source definition:
   - *WinUI*: Trace XAML `Click="Handler"` or `Command="{Binding CommandName}"` in `Views/` to `ViewModels/`.
   - *MaterialUI*: Trace `onClick` handler and `setCurrentPage(...)` in `src/pages/` to `src/App.tsx`.
4. **IDENTIFY ROOT CAUSE**: Look for the most common vibe-coding pitfalls:
   - Unhandled switch cases in route tables (e.g. dead buttons).
   - Missing `{ThemeResource ...}` keys causing `XamlParseException`.
   - Missing `[RelayCommand]` or asynchronous task deadlocks (`.Result` on UI thread).
   - Missing `TwoWay` binding on editable input controls.
5. **MINIMAL FIX**: Implement the smallest possible fix that addresses the root cause directly. Avoid restructuring unrelated code.
6. **BUILD**: Recompile the affected project and ensure zero build errors.
7. **RETEST**: Re-execute the reproduction steps to verify the bug is eliminated.
8. **REGRESSION CHECK**: Run the automated test suite to ensure no other features were broken.
9. **REPORT**: Document what was observed, what was changed, and how it was verified.
