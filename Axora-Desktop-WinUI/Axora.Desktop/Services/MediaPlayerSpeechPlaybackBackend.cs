using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models.Voice;
using Axora.Desktop.Services.Contracts;

[assembly: InternalsVisibleTo("Axora.Desktop.Tests")]

namespace Axora.Desktop.Services;

/// <summary>
/// Owns the WinRT media objects for exactly one physical playback operation.
/// Every admitted operation completes once on media end, failure, cancellation or stop.
/// </summary>
public sealed class MediaPlayerSpeechPlaybackBackend : ISpeechPlaybackBackend
{
    private sealed class ActivePlayback
    {
        public required long Generation { get; init; }
        public required SpeechSynthesisStream Stream { get; init; }
        public required MediaSource Source { get; init; }
        public TaskCompletionSource<SpeechPlaybackResult> Completion => Terminal.Completion;
        public required TypedEventHandler<MediaPlayer, object> EndedHandler { get; init; }
        public required TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs> FailedHandler { get; init; }
        public CancellationTokenRegistration CancellationRegistration { get; set; }
        public TerminalSettlement Terminal { get; } = new();
    }

    // The same settlement path is exercised with injected cleanup failures by the
    // deterministic tests; native WinRT objects are not required to prove liveness.
    internal sealed class TerminalSettlement
    {
        private int _claimed;
        public TaskCompletionSource<SpeechPlaybackResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool TryComplete(
            SpeechPlaybackResult result,
            bool stopPlayer,
            Action detachHandlers,
            Action pause,
            Action clearSource,
            Action disposePlayer,
            Action unregister,
            Action disposeSource,
            Action disposeStream,
            Action<bool> afterCleanup,
            Action<Exception> reportFailure)
        {
            if (Interlocked.Exchange(ref _claimed, 1) != 0) return false;
            bool failed = false;
            bool detached = false;
            void Report(Exception ex)
            {
                try { reportFailure(ex); } catch { /* A diagnostic sink cannot prevent terminal publication. */ }
            }
            void Attempt(Action action)
            {
                try { action(); }
                catch (Exception ex) { failed = true; Report(ex); }
            }

            try
            {
                Attempt(detachHandlers);
                if (stopPlayer) Attempt(pause);
                try { clearSource(); detached = true; }
                catch (Exception ex)
                {
                    failed = true;
                    Report(ex);
                    // A failed Source setter leaves ownership with MediaPlayer. Dispose
                    // the player before releasing the source or synthesized stream.
                    try { disposePlayer(); detached = true; }
                    catch (Exception fallbackError) { Report(fallbackError); }
                }
                Attempt(unregister);
                if (detached)
                {
                    Attempt(disposeSource);
                    Attempt(disposeStream);
                }
                else
                {
                    failed = true;
                    Report(new InvalidOperationException(
                        "MediaPlayer could not release its source; media resources are retained safely."));
                }
            }
            finally
            {
                try { afterCleanup(detached); }
                catch (Exception ex) { failed = true; Report(ex); }
                Completion.TrySetResult(failed ? SpeechPlaybackResult.Failed : result);
            }
            return true;
        }
    }

    // Called under the backend gate. A late event from a previous media source
    // cannot claim a newer request even when the player object is reused.
    internal sealed class TerminalGenerationGate
    {
        private long? _current;
        public void Begin(long generation) => _current = generation;
        public bool TryClaim(long generation)
        {
            if (_current != generation) return false;
            _current = null;
            return true;
        }
    }

    private readonly object _gate = new();
    private readonly TerminalGenerationGate _terminalGate = new();
    private readonly ILogger<MediaPlayerSpeechPlaybackBackend>? _logger;
    private MediaPlayer? _player;
    private ActivePlayback? _active;
    private readonly List<ActivePlayback> _retainedOnDetachFailure = [];
    private bool _terminalCleanupPending;
    private bool _unavailable;
    private long _generation;
    private bool _disposed;

    public bool IsPlaying
    {
        get { lock (_gate) return _active != null; }
    }

    public event EventHandler? PlaybackStarted;

    public MediaPlayerSpeechPlaybackBackend(ILogger<MediaPlayerSpeechPlaybackBackend>? logger = null)
    {
        _logger = logger;
    }

