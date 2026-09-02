---
name: axora-winui
description: Development, architecture, MVVM conventions, XAML visual states, and C# service integration for the Axora WinUI desktop application (.NET 9 + Windows App SDK 1.6 + WinUI 3 + XAML + C# 13). Use when creating pages, modifying view models, or debugging WinUI 3 XAML issues in Axora-Desktop-WinUI.
---

# Axora WinUI Development Guide

This skill documents the architecture, XAML invariants, MVVM conventions, and build instructions for `Axora-Desktop-WinUI`.

## Architecture Overview
- **Framework**: .NET 9.0 (`net9.0-windows10.0.26100.0`, `win-x64`), Windows App SDK 1.6.250228001.
- **Pattern**: Strict MVVM with `CommunityToolkit.Mvvm` 8.4 (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`).
- **Dependency Injection**: `Microsoft.Extensions.Hosting` + `Microsoft.Extensions.DependencyInjection` configured in `App.xaml.cs`.
- **Windowing**: `MainWindow.cs` using `MicaBackdrop` (`Kind = MicaKind.BaseAlt`), custom tall titlebar, and `WM_GETMINMAXINFO` subclassing (1000x620 DIP minimum).

## Project Layout
- `Axora.Desktop/Views/`: WinUI 3 Pages (`DashboardPage.xaml`, `ScholarKitPage.xaml`, `ResumeStudioPage.xaml`, `ResumeStudioDashboardPage.xaml`, `BatchImagePage.xaml`, `CompressorPage.xaml`, `VaultPage.xaml`, `FlashcardsPage.xaml`, `MobileLinkPage.xaml`, `SettingsPage.xaml`, `ShellView.xaml`).
- `Axora.Desktop/ViewModels/`: ViewModels registered as singletons (`ShellViewModel.cs`, `DashboardViewModel.cs`, `ResumeStudioViewModel.cs`, `ScholarKitViewModel.cs`, `FlashcardsViewModel.cs`, etc.).
- `Axora.Desktop/Services/Contracts/`: 19 core interface contracts (`IResumePdfCompilerService`, `IAtsOptimizerService`, `ISecurityVaultService`, `IOcrService`, `IP2pSyncService`, etc.).
- `Axora.Desktop/Services/`: Concrete implementations (`ResumePdfCompilerService.cs` using `PdfSharpCore`, `DirectMlEmbeddingService.cs` using `Microsoft.ML.OnnxRuntime.DirectML`, `StreamingVaultService.cs`, `WiaScannerService.cs`).
- `Axora.Desktop/Helpers/`: `NativeFilePickerHelper.cs` (multi-tier STA file/folder pickers), `SimdVectorHelper.cs`, `DispatcherHelper.cs`.
- `Axora.Desktop.Tests/`: Standalone console test suite executing 59 adversarial stress assertions.

## WinUI 3 Invariants & Rules
1. **Preserve MarkupCompilePass2**: Never bypass or stub out Pass 2 compilation in project targets.
2. **Never Implement `IXamlMetadataProvider` in App**: Rely exclusively on auto-generated `XamlTypeInfo.g.cs`.
3. **Explicit Button Visual States**: Custom `ToggleButton` or `RadioButton` styles must specify explicit `Unchecked` state setters.
4. **ThemeResource Safety**: Ensure all `{ThemeResource ...}` keys exist in system dictionaries before binding.
5. **MVVM Observable Properties**: Use field-backed `[ObservableProperty]` declarations for reliable WinRT AOT marshalling.

## Build & Test Commands
- **Recommended QA Script Execution**:
  ```powershell
  # Clean build WinUI application and tests:
  .\scripts\qa\build-all.ps1 -Target WinUI

  # Run automated adversarial stress tests:
  .\scripts\qa\run-tests.ps1 -Target WinUI

  # Perform runtime smoke launch and startup log validation:
  .\scripts\qa\smoke-test.ps1 -Target WinUI
  ```

- **Direct Toolchain Commands**:
  - Build via CLI `dotnet build`:
    ```powershell
    dotnet build Axora.Desktop\Axora.Desktop.csproj -p:Platform=x64
    ```
  - Direct Test Runner Execution:
    ```powershell
    .\Axora.Desktop.Tests\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.Tests.exe
    ```
  - Direct Application Launch:
    ```powershell
    .\Axora.Desktop\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Desktop.exe
    ```
