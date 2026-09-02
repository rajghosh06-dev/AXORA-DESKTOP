# Axora Desktop — 25-Point UI Quality Checklist

This reusable checklist must be evaluated whenever new screens, components, or layout modifications are introduced into either `Axora-Desktop-MaterialUI` or `Axora-Desktop-WinUI`.

---

## 1. Visual Hierarchy & Layout
- [ ] **1. Grid Alignment**: All headers, cards, and input controls align strictly to the horizontal and vertical layout grid.
- [ ] **2. Consistent Margins**: Content containers maintain standard padding (16px or 24px) with no uneven spacing.
- [ ] **3. Icon Alignment**: Icons are vertically centered with their accompanying label text and use consistent glyph sizes (16px, 18px, or 24px).
- [ ] **4. Responsive Resizing**: At minimum window dimensions (WinUI: 1000x620; MaterialUI: 960x600), no controls overlap or become unreachable.
- [ ] **5. Clean Scrolling**: Content extending beyond screen height is wrapped in a smooth `ScrollViewer` / `overflow-y-auto` container without double scrollbars.
- [ ] **6. No Text Clipping**: Multi-line headers or status badges use `TextWrapping="Wrap"` or `TextTrimming="CharacterEllipsis"` to prevent harsh clipping.

---

## 2. Interactive Control States
- [ ] **7. Default / Rest State**: Controls have clear visual boundaries and intuitive affordance.
- [ ] **8. Hover State**: Buttons, menu items, and list rows provide clear subtle background brightening/tinting on hover.
- [ ] **9. Pressed State**: Buttons provide tactile scale or accent fill feedback on pointer press.
- [ ] **10. Focus State**: Keyboard navigation (Tab) highlights the active control with a distinct focus ring.
- [ ] **11. Disabled State**: Buttons are disabled with reduced opacity when mandatory prerequisites (e.g. file selection, password) are absent.
- [ ] **12. Loading State**: Long-running asynchronous operations display an indeterminate progress ring or spinner.
- [ ] **13. No Dead Buttons**: Every clickable element is bound to an active command or navigation route.

---

## 3. Forms & Data Input
- [ ] **14. Informative Placeholders**: Text inputs display clear hint text indicating expected formats.
- [ ] **15. Dialog Cancellation**: Cancelling a file picker or modal dialog preserves previous state without error.
- [ ] **16. Input Validation**: Password inputs, file paths, and numerical parameters validate constraints before submission.
- [ ] **17. Dirty State Tracking**: Forms with unsaved changes show a clear indicator or floating save/revert pill.

---

## 4. Theme & Accessibility
- [ ] **18. High Contrast**: Text maintains at least 4.5:1 contrast against surface backgrounds in both Dark and Light modes.
- [ ] **19. Theme Token Adherence**: Colors use framework theme tokens (`{ThemeResource ...}` in WinUI, `var(--md-sys-color-*)` in MaterialUI) rather than hardcoded hex codes.
- [ ] **20. Tooltips**: Icon-only buttons provide descriptive tooltips via `ToolTipService.ToolTip` or `title="..."`.
- [ ] **21. Keyboard Navigation**: Core workflows (e.g. flipping flashcards, opening Command Palette) support keyboard shortcuts.

---

## 5. System Feedback & Alerts
- [ ] **22. Success Confirmation**: Completed actions display unambiguous confirmation toasts.
- [ ] **23. Actionable Errors**: Failures report the exact cause with guidance on how to remediate.
- [ ] **24. Progress Telemetry**: Multi-file batch queues report completed count, percentage, and ETA.
- [ ] **25. Empty State Views**: Lists or tables with zero records render friendly illustrations and call-to-action buttons.
