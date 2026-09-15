using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models.Voice;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Centralized coordinator for the voice subsystem.
/// Synchronizes state transitions between dictation, command listening, and speech synthesis,
/// enforces mutual exclusion to prevent speaker-to-microphone acoustic feedback,
/// and applies a configurable acoustic debounce interval upon speech completion.
/// </summary>
public sealed class VoiceCoordinator : IVoiceCoordinator, IDisposable
{
    private readonly IVoiceTranscriberService _transcriber;
    private readonly ISpeechSynthesisService _speechService;
    private readonly IVoiceCommandRouter _commandRouter;
    private readonly IAudioDeviceMonitor _deviceMonitor;
    private readonly IVoiceTextFormatter _textFormatter;
    private readonly IAppSettingsService _settings;
    private readonly ILogger<VoiceCoordinator>? _logger;

    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private VoiceSessionState _currentState = VoiceSessionState.Idle;
    private bool _isVoiceNavigationEnabled;
    private TimeSpan _acousticDebounceInterval = TimeSpan.FromMilliseconds(250);
    private Action<string>? _activeDictationCallback;
    private bool _wasListeningBeforeSpeech;

    public VoiceSessionState CurrentState
    {
        get
        {
            return _currentState;
        }
        private set
        {
            if (_currentState != value)
            {
                var prev = _currentState;
                _currentState = value;
                _logger?.LogInformation("VoiceCoordinator state transition: {Previous} -> {Current}", prev, _currentState);
                StateChanged?.Invoke(this, new VoiceSessionStateChangedEventArgs(prev, _currentState));
            }
        }
    }

    public bool IsVoiceNavigationEnabled
    {
        get => _isVoiceNavigationEnabled;
        set
        {
            if (_isVoiceNavigationEnabled != value)
            {
                _isVoiceNavigationEnabled = value;
                _settings.IsVoiceNavigationEnabled = value;
                _settings.Save();
                if (!value && CurrentState == VoiceSessionState.ListeningForCommand)
                {
                    _ = StopVoiceNavigationAsync();
                }
            }
        }
    }

    public AudioCaptureHealth CaptureHealth => _deviceMonitor.CurrentHealth;

    public TimeSpan AcousticDebounceInterval
    {
        get => _acousticDebounceInterval;
        set => _acousticDebounceInterval = value < TimeSpan.Zero ? TimeSpan.Zero : value;
    }

    public event EventHandler<VoiceSessionStateChangedEventArgs>? StateChanged;

