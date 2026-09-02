# Project: Axora Desktop (WinUI 3) Navigation, File Pickers, Resume Studio PDF & UI Reactivity Audit

## Architecture
- **Framework**: .NET 9.0 Windows Desktop (`net9.0-windows10.0.26100.0`), Unpackaged WinUI 3 (`WindowsPackageType=None`, `win-x64`).
- **Architecture Pattern**: MVVM with `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`), Dependency Injection via `Microsoft.Extensions.DependencyInjection`.
- **PDF Engine**: Vector PDF compilation via `PdfSharpCore` with multi-page budgeting, vector typography, dividers, URL annotations, and 2-pass footers.
- **Native Pickers**: Multi-tier STA-threaded picker helper (`NativeFilePickerHelper`) utilizing WinRT `IInitializeWithWindow` + COM `IFileDialog` + Win32 `comdlg32`.
- **Test Infrastructure**: Standalone test project `Axora.Desktop.Tests` (`Axora.Desktop.Tests.csproj`).

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | ShellView Navigation Backstack & Guard | Clean navigation without duplicate frame pushes or backstack loops | M1 | Survey 1 (BUG-NAV-1) |
| 2 | Footer Menu Rail Synchronization | Sync `NavView.SelectedItem` for FooterMenuItems (`NavSettings`) on navigated | M1 | Survey 1 (BUG-NAV-2) |
| 3 | Dashboard-to-ResumeStudio Routing | Smooth transition from Dashboard buttons to Resume Editor without bouncing | M1 | ORIGINAL_REQUEST R1 |
| 4 | Navigation Service Abstraction | Centralized `INavigationService` parameter passing and decoupled navigation | M1 | Survey 1 (REC-NAV-3) |
| 5 | Native File/Folder Pickers STA & COM Interop | Multi-tier STA-threaded native WinRT/COM pickers with HWND binding | M2 | ORIGINAL_REQUEST R2 |
| 6 | FileTypeFilter Sanitization & Safety | Filter parsing stripping `*.*` to `*` and preventing empty filters | M2 | ORIGINAL_REQUEST R2 |
| 7 | Resume Studio Target Page Selectors | ToggleButton mutual exclusion preventing unselected visual state | M3 | Survey 3 (BUG-01) |
| 8 | Live Preview Visual Margin Binding | Live preview padding bound to `Formatting.MarginInches` | M3 | Survey 3 (BUG-02) |
| 9 | Live Preview Font Family Binding | Live preview typography bound to `Formatting.FontFamily` | M3 | Survey 3 (BUG-03) |
| 10 | Live Preview Divider Visibility Binding | Live preview section dividers bound to `Formatting.ShowDividers` | M3 | Survey 3 (BUG-04) |
| 11 | Header Centering & Section Title Casing | Implement `CenterHeader` and `UppercaseSectionTitles` in PDF compiler and preview | M3 | Survey 3 (BUG-05, BUG-06) |
| 12 | Resume Model Nested Item Budget Reactivity | Child item property change listeners for real-time page budget calculation | M3 | Survey 3 (BUG-07) |
| 13 | ATS Optimizer Comprehensive Text Extraction | Include Certifications, Achievements, Responsibilities in ATS text scanner | M3 | Survey 3 (BUG-08) |
| 14 | ATS Tokenizer 2-Letter Acronym Whitelist | Whitelist short technical keywords (`Go`, `JS`, `TS`, `CI`, `CD`, `UI`, `UX`, `DB`, `QA`, `OS`) | M3 | Survey 3 (BUG-09) |
| 15 | Resume Studio Editor Tab Toggle Fix | Prevent tab buttons from deselecting on repeated clicks | M3 | Survey 3 (BUG-10) |
| 16 | Flashcards Deck Selection Reactivity | Implement `OnActiveDeckChanged` to synchronize cards upon TwoWay `SelectedItem` changes | M4 | Survey 1 (BUG-FC-1) |
| 17 | FlashCard Model ObservableObject | Make `FlashCard` inherit `ObservableObject` for single-card deck rating reactivity | M4 | Survey 1 (BUG-FC-2) |
| 18 | Flashcards Keyboard Navigation & Shortcuts | Add KeyDown accelerators (Space/Enter to flip, Left/Right for prev/next, 1/2/3 for ratings) | M4 | Survey 1 (REC-FC-3) |
| 19 | Flashcards Declarative Binding Properties | Expose `CurrentCardPrompt` and `CurrentCardLabel` for direct XAML binding | M4 | Survey 1 (REC-FC-4) |
| 20 | E2E Testing Suite (Tiers 1-4) | Comprehensive opaque-box test suite validating all features | E2E | Dual Track |
| 21 | Adversarial Hardening (Tier 5) | White-box adversarial test cases and edge-case stress verification | Final | Dual Track Phase 2 |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| E2E | E2E Testing Track | Test harness, test runners, Tiers 1-4 tests covering all 19 functional features | none | IN_PROGRESS |
| 1 | Dashboard & Shell Navigation Routing | ShellView backstack guards, footer menu synchronization, INavigationService | none | IN_PROGRESS |
| 2 | Native Pickers & Dialog Robustness | Verify NativeFilePickerHelper across all 8 tools, STA threading, filter validation | none | IN_PROGRESS |
| 3 | Resume Studio & PDF Vector Export Engine | Target page selector fix, preview styling bindings, ATS text coverage, acronym whitelist, PDF layout | M1, M2 | PLANNED |
| 4 | Flashcards & Interactive Tools Reactivity | OnActiveDeckChanged, FlashCard ObservableObject, keyboard shortcuts, declarative bindings | M1 | PLANNED |
| Final | E2E Verification & Adversarial Hardening | 100% pass of E2E suite (Tiers 1-4) + Tier 5 Adversarial Coverage Hardening | E2E, M1, M2, M3, M4 | PLANNED |

