using System;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models.Voice;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Centralized coordinator for all voice subsystem activities.
/// Manages mutual exclusion between microphone listening and speech output,
/// provides configurable acoustic debounce, and enforces session state transitions.
/// </summary>
public interface IVoiceCoordinator : IDisposable
{
    VoiceSessionState CurrentState { get; }
    bool IsVoiceNavigationEnabled { get; set; }
    /// <summary>Live command-recognition intent, including speech suspension; independent of saved preference.</summary>
    bool IsVoiceNavigationDesired { get; }
    AudioCaptureHealth CaptureHealth { get; }
    TimeSpan AcousticDebounceInterval { get; set; }

    Task<VoiceRecognitionStartResult> RequestStartDictationAsync(Action<string> onFormattedChunk, CancellationToken ct = default);
    Task RequestStopDictationAsync();
    Task<SpeechPlaybackResult> RequestSpeakAsync(string text, double? pitch = null, double? rate = null, CancellationToken ct = default);
    void RequestStopSpeech();
    Task<VoiceRecognitionStartResult> StartVoiceNavigationAsync(CancellationToken ct = default);
    Task StopVoiceNavigationAsync();
    Task StopAsync();

    event EventHandler<VoiceSessionStateChangedEventArgs>? StateChanged;
}
