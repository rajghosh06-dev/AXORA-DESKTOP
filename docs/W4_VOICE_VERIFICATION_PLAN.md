# Phase W4 Verification Plan: AXORA Voice Subsystem

**Phase**: `W4 — Voice Capabilities (AXORA Voice Subsystem)`  
**Status**: `PLANNING HARDENED (PASS 2 — AUDIT VERIFIED) / AWAITING IMPLEMENTATION AUTHORIZATION`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Implementation Baseline**: `2e8eb83a3d2735a70d288f3d2702f3507df3529a` (`feat(winui): complete W3-F study synthesis engine and grounding verification`)  
**Historical Preceding Baseline**: `a3dad9325f3fcf30ab4b71b088a2047e44a216ce` (`feat(winui): complete W3-E search and retrieval integration`)  

---

## 1. Verification Principles & Testing Philosophy

The **AXORA Voice Subsystem** touches hardware audio endpoints, unmanaged WinRT COM handles, and real-time user speech interactions. To ensure reliable execution across both automated CI environments (where physical microphones, sound cards, and audio input streams are absent) and real-world hardware rigs, the verification strategy is founded upon four distinct testing tiers:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       W4 VERIFICATION TIER HIERARCHY                        │
├─────────────────────────────────────────────────────────────────────────────┤
│  TIER 1: DETERMINISTIC LOGIC (Unit Tests - Headless CI)                     │
│  - Proves pure algorithmic logic: normalization, punctuation replacement,   │
│    disfluency stripping, deterministic command matching, safety levels.     │
│                                                                             │
│  TIER 2: MOCKED PLATFORM RESILIENCE (Mock Driver Tests - Headless CI)       │
│  - Proves state machine coordination, mutual exclusion, exception traps,    │
│    and lifecycle disposal using driver interfaces (ISpeechRecognizerDriver).│
│                                                                             │
│  TIER 3: ENVIRONMENT-DEPENDENT RUNTIME TESTS (Physical Hardware / Windows)  │
│  - Gathers empirical runtime evidence: microphone endpoint discovery,       │
│    installed speech language pack detection, physical audio playback.       │
│                                                                             │
│  TIER 4: RUNTIME OBSERVATIONS & DIAGNOSTICS (Profiling & Telemetry Checks)  │
│  - Provides empirical observations: network socket monitoring (0 packets),   │
│    memory working-set diagnostics, and UI responsiveness observations.      │
└─────────────────────────────────────────────────────────────────────────────┘
```

> [!IMPORTANT]
> **Environment Dependency Distinction**: Tests requiring physical audio hardware, active microphone endpoints, or specific installed Windows language packs are explicitly classified as **Environment-Dependent**. Headless CI environments rely on Tier 1 and Tier 2 driver-isolated tests for gating.

---

## 2. Test Suite Architecture & Organization

The W4 test suite is integrated directly into the standalone console test project `Axora.Desktop.Tests`:

```
Axora.Desktop.Tests/
├── Suites/
│   ├── Voice/
│   │   ├── W4_VoiceCommandRouterTests.cs      (Command grammar, normalization, alias matching, safety levels)
│   │   ├── W4_VoiceTextFormatterTests.cs      (Punctuation commands, capitalization, disfluencies)
│   │   ├── W4_VoiceCoordinatorTests.cs        (State machine, acoustic feedback gating, configurable debounce)
│   │   ├── W4_AudioDeviceMonitorTests.cs      (Device enumeration, hotplug events, permissions)
│   │   ├── W4_SpeechSynthesisTests.cs         (Pitch/rate clamping, voice enumeration, serialization)
│   │   ├── W4_ViewModelVoiceIntegrationTests.cs (Shell, ScholarKit, Flashcards, Settings wiring)
│   │   ├── W4_VoiceAdversarialChaosTests.cs   (Handle diagnostics, race conditions, mock hardware faults)
│   │   └── W4_VoicePrivacySecurityTests.cs    (Network socket observation, disk file scan, log redaction)
└── Mocks/
    ├── MockSpeechRecognizerDriver.cs
    ├── MockSpeechSynthesizerDriver.cs
    └── MockAudioEndpointWatcher.cs
