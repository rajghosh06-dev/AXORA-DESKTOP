using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Storage;
using Windows.System;
using Axora.Desktop.Helpers;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop.Views;

public sealed partial class ScholarKitPage : Page
{
    public ScholarKitViewModel ViewModel { get; } = App.GetService<ScholarKitViewModel>();

    public ScholarKitPage()
    {
        InitializeComponent();
        DataContext = this;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.SelectedStudioTabIndex))
        {
            SwitchStudioView(ViewModel.SelectedStudioTabIndex);
        }
    }

    // ── Tab Switching ─────────────────────────────────────────────────────────

    private void TabSelector_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tagStr && int.TryParse(tagStr, out int tabIdx))
        {
            ViewModel.SelectedStudioTabIndex = tabIdx;
            SwitchStudioView(tabIdx);
        }
    }

    private void SwitchStudioView(int tabIndex)
    {
        if (ViewEditor == null || ViewMarkdown == null || ViewSynthesizer == null || ViewRagChat == null)
            return;

        ViewEditor.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        ViewMarkdown.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        ViewSynthesizer.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        ViewRagChat.Visibility = tabIndex == 3 ? Visibility.Visible : Visibility.Collapsed;

        // Keep RadioButtons in sync
        if (TabBtnEditor != null) TabBtnEditor.IsChecked = tabIndex == 0;
        if (TabBtnMarkdown != null) TabBtnMarkdown.IsChecked = tabIndex == 1;
        if (TabBtnSynthesizer != null) TabBtnSynthesizer.IsChecked = tabIndex == 2;
        if (TabBtnRagChat != null) TabBtnRagChat.IsChecked = tabIndex == 3;
    }

    // ── Chat Enter Key Handler ────────────────────────────────────────────────

    private void ChatInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            if (ViewModel.AskDocumentAiCommand.CanExecute(null))
            {
                ViewModel.AskDocumentAiCommand.Execute(null);
            }
        }
    }

    // ── Drop Zone Handlers ────────────────────────────────────────────────────

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Drop to extract text with OCR";
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            await ProcessDroppedFilesAsync(items);
        }
    }

    public async Task ProcessDroppedFilesAsync(IReadOnlyList<IStorageItem> items)
    {
        var file = items.OfType<StorageFile>().FirstOrDefault();
        if (file is null) return;
        ViewModel.ImportedFileName = file.Name;
        using var stream = await file.OpenStreamForReadAsync();

        if (file.FileType.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            await ViewModel.AnalyzePdfCommand.ExecuteAsync(stream);
        }
        else
        {
            await ViewModel.ScanDocumentCommand.ExecuteAsync(stream);
        }
    }

    // ── File Pickers ──────────────────────────────────────────────────────────

    private async void BrowseImage_Click(object sender, RoutedEventArgs e)
    {
        var files = await NativeFilePickerHelper.PickFilesAsync(
            title: "Select Image for OCR Extraction",
            filter: "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.tiff)\0*.png;*.jpg;*.jpeg;*.bmp;*.tiff\0All Files (*.*)\0*.*\0",
            allowMultiple: false);

        if (files != null && files.Count > 0)
        {
            var path = files[0];
            ViewModel.ImportedFileName = Path.GetFileName(path);
            using var stream = File.OpenRead(path);
            await ViewModel.ScanDocumentCommand.ExecuteAsync(stream);
        }
    }

    private async void BrowsePdf_Click(object sender, RoutedEventArgs e)
    {
        var files = await NativeFilePickerHelper.PickFilesAsync(
            title: "Select PDF Document for Structural Extraction",
            filter: "PDF Documents (*.pdf)\0*.pdf\0All Files (*.*)\0*.*\0",
            allowMultiple: false);

        if (files != null && files.Count > 0)
        {
            var path = files[0];
            ViewModel.ImportedFileName = Path.GetFileName(path);
            using var stream = File.OpenRead(path);
            await ViewModel.AnalyzePdfCommand.ExecuteAsync(stream);
        }
    }
}
