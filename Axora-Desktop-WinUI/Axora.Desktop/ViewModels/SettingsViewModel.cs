using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Axora.Desktop.Models.Voice;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.ViewModels;

/// <summary>
/// Settings ViewModel — manages application preferences, P2P background server behavior,
/// voice synthesis / navigation settings, and local data retention policies via IAppSettingsService (%APPDATA%\Axora\settings.json).
///
/// FEAT-6: IsDirty tracking drives the floating save/revert pill in SettingsPage.xaml.
/// All OnXxxChanged partial methods mark IsDirty=true when any setting is modified.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _settingsService;
    private readonly IThemeService? _themeService;
    private readonly ISpeechSynthesisService? _speechService;
    private readonly IVoiceCoordinator? _voiceCoordinator;
    private readonly IAudioDeviceMonitor? _deviceMonitor;
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

    // W4 Voice Subsystem settings
    [ObservableProperty] private string? _selectedVoiceId;
    [ObservableProperty] private double _speechRate = 1.0;
    [ObservableProperty] private double _speechPitch = 1.0;
    [ObservableProperty] private bool _isVoiceNavigationEnabled;
    [ObservableProperty] private bool _isAutoPunctuationEnabled = true;
    [ObservableProperty] private string _microphoneStatusText = "Checking...";
    [ObservableProperty] private IReadOnlyList<VoiceInfo> _availableVoices = Array.Empty<VoiceInfo>();

    public SettingsViewModel(
        IAppSettingsService settingsService,
        IThemeService? themeService = null,
        ISpeechSynthesisService? speechService = null,
        IAudioDeviceMonitor? deviceMonitor = null,
        IVoiceCoordinator? voiceCoordinator = null)
    {
        _settingsService = settingsService;
        _themeService = themeService;
        _speechService = speechService;
        _deviceMonitor = deviceMonitor;
        _voiceCoordinator = voiceCoordinator;
        _appVersion = GetAppVersion();

        if (_deviceMonitor != null)
        {
            _deviceMonitor.DeviceStatusChanged += (s, e) =>
            {
                UpdateMicrophoneStatus(e.Health, e.DeviceName);
            };
        }

        LoadSettings();
    }

    private void UpdateMicrophoneStatus(AudioCaptureHealth health, string? deviceName)
    {
        MicrophoneStatusText = health switch
        {
            AudioCaptureHealth.Healthy => $"Active: {deviceName ?? "Default Microphone"}",
            AudioCaptureHealth.PermissionDenied => "Access Denied by Windows Privacy Settings",
            AudioCaptureHealth.NoMicrophoneDetected => "No microphone detected",
            AudioCaptureHealth.DeviceBusy => "Microphone in use by another app",
            _ => "Voice input unavailable"
        };
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

        SelectedVoiceId = _settingsService.SelectedVoiceId;
        SpeechRate = _settingsService.SpeechRate;
        SpeechPitch = _settingsService.SpeechPitch;
        IsVoiceNavigationEnabled = _settingsService.IsVoiceNavigationEnabled;
        IsAutoPunctuationEnabled = _settingsService.IsAutoPunctuationEnabled;

        if (_speechService != null)
        {
            AvailableVoices = _speechService.AvailableVoices;
            if (string.IsNullOrEmpty(SelectedVoiceId) && _speechService.CurrentVoice != null)
            {
                SelectedVoiceId = _speechService.CurrentVoice.Id;
            }
        }

        if (_deviceMonitor != null)
        {
            UpdateMicrophoneStatus(_deviceMonitor.CurrentHealth, _deviceMonitor.DefaultCaptureDeviceName);
        }

        _isLoading = false;
        IsDirty = false;
    }

    // FEAT-6: Dirty-state partial method handlers with immediate theme/accent visual propagation
    partial void OnSelectedThemeIndexChanged(int value)
    {
        if (!_isLoading)
        {
            IsDirty = true;
            _themeService?.SetTheme(value);
        }
    }

    partial void OnAccentColorChanged(string value)
    {
        if (!_isLoading)
        {
            IsDirty = true;
            if (!string.IsNullOrWhiteSpace(value))
            {
                _themeService?.SetAccentColor(value);
            }
        }
    }

    partial void OnIsTelemetryEnabledChanged(bool value) { if (!_isLoading) IsDirty = true; }
    partial void OnAutoStartP2pEngineChanged(bool value) { if (!_isLoading) IsDirty = true; }
    partial void OnBackgroundQuickDropListenChanged(bool value) { if (!_isLoading) IsDirty = true; }
    partial void OnP2pPortChanged(string value) { if (!_isLoading) IsDirty = true; }
    partial void OnDownloadDirectoryChanged(string value) { if (!_isLoading) IsDirty = true; }
    partial void OnArgon2MemoryMbChanged(int value) { if (!_isLoading) IsDirty = true; }
    partial void OnArgon2IterationsChanged(int value) { if (!_isLoading) IsDirty = true; }

    partial void OnSelectedVoiceIdChanged(string? value) { if (!_isLoading) IsDirty = true; }
    partial void OnSpeechRateChanged(double value) { if (!_isLoading) IsDirty = true; }
    partial void OnSpeechPitchChanged(double value) { if (!_isLoading) IsDirty = true; }
    partial void OnIsVoiceNavigationEnabledChanged(bool value) { if (!_isLoading) IsDirty = true; }
    partial void OnIsAutoPunctuationEnabledChanged(bool value) { if (!_isLoading) IsDirty = true; }

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

        _settingsService.SelectedVoiceId = SelectedVoiceId;
        _settingsService.SpeechRate = SpeechRate;
        _settingsService.SpeechPitch = SpeechPitch;
        _settingsService.IsVoiceNavigationEnabled = IsVoiceNavigationEnabled;
        _settingsService.IsAutoPunctuationEnabled = IsAutoPunctuationEnabled;

        if (!string.IsNullOrEmpty(SelectedVoiceId) && _speechService != null)
        {
            _speechService.SetVoice(SelectedVoiceId);
        }

        _settingsService.Save();

        if (_settingsService.LastPersistenceError != null)
        {
            SaveStatus = $"Save failed: {_settingsService.LastPersistenceError.Message}";
        }
        else
        {
            SaveStatus = $"Preferences saved ({DateTime.Now:HH:mm:ss})";
            IsDirty = false;
        }
    }

    [RelayCommand]
    public async Task TestSpeechAsync()
    {
        if (_voiceCoordinator != null)
        {
            if (!string.IsNullOrEmpty(SelectedVoiceId))
            {
                _speechService?.SetVoice(SelectedVoiceId);
            }
            SpeechPlaybackResult result = await _voiceCoordinator.RequestSpeakAsync(
                "Hello from Axora Desktop speech synthesis.",
                pitch: SpeechPitch,
                rate: SpeechRate);
            SaveStatus = result == SpeechPlaybackResult.Completed
                ? "Speech test completed."
                : $"Speech test {result.ToString().ToLowerInvariant()}; settings remain available.";
        }
    }

    [RelayCommand]
    public void RevertSettings()
    {
        LoadSettings();
        SaveStatus = "Changes discarded.";
    }

    [RelayCommand]
    public void ResetDefaults()
    {
        _settingsService.ResetToDefaults();
        LoadSettings();
        SaveStatus = "Settings reset to defaults.";
    }

    private static string GetAppVersion()
    {
        try
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }
}
