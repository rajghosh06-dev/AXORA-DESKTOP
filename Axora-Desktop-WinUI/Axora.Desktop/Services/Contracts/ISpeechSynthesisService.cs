using System;
using System.Threading;
using System.Threading.Tasks;

namespace Axora.Desktop.Services.Contracts;

public interface ISpeechSynthesisService
{
    Task SpeakTextAsync(string text, double pitch = 1.0, double rate = 1.0, CancellationToken ct = default);
    void Stop();
    bool IsSpeaking { get; }
}
