---
name: axora-ui-qa
description: Systematic desktop UI quality assurance, layout inspection, and control validation for Axora Desktop (MaterialUI and WinUI). Use when validating UI appearance, checking button clicks, testing navigation, or verifying visual states.
---

# Axora Desktop UI Quality Assurance Skill

This skill provides a systematic protocol for inspecting and validating desktop user interfaces across both `Axora-Desktop-MaterialUI` and `Axora-Desktop-WinUI`.

## UI Validation Protocol

### Step 1: Target Identification
- Identify the application under test (`MaterialUI` or `WinUI`).
- Identify the target screen/view (e.g., Dashboard, Scholar Kit, Resume Studio, Flashcard Studio, Vault).
- Identify the specific interactive controls (Buttons, TextBoxes, Dropdowns, Toggles, Sliders).

### Step 2: Build & Launch
- Ensure the application is compiled with zero errors:
  - WinUI: `.\scripts\qa\build-all.ps1 -Target WinUI`
  - MaterialUI: `.\scripts\qa\build-all.ps1 -Target MaterialUI`
- Launch the application for runtime observation:
  - WinUI: `.\scripts\qa\smoke-test.ps1 -Target WinUI`

### Step 3: 25-Point Visual & Interaction Audit
Execute the following verification checklist against the view:
1. **Layout & Alignment**: All cards and text columns follow grid alignment.
2. **Padding & Spacing**: Margins are consistent (12px/16px/24px).
3. **Clipping & Truncation**: No text or icons clipped at minimum window sizes.
4. **Scrolling**: ScrollViewers appear when content overflows and scroll smoothly.
5. **Button Response**: Every button provides visual press feedback and dispatches an event/command.
6. **No Dead Buttons**: Ensure no buttons are wired to missing routes or empty handlers.
7. **Loading States**: Async actions display `ProgressRing` / `Loader2` during execution.
8. **Disabled States**: Action buttons are disabled when required input is missing.
9. **Error Feedback**: Invalid input displays inline error text or toast alerts.
10. **Toast Notifications**: Completed operations trigger clear success toasts.
11. **Keyboard Shortcuts**: Global accelerators (e.g. `Ctrl+K` for Command Palette, `Alt+Shift+V` for Snippet Vault) trigger reliably.
12. **Theme Contrast**: Text and icons maintain legible contrast across both Dark and Light modes.

### Step 4: Evidence Recording
- Categorize findings:
  - `PASS`: Control behavior observed and verified.
  - `FAIL`: Defect observed (record exact visual or functional fault).
  - `NOT VERIFIED`: Feature was not exercised at runtime.
