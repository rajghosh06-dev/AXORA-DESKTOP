using System.ComponentModel;
using Axora.Studio.Models;
using Axora.Studio.Services;
using Axora.Studio.ViewModels;
using Axora.Studio.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Axora.Studio;

public sealed partial class MainWindow : Window
{
    private readonly SettingsViewModel _settings;
    private readonly StudioDiagnostics _log;
    private readonly Lazy<FlashcardsViewModel> _flashcards;
    private readonly StudioExportSession _exports;
    public ShellViewModel ViewModel { get; }
    public MainWindow(ShellViewModel shell, SettingsViewModel settings, StudioDiagnostics log,
        Func<FlashcardsViewModel> flashcardsFactory, StudioExportSession exports)
    {
        (ViewModel, _settings, _log) = (shell, settings, log);
        _flashcards = new(flashcardsFactory);
        _exports = exports;
        InitializeComponent();
        Title = AppWindow.Title = "AXORA Studio";
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1060, 720));
        ViewModel.PropertyChanged += OnRouteChanged;
        _settings.ThemeSaved += ApplyTheme;
        Closed += OnClosed;
        ShowRoute(ViewModel.SelectedRoute);
    }
    private void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is StudioRouteEntry entry) ViewModel.Navigate(entry.Route);
        else if (args.InvokedItemContainer?.DataContext is StudioRouteEntry context) ViewModel.Navigate(context.Route);
        else if (args.InvokedItemContainer?.Tag is StudioRoute route) ViewModel.Navigate(route);
    }
    private void OnRouteChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ShellViewModel.SelectedRoute)) ShowRoute(ViewModel.SelectedRoute);
    }
    private void ShowRoute(StudioRoute route)
    {
        var entry = StudioRoutes.Resolve(route);
        bool firstFlashcards = route == StudioRoute.Flashcards && !_flashcards.IsValueCreated;
        var activation = System.Diagnostics.Stopwatch.StartNew();
        var flashcards = ResolveFlashcards(route, _flashcards);
        Page page = entry.PageType == typeof(HomePage) ? new HomePage()
            : entry.PageType == typeof(SettingsPage) ? new SettingsPage(_settings)
            : entry.PageType == typeof(AboutPage) ? new AboutPage()
            : entry.PageType == typeof(FlashcardsPage) ? ExportPage(flashcards!)
            : throw new InvalidOperationException("The route has no Studio page factory.");
        RoutedEventHandler? loaded = null;
        loaded = (_, _) =>
        {
            page.Loaded -= loaded;
            _log.Write($"Route rendered: {entry.Label}; page={page.GetType().Name}; flashcardsCreated={_flashcards.IsValueCreated}; exportCreated={_exports.IsCreated}");
            if (firstFlashcards) _log.Write($"Flashcards first render; elapsedMs={activation.ElapsedMilliseconds}");
        };
        page.Loaded += loaded;
        PageContent.Content = page;
        Navigation.SelectedItem = entry;
    }
    // Narrow production seam, also exercised without constructing a native Window in deterministic tests.
    public static FlashcardsViewModel? ResolveFlashcards(StudioRoute route, Lazy<FlashcardsViewModel> feature)
    {
        _ = StudioRoutes.Resolve(route);
        return route == StudioRoute.Flashcards ? feature.Value : null;
    }
    private FlashcardsPage ExportPage(FlashcardsViewModel viewModel)
    {
        viewModel.ConfigureExport(_exports);
        return new(viewModel, () => WinRT.Interop.WindowNative.GetWindowHandle(this));
    }
    public void ShowExportClosePending()
    {
        Title = AppWindow.Title = "AXORA Studio — Finishing export before closing";
        if (_flashcards.IsValueCreated) _flashcards.Value.ExportStatus = "Finishing export before closing.";
    }
    public void ApplyTheme(StudioTheme theme)
    {
        Root.RequestedTheme = theme switch
        {
            StudioTheme.System => ElementTheme.Default,
            StudioTheme.Light => ElementTheme.Light,
            StudioTheme.Dark => ElementTheme.Dark,
            _ => throw new ArgumentOutOfRangeException(nameof(theme))
        };
        _log.Write($"Theme applied: {theme}; requested={Root.RequestedTheme}");
    }
    private void OnClosed(object sender, WindowEventArgs args)
    {
        ViewModel.PropertyChanged -= OnRouteChanged;
        _settings.ThemeSaved -= ApplyTheme;
        Closed -= OnClosed;
    }
}
