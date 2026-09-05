# AXORA Desktop — Native WinUI Development Guide & Engineering Rules
**Document Version**: 1.0.0 (Phase W0 Baseline)  
**Target Platform**: Windows 11 (x64) · .NET 9.0 (`net9.0-windows10.0.26100.0`) · Windows App SDK 1.6  
**Pattern**: Strict MVVM · CommunityToolkit.Mvvm 8.4 · Microsoft.Extensions.Hosting DI  

---

## 1. Core Architectural Invariants

### 1.1 Never Bypass `MarkupCompilePass2`
- In WinUI 3 / Windows App SDK projects, **never** override or stub out `MarkupCompilePass2` in `.csproj` or `Directory.Build.targets`.
- `MarkupCompilePass2` generates `.g.cs` files containing `IComponentConnector.Connect()`.
- Bypassing Pass 2 generates empty 0-byte `.g.cs` files, causing all XAML event handlers (`Click="Handler"`) and named element references (`x:Name="Control"`) to silently fail to connect at runtime without throwing compilation errors.

### 1.2 Never Manually Implement `IXamlMetadataProvider` in `App`
- WinUI 3 Pass 2 automatically creates `XamlTypeInfo.g.cs`, which implements `IXamlMetadataProvider` on `public sealed partial class App : Application`.
- Manually implementing `IXamlMetadataProvider` conflicts with the compiler-generated implementation, causing `CS0111` duplicate member compiler errors.

### 1.3 Strict VisualState Unchecked Setters for ToggleButton & RadioButton
- When authoring custom `ControlTemplate` styles with `VisualStateManager` for `RadioButton` or `ToggleButton`:
- The `Unchecked` visual state **MUST** define explicit Setters for `Background`, `BorderBrush`, and `Foreground` (matching the default inactive rest state).
- Without explicit setters in `Unchecked`, WinUI 3 does not revert the `Checked` visual state appearance when deselected, causing controls to remain permanently highlighted.

---

## 2. XAML Layout & Structure Rules

### 2.1 Window Dimensions & Clamping
- The minimum window dimensions are **1000x620 DIP**, enforced via Win32 `WM_GETMINMAXINFO` subclassing in `MainWindow.cs`.
- All pages must render cleanly without clipping, overflow, or truncated text at 1000x620 DIP.

### 2.2 Grid Hierarchy & Spacing
- Use standard layout grids with row/column definitions. Never rely on absolute positioning (`Canvas`) for general layouts.
- **Row & Column Spacing**: Always use `RowSpacing` and `ColumnSpacing` attributes on `Grid` and `StackPanel` instead of redundant margins on individual children.
- **Spacing Scale**: Follow the 4px/8px design grid:
  - Micro spacing: `4px`, `6px`
  - Control spacing: `8px`, `12px`
  - Card/Container padding: `16px`, `18px`, `24px`
  - Page outer margin/padding: `24px` horizontal, `16px`/`20px` vertical

### 2.3 ScrollViewer Discipline
- When content can exceed the viewport, wrap scrollable sections in `<ScrollViewer VerticalScrollBarVisibility="Auto">`.
- In split-column layouts (e.g. `BatchImagePage`, `CompressorPage`, `ScholarKitPage`), wrap the left options/controls panel in its own `ScrollViewer` so controls remain accessible on smaller screens while the right queue/preview panel expands independently.

### 2.4 Text Wrapping & Truncation
- Titles and short headers: Use `TextTrimming="CharacterEllipsis"` and `MaxLines="1"`.
- Body descriptions, status banners, and multi-line notes: Use `TextWrapping="Wrap"`. Never leave multi-line text unwrapped inside horizontally unbounded containers.

---

## 3. MVVM Toolkit & ViewModel Guidelines

### 3.1 ViewModel Responsibilities
- ViewModels inherit from `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`.
- **Zero UIElement References**: ViewModels must never reference `Microsoft.UI.Xaml.UIElement`, `Window`, `Page`, `Frame`, `Control`, or XAML brush types.
- All UI communication occurs via data-bound properties, `[RelayCommand]`, and constructor-injected service contracts.

### 3.2 Property Declarations
- Use field-backed `[ObservableProperty]` attributes:
  ```csharp
  [ObservableProperty]
  private string _statusMessage = string.Empty;

  [ObservableProperty]
  private bool _isProcessing;
  ```
- *Rationale*: Field-backed properties are fully supported across Roslyn source generator versions and ensure reliable WinRT AOT marshalling.
- To react to property changes, implement the generated partial methods (`partial void OnStatusMessageChanged(string value)`) rather than overriding property setters manually.

