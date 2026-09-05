using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Axora.Desktop.Services.Contracts;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop.Views;

/// <summary>
/// NavigationView shell — routes page selections to the ContentFrame and
/// exposes drag-drop / command palette surface APIs called by MainWindow.
/// </summary>
public sealed partial class ShellView : UserControl
{
    public ShellViewModel ViewModel { get; } = App.GetService<ShellViewModel>();
    public Controls.FileDropZoneOverlay DropOverlay { get; private set; } = null!;
    public Controls.CommandPaletteDialog CommandPalette { get; private set; } = null!;

    private readonly INotificationService _notificationService = App.GetService<INotificationService>();
    private DispatcherTimer? _notificationTimer;

    public ShellView()
    {
        InitializeComponent();
        DataContext = this;

        // Resolve named controls safely
        NavView ??= FindName("NavView") as NavigationView;
        ContentFrame ??= FindName("ContentFrame") as Frame;
        OverlayContainer ??= FindName("OverlayContainer") as Grid;
        GlobalNotificationInfoBar ??= FindName("GlobalNotificationInfoBar") as InfoBar;

        if (GlobalNotificationInfoBar != null)
        {
            GlobalNotificationInfoBar.Closed += (_, _) => _notificationTimer?.Stop();
        }

        _notificationService.NotificationRequested += OnNotificationRequested;
        _notificationService.DismissRequested += OnNotificationDismissRequested;

        DropOverlay = new Controls.FileDropZoneOverlay { Visibility = Visibility.Collapsed };
        OverlayContainer?.Children.Add(DropOverlay);

        CommandPalette = new Controls.CommandPaletteDialog();
        OverlayContainer?.Children.Add(CommandPalette);

        // Synchronize CommandPalette IsOpen with ViewModel
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.IsCommandPaletteOpen))
            {
                CommandPalette.IsOpen = ViewModel.IsCommandPaletteOpen;
            }
        };

        if (NavView != null)
        {
            NavView.Loaded += NavView_Loaded;
            NavView.ItemInvoked += NavView_ItemInvoked;
            NavView.SelectionChanged += NavView_SelectionChanged;
        }

        if (ContentFrame != null)
        {
            ContentFrame.Navigated += ContentFrame_Navigated;
            ContentFrame.NavigationFailed += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[NAVIGATION FAILED] Target: {e.SourcePageType}, Error: {e.Exception}");
                Console.WriteLine($"[NAVIGATION FAILED] Target: {e.SourcePageType}, Error: {e.Exception}");
            };
        }
    }

    private void OnNotificationRequested(object? sender, NotificationEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            GlobalNotificationInfoBar ??= FindName("GlobalNotificationInfoBar") as InfoBar;
            if (GlobalNotificationInfoBar == null) return;

            GlobalNotificationInfoBar.Title = e.Title ?? string.Empty;
            GlobalNotificationInfoBar.Message = e.Message;
            GlobalNotificationInfoBar.Severity = e.Severity switch
            {
                NotificationSeverity.Informational => InfoBarSeverity.Informational,
                NotificationSeverity.Success => InfoBarSeverity.Success,
                NotificationSeverity.Warning => InfoBarSeverity.Warning,
                NotificationSeverity.Error => InfoBarSeverity.Error,
                _ => InfoBarSeverity.Informational
            };
            GlobalNotificationInfoBar.IsOpen = true;

            if (_notificationTimer == null)
            {
                _notificationTimer = new DispatcherTimer();
                _notificationTimer.Tick += (s, args) =>
                {
                    _notificationTimer.Stop();
                    if (GlobalNotificationInfoBar != null)
                    {
                        GlobalNotificationInfoBar.IsOpen = false;
                    }
                };
            }
            else
            {
                _notificationTimer.Stop();
            }

            if (e.Duration.HasValue && e.Duration.Value > TimeSpan.Zero)
            {
                _notificationTimer.Interval = e.Duration.Value;
                _notificationTimer.Start();
            }
        });
    }

    private void OnNotificationDismissRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _notificationTimer?.Stop();
            if (GlobalNotificationInfoBar != null)
            {
                GlobalNotificationInfoBar.IsOpen = false;
            }
        });
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        NavView ??= FindName("NavView") as NavigationView;
        NavDashboard ??= FindName("NavDashboard") as NavigationViewItem;
        ContentFrame ??= FindName("ContentFrame") as Frame;

        if (NavView != null && NavDashboard != null)
            NavView.SelectedItem = NavDashboard;

        NavigateTo("Dashboard");
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            NavigateTo("Settings");
            return;
        }

        if (args.InvokedItemContainer is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateTo(tag);
        }
        else if (args.InvokedItem is string title)
        {
            var matched = ShellViewModel.PageMap.FirstOrDefault(
                kvp => kvp.Value.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(matched.Key))
                NavigateTo(matched.Key);
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavigateTo("Settings");
            return;
        }

        if (args.SelectedItemContainer is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateTo(tag);
        }
        else if (args.SelectedItem is NavigationViewItem selItem && selItem.Tag is string selTag)
        {
            NavigateTo(selTag);
        }
        else if (args.SelectedItem is string title)
        {
            var matched = ShellViewModel.PageMap.FirstOrDefault(
                kvp => kvp.Value.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(matched.Key))
                NavigateTo(matched.Key);
        }
    }

    public void NavigateTo(string pageTag, object? parameter = null)
    {
        if (!ShellViewModel.PageMap.TryGetValue(pageTag, out var pageInfo)) return;

        ContentFrame ??= FindName("ContentFrame") as Frame;
        if (ContentFrame == null) return;

        // Skip if already on the exact target page type and no parameter specified
        if (ContentFrame.CurrentSourcePageType == pageInfo.PageType && parameter == null) return;

        // Top-level navigation: clear backstack to prevent page accumulation & memory leaks
        ContentFrame.BackStack.Clear();

        ViewModel.CurrentPageTitle = pageInfo.Title;
        ContentFrame.Navigate(pageInfo.PageType, parameter,
            new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        NavView ??= FindName("NavView") as NavigationView;
        if (NavView == null) return;

        // Auto-minimize navigation rail when opening the Resume Editor to give maximum editing space
        if (e.SourcePageType == typeof(ResumeStudioPage))
        {
            ViewModel.IsPaneOpen = false;
        }
        else if (e.SourcePageType == typeof(ResumeStudioDashboardPage))
        {
            ViewModel.IsPaneOpen = true;
        }

        // Map the Resume Studio editor page back to the "ResumeStudio" nav item
        // so the nav rail stays highlighted while the editor is open.
        var effectivePageType = e.SourcePageType == typeof(ResumeStudioPage)
            ? typeof(ResumeStudioDashboardPage)
            : e.SourcePageType;

        // Synchronize Header Title with active page
        var matched = ShellViewModel.PageMap.FirstOrDefault(kvp => kvp.Value.PageType == effectivePageType);
        if (!string.IsNullOrEmpty(matched.Key))
        {
            ViewModel.CurrentPageTitle = matched.Value.Title;
        }

        if (effectivePageType == typeof(SettingsPage))
        {
            NavSettings ??= FindName("NavSettings") as NavigationViewItem;
            if (NavSettings != null && (NavigationViewItem?)NavView.SelectedItem != NavSettings)
            {
                NavView.SelectedItem = NavSettings;
            }
            return;
        }

        var allItems = NavView.MenuItems.OfType<NavigationViewItem>()
            .Concat(NavView.FooterMenuItems.OfType<NavigationViewItem>());

        foreach (var item in allItems)
        {
            if (ShellViewModel.PageMap.TryGetValue(item.Tag?.ToString() ?? "", out var info)
                && info.PageType == effectivePageType)
            {
                if ((NavigationViewItem?)NavView.SelectedItem != item)
                {
                    NavView.SelectedItem = item;
                }
                break;
            }
        }
    }

    // ── Command Palette API (called from MainWindow keyboard accelerator) ──────

    public void OpenCommandPalette() => ViewModel.IsCommandPaletteOpen = true;
    public void TogglePane() => ViewModel.IsPaneOpen = !ViewModel.IsPaneOpen;

    // ── Drag-and-Drop API (called from MainWindow root drop handlers) ─────────

    public void ShowDropOverlay()
    {
        if (DropOverlay != null) DropOverlay.Visibility = Visibility.Visible;
    }

    public void HideDropOverlay()
    {
        if (DropOverlay != null) DropOverlay.Visibility = Visibility.Collapsed;
    }

    public void HandleDroppedFiles(IReadOnlyList<IStorageItem> items)
    {
        ContentFrame ??= FindName("ContentFrame") as Frame;
        if (ContentFrame?.Content is ScholarKitPage scholarKit)
        {
            _ = scholarKit.ProcessDroppedFilesAsync(items);
        }
        else if (ContentFrame?.Content is VaultPage vault)
        {
            vault.SetInputFromDrop(items);
        }
        else if (ContentFrame?.Content is UniversalConverterPage converter)
        {
            var filePaths = items.OfType<StorageFile>().Select(f => f.Path).ToList();
            if (filePaths.Count > 0)
            {
                converter.ViewModel.AddFiles(filePaths);
            }
        }
    }
}
