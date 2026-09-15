# Phase W4 Product Contract: Voice Capabilities

**Phase**: `W4 — Voice Capabilities (AXORA Voice Subsystem)`  
**Status**: `PLANNING HARDENED (PASS 2 — AUDIT VERIFIED) / AWAITING IMPLEMENTATION AUTHORIZATION`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Implementation Baseline**: `2e8eb83a3d2735a70d288f3d2702f3507df3529a` (`feat(winui): complete W3-F study synthesis engine and grounding verification`)  
**Preceding Historical Stages**:
- `Phase W3-F — Study Synthesis Engine` (CLOSED — VERIFIED & COMMITTED: `2e8eb83a3d2735a70d288f3d2702f3507df3529a`)
- `Phase W3-E — Search / Retrieval Integration Stage` (CLOSED — HISTORICAL BASELINE: `a3dad9325f3fcf30ab4b71b088a2047e44a216ce`)
- `Phase W3-D — Local Vector Embedding & Hybrid Indexing Stage` (CLOSED — VERIFIED)
- `Phase W3-C — Scholar Document Extraction & Context Windows` (CLOSED — VERIFIED)
- `Phase W2-F — Universal Format Optimization & Quality Presets` (CLOSED — VERIFIED)
**Downstream Dependents**:
- `ShellViewModel` & `ShellView` (Hands-free voice navigation across 12 application surfaces)
- `ScholarKitViewModel` (Voice note dictation, live punctuation, document read-aloud, chat response speech)
- `FlashcardsViewModel` (Active card pronunciation, hands-free review progression)
- `SettingsViewModel` & `SettingsPage` (Voice selection, rate/pitch sliders, auto-punctuation, microphone status)
- Future Phase `W5 — AXORA Image Studio` (Modal voice triggers, voice parameter adjustment)

---

## 1. Executive Summary & User Problem Justification

### 1.1 The User Problem
Modern knowledge workers, researchers, and students spend hours continuously typing, manually navigating complex application menus, and reading dense academic prose on backlit computer displays:
1. **Academic Note-Taking Friction**: When reviewing long papers or books in Scholar Kit, manually typing notes breaks cognitive focus. Users benefit from verbally dictating summaries, questions, and insights directly into their study workspace without switching to external speech utilities.
2. **Reading Fatigue & Auditory Learning**: Reading thousands of lines of dense academic text causes visual strain. Auditory learners and proofreaders require speech synthesis to listen to extracted document sections, synthesized study summaries, and practice flashcards.
3. **Hands-Free Navigation & Accessibility Friction**: Users with temporary motor strain (RSI, tendonitis), permanent physical disabilities, or multitasking workflows (e.g. consulting physical textbooks while interacting with AXORA) face friction repeatedly clicking navigation items or typing commands.
4. **Privacy & Data Governance Risks in Cloud Speech Tools**: Many commercial speech-to-text (STT) and text-to-speech (TTS) solutions transmit user audio streams to remote cloud datacenters. For proprietary academic research, confidential student drafts, medical notes, or personal study sessions, this creates data exposure risks under FERPA, GDPR, and academic privacy standards.
5. **Fragility of Ad-Hoc Speech Code**: In AXORA's prior baseline, voice dictation and speech synthesis were wired directly into individual ViewModels (`ScholarKitViewModel`, `FlashcardsViewModel`) as isolated, ad-hoc WinRT calls. There was no centralized voice state coordinator (causing synthetic speech to echo back into the open microphone), no audio device discovery, no permission failure recovery, no voice navigation router, and zero automated test coverage.

