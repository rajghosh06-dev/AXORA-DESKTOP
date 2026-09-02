using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;

namespace Axora.Desktop.ViewModels;

/// <summary>
/// Settings ViewModel — manages application preferences, P2P background server behavior,
/// and local data retention policies via IAppSettingsService (%APPDATA%\Axora\settings.json).
///
/// FEAT-6: IsDirty tracking drives the floating save/revert pill in SettingsPage.xaml.
/// All OnXxxChanged partial methods mark IsDirty=true when any setting is modified.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly Services.Contracts.IAppSettingsService _settingsService;
    private bool _isLoading; // Guard to suppress IsDirty during LoadSettings()

    [ObservableProperty] private int _selectedThemeIndex;
    [ObservableProperty] private string _accentColor = string.Empty;
    [ObservableProperty] private bool _isTelemetryEnabled;
    [ObservableProperty] private bool _autoStartP2pEngine;
    [ObservableProperty] private bool _backgroundQuickDropListen;
    [ObservableProperty] private string _p2pPort = string.Empty;
    [ObservableProperty] private string _downloadDirectory = string.Empty;
    [ObservableProperty] private string _saveStatus = string.Empty;
    [ObservableProperty] private string _appVersion = string.Empty;

    // FEAT-6: Dirty state for floating save/revert pill
    [ObservableProperty] private bool _isDirty;

    // FEAT-6: Argon2id advanced security settings
    [ObservableProperty] private int _argon2MemoryMb = 64;
    [ObservableProperty] private int _argon2Iterations = 3;

    public SettingsViewModel(Services.Contracts.IAppSettingsService settingsService)
    {
        _settingsService = settingsService;
        _appVersion = GetAppVersion();
        LoadSettings();
    }

    private void LoadSettings()
    {
        _isLoading = true;
        SelectedThemeIndex = _settingsService.ThemeIndex;
        AccentColor = _settingsService.AccentColor;
        IsTelemetryEnabled = _settingsService.IsTelemetryEnabled;
        AutoStartP2pEngine = _settingsService.AutoStartP2pEngine;
        BackgroundQuickDropListen = _settingsService.BackgroundQuickDropListen;
        P2pPort = _settingsService.P2pPort;
        DownloadDirectory = _settingsService.DownloadDirectory;
        Argon2MemoryMb = _settingsService.Argon2MemoryMb > 0 ? _settingsService.Argon2MemoryMb : 64;
        Argon2Iterations = _settingsService.Argon2Iterations > 0 ? _settingsService.Argon2Iterations : 3;
        _isLoading = false;
        IsDirty = false;
    }

    // FEAT-6: Dirty-state partial method handlers
    partial void OnSelectedThemeIndexChanged(int value) { if (!_isLoading) IsDirty = true; }
    partial void OnAccentColorChanged(string value) { if (!_isLoading) IsDirty = true; }
    partial void OnIsTelemetryEnabledChanged(bool value) { if (!_isLoading) IsDirty = true; }
    partial void OnAutoStartP2pEngineChanged(bool value) { if (!_isLoading) IsDirty = true; }
    partial void OnBackgroundQuickDropListenChanged(bool value) { if (!_isLoading) IsDirty = true; }
    partial void OnP2pPortChanged(string value) { if (!_isLoading) IsDirty = true; }
    partial void OnDownloadDirectoryChanged(string value) { if (!_isLoading) IsDirty = true; }
    partial void OnArgon2MemoryMbChanged(int value) { if (!_isLoading) IsDirty = true; }
    partial void OnArgon2IterationsChanged(int value) { if (!_isLoading) IsDirty = true; }

    [RelayCommand]
    public void SaveSettings()
    {
        _settingsService.ThemeIndex = SelectedThemeIndex;
        _settingsService.AccentColor = AccentColor;
        _settingsService.IsTelemetryEnabled = IsTelemetryEnabled;
        _settingsService.AutoStartP2pEngine = AutoStartP2pEngine;
        _settingsService.BackgroundQuickDropListen = BackgroundQuickDropListen;
        _settingsService.P2pPort = P2pPort;
        _settingsService.DownloadDirectory = DownloadDirectory;
        _settingsService.Argon2MemoryMb = Argon2MemoryMb;
        _settingsService.Argon2Iterations = Argon2Iterations;
        _settingsService.Save();
        SaveStatus = "Settings saved to %APPDATA%\\Axora\\settings.json";
        IsDirty = false;
    }

    [RelayCommand] public void RevertSettings() { LoadSettings(); SaveStatus = "Changes reverted."; }

    [RelayCommand]
    public void ResetToDefaults()
    {
        _settingsService.ResetToDefaults();
        LoadSettings();
        SaveStatus = "Reset to default preferences.";
        IsDirty = false;
    }

    private static string GetAppVersion()
    {
        var v = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
        return v is null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}
