using System;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models.Voice;

namespace Axora.Desktop.Services.Contracts;

public interface IVoiceTranscriberService : IDisposable
{
    bool IsRecording { get; }
    AudioCaptureHealth DeviceHealth { get; }

    Task<bool> CheckPrerequisitesAsync(CancellationToken ct = default);
    Task<VoiceRecognitionStartResult> StartDictationAsync(
        Action<TranscriptionChunk> onChunkRecognized,
        CancellationToken ct = default);
    Task StopDictationAsync();

    event EventHandler<VoiceTranscriberStateChangedEventArgs>? StateChanged;
}
