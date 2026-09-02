using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Axora.Desktop.Helpers;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; } = App.GetService<SettingsViewModel>();

    public SettingsPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void Accent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            ViewModel.AccentColor = hex;
        }
    }

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var folderPath = await NativeFilePickerHelper.PickFolderAsync("Select Download / Export Directory");
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            ViewModel.DownloadDirectory = folderPath;
        }
    }
}
