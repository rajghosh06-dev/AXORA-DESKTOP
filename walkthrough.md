# Phase W3-C.6.4 Walkthrough: Normalization End-to-End Integration, Resilience & Diagnostic Telemetry

## Overview
Phase W3-C.6.4 integrates the C6.1/C6.2/C6.3 text normalization pipeline (`ITextNormalizer` / `TextNormalizer`) into the real Scholar document extraction workflow through an authoritative orchestrator service (`IScholarExtractionOrchestrator` / `ScholarExtractionOrchestrator`).

## Key Changes Made

### 1. `ScholarExtractionOrchestrator.cs` (`Axora.Desktop/Services/ScholarExtractionOrchestrator.cs`)
- **Single Normalization Entry Point**: Implements the authoritative lifecycle:
  `IDocumentExtractorEngine` $\to$ `RawExtractionResult` $\to$ `ITextNormalizer.NormalizePage` $\to$ `Normalized ExtractedPageRaw` $\to$ `DocumentPageBuilder` $\to$ `ScholarDocument`.
- **One-Page Failure Isolation**: If normalization encounters an unhandled exception on page $K$, only page $K$ falls back to `RawText` with diagnostic warning `ERR_NORMALIZATION_FAILED`. All other pages normalize without interruption.
- **Transparent Cancellation**: Pre-cancelled or triggered `CancellationToken` instances immediately propagate `OperationCanceledException` without being swallowed or wrapped.
- **Format-Aware Dispatch**: Correctly dispatches pages according to format (`PlainText`, `Markdown`, `DelimitedText`, `LocalHtml`, `PdfDigital`, `PdfScanned`, `PdfMixed`, `Docx`, `RasterImage`, `MultiPageTiff`).

### 2. Telemetry Contracts (`Axora.Desktop/Models/ExtractionContracts.cs`)
- **`NormalizationDiagnosticCodes`**: Standardized string status constants (`NORM_OK`, `NORM_WARNING`, `NORM_FAILED`, `NORM_CANCELLED`, `NORM_FALLBACK_RAW`).
- **`NormalizationStatus`**: Enum indicating aggregate document status (`NotRun`, `Succeeded`, `SucceededWithWarnings`, `Failed`, `Cancelled`, `FallbackToRaw`).
- **`PageNormalizationTelemetry`**: Tracks per-page execution duration, input/output character counts, OCR flags, confidence, status codes, and non-sensitive warnings. Strictly zero document text or excerpts.
- **`DocumentNormalizationTelemetry`**: Tracks total durations, page counts (total, successful, failed, fallback), character deltas, and OCR flags.
- **Contract Wiring**: Added `NormalizationTelemetry` to `ExtractionReport` and `ScholarExtractionResult`.

### 3. Dependency Injection Registration (`Axora.Desktop/App.xaml.cs`)
- Registered `IScholarExtractionOrchestrator` and `ScholarExtractionOrchestrator` as application singletons in the WinUI DI container.

### 4. Test Suite Integration (`Axora.Desktop.Tests/Program.cs`)
- Added `RunW3_C6_4NormalizationIntegrationTests()` covering all 21 categories A through U with 52 new assertions (`W3C6_4_1a` through `W3C6_4_15a`).
- Added `StubMultiPageExtractorEngine` test helper class.
- Total integrated assertions: **1,203 / 1,203 passed (100% green)**.

## Verification Results
- **Build**: `dotnet build Axora-Desktop-WinUI\Axora.Desktop.sln -c Debug --no-restore` $\to$ `0 Error(s)`, `0 new Warnings`.
- **Tests**: `run-tests.ps1 -Target WinUI` $\to$ `1203 Passed | 0 Failed | 0 Skipped`.
- **Git HEAD**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`.
- **Git Staging**: 0 staged files (`git diff --cached --stat` empty).
- **MaterialUI**: 100% untouched.

## Phase Status
Phase W3-C.6.4 is **COMPLETE AND READY FOR INDEPENDENT CLOSURE AUDIT**.
