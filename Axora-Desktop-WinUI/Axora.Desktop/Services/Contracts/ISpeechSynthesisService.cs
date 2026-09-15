using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models.Voice;

namespace Axora.Desktop.Services.Contracts;

public interface ISpeechSynthesisService : IDisposable
{
    bool IsSpeaking { get; }
    VoiceInfo? CurrentVoice { get; }
    IReadOnlyList<VoiceInfo> AvailableVoices { get; }
    double SpeechRate { get; set; }
    double SpeechPitch { get; set; }
    double SpeechVolume { get; set; }

    Task InitializeAsync(CancellationToken ct = default);
    Task SpeakTextAsync(string text, double pitch = 1.0, double rate = 1.0, CancellationToken ct = default);
    void Stop();
    void Pause();
    void Resume();
    void SetVoice(string voiceId);

    event EventHandler<SpeechPlaybackStateChangedEventArgs>? PlaybackStateChanged;
}
