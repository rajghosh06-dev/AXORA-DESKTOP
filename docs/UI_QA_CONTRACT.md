# AXORA Desktop - UI Quality Assurance Contract

This document establishes the formal engineering contract for what constitutes a **"UI PASS"** across both implementations of the AXORA Desktop workstation suite:

1. **Axora Desktop - MaterialUI** (Tauri v2 + React 18 + Tailwind CSS MD3 + Rust)
2. **Axora Desktop - WinUI** (.NET 9 + Windows App SDK 1.6 + WinUI 3 XAML + C# 13)

---

## 1. Six-Layer Verification Architecture

To eliminate ambiguity between automated logic assertions and visual fidelity, AXORA mandates explicit classification into six distinct validation layers:

```
+-------------------------------------------------------------+
| LAYER 1: STATIC & COMPILATION VALIDATION                   |
| (MSBuild / dotnet build / tsc / cargo check: 0 errors)      |
+-------------------------------------------------------------+
| LAYER 2: AUTOMATED FUNCTIONAL VALIDATION                   |
| (Unit tests, algorithmic stress, boundary defense)          |
+-------------------------------------------------------------+
| LAYER 3: RUNTIME PROCESS & LIFECYCLE SMOKE                 |
| (Process spawn, PID check, startup diagnostics, shutdown)   |
+-------------------------------------------------------------+
| LAYER 4: INTERACTIVE & NAVIGATION VALIDATION               |
| (Route switching, command palette, accelerators, drag-drop) |
+-------------------------------------------------------------+
| LAYER 5: VISUAL LAYOUT & HIERARCHY VALIDATION              |
| (Grid alignment, padding, no clipping, theme contrast)      |
+-------------------------------------------------------------+
| LAYER 6: ACCESSIBILITY (A11Y) VALIDATION                   |
| (Focus rings, keyboard tab order, tooltips, high contrast)  |
+-------------------------------------------------------------+
```

> **Zero-Fabrication Invariant**: Never equate passing automated logic assertions (Layer 2) with visual layout verification (Layer 5). Automated tests prove that data structures and algorithms execute without exception; only active rendering inspection (via screenshots, Chrome DevTools MCP, or human visual protocol) constitutes visual validation.

---

## 2. Universal "UI PASS" Criteria

For any view, dialog, or workflow in AXORA Desktop to receive a **UI PASS**, it must satisfy all 15 universal dimensions:

### 1. Layout & Alignment
- **Grid Alignment**: Headers, cards, form fields, and action buttons align to the established layout grid (multiples of 4px/8px).
- **Icon Alignment**: Glyphs are vertically centered with their adjacent text labels.
- **Visual Balance**: Equal margins on opposing screen edges (standard 16px, 20px, or 24px).

### 2. Spacing & Padding
- **Touch & Click Targets**: Interactive targets must be at least 32x32 DIP (desktop standard) with minimum 8px separation.
- **Card Containers**: Padding within cards is consistent (16px or 20px).

### 3. Typography & Text Hierarchy
- **Type Scale**: Clear hierarchy distinguishing Display Titles, Page Headers, Section Subtitles, Body Copy, and Captions.
- **Clipping & Truncation**: No text may be clipped at minimum window sizes. Multi-line descriptions must use `TextWrapping="Wrap"` or explicit ellipsis `TextTrimming="CharacterEllipsis"`.

### 4. Interactive Control States
Every clickable element (button, toggle, card, list item) must provide distinct feedback across all states:
- **Rest (Default)**: Clear boundary and intuitive affordance.
- **Hover**: Subtle background tint or brightness shift within 100ms.
- **Pressed**: Tactile scale down (0.98x) or accent color fill on pointer press.
- **Focus**: Distinct focus ring when navigated via keyboard.
- **Disabled**: Reduced opacity (38%-50%) and cursor change when prerequisites are unmet.
- **Loading**: Spinner or indeterminate progress indicator during async work.

### 5. Keyboard & Accelerator Interaction
- **Tab Navigation**: Tab order flows logically from top-to-bottom, left-to-right.
- **Global Accelerators**:
  - `Ctrl+K`: Opens the Command Palette from any screen.
  - `Ctrl+\`: Toggles the navigation pane/rail.
  - `Escape`: Closes active dialogs, flyouts, or modal sheets.
  - `Enter`: Submits the focused primary action form.

### 6. Mouse & Pointer Interaction
- **Cursor Feedback**: Pointer changes to hand over interactive links and action buttons.
- **Drag-and-Drop**: Dragging files over the window displays a semi-transparent drop overlay (`FileDropZoneOverlay`). Dropping valid files adds them to processing queues.

### 7. Navigation Integrity
- **No Dead Links**: Every button, navigation item, and quick action must navigate to a real screen or trigger a defined command.
- **State Preservation**: Navigating away from a page and returning must preserve in-memory user inputs where appropriate.
- **Breadcrumbs/Active Indicator**: Navigation rail/sidebar highlights the active page accurately.

### 8. Loading States
- **Indeterminate Progress**: Operations of indeterminate duration (e.g. OCR, AI embeddings) display a continuous indeterminate progress ring.
- **Determinate Progress**: Batch queues display completed count, total count, percentage, and estimated time remaining.

### 9. Error States & Input Validation
- **Inline Validation**: Invalid text fields display informative red/amber error text immediately below the field.
- **Non-Destructive Failures**: Failed operations (e.g. corrupted file import) display actionable error toasts and leave existing state intact.

### 10. Empty States
- **Friendly Illustration & Guidance**: Lists with zero records (e.g. empty flashcard decks, empty batch queues) display a clear illustration, explanatory text, and a prominent call-to-action button (e.g. "Create Card", "Select Files").

### 11. Responsive & Resizing Behavior
- **Minimum Dimensions**:
  - WinUI: **1000x620 DIP** (enforced by `WM_GETMINMAXINFO` subclassing).
  - MaterialUI: **960x600 px** (enforced by Tauri window configuration).
- **Maximized & Ultrawide**: Content must not stretch awkwardly; use centered max-width content containers where appropriate.

### 12. Accessibility (a11y)
- **Contrast**: Text-to-surface contrast ratio meets or exceeds **4.5:1** (WCAG AA).
- **Tooltips**: Icon-only buttons provide descriptive tooltips.
- **Automation IDs / Names**: Controls have defined names for accessibility tools (`AutomationProperties.Name` in XAML / `aria-label` in HTML).

### 13. Window Startup & Shutdown
- **Zero White Flash**:
  - WinUI: Mica Alt backdrop and `AppWindow` activated immediately upon composition ready.
  - MaterialUI: 120ms hidden window initialization delay until Vite mounts `SplashScreen`.
- **Clean Exit**: Closing the window releases all process locks and background threads.

### 14. Dialogs & Flyouts
- **Modal Discipline**: Dialogs darken background content and block underlying interaction until dismissed.
- **Dismissal Safety**: Clicking Cancel or pressing Escape must discard uncommitted form changes without throwing exceptions.

### 15. Notifications & Feedback
- **Toast Notifications**: Completed operations trigger unambiguous confirmation toasts auto-dismissing after 3-5 seconds.

---

## 3. Platform-Specific Conventions

### A. Axora-Desktop-WinUI (Windows 11 Fluent)
- **Backdrop**: `MicaBackdrop` with `Kind = MicaKind.BaseAlt` integrated into custom tall titlebar.
- **Design Language**: Windows 11 Fluent Design System, Segoe UI Variable fonts, standard corner radiuses (8px controls, 4px inner elements).
- **Theme Resources**: Strict adherence to Windows App SDK theme brushes (`{ThemeResource ApplicationPageBackgroundThemeBrush}`, `{ThemeResource CardBackgroundFillColorDefaultBrush}`).
- **VisualState Discipline**: Every custom `ControlTemplate` for `ToggleButton` or `RadioButton` must provide explicit Setters in the `Unchecked` state to ensure correct unhighlighting.

### B. Axora-Desktop-MaterialUI (Material Design 3)
- **Design Tokens**: Material Design 3 CSS custom properties (`var(--md-sys-color-surface)`, `var(--md-sys-color-primary)`).
- **Surfaces**: Elevation levels (Level 0 through Level 5) using surface tonal elevation.
- **Motion**: Framer Motion transitions with standard MD3 easing (`cubic-bezier(0.2, 0.0, 0, 1.0)`).
- **State Management**: Zustand stores (`themeStore`, `toastStore`, `useQuickDropStore`).

---

## 4. QA Evidence Requirements

For any verification gate, evidence must be recorded with:
1. Target application (`WinUI` or `MaterialUI`).
2. Timestamp of execution.
3. Exit code and duration.
4. Detailed breakdown across the 6 verification layers.
5. Exact error message and stack trace for any failure.