## Interface Contracts
### INavigationService ↔ ShellView / ViewModels
- `void NavigateTo(string pageTag, object? parameter = null)`: Navigates ContentFrame to target page mapped in `ShellViewModel.PageMap`.
- `bool CanGoBack { get; }`: Returns whether navigation backstack is non-empty.
- `void GoBack()`: Navigates back in ContentFrame.
- `event EventHandler<NavigationEventArgs> Navigated`: Notifies listeners when navigation completes.

### IResumePdfCompilerService ↔ ResumeStudioViewModel
- `Task<string> CompileToPdfAsync(ResumeDocument document, string destinationPath)`: Compiles document into vector PDF file.
- `Task<byte[]> CompileToBytesAsync(ResumeDocument document)`: Compiles document into in-memory PDF byte stream.

### IAtsOptimizerService ↔ ResumeStudioViewModel
- `Task<AtsAnalysisResult> AnalyzeAsync(ResumeDocument document, string jobDescription)`: Extracts keywords, verbs, calculates score.

## Code Layout
- `Axora.Desktop/` - Main WinUI 3 Desktop Application
  - `Views/` - WinUI 3 XAML Pages (`ShellView`, `DashboardPage`, `ResumeStudioPage`, `ResumeStudioDashboardPage`, `FlashcardsPage`, `BatchImagePage`, etc.)
  - `ViewModels/` - CommunityToolkit.Mvvm ViewModels (`ShellViewModel`, `ResumeStudioViewModel`, `FlashcardsViewModel`, etc.)
  - `Models/` - Observable data models (`ResumeModel.cs`, `FlashcardDeck.cs`, etc.)
  - `Services/` - Core services & interfaces (`ResumePdfCompilerService.cs`, `AtsOptimizerService.cs`, `NavigationService.cs`, etc.)
  - `Helpers/` - Native interop helpers (`NativeFilePickerHelper.cs`, etc.)
  - `Converters/` - XAML Value Converters
- `Axora.Desktop.Tests/` - Automated Test Suite
  - `Infrastructure/` - Test runners and assertions
  - `Tests/` - Unit, integration, and E2E test cases
