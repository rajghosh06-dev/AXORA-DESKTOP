using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Offline Speech Synthesis Service using Windows.Media.SpeechSynthesis.
/// Streams natural neural voice audio into a dedicated Windows MediaPlayer instance.
///
/// Threading: SpeakTextAsync is awaitable and safe to call from any thread.
/// Disposal: Stop() is idempotent; Dispose() cleans both synthesizer and player.
/// </summary>
public sealed class SpeechSynthesisService : ISpeechSynthesisService, IDisposable
{
    private readonly ILogger<SpeechSynthesisService> _logger;
    private readonly SemaphoreSlim _speakLock = new(1, 1);
    private SpeechSynthesizer? _synthesizer;
    private MediaPlayer? _player;
    private volatile bool _isSpeaking;

    public bool IsSpeaking => _isSpeaking;

    public SpeechSynthesisService(ILogger<SpeechSynthesisService> logger)
    {
        _logger = logger;
    }

    private void EnsureInitialized()
    {
        _synthesizer ??= new SpeechSynthesizer();
        if (_player == null)
        {
            _player = new MediaPlayer();
            _player.MediaEnded += (s, e) => _isSpeaking = false;
            _player.MediaFailed += (s, e) => _isSpeaking = false;
        }
    }

    public async Task SpeakTextAsync(string text, double pitch = 1.0, double rate = 1.0, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // Guard against concurrent speak calls — serialize them with a lock
        await _speakLock.WaitAsync(ct);
        try
        {
            Stop();
            EnsureInitialized();

            _synthesizer!.Options.AudioPitch = Math.Clamp(pitch, 0.0, 2.0);
            _synthesizer.Options.SpeakingRate = Math.Clamp(rate, 0.5, 3.0);

            _isSpeaking = true;
            using var stream = await _synthesizer.SynthesizeTextToStreamAsync(text);

            ct.ThrowIfCancellationRequested();

            var mediaSource = MediaSource.CreateFromStream(stream, stream.ContentType);
            _player!.Source = mediaSource;
            _player.Play();

            _logger.LogInformation("Synthesized speech ({Length} chars)", text.Length);
        }
        catch (OperationCanceledException)
        {
            _isSpeaking = false;
            Stop();
        }
        catch (Exception ex)
        {
            _isSpeaking = false;
            _logger.LogWarning(ex, "Speech synthesis failed");
        }
        finally
        {
            _speakLock.Release();
        }
    }

    public void Stop()
    {
        if (_player != null)
        {
            _player.Pause();
            _player.Source = null;
        }
        _isSpeaking = false;
    }

    public void Dispose()
    {
        Stop();
        _synthesizer?.Dispose();
        _player?.Dispose();
        _speakLock.Dispose();
    }
}
