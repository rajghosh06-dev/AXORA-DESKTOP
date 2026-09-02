using Microsoft.UI.Xaml.Controls;

namespace Axora.Desktop.Controls;

/// <summary>
/// Full-window drag-over visual overlay. Set Visibility from MainWindow's DragOver/DragLeave events.
/// IsHitTestVisible is false so drop events pass through to the root grid.
/// </summary>
public sealed partial class FileDropZoneOverlay : UserControl
{
    public FileDropZoneOverlay()
    {
        InitializeComponent();
    }
}
