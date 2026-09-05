using System;
using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Native WinUI 3 ThemeService. Manages runtime theme switching (System, Light, Dark)
/// and dynamic accent color resource propagation across the active visual tree.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly IAppSettingsService _settings;
    private readonly ILogger<ThemeService> _logger;
    private Window? _window;
    private DispatcherQueue? _dispatcher;

    public int CurrentThemeIndex { get; private set; }
    public string CurrentAccentColorHex { get; private set; } = "#5B7DE8";

    public event EventHandler<int>? ThemeChanged;
    public event EventHandler<string>? AccentColorChanged;

    public ThemeService(IAppSettingsService settings, ILogger<ThemeService> logger)
    {
        _settings = settings;
        _logger = logger;
        CurrentThemeIndex = settings.ThemeIndex;
        CurrentAccentColorHex = settings.AccentColor;
    }

    public void Initialize(Window window)
    {
        _window = window;
        _dispatcher = window.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();

        // Apply persisted settings immediately
        SetTheme(_settings.ThemeIndex);
        if (!string.IsNullOrWhiteSpace(_settings.AccentColor))
        {
            SetAccentColor(_settings.AccentColor);
        }

        // React to live changes in Settings without application restart
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        _logger.LogInformation("ThemeService initialized. ThemeIndex={Index}, AccentColor={Color}",
            CurrentThemeIndex, CurrentAccentColorHex);
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAppSettingsService.ThemeIndex))
        {
            SetTheme(_settings.ThemeIndex);
        }
        else if (e.PropertyName == nameof(IAppSettingsService.AccentColor))
        {
            SetAccentColor(_settings.AccentColor);
        }
    }

    public void SetTheme(int themeIndex)
    {
        CurrentThemeIndex = themeIndex;

        void ApplyTheme()
        {
            if (_window?.Content is FrameworkElement rootElement)
            {
                var targetTheme = themeIndex switch
                {
                    1 => ElementTheme.Light,
                    2 => ElementTheme.Dark,
                    _ => ElementTheme.Default // 0 = System
                };

                rootElement.RequestedTheme = targetTheme;
                _logger.LogInformation("Visual tree RequestedTheme updated to {Theme}", targetTheme);
            }
        }

        if (_dispatcher != null && !_dispatcher.HasThreadAccess)
        {
            _dispatcher.TryEnqueue(ApplyTheme);
        }
        else
        {
            ApplyTheme();
        }

        ThemeChanged?.Invoke(this, CurrentThemeIndex);
    }

    public void SetAccentColor(string hexColor)
    {
        if (string.IsNullOrWhiteSpace(hexColor)) return;
        CurrentAccentColorHex = hexColor;

        if (!TryParseHexColor(hexColor, out var color))
        {
            _logger.LogWarning("Invalid accent color format: {Hex}", hexColor);
            return;
        }

        void ApplyAccent()
        {
            try
            {
                if (Application.Current?.Resources != null)
                {
                    var resources = Application.Current.Resources;
                    resources["SystemAccentColor"] = color;
                    resources["AccentFillColorDefaultBrush"] = new SolidColorBrush(color);
                    resources["AccentTextFillColorPrimaryBrush"] = new SolidColorBrush(color);

                    // Slightly darker/lighter brush for pressed/hover states
                    var hoverColor = Color.FromArgb(
                        color.A,
                        (byte)Math.Clamp(color.R + 20, 0, 255),
                        (byte)Math.Clamp(color.G + 20, 0, 255),
                        (byte)Math.Clamp(color.B + 20, 0, 255));
                    resources["AccentFillColorSecondaryBrush"] = new SolidColorBrush(hoverColor);
                    resources["AccentFillColorTertiaryBrush"] = new SolidColorBrush(hoverColor);

                    // Refresh visual tree so ThemeResource references re-evaluate immediately
                    if (_window?.Content is FrameworkElement root)
                    {
                        var cur = root.RequestedTheme;
                        root.RequestedTheme = cur == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
                        root.RequestedTheme = cur;
                    }

                    _logger.LogInformation("Applied accent color {Hex} to Application resources", hexColor);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update dynamic accent color resources.");
            }
        }

        if (_dispatcher != null && !_dispatcher.HasThreadAccess)
        {
            _dispatcher.TryEnqueue(ApplyAccent);
        }
        else
        {
            ApplyAccent();
        }

        AccentColorChanged?.Invoke(this, CurrentAccentColorHex);
    }

    public static bool TryParseHexColor(string hex, out Color color)
    {
        color = Colors.Transparent;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        string cleanHex = hex.Trim().TrimStart('#');
        try
        {
            if (cleanHex.Length == 6)
            {
                byte r = byte.Parse(cleanHex.Substring(0, 2), NumberStyles.HexNumber);
                byte g = byte.Parse(cleanHex.Substring(2, 2), NumberStyles.HexNumber);
                byte b = byte.Parse(cleanHex.Substring(4, 2), NumberStyles.HexNumber);
                color = Color.FromArgb(255, r, g, b);
                return true;
            }
            else if (cleanHex.Length == 8)
            {
                byte a = byte.Parse(cleanHex.Substring(0, 2), NumberStyles.HexNumber);
                byte r = byte.Parse(cleanHex.Substring(2, 2), NumberStyles.HexNumber);
                byte g = byte.Parse(cleanHex.Substring(4, 2), NumberStyles.HexNumber);
                byte b = byte.Parse(cleanHex.Substring(6, 2), NumberStyles.HexNumber);
                color = Color.FromArgb(a, r, g, b);
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