### 3.3 Command Usage & Async Execution
- Decorate command methods with `[RelayCommand]`.
- For asynchronous operations, return `Task` and accept a `CancellationToken`:
  ```csharp
  [RelayCommand(IncludeCancelCommand = true)]
  public async Task ProcessQueueAsync(CancellationToken ct)
  {
      IsProcessing = true;
      try
      {
          await _service.ExecuteAsync(ct);
      }
      catch (OperationCanceledException)
      {
          StatusMessage = "Operation cancelled.";
      }
      catch (Exception ex)
      {
          StatusMessage = $"Error: {ex.Message}";
      }
      finally
      {
          IsProcessing = false;
      }
  }
  ```
- Setting `IncludeCancelCommand = true` automatically generates `ProcessQueueCancelCommand` which can be bound directly to a "Cancel" button in XAML.

### 3.4 Page Lifecycle & Memory Leak Prevention
- When Pages connect to singleton ViewModels:
  ```csharp
  // IN PAGE CODE-BEHIND:
  public sealed partial class MyPage : Page
  {
      public MyViewModel ViewModel { get; } = App.GetService<MyViewModel>();

      public MyPage()
      {
          InitializeComponent();
          DataContext = this;
          Loaded += MyPage_Loaded;
          Unloaded += MyPage_Unloaded;
      }

      private void MyPage_Loaded(object sender, RoutedEventArgs e)
      {
          ViewModel.PropertyChanged += OnViewModelPropertyChanged;
      }

      private void MyPage_Unloaded(object sender, RoutedEventArgs e)
      {
          // CRITICAL: Unhook delegate to prevent permanently leaking this Page in memory!
          ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
      }
  }
  ```

---

## 4. Styling, Brushes & Theme Resources

### 4.1 Strict `{ThemeResource}` Usage
- Always reference system theme brushes rather than hardcoding hexadecimal colors.
- Essential ThemeResource Keys:
  - Page Background: `{ThemeResource ApplicationPageBackgroundThemeBrush}`
  - Card Fill: `{ThemeResource CardBackgroundFillColorDefaultBrush}`
  - Card Stroke / Border: `{ThemeResource CardStrokeColorDefaultBrush}`
  - Primary Text: `{ThemeResource TextFillColorPrimaryBrush}`
  - Secondary Text: `{ThemeResource TextFillColorSecondaryBrush}`
  - Disabled Text: `{ThemeResource TextFillColorDisabledBrush}`
  - Accent Primary: `{ThemeResource AccentFillColorDefaultBrush}`
  - Accent Text: `{ThemeResource AccentTextFillColorPrimaryBrush}`
  - Layer Fill: `{ThemeResource LayerFillColorDefaultBrush}`
  - Divider: `{ThemeResource DividerStrokeColorDefaultBrush}`

### 4.2 Corner Radii & Elevation
- Controls (Buttons, TextBoxes, ComboBoxes): `CornerRadius="8"` or `CornerRadius="6"`.
- Container Cards (Borders, Panels): `CornerRadius="12"` or `CornerRadius="14"`.
- Floating Badges / Status Pills: `CornerRadius="20"`.

---

## 5. UI Feedback: Loading, Empty, Error & Toast States

### 5.1 Loading States
- Every async operation must bind its active state to a `ProgressRing`:
  ```xml
  <ProgressRing IsActive="{Binding ViewModel.IsProcessing}" Width="16" Height="16"
                Visibility="{Binding ViewModel.IsProcessing, Converter={StaticResource BoolToVisibilityConverter}}"/>
  ```
- Disable action buttons during processing:
  ```xml
  <Button Content="Start Processing"
          Command="{Binding ViewModel.StartCommand}"
          IsEnabled="{Binding ViewModel.IsProcessing, Converter={StaticResource InvertedBoolConverter}}"/>
  ```

### 5.2 Empty States
- Every collection view (`ListView`, `ItemsRepeater`, file queue) must provide an empty state container displayed when item count equals 0:
  - Neutral illustration or glyph (`FontIcon FontSize="36"`)
  - Informative title (e.g. "No Resumes in Library", "Queue is Empty")
  - Subtitle explaining the action needed
  - Prominent Call-to-Action button (e.g. "Select Files", "Create Resume")

### 5.3 Non-Destructive Errors & Inline Validation
- Failed operations must never crash or wipe existing user input.
- Render error messages inline using red/amber text with an alert icon (`FontIcon Glyph="&#xE783;" Foreground="#E53935"`).
- Dismiss uncommitted dialog changes cleanly when Cancel or Escape is pressed.

