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

### [RESOLVED] Issue M-02: `ToastNotification` Component Unmounted in `App.tsx` (P2)
- **Location**: `Axora-Desktop-MaterialUI\src\App.tsx`
- **Symptom**: Toast notifications dispatched via `toast.warning()`, `toast.error()`, or `toast.success()` updated the Zustand store but did not render visual notifications in the DOM.
- **Root Cause**: `App.tsx` imported `ToastNotification` but did not mount `<ToastNotification />` in the JSX tree.
- **Resolution**: Mounted `<ToastNotification />` inside `App.tsx` directly above the closing tag of the main layout. Verified with automated negative validation toast assertions in `test-materialui-product-flows.mjs`.

---

## 2. Active Application Defects
*(None currently blocking. All critical and P2 defects resolved).*

---

### [RESOLVED] Limitation E-01: MaterialUI Windows Toolchain Fully Installed & Verified (P3)
- **Location**: `Axora-Desktop-MaterialUI`
- **Previous Status**: Node.js, Cargo, and Windows SDK were missing on host.
- **Resolution**: Host environment equipped with Node.js v26.8.1, npm 11.19.0, Rust/Cargo 1.98.1 (stable-x86_64-pc-windows-msvc), MSVC v143/v144 (14.51.36231), and Windows 11 SDK 10.0.26100.0. MaterialUI frontend (`npm run build`), Rust backend (`cargo check`), automated tests (`cargo test`: 15/15 passed), release packaging (`npx tauri build`: `axora-desktop.exe`), and runtime smoke launch verified operational.

---

## 3. Host Environment Limitations
*(Zero blocking host limitations remain. Both WinUI and MaterialUI toolchains are fully operational).*

## 4. Automation & Testing Enhancements

### [RESOLVED] Enhancement A-01: Automated Desktop UI Testing Framework (P4)
- **Scope**: Layers 4 & 5 (Interactive, Product-Flow & Visual UI Validation).
- **Resolution**: Implemented native Windows UI Automation (`UIAutomationClient`) and Edge WebView2 Chrome DevTools Protocol (CDP) test suites across both desktop implementations. 100/100 automated UI tests passing across shell navigation, adversarial stress, and product flows.
- **Remaining Backlog**: Human verification for physical peripherals (WIA scanner device acquisition, Android 16 Wi-Fi Direct socket discovery) and formal screen reader auditory readout (Tier C).
