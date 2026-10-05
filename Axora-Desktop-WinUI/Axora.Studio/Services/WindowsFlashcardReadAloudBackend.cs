using System.Diagnostics;
using Axora.Studio.Services.Contracts;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace Axora.Studio.Services;

/// <summary>Instance-injected adapter seam; deterministic tests exercise the production cleanup driver.</summary>
public interface IReadAloudNativeRequest
{
    Task SynthesizeAsync(string text);
    bool HasUsableStream { get; }
    void CancelSynthesis();
    void Play(Action started, Action ended, Action failed);
    void DetachHandlers();
    void PauseAndClearSource();
    void ReleasePlayer();
    void ReleaseSource();
    void ReleaseStream();
    void ReleaseSynthesizer();
}

public sealed class ReadAloudNativeUnavailableException : Exception
{
    public ReadAloudNativeUnavailableException(int hresult) { HResult = hresult; }
    public override string Message => "NativeUnavailable";
}

public sealed class WindowsFlashcardReadAloudBackend : IFlashcardReadAloudBackend
{
    private readonly Func<IReadAloudNativeRequest> _create;
    private readonly Func<Task> _retryCleanup;
    private readonly Action<string>? _log;
    private IReadAloudNativeRequest? _retainedNative; // Exceptional settlement cannot lose the exact native cleanup owner.
    public WindowsFlashcardReadAloudBackend(Action<string>? log = null,
        Func<IReadAloudNativeRequest>? create = null, Func<Task>? retryCleanup = null)
    {
        _create = create ?? (() => new WindowsRequest());
        _retryCleanup = retryCleanup ?? (() => Task.Delay(250));
        _log = log;
    }
    public Task<ReadAloudResult> RunAsync(ReadAloudRequest request, CancellationToken cancellation,
        Func<bool> mayPlay, Action<ReadAloudProgress> progress)
        => Task.Run(() => RunNativeAsync(request, cancellation, mayPlay, progress)); // Returned task owns every native continuation.