```

---

## 3. Detailed Test Matrix by Tier

### 3.1 Tier 1: Deterministic Unit Tests (Pure Logic, Headless CI)
- `Test_VoiceTextFormatter_Punctuation_ReplacesSpokenPeriod`: Spoken `"hello period world"` -> `"hello. world"`.
- `Test_VoiceTextFormatter_Punctuation_ReplacesSpokenComma`: Spoken `"apples comma oranges"` -> `"apples, oranges"`.
- `Test_VoiceTextFormatter_Punctuation_ReplacesNewLine`: Spoken `"first line new line second line"` -> `"first line\nsecond line"`.
- `Test_VoiceTextFormatter_Punctuation_ReplacesNewParagraph`: Spoken `"intro new paragraph details"` -> `"intro\n\ndetails"`.
- `Test_VoiceTextFormatter_Disfluency_StripsIsolatedUmAndAh`: Spoken `"we should um consider this ah hypothesis"` -> `"we should consider this hypothesis"`.
- `Test_VoiceTextFormatter_Disfluency_PreservesWordsWithSubstrings`: Spoken `"an umbrella for the autumn"` -> retains `"umbrella"` and `"autumn"`.
- `Test_VoiceCommandRouter_NormalizePhrase_TrimsAndLowercases`: Spoken `"   Go To Dashboard !  "` -> `"go to dashboard"`.
- `Test_VoiceCommandRouter_MatchCommand_ExactPrimary`: Matches registered primary phrase deterministically.
- `Test_VoiceCommandRouter_MatchCommand_ExactAlias`: Matches registered alias (e.g. `"open scholar kit"` -> `ScholarKit`) deterministically.
- `Test_VoiceCommandRouter_MatchCommand_RejectsUnregistered`: Input `"fly me to the moon"` matches zero commands and returns `IsMatched = false`.
- `Test_VoiceCommandRouter_SafetyGuard_BlocksVoiceProhibitedCommand`: Direct voice invocation of a `CommandSafetyLevel.VoiceProhibited` command is rejected.
- `Test_VoiceCommandRouter_SafetyGuard_RequiresConfirmationForDangerousAction`: Command marked `ConfirmationRequired` invokes confirmation flow rather than executing directly.

### 3.2 Tier 2: Mocked Platform & Coordinator Tests (Headless CI)
- `Test_VoiceCoordinator_MutualExclusion_SpeechPausesDictation`: Invoking `RequestSpeakAsync()` while `Dictating` transitions state to `Synthesizing` and pauses recognition.
- `Test_VoiceCoordinator_ConfigurableDebounce_DelaysMicResumption`: Verifies that microphone listening resumption is delayed by `AcousticDebounceInterval` after speech ends.
- `Test_VoiceCoordinator_StateTransition_IdleToDictating`: Validates state transition event fired and properties updated.
- `Test_VoiceCoordinator_ConcurrentRequests_SerializedCleanly`: Multiple concurrent start/stop/speak requests execute without throwing deadlocks or race exceptions.
- `Test_AudioDeviceMonitor_PermissionDenied_SurfacesGracefully`: Simulates WinRT throwing `UnauthorizedAccessException` (`0x80070005`); verifies `AudioCaptureHealth.PermissionDenied` is set and app does not crash.
- `Test_AudioDeviceMonitor_NoMicrophone_DisablesVoiceState`: Simulates 0 connected capture devices; verifies `VoiceSessionState.Disabled`.
- `Test_AudioDeviceMonitor_HotplugDisconnect_HaltsActiveDictation`: Simulates active microphone unplugged; verifies active session stops safely without hanging.

### 3.3 Tier 3: Environment-Dependent Runtime Tests (Physical Windows Environment)
- `Test_SpeechSynthesis_EnumerateSystemVoices_ReturnsInstalledVoices`: Enumerate system-provided Windows speech voices on host machine (`SpeechSynthesizer.AllVoices`).
- `Test_SpeechRecognition_CheckLanguagePackPrerequisites`: Query Windows speech recognition engine for installed language packs and dictation scenario availability.
- `Test_AudioEndpoint_VerifyDefaultCaptureDevice`: Query Windows CoreAudio API for physical capture device availability on current machine.

### 3.4 Tier 4: Runtime Observations & Diagnostics
- `Diagnostic_VoicePrivacy_SocketObservation`: Monitors system socket activity during voice processing session; provides runtime evidence of zero outbound network bytes.
- `Diagnostic_VoicePrivacy_ZeroDiskSpooling`: Scans temp folders and app data; verifies zero temporary audio files (`.wav`, `.pcm`, `.raw`) created during voice operations.
- `Diagnostic_VoicePrivacy_LogSanitization`: Runs dictation with sensitive canary phrase `"CONFIDENTIAL_RESEARCH_PAPER_TEST"`; asserts phrase does NOT appear in `ILogger` output.
- `Diagnostic_VoiceTranscriber_RapidStartStop_HandleInspection`: Executes 50 rapid start/stop cycles; inspects process handles to observe clean cleanup.
- `Diagnostic_SpeechSynthesis_RapidConcurrentCalls_10Threads`: Dispatches 10 overlapping `SpeakTextAsync` calls; verifies serialization and clean completion.

---

## 4. Engineering Design Targets (To Be Validated During Testing)

> [!NOTE]
> All numerical figures below are **engineering design targets** to be evaluated through runtime diagnostics during Phase W4 implementation. They are not guaranteed platform limits or correctness proofs.

| Metric | Proposed Design Target | Measurement Methodology | Target Status |
|---|:---:|---|:---:|
| **Voice Command Parsing Latency** | < 10 ms | Stopwatch timing of `MatchCommand()` across 50 registered commands. | Engineering target until measured |
| **Dictation Text Formatting Latency** | < 5 ms | Stopwatch timing of `FormatSpokenChunk()` on 500-character string. | Engineering target until measured |
| **State Machine Transition Latency** | < 15 ms | Transition timing between `Idle`, `Dictating`, `Synthesizing`. | Engineering target until measured |
| **Acoustic Gating Debounce Window** | 250 ms | Configurable silence duration before microphone listening resumes. | Initial design target (configurable) |
| **Microphone Handle Cleanup Time** | < 50 ms | Teardown and disposal duration of WinRT `SpeechRecognizer`. | Engineering target until measured |
| **Process Working Set Delta** | < 30 MB | Observed delta in managed memory during active voice operations. | Diagnostic target until measured |
| **UI Responsiveness** | Responsive rendering | WinUI composition thread rendering during active speech recognition. | Performance observation target |

---

## 5. Traceability Matrix (Rule to Test Mapping)

| Rule ID | Rule Summary | Primary Verification Method | Test Classification |
|---|---|---|---|
| **R-VOICE-01** | Network Isolation | `Diagnostic_VoicePrivacy_SocketObservation` | Runtime Observation |
| **R-VOICE-02** | Ephemeral Audio Buffering | `Diagnostic_VoicePrivacy_MemoryObservation` | Runtime Diagnostic |
| **R-VOICE-03** | No Audio File Spooling | `Diagnostic_VoicePrivacy_ZeroDiskSpooling` | Deterministic Assertion |
| **R-VOICE-04** | Acoustic Feedback Gating | `Test_VoiceCoordinator_MutualExclusion_SpeechPausesDictation` | Mocked Platform Test |
| **R-VOICE-05** | Configurable Acoustic Debounce | `Test_VoiceCoordinator_ConfigurableDebounce_DelaysMicResumption` | Mocked Platform Test |
| **R-VOICE-06** | Microphone Permission Handling | `Test_AudioDeviceMonitor_PermissionDenied_SurfacesGracefully` | Mocked Platform Test |
| **R-VOICE-07** | No Microphone Fallback | `Test_AudioDeviceMonitor_NoMicrophone_DisablesVoiceState` | Mocked Platform Test |
| **R-VOICE-08** | Device Hotplug Recovery | `Test_AudioDeviceMonitor_HotplugDisconnect_HaltsActiveDictation` | Mocked Platform Test |
| **R-VOICE-09** | Command Safety Classification | `Test_VoiceCommandRouter_SafetyGuard_BlocksVoiceProhibitedCommand` | Deterministic Unit Test |
| **R-VOICE-10** | Deterministic Command Matching | `Test_VoiceCommandRouter_MatchCommand_ExactPrimary` | Deterministic Unit Test |
| **R-VOICE-11** | Command Normalization | `Test_VoiceCommandRouter_NormalizePhrase_TrimsAndLowercases` | Deterministic Unit Test |
| **R-VOICE-12** | Dictation Punctuation Commands | `Test_VoiceTextFormatter_Punctuation_ReplacesSpokenPeriod` | Deterministic Unit Test |
| **R-VOICE-13** | Disfluency Suppression | `Test_VoiceTextFormatter_Disfluency_StripsIsolatedUmAndAh` | Deterministic Unit Test |
| **R-VOICE-14** | Deterministic WinRT Disposal | `Diagnostic_VoiceTranscriber_RapidStartStop_HandleInspection` | Runtime Diagnostic |
| **R-VOICE-15** | TTS Pitch/Rate Clamping | `Test_SpeechSynthesis_ClampsRateAndPitchSafeBoundaries` | Deterministic Unit Test |
| **R-VOICE-16** | Empty Speech Text Guard | `Test_SpeechSynthesis_EmptyText_IsNoOp` | Deterministic Unit Test |
| **R-VOICE-17** | Concurrent Speak Serialization | `Diagnostic_SpeechSynthesis_RapidConcurrentCalls_10Threads` | Runtime Diagnostic |
| **R-VOICE-18** | Non-Blocking UI Execution | `Test_VoiceCoordinator_AsyncOperations_DoNotBlockUI` | Mocked Platform Test |
| **R-VOICE-19** | UI Event Thread Marshalling | `Test_ViewModelVoiceIntegration_DispatchesToUIThread` | Mocked Platform Test |
| **R-VOICE-20** | Log Redaction | `Diagnostic_VoicePrivacy_LogSanitization` | Deterministic Assertion |
| **R-VOICE-21** | Process Shutdown Cancellation | `Test_VoiceCoordinator_ProcessShutdown_CancelsPendingOperations` | Mocked Platform Test |
| **R-VOICE-22** | Speech Language Pack Prerequisite | `Test_SpeechRecognition_CheckLanguagePackPrerequisites` | Environment-Dependent |
| **R-VOICE-23** | Voice Settings Round-Trip | `Test_SettingsViewModel_VoiceSettings_RoundTripPersistence` | Deterministic Unit Test |
| **R-VOICE-24** | W3-F Component Immutability | `Audit_W3F_Architecture_RemainsUnmodified` | Git Diff Audit |
