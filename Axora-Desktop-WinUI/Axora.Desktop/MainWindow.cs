using System;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Axora.Desktop;

/// <summary>
/// Main application window. Configures Mica Alt backdrop, custom title bar,
/// window size constraints, and global keyboard accelerators.
/// </summary>
public sealed class MainWindow : Window
{
    private AppWindow _appWindow = null!;
    public Views.ShellView ShellRoot { get; private set; } = null!;

    public MainWindow()
    {
        Title = "Axora Desktop";

        var rootGrid = new Grid
        {
            KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden
        };

        // ── Custom Title Bar Region (non-client area) ─────────────────────────
        var appTitleBar = new Grid
        {
            Height = 36,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Colors.Transparent),
            IsHitTestVisible = true
        };
        appTitleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        appTitleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        appTitleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new Image
        {
            Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/Square44x44Logo.png")),
            Width = 18,
            Height = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(icon, 1);
        appTitleBar.Children.Add(icon);

        var titleBlock = new TextBlock
        {
            Text = "Axora Desktop",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleBlock, 2);
        appTitleBar.Children.Add(titleBlock);

        rootGrid.Children.Add(appTitleBar);

        // ── Shell Content (starts below titlebar) ─────────────────────────────
        ShellRoot = new Views.ShellView { Margin = new Thickness(0, 36, 0, 0) };
        rootGrid.Children.Add(ShellRoot);

        Content = rootGrid;

        ConfigureWindow(appTitleBar);
        RegisterKeyboardAccelerators();
    }

    private void ConfigureWindow(UIElement titleBarElement)
    {
        _appWindow = GetAppWindowForCurrentWindow();

        var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppIcon.ico");
        if (System.IO.File.Exists(iconPath))
        {
            _appWindow.SetIcon(iconPath);
        }

        // ── Mica Alt System Backdrop ──────────────────────────────────────────
        SystemBackdrop = new MicaBackdrop
        {
            Kind = MicaKind.BaseAlt
        };

        // ── Custom Title Bar ──────────────────────────────────────────────────
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(titleBarElement);

        _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        _appWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(32, 255, 255, 255);

        // ── Window Title & Constraints ────────────────────────────────────────
        Title = "Axora Desktop";
        _appWindow.Title = "Axora Desktop";

        if (_appWindow.Presenter is OverlappedPresenter overlapped)
        {
            overlapped.IsResizable = true;
            overlapped.IsMaximizable = true;
            overlapped.IsMinimizable = true;
        }

        // Center on primary screen at 82% x 85% coverage
        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest);
        var workArea = displayArea.WorkArea;
        int w = (int)(workArea.Width * 0.82);
        int h = (int)(workArea.Height * 0.85);
        int x = (workArea.Width - w) / 2;
        int y = (workArea.Height - h) / 2;
        _appWindow.MoveAndResize(new RectInt32(x, y, w, h));

        // ── Drag-and-Drop ─────────────────────────────────────────────────────
        Content.AllowDrop = true;
        Content.DragOver += OnRootDragOver;
        Content.Drop += OnRootDrop;
        Content.DragLeave += OnRootDragLeave;
    }

    private void RegisterKeyboardAccelerators()
    {
        if (Content is not UIElement rootElement) return;

        // Ctrl+K — Command Palette
        var ctrlK = new KeyboardAccelerator
        {
            Modifiers = Windows.System.VirtualKeyModifiers.Control,
            Key = Windows.System.VirtualKey.K
        };
        ctrlK.Invoked += (_, _) => ShellRoot.OpenCommandPalette();
        rootElement.KeyboardAccelerators.Add(ctrlK);

        // Ctrl+\ — Toggle nav pane
        var ctrlSlash = new KeyboardAccelerator
        {
            Modifiers = Windows.System.VirtualKeyModifiers.Control,
            Key = Windows.System.VirtualKey.Back
        };
        ctrlSlash.Invoked += (_, _) => ShellRoot.TogglePane();
        rootElement.KeyboardAccelerators.Add(ctrlSlash);
    }

    // ── Drag-and-Drop Handlers ────────────────────────────────────────────────

    private void OnRootDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        ShellRoot.ShowDropOverlay();
    }

    private void OnRootDragLeave(object sender, DragEventArgs e)
    {
        ShellRoot.HideDropOverlay();
    }

    private async void OnRootDrop(object sender, DragEventArgs e)
    {
        ShellRoot.HideDropOverlay();
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            ShellRoot.HandleDroppedFiles(items);
        }
    }

    // ── Window Resizing Constraints & Subclassing ───────────────────────────
    private const int MinWindowWidthDip = 1000;
    private const int MinWindowHeightDip = 620;
    private IntPtr _hWnd = IntPtr.Zero;
    private SUBCLASSPROC? _subclassProc;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    private const int WM_GETMINMAXINFO = 0x0024;

    private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

    [System.Runtime.InteropServices.DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    private IntPtr WindowSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_GETMINMAXINFO)
        {
            var mmi = System.Runtime.InteropServices.Marshal.PtrToStructure<MINMAXINFO>(lParam);
            uint dpi = GetDpiForWindow(hWnd);
            if (dpi == 0) dpi = 96;
            float scale = dpi / 96f;

            mmi.ptMinTrackSize.x = (int)(MinWindowWidthDip * scale);
            mmi.ptMinTrackSize.y = (int)(MinWindowHeightDip * scale);

            System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, true);
            return IntPtr.Zero;
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    // ── Win32 AppWindow Resolution ────────────────────────────────────────────

    private AppWindow GetAppWindowForCurrentWindow()
    {
        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _subclassProc = new SUBCLASSPROC(WindowSubclassProc);
        SetWindowSubclass(_hWnd, _subclassProc, UIntPtr.Zero, IntPtr.Zero);

        var windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
        return AppWindow.GetFromWindowId(windowId);
    }
}
