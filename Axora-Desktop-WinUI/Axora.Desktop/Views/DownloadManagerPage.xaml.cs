using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Axora.Desktop.Models;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop.Views;

/// <summary>
/// Download & Extension Manager Page.
/// Enforces user-controlled updates and explicit confirmations for Clean Reinstall, Repair, and Update.
/// </summary>
public sealed partial class DownloadManagerPage : Page
{
    public DownloadManagerViewModel ViewModel { get; } = App.GetService<DownloadManagerViewModel>();

    public DownloadManagerPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string targetId && !string.IsNullOrWhiteSpace(targetId))
        {
            ViewModel.HighlightExtension(targetId);
        }
        else
        {
            ViewModel.HighlightExtension(string.Empty);
        }

        _ = ViewModel.InitializeAsync();
    }

    private void OnInstallClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ExtensionModel ext)
        {
            _ = ViewModel.InstallAsync(ext);
        }
    }

    private async void OnUpdateClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ExtensionModel ext) return;

        var dialog = new ContentDialog
        {
            Title = $"Update {ext.DisplayName}?",
            Content = $"Installed version: {ext.FormattedInstalledVersion}\n" +
                      $"Latest available:  {ext.FormattedLatestVersion}\n\n" +
                      "AXORA enforces explicit user approval and never performs silent background updates.\n\n" +
                      "Proceed with download and upgrade?",
            PrimaryButtonText = "Update Now",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.UpdateAsync(ext);
        }
    }

    private async void OnRepairClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ExtensionModel ext) return;

        var dialog = new ContentDialog
        {
            Title = $"Repair {ext.DisplayName}?",
            Content = "This operation will revalidate installed binaries and restore missing or damaged files while preserving your custom preferences.\n\n" +
                      "Proceed with repair?",
            PrimaryButtonText = "Repair",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.RepairAsync(ext);
        }
    }

    private async void OnReinstallClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ExtensionModel ext) return;

        var dialog = new ContentDialog
        {
            Title = $"Reinstall {ext.DisplayName}?",
            Content = $"This operation will remove and reinstall {ext.DisplayName} v{ext.LatestVersion}.\n\n" +
                      "Proceed with reinstallation?",
            PrimaryButtonText = "Reinstall",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.ReinstallAsync(ext);
        }
    }

    private async void OnCleanReinstallClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ExtensionModel ext) return;

        var dialog = new ContentDialog
        {
            Title = $"Confirm Clean Reinstallation — {ext.DisplayName}",
            Content = "A Clean Reinstall purges all caches and reinstalls the extension into a pristine state.\n\n" +
                      "WHAT WILL BE REMOVED:\n" +
                      $"• Staged installer packages and archives for {ext.DisplayName}\n" +
                      $"• Isolated AXORA cache directories for {ext.DisplayName}\n" +
                      $"• Installed binary files in the AXORA extension path\n" +
                      "• Broken or corrupted local metadata\n\n" +
                      "WHAT WILL NEVER BE TOUCHED:\n" +
                      "• Your user documents, exported images, and projects\n" +
                      "• Application preferences and personal settings\n" +
                      "• Unrelated extensions and third-party tools\n\n" +
                      "Do you want to proceed with Clean Reinstallation?",
            PrimaryButtonText = "Clean Reinstall",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.CleanReinstallAsync(ext);
        }
    }

    private async void OnRemoveClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ExtensionModel ext) return;

        var dialog = new ContentDialog
        {
            Title = $"Uninstall {ext.DisplayName}?",
            Content = $"Are you sure you want to remove {ext.DisplayName}?\n\n" +
                      "Any features requiring this extension will automatically switch to native fallbacks where available.",
            PrimaryButtonText = "Uninstall",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.RemoveAsync(ext);
        }
    }

    private async void OnViewDetailsClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ExtensionModel ext) return;

        var dialog = new ContentDialog
        {
            Title = $"{ext.DisplayName} — Extension Details",
            Content = $"Identifier: {ext.Id}\n" +
                      $"Status: {ext.FormattedStatus}\n" +
                      $"Installed Version: {ext.FormattedInstalledVersion}\n" +
                      $"Latest Version: {ext.FormattedLatestVersion}\n" +
                      $"Supported Architecture: {ext.SupportedArchitecture}\n" +
                      $"Supported OS: {ext.SupportedOs}\n" +
                      $"Executable: {ext.ExecutableName}\n" +
                      $"Detection Strategy: {ext.DetectionStrategy}\n" +
                      $"Capabilities: {ext.CapabilityInformation}\n" +
                      $"Required By: {ext.RequiredByDisplay}\n" +
                      $"Distribution Source: {ext.InstallSource}\n" +
                      $"Cache Size: {ext.FormattedCacheSize}",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }
}