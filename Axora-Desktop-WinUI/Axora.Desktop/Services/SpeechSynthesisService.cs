using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models.Voice;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Synthesizes text with system voices and awaits a truthful terminal result from
/// the single-operation playback backend.
/// </summary>
public sealed class SpeechSynthesisService : ISpeechSynthesisService
{
    private readonly ILogger<SpeechSynthesisService>? _logger;
    private readonly ISpeechPlaybackBackend _playbackBackend;
    private readonly bool _ownsPlaybackBackend;
    private readonly SemaphoreSlim _speakLock = new(1, 1);
    private SpeechSynthesizer? _synthesizer;
    private volatile bool _isSpeaking;
    private VoiceInfo? _currentVoice;
    private List<VoiceInfo> _availableVoices = [];
    private double _speechRate = 1.0;
    private double _speechPitch = 1.0;
    private double _speechVolume = 1.0;
    private int _disposed;

    public bool IsSpeaking => _isSpeaking;
    public VoiceInfo? CurrentVoice => _currentVoice;
    public IReadOnlyList<VoiceInfo> AvailableVoices => _availableVoices;

    public double SpeechRate
    {
        get => _speechRate;
        set => _speechRate = Math.Clamp(value, 0.5, 3.0);
    }

    public double SpeechPitch
    {
        get => _speechPitch;
        set => _speechPitch = Math.Clamp(value, 0.5, 1.5);
    }

    public double SpeechVolume
    {
        get => _speechVolume;
        set => _speechVolume = Math.Clamp(value, 0.0, 1.0);
    }

    public event EventHandler<SpeechPlaybackStateChangedEventArgs>? PlaybackStateChanged;

    public SpeechSynthesisService(
        ISpeechPlaybackBackend? playbackBackend = null,
        ILogger<SpeechSynthesisService>? logger = null)
    {
        _playbackBackend = playbackBackend ?? new MediaPlayerSpeechPlaybackBackend();
        _ownsPlaybackBackend = playbackBackend == null;
        _logger = logger;
        _playbackBackend.PlaybackStarted += OnPlaybackStarted;
    }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        EnsureInitialized();
        return Task.CompletedTask;
    }

    private void EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_synthesizer != null) return;
        _synthesizer = new SpeechSynthesizer();
        PopulateVoices();
    }

    private void PopulateVoices()
    {
        try
        {
            var defaultVoice = SpeechSynthesizer.DefaultVoice;
            _availableVoices = SpeechSynthesizer.AllVoices.Select(v => new VoiceInfo(
                v.Id,
                v.DisplayName,
                v.Language,
                v.Gender.ToString(),
                v.Description,
                v.Id == defaultVoice?.Id)).ToList();
            _currentVoice = _availableVoices.FirstOrDefault(v => v.IsDefault)
                ?? _availableVoices.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enumerate Windows speech voices.");
            _availableVoices = [];
            _currentVoice = null;
        }
    }

    public void SetVoice(string voiceId)
    {
        if (string.IsNullOrWhiteSpace(voiceId) || Volatile.Read(ref _disposed) != 0) return;
        try
        {
            EnsureInitialized();
            var selected = _availableVoices.FirstOrDefault(v => v.Id == voiceId);
            var winRtVoice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.Id == voiceId);
            if (selected == null || winRtVoice == null || _synthesizer == null) return;
            _synthesizer.Voice = winRtVoice;
            _currentVoice = selected;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to apply speech voice {VoiceId}.", voiceId);
        }
    }

    public async Task<SpeechPlaybackResult> SpeakTextAsync(
        string text,
        double pitch = 1.0,
        double rate = 1.0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return SpeechPlaybackResult.Completed;
        if (Volatile.Read(ref _disposed) != 0) return SpeechPlaybackResult.Unavailable;

        try
        {
            await _speakLock.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return SpeechPlaybackResult.Canceled;
        }

        bool playbackStarted = false;
        void Started(object? sender, SpeechPlaybackStateChangedEventArgs args)
        {
            if (args.IsSpeaking) playbackStarted = true;
        }
        PlaybackStateChanged += Started;
        try
        {
            if (Volatile.Read(ref _disposed) != 0) return SpeechPlaybackResult.Unavailable;
            EnsureInitialized();
            if (_synthesizer == null || _availableVoices.Count == 0)
                return SpeechPlaybackResult.Unavailable;

            _synthesizer.Options.AudioPitch = Math.Clamp(pitch, 0.5, 1.5);
            _synthesizer.Options.SpeakingRate = Math.Clamp(rate, 0.5, 3.0);

            SpeechPlaybackResult result = await _playbackBackend.PlayAsync(
                async token =>
                {
                    token.ThrowIfCancellationRequested();
                    var stream = await _synthesizer.SynthesizeTextToStreamAsync(text);
                    if (token.IsCancellationRequested)
                    {
                        stream.Dispose();
                        token.ThrowIfCancellationRequested();
                    }
                    return stream;
                },
                Math.Clamp(_speechVolume, 0.0, 1.0),
                ct);

            _logger?.LogInformation("Speech request reached terminal result {Result} ({Length} chars).", result, text.Length);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return SpeechPlaybackResult.Canceled;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Speech synthesis/playback failed.");
            return SpeechPlaybackResult.Failed;
        }
        finally
        {
            PlaybackStateChanged -= Started;
            if (playbackStarted || _isSpeaking)
            {
                _isSpeaking = false;
                PlaybackStateChanged?.Invoke(this,
                    new SpeechPlaybackStateChangedEventArgs(false, _currentVoice?.Id));
            }
            _speakLock.Release();
        }
    }

    private void OnPlaybackStarted(object? sender, EventArgs e)
    {
        _isSpeaking = true;
        PlaybackStateChanged?.Invoke(this,
            new SpeechPlaybackStateChangedEventArgs(true, _currentVoice?.Id));
    }

    public void Stop() => _playbackBackend.Stop();
    public void Pause() => _playbackBackend.Pause();
    public void Resume() => _playbackBackend.Resume();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _playbackBackend.Stop();
        _playbackBackend.PlaybackStarted -= OnPlaybackStarted;
        _synthesizer?.Dispose();
        _synthesizer = null;
        if (_ownsPlaybackBackend) _playbackBackend.Dispose();
        PlaybackStateChanged = null;
        _speakLock.Dispose();
    }
}
