using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Axora.Desktop.Services.Contracts;
using Microsoft.Extensions.Logging;

namespace Axora.Desktop.Services;

/// <summary>
/// Persists and loads application preferences safely from %APPDATA%\Axora\settings.json.
/// Eliminates UWP ApplicationData.Current dependencies in unpackaged Win32 execution.
/// </summary>
public sealed class AppSettingsService : IAppSettingsService
{
    private readonly string _settingsDirectory;
    private readonly string _settingsFilePath;

    private readonly Microsoft.Extensions.Logging.ILogger<AppSettingsService>? _logger;
    private SettingsData _data = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public Exception? LastPersistenceError { get; private set; }

    public AppSettingsService(Microsoft.Extensions.Logging.ILogger<AppSettingsService>? logger = null, string? customDirectory = null)
    {
        _logger = logger;

        string baseDir = !string.IsNullOrWhiteSpace(customDirectory)
            ? customDirectory
            : (Environment.GetEnvironmentVariable("APPDATA") ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

        if (string.IsNullOrWhiteSpace(baseDir))
        {
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
        }

        _settingsDirectory = Path.Combine(baseDir, "Axora");
        _settingsFilePath = Path.Combine(_settingsDirectory, "settings.json");

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
            Directory.CreateDirectory(_settingsDirectory);
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
            LastPersistenceError = null;
        }
        catch (Exception ex)
        {
            LastPersistenceError = ex;
            _logger?.LogError(ex, "Failed to persist application settings to {Path}. Error: {Message}", _settingsFilePath, ex.Message);
        }
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
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                _data = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
                LastPersistenceError = null;
                return;
            }
        }
        catch (Exception ex)
        {
            LastPersistenceError = ex;
            _logger?.LogWarning(ex, "Failed to read application settings from {Path}. Using defaults. Error: {Message}", _settingsFilePath, ex.Message);
        }

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
