---
description: Perform runtime UI inspection, layout validation, and interactive control testing on desktop screens.
---

# Workflow: /axora-ui-qa — Vibe Coding Quality Gate

## Objective
Prevent UI regressions, dead buttons, broken navigation, and layout defects during AI-assisted development by strictly executing the 9-stage **Vibe Coding Quality Gate** before declaring any task complete.

---

## The 9-Stage Quality Gate

```
[1. IMPLEMENT] ──► [2. STATIC CHECK] ──► [3. COMPILE] ──► [4. LOGIC TEST]
                                                                │
┌───────────────────────────────────────────────────────────────┘
▼
[5. RUNTIME SMOKE] ──► [6. INTERACTION] ──► [7. VISUAL AUDIT]
                                                   │
┌──────────────────────────────────────────────────┘
▼
[8. REGRESSION] ────► [9. EVIDENCE REPORT] ──► [TASK DECLARED COMPLETE]
```

### Execution Steps

1. **Stage 1: IMPLEMENT**
   - Author minimal, targeted modifications. Never perform speculative wide refactors.

2. **Stage 2: STATIC CHECK**
   - WinUI: Inspect XAML elements, ensure theme brushes (`{ThemeResource ...}`) exist.
   - MaterialUI: Verify MD3 tokens (`var(--md-sys-color-*)`) and TypeScript imports.

3. **Stage 3: COMPILE**
   - Run `.\scripts\qa\build-all.ps1 -Target <WinUI|MaterialUI|All>`.
   - Exit code must be `0` with `0` compiler errors.

4. **Stage 4: LOGIC TEST**
   - Run `.\scripts\qa\run-tests.ps1 -Target <WinUI|MaterialUI|All>`.
   - Assert all stress and unit test assertions pass. Exit code must be `0`.

5. **Stage 5: RUNTIME SMOKE**
   - Run `.\scripts\qa\smoke-test.ps1 -Target <WinUI|MaterialUI>`.
   - Assert process launches, stays alive, and `startup.log` records clean initialization without exceptions.

6. **Stage 6: INTERACTIVE & NAVIGATION QA**
   - Verify every modified button, menu item, or route reaches its command/event handler.
   - Verify keyboard accelerators (`Ctrl+K`, `Escape`, `Enter`) and tab order.

7. **Stage 7: VISUAL LAYOUT & HIERARCHY AUDIT**
   - Validate against the 25-Point Checklist (`docs/UI_QUALITY_CHECKLIST.md`).
   - Test window resizing at minimum dimensions (1000x620 DIP for WinUI, 960x600 px for MaterialUI).
   - Verify no text clipping, zero overlapping elements, and correct theme contrast.

8. **Stage 8: REGRESSION SUITE**
   - Re-run `.\scripts\qa\run-tests.ps1` to ensure adjacent components are unharmed.

9. **Stage 9: EVIDENCE REPORT**
   - Output real executed test counts, runtime log snippets, and visual state confirmation.

---

## Critical Invariant
> **NEVER** declare a UI task complete based solely on compilation or code review. The agent must traverse all applicable stages and provide empirical evidence for every claim.