    public async Task<SpeechPlaybackResult> PlayAsync(
        Func<CancellationToken, Task<SpeechSynthesisStream>> streamFactory,
        double volume,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(streamFactory);
        if (ct.IsCancellationRequested) return SpeechPlaybackResult.Canceled;

        SpeechSynthesisStream stream;
        try
        {
            stream = await streamFactory(ct);
            if (ct.IsCancellationRequested)
            {
                stream.Dispose();
                return SpeechPlaybackResult.Canceled;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return SpeechPlaybackResult.Canceled;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Speech stream synthesis failed before playback admission.");
            return SpeechPlaybackResult.Failed;
        }

        ActivePlayback? active = null;
        try
        {
            lock (_gate)
            {
                if (_disposed || _unavailable)
                {
                    stream.Dispose();
                    return SpeechPlaybackResult.Unavailable;
                }
                if (_active != null || _terminalCleanupPending)
                {
                    stream.Dispose();
                    _logger?.LogError("Playback backend received overlapping physical playback requests.");
                    return SpeechPlaybackResult.Failed;
                }

                _player ??= new MediaPlayer();
                long generation = ++_generation;
                var source = MediaSource.CreateFromStream(stream, stream.ContentType);

                TypedEventHandler<MediaPlayer, object> ended = (_, _) =>
                    Complete(generation, SpeechPlaybackResult.Completed, stopPlayer: false);
                TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs> failed = (_, args) =>
                {
                    _logger?.LogWarning("MediaPlayer speech playback failed: {Error} ({Message})", args.Error, args.ErrorMessage);
                    Complete(generation, SpeechPlaybackResult.Failed, stopPlayer: true);
                };

                active = new ActivePlayback
                {
                    Generation = generation,
                    Stream = stream,
                    Source = source,
                    EndedHandler = ended,
                    FailedHandler = failed
                };

                _active = active;
                _terminalGate.Begin(generation);
                _player.MediaEnded += active.EndedHandler;
                _player.MediaFailed += active.FailedHandler;
                _player.Volume = Math.Clamp(volume, 0.0, 1.0);
                _player.Source = source;

                var registration = ct.Register(() =>
                    Complete(generation, SpeechPlaybackResult.Canceled, stopPlayer: true));
                if (ReferenceEquals(_active, active))
                    active.CancellationRegistration = registration;
                else
                    registration.Unregister();

                if (ReferenceEquals(_active, active))
                    _player.Play();
            }

            if (IsCurrent(active!.Generation))
                PlaybackStarted?.Invoke(this, EventArgs.Empty);
            return await active.Completion.Task;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "MediaPlayer failed to begin speech playback.");
            if (active != null)
            {
                Complete(active.Generation, SpeechPlaybackResult.Failed, stopPlayer: true);
                return await active.Completion.Task;
            }
            try { stream.Dispose(); }
            catch (Exception disposalError)
            {
                _logger?.LogWarning(disposalError, "Speech stream disposal after startup failure failed.");
            }
            return SpeechPlaybackResult.Failed;
        }
    }

    public void Stop()
    {
        long generation;
        lock (_gate) generation = _active?.Generation ?? 0;
        if (generation != 0)
            Complete(generation, SpeechPlaybackResult.Canceled, stopPlayer: true);
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (_active != null) _player?.Pause();
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (_active != null) _player?.Play();
        }
    }

    private bool IsCurrent(long generation)
    {
        lock (_gate) return MatchesGeneration(_active?.Generation, generation);
    }

    internal static bool MatchesGeneration(long? activeGeneration, long signalGeneration) =>
        activeGeneration == signalGeneration;

    private void Complete(long generation, SpeechPlaybackResult result, bool stopPlayer)
    {
        ActivePlayback? completed;
        MediaPlayer? player;
        lock (_gate)
        {
            if (_active == null || !_terminalGate.TryClaim(generation)) return;
            completed = _active;
            _active = null;
            _terminalCleanupPending = true;
            player = _player;
        }
        completed.Terminal.TryComplete(result, stopPlayer,
            () =>
            {
                if (player == null) return;
                player.MediaEnded -= completed.EndedHandler;
                player.MediaFailed -= completed.FailedHandler;
            },
            () => player?.Pause(),
            () => { if (player != null) player.Source = null; },
            () =>
            {
                player?.Dispose();
                lock (_gate) { if (ReferenceEquals(_player, player)) _player = null; }
            },
            () => completed.CancellationRegistration.Unregister(),
            () => completed.Source.Dispose(),
            () => completed.Stream.Dispose(),
            detached =>
            {
                lock (_gate)
                {
                    if (!detached)
                    {
                        _unavailable = true;
                        _retainedOnDetachFailure.Add(completed);
                    }
                    _terminalCleanupPending = false;
                    if (_disposed) DisposePlayerAndRetainedUnderLock();
                }
            },
            ex => _logger?.LogWarning(ex, "Speech playback terminal cleanup failed."));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }

        Stop();
        lock (_gate)
        {
            if (!_terminalCleanupPending) DisposePlayerAndRetainedUnderLock();
        }
    }

    private void DisposePlayerAndRetainedUnderLock()
    {
        bool playerDisposed = false;
        try { _player?.Dispose(); playerDisposed = true; }
        catch (Exception ex) { _logger?.LogWarning(ex, "MediaPlayer disposal failed; retaining its media resources."); }
        if (!playerDisposed) return;
        _player = null;
        foreach (ActivePlayback retained in _retainedOnDetachFailure)
        {
            try { retained.Source.Dispose(); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Retained media source disposal failed."); }
            try { retained.Stream.Dispose(); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Retained speech stream disposal failed."); }
        }
        _retainedOnDetachFailure.Clear();
        PlaybackStarted = null;
    }
}
