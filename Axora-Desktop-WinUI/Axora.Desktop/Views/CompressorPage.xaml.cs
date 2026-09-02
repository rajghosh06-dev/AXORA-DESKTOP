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

public sealed partial class CompressorPage : Page
{
    public CompressorViewModel ViewModel { get; } = App.GetService<CompressorViewModel>();

    public CompressorPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private async void BrowseDocs_Click(object sender, RoutedEventArgs e)
    {
        var files = await NativeFilePickerHelper.PickFilesAsync(
            title: "Select Documents and Images to Compress",
            filter: "Compressible Files (*.pdf;*.docx;*.pptx;*.xlsx;*.zip;*.jpg;*.png)\0*.pdf;*.docx;*.pptx;*.xlsx;*.zip;*.jpg;*.png\0All Files (*.*)\0*.*\0",
            allowMultiple: true);

        if (files != null && files.Count > 0)
        {
            ViewModel.AddFiles(files);
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
            var paths = new List<string>();
            foreach (var item in items)
            {
                if (item is StorageFile file)
                {
                    paths.Add(file.Path);
                }
                else if (item is StorageFolder folder)
                {
                    var folderFiles = await folder.GetFilesAsync();
                    paths.AddRange(folderFiles.Select(f => f.Path));
                }
            }
            ViewModel.AddFiles(paths);
        }
    }
}
