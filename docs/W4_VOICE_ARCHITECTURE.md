# Phase W4 Architecture: AXORA Voice Subsystem

**Phase**: `W4 — Voice Capabilities (AXORA Voice Subsystem)`  
**Status**: `PLANNING HARDENED (PASS 2 — AUDIT VERIFIED) / AWAITING IMPLEMENTATION AUTHORIZATION`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Implementation Baseline**: `2e8eb83a3d2735a70d288f3d2702f3507df3529a` (`feat(winui): complete W3-F study synthesis engine and grounding verification`)  
**Historical Preceding Baseline**: `a3dad9325f3fcf30ab4b71b088a2047e44a216ce` (`feat(winui): complete W3-E search and retrieval integration`)  

---

## 1. Executive Architectural Overview & System Topology

The **AXORA Voice Subsystem** is an integrated, local-first audio and speech architecture designed for Windows App SDK 1.6 and .NET 9. It provides voice dictation, speech synthesis via system-provided Windows speech voices, and hands-free voice command routing across the AXORA desktop shell without transmitting audio to external cloud services.

```mermaid
flowchart TB
    subgraph UI_Shell [UI & Presentation Layer]
        ShellView[ShellView / MainWindow]
        SettingsPage[SettingsPage / Audio Settings]
        ScholarPage[ScholarKitPage / Speech Lab]
        FlashcardsPage[FlashcardsPage / Pronunciation]
    end

    subgraph ViewModels [MVVM ViewModel Layer]
        ShellVM[ShellViewModel]
        SettingsVM[SettingsViewModel]
        ScholarVM[ScholarKitViewModel]
        FlashcardsVM[FlashcardsViewModel]
    end

    subgraph Voice_Coordinator [Centralized Voice Coordination Layer]
        IVoiceCoordinator[IVoiceCoordinator\nVoiceCoordinator]
        VoiceState[(VoiceSessionState\nIdle | Listening | Dictating | Synthesizing)]
    end

    subgraph Voice_Engines [Engine & Service Layer - Deterministic Logic]
        IVoiceCommandRouter[IVoiceCommandRouter\nVoiceCommandRouter]
        IVoiceTranscriber[IVoiceTranscriberService\nVoiceTranscriberService]
        ISpeechSynthesis[ISpeechSynthesisService\nSpeechSynthesisService]
        IAudioDeviceMonitor[IAudioDeviceMonitor\nAudioDeviceMonitor]
        IVoiceTextFormatter[IVoiceTextFormatter\nVoiceTextFormatter]
    end

    subgraph WinRT_Platform [Windows 11 Platform & Environment Runtime Layer]
        WinRTSpeechRecog[Windows.Media.SpeechRecognition\nSpeechRecognizer + ListConstraints]
        WinRTSpeechSynth[Windows.Media.SpeechSynthesis\nSpeechSynthesizer + System Voices]
        WinRTMediaPlayer[Windows.Media.Playback\nMediaPlayer Audio Pipeline]
        WinRTAudioDevices[Windows.Devices.Enumeration\nMediaDevice AudioCapture Watcher]
    end

    %% UI to ViewModel
    ShellView --> ShellVM
    SettingsPage --> SettingsVM
    ScholarPage --> ScholarVM
    FlashcardsPage --> FlashcardsVM

    %% ViewModels to Coordinator & Services
    ShellVM --> IVoiceCoordinator
    ScholarVM --> IVoiceCoordinator
    FlashcardsVM --> ISpeechSynthesis
    SettingsVM --> IAudioDeviceMonitor
    SettingsVM --> ISpeechSynthesis

    %% Coordinator Orchestration
    IVoiceCoordinator --> VoiceState
    IVoiceCoordinator --> IVoiceCommandRouter
    IVoiceCoordinator --> IVoiceTranscriber
    IVoiceCoordinator --> ISpeechSynthesis
    IVoiceCoordinator --> IAudioDeviceMonitor
    IVoiceTranscriber --> IVoiceTextFormatter

    %% Routing to Actions
    IVoiceCommandRouter -.->|Dispatched Actions| ShellVM

    %% Services to WinRT Platform
    IVoiceCommandRouter --> WinRTSpeechRecog
    IVoiceTranscriber --> WinRTSpeechRecog
    ISpeechSynthesis --> WinRTSpeechSynth
    ISpeechSynthesis --> WinRTMediaPlayer
    IAudioDeviceMonitor --> WinRTAudioDevices
```

---

## 2. Platform Boundaries: Deterministic Logic vs. Environment Dependencies

To maintain rigorous architectural separation, AXORA partitions responsibilities:

