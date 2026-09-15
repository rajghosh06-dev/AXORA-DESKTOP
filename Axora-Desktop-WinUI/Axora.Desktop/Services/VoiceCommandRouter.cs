using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models.Voice;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Deterministic voice command router.
/// Normalizes spoken phrases, executes exact and alias matches against registered commands,
/// and strictly enforces CommandSafetyLevel (Safe, ConfirmationRequired, VoiceProhibited).
/// </summary>
public sealed class VoiceCommandRouter : IVoiceCommandRouter
{
    private readonly ILogger<VoiceCommandRouter>? _logger;
    private readonly List<VoiceCommandRegistration> _commands = new();
    private readonly object _lock = new();
    private bool _isListening;

    public bool IsListening
    {
        get { lock (_lock) return _isListening; }
        private set { lock (_lock) _isListening = value; }
    }

    public IReadOnlyList<VoiceCommandRegistration> RegisteredCommands
    {
        get { lock (_lock) return _commands.ToList(); }
    }

    public event EventHandler<VoiceCommandExecutedEventArgs>? CommandExecuted;

    public VoiceCommandRouter(ILogger<VoiceCommandRouter>? logger = null)
    {
        _logger = logger;
    }

    public void RegisterCommand(VoiceCommandRegistration command)
    {
        ArgumentNullException.ThrowIfNull(command);

        lock (_lock)
        {
            _commands.RemoveAll(c => c.CommandId.Equals(command.CommandId, StringComparison.OrdinalIgnoreCase));
            _commands.Add(command);
            _logger?.LogInformation("Registered voice command {CommandId} (Safety: {Safety})", command.CommandId, command.SafetyLevel);
        }
    }

    public void UnregisterCommand(string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId)) return;

        lock (_lock)
        {
            _commands.RemoveAll(c => c.CommandId.Equals(commandId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static string NormalizePhrase(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase)) return string.Empty;

        string normalized = phrase.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"^[\p{P}\p{S}]+", string.Empty);
        normalized = Regex.Replace(normalized, @"[\p{P}\p{S}]+$", string.Empty);
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }

    public VoiceCommandMatchResult MatchCommand(string spokenPhrase)
    {
        string normalized = NormalizePhrase(spokenPhrase);
        if (string.IsNullOrEmpty(normalized))
        {
            return VoiceCommandMatchResult.NoMatch(string.Empty);
        }

        lock (_lock)
        {
            // 1. Exact primary phrase match
            var primaryMatches = _commands.Where(c =>
                NormalizePhrase(c.PrimaryPhrase).Equals(normalized, StringComparison.OrdinalIgnoreCase)).ToList();

            if (primaryMatches.Count == 1)
            {
                return VoiceCommandMatchResult.Match(primaryMatches[0], normalized);
            }
            if (primaryMatches.Count > 1)
            {
                _logger?.LogWarning("Ambiguous command match for primary phrase '{Phrase}'. Multiple matches found.", normalized);
                return VoiceCommandMatchResult.NoMatch(normalized);
            }

            // 2. Exact alias match
            var aliasMatches = _commands.Where(c =>
                c.Aliases.Any(a => NormalizePhrase(a).Equals(normalized, StringComparison.OrdinalIgnoreCase))).ToList();

            if (aliasMatches.Count == 1)
            {
                return VoiceCommandMatchResult.Match(aliasMatches[0], normalized);
            }
            if (aliasMatches.Count > 1)
            {
                _logger?.LogWarning("Ambiguous command match for alias '{Phrase}'. Multiple matches found.", normalized);
                return VoiceCommandMatchResult.NoMatch(normalized);
            }

            return VoiceCommandMatchResult.NoMatch(normalized);
        }
    }

    public async Task<bool> ExecuteCommandAsync(string spokenPhrase, CancellationToken ct = default)
    {
        var match = MatchCommand(spokenPhrase);
        if (!match.IsMatched || match.MatchedCommand == null)
        {
            _logger?.LogInformation("Voice command not recognized: '{Phrase}'", NormalizePhrase(spokenPhrase));
            return false;
        }

        var command = match.MatchedCommand;

        // Enforce safety classification
        switch (command.SafetyLevel)
        {
            case CommandSafetyLevel.VoiceProhibited:
                _logger?.LogWarning("Command {CommandId} is VoiceProhibited and cannot be executed via voice.", command.CommandId);
                return false;

            case CommandSafetyLevel.ConfirmationRequired:
                _logger?.LogWarning("Command {CommandId} requires interactive confirmation before voice execution.", command.CommandId);
                return false;

            case CommandSafetyLevel.Safe:
            default:
                try
                {
                    _logger?.LogInformation("Executing voice command {CommandId}", command.CommandId);
                    await command.Action(ct);
                    CommandExecuted?.Invoke(this, new VoiceCommandExecutedEventArgs(command, spokenPhrase));
                    return true;
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("Voice command {CommandId} canceled.", command.CommandId);
                    return false;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error executing voice command {CommandId}", command.CommandId);
                    return false;
                }
        }
    }

    public Task StartListeningAsync(CancellationToken ct = default)
    {
        IsListening = true;
        _logger?.LogInformation("Voice command router listening started.");
        return Task.CompletedTask;
    }

    public Task StopListeningAsync()
    {
        IsListening = false;
        _logger?.LogInformation("Voice command router listening stopped.");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopListeningAsync();
        lock (_lock)
        {
            _commands.Clear();
        }
    }
}
