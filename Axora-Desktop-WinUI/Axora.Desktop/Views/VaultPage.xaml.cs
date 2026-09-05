using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Axora.Desktop.Helpers;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop.Views;

public sealed partial class VaultPage : Page
{
    public VaultViewModel ViewModel { get; } = App.GetService<VaultViewModel>();

    public VaultPage()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += VaultPage_Loaded;
        Unloaded += VaultPage_Unloaded;
    }

    private void VaultPage_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        if (VaultPasswordBox != null && VaultPasswordBox.Password != ViewModel.Password)
        {
            VaultPasswordBox.Password = ViewModel.Password;
        }
    }

    private void VaultPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VaultViewModel.Password))
        {
            if (VaultPasswordBox != null && VaultPasswordBox.Password != ViewModel.Password)
            {
                VaultPasswordBox.Password = ViewModel.Password;
            }
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb)
        {
            ViewModel.Password = pb.Password;
        }
    }

    private async void BrowseInput_Click(object sender, RoutedEventArgs e)
    {
        var files = await NativeFilePickerHelper.PickFilesAsync(
            title: "Select Files to Encrypt or Decrypt",
            filter: "All Files (*.*)\0*.*\0Axora Vault Archives (*.axvault)\0*.axvault\0",
            allowMultiple: true);

        if (files != null && files.Count > 0)
        {
            foreach (var file in files)
            {
                ViewModel.AddFileToQueue(file);
            }
        }
    }

    public void SetInputFromDrop(IReadOnlyList<IStorageItem> items)
    {
        foreach (var item in items.OfType<StorageFile>())
        {
            ViewModel.AddFileToQueue(item.Path);
        }
    }
}
