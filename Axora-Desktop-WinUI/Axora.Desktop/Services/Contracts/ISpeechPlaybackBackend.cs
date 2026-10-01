using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;
using Axora.Desktop.Models.Voice;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Narrow media seam for one terminal, cancelable speech playback operation.
/// The backend owns the synthesized stream and media source until completion.
/// </summary>
public interface ISpeechPlaybackBackend : IDisposable
{
    bool IsPlaying { get; }

    Task<SpeechPlaybackResult> PlayAsync(
        Func<CancellationToken, Task<SpeechSynthesisStream>> streamFactory,
        double volume,
        CancellationToken ct = default);

    void Stop();
    void Pause();
    void Resume();

    event EventHandler? PlaybackStarted;
}
