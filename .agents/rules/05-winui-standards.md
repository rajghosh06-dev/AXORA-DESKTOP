# Rule 05: WinUI Application Standards & Invariants

## Technology Stack Conventions
- **Framework**: .NET 9.0 Windows Desktop (`net9.0-windows10.0.26100.0`), Windows App SDK 1.6.250228001, C# 13.
- **Packaging**: Unpackaged Win32 application (`WindowsPackageType=None`, `win-x64`).
- **Architecture**: Strict MVVM using `CommunityToolkit.Mvvm` 8.4 (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`), and Microsoft.Extensions Dependency Injection singletons inside `App.xaml.cs`.

## Critical WinUI 3 XAML Compilation Invariants
1. **Never Bypass `MarkupCompilePass2`**: Never override or empty out `MarkupCompilePass2` in `.csproj` or `Directory.Build.targets`. Bypassing Pass 2 generates empty `.g.cs` files, silently breaking all named elements (`x:Name`) and event connections.
2. **Never Manually Override `IXamlMetadataProvider` in App**: WinUI 3 automatically generates `XamlTypeInfo.g.cs` implementing `IXamlMetadataProvider`. Manually defining this creates `CS0111` duplicate member compiler errors.
3. **Explicit VisualState Transitions for Buttons**: When authoring `ControlTemplate` styles with `VisualStateManager` for `RadioButton` or `ToggleButton`, the `Unchecked` state **MUST** define explicit Setters for `Background`, `BorderBrush`, and `Foreground`.
4. **ThemeResource Safety**: Ensure all `{ThemeResource ...}` keys actually exist in Windows App SDK dictionaries or `App.xaml` merged dictionaries (e.g. avoiding undefined keys like `Elevation16Shadow`).

## Build & Process Safety
- Always build using Visual Studio MSBuild or pass valid `AppxMSBuildToolsPath` parameters.
- Always ensure running `Axora.Desktop.exe` processes are terminated before compilation to prevent file lock errors (`MSB3021/MSB3027`).