    private void Log(Guid id, string phase, Stopwatch timer, Exception? error = null)
    {
        try { _log?.Invoke($"ReadAloud operation={id:N}; phase={phase}; elapsedMs={timer.Elapsed.TotalMilliseconds:0.00}"
            + (error is null ? "" : $"; exceptionType={error.GetType().Name}; hresult={error.HResult}")); }
        catch (Exception) { /* Optional diagnostics never affect cleanup. */ }
    }
    private static bool IsUnavailable(Exception error) => error is ReadAloudNativeUnavailableException or NotSupportedException
        or TypeLoadException or DllNotFoundException or EntryPointNotFoundException || error.HResult == unchecked((int)0x80040154);
    private async Task<ReadAloudResult> RunNativeAsync(ReadAloudRequest request, CancellationToken cancellation,
        Func<bool> mayPlay, Action<ReadAloudProgress> progress)
    {
        var timer = Stopwatch.StartNew();
        var terminal = new TaskCompletionSource<ReadAloudOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellation.Register(() => terminal.TrySetResult(ReadAloudOutcome.Canceled));
        IReadAloudNativeRequest? native = null;
        ReadAloudOutcome outcome = ReadAloudOutcome.Canceled;
        string reason = "Canceled";
        try
        {
            if (cancellation.IsCancellationRequested || !mayPlay()) return new(request.OperationId, outcome, reason);
            try { native = _create(); }
            catch (Exception ex) { Log(request.OperationId, "Unavailable", timer, ex); return new(request.OperationId, ReadAloudOutcome.Unavailable, "NativeUnavailable"); }
            Log(request.OperationId, "NativeCreated", timer);
            _retainedNative = native;
            progress(new(request.OperationId, ReadAloudPhase.Preparing));
            Task synthesis;
            try { synthesis = native.SynthesizeAsync(request.Text); }
            catch (Exception ex) { Log(request.OperationId, "SynthesisFailed", timer, ex); outcome = IsUnavailable(ex) ? ReadAloudOutcome.Unavailable : ReadAloudOutcome.SynthesisFailed; reason = "SynthesisStartFailed"; synthesis = Task.CompletedTask; }
            if (outcome is not (ReadAloudOutcome.SynthesisFailed or ReadAloudOutcome.Unavailable))
            {
                Log(request.OperationId, "SynthesisStarted", timer);
                if (await Task.WhenAny(synthesis, terminal.Task).ConfigureAwait(false) != synthesis)
                {
                    try { native.CancelSynthesis(); }
                    catch (Exception ex) { Log(request.OperationId, "CancelRequestedFailed", timer, ex); }
                }
                try
                {
                    await synthesis.ConfigureAwait(false); // Raw operation result/late stream remains owned even after cancellation.
                    if (terminal.Task.IsCompleted) { outcome = await terminal.Task.ConfigureAwait(false); reason = "Canceled"; }
                    else if (!native.HasUsableStream) { outcome = ReadAloudOutcome.SynthesisFailed; reason = "InvalidSynthesisStream"; }
                    else
                    {
                        Log(request.OperationId, "StreamProduced", timer);
                        // No logging/observer call occurs between final admission and the native Play command.
                        if (cancellation.IsCancellationRequested || !mayPlay()) { outcome = ReadAloudOutcome.Canceled; reason = "PlaybackFenced"; }
                        else try
                        {
                            native.Play(() => { progress(new(request.OperationId, ReadAloudPhase.Reading)); Log(request.OperationId, "PlaybackStarted", timer); },
                                () => terminal.TrySetResult(ReadAloudOutcome.Completed),
                                () => terminal.TrySetResult(ReadAloudOutcome.PlaybackFailed));
                            Log(request.OperationId, "PlaybackRequested", timer);
                            outcome = await terminal.Task.ConfigureAwait(false);
                            reason = outcome == ReadAloudOutcome.Completed ? "MediaEnded" : outcome == ReadAloudOutcome.Canceled ? "Canceled" : "MediaFailed";
                        }
                        catch (Exception ex) { Log(request.OperationId, "PlaybackFailed", timer, ex); outcome = IsUnavailable(ex) ? ReadAloudOutcome.Unavailable : ReadAloudOutcome.PlaybackFailed; reason = "PlaybackStartFailed"; }
                    }
                }
                catch (Exception ex)
                {
                    if (terminal.Task.IsCompleted) { outcome = await terminal.Task.ConfigureAwait(false); reason = "Canceled"; }
                    else { outcome = IsUnavailable(ex) ? ReadAloudOutcome.Unavailable : ReadAloudOutcome.SynthesisFailed; reason = "SynthesisFailed"; Log(request.OperationId, "SynthesisFailed", timer, ex); }
                }
            }
        }
        catch (Exception ex) { outcome = ReadAloudOutcome.PlaybackFailed; reason = "NativeOperationFailed"; Log(request.OperationId, "NativeFailed", timer, ex); }
        finally
        {
            if (native is not null)
            {
                bool cleanupFailed = false;
                bool cleanupBlocked = false;
                try { native.DetachHandlers(); } catch (Exception ex) { cleanupFailed = true; Log(request.OperationId, "DetachFailed", timer, ex); }
                try { native.PauseAndClearSource(); } catch (Exception ex) { cleanupFailed = true; Log(request.OperationId, "PauseClearFailed", timer, ex); }
                // Never free a dependency underneath an unreleased player. Failed release retains this driver and exact objects.
                foreach (Action release in new Action[] { native.ReleasePlayer, native.ReleaseSource, native.ReleaseStream, native.ReleaseSynthesizer })
                {
                    bool released = false;
                    while (!released)
                    {
                        try { release(); released = true; }
                        catch (Exception ex)
                        {
                            if (!cleanupFailed) Log(request.OperationId, "CleanupRetry", timer, ex);
                            cleanupFailed = true;
                            if (!cleanupBlocked) { cleanupBlocked = true; progress(new(request.OperationId, ReadAloudPhase.Stopping, "CleanupBlocked")); }
                            try { await _retryCleanup().ConfigureAwait(false); }
                            catch (Exception) { await Task.Delay(250).ConfigureAwait(false); } // A failed injected observer cannot release resource ownership.
                        }
                    }
                }
                if (cleanupFailed) { outcome = ReadAloudOutcome.PlaybackFailed; reason = "CleanupFailure"; }
                Log(request.OperationId, "ResourcesReleased", timer);
                _retainedNative = null;
            }
        }
        return new(request.OperationId, outcome, reason);
    }

