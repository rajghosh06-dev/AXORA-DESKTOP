# AXORA Desktop — Real UI Visual Audit Report

**Author**: Senior Desktop UI/UX Architect & Visual QA Reviewer  
**Date**: September 4, 2026  
**Status**: VERIFIED  
**Evidence**: Native PNG Screenshots captured under `docs/qa/screenshots/`

---

## 1. Scope & Methodology

This visual audit evaluates the visual layout, typography, alignment, responsive behavior, and visual hierarchy across both desktop implementations:
- **Axora-Desktop-MaterialUI** (Google Material Design 3 specification, Tailwind CSS, Framer Motion)
- **Axora-Desktop-WinUI** (Windows 11 Fluent Design System, Mica Alt, XAML Controls)

All evaluations were conducted against live rendered surfaces captured during active desktop runs.

---

## 2. 25-Point Visual Inspection Checklist

| # | Inspection Point | MaterialUI (Tauri) | WinUI 3 (WinAppSDK) | Visual Assessment |
|---|---|---|---|---|
| 1 | Window Framing & Title Bar | Embedded custom titlebar with drag handle, window controls, and logo | Custom Tall AppTitleBar (36px), Mica Alt backdrop integration | **PASS** — Both adhere to platform conventions |
| 2 | Minimum Size Clamping | Window clamps at 960x600 px without content distortion | Window clamps at 1000x620 DIP via `WM_GETMINMAXINFO` subclassing | **PASS** — No squished layouts or truncated cards |
| 3 | Horizontal Overflow | Verified 0 horizontal overflow (`scrollWidth <= innerWidth + 5`) | ScrollViewers scoped vertically with horizontal wrapping | **PASS** — No unwanted horizontal scrollbars |
| 4 | Vertical Scrollbars | `scrollbar-thin` styled with rounded thumb matching MD3 theme | Native Fluent thin overlay scrollbars with auto-hide | **PASS** — Clean unobtrusive scrollbars |
| 5 | Spacing & Margins | Consistent 4/8/12/16/24 px rhythm (Tailwind scale) | Consistent 4/8/12/16/20/24 px Spacing in StackPanels | **PASS** — Harmonious layout rhythm |
| 6 | Padding & Safe Areas | 24px content margin, 16px card padding | 24px Page margin, 20px Card padding | **PASS** — Comfortable breathing room |
| 7 | Typography: Headers | Google Sans / Inter (`text-headline-md`, `text-title-lg`) | Segoe UI Variable (`SubtitleTextBlockStyle`, `FontWeight="SemiBold"`) | **PASS** — Clear typographic hierarchy |
| 8 | Typography: Body | Roboto / Inter (`text-body-md`, `text-body-lg`) | Segoe UI Variable (`BodyTextBlockStyle`) | **PASS** — High legibility |
| 9 | Typography: Captions | `text-xs` / `text-caption` with 60% opacity secondary color | `CaptionTextBlockStyle` with `TextFillColorSecondaryBrush` | **PASS** — Subtle secondary metadata |
| 10 | Icon Sizing & Consistency | Lucide icons sized consistently (16px, 20px, 24px) | Segoe Fluent Icons (E-glyphs) sized 12-16px | **PASS** — Unified icon weights |
| 11 | Button Sizing & Touch Targets | Minimum 36px height, rounded-full pills or rounded-2xl | Minimum 32px height, corner radius 4-8px | **PASS** — Clean clickable footprints |
| 12 | Card Surfaces | MD3 surface containers (`surface-container-low/high`) with 1px outline | `CardBackgroundFillColorDefaultBrush` with `CardStrokeColorDefaultBrush` | **PASS** — Elevated visual depth |
| 13 | Corner Radii | MD3 rounded-2xl (16px) and rounded-3xl (24px) | Win11 standard rounded-md (8px) and rounded-lg (12px) | **PASS** — Modern rounded aesthetics |
| 14 | Accent Theming | Dynamic CSS variable tinting (`--md-sys-color-primary`) | Dynamic Resource Dictionary brushes (`AccentFillColorDefaultBrush`) | **PASS** — Accent colors propagate across controls |
| 15 | Theme Switching: Dark Mode | Deep charcoal/zinc tones (`rgb(17, 20, 24)`), excellent contrast | Mica Alt dark slate backdrop, high contrast text | **PASS** — True dark theme immersion |
| 16 | Theme Switching: Light Mode | Soft warm neutral surfaces (`rgb(250, 248, 245)`), crisp typography | Clean light Mica surface, deep text fill | **PASS** — Soft, non-glaring light mode |
| 17 | Hover States | MD3 hover state overlay (8% primary tint) | Windows Fluent PointerOver theme brushes | **PASS** — Immediate interactive feedback |
| 18 | Active / Pressed States | MD3 ripple expansion from pointer coordinates | Windows Fluent Pressed scale and tint effect | **PASS** — Tactile responsiveness |
| 19 | Focus Rings | Outlined focus indicators with 2px offset | High-contrast double-line focus rectangle | **PASS** — Clear keyboard focus tracking |
| 20 | Modal / Dialog Overlay | Fixed inset blur backdrop (`backdrop-blur-md bg-black/45`) | Dim overlay with `AcrylicInAppFillColorDefaultBrush` | **PASS** — Focused dialog elevation |
| 21 | Modal Centering & Scale | Spring-animated scale in center of viewport | Centered modal with top margin offset | **PASS** — Balanced placement |
| 22 | Z-Ordering | Modals: z-50, Dropzone: z-40, Sidebar: z-20, Content: z-10 | OverlayContainer rendered in top XAML layer over NavigationView | **PASS** — Zero visual clipping behind content |
| 23 | Empty States | Centered iconography, explanatory guidance, primary call-to-action | Clean empty placeholders with instruction hints | **PASS** — Welcoming zero-data states |
| 24 | Loading States | Animated spinners (`Loader2 animate-spin`, ProgressRing) | Windows App SDK `ProgressRing` and `ProgressBar` | **PASS** — Informative busy states |
| 25 | Brand Asset Integrity | Logo imported via ES module, rendered with crisp edges | Vector AppIcon & Square44x44Logo rendered cleanly | **PASS** — Crisp brand presentation |