### 2.1 Deterministic Subsystem Logic (AXORA Controlled)
- **Voice Command Normalization**: Trimming, lowercasing, and stripping punctuation deterministically.
- **Deterministic Matcher**: Exact primary and alias phrase matching with unambiguous rejection.
- **Safety Classification**: Enforcement of `Safe`, `ConfirmationRequired`, and `VoiceProhibited` command metadata.
- **Text Formatter**: Deterministic substitution of punctuation commands ("comma" -> `,`) and filler suppression ("um", "ah").
- **Voice Coordinator State Machine**: Atomic state transitions (`Idle`, `ListeningForCommand`, `Dictating`, `Synthesizing`, `Paused`, `Error`) preventing acoustic feedback.
- **Configurable Gating Interval**: Configurable debounce interval (initial design target: 250 ms) to allow room reverberation to settle after speech playback.
- **Sanitization & Redaction**: Ensuring private dictation text is never emitted into diagnostic log sinks.

### 2.2 Environment & Runtime Dependencies (Host Windows Environment)
- **Audio Capture Devices**: Physical presence and OS connectivity of microphone endpoints.
- **OS Microphone Permission**: User consent configured under Windows Settings > Privacy & security > Microphone.
- **Installed Speech/Language Packs**: Speech recognition acoustic and language models installed on Windows. Free-form dictation is available where supported by installed Windows language resources; if unavailable, command recognition remains operational where supported.
- **Installed Speech Voices**: System-provided Windows speech voices (`SpeechSynthesizer.AllVoices`) enumerated at runtime.
- **Audio Output Endpoint**: Active default playback device managed by Windows CoreAudio / MediaPlayer.

---

## 3. Core Service Contracts & Domain Models

### 3.1 Domain Models & Enumerations

```csharp
namespace Axora.Desktop.Models.Voice;

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

public enum AudioCaptureHealth
{
    Healthy,
    NoMicrophoneDetected,
    PermissionDenied,
    DeviceBusy,
    RecognitionUnavailable
}

public enum CommandSafetyLevel
{
    Safe,
    ConfirmationRequired,
    VoiceProhibited
}

public enum VoiceCommandCategory
{
    Navigation,
    Playback,
    DocumentEditing,
    ShellControl
}

public sealed record VoiceInfo(
    string Id,
    string DisplayName,
    string Language,
    string Gender,
    string Description,
    bool IsDefault
);

public sealed record VoiceCommandRegistration(
    string CommandId,
    string PrimaryPhrase,
    IReadOnlyList<string> Aliases,
    VoiceCommandCategory Category,
    CommandSafetyLevel SafetyLevel,
    Func<CancellationToken, Task> Action
);

public sealed record VoiceCommandMatchResult(
    bool IsMatched,
    VoiceCommandRegistration? MatchedCommand,
    string NormalizedSpokenText
);

public sealed record TranscriptionChunk(
    string RawText,
    string FormattedText,
    bool IsFinal
);
```

### 3.2 Unified Voice Coordinator (`IVoiceCoordinator`)
The `IVoiceCoordinator` serves as the centralized state manager and arbiter between speaking and listening:

```csharp
namespace Axora.Desktop.Services.Contracts;

public interface IVoiceCoordinator : IDisposable
{
    VoiceSessionState CurrentState { get; }
    bool IsVoiceNavigationEnabled { get; set; }
    AudioCaptureHealth CaptureHealth { get; }
    TimeSpan AcousticDebounceInterval { get; set; }

    Task<bool> RequestStartDictationAsync(Action<string> onFormattedChunk, CancellationToken ct = default);
    Task RequestStopDictationAsync();
    Task<bool> RequestSpeakAsync(string text, double? pitch = null, double? rate = null, CancellationToken ct = default);
    void RequestStopSpeech();
    Task<bool> StartVoiceNavigationAsync(CancellationToken ct = default);
    Task StopVoiceNavigationAsync();

    event EventHandler<VoiceSessionStateChangedEventArgs>? StateChanged;
}
```

### 3.3 Voice Command Router (`IVoiceCommandRouter`)
```csharp
namespace Axora.Desktop.Services.Contracts;

public interface IVoiceCommandRouter : IDisposable
{
    bool IsListening { get; }
    IReadOnlyList<VoiceCommandRegistration> RegisteredCommands { get; }

    void RegisterCommand(VoiceCommandRegistration command);
    void UnregisterCommand(string commandId);
    VoiceCommandMatchResult MatchCommand(string spokenPhrase);
    Task<bool> ExecuteCommandAsync(string spokenPhrase, CancellationToken ct = default);
    Task StartListeningAsync(CancellationToken ct = default);
    Task StopListeningAsync();

    event EventHandler<VoiceCommandExecutedEventArgs>? CommandExecuted;
}
```

### 3.4 Voice Transcriber & Dictation Service (`IVoiceTranscriberService`)
```csharp
namespace Axora.Desktop.Services.Contracts;

public interface IVoiceTranscriberService : IDisposable
{
    bool IsRecording { get; }
    AudioCaptureHealth DeviceHealth { get; }

    Task<bool> CheckPrerequisitesAsync(CancellationToken ct = default);
    Task StartDictationAsync(Action<TranscriptionChunk> onChunkRecognized, CancellationToken ct = default);
    Task StopDictationAsync();

    event EventHandler<VoiceTranscriberStateChangedEventArgs>? StateChanged;
}
```

