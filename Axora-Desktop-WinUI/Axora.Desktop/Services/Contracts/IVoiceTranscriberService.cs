using System;
using System.Threading;
using System.Threading.Tasks;

namespace Axora.Desktop.Services.Contracts;

public interface IVoiceTranscriberService
{
    bool IsRecording { get; }
    Task StartDictationAsync(Action<string> onTextRecognized, CancellationToken ct = default);
    Task StopDictationAsync();
}
