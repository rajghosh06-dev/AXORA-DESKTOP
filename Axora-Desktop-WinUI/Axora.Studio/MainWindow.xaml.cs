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
    private readonly StudioReadAloudSession _readAloud;
    private readonly Lazy<ResumeViewModel> _resume;
    public ShellViewModel ViewModel { get; }
    public MainWindow(ShellViewModel shell, SettingsViewModel settings, StudioDiagnostics log,
        Func<FlashcardsViewModel> flashcardsFactory, StudioExportSession exports, StudioReadAloudSession readAloud, Lazy<ResumeViewModel> resume)
    {
        (ViewModel, _settings, _log) = (shell, settings, log);
        _flashcards = new(flashcardsFactory);
        _exports = exports;
        _readAloud = readAloud;
        _resume = resume;
        InitializeComponent();
        Title = AppWindow.Title = "AXORA Studio";
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1060, 720));
        ViewModel.PropertyChanged += OnRouteChanged;
        _settings.ThemeSaved += ApplyTheme;
        Closed += OnClosed;
        ViewModel.DepartureGuard = GuardRouteAsync;
        ShowRoute(ViewModel.SelectedRoute);
    }
    private async void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        StudioRoute? requested = args.InvokedItem is StudioRouteEntry entry ? entry.Route
            : args.InvokedItemContainer?.DataContext is StudioRouteEntry context ? context.Route
            : args.InvokedItemContainer?.Tag is StudioRoute route ? route : null;
        if (requested is not null) await ViewModel.NavigateAsync(requested.Value);
        Navigation.SelectedItem = StudioRoutes.Resolve(ViewModel.SelectedRoute);
    }
    private void OnRouteChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ShellViewModel.SelectedRoute)) ShowRoute(ViewModel.SelectedRoute);
    }
    private void ShowRoute(StudioRoute route)
    {
        var entry = StudioRoutes.Resolve(route);
        DepartFlashcards(route, _flashcards);
        bool firstFlashcards = route == StudioRoute.Flashcards && !_flashcards.IsValueCreated;
        var activation = System.Diagnostics.Stopwatch.StartNew();
        var flashcards = ResolveFlashcards(route, _flashcards);
        var resume = ResolveResume(route, _resume);
        if (resume is not null)
        {
            resume.Configure(() => WinRT.Interop.WindowNative.GetWindowHandle(this), DepartureDecisionAsync, ImportConsentAsync,
                ViewModel.NavigateAsync, action => { if (DispatcherQueue.HasThreadAccess) action(); else DispatcherQueue.TryEnqueue(() => action()); });
            if (route == StudioRoute.ResumeEditor) resume.RebuildEditor();
        }
        Page page = entry.PageType == typeof(HomePage) ? new HomePage()
            : entry.PageType == typeof(SettingsPage) ? new SettingsPage(_settings)
            : entry.PageType == typeof(AboutPage) ? new AboutPage()
            : entry.PageType == typeof(FlashcardsPage) ? ExportPage(flashcards!)
            : entry.PageType == typeof(ResumeDashboardPage) ? new ResumeDashboardPage(resume!)
            : entry.PageType == typeof(ResumeEditorPage) ? new ResumeEditorPage(resume!)
            : throw new InvalidOperationException("The route has no Studio page factory.");
        RoutedEventHandler? loaded = null;
        loaded = (_, _) =>
        {
            page.Loaded -= loaded;
            _log.Write($"Route rendered: {entry.Label}; page={page.GetType().Name}; flashcardsCreated={_flashcards.IsValueCreated}; exportCreated={_exports.IsCreated}; readAloudCreated={_readAloud.IsCreated}; resumeCreated={_resume.IsValueCreated}");
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
    public static void DepartFlashcards(StudioRoute nextRoute, Lazy<FlashcardsViewModel> feature)
    {
        if (nextRoute != StudioRoute.Flashcards && feature.IsValueCreated) feature.Value.OnFlashcardsDeparture();
    }
    public static ResumeViewModel? ResolveResume(StudioRoute route, Lazy<ResumeViewModel> feature)
    { _ = StudioRoutes.Resolve(route); return route is StudioRoute.ResumeDashboard or StudioRoute.ResumeEditor ? feature.Value : null; }
    private Task<bool> GuardRouteAsync(StudioRoute route) => !_resume.IsValueCreated || route == StudioRoute.ResumeEditor
        ? Task.FromResult(true) : _resume.Value.GuardDepartureAsync();
    public Task<bool> PrepareCloseAsync() => _resume.IsValueCreated ? _resume.Value.PrepareCloseAsync() : Task.FromResult(true);
    private async Task<ResumeDeparture> DepartureDecisionAsync()
    {
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "Unsaved Resume changes",
            Content = "Save before leaving? Discard restores the last saved content. Cancel keeps editing.",
            PrimaryButtonText = "Save", SecondaryButtonText = "Discard", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
        var answer = await dialog.ShowAsync();
        return answer switch { ContentDialogResult.Primary => ResumeDeparture.Save, ContentDialogResult.Secondary => ResumeDeparture.Discard, _ => ResumeDeparture.Cancel };
    }
    private async Task<bool> ImportConsentAsync(ResumeDocument document)
    {
        int count = document.Education.Length + document.Experiences.Length + document.SkillCategories.Length + document.Projects.Length
            + document.Certifications.Length + document.Achievements.Length + document.Responsibilities.Length;
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "Import a managed copy?",
            Content = $"{document.ResumeTitle}\n{count} entries\n\nStudio will preserve the selected bytes, create its own copy and leave the source untouched.",
            PrimaryButtonText = "Import Copy", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
    private FlashcardsPage ExportPage(FlashcardsViewModel viewModel)
    {
        viewModel.ConfigureExport(_exports);
        viewModel.ConfigureReadAloud(_readAloud, action => DispatcherQueue.TryEnqueue(() => action()), _log.Write);
        return new(viewModel, () => WinRT.Interop.WindowNative.GetWindowHandle(this));
    }
    public void ShowExportClosePending()
    {
        Title = AppWindow.Title = "AXORA Studio — Finishing export before closing";
        if (_flashcards.IsValueCreated) _flashcards.Value.ExportStatus = "Finishing export before closing.";
    }
    public void ShowFileClosePending()
    {
        if (_exports.IsActive) ShowExportClosePending();
        if (_resume.IsValueCreated && _resume.Value.Session.IsActive) Title = AppWindow.Title = "AXORA Studio — Finishing save before closing";
    }
    public void ClearClosePendingStatus() => Title = AppWindow.Title = "AXORA Studio";
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
        ViewModel.DepartureGuard = null;
    }
}
