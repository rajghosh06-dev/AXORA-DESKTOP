using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models.Voice;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Deterministic voice command router. Maps spoken phrases to registered actions with
/// strict command safety classification (Safe, ConfirmationRequired, VoiceProhibited).
/// </summary>
public interface IVoiceCommandRouter : IDisposable
{
    IReadOnlyList<VoiceCommandRegistration> RegisteredCommands { get; }

    void RegisterCommand(VoiceCommandRegistration command);
    void UnregisterCommand(string commandId);
    VoiceCommandMatchResult MatchCommand(string spokenPhrase);
    Task<bool> ExecuteCommandAsync(string spokenPhrase, CancellationToken ct = default);
    event EventHandler<VoiceCommandExecutedEventArgs>? CommandExecuted;
}
