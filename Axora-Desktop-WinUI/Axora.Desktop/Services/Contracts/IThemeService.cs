using System;
using Microsoft.UI.Xaml;

namespace Axora.Desktop.Services.Contracts;

public interface IThemeService
{
    int CurrentThemeIndex { get; }
    string CurrentAccentColorHex { get; }

    void Initialize(Window window);
    void SetTheme(int themeIndex);
    void SetAccentColor(string hexColor);

    event EventHandler<int>? ThemeChanged;
    event EventHandler<string>? AccentColorChanged;
}