### 5.4 In-App Notifications (`InfoBarHost`)
- Use WinUI 3 `InfoBar` controls for high-visibility user feedback:
  ```xml
  <InfoBar IsOpen="{Binding ViewModel.HasAlert}"
           Severity="{Binding ViewModel.AlertSeverity}"
           Title="{Binding ViewModel.AlertTitle}"
           Message="{Binding ViewModel.AlertMessage}"
           IsClosable="True"/>
  ```

---

## 6. Native Windows Integration

### 6.1 File & Folder Pickers
- Always invoke native pickers through `NativeFilePickerHelper`.
- Never call `Windows.Storage.Pickers` directly without initializing `InitializeWithWindow.Initialize(picker, hwnd)`.
- Pass proper filter strings without combining wildcards (`*.*` or `*`) with explicit extensions in the same WinRT filter group.

### 6.2 Global Drag & Drop
- Root drag/drop is captured by `MainWindow.Content` and forwarded through `ShellView.HandleDroppedFiles(IReadOnlyList<IStorageItem> items)`.
- Handlers in individual pages must:
  1. Inspect `e.DataView.Contains(StandardDataFormats.StorageItems)` in `DragOver`.
  2. Set `e.AcceptedOperation = DataPackageOperation.Copy`.
  3. Set `e.DragUIOverride.Caption` to an informative action label (e.g. "Drop to extract text with OCR").
  4. Offload file reads to background tasks.