    private sealed class WindowsRequest : IReadAloudNativeRequest
    {
        private SpeechSynthesizer? _synth;
        private readonly VoiceInformation _voice;
        private IAsyncOperation<SpeechSynthesisStream>? _raw;
        private SpeechSynthesisStream? _stream;
        private MediaSource? _source;
        private MediaPlayer? _player;
        private TypedEventHandler<MediaPlayer, object>? _ended;
        private TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs>? _failed;
        private TypedEventHandler<MediaPlaybackSession, object>? _state;
        public WindowsRequest()
        {
            var voice = SpeechSynthesizer.DefaultVoice;
            if (voice is null || !SpeechSynthesizer.AllVoices.Any(v => v.Id == voice.Id))
                throw new NotSupportedException("DefaultVoiceUnavailable");
            _voice = voice; // No disposable object is allocated in the factory/constructor.
        }
        public async Task SynthesizeAsync(string text)
        {
            try { _synth = new SpeechSynthesizer(); _synth.Voice = _voice; }
            catch (Exception ex) { throw new ReadAloudNativeUnavailableException(ex.HResult); }
            _raw = _synth!.SynthesizeTextToStreamAsync(text);
            _stream = await _raw.AsTask().ConfigureAwait(false); // No managed cancellation wrapper that discards GetResults.
        }
        public bool HasUsableStream => _stream is { Size: > 0 } && !string.IsNullOrWhiteSpace(_stream.ContentType);
        public void CancelSynthesis() => _raw?.Cancel();
        public void Play(Action started, Action ended, Action failed)
        {
            _stream!.Seek(0);
            _source = MediaSource.CreateFromStream(_stream, _stream.ContentType);
            _player = new MediaPlayer();
            _player.AutoPlay = false; _player.IsLoopingEnabled = false;
            _player.AudioCategory = MediaPlayerAudioCategory.Speech;
            _player.CommandManager.IsEnabled = false;
            _ended = (_, _) => ended(); _failed = (_, _) => failed();
            _state = (session, _) =>
            {
                try { if (session.PlaybackState == MediaPlaybackState.Playing) started(); }
                catch (Exception) { /* A late state event racing player release cannot escape to native dispatch. */ }
            };
            _player.MediaEnded += _ended; _player.MediaFailed += _failed;
            _player.PlaybackSession.PlaybackStateChanged += _state;
            _player.Source = _source;
            _player.Play();
        }
        public void DetachHandlers()
        {
            if (_player is null) return;
            if (_ended is not null) { _player.MediaEnded -= _ended; _ended = null; }
            if (_failed is not null) { _player.MediaFailed -= _failed; _failed = null; }
            if (_state is not null) { _player.PlaybackSession.PlaybackStateChanged -= _state; _state = null; }
        }
        public void PauseAndClearSource() { if (_player is not null) { _player.Pause(); _player.Source = null; } }
        public void ReleasePlayer() { _player?.Dispose(); _player = null; _ended = null; _failed = null; _state = null; }
        public void ReleaseSource() { _source?.Dispose(); _source = null; }
        public void ReleaseStream() { _stream?.Dispose(); _stream = null; }
        public void ReleaseSynthesizer() { _raw?.Close(); _raw = null; _synth?.Dispose(); _synth = null; }
    }
}
