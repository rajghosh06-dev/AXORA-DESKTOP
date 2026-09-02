using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Persists and loads application preferences safely from %APPDATA%\Axora\settings.json.
/// Eliminates UWP ApplicationData.Current dependencies in unpackaged Win32 execution.
/// </summary>
public sealed class AppSettingsService : IAppSettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Axora");

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    private SettingsData _data = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppSettingsService()
    {
        Load();
    }

    public int ThemeIndex
    {
        get => _data.ThemeIndex;
        set { if (_data.ThemeIndex != value) { _data.ThemeIndex = value; OnPropertyChanged(); } }
    }

    public string AccentColor
    {
        get => _data.AccentColor;
        set { if (_data.AccentColor != value) { _data.AccentColor = value; OnPropertyChanged(); } }
    }

    public bool IsTelemetryEnabled
    {
        get => _data.IsTelemetryEnabled;
        set { if (_data.IsTelemetryEnabled != value) { _data.IsTelemetryEnabled = value; OnPropertyChanged(); } }
    }

    public bool AutoStartP2pEngine
    {
        get => _data.AutoStartP2pEngine;
        set { if (_data.AutoStartP2pEngine != value) { _data.AutoStartP2pEngine = value; OnPropertyChanged(); } }
    }

    public bool BackgroundQuickDropListen
    {
        get => _data.BackgroundQuickDropListen;
        set { if (_data.BackgroundQuickDropListen != value) { _data.BackgroundQuickDropListen = value; OnPropertyChanged(); } }
    }

    public string P2pPort
    {
        get => _data.P2pPort;
        set { if (_data.P2pPort != value) { _data.P2pPort = value; OnPropertyChanged(); } }
    }

    public string DownloadDirectory
    {
        get => _data.DownloadDirectory;
        set { if (_data.DownloadDirectory != value) { _data.DownloadDirectory = value; OnPropertyChanged(); } }
    }

    // FEAT-6: Argon2id advanced cryptography settings
    public int Argon2MemoryMb
    {
        get => _data.Argon2MemoryMb;
        set { if (_data.Argon2MemoryMb != value) { _data.Argon2MemoryMb = value; OnPropertyChanged(); } }
    }

    public int Argon2Iterations
    {
        get => _data.Argon2Iterations;
        set { if (_data.Argon2Iterations != value) { _data.Argon2Iterations = value; OnPropertyChanged(); } }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch { /* Fallback */ }
    }

    public void ResetToDefaults()
    {
        _data = new SettingsData();
        Save();
        OnPropertyChanged(string.Empty);
    }

    private void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                _data = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
                return;
            }
        }
        catch { /* Defaults will be used */ }

        _data = new SettingsData();
        Save();
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class SettingsData
    {
        public int ThemeIndex { get; set; } = 0; // 0=System, 1=Light, 2=Dark
        public string AccentColor { get; set; } = "#5B7DE8";
        public bool IsTelemetryEnabled { get; set; } = true;
        public bool AutoStartP2pEngine { get; set; } = true;
        public bool BackgroundQuickDropListen { get; set; } = true;
        public string P2pPort { get; set; } = "5050";
        public string DownloadDirectory { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "Axora_QuickDrop");
        // FEAT-6: Argon2id parameters persisted to settings.json
        public int Argon2MemoryMb { get; set; } = 64;
        public int Argon2Iterations { get; set; } = 3;
    }
}