    public VoiceCoordinator(
        IVoiceTranscriberService transcriber,
        ISpeechSynthesisService speechService,
        IVoiceCommandRouter commandRouter,
        IAudioDeviceMonitor deviceMonitor,
        IVoiceTextFormatter textFormatter,
        IAppSettingsService settings,
        ILogger<VoiceCoordinator>? logger = null)
    {
        _transcriber = transcriber;
        _speechService = speechService;
        _commandRouter = commandRouter;
        _deviceMonitor = deviceMonitor;
        _textFormatter = textFormatter;
        _settings = settings;
        _logger = logger;

        _isVoiceNavigationEnabled = _settings.IsVoiceNavigationEnabled;

        _deviceMonitor.DeviceStatusChanged += OnDeviceStatusChanged;
        _speechService.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    private void OnDeviceStatusChanged(object? sender, AudioDeviceStatusChangedEventArgs e)
    {
        if (e.Health == AudioCaptureHealth.NoMicrophoneDetected || e.Health == AudioCaptureHealth.PermissionDenied)
        {
            if (CurrentState == VoiceSessionState.Dictating || CurrentState == VoiceSessionState.ListeningForCommand)
            {
                CurrentState = VoiceSessionState.Disabled;
            }
        }
    }

    private void OnPlaybackStateChanged(object? sender, SpeechPlaybackStateChangedEventArgs e)
    {
        if (!e.IsSpeaking && CurrentState == VoiceSessionState.Synthesizing)
        {
            _ = HandleSpeechEndedAsync();
        }
    }

    private async Task HandleSpeechEndedAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (CurrentState != VoiceSessionState.Synthesizing) return;

            // R-VOICE-05: Apply configurable acoustic debounce interval to allow room reverberation to settle
            if (_acousticDebounceInterval > TimeSpan.Zero)
            {
                await Task.Delay(_acousticDebounceInterval);
            }

            if (_wasListeningBeforeSpeech && _isVoiceNavigationEnabled)
            {
                CurrentState = VoiceSessionState.ListeningForCommand;
                await _commandRouter.StartListeningAsync();
            }
            else
            {
                CurrentState = VoiceSessionState.Idle;
            }
            _wasListeningBeforeSpeech = false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error handling speech completion in VoiceCoordinator.");
            CurrentState = VoiceSessionState.Idle;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<bool> RequestStartDictationAsync(Action<string> onFormattedChunk, CancellationToken ct = default)
    {
        await _stateLock.WaitAsync(ct);
        try
        {
            // If already synthesizing, do not start dictation (mutual exclusion)
            if (CurrentState == VoiceSessionState.Synthesizing)
            {
                _logger?.LogWarning("Cannot start dictation while speech synthesis is active.");
                return false;
            }

            if (_deviceMonitor.CurrentHealth == AudioCaptureHealth.PermissionDenied ||
                _deviceMonitor.CurrentHealth == AudioCaptureHealth.NoMicrophoneDetected)
            {
                CurrentState = VoiceSessionState.Disabled;
                return false;
            }

            _activeDictationCallback = onFormattedChunk;

            // Stop navigation listening if active
            if (CurrentState == VoiceSessionState.ListeningForCommand)
            {
                await _commandRouter.StopListeningAsync();
            }

            await _transcriber.StartDictationAsync(chunk =>
            {
                string formatted = _settings.IsAutoPunctuationEnabled
                    ? _textFormatter.FormatSpokenChunk(chunk.RawText)
                    : chunk.RawText;

                if (!string.IsNullOrWhiteSpace(formatted))
                {
                    _activeDictationCallback?.Invoke(formatted);
                }
            }, ct);

            CurrentState = VoiceSessionState.Dictating;
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start dictation session in VoiceCoordinator.");
            CurrentState = VoiceSessionState.Error;
            return false;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task RequestStopDictationAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (CurrentState != VoiceSessionState.Dictating) return;

            await _transcriber.StopDictationAsync();
            _activeDictationCallback = null;

            if (_isVoiceNavigationEnabled)
            {
                CurrentState = VoiceSessionState.ListeningForCommand;
                await _commandRouter.StartListeningAsync();
            }
            else
            {
                CurrentState = VoiceSessionState.Idle;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping dictation session in VoiceCoordinator.");
            CurrentState = VoiceSessionState.Idle;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<bool> RequestSpeakAsync(string text, double? pitch = null, double? rate = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        await _stateLock.WaitAsync(ct);
        try
        {
            // R-VOICE-04: Mutual exclusion — stop/pause mic listening during active speech synthesis
            if (CurrentState == VoiceSessionState.Dictating)
            {
                await _transcriber.StopDictationAsync();
            }
            else if (CurrentState == VoiceSessionState.ListeningForCommand)
            {
                _wasListeningBeforeSpeech = true;
                await _commandRouter.StopListeningAsync();
            }

            CurrentState = VoiceSessionState.Synthesizing;

            double p = pitch ?? _settings.SpeechPitch;
            double r = rate ?? _settings.SpeechRate;

            _ = _speechService.SpeakTextAsync(text, p, r, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initiate speech synthesis in VoiceCoordinator.");
            CurrentState = VoiceSessionState.Error;
            return false;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public void RequestStopSpeech()
    {
        _speechService.Stop();
        if (CurrentState == VoiceSessionState.Synthesizing)
        {
            _ = HandleSpeechEndedAsync();
        }
    }

    public async Task<bool> StartVoiceNavigationAsync(CancellationToken ct = default)
    {
        await _stateLock.WaitAsync(ct);
        try
        {
            if (CurrentState == VoiceSessionState.Synthesizing || CurrentState == VoiceSessionState.Dictating)
            {
                _isVoiceNavigationEnabled = true;
                return true;
            }

            await _commandRouter.StartListeningAsync(ct);
            _isVoiceNavigationEnabled = true;
            CurrentState = VoiceSessionState.ListeningForCommand;
            return true;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task StopVoiceNavigationAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            await _commandRouter.StopListeningAsync();
            _isVoiceNavigationEnabled = false;
            if (CurrentState == VoiceSessionState.ListeningForCommand)
            {
                CurrentState = VoiceSessionState.Idle;
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public void Dispose()
    {
        _deviceMonitor.DeviceStatusChanged -= OnDeviceStatusChanged;
        _speechService.PlaybackStateChanged -= OnPlaybackStateChanged;
        RequestStopSpeech();
        CurrentState = VoiceSessionState.Idle;
        _transcriber.Dispose();
        _commandRouter.Dispose();
        _deviceMonitor.Dispose();
        _speechService.Dispose();
        _stateLock.Dispose();
    }
}