### 6.3 Keyboard Accelerators
- Global accelerators are registered on `MainWindow.Content.KeyboardAccelerators`:
  - `Ctrl+K`: Opens `CommandPaletteDialog`.
  - `Ctrl+\`: Toggles `NavigationView.IsPaneOpen`.
  - `Escape`: Closes active overlays/dialogs.
- In-page accelerators:
  - `Space`: Flips cards in `FlashcardsPage`.
  - `Enter`: Submits active query in `ScholarKitPage` RAG chat.

### 6.4 Accessibility (a11y)
- Every interactive control must declare an explicit `AutomationProperties.Name`:
  ```xml
  <Button x:Name="RefreshTelemetryButton"
          AutomationProperties.Name="Refresh System Telemetry"
          Command="{Binding ViewModel.RefreshTelemetryCommand}">
      <FontIcon Glyph="&#xE72C;"/>
  </Button>
  ```
- Interactive targets must measure at least **32x32 DIP**.
- Maintain a minimum contrast ratio of **4.5:1** (WCAG AA) across both Light and Dark themes.

---

## 7. Quality Assurance Gates for New Features

Every PR or feature commit must pass the 5-layer QA gate:
1. **Logic Test**: Execute `Axora.Desktop.Tests.exe` (must exit 0 with 0 failures).
2. **UI Automation**: Execute `scripts/qa/test-winui-product-flows.ps1` (asserts control patterns and state).
3. **Visual Audit**: Verify alignment, padding, no clipping, and capture screenshot to `docs/qa/screenshots/`.
4. **Accessibility Audit**: Check tab order, automation names, and high contrast.
5. **Negative Path Test**: Exercise null inputs, corrupt files, and cancellations.

---

## 8. Native Foundation Hardening Rules (Phase W1)

### 8.1 In-App Notifications (`INotificationService`)
- Never use Win32 `MessageBox`, native message dialog popups, or OS system tray balloon tips for regular in-app operational feedback.
- Inject `INotificationService` into ViewModels or resolve via `App.GetService<INotificationService>()`.
- Dispatch notifications via:
  ```csharp
  _notificationService.Show("Conversion Succeeded", "All 5 documents converted to PDF.", InfoBarSeverity.Success, TimeSpan.FromSeconds(5));
  ```
- `ShellView.xaml` hosts the central `InfoBar` bound to `NotificationService`. Reusable dismiss timers must be cleaned up to prevent closure delegate leaks.

### 8.2 Live Theme & Accent Resource Invariants
- Runtime theme switching MUST be performed through `IThemeService`.
- Setting `ElementTheme.Light` or `ElementTheme.Dark` on `MainWindow.Content` is required to propagate changes through the visual tree.
- Dynamic accent changes must update both `AccentColorBrush` and `AccentColorTertiaryBrush` in `Application.Current.Resources` and toggle `RequestedTheme` or invalidate visuals to force XAML `{ThemeResource}` recalculation across all cached pages.

### 8.3 Guaranteed Page Unload & Timer Teardown
- Because ViewModels are Singletons, any strong event subscriptions (`ViewModel.PropertyChanged += ...` or `ViewModel.Document.PropertyChanged += ...`) MUST be balanced by unhooking in `Page.Unloaded`.
- DispatcherTimers (e.g., search debouncers, auto-save triggers) must be stopped in `Page.Unloaded`:
  ```csharp
  private void OnPageUnloaded(object sender, RoutedEventArgs e)
  {
      _debounceTimer.Stop();
      UnhookDocument();
  }
  ```

### 8.4 External Process & Peripheral Defense
- Any external process invocation (such as `magick.exe`) must verify executable presence on PATH before execution.
- If the binary is missing, provide automated fallback to native Windows APIs (e.g. WIC) and notify the user via `INotificationService` or job status.
- Dynamic COM wrappers (WIA scanner items, property enumerators) must be released in guaranteed `finally` blocks using `Marshal.ReleaseComObject()` to prevent hardware driver locks (0x80210006 `WIA_ERROR_BUSY`).

### 8.5 Stream Decoupling & Position Offset Preservation
- Services consuming caller-provided streams (such as `WinRtOcrService`) must clone or copy the payload to an isolated `InMemoryRandomAccessStream` to prevent disposing caller streams.
- If the input stream is seekable (`CanSeek == true`), its original `.Position` offset MUST be restored in a `finally` block before returning.

### 8.6 Process Termination & Thread-Safe Shutdown Synchronization
- Window closure (`MainWindow.Closed`) and process termination (`Program.Main` `finally`) must synchronize on a shared `App.EnsureShutdownAsync()` task.
- Background loops (`P2pSyncService`, socket listeners, telemetry tickers) must be cancelled via `CancellationTokenSource`, and `AppHost.StopAsync()` must be awaited before process exit to prevent lingering Task Manager zombies.

---

## 9. Extension & Download Manager Guidelines (Phase W1.5)

### 9.1 The Golden Rule: No Silent Updates
- AXORA may detect that updates exist and display status notices, but **MUST NEVER** automatically or silently download, install, or update external dependencies in the background.
- Every install, update, repair, reinstall, or clean reinstall requires explicit user interaction and confirmation.

### 9.2 Consumer Feature Page Integration (`DependencyStatusControl`)
- Feature pages that rely on external dependencies (e.g. `BatchImagePage`, future `ConverterPage`) must not re-implement dependency status tracking, update checks, or download management.
- Instead, instantiate `<controls:DependencyStatusControl>` in XAML:
  ```xml
  <controls:DependencyStatusControl
      DependencyId="imagemagick"
      IsOptional="True"
      FeatureName="Batch Image Studio"
      FallbackMessage="Windows Hardware WIC GPU engine active. Install ImageMagick for advanced compression."/>
  ```
- **Optional Dependencies**: When `IsOptional="True"`, the page displays a subtle warning InfoBar with fallback details and an "Open Download Manager" action button. The page remains fully operational via native fallbacks.
- **Mandatory Dependencies**: When `IsOptional="False"`, the page displays an error InfoBar and disables the specific dependency-dependent action button, with an "Open Download Manager" button that deep-links directly to `DownloadManagerPage` with the dependency card highlighted.

### 9.3 Extension Registration (`IExtensionRegistry`)
- All managed dependencies are registered via `IExtensionRegistry.RegisterExtension(ExtensionModel)` in service startup (`ExtensionRegistry.cs`).
- Model requirements:
  - `Id`: Lowercase alphanumeric identifier (e.g. `"imagemagick"`, `"pandoc"`).
  - `InstallSource`: Strictly HTTPS URI from an approved vendor distribution domain.
  - `ExecutableName`: Name of binary probe (e.g. `"magick.exe"`).
  - `ProbeArguments`: Version query flag (e.g. `"-version"`).
  - `ExpectedChecksumSha256`: Vendor SHA-256 hash for verification.

### 9.4 Clean Reinstall Semantics & Data Safety
- Clean Reinstall is a specialized recovery workflow for damaged or corrupted dependencies.
- **Permitted Operations**:
  - Remove extension binary directory in `%LOCALAPPDATA%\Axora\Extensions\<id>\`
  - Purge extension download cache in `%LOCALAPPDATA%\Axora\ExtensionCache\<id>\`
  - Delete temporary staging installers
  - Clear extension-specific configuration entries
- **Strictly Prohibited**:
  - NEVER delete user documents, projects, or exported files
  - NEVER clear settings files (`%APPDATA%\Axora\settings.json`)
  - NEVER delete files belonging to other extensions
- Clean Reinstall must present an explicit ContentDialog explaining precisely what will and will not be removed before proceeding.

### 9.5 Download Cache Subsystem (`IExtensionCacheService`)
- All downloads and temporary installers are isolated in `%LOCALAPPDATA%\Axora\ExtensionCache\`.
- Use `IExtensionCacheService` to query cache size and perform purges.
- Cache cleanup operations must never delete files outside the controlled cache root.


