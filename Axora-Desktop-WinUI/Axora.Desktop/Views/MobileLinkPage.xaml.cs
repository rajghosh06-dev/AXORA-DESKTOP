using System;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using WinRT.Interop;
using Axora.Desktop.Models;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop.Views;

public sealed partial class MobileLinkPage : Page
{
    public MobileLinkViewModel ViewModel { get; } = App.GetService<MobileLinkViewModel>();

    public MobileLinkPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AxoraDevice device)
        {
            ViewModel.DisconnectDevice(device);
        }
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Drop to push to paired phone";
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var file = items.OfType<StorageFile>().FirstOrDefault();
            if (file is not null)
            {
                await ViewModel.PushFileToMobileAsync(file.Path);
            }
        }
    }

    private async void BrowsePush_Click(object sender, RoutedEventArgs e)
    {
        var files = await Axora.Desktop.Helpers.NativeFilePickerHelper.PickFilesAsync(
            title: "Select File to Push to Paired Mobile Device",
            filter: "All Files (*.*)\0*.*\0",
            allowMultiple: false);

        if (files != null && files.Count > 0)
        {
            await ViewModel.PushFileToMobileAsync(files[0]);
        }
    }
}
