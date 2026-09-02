using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Axora.Desktop.ViewModels;

/// <summary>
/// Shell navigation coordinator. Tracks the active page and drives the NavigationView selection state.
/// FIX-0: Using field-backed [ObservableProperty] (proven working pattern with MVVMTK 8.4 + .NET 9).
/// The partial property approach requires Roslyn 4.12+ in the source generator host.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentPageTitle = "Dashboard";

    [ObservableProperty]
    private bool _isPaneOpen = true;

    [ObservableProperty]
    private bool _isCommandPaletteOpen;

    /// <summary>
    /// Page tag → (Type, Title) mapping used by ShellView to navigate the Frame.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (Type PageType, string Title)> PageMap =
        new Dictionary<string, (Type, string)>
        {
            ["Dashboard"]           = (typeof(Views.DashboardPage),              "Dashboard"),
            ["ScholarKit"]          = (typeof(Views.ScholarKitPage),             "Scholar Kit"),
            ["ResumeStudio"]        = (typeof(Views.ResumeStudioDashboardPage),  "Resume Studio"),
            ["ResumeStudioEditor"]  = (typeof(Views.ResumeStudioPage),           "Resume Studio — Editor"),
            ["BatchImage"]          = (typeof(Views.BatchImagePage),             "Batch Image Studio"),
            ["Compressor"]          = (typeof(Views.CompressorPage),             "Intelligent Compressor"),
            ["Vault"]               = (typeof(Views.VaultPage),                  "Encrypted Vault"),
            ["Flashcards"]          = (typeof(Views.FlashcardsPage),             "Flashcard Studio"),
            ["MobileLink"]          = (typeof(Views.MobileLinkPage),             "Mobile Link"),
            ["Settings"]            = (typeof(Views.SettingsPage),               "Settings"),
        };

    [RelayCommand]
    private void OpenCommandPalette() => IsCommandPaletteOpen = true;

    [RelayCommand]
    private void CloseCommandPalette() => IsCommandPaletteOpen = false;

    [RelayCommand]
    private void TogglePane() => IsPaneOpen = !IsPaneOpen;
}
