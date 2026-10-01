using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models.Voice;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Owns desired and actual voice modes, one recognition generation and serialized
/// terminal speech playback. Low-level services remain DI-owned resources.
/// </summary>
public sealed class VoiceCoordinator : IVoiceCoordinator
{
    private enum RecognitionMode { None, Dictation, Command }
    private enum ActivationState { Pending, Active, Completed }
    private sealed class RecognitionActivation(long generation, RecognitionMode mode)
    {
        public long Generation { get; } = generation;
        public RecognitionMode Mode { get; } = mode;
        public ActivationState State { get; set; }
    }
    private sealed class SpeechAdmission
    {
        public TaskCompletionSource Granted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public LinkedListNode<SpeechAdmission>? Node { get; set; }
    }

    private const int MaxPendingSpeechRequests = 8;
    private readonly IVoiceTranscriberService _transcriber;
    private readonly ISpeechSynthesisService _speechService;
    private readonly IVoiceCommandRouter _commandRouter;
    private readonly IAudioDeviceMonitor _deviceMonitor;
    private readonly IVoiceTextFormatter _textFormatter;
    private readonly IAppSettingsService _settings;
    private readonly ILogger<VoiceCoordinator>? _logger;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly object _recognitionCallbackGate = new();
    private readonly object _recognitionLifecycleGate = new();
    private RecognitionActivation? _recognitionActivation;
    internal Action? BeforeRecognitionActivation { get; set; }
    private readonly HashSet<long> _dispatchedCommandSequences = [];
    private readonly object _speechQueueGate = new();
    private readonly LinkedList<SpeechAdmission> _speechQueue = [];
    private TaskCompletionSource _speechIdle = CompletedSignal();
    private volatile int _pendingSpeechRequests;
    private volatile VoiceSessionState _currentState = VoiceSessionState.Idle;
    private volatile RecognitionMode _activeRecognitionMode;
    private volatile RecognitionMode _desiredRecognitionMode;
    private long _desiredRevision;
    private long _recognitionGeneration;
    private long _speechGeneration;
    private long _speechStopEpoch;
    private bool _isVoiceNavigationEnabled;
    private TimeSpan _acousticDebounceInterval = TimeSpan.FromMilliseconds(250);
    private Action<string>? _activeDictationCallback;

    private readonly object _stopGate = new();
    private Task? _stopTask;
    private int _stopping;
    private int _disposed;
    private int _subscriptionsDetached;
    private readonly CancellationTokenSource _speechShutdown = new();
    private readonly object _ownedTasksGate = new();
    private readonly HashSet<Task> _speechTasks = [];
    private readonly HashSet<Task> _completionTasks = [];

    public VoiceSessionState CurrentState
    {
        get => _currentState;
        private set
        {
            if (_currentState == value) return;
            var previous = _currentState;
            _currentState = value;
            _logger?.LogInformation("Voice state: {Previous} -> {Current}", previous, value);
            StateChanged?.Invoke(this, new VoiceSessionStateChangedEventArgs(previous, value));
        }
    }

    public bool IsVoiceNavigationEnabled
    {
        get => _isVoiceNavigationEnabled;
        set
        {
            if (Volatile.Read(ref _stopping) != 0 || _isVoiceNavigationEnabled == value) return;
            _isVoiceNavigationEnabled = value;
            _settings.IsVoiceNavigationEnabled = value;
            _settings.Save();
            if (!value)
                TrackCompletion(StopVoiceNavigationAsync());
        }
    }

    public AudioCaptureHealth CaptureHealth => _deviceMonitor.CurrentHealth;

