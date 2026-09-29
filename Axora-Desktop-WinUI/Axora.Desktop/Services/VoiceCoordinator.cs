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
    private readonly object _stopGate = new();
    private Task? _stopTask;
    private int _stopping;
    private int _disposed;
    private int _subscriptionsDetached;
    private readonly CancellationTokenSource _speechShutdown = new();
    private readonly object _speechTasksGate = new();
    private readonly System.Collections.Generic.HashSet<Task> _speechTasks = new();
    private readonly System.Collections.Generic.HashSet<Task> _completionTasks = new();

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
            if (Volatile.Read(ref _stopping) != 0) return;
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
        if (Volatile.Read(ref _stopping) != 0) return;
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
        if (Volatile.Read(ref _stopping) != 0) return;
        if (!e.IsSpeaking && CurrentState == VoiceSessionState.Synthesizing)
        {
            QueueSpeechEnded();
        }
    }

    private void QueueSpeechEnded()
    {
        lock (_speechTasksGate)
        {
            if (Volatile.Read(ref _stopping) != 0) return;
            var task = HandleSpeechEndedAsync();
            _completionTasks.Add(task);
            _ = task.ContinueWith(completed =>
            {
                lock (_speechTasksGate) _completionTasks.Remove(completed);
                // Observe an unexpected callback failure; it must not become an
                // unobserved fire-and-forget exception.
                if (completed.IsFaulted)
                {
                    var failure = completed.Exception;
                    try { _logger?.LogWarning(failure, "Voice completion callback failed."); }
                    catch { System.Diagnostics.Debug.WriteLine($"Voice completion callback failed: {failure}"); }
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    private async Task HandleSpeechEndedAsync()
    {
        if (Volatile.Read(ref _stopping) != 0) return;
        await _stateLock.WaitAsync();
        try
        {
            if (Volatile.Read(ref _stopping) != 0) return;
            if (CurrentState != VoiceSessionState.Synthesizing) return;

            // R-VOICE-05: Apply configurable acoustic debounce interval to allow room reverberation to settle
            if (_acousticDebounceInterval > TimeSpan.Zero)
            {
                await Task.Delay(_acousticDebounceInterval);
            }

            if (Volatile.Read(ref _stopping) != 0) return;

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
        if (Volatile.Read(ref _stopping) != 0) return false;
        await _stateLock.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref _stopping) != 0) return false;
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
        if (Volatile.Read(ref _stopping) != 0) return;
        await _stateLock.WaitAsync();
        try
        {
            if (Volatile.Read(ref _stopping) != 0) return;
            if (CurrentState != VoiceSessionState.Dictating) return;

            await _transcriber.StopDictationAsync();
            _activeDictationCallback = null;

            if (_isVoiceNavigationEnabled && Volatile.Read(ref _stopping) == 0)
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
        if (string.IsNullOrWhiteSpace(text) || Volatile.Read(ref _stopping) != 0) return false;

        await _stateLock.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref _stopping) != 0) return false;
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

            TrackSpeech(SpeakForLifecycleAsync(text, p, r, ct));
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
        if (Volatile.Read(ref _stopping) != 0) return;
        _speechService.Stop();
        if (CurrentState == VoiceSessionState.Synthesizing)
        {
            QueueSpeechEnded();
        }
    }

    public async Task<bool> StartVoiceNavigationAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _stopping) != 0) return false;
        await _stateLock.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref _stopping) != 0) return false;
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
        if (Volatile.Read(ref _stopping) != 0) return;
        await _stateLock.WaitAsync();
        try
        {
            if (Volatile.Read(ref _stopping) != 0) return;
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

    public Task StopAsync()
    {
        lock (_stopGate)
            return _stopTask ??= StopCoreAsync();
    }

    private async Task StopCoreAsync()
    {
        // Close callback admission atomically with the owned-task registry.
        lock (_speechTasksGate) Interlocked.Exchange(ref _stopping, 1);
        // Publish the owned stop Task to AppLifecycle before entering WinRT
        // dictation or media teardown. Stay on the calling apartment; moving
        // these APIs to Task.Run would not be a safe threading assumption.
        await Task.Yield();
        var errors = new System.Collections.Generic.List<Exception>();
        try { _speechShutdown.Cancel(); }
        catch (Exception ex) { errors.Add(ex); }
        try { DetachSubscriptions(); }
        catch (Exception ex) { errors.Add(ex); }
        await _stateLock.WaitAsync();
        try
        {
            try { await _transcriber.StopDictationAsync(); }
            catch (Exception ex) { errors.Add(ex); }
            try { await _commandRouter.StopListeningAsync(); }
            catch (Exception ex) { errors.Add(ex); }
            try { _speechService.Stop(); }
            catch (Exception ex) { errors.Add(ex); }
            _activeDictationCallback = null;
            _wasListeningBeforeSpeech = false;
            try { CurrentState = VoiceSessionState.Idle; }
            catch (Exception ex)
            {
                _currentState = VoiceSessionState.Idle;
                errors.Add(ex);
            }
        }
        finally
        {
            _stateLock.Release();
        }

        Task[] pendingWork;
        lock (_speechTasksGate)
            pendingWork = _speechTasks.Concat(_completionTasks).ToArray();
        try { await Task.WhenAll(pendingWork); }
        catch (Exception ex) { errors.Add(ex); }
        if (errors.Count > 0)
            throw new AggregateException("Voice operational stop had one or more failures.", errors);
    }

    private async Task SpeakForLifecycleAsync(string text, double pitch, double rate, CancellationToken callerToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, _speechShutdown.Token);
        try { await _speechService.SpeakTextAsync(text, pitch, rate, linked.Token); }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (Exception ex) { _logger?.LogWarning(ex, "Speech task failed during voice lifecycle."); }
    }

    private void TrackSpeech(Task task)
    {
        lock (_speechTasksGate) _speechTasks.Add(task);
        _ = task.ContinueWith(completed =>
        {
            lock (_speechTasksGate) _speechTasks.Remove(completed);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void DetachSubscriptions()
    {
        if (Interlocked.Exchange(ref _subscriptionsDetached, 1) != 0) return;
        _deviceMonitor.DeviceStatusChanged -= OnDeviceStatusChanged;
        _speechService.PlaybackStateChanged -= OnPlaybackStateChanged;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Interlocked.Exchange(ref _stopping, 1);
        DetachSubscriptions();
        _activeDictationCallback = null;
        _wasListeningBeforeSpeech = false;
        StateChanged = null;
        _currentState = VoiceSessionState.Idle;
        if (_stopTask?.IsCompletedSuccessfully == true)
        {
            _speechShutdown.Dispose();
            _stateLock.Dispose();
        }
    }
}
