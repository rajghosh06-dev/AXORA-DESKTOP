using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
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

    public ShellView()
    {
        InitializeComponent();
        DataContext = this;

        // Resolve named controls safely
        NavView ??= FindName("NavView") as NavigationView;
        ContentFrame ??= FindName("ContentFrame") as Frame;
        OverlayContainer ??= FindName("OverlayContainer") as Grid;

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
        }

        if (ContentFrame != null)
        {
            ContentFrame.Navigated += ContentFrame_Navigated;
        }
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

    public void NavigateTo(string pageTag)
    {
        if (!ShellViewModel.PageMap.TryGetValue(pageTag, out var pageInfo)) return;

        ViewModel.CurrentPageTitle = pageInfo.Title;
        ContentFrame ??= FindName("ContentFrame") as Frame;
        if (ContentFrame == null) return;

        // Skip only if we are already on that exact page with no back stack.
        bool alreadyThere = ContentFrame.CurrentSourcePageType == pageInfo.PageType
                            && !ContentFrame.CanGoBack;
        if (!alreadyThere)
        {
            ContentFrame.Navigate(pageInfo.PageType, null,
                new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
        }
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
            if (NavView.SelectedItem != NavView.SettingsItem)
            {
                NavView.SelectedItem = NavView.SettingsItem;
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
    }
}
