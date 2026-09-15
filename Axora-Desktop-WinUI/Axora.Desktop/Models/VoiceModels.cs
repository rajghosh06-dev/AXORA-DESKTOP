using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Axora.Desktop.Models.Voice;

/// <summary>
/// Lifecycle state of the centralized voice coordination subsystem.
/// </summary>
public enum VoiceSessionState
{
    Disabled,
    Idle,
    ListeningForCommand,
    Dictating,
    Synthesizing,
    Paused,
    Error
}

/// <summary>
/// Health and availability status of the microphone audio capture hardware and Windows permissions.
/// </summary>
public enum AudioCaptureHealth
{
    Healthy,
    NoMicrophoneDetected,
    PermissionDenied,
    DeviceBusy,
    RecognitionUnavailable
}

/// <summary>
/// Safety classification for voice commands.
/// Replaces naive keyword matching with strongly typed metadata.
/// </summary>
public enum CommandSafetyLevel
{
    /// <summary>
    /// Safe to execute immediately upon voice recognition (e.g. navigation, read aloud, stop).
    /// </summary>
    Safe,

    /// <summary>
    /// Requires an explicit interactive confirmation modal before execution (e.g. clear editor text).
    /// </summary>
    ConfirmationRequired,

    /// <summary>
    /// Strictly prohibited from voice execution (e.g. file deletion, database reset, vault wipe).
    /// Router immediately rejects voice invocation with an audible or visual warning.
    /// </summary>
    VoiceProhibited
}

/// <summary>
/// Functional category for registered voice commands.
/// </summary>
public enum VoiceCommandCategory
{
    Navigation,
    Playback,
    DocumentEditing,
    ShellControl
}

/// <summary>
/// Metadata describing a system-provided Windows speech synthesis voice.
/// </summary>
public sealed record VoiceInfo(
    string Id,
    string DisplayName,
    string Language,
    string Gender,
    string Description,
    bool IsDefault
);

/// <summary>
/// A registered voice command with primary phrase, aliases, safety classification, and execution delegate.
/// </summary>
public sealed record VoiceCommandRegistration(
    string CommandId,
    string PrimaryPhrase,
    IReadOnlyList<string> Aliases,
    VoiceCommandCategory Category,
    CommandSafetyLevel SafetyLevel,
    Func<CancellationToken, Task> Action
);

/// <summary>
/// Result of a deterministic voice command matching attempt.
/// </summary>
public sealed record VoiceCommandMatchResult(
    bool IsMatched,
    VoiceCommandRegistration? MatchedCommand,
    string NormalizedSpokenText
)
{
    public static VoiceCommandMatchResult NoMatch(string normalizedText) =>
        new(false, null, normalizedText);

    public static VoiceCommandMatchResult Match(VoiceCommandRegistration command, string normalizedText) =>
        new(true, command, normalizedText);
}

/// <summary>
/// A chunk of transcribed speech emitted during continuous dictation.
/// </summary>
public sealed record TranscriptionChunk(
    string RawText,
    string FormattedText,
    bool IsFinal
);

/// <summary>
/// Event arguments emitted when the voice session state transitions.
/// </summary>
public sealed class VoiceSessionStateChangedEventArgs : EventArgs
{
    public VoiceSessionState PreviousState { get; }
    public VoiceSessionState NewState { get; }
    public string? Reason { get; }

    public VoiceSessionStateChangedEventArgs(VoiceSessionState previousState, VoiceSessionState newState, string? reason = null)
    {
        PreviousState = previousState;
        NewState = newState;
        Reason = reason;
    }
}

/// <summary>
/// Event arguments emitted when audio device health or connectivity changes.
/// </summary>
public sealed class AudioDeviceStatusChangedEventArgs : EventArgs
{
    public AudioCaptureHealth Health { get; }
    public string? DeviceName { get; }
    public string? Message { get; }

    public AudioDeviceStatusChangedEventArgs(AudioCaptureHealth health, string? deviceName = null, string? message = null)
    {
        Health = health;
        DeviceName = deviceName;
        Message = message;
    }
}

/// <summary>
/// Event arguments emitted when a voice command is successfully executed.
/// </summary>
public sealed class VoiceCommandExecutedEventArgs : EventArgs
{
    public VoiceCommandRegistration Command { get; }
    public string SpokenPhrase { get; }

    public VoiceCommandExecutedEventArgs(VoiceCommandRegistration command, string spokenPhrase)
    {
        Command = command;
        SpokenPhrase = spokenPhrase;
    }
}

/// <summary>
/// Event arguments emitted when speech synthesis playback state changes.
/// </summary>
public sealed class SpeechPlaybackStateChangedEventArgs : EventArgs
{
    public bool IsSpeaking { get; }
    public string? VoiceId { get; }

    public SpeechPlaybackStateChangedEventArgs(bool isSpeaking, string? voiceId = null)
    {
        IsSpeaking = isSpeaking;
        VoiceId = voiceId;
    }
}

/// <summary>
/// Event arguments emitted when voice transcriber status changes.
/// </summary>
public sealed class VoiceTranscriberStateChangedEventArgs : EventArgs
{
    public bool IsRecording { get; }
    public AudioCaptureHealth Health { get; }

    public VoiceTranscriberStateChangedEventArgs(bool isRecording, AudioCaptureHealth health)
    {
        IsRecording = isRecording;
        Health = health;
    }
}
