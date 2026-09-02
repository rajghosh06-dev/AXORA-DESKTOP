using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Native Windows System Tray (Shell_NotifyIconW) Integration.
/// Manages tray icon lifecycle, background notifications, and quick restore.
/// </summary>
public sealed class TrayService : ITrayService, IDisposable
{
    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int NIF_INFO = 0x00000010;
    private const int NIIF_INFO = 0x00000001;

    private const int WM_USER = 0x0400;
    public const int WM_TRAYICON = WM_USER + 101;

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;
    private const uint LR_DEFAULTSIZE = 0x00000040;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly ILogger<TrayService> _logger;
    private IntPtr _hwnd;
    private IntPtr _loadedIcon = IntPtr.Zero;
    private bool _isInitialized;

    public TrayService(ILogger<TrayService> logger)
    {
        _logger = logger;
    }

    public void Initialize(IntPtr hwnd)
    {
        if (_isInitialized) return;
        _hwnd = hwnd;

        try
        {
            IntPtr hIcon = IntPtr.Zero;
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
                if (hIcon != IntPtr.Zero)
                {
                    _loadedIcon = hIcon;
                }
            }

            if (hIcon == IntPtr.Zero)
            {
                hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512); // fallback to IDI_APPLICATION
            }

            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1001,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = hIcon,
                szTip = "Axora Desktop"
            };

            Shell_NotifyIconW(NIM_ADD, ref nid);
            _isInitialized = true;
            _logger.LogInformation("System Tray icon initialized with AppIcon");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize System Tray icon");
        }
    }

    public void ShowNotification(string title, string message)
    {
        if (!_isInitialized) return;

        try
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1001,
                uFlags = NIF_INFO,
                szInfoTitle = title,
                szInfo = message,
                dwInfoFlags = NIIF_INFO
            };

            Shell_NotifyIconW(NIM_MODIFY, ref nid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show tray notification");
        }
    }

    public void Remove()
    {
        if (!_isInitialized) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1001
        };

        Shell_NotifyIconW(NIM_DELETE, ref nid);
        _isInitialized = false;

        if (_loadedIcon != IntPtr.Zero)
        {
            try { DestroyIcon(_loadedIcon); } catch { }
            _loadedIcon = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        Remove();
    }
}
