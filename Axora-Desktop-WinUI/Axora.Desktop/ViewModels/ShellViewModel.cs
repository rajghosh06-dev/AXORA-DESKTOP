using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Axora.Desktop.Helpers;
using Axora.Desktop.Models.Voice;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.ViewModels;

/// <summary>
/// Shell navigation coordinator. Tracks the active page, drives the NavigationView selection state,
/// and registers hands-free voice navigation and control commands via IVoiceCommandRouter.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly IVoiceCoordinator? _voiceCoordinator;
    private readonly IVoiceCommandRouter? _commandRouter;
    private readonly DispatcherQueue? _dispatcher;

    [ObservableProperty]
    private string _currentPageTitle = "Dashboard";

    [ObservableProperty]
    private bool _isPaneOpen = true;

    [ObservableProperty]
    private bool _isCommandPaletteOpen;

    [ObservableProperty]
    private bool _isVoiceListening;

    [ObservableProperty]
    private string _voiceStatusText = "Voice: Idle";

    [ObservableProperty]
    private AudioCaptureHealth _voiceHealth = AudioCaptureHealth.Healthy;

    /// <summary>
    /// Optional navigation delegate wired by ShellView for testability without UI thread.
    /// </summary>
    public Action<string>? NavigationHandler { get; set; }

    /// <summary>
    /// Page tag → (Type, Title) mapping used by ShellView to navigate the Frame.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (Type PageType, string Title)> PageMap =
        new Dictionary<string, (Type, string)>
        {
            ["Dashboard"]           = (typeof(Views.DashboardPage),              "Dashboard"),
            ["ScholarKit"]          = (typeof(Views.ScholarKitPage),             "Scholar Kit"),
            ["ResumeStudio"]        = (typeof(Views.ResumeStudioDashboardPage),  "Resume Studio"),
            ["ResumeStudioEditor"]  = (typeof(Views.ResumeStudioPage),           "Resume Studio — Editor"),
            ["BatchImage"]          = (typeof(Views.BatchImagePage),             "Batch Image Studio"),
            ["Compressor"]          = (typeof(Views.CompressorPage),             "Intelligent Compressor"),
            ["UniversalConverter"]  = (typeof(Views.UniversalConverterPage),     "Universal Converter"),
            ["Vault"]               = (typeof(Views.VaultPage),                  "Encrypted Vault"),
            ["Flashcards"]          = (typeof(Views.FlashcardsPage),             "Flashcard Studio"),
            ["MobileLink"]          = (typeof(Views.MobileLinkPage),             "Mobile Link"),
            ["DownloadManager"]     = (typeof(Views.DownloadManagerPage),        "Download Manager"),
            ["Settings"]            = (typeof(Views.SettingsPage),               "Settings"),
        };

    public ShellViewModel(
        IVoiceCoordinator? voiceCoordinator = null,
        IVoiceCommandRouter? commandRouter = null)
    {
        _voiceCoordinator = voiceCoordinator;
        _commandRouter = commandRouter;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        if (_voiceCoordinator != null)
        {
            _voiceCoordinator.StateChanged += OnVoiceStateChanged;
            UpdateVoiceStatus(_voiceCoordinator.CurrentState);
        }

        RegisterVoiceCommands();
    }

    private void RegisterVoiceCommands()
    {
        if (_commandRouter == null) return;

        // Register navigation commands for each page in PageMap
        foreach (var kvp in PageMap)
        {
            string tag = kvp.Key;
            string title = kvp.Value.Title.ToLowerInvariant();
            string primaryPhrase = $"navigate to {title}";
            var aliases = new List<string>
            {
                $"go to {title}",
                $"open {title}",
                title
            };

            _commandRouter.RegisterCommand(new VoiceCommandRegistration(
                CommandId: $"Navigate.{tag}",
                PrimaryPhrase: primaryPhrase,
                Aliases: aliases,
                Category: VoiceCommandCategory.Navigation,
                SafetyLevel: CommandSafetyLevel.Safe,
                Action: ct => RunUiActionAsync(() => NavigateTo(tag), ct)
            ));
        }

        // Register shell control commands
        _commandRouter.RegisterCommand(new VoiceCommandRegistration(
            CommandId: "Shell.ToggleSidebar",
            PrimaryPhrase: "toggle sidebar",
            Aliases: new[] { "toggle pane", "toggle menu" },
            Category: VoiceCommandCategory.ShellControl,
            SafetyLevel: CommandSafetyLevel.Safe,
            Action: ct => RunUiActionAsync(TogglePane, ct)
        ));

        _commandRouter.RegisterCommand(new VoiceCommandRegistration(
            CommandId: "Shell.OpenCommandPalette",
            PrimaryPhrase: "open command palette",
            Aliases: new[] { "command palette", "show command palette" },
            Category: VoiceCommandCategory.ShellControl,
            SafetyLevel: CommandSafetyLevel.Safe,
            Action: ct => RunUiActionAsync(OpenCommandPalette, ct)
        ));

        _commandRouter.RegisterCommand(new VoiceCommandRegistration(
            CommandId: "Playback.StopSpeech",
            PrimaryPhrase: "stop reading",
            Aliases: new[] { "stop speech", "stop speaking", "silence" },
            Category: VoiceCommandCategory.Playback,
            SafetyLevel: CommandSafetyLevel.Safe,
            Action: _ =>
            {
                _voiceCoordinator?.RequestStopSpeech();
                return Task.CompletedTask;
            }
        ));
    }

    public void NavigateTo(string tag)
    {
        if (NavigationHandler != null)
        {
            NavigationHandler(tag);
        }
        else
        {
            App.MainAppWindow?.ShellRoot?.NavigateTo(tag);
        }
    }

    private void OnVoiceStateChanged(object? sender, VoiceSessionStateChangedEventArgs e)
    {
        if (_dispatcher == null || _dispatcher.HasThreadAccess)
            UpdateVoiceStatus(e.NewState);
        else
            _ = _dispatcher.RunOnUiThreadAsync(() => UpdateVoiceStatus(e.NewState));
    }

    private Task RunUiActionAsync(Action action, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_dispatcher == null)
        {
            action();
            return Task.CompletedTask;
        }
        return _dispatcher.RunOnUiThreadAsync(action);
    }

    private void UpdateVoiceStatus(VoiceSessionState state)
    {
        IsVoiceListening = state == VoiceSessionState.ListeningForCommand;
        VoiceStatusText = state switch
        {
            VoiceSessionState.ListeningForCommand => "Voice: Listening for commands",
            VoiceSessionState.Dictating => "Voice: Dictating notes",
            VoiceSessionState.Synthesizing => "Voice: Speaking aloud",
            VoiceSessionState.Paused => "Voice: Paused",
            VoiceSessionState.Disabled => "Voice: Disabled / No mic",
            VoiceSessionState.Error => "Voice: Error",
            _ => "Voice: Idle"
        };
        if (_voiceCoordinator != null)
        {
            VoiceHealth = _voiceCoordinator.CaptureHealth;
        }
    }

    [RelayCommand]
    public async Task ToggleVoiceNavigationAsync()
    {
        if (_voiceCoordinator == null) return;

        if (_voiceCoordinator.IsVoiceNavigationDesired)
        {
            await _voiceCoordinator.StopVoiceNavigationAsync();
        }
        else
        {
            await _voiceCoordinator.StartVoiceNavigationAsync();
        }
    }

    [RelayCommand]
    private void OpenCommandPalette() => IsCommandPaletteOpen = true;

    [RelayCommand]
    private void CloseCommandPalette() => IsCommandPaletteOpen = false;

    [RelayCommand]
    private void TogglePane() => IsPaneOpen = !IsPaneOpen;

    public void Dispose()
    {
        if (_voiceCoordinator != null)
        {
            _voiceCoordinator.StateChanged -= OnVoiceStateChanged;
        }
    }
}