### 1.2 The W4 Solution
Phase **W4** introduces the **AXORA Voice Subsystem**, an integrated, local-first, privacy-respecting voice infrastructure for Windows Desktop:
- **Local-First Voice Architecture**: Voice synthesis, command parsing, and core speech operations are designed to execute locally on Windows without cloud speech services, using in-box Windows Media Speech APIs and deterministic local grammar engines. Audio frames are never transmitted over network sockets.
- **Unified Voice State Coordinator (`IVoiceCoordinator`)**: A centralized state machine preventing acoustic feedback loops, managing exclusive audio sessions (`Idle`, `ListeningForCommand`, `Dictating`, `Synthesizing`, `Paused`, `Error`), and orchestrating transitions between speaking and listening.
- **Deterministic Voice Command Router (`IVoiceCommandRouter`)**: A fast, deterministic command engine that interprets spoken navigation intents ("go to dashboard", "open settings", "open flashcards") and application control actions ("read aloud", "stop speaking", "start dictation", "clear text") without cloud NLP dependencies.
- **Contextual Dictation & Formatting Engine (`IVoiceTextFormatter`)**: Continuous speech transcription formatting with automatic punctuation, capitalization, formatting commands ("new line", "new paragraph", "comma", "period", "bullet point"), and conversational filler suppression ("um", "ah").
- **Speech Synthesis Engine (`ISpeechSynthesisService`)**: Offline voice playback supporting system voice enumeration (identifying all system-provided Windows speech voices installed on the host OS), dynamic rate and pitch tuning, and sentence-level cancellation.
- **Hardware & Privacy Health Monitoring (`IAudioDeviceMonitor`)**: Continuous tracking of microphone presence, default input device switches, and graceful recovery from Windows privacy permission denial (`UnauthorizedAccessException`) with actionable user guidance to Windows Settings.
- **Full MVVM & Shell Integration**: Unified voice status indicators in `ShellView`, audio configuration panel in `SettingsPage`, and clean dependency injection throughout all consumer ViewModels.

---

## 2. Platform Boundaries: Deterministic Logic vs. Environment Dependencies

