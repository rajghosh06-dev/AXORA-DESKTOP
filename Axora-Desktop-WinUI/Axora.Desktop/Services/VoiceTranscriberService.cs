using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.SpeechRecognition;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models.Voice;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

// The physical adapter exposes its session gate without changing the public
// transcriber contract. Coordinator activation must compete with native completion
// at this gate as well as at its own Pending/Active/Completed generation gate.
internal interface IRecognitionActivationSource
{
    Task<VoiceRecognitionStartResult> StartRecognitionAsync(
        Action<TranscriptionChunk> callback, Action<AudioCaptureHealth> completed, CancellationToken ct);
    bool TryActivateRecognition(Action activation);
}

/// <summary>
/// Owns the single physical Windows speech-recognition session used by the coordinator.
/// Startup returns a truthful typed outcome and only one callback owns each session.
/// </summary>
public sealed class VoiceTranscriberService : IVoiceTranscriberService, IRecognitionActivationSource
{
    // WinRT exposes no documented result ID. The native result object is the
    // adapter identity; weak keys avoid retaining every utterance for a session.
    internal sealed class RecognitionSession
    {
        private sealed class ResultNumber(long number) { public long Number { get; } = number; }
        private readonly object _gate = new();
        private readonly ConditionalWeakTable<object, ResultNumber> _resultNumbers = new();
        private Action<TranscriptionChunk>? _callback;
        private Action? _completed;
        private long _nextNumber;
        private int _state; // 0 starting, 1 recording, 2 completed

        public RecognitionSession(Action<TranscriptionChunk> callback, Action? completed = null)
        {
            _callback = callback;
            _completed = completed;
        }
        public bool IsRecording { get { lock (_gate) return _state == 1; } }

        public async Task<bool> StartAsync(Func<Task> nativeStart)
        {
            var publication = new TaskCompletionSource<VoiceRecognitionStartResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            await StartAsync(nativeStart, publication, () => { });
            return await publication.Task == VoiceRecognitionStartResult.Started;
        }

        // Both the public start Task and the recording notification are published
        // under the same gate as completion. The optional boundary lets tests stop
        // the production session after native startup but before this transition.
        public async Task StartAsync(
            Func<Task> nativeStart,
            TaskCompletionSource<VoiceRecognitionStartResult> publication,
            Action publishStarted,
            Func<Task>? beforePublication = null)
        {
            await nativeStart();
            if (beforePublication != null) await beforePublication();
            lock (_gate)
            {
                if (_state != 0)
                {
                    publication.TrySetResult(VoiceRecognitionStartResult.Unavailable);
                    return;
                }
                _state = 1;
                publishStarted();
                // A subscriber may complete the session synchronously/reentrantly.
                publication.TrySetResult(_state == 1
                    ? VoiceRecognitionStartResult.Started
                    : VoiceRecognitionStartResult.Unavailable);
            }
        }

        public void Complete(Action? publishCompleted = null)
        {
            lock (_gate)
            {
                if (_state == 2) return;
                _state = 2;
                _callback = null;
                Action? completed = _completed;
                _completed = null;
                completed?.Invoke();
                publishCompleted?.Invoke();
            }
        }

        public bool TryActivate(Action activation)
        {
            lock (_gate)
            {
                if (_state != 1) return false;
                activation();
                return _state == 1;
            }
        }

        public void Deliver(object nativeResult, string text, bool isFinal)
        {
            Action<TranscriptionChunk>? callback;
            long number;
            lock (_gate)
            {
                if (_state != 1 || string.IsNullOrWhiteSpace(text)) return;
                callback = _callback;
                number = _resultNumbers.GetValue(nativeResult,
                    _ => new ResultNumber(++_nextNumber)).Number;
            }
            callback?.Invoke(new TranscriptionChunk(text, text, isFinal, number));
        }
    }

    private readonly ILogger<VoiceTranscriberService>? _logger;
    private readonly SemaphoreSlim _startStopLock = new(1, 1);
    private readonly object _callbackGate = new();
    private SpeechRecognizer? _recognizer;
    private RecognitionSession? _session;
    private AudioCaptureHealth _deviceHealth = AudioCaptureHealth.Healthy;
    private int _disposed;

    public bool IsRecording => Volatile.Read(ref _session)?.IsRecording == true;
    public AudioCaptureHealth DeviceHealth => _deviceHealth;

    bool IRecognitionActivationSource.TryActivateRecognition(Action activation) =>
        Volatile.Read(ref _session)?.TryActivate(activation) == true;

    Task<VoiceRecognitionStartResult> IRecognitionActivationSource.StartRecognitionAsync(
        Action<TranscriptionChunk> callback, Action<AudioCaptureHealth> completed, CancellationToken ct) =>
        StartPublishedRecognition(callback, ct, completed);

    public event EventHandler<VoiceTranscriberStateChangedEventArgs>? StateChanged;

