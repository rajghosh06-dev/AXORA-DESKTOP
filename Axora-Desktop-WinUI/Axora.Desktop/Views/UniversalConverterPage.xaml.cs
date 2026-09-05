using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Axora.Desktop.Helpers;
using Axora.Desktop.Models;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop.Views;

/// <summary>
/// Universal Converter Page — local, private batch format transformation.
/// </summary>
public sealed partial class UniversalConverterPage : Page
{
    public UniversalConverterViewModel ViewModel { get; } = App.GetService<UniversalConverterViewModel>();

    public UniversalConverterPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private static void Log(string msg)
    {
        try
        {
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] [UniversalConverterPage] {msg}\n");
        }
        catch { }
    }

    private async void BrowseFiles_Click(object sender, RoutedEventArgs e)
    {
        Log("BrowseFiles_Click invoked.");
        var files = await NativeFilePickerHelper.PickFilesAsync(
            title: "Select Files to Convert",
            filter: "All Supported Files (*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tiff;*.pdf;*.txt;*.md;*.html)\0*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tiff;*.pdf;*.txt;*.md;*.html\0Image Files (*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tiff)\0*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tiff\0Document Files (*.pdf;*.txt;*.md;*.html)\0*.pdf;*.txt;*.md;*.html\0All Files (*.*)\0*.*\0",
            allowMultiple: true);

        Log($"PickFilesAsync returned {(files == null ? "null" : files.Count + " files")}.");
        if (files != null && files.Count > 0)
        {
            ViewModel.AddFiles(files);
        }
    }

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await NativeFilePickerHelper.PickFolderAsync("Select Folder of Files to Convert");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            await ViewModel.AddFolderAsync(folder);
        }
    }

    private async void ChangeOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await NativeFilePickerHelper.PickFolderAsync("Select Custom Output Destination Folder");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            ViewModel.SetCustomOutputDirectory(folder);
        }
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        if (DropZoneBorder != null)
        {
            DropZoneBorder.BorderBrush = Application.Current.Resources["AxoraPrimaryBrush"] as Brush;
        }
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        if (DropZoneBorder != null)
        {
            DropZoneBorder.BorderBrush = Application.Current.Resources["AccentControlElevationBorderBrush"] as Brush;
        }
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (DropZoneBorder != null)
        {
            DropZoneBorder.BorderBrush = Application.Current.Resources["AccentControlElevationBorderBrush"] as Brush;
        }

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

    private void RejectedInfoBar_CloseButtonClick(InfoBar sender, object args)
    {
        ViewModel.DismissRejectedMessage();
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            ViewModel.OpenOutputFile(path);
        }
    }

    private async void RetryItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ConversionJobUiModel item)
        {
            await ViewModel.RetryJobAsync(item);
        }
    }

    private void CancelItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string jobId)
        {
            ViewModel.CancelJob(jobId);
        }
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ConversionJobUiModel item)
        {
            ViewModel.RemoveJob(item);
        }
    }

    private async void StartConversion_Click(object sender, RoutedEventArgs e)
    {
        Log("StartConversion_Click invoked.");
        await ViewModel.StartConversionAsync();
        Log("StartConversion_Click completed.");
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearQueue();
    }

    private void ClearCompleted_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearCompleted();
    }

    private void CancelAll_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelAll();
    }

    private async void PauseQueue_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.PauseQueueAsync();
    }

    private async void ResumeQueue_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ResumeQueueAsync();
    }
}