    public bool IsVoiceNavigationDesired => _desiredRecognitionMode == RecognitionMode.Command;

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
        _isVoiceNavigationEnabled = settings.IsVoiceNavigationEnabled;
        _transcriber.StateChanged += OnTranscriberStateChanged;
        _deviceMonitor.DeviceStatusChanged += OnDeviceStatusChanged;
    }

    public Task<VoiceRecognitionStartResult> RequestStartDictationAsync(
        Action<string> onFormattedChunk,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(onFormattedChunk);
        return RequestRecognitionAsync(RecognitionMode.Dictation, onFormattedChunk, ct);
    }

    public Task<VoiceRecognitionStartResult> StartVoiceNavigationAsync(CancellationToken ct = default) =>
        RequestRecognitionAsync(RecognitionMode.Command, null, ct);

    private async Task<VoiceRecognitionStartResult> RequestRecognitionAsync(
        RecognitionMode mode,
        Action<string>? dictationCallback,
        CancellationToken ct)
    {
        if (Volatile.Read(ref _stopping) != 0) return VoiceRecognitionStartResult.Unavailable;

        long desiredRevision;
        Task speechIdle;
        try
        {
            await _stateLock.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return VoiceRecognitionStartResult.Canceled;
        }

        try
        {
            if (Volatile.Read(ref _stopping) != 0) return VoiceRecognitionStartResult.Unavailable;
            _desiredRecognitionMode = mode;
            desiredRevision = ++_desiredRevision;
            _isVoiceNavigationEnabled = mode == RecognitionMode.Command;
            if (mode == RecognitionMode.Dictation)
                _activeDictationCallback = dictationCallback;
            else
                _activeDictationCallback = null;

            InvalidateRecognitionGeneration();
            if (_transcriber.IsRecording)
            {
                _activeRecognitionMode = RecognitionMode.None;
                await _transcriber.StopDictationAsync();
            }
            _activeRecognitionMode = RecognitionMode.None;

            lock (_speechQueueGate) speechIdle = _speechIdle.Task;
            if (_pendingSpeechRequests == 0)
                return await StartDesiredRecognitionUnderLockAsync(mode, desiredRevision, ct);
            CurrentState = VoiceSessionState.Paused;
        }
        finally
        {
            _stateLock.Release();
        }

        try
        {
            await speechIdle.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            await ClearDesiredModeIfCurrentAsync(mode, desiredRevision);
            return VoiceRecognitionStartResult.Canceled;
        }

        try { await _stateLock.WaitAsync(ct); }
        catch (OperationCanceledException)
        {
            await ClearDesiredModeIfCurrentAsync(mode, desiredRevision);
            return VoiceRecognitionStartResult.Canceled;
        }
        try
        {
            if (_desiredRecognitionMode != mode || _desiredRevision != desiredRevision)
                return VoiceRecognitionStartResult.Canceled;
            if (_pendingSpeechRequests != 0)
                return VoiceRecognitionStartResult.Canceled;
            return await StartDesiredRecognitionUnderLockAsync(mode, desiredRevision, ct);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task<VoiceRecognitionStartResult> StartDesiredRecognitionUnderLockAsync(
        RecognitionMode mode,
        long desiredRevision,
        CancellationToken ct)
    {
        if (_desiredRecognitionMode != mode || _desiredRevision != desiredRevision)
            return VoiceRecognitionStartResult.Canceled;

        AudioCaptureHealth health = _deviceMonitor.CurrentHealth;
        if (health != AudioCaptureHealth.Healthy)
        {
            if (_desiredRecognitionMode == mode) _desiredRecognitionMode = RecognitionMode.None;
            if (mode == RecognitionMode.Command) _isVoiceNavigationEnabled = false;
            CurrentState = VoiceSessionState.Disabled;
            return health == AudioCaptureHealth.PermissionDenied
                ? VoiceRecognitionStartResult.PermissionDenied
                : VoiceRecognitionStartResult.Unavailable;
        }

        long generation = ++_recognitionGeneration;
        var activation = new RecognitionActivation(generation, mode);
        lock (_recognitionLifecycleGate) _recognitionActivation = activation;
        lock (_recognitionCallbackGate) _dispatchedCommandSequences.Clear();
        Action<TranscriptionChunk> recognized = chunk => OnRecognitionChunk(generation, mode, chunk);
        VoiceRecognitionStartResult result = _transcriber is IRecognitionActivationSource nativeSource
            ? await nativeSource.StartRecognitionAsync(recognized,
                health => OnRecognitionCompleted(activation, health), ct)
            : await _transcriber.StartDictationAsync(recognized, ct);

        // Read the native state outside the lifecycle gate. Native notifications
        // take their session gate before ours, so the reverse lock order is forbidden.
        bool recording = _transcriber.IsRecording;
        BeforeRecognitionActivation?.Invoke();
        bool activated = false;
        void PublishActivation()
        {
            lock (_recognitionLifecycleGate)
            {
                if (result != VoiceRecognitionStartResult.Started || !recording ||
                    !ReferenceEquals(_recognitionActivation, activation) ||
                    activation.State != ActivationState.Pending ||
                    generation != Volatile.Read(ref _recognitionGeneration) ||
                    _desiredRecognitionMode != mode || ct.IsCancellationRequested ||
                    Volatile.Read(ref _stopping) != 0) return;
                activation.State = ActivationState.Active;
                _activeRecognitionMode = mode;
                CurrentState = mode == RecognitionMode.Dictation
                    ? VoiceSessionState.Dictating
                    : VoiceSessionState.ListeningForCommand;
                activated = activation.State == ActivationState.Active;
            }
        }
        if (result == VoiceRecognitionStartResult.Started && _transcriber is IRecognitionActivationSource source)
        {
            // Lock order is native session -> coordinator generation. Completion
            // uses that same order, closing even the physical-stop/notification gap.
            if (!source.TryActivateRecognition(PublishActivation))
            {
                activated = false;
                lock (_recognitionLifecycleGate)
                {
                    activation.State = ActivationState.Completed;
                    _activeRecognitionMode = RecognitionMode.None;
                }
            }
        }
        else
        {
            // Compatibility adapters report completion through StateChanged. Its
            // synchronous pending-generation invalidation still serializes here.
            PublishActivation();
        }
        if (activated) return result;
        bool completionWon;
        lock (_recognitionLifecycleGate) completionWon = activation.State == ActivationState.Completed;

        if (result == VoiceRecognitionStartResult.Started)
            result = ct.IsCancellationRequested
                ? VoiceRecognitionStartResult.Canceled : VoiceRecognitionStartResult.Failed;

        InvalidateRecognitionGeneration();
        if (recording && !completionWon) await _transcriber.StopDictationAsync();
        if (_desiredRecognitionMode == mode && _desiredRevision == desiredRevision)
        {
            _desiredRecognitionMode = RecognitionMode.None;
            _desiredRevision++;
            _activeDictationCallback = null;
        }
        if (mode == RecognitionMode.Command) _isVoiceNavigationEnabled = false;
        CurrentState = completionWon
            ? (_transcriber.DeviceHealth == AudioCaptureHealth.Healthy
                ? VoiceSessionState.Idle : VoiceSessionState.Disabled)
            : result switch
        {
            VoiceRecognitionStartResult.PermissionDenied or VoiceRecognitionStartResult.Unavailable => VoiceSessionState.Disabled,
            VoiceRecognitionStartResult.Failed => VoiceSessionState.Error,
            _ => VoiceSessionState.Idle
        };
        return result;
    }

    private void OnRecognitionChunk(long generation, RecognitionMode mode, TranscriptionChunk chunk)
    {
        if (Volatile.Read(ref _stopping) != 0 ||
            generation != Volatile.Read(ref _recognitionGeneration) ||
            _activeRecognitionMode != mode) return;

        if (mode == RecognitionMode.Dictation)
        {
            string formatted = _settings.IsAutoPunctuationEnabled
                ? _textFormatter.FormatSpokenChunk(chunk.RawText)
                : chunk.RawText;
            if (string.IsNullOrWhiteSpace(formatted)) return;
            try { _activeDictationCallback?.Invoke(formatted); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Dictation consumer rejected a recognized chunk."); }
            return;
        }

        if (!chunk.IsFinal || string.IsNullOrWhiteSpace(chunk.FormattedText)) return;
        long sequence = chunk.SequenceNumber;
        lock (_recognitionCallbackGate)
        {
            if (sequence <= 0) sequence = 0;
            if (!_dispatchedCommandSequences.Add(sequence)) return;
        }

        TrackCompletion(DispatchCommandIfCurrentAsync(generation, chunk.FormattedText));
    }

    private async Task DispatchCommandIfCurrentAsync(long generation, string spokenText)
    {
        if (generation != Volatile.Read(ref _recognitionGeneration) ||
            _activeRecognitionMode != RecognitionMode.Command) return;
        await _commandRouter.ExecuteCommandAsync(spokenText, _speechShutdown.Token);
    }

    public async Task RequestStopDictationAsync()
    {
        await StopRecognitionModeAsync(RecognitionMode.Dictation);
    }

    public async Task StopVoiceNavigationAsync()
    {
        _isVoiceNavigationEnabled = false;
        await StopRecognitionModeAsync(RecognitionMode.Command);
    }

    private async Task StopRecognitionModeAsync(RecognitionMode mode)
    {
        if (Volatile.Read(ref _stopping) != 0) return;
        await _stateLock.WaitAsync();
        try
        {
            bool desiredMatches = _desiredRecognitionMode == mode;
            if (_desiredRecognitionMode == mode)
            {
                _desiredRecognitionMode = RecognitionMode.None;
                _desiredRevision++;
            }
            if (mode == RecognitionMode.Command) _isVoiceNavigationEnabled = false;
            bool activeMatches = _activeRecognitionMode == mode;
            if (mode == RecognitionMode.Dictation && (desiredMatches || activeMatches))
                _activeDictationCallback = null;
            if (activeMatches)
            {
                InvalidateRecognitionGeneration();
                _activeRecognitionMode = RecognitionMode.None;
                await _transcriber.StopDictationAsync();
            }
            if (activeMatches && _pendingSpeechRequests == 0)
                CurrentState = VoiceSessionState.Idle;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to stop recognition mode {Mode}.", mode);
            CurrentState = VoiceSessionState.Idle;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public Task<SpeechPlaybackResult> RequestSpeakAsync(
        string text,
        double? pitch = null,
        double? rate = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(SpeechPlaybackResult.Failed);
        var publication = new TaskCompletionSource<SpeechPlaybackResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_ownedTasksGate)
        {
            if (Volatile.Read(ref _stopping) != 0)
                return Task.FromResult(SpeechPlaybackResult.Unavailable);
            _speechTasks.Add(publication.Task);
        }
        _ = RunPublishedSpeechAsync(publication, text, pitch, rate, ct);
        return publication.Task;
    }

    private async Task RunPublishedSpeechAsync(
        TaskCompletionSource<SpeechPlaybackResult> publication,
        string text,
        double? pitch,
        double? rate,
        CancellationToken ct)
    {
        try
        {
            publication.TrySetResult(await RequestSpeakCoreAsync(text, pitch, rate, ct));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Published speech operation failed unexpectedly.");
            publication.TrySetResult(SpeechPlaybackResult.Failed);
        }
        finally
        {
            lock (_ownedTasksGate) _speechTasks.Remove(publication.Task);
        }
    }

    private async Task<SpeechPlaybackResult> RequestSpeakCoreAsync(
        string text,
        double? pitch,
        double? rate,
        CancellationToken callerToken)
    {
        SpeechAdmission? admission = EnqueueSpeech();
        if (admission == null)
        {
            _logger?.LogWarning("Speech request rejected because the bounded queue is full.");
            return SpeechPlaybackResult.Failed;
        }

        long stopEpoch = Volatile.Read(ref _speechStopEpoch);
        bool admitted = false;
        bool isLast = false;
        bool canPlay = true;
        SpeechPlaybackResult result = SpeechPlaybackResult.Canceled;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, _speechShutdown.Token);
        try
        {
            try
            {
                await admission.Granted.Task.WaitAsync(linked.Token);
                admitted = true;
            }
            catch (OperationCanceledException)
            {
                canPlay = false;
            }

            if (!admitted || stopEpoch != Volatile.Read(ref _speechStopEpoch) ||
                Volatile.Read(ref _stopping) != 0)
                canPlay = false;

            if (canPlay)
            {
                long generation;
                await _stateLock.WaitAsync(linked.Token);
                try
                {
                    generation = ++_speechGeneration;
                    if (_transcriber.IsRecording || _activeRecognitionMode != RecognitionMode.None)
                    {
                        InvalidateRecognitionGeneration();
                        _activeRecognitionMode = RecognitionMode.None;
                        await _transcriber.StopDictationAsync();
                        CurrentState = VoiceSessionState.Paused;
                    }
                }
                finally
                {
                    _stateLock.Release();
                }

                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                void PlaybackChanged(object? sender, SpeechPlaybackStateChangedEventArgs args)
                {
                    if (args.IsSpeaking) started.TrySetResult();
                }

                _speechService.PlaybackStateChanged += PlaybackChanged;
                try
                {
                    Task<SpeechPlaybackResult> playback = _speechService.SpeakTextAsync(
                        text,
                        pitch ?? _settings.SpeechPitch,
                        rate ?? _settings.SpeechRate,
                        linked.Token);

                    Task first = await Task.WhenAny(started.Task, playback);
                    if (ReferenceEquals(first, started.Task))
                    {
                        await _stateLock.WaitAsync();
                        try
                        {
                            if (generation == Volatile.Read(ref _speechGeneration) &&
                                stopEpoch == Volatile.Read(ref _speechStopEpoch))
                                CurrentState = VoiceSessionState.Synthesizing;
                        }
                        finally
                        {
                            _stateLock.Release();
                        }
                    }
                    result = await playback;
                }
                finally
                {
                    _speechService.PlaybackStateChanged -= PlaybackChanged;
                }
            }
        }
        catch (OperationCanceledException)
        {
            result = SpeechPlaybackResult.Canceled;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Coordinated speech request failed.");
            result = SpeechPlaybackResult.Failed;
        }
        finally
        {
            isLast = DequeueSpeech(admission);
        }

        if (isLast)
            await ResumeDesiredRecognitionAfterSpeechAsync();
        return result;
    }

    public void RequestStopSpeech()
    {
        if (Volatile.Read(ref _stopping) != 0) return;
        Interlocked.Increment(ref _speechStopEpoch);
        _speechService.Stop();
    }

    private async Task ResumeDesiredRecognitionAfterSpeechAsync()
    {
        try
        {
            if (_acousticDebounceInterval > TimeSpan.Zero)
                await Task.Delay(_acousticDebounceInterval, _speechShutdown.Token);

            await _stateLock.WaitAsync(_speechShutdown.Token);
            try
            {
                if (Volatile.Read(ref _stopping) != 0 || _pendingSpeechRequests != 0) return;
                RecognitionMode desired = _desiredRecognitionMode;
                long revision = _desiredRevision;
                if (desired == RecognitionMode.None)
                {
                    CurrentState = _deviceMonitor.CurrentHealth == AudioCaptureHealth.Healthy
                        ? VoiceSessionState.Idle
                        : VoiceSessionState.Disabled;
                    return;
                }
                await StartDesiredRecognitionUnderLockAsync(desired, revision, _speechShutdown.Token);
            }
            finally
            {
                _stateLock.Release();
            }
        }
        catch (OperationCanceledException) when (_speechShutdown.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to resume the desired recognition mode after speech.");
            CurrentState = VoiceSessionState.Idle;
        }
    }

    private SpeechAdmission? EnqueueSpeech()
    {
        lock (_speechQueueGate)
        {
            if (Volatile.Read(ref _stopping) != 0 ||
                _speechQueue.Count == MaxPendingSpeechRequests) return null;
            var admission = new SpeechAdmission();
            admission.Node = _speechQueue.AddLast(admission);
            if (_pendingSpeechRequests++ == 0)
                _speechIdle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_speechQueue.First == admission.Node)
                admission.Granted.TrySetResult();
            return admission;
        }
    }

    private bool DequeueSpeech(SpeechAdmission admission)
    {
        lock (_speechQueueGate)
        {
            bool wasHead = _speechQueue.First == admission.Node;
            if (admission.Node != null)
            {
                _speechQueue.Remove(admission.Node);
                admission.Node = null;
            }
            if (wasHead) _speechQueue.First?.Value.Granted.TrySetResult();
            _pendingSpeechRequests--;
            if (_pendingSpeechRequests != 0) return false;
            _speechIdle.TrySetResult();
            return true;
        }
    }

    private static TaskCompletionSource CompletedSignal()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult();
        return signal;
    }

    private async Task ClearDesiredModeIfCurrentAsync(RecognitionMode mode, long revision)
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_desiredRecognitionMode == mode && _desiredRevision == revision)
            {
                _desiredRecognitionMode = RecognitionMode.None;
                _desiredRevision++;
                if (mode == RecognitionMode.Dictation) _activeDictationCallback = null;
                if (mode == RecognitionMode.Command) _isVoiceNavigationEnabled = false;
            }
        }
        finally { _stateLock.Release(); }
    }

    private void InvalidateRecognitionGeneration()
    {
        lock (_recognitionLifecycleGate)
        {
            if (_recognitionActivation != null) _recognitionActivation.State = ActivationState.Completed;
            _recognitionActivation = null;
            _activeRecognitionMode = RecognitionMode.None;
            Interlocked.Increment(ref _recognitionGeneration);
        }
        lock (_recognitionCallbackGate) _dispatchedCommandSequences.Clear();
    }

    private void OnDeviceStatusChanged(object? sender, AudioDeviceStatusChangedEventArgs args)
    {
        if (Volatile.Read(ref _stopping) != 0) return;
        if (args.Health == AudioCaptureHealth.Healthy) return;
        TrackCompletion(HandleCaptureLossAsync(args.Health));
    }

    private void OnTranscriberStateChanged(object? sender, VoiceTranscriberStateChangedEventArgs args)
    {
        if (Volatile.Read(ref _stopping) != 0 || args.IsRecording) return;
        // The physical adapter supplies a completion closure for the exact pending
        // generation. Its untagged compatibility event must not complete a newer one.
        if (_transcriber is IRecognitionActivationSource) return;
        RecognitionActivation activation;
        lock (_recognitionLifecycleGate)
        {
            if (_recognitionActivation == null ||
                _recognitionActivation.State == ActivationState.Completed) return;
            activation = _recognitionActivation;
        }
        OnRecognitionCompleted(activation, args.Health);
    }

    private void OnRecognitionCompleted(RecognitionActivation activation, AudioCaptureHealth health)
    {
        if (Volatile.Read(ref _stopping) != 0) return;
        lock (_recognitionLifecycleGate)
        {
            if (!ReferenceEquals(_recognitionActivation, activation) ||
                activation.State == ActivationState.Completed) return;
            activation.State = ActivationState.Completed;
            _activeRecognitionMode = RecognitionMode.None;
        }
        TrackCompletion(HandleUnexpectedRecognitionStopAsync(activation, health));
    }

    private async Task HandleUnexpectedRecognitionStopAsync(
        RecognitionActivation activation,
        AudioCaptureHealth health)
    {
        await _stateLock.WaitAsync();
        try
        {
            if (Volatile.Read(ref _stopping) != 0 ||
                activation.Generation != Volatile.Read(ref _recognitionGeneration) ||
                !ReferenceEquals(_recognitionActivation, activation)) return;

            RecognitionMode mode = activation.Mode;
            InvalidateRecognitionGeneration();
            _activeRecognitionMode = RecognitionMode.None;
            if (_desiredRecognitionMode == mode)
            {
                _desiredRecognitionMode = RecognitionMode.None;
                _desiredRevision++;
            }
            if (mode == RecognitionMode.Command) _isVoiceNavigationEnabled = false;
            if (mode == RecognitionMode.Dictation)
                _activeDictationCallback = null;
            CurrentState = health == AudioCaptureHealth.Healthy
                ? VoiceSessionState.Idle
                : VoiceSessionState.Disabled;
            _logger?.LogWarning(
                "Recognition generation {Generation} ended outside a coordinator stop; state was reconciled.",
                activation.Generation);
        }
        finally { _stateLock.Release(); }
    }

    private async Task HandleCaptureLossAsync(AudioCaptureHealth health)
    {
        await _stateLock.WaitAsync();
        try
        {
            if (Volatile.Read(ref _stopping) != 0) return;
            bool physicallyActive = _activeRecognitionMode != RecognitionMode.None || _transcriber.IsRecording;
            if (!physicallyActive && _desiredRecognitionMode == RecognitionMode.None) return;
            InvalidateRecognitionGeneration();
            _activeRecognitionMode = RecognitionMode.None;
            _desiredRecognitionMode = RecognitionMode.None;
            _desiredRevision++;
            _activeDictationCallback = null;
            _isVoiceNavigationEnabled = false;
            if (physicallyActive) await _transcriber.StopDictationAsync();
            CurrentState = VoiceSessionState.Disabled;
            _logger?.LogWarning("Recognition intent invalidated after capture health changed to {Health}.", health);
        }
        finally { _stateLock.Release(); }
    }

    public Task StopAsync()
    {
        TaskCompletionSource? publication = null;
        lock (_stopGate)
        {
            if (_stopTask != null) return _stopTask;
            publication = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _stopTask = publication.Task;
            lock (_ownedTasksGate) Interlocked.Exchange(ref _stopping, 1);
        }

        var errors = new List<Exception>();
        try { _speechShutdown.Cancel(); } catch (Exception ex) { errors.Add(ex); }
        Interlocked.Increment(ref _speechStopEpoch);
        try { DetachSubscriptions(); } catch (Exception ex) { errors.Add(ex); }
        try { _speechService.Stop(); } catch (Exception ex) { errors.Add(ex); }

        _ = CompletePublishedStopAsync(publication, errors);
        return publication.Task;
    }

    private async Task CompletePublishedStopAsync(
        TaskCompletionSource publication,
        List<Exception> errors)
    {
        try
        {
            // The owned task is published and speech admission/cancellation is closed
            // before native recognition teardown, which may block before returning a Task.
            await Task.Yield();
            await StopCoreAsync(errors);
            publication.TrySetResult();
        }
        catch (Exception ex)
        {
            publication.TrySetException(ex);
        }
    }

    private async Task StopCoreAsync(List<Exception> errors)
    {
        await _stateLock.WaitAsync();
        try
        {
            _desiredRecognitionMode = RecognitionMode.None;
            _desiredRevision++;
            InvalidateRecognitionGeneration();
            _activeRecognitionMode = RecognitionMode.None;
            _activeDictationCallback = null;
            try { await _transcriber.StopDictationAsync(); } catch (Exception ex) { errors.Add(ex); }
            try { CurrentState = VoiceSessionState.Idle; }
            catch (Exception ex) { _currentState = VoiceSessionState.Idle; errors.Add(ex); }
        }
        finally { _stateLock.Release(); }

        Task[] pending;
        lock (_ownedTasksGate) pending = _speechTasks.Concat(_completionTasks).ToArray();
        try { await Task.WhenAll(pending); } catch (Exception ex) { errors.Add(ex); }
        if (errors.Count > 0)
            throw new AggregateException("Voice operational stop had one or more failures.", errors);
    }

    private void TrackCompletion(Task task)
    {
        lock (_ownedTasksGate)
        {
            if (Volatile.Read(ref _stopping) != 0) return;
            _completionTasks.Add(task);
        }
        _ = task.ContinueWith(completed =>
        {
            lock (_ownedTasksGate) _completionTasks.Remove(completed);
            if (completed.IsFaulted)
                _logger?.LogWarning(completed.Exception, "Tracked voice callback failed.");
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void DetachSubscriptions()
    {
        if (Interlocked.Exchange(ref _subscriptionsDetached, 1) != 0) return;
        _transcriber.StateChanged -= OnTranscriberStateChanged;
        _deviceMonitor.DeviceStatusChanged -= OnDeviceStatusChanged;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Interlocked.Exchange(ref _stopping, 1);
        DetachSubscriptions();
        _activeDictationCallback = null;
        StateChanged = null;
        _currentState = VoiceSessionState.Idle;
        if (_stopTask?.IsCompletedSuccessfully == true)
        {
            _speechShutdown.Dispose();
            _stateLock.Dispose();
        }
    }
}