    public VoiceTranscriberService(ILogger<VoiceTranscriberService>? logger = null)
    {
        _logger = logger;
    }

    public Task<bool> CheckPrerequisitesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var systemLanguage = SpeechRecognizer.SystemSpeechLanguage;
            bool supported = SpeechRecognizer.SupportedTopicLanguages.Contains(systemLanguage);
            _deviceHealth = supported
                ? AudioCaptureHealth.Healthy
                : AudioCaptureHealth.RecognitionUnavailable;
            if (!supported)
                _logger?.LogWarning("System speech language {Language} is unavailable.", systemLanguage.DisplayName);
            return Task.FromResult(supported);
        }
        catch (UnauthorizedAccessException)
        {
            _deviceHealth = AudioCaptureHealth.PermissionDenied;
            return Task.FromResult(false);
        }
        catch (Exception ex) when ((uint)ex.HResult == 0x80070005)
        {
            _deviceHealth = AudioCaptureHealth.PermissionDenied;
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check speech-recognition prerequisites.");
            _deviceHealth = AudioCaptureHealth.RecognitionUnavailable;
            return Task.FromResult(false);
        }
    }

    public Task<VoiceRecognitionStartResult> StartDictationAsync(
        Action<TranscriptionChunk> onChunkRecognized,
        CancellationToken ct = default) => StartPublishedRecognition(onChunkRecognized, ct, null);

    private Task<VoiceRecognitionStartResult> StartPublishedRecognition(
        Action<TranscriptionChunk> onChunkRecognized,
        CancellationToken ct,
        Action<AudioCaptureHealth>? completed)
    {
        ArgumentNullException.ThrowIfNull(onChunkRecognized);
        var publication = new TaskCompletionSource<VoiceRecognitionStartResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _ = RunPublishedStartAsync(onChunkRecognized, ct, publication, completed);
        return publication.Task;
    }

    private async Task RunPublishedStartAsync(
        Action<TranscriptionChunk> callback,
        CancellationToken ct,
        TaskCompletionSource<VoiceRecognitionStartResult> publication,
        Action<AudioCaptureHealth>? completed)
    {
        try { publication.TrySetResult(await StartDictationCoreAsync(callback, ct, publication, completed)); }
        catch (Exception ex) { publication.TrySetException(ex); }
    }

    private async Task<VoiceRecognitionStartResult> StartDictationCoreAsync(
        Action<TranscriptionChunk> onChunkRecognized,
        CancellationToken ct,
        TaskCompletionSource<VoiceRecognitionStartResult> publication,
        Action<AudioCaptureHealth>? completed)
    {
        if (Volatile.Read(ref _disposed) != 0) return VoiceRecognitionStartResult.Unavailable;

        try
        {
            await _startStopLock.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return VoiceRecognitionStartResult.Canceled;
        }

        try
        {
            if (IsRecording)
            {
                _logger?.LogWarning("Rejected a second recognition owner while a session is active.");
                return VoiceRecognitionStartResult.Failed;
            }
            if (_recognizer != null)
                DisposeRecognizer();

            ct.ThrowIfCancellationRequested();
            var language = SpeechRecognizer.SystemSpeechLanguage;
            _recognizer = new SpeechRecognizer(language);
            _recognizer.Constraints.Add(new SpeechRecognitionTopicConstraint(
                SpeechRecognitionScenario.Dictation, "AxoraVoice"));

            var compilation = await _recognizer.CompileConstraintsAsync();
            ct.ThrowIfCancellationRequested();
            if (compilation.Status != SpeechRecognitionResultStatus.Success)
            {
                _deviceHealth = AudioCaptureHealth.RecognitionUnavailable;
                _logger?.LogWarning("Speech-recognition constraint compilation failed: {Status}", compilation.Status);
                DisposeRecognizer();
                PublishState(false);
                return VoiceRecognitionStartResult.Unavailable;
            }

            var session = new RecognitionSession(onChunkRecognized, () => completed?.Invoke(_deviceHealth));
            lock (_callbackGate) _session = session;
            _recognizer.ContinuousRecognitionSession.ResultGenerated += OnResultGenerated;
            _recognizer.ContinuousRecognitionSession.Completed += OnSessionCompleted;
            await session.StartAsync(
                async () => await _recognizer.ContinuousRecognitionSession.StartAsync(),
                publication,
                () =>
                {
                    ct.ThrowIfCancellationRequested();
                    _deviceHealth = AudioCaptureHealth.Healthy;
                    _logger?.LogInformation("Voice recognition started ({Language}).", language.DisplayName);
                    PublishState(true);
                });
            VoiceRecognitionStartResult result = await publication.Task;
            if (result != VoiceRecognitionStartResult.Started)
            {
                await StopRecognizerAfterFailedStartAsync();
                PublishState(false);
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            await StopRecognizerAfterFailedStartAsync();
            PublishState(false);
            return VoiceRecognitionStartResult.Canceled;
        }
        catch (UnauthorizedAccessException ex)
        {
            return await HandleStartFailureAsync(ex, AudioCaptureHealth.PermissionDenied,
                VoiceRecognitionStartResult.PermissionDenied);
        }
        catch (Exception ex) when ((uint)ex.HResult == 0x80070005)
        {
            return await HandleStartFailureAsync(ex, AudioCaptureHealth.PermissionDenied,
                VoiceRecognitionStartResult.PermissionDenied);
        }
        catch (Exception ex)
        {
            return await HandleStartFailureAsync(ex, AudioCaptureHealth.RecognitionUnavailable,
                VoiceRecognitionStartResult.Failed);
        }
        finally
        {
            _startStopLock.Release();
        }
    }

    public async Task StopDictationAsync()
    {
        await _startStopLock.WaitAsync();
        try
        {
            SpeechRecognizer? recognizer = _recognizer;
            if (recognizer == null)
            {
                Volatile.Read(ref _session)?.Complete();
                ClearCallback();
                return;
            }

            recognizer.ContinuousRecognitionSession.ResultGenerated -= OnResultGenerated;
            recognizer.ContinuousRecognitionSession.Completed -= OnSessionCompleted;
            try
            {
                if (IsRecording)
                    await recognizer.ContinuousRecognitionSession.StopAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error stopping the speech-recognition session.");
            }
            finally
            {
                Volatile.Read(ref _session)?.Complete();
                ClearCallback();
                DisposeRecognizer();
                PublishState(false);
            }
        }
        finally
        {
            _startStopLock.Release();
        }
    }

    private async Task<VoiceRecognitionStartResult> HandleStartFailureAsync(
        Exception exception,
        AudioCaptureHealth health,
        VoiceRecognitionStartResult result)
    {
        _deviceHealth = health;
        await StopRecognizerAfterFailedStartAsync();
        _logger?.LogWarning(exception, "Failed to start speech recognition ({Result}).", result);
        PublishState(false);
        return result;
    }

    private async Task StopRecognizerAfterFailedStartAsync()
    {
        SpeechRecognizer? recognizer = _recognizer;
        if (recognizer != null)
        {
            recognizer.ContinuousRecognitionSession.ResultGenerated -= OnResultGenerated;
            recognizer.ContinuousRecognitionSession.Completed -= OnSessionCompleted;
            if (IsRecording)
            {
                try { await recognizer.ContinuousRecognitionSession.StopAsync(); }
                catch (Exception ex) { _logger?.LogDebug(ex, "Recognition stop after failed start was unavailable."); }
            }
        }
        Volatile.Read(ref _session)?.Complete();
        ClearCallback();
        DisposeRecognizer();
    }

    private void OnResultGenerated(
        SpeechContinuousRecognitionSession sender,
        SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        RecognitionSession? session;
        lock (_callbackGate)
        {
            if (_recognizer == null ||
                !ReferenceEquals(sender, _recognizer.ContinuousRecognitionSession)) return;
            session = _session;
        }

        var result = args.Result;
        session?.Deliver(result, result.Text,
            result.Status == SpeechRecognitionResultStatus.Success);
    }

    private void OnSessionCompleted(
        SpeechContinuousRecognitionSession sender,
        SpeechContinuousRecognitionCompletedEventArgs args)
    {
        RecognitionSession? session;
        lock (_callbackGate)
        {
            if (_recognizer == null ||
                !ReferenceEquals(sender, _recognizer.ContinuousRecognitionSession)) return;
            session = _session;
        }
        session?.Complete(() =>
        {
            lock (_callbackGate)
            {
                if (!ReferenceEquals(_session, session)) return;
                _session = null;
                _logger?.LogInformation("Continuous recognition completed: {Status}", args.Status);
                PublishState(false);
            }
        });
    }

    private bool ClearCallback(RecognitionSession? expected = null)
    {
        RecognitionSession? removed;
        lock (_callbackGate)
        {
            if (expected != null && !ReferenceEquals(_session, expected)) return false;
            removed = _session;
            _session = null;
        }
        // Never take the session gate while holding the owner gate: completion
        // publishes under the session gate and removes only that session's owner.
        removed?.Complete();
        return true;
    }

    private void PublishState(bool recording) =>
        StateChanged?.Invoke(this, new VoiceTranscriberStateChangedEventArgs(recording, _deviceHealth));

    private void DisposeRecognizer()
    {
        SpeechRecognizer? recognizer = _recognizer;
        _recognizer = null;
        if (recognizer == null) return;
        try
        {
            recognizer.ContinuousRecognitionSession.ResultGenerated -= OnResultGenerated;
            recognizer.ContinuousRecognitionSession.Completed -= OnSessionCompleted;
            recognizer.Dispose();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error disposing SpeechRecognizer.");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Volatile.Read(ref _session)?.Complete();
        ClearCallback();
        DisposeRecognizer();
        StateChanged = null;
        _startStopLock.Dispose();
    }
}