---

## 3. Visual Artifact Evidence

Captured during live desktop execution:
1. `docs/qa/screenshots/materialui-01-dashboard.png`: MaterialUI Workspace Hub with navigation rail, status badge, theme toggles, and 6 quick action cards.
2. `docs/qa/screenshots/materialui-02-scholar-kit.png`: Scholar Kit offline OCR and document research suite.
3. `docs/qa/screenshots/materialui-02-settings.png`: MaterialUI Settings view showing theme mode controls and accent color palette.
4. `docs/qa/screenshots/winui-01-dashboard.png`: WinUI 3 Dashboard with Mica Alt backdrop, system telemetry, and quick drops.
5. `docs/qa/screenshots/adversarial-stress-layout.png`: MaterialUI layout under boundary viewport stress (960x600 and 1920x800).
6. `docs/qa/screenshots/winui-adversarial-stress.png`: WinUI 3 desktop surface following rapid route thrashing and window clamping.
7. `docs/qa/screenshots/product-flow-converter-queue.png`: MaterialUI Universal Engine with 2 files queued, format selected (.png), and Start Conversion button enabled.
8. `docs/qa/screenshots/product-flow-vault-dialog.png`: MaterialUI AxoraVault interactive encryption password dialog with password reveal toggle and cancellation controls.
9. `docs/qa/screenshots/product-flow-flashcards-deck.png`: MaterialUI Flashcard Studio showing active Android Native Development study deck, card count, and SM-2 retention curve SVG with 3 milestone data points.
10. `docs/qa/screenshots/winui-product-flow-settings.png`: WinUI 3 Settings page with Green Accent (#00C853) swatch selected, Auto-Start P2P Engine switch toggled, and Argon2 memory slider adjusted to 128 MB.
11. `docs/qa/screenshots/winui-product-flow-flashcards.png`: WinUI 3 Flashcard Studio with SM-2 rating buttons (Hard, Medium, Easy) and active recall card interaction.
12. `docs/qa/screenshots/winui-product-flow-compressor.png`: WinUI 3 Compressor view with compression profile selection and queue management controls.

---

## 4. Visual QA Verdict & Honest Boundaries

- **Visual Alignment & Harmony (Key Surfaces Evaluated)**: **PASS**
- **Responsive Clamping & Overflow Bounds**: **PASS** (Zero horizontal overflow; min-size hooks active)
- **Theme Transitions**: **PASS** (Dynamic Dark/Light tokens verified)
- **Production Brand Asset Integrity**: **PASS** (Logo bundling verified)
- **Full Multi-Resolution Pixel-Perfect Regression Across All 80 Surfaces**: **NOT VERIFIED / MANUAL VERIFICATION REQUIRED** (Full pixel-by-pixel perceptual diffing across every resolution and state requires human designer sign-off or dedicated Percy/Playwright visual regression tooling).