### 3.5 Speech Synthesis Service (`ISpeechSynthesisService`)
```csharp
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
    Task SpeakTextAsync(string text, double? pitch = null, double? rate = null, CancellationToken ct = default);
    void Stop();
    void Pause();
    void Resume();
    void SetVoice(string voiceId);

    event EventHandler<SpeechPlaybackStateChangedEventArgs>? PlaybackStateChanged;
}
```

### 3.6 Audio Device & Permission Monitor (`IAudioDeviceMonitor`)
```csharp
namespace Axora.Desktop.Services.Contracts;

public interface IAudioDeviceMonitor : IDisposable
{
    bool HasMicrophone { get; }
    bool IsPermissionGranted { get; }
    string? DefaultCaptureDeviceName { get; }
    AudioCaptureHealth CurrentHealth { get; }

    Task RefreshStatusAsync(CancellationToken ct = default);
    event EventHandler<AudioDeviceStatusChangedEventArgs>? DeviceStatusChanged;
}
```

### 3.7 Dictation Text Formatter (`IVoiceTextFormatter`)
```csharp
namespace Axora.Desktop.Services.Contracts;

public interface IVoiceTextFormatter
{
    string FormatSpokenChunk(string rawSpokenText, bool isStartOfSentence = false);
    string CleanDisfluencies(string text);
    string ApplyPunctuationCommands(string text);
}
```

---

## 4. Command Safety Architecture & Execution Lifecycle

```
Spoken Input Received
         │
         ▼
[ Normalization Pipeline ] (Trim, lowercase, strip punctuation)
         │
         ▼
[ Deterministic Matcher ] (Exact primary phrase or registered alias match)
         │
    ┌────┴───────────────────────────┐
    │ Unmatched or Ambiguous         │ Matched Exactly
    ▼                                ▼
[ Reject & Emit Audio Cue ]    [ Check CommandSafetyLevel ]
                                     │
                 ┌───────────────────┼────────────────────────┐
                 │ Safe              │ ConfirmationRequired   │ VoiceProhibited
                 ▼                   ▼                        ▼
         [ Execute Action ]  [ Open Confirmation UI ]  [ Reject & Notify ]
                                     │
                             ┌───────┴────────┐
                             │ Confirmed      │ Cancelled
                             ▼                ▼
                     [ Execute Action ]  [ Abort Action ]
```

1. **`Safe` Commands**: Navigation across `PageMap` (12 pages), read aloud, stop playback, clear non-persistent search filter. Executed immediately upon match.
2. **`ConfirmationRequired` Commands**: Clear active editor text, reset session form. Trigger an explicit interactive UI dialog; voice cannot bypass user confirmation.
3. **`VoiceProhibited` Commands**: Deleting files, wiping vault credentials, dropping database tables, factory resetting settings. The router strictly rejects voice invocation.

---

## 5. Concurrency, Acoustic Feedback & Synchronization Model

### 5.1 Mutual Exclusion Between Speech and Microphone
To prevent acoustic feedback loops where speaker output is recaptured by the microphone:
1. **Active Speech Gating**: Calling `RequestSpeakAsync()` pauses or mutes microphone listening before playback begins.
2. **Configurable Acoustic Gating Interval**: When speech playback finishes, microphone listening is delayed by a configurable gating interval (initial design target: 250 ms). This interval provides time for acoustic reverberation to dissipate and will be validated during runtime testing.
3. **Thread Safety**: All state transitions in `VoiceCoordinator` use `SemaphoreSlim(1, 1)` serialization.
4. **UI Thread Marshalling**: WinRT background events are marshalled to the UI thread via `DispatcherQueue.TryEnqueue()`.

---

## 6. Integration Points & Scope Isolation

### 6.1 Consumer ViewModels
- **`ShellViewModel`**: Injects `IVoiceCoordinator`; registers 12 navigation commands matching `ShellViewModel.PageMap`.
- **`ScholarKitViewModel`**: Injects `IVoiceCoordinator`; routes formatted dictation into `OcrResultText`; controls document read-aloud.
- **`FlashcardsViewModel`**: Injects `ISpeechSynthesisService` for active card pronunciation.
- **`SettingsViewModel`**: Manages voice preferences, speech rate, pitch, and displays microphone status.

### 6.2 Untouched Components (W3-F Preservation)
The following W3-F components must remain completely unmodified:
- `IScholarSynthesisEngine` & `ScholarSynthesisEngine`
- `IScholarSearchService` & `ScholarSearchService`
- `IScholarIndexService` & `ScholarIndexService`
- `IScholarExtractionOrchestrator` & `ScholarExtractionOrchestrator`
- `IBoundedContextWindowBuilder` & `BoundedContextWindowBuilder`
- `IPassageChunker` & `PassageChunker`
- `GroundingValidator` & `StudyGroundingMatrix`
- `DirectMlEmbeddingService` & `SimdVectorHelper`

### 6.3 MaterialUI Isolation
- `Axora-Desktop-MaterialUI` remains completely untouched.
