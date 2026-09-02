# E2E Test Infra: Axora Desktop (WinUI 3)

## Test Philosophy
- Requirement-driven, opaque-box and contract-level verification.
- Methodology: Category-Partition, Boundary Value Analysis, Pairwise Combinations, Real-World Workload Scenarios.

## Feature Inventory & Test Coverage Goals
| # | Feature | Requirement | Tier 1 (Functional) | Tier 2 (Boundaries) | Tier 3 (Cross-Feature) | Tier 4 (Workload) |
|---|---------|-------------|:-------------------:|:-------------------:|:---------------------:|:-----------------:|
| 1 | Navigation & DI Lifecycle | R1 | 5 cases | 5 cases | ✓ | ✓ |
| 2 | Native Pickers Across Tools | R2 | 5 cases | 5 cases | ✓ | ✓ |
| 3 | Resume Studio & PDF Engine | R3 | 5 cases | 5 cases | ✓ | ✓ |
| 4 | Flashcards & Reactivity | R4 | 5 cases | 5 cases | ✓ | ✓ |

## Test Architecture
- **Compilation Check**: `dotnet build Axora.Desktop/Axora.Desktop.csproj -c Debug` -> 0 errors.
- **Unit & Logic Verification**: Tests covering ATS calculations, PDF pagination wrapping, SM-2 retention metrics, and DI singleton resolution.
- **Interactive UI & Dialog Verification**: File picker prompts, shell navigation routing, and live property notification synchronization.

## Verification Tiers
- **Tier 1 (Feature Coverage)**: Individual verification of navigation routes, picker dialog invocation, ATS score computation, deck flipping.
- **Tier 2 (Boundary & Corner Cases)**: Empty job description in ATS, multi-page text overflow across page boundaries in PDF compiler, 0-byte batch image jobs, empty flashcard decks.
- **Tier 3 (Cross-Feature Combinations)**: Scholar Kit OCR text push -> Flashcard Studio deck generation -> SM-2 card rating -> CSV export via native save picker.
- **Tier 4 (Real-World Workloads)**: Multi-page CV compilation with custom margins and links, Batch image pipeline execution with real-time size updates.
