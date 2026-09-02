using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop.Controls;

public sealed partial class FloatingDropWidget : UserControl
{
    public event EventHandler? Closed;

    public FloatingDropWidget()
    {
        InitializeComponent();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            DropLabel.Text = "Release to Ingest Files";
            DropIcon.Glyph = "\uE898"; // Folder open / active
        }
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        DropLabel.Text = "Drag & Drop Files Here";
        DropIcon.Glyph = "\uE896";
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        DropLabel.Text = "Drag & Drop Files Here";
        DropIcon.Glyph = "\uE896";

        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

        var items = await e.DataView.GetStorageItemsAsync();
        var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();

        if (paths.Count == 0) return;

        ActionProgress.IsActive = true;
        try
        {
            await RouteFilesAsync(paths, TargetModuleCombo.SelectedIndex);
        }
        finally
        {
            ActionProgress.IsActive = false;
        }
    }

    private async Task RouteFilesAsync(IReadOnlyList<string> paths, int targetIndex)
    {
        switch (targetIndex)
        {
            case 0: // Mobile QuickDrop (P2P)
                var mobileVm = App.TryGetService<MobileLinkViewModel>();
                if (mobileVm != null && paths.Count > 0)
                {
                    await mobileVm.PushFileToMobileAsync(paths[0]);
                    App.MainAppWindow?.ShellRoot.NavigateTo("MobileLink");
                }
                break;

            case 1: // Encrypted Vault
                var vaultVm = App.TryGetService<VaultViewModel>();
                if (vaultVm != null)
                {
                    foreach (var path in paths) vaultVm.AddFileToQueue(path);
                    App.MainAppWindow?.ShellRoot.NavigateTo("Vault");
                }
                break;

            case 2: // Intelligent Compressor
                var compVm = App.TryGetService<CompressorViewModel>();
                if (compVm != null)
                {
                    compVm.AddFiles(paths);
                    App.MainAppWindow?.ShellRoot.NavigateTo("Compressor");
                }
                break;

            case 3: // Scholar Kit OCR
                var scholarVm = App.TryGetService<ScholarKitViewModel>();
                if (scholarVm != null && paths.Count > 0)
                {
                    var ext = Path.GetExtension(paths[0]).ToLowerInvariant();
                    if (ext == ".pdf")
                    {
                        using var stream = File.OpenRead(paths[0]);
                        await scholarVm.AnalyzePdfCommand.ExecuteAsync(stream);
                    }
                    else
                    {
                        using var stream = File.OpenRead(paths[0]);
                        await scholarVm.ScanDocumentCommand.ExecuteAsync(stream);
                    }
                    App.MainAppWindow?.ShellRoot.NavigateTo("ScholarKit");
                }
                break;
        }
    }
}
