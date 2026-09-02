# Axora Desktop — Catalog of Known Issues & Limitations

This document tracks known defects, environment limitations, and architectural backlog across the AXORA Desktop workspace, classified by severity:
- **P0 — Blocking**: Prevents build, crash on startup.
- **P1 — Critical**: Critical workflow broken with no workaround.
- **P2 — Important**: Functional defect or missing resource with known workaround.
- **P3 — Minor / Environment Limitation**: Host environment constraint or non-blocking defect.
- **P4 — Enhancement**: Future automation or UX improvement.

---

## 1. Resolved Issues

### [RESOLVED] Issue W-01: Hardcoded MSBuild Path in `Directory.Build.props` (P2)
- **Location**: `Axora-Desktop-WinUI\Directory.Build.props`
- **Symptom**: CLI `dotnet build` failed with `MSB4062: The "Microsoft.Build.Packaging.Pri.Tasks.ExpandPriContent" task could not be loaded`.
- **Root Cause**: Hardcoded path to `D:\Program Files` ignored standard `C:\Program Files` Visual Studio installations.
- **Resolution**: Updated `Directory.Build.props` to check candidate paths on both `C:\` and `D:\` drives. CLI `dotnet build` now succeeds with 0 errors.

### [RESOLVED] Issue W-02: Missing ThemeResource `Elevation16Shadow` (P2)
- **Location**: `Axora-Desktop-WinUI\Axora.Desktop\Views\SettingsPage.xaml` (Line 263) & `App.xaml`
- **Symptom**: Potential runtime `XamlParseException: Cannot find a Resource with the Name/Key Elevation16Shadow` when navigating to `SettingsPage`.
- **Root Cause**: Floating save/revert pill referenced `Shadow="{ThemeResource Elevation16Shadow}"` which was missing from standard Windows App SDK dictionaries.
- **Resolution**: Declared `<ThemeShadow x:Key="Elevation16Shadow" />` in `App.xaml` global resources. Verified clean compilation and runtime smoke test.

### [CLARIFIED / RESOLVED] Issue M-01: "Analytics" Quick Action Event Handling (P2)
- **Location**: `Axora-Desktop-MaterialUI\src\pages\Dashboard.tsx` & `src\components\Sidebar.tsx`
- **Symptom**: Previously reported as unrouted due to `page: "CompatibilityModal"`.
- **Audit Finding**: In reality, `Dashboard.tsx` (lines 128–134) intercepts `action.page === "CompatibilityModal"` and dispatches `new CustomEvent("open-compatibility-modal")`, which `Sidebar.tsx` (lines 71–75) listens to and opens `SystemInfoModal`. Verified functional as intended.

---

## 2. Active Application Defects
*(None currently blocking. All critical and P2 defects resolved).*

---

## 3. Host Environment Limitations

### Limitation E-01: Missing Node.js and Rust Toolchains on Host (P3)
- **Symptom**: Cannot recompile `Axora-Desktop-MaterialUI` frontend (`npm run build`) or Rust backend (`cargo check`) directly in this machine shell.
- **Impact**: Pre-built web assets in `dist/` and intact source files are preserved, but incremental builds require installing Node.js (v18+) and Rustup.

---

## 4. Automation & Testing Enhancements

### Enhancement A-01: Automated Visual UI Testing Framework (P4)
- **Scope**: Layers 4 & 5 (Interactive & Visual UI Validation).
- **Current State**: Automated logic stress testing is 100% verified (59/59 assertions). Visual validation currently relies on manual checklist evaluation (`docs/UI_QA_CONTRACT.md`).
- **Future Plan**: Integrate FlaUI / Windows UI Automation test harness for automated end-to-end visual assertions.