To maintain absolute technical precision, the AXORA Voice Subsystem strictly separates deterministic local logic from platform/runtime dependencies:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    AXORA VOICE SUBSYSTEM RESPONSIBILITY BOUNDARY            │
├─────────────────────────────────────────────────────────────────────────────┤
│  A. DETERMINISTIC LOCAL LOGIC (Controlled by AXORA Subsystem)               │
│  - Command phrase normalization (whitespace, lowercase, punctuation removal)│
│  - Command registration & alias mapping across 12 application surfaces      │
│  - Deterministic exact & alias matching with ambiguity rejection            │
│  - Command safety classification (Safe, Confirmation-Required, Prohibited)  │
│  - Text formatting & spoken punctuation replacement rules                   │
│  - Conversational filler token suppression ("um", "ah")                     │
│  - Voice coordinator state machine transitions & mutual exclusion gating    │
│  - Configurable acoustic gating interval (250 ms initial design target)     │
│  - Async cancellation token propagation & process shutdown cleanup          │
│  - Logging sanitization & PII redaction (zero dictated text in logs)        │
├─────────────────────────────────────────────────────────────────────────────┤
│  B. ENVIRONMENT & RUNTIME DEPENDENCIES (Provided by Windows 11 / Host PC)   │
│  - Microphone hardware presence, endpoint availability, and hotplug events  │
│  - Windows OS microphone privacy permission consent (Settings > Privacy)    │
│  - Speech recognition engine runtime availability (WinRT SpeechRecognizer)  │
│  - Installed Windows speech/language resources (Acoustic & Language Packs)  │
│  - Installed system-provided Windows speech voices (VoiceInformation list)  │
│  - Audio playback device endpoint & Windows Media Player audio pipeline     │
│  - Exact offline dictation availability (dependent on OS language model)    │
└─────────────────────────────────────────────────────────────────────────────┘
```

> [!IMPORTANT]
> **Windows Speech/Language Resource Prerequisite**: Free-form dictation via `SpeechRecognitionTopicConstraint` on Windows 10/11 is not universally pre-installed across all Windows editions. It relies on the host OS having installed speech recognition language packs. If speech resources or permissions are unavailable, AXORA gracefully disables dictation and provides clear user guidance while preserving local command grammar routing where supported.

---

## 3. Capability Classification Scheme

In strict accordance with the AXORA architectural governance standards, every capability is classified under one authoritative scheme:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      AUTHORITATIVE CAPABILITY CLASSIFICATION                │
├─────────────────────────────────────────────────────────────────────────────┤
│  1. REQUIRED LOCAL LOGIC                                                    │
│     Bundled natively in AXORA application logic; executes deterministically │
│     offline with zero network requests and zero additional downloads.       │
│                                                                             │
│  2. REQUIRED SYSTEM-PROVIDED WINDOWS CAPABILITY                             │
│     Provided by Windows 11 in-box WinRT runtime APIs; executes locally      │
│     subject to host OS configuration, device presence, and speech packs.    │
│                                                                             │
│  3. OPTIONAL FUTURE LOCAL CAPABILITY                                        │
│     Self-contained local offline model (e.g. Whisper ONNX via DirectML)     │
│     downloadable via Download Manager; NOT required for Phase W4.           │
│                                                                             │
│  4. OUT OF SCOPE / PROHIBITED NETWORK CAPABILITY                            │
│     Cloud speech services, external telemetry, always-listening daemons.    │
│     Strictly prohibited or excluded from Phase W4.                          │
│                                                                             │
│  5. FUTURE INTEGRATION SEAM                                                 │
│     Architectural interface point reserved for post-W4 integration          │
│     (e.g. Scholar Synthesis voice queries, custom SRGS academic grammars).  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Detailed Capability Classification Matrix

| Capability Identifier | Description | Authoritative Classification | Rationale & Governance Boundary |
|---|---|:---:|---|
| **W4-CAP-TTS-01** | System Voice Enumeration | **REQUIRED SYSTEM-PROVIDED WINDOWS CAPABILITY** | Enumerate system-provided Windows speech voices (`SpeechSynthesizer.AllVoices`) with name, language, gender, and description. |
| **W4-CAP-TTS-02** | Speech Synthesis Playback | **REQUIRED SYSTEM-PROVIDED WINDOWS CAPABILITY** | Synthesize text to speech with pitch (0.5x–1.5x) and rate (0.5x–3.0x) controls, with instant, non-blocking cancellation. |
| **W4-CAP-TTS-03** | TTS Playback Progression | **REQUIRED LOCAL LOGIC** | Track playback progression and notify consumers when speech begins, completes, pauses, or fails. |
| **W4-CAP-CMD-01** | Voice Navigation Routing | **REQUIRED LOCAL LOGIC** | Deterministically match spoken navigation phrases matching `ShellViewModel.PageMap` (12 pages) using local constraint grammars. |
| **W4-CAP-CMD-02** | Voice Control Commands | **REQUIRED LOCAL LOGIC** | Recognize playback and document commands ("stop", "read aloud", "dictate note", "clear text", "open command palette"). |
| **W4-CAP-CMD-03** | Command Safety Guard | **REQUIRED LOCAL LOGIC** | Strictly categorize commands into `Safe`, `ConfirmationRequired`, and `VoiceProhibited`. Prohibit voice execution of destructive operations. |
| **W4-CAP-COORD-01** | Unified Voice Coordinator | **REQUIRED LOCAL LOGIC** | Centralized state machine preventing acoustic feedback loops: muting/pausing microphone capture during active TTS output. |
| **W4-CAP-COORD-02** | Configurable Acoustic Debounce | **REQUIRED LOCAL LOGIC** | Configurable gating interval (initial design target: 250 ms) after speech finishes before microphone listening resumes. |
| **W4-CAP-DEV-01** | Microphone Device Detection | **REQUIRED SYSTEM-PROVIDED WINDOWS CAPABILITY** | Detect physical microphone presence, track default capture endpoint changes, and handle hotplug events cleanly via WinRT. |
| **W4-CAP-DEV-02** | Permission Denial Handling | **REQUIRED LOCAL LOGIC** | Intercept `0x80070005` (E_ACCESSDENIED) gracefully without app crash; surface non-intrusive guide to Windows Privacy Settings. |
| **W4-CAP-FMT-01** | Dictation Text Formatting | **REQUIRED LOCAL LOGIC** | Deterministic rule-based normalization for spoken punctuation ("comma" -> `,`, "period" -> `.`, "new line" -> `\n`, etc.). |
| **W4-CAP-SET-01** | Voice Settings Persistence | **REQUIRED LOCAL LOGIC** | Persist selected voice ID, speech rate, pitch, voice navigation enabled, and auto-punctuation in `IAppSettingsService`. |
| **W4-CAP-A11Y-01** | Visual Voice Status Badges | **REQUIRED LOCAL LOGIC** | Accessible visual cues in Shell and Scholar Kit for active listening, speaking, muted, or error states. |
| **W4-CAP-METER-01** | Audio Input Level Metering | **OPTIONAL FUTURE LOCAL CAPABILITY** | Peak amplitude indicator for microphone audio activity to provide visual feedback if supported without native audio driver bloat. |
| **W4-CAP-WAKE-01** | Always-Listening Wake Word | **OUT OF SCOPE / PROHIBITED NETWORK CAPABILITY** | Continuous background daemon ("Hey Axora") causes severe battery drain, CPU load, and privacy perception concerns. |
| **W4-CAP-LLM-01** | Conversational Voice AI Chatbot | **OUT OF SCOPE / PROHIBITED NETWORK CAPABILITY** | Full conversational speech LLM agent is explicitly decoupled; W4 is an input/output subsystem, not an open-ended chatbot. |
| **W4-CAP-BATCH-01** | Audio File Batch Transcription | **OUT OF SCOPE / PROHIBITED NETWORK CAPABILITY** | Transcribing external `.mp3`/`.wav` recordings belongs to downstream batch media processing, not real-time desktop UI. |
| **W4-CAP-CLOUD-01** | Cloud Speech API Telemetry | **OUT OF SCOPE / PROHIBITED NETWORK CAPABILITY** | Transmitting voice audio to cloud services is prohibited; local-first invariant. |
| **W4-CAP-WHISPER-01** | Local Whisper ONNX Engine | **FUTURE INTEGRATION SEAM** | Optional local Whisper model downloadable via Download Manager for open-vocabulary dictation independent of Windows Speech Packs. |
| **W4-CAP-AGENT-01** | Scholar Synthesizer Voice Seam | **FUTURE INTEGRATION SEAM** | Direct voice query pipeline connecting `IVoiceCoordinator` to `IScholarSynthesisEngine` and `DocumentChatService`. |

---

## 5. Command Safety Architecture

Voice commands must never perform unconfirmed destructive actions. To enforce this, every command registered with the `IVoiceCommandRouter` must specify an explicit safety level:

```csharp
namespace Axora.Desktop.Models.Voice;

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
```

### Safety Rules:
1. **Direct Execution**: Only commands marked `CommandSafetyLevel.Safe` may execute directly upon voice match.
2. **Interactive Confirmation**: Commands marked `ConfirmationRequired` must invoke a UI confirmation dialog requesting manual user verification before proceeding.
3. **Voice Prohibited**: Destructive operations (such as deleting files, purging caches, wiping vault credentials, or dropping database sessions) are classified as `VoiceProhibited`. The command router will reject any attempt to invoke them via voice and display a safety notification.

---

## 6. Deterministic Command Matching Specification

To eliminate unpredictability, command matching in Phase W4 is strictly deterministic:

1. **Input Normalization Pipeline**:
   - Trim leading and trailing whitespace.
   - Convert to lowercase using invariant culture.
   - Strip leading/trailing punctuation characters (`.`, `,`, `!`, `?`).
   - Collapse consecutive internal whitespace into single spaces.
2. **Deterministic Matching Strategy**:
   - **Step 1 (Exact Primary Match)**: Test normalized spoken phrase against `PrimaryPhrase` of all registered commands.
   - **Step 2 (Exact Alias Match)**: Test normalized spoken phrase against all registered `Aliases` of each command.
   - **Step 3 (Ambiguity & Rejection Guard)**: If the spoken phrase matches multiple registered commands with identical precedence, or matches zero commands, the router returns `IsMatched = false` with `MatchedCommand = null`.
3. **No Unbounded Fuzzy Matching**: Vague "semantic fuzzy matching" is prohibited. String distance algorithms (e.g. Levenshtein) are only permitted if bounded by a strict threshold and deterministic disambiguation rules. Any ambiguous phrase results in non-execution and a neutral auditory/visual cue.

---

## 7. Primary Data Contracts & Service Boundaries

### 7.1 Speech Synthesis Contract (`ISpeechSynthesisService`)
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

### 7.2 Voice Transcriber & Dictation Contract (`IVoiceTranscriberService`)
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

### 7.3 Voice Command Router Contract (`IVoiceCommandRouter`)
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

### 7.4 Unified Voice Coordinator Contract (`IVoiceCoordinator`)
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

### 7.5 Audio Device Monitor Contract (`IAudioDeviceMonitor`)
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

---

## 8. Privacy & Data Governance Contract

1. **Local-First Processing**: Designed to execute speech processing locally without sending audio to cloud servers.
2. **Ephemeral Audio Buffering**: Microphone audio frames are processed in volatile memory and discarded upon completion of recognition.
3. **No Audio Spooling to Disk**: Voice dictation and voice commands do not save recorded audio streams (`.wav`, `.pcm`) to local disk storage.
4. **Log Sanitization & Redaction**: Application logging frameworks (`ILogger`) must redact dictated text and private user note content. Logs are restricted to operational metadata (e.g. `CommandExecuted: tag=Dashboard`, `DictationSessionStarted`).
5. **Clear Permission Boundaries**: If microphone permission is revoked by the user in Windows Settings, capture halts immediately and a non-intrusive prompt directs the user to `ms-settings:privacy-microphone`.
