# Rule 06: Desktop UI/UX Quality Standards

## Presentation & Layout Standards
- **Alignment & Visual Hierarchy**: Controls and text must align cleanly to the primary layout grid. Header titles, iconography, and action buttons must have consistent margins (typically 12px, 16px, or 24px).
- **Responsive Bounds & Resizing**: Test layouts at both minimum window dimensions (WinUI: 1000x620 DIP; MaterialUI: 960x600 px) and maximized states. No critical controls may be clipped, overlapped, or truncated.
- **Interactive State Completeness**: Every interactive button, list item, or input must render distinct visual feedback for:
  - Default / Rest
  - Hover
  - Pressed / Active
  - Focus (Keyboard navigation indicator)
  - Disabled
  - Loading / Progress state
- **Form Controls & Validation**:
  - Empty text inputs must display informative placeholder hints.
  - File pickers must handle cancelled dialogs without clearing previous selections or throwing exceptions.
  - Number inputs must clamp values to valid ranges.
- **Theme Consistency**:
  - All text must maintain high contrast ratios against its container surface (WCAG AA compliant).
  - Both Dark and Light themes must be tested if theme toggling is supported.
- **Feedback & Notifications**:
  - Long-running async tasks (OCR, PDF generation, batch processing) must show an indeterminate loader or determinate progress bar.
  - Finished tasks must report unambiguous success toasts or clear error explanations.
