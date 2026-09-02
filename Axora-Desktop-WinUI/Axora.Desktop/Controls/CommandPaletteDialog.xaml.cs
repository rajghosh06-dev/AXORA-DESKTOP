using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop.Controls;

/// <summary>
/// Global Ctrl+K command palette providing keyboard-driven navigation across all app features.
/// </summary>
public sealed partial class CommandPaletteDialog : UserControl
{
    private static readonly IReadOnlyList<string> AllCommands =
    [
        "Navigate → Dashboard",
        "Navigate → Scholar Kit",
        "Navigate → Resume Studio",
        "Navigate → Batch Image Studio",
        "Navigate → Intelligent Compressor",
        "Navigate → Encrypted Vault",
        "Navigate → Flashcard Studio",
        "Navigate → Mobile Link",
        "Navigate → Settings",
        "Vault: Encrypt File",
        "Vault: Decrypt File",
        "Mobile Link: Start Server",
        "Mobile Link: Stop Server",
        "Flashcards: New Deck",
        "Scholar Kit: Browse Image",
        "Settings: Reset to Defaults",
    ];

    public ObservableCollection<string> FilteredCommands { get; } = [.. AllCommands];

    private bool _isOpen;

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            _isOpen = value;
            Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            if (value)
            {
                FilteredCommands.Clear();
                foreach (var cmd in AllCommands) FilteredCommands.Add(cmd);
                SearchBox ??= FindName("SearchBox") as AutoSuggestBox;
                if (SearchBox != null)
                {
                    SearchBox.Text = string.Empty;
                    SearchBox.Focus(FocusState.Programmatic);
                }
            }
        }
    }

    public CommandPaletteDialog()
    {
        InitializeComponent();
        DataContext = this;

        SearchBox ??= FindName("SearchBox") as AutoSuggestBox;
        ResultsList ??= FindName("ResultsList") as ListView;
        DimOverlay ??= FindName("DimOverlay") as Microsoft.UI.Xaml.Shapes.Rectangle;

        if (SearchBox != null)
        {
            SearchBox.TextChanged += SearchBox_TextChanged;
            SearchBox.SuggestionChosen += SearchBox_SuggestionChosen;
        }

        if (ResultsList != null)
        {
            ResultsList.ItemClick += ResultsList_ItemClick;
        }

        if (DimOverlay != null)
        {
            DimOverlay.Tapped += Overlay_Tapped;
        }

        // Close on Escape key
        KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
                Close();
        };
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        var query = sender.Text.Trim().ToLowerInvariant();
        FilteredCommands.Clear();

        var matches = string.IsNullOrEmpty(query)
            ? AllCommands
            : AllCommands.Where(c => c.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var cmd in matches)
            FilteredCommands.Add(cmd);
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string cmd)
            ExecuteCommand(cmd);
    }

    private void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is string cmd)
            ExecuteCommand(cmd);
    }

    private void Overlay_Tapped(object sender, TappedRoutedEventArgs e)
    {
        Close();
    }

    private void ExecuteCommand(string command)
    {
        Close();

        // Route navigation commands to shell
        var pageTag = command switch
        {
            "Navigate → Dashboard"             => "Dashboard",
            "Navigate → Scholar Kit"           => "ScholarKit",
            "Navigate → Resume Studio"         => "ResumeStudio",
            "Navigate → Batch Image Studio"    => "BatchImage",
            "Navigate → Intelligent Compressor"=> "Compressor",
            "Navigate → Encrypted Vault"       => "Vault",
            "Navigate → Flashcard Studio"      => "Flashcards",
            "Navigate → Mobile Link"           => "MobileLink",
            "Navigate → Settings"              => "Settings",
            _ => null
        };

        if (pageTag is not null)
        {
            App.MainAppWindow?.ShellRoot.NavigateTo(pageTag);
        }
    }

    private void Close()
    {
        IsOpen = false;
        var shellVm = App.TryGetService<ShellViewModel>();
        if (shellVm is not null) shellVm.IsCommandPaletteOpen = false;
    }
}
