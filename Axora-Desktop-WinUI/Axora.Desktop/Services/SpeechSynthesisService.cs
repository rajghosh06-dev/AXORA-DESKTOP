using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models.Voice;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Speech synthesis service utilizing system-provided Windows speech voices.
/// Enforces pitch/rate boundary clamping, serializes concurrent speech requests,
/// provides voice selection, and raises playback state events.
/// </summary>
public sealed class SpeechSynthesisService : ISpeechSynthesisService, IDisposable
{
    private readonly ILogger<SpeechSynthesisService>? _logger;
    private readonly SemaphoreSlim _speakLock = new(1, 1);
    private SpeechSynthesizer? _synthesizer;
    private MediaPlayer? _player;
    private volatile bool _isSpeaking;
    private VoiceInfo? _currentVoice;
    private List<VoiceInfo> _availableVoices = new();
    private double _speechRate = 1.0;
    private double _speechPitch = 1.0;
    private double _speechVolume = 1.0;

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

    public SpeechSynthesisService(ILogger<SpeechSynthesisService>? logger = null)
    {
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        EnsureInitialized();
        return Task.CompletedTask;
    }

    private void EnsureInitialized()
    {
        if (_synthesizer == null)
        {
            _synthesizer = new SpeechSynthesizer();
            PopulateVoices();
        }

        if (_player == null)
        {
            _player = new MediaPlayer();
            _player.MediaEnded += (s, e) =>
            {
                _isSpeaking = false;
                PlaybackStateChanged?.Invoke(this, new SpeechPlaybackStateChangedEventArgs(false, _currentVoice?.Id));
            };
            _player.MediaFailed += (s, e) =>
            {
                _isSpeaking = false;
                PlaybackStateChanged?.Invoke(this, new SpeechPlaybackStateChangedEventArgs(false, _currentVoice?.Id));
            };
        }
    }

    private void PopulateVoices()
    {
        try
        {
            var defaultVoice = SpeechSynthesizer.DefaultVoice;
            _availableVoices = SpeechSynthesizer.AllVoices.Select(v => new VoiceInfo(
                Id: v.Id,
                DisplayName: v.DisplayName,
                Language: v.Language,
                Gender: v.Gender.ToString(),
                Description: v.Description,
                IsDefault: v.Id == defaultVoice?.Id
            )).ToList();

            if (_availableVoices.Count > 0)
            {
                _currentVoice = _availableVoices.FirstOrDefault(v => v.IsDefault) ?? _availableVoices[0];
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enumerate system-provided Windows speech voices.");
            _availableVoices = new List<VoiceInfo>();
        }
    }

    public void SetVoice(string voiceId)
    {
        if (string.IsNullOrWhiteSpace(voiceId)) return;

        EnsureInitialized();
        var selected = _availableVoices.FirstOrDefault(v => v.Id == voiceId);
        if (selected != null && _synthesizer != null)
        {
            try
            {
                var winRtVoice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.Id == voiceId);
                if (winRtVoice != null)
                {
                    _synthesizer.Voice = winRtVoice;
                    _currentVoice = selected;
                    _logger?.LogInformation("Active speech voice set to {VoiceName}", selected.DisplayName);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to apply selected voice {VoiceId}", voiceId);
            }
        }
    }

    public async Task SpeakTextAsync(string text, double pitch = 1.0, double rate = 1.0, CancellationToken ct = default)
    {
        // R-VOICE-16: Empty or whitespace text guard is an immediate no-op
        if (string.IsNullOrWhiteSpace(text)) return;

        // R-VOICE-17: Serialize overlapping speak requests
        await _speakLock.WaitAsync(ct);
        try
        {
            Stop();
            EnsureInitialized();

            double effectivePitch = Math.Clamp(pitch, 0.5, 1.5);
            double effectiveRate = Math.Clamp(rate, 0.5, 3.0);

            _synthesizer!.Options.AudioPitch = effectivePitch;
            _synthesizer.Options.SpeakingRate = effectiveRate;
            _player!.Volume = Math.Clamp(_speechVolume, 0.0, 1.0);

            _isSpeaking = true;
            PlaybackStateChanged?.Invoke(this, new SpeechPlaybackStateChangedEventArgs(true, _currentVoice?.Id));

            using var stream = await _synthesizer.SynthesizeTextToStreamAsync(text);
            ct.ThrowIfCancellationRequested();

            var mediaSource = MediaSource.CreateFromStream(stream, stream.ContentType);
            _player.Source = mediaSource;
            _player.Play();

            _logger?.LogInformation("Synthesizing speech via system voice ({Length} chars)", text.Length);
        }
        catch (OperationCanceledException)
        {
            _isSpeaking = false;
            Stop();
            PlaybackStateChanged?.Invoke(this, new SpeechPlaybackStateChangedEventArgs(false, _currentVoice?.Id));
        }
        catch (Exception ex)
        {
            _isSpeaking = false;
            PlaybackStateChanged?.Invoke(this, new SpeechPlaybackStateChangedEventArgs(false, _currentVoice?.Id));
            _logger?.LogWarning(ex, "Speech synthesis playback failed");
        }
        finally
        {
            _speakLock.Release();
        }
    }

    public void Stop()
    {
        if (_player?.Source != null)
        {
            _player.Pause();
            _player.Source = null;
        }
        if (_isSpeaking)
        {
            _isSpeaking = false;
            PlaybackStateChanged?.Invoke(this, new SpeechPlaybackStateChangedEventArgs(false, _currentVoice?.Id));
        }
    }

    public void Pause()
    {
        _player?.Pause();
    }

    public void Resume()
    {
        _player?.Play();
    }

    public void Dispose()
    {
        Stop();
        _synthesizer?.Dispose();
        _synthesizer = null;
        _player?.Dispose();
        _player = null;
        _speakLock.Dispose();
    }
}
