using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Axora.Desktop.Helpers;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop.Views;

public sealed partial class BatchImagePage : Page
{
    public BatchImageViewModel ViewModel { get; } = App.GetService<BatchImageViewModel>();

    public BatchImagePage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private async void BrowseImages_Click(object sender, RoutedEventArgs e)
    {
        var files = await NativeFilePickerHelper.PickFilesAsync(
            title: "Select Images to Batch Process",
            filter: "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.webp;*.heic;*.raw;*.gif;*.svg)\0*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.webp;*.heic;*.raw;*.gif;*.svg\0All Files (*.*)\0*.*\0",
            allowMultiple: true);

        if (files != null && files.Count > 0)
        {
            ViewModel.AddFiles(files);
        }
    }

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var folderPath = await NativeFilePickerHelper.PickFolderAsync("Select Image Directory to Ingest");
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            await ViewModel.AddFolderAsync(folderPath);
        }
    }

    private async void ChangeOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        var folderPath = await NativeFilePickerHelper.PickFolderAsync("Select Output Destination Folder");
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            ViewModel.SetCustomOutputDirectory(folderPath);
        }
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var filePaths = new List<string>();

            foreach (var item in items)
            {
                if (item is StorageFolder folder)
                {
                    await ViewModel.AddFolderAsync(folder.Path);
                }
                else if (item is StorageFile file)
                {
                    filePaths.Add(file.Path);
                }
            }

            if (filePaths.Count > 0)
            {
                ViewModel.AddFiles(filePaths);
            }
        }
    }
}
