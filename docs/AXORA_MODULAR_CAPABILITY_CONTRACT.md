# AXORA Desktop — Modular Capability Contract
## Capability Classification, Manifest Specification & Lifecycle Governance

**Document Version**: 1.0.0 (W3-A.1 Architectural Baseline)  
**Target Platform**: Windows 11 (x64 / ARM64) · .NET 9.0 · Windows App SDK 1.6  
**Status**: **PLANNING CONTRACT (ZERO IMPLEMENTATION COMMENCED)**  

---

## 1. Capability Taxonomy

To prevent installer bloat, maintain user control, and respect storage limits, AXORA classifies every feature and engine into one of four formal capability classes:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       AXORA CAPABILITY CLASSIFICATION                       │
├─────────────────────────────────────────────────────────────────────────────┤
│  CLASS A: CORE LOCAL CAPABILITY                                             │
│  - Bundled natively in the base AXORA installer (<150 MB total package).    │
│  - Executes 100% locally and offline out-of-the-box.                       │
│  - Zero network, zero downloads, zero external account required.            │
│  - Examples: Universal Converter (WIC/SkiaSharp), basic PDF text extraction │
│    (PdfSharpCore), text formatting, local JSON persistence, settings.      │
├─────────────────────────────────────────────────────────────────────────────┤
│  CLASS B: OPTIONAL LOCAL CAPABILITY                                         │
│  - Decoupled from base installation to preserve disk space.                 │
│  - Requires a deliberate, one-time user-approved download.                  │
│  - Once downloaded, functions 100% locally and offline forever.             │
│  - Stored in controlled AXORA storage (%APPDATA%\Axora\Capabilities\).      │
│  - Examples: DirectML dense vector embedding model (all-MiniLM-L6-v2),      │
│    Whisper speech recognition model, local SLM (Phi-3-mini), ImageMagick.   │
├─────────────────────────────────────────────────────────────────────────────┤
│  CLASS C: OPTIONAL NETWORK CAPABILITY                                       │
│  - Inherently requires an external Internet connection to function.         │
│  - Must be explicitly labeled with a distinct network badge (🌐).           │
│  - Requires active user consent before any connection or query.             │
│  - Shows transparent preview of outbound data; logs zero private text.      │
│  - Examples: Online academic cross-referencing (Crossref/arXiv API),        │
│    web-wide plagiarism verification, optional cloud LLM fallback.           │
├─────────────────────────────────────────────────────────────────────────────┤
│  CLASS D: SYSTEM-PROVIDED CAPABILITY                                        │
│  - Leverages hardware or operating system infrastructure already present    │
│    in Windows 11 without shipping duplicate binaries.                       │
│  - Zero download required if OS feature is active; guides user if missing.  │
│  - Examples: Windows Runtime Native OCR (OcrEngine), Windows Media Speech   │
│    dictation and Neural TTS, DirectML DirectX 12 driver runtime.            │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Capability States & Visual Language

Every capability communicates its real-time readiness through a standardized visual state badge across all AXORA pages:

| State Code | UI Badge Text | Visual Tone | Meaning & User Action |
|---|---|:---:|---|
| `AvailableLocally` | **Ready (Offline)** | Green / Neutral | Fully installed, verified, and ready for instant offline execution. |
| `NotInstalled` | **Available to Install** | Accent Blue | Optional capability not yet downloaded. Clicking initiates review. |
| `DownloadRequired` | **Download Required (X MB)** | Accent Blue | Feature requires downloading assets before first use. |
| `NetworkRequired` | **Online Only (🌐)** | Amber / Purple | Inherently requires Internet. Disables gracefully when offline. |
| `HardwareUnsupported` | **Hardware Unsupported** | Muted Grey | Device lacks required GPU feature level, VRAM, AVX2, or NPU. |
| `TemporarilyUnavailable` | **Service Unavailable** | Red / Warning | Corrupt files, missing driver, or background task busy. |

---

## 3. Modular Capability Manifest Specification

Future capabilities will be defined declaratively via a strongly-typed manifest. This specification establishes the data contract for capability registration:

```csharp
namespace Axora.Desktop.Models.Capabilities;

public enum CapabilityClass
{
    CoreLocal,
    OptionalLocal,
    OptionalNetwork,
    SystemProvided
}

public enum CapabilityCategory
{
    DocumentAndText,
    ImageAndVision,
    AudioAndSpeech,
    NeuralIntelligence,
    AcademicAndIntegrity,
    SystemAndPreservation
}

public sealed record CapabilityManifest
{
    public required string CapabilityId { get; init; }
    public required string DisplayName { get; init; }
    public required string Purpose { get; init; }
    public required CapabilityCategory Category { get; init; }
    public required CapabilityClass Class { get; init; }

    // Runtime & Engine Details
    public string RequiredRuntime { get; init; } = "None"; // e.g., "DirectX 12 (DML)", "Windows App SDK 1.6"
    public string RequiredModel { get; init; } = "None";   // e.g., "all-MiniLM-L6-v2-int8.onnx"
    public string DetectionStrategy { get; init; } = "FileExists";

    // Storage & Footprint
    public long DownloadSizeBytes { get; init; }
    public long InstalledSizeBytes { get; init; }
    public string StorageSubdirectory { get; init; } = "Capabilities";

    // Hardware & OS Compatibility
    public string MinimumOs { get; init; } = "Windows 10 Build 19041+";
    public string RecommendedOs { get; init; } = "Windows 11 24H2+";
    public string HardwareRequirements { get; init; } = "x64 or ARM64 with 4 GB RAM";
    public bool RequiresDirectX12 { get; init; }
    public bool RequiresNpu { get; init; }

    // Network & Offline Availability
    public bool FunctionsCompletelyOffline { get; init; } = true;
    public string NetworkTargetEndpoints { get; init; } = "None";

    // Provenance & Licensing
    public IReadOnlyList<string> Dependencies { get; init; } = [];
    public required string License { get; init; } // e.g., "Apache-2.0", "MIT", "Microsoft OS Component"
    public string Vendor { get; init; } = "Axora Open Desktop";

    // Lifecycle Policies
    public bool CanRepair { get; init; } = true;
    public bool CanReinstall { get; init; } = true;
    public bool CanRemove { get; init; } = true;
    public required string DataPrivacyBehavior { get; init; }
}
```

---

## 4. Evolution of W1.5 Download Manager into Capability Manager

Phase **W1.5** introduced the `ExtensionModel`, `IExtensionRegistry`, and `DownloadManagerPage` for managing external dependencies (specifically ImageMagick). 

In Phase W3 and future phases, this subsystem cleanly evolves into the **AXORA Capability Manager** without sacrificing any safety invariants:

```
┌─────────────────────────────────────────────────────────────────────────┐
│              W1.5 FOUNDATION                FUTURE CAPABILITY MANAGER   │
├─────────────────────────────────────────────────────────────────────────┤
│  ExtensionModel                ───>         CapabilityModel             │
│  IExtensionRegistry            ───>         ICapabilityRegistry         │
│  IExtensionDownloader          ───>         ICapabilityDownloader       │
│  IExtensionValidator (SHA256)  ───>         ICapabilityValidator (Hash) │
│  IExtensionCacheService        ───>         ICapabilityStorageService   │
│  DownloadManagerPage           ───>         CapabilityStudioPage        │
└─────────────────────────────────────────────────────────────────────────┘
```

### Preserved Safety Guarantees:
1. **Zero Silent Downloads**: Downloads occur exclusively upon direct user confirmation.
2. **Cryptographic Verification**: Every downloaded package or model weight is verified against a hardcoded SHA-256 hash before extraction.
3. **Staging & Atomic Activation**: Files are staged in `%TEMP%\Axora\Staging\` and moved atomically to `%APPDATA%\Axora\Capabilities\<Id>\`.
4. **Isolated Cache Ownership**: Clearing capability caches or executing a "Clean Reinstall" touches ONLY the target capability's folder, NEVER user documents, notes, or settings.
5. **No Elevate Privileges Unless Required**: Capabilities are installed in user-profile `%APPDATA%`, requiring zero UAC administrative prompts.

---

## 5. Graceful Degradation Invariant

Whenever an optional capability is missing, uninstalled, or unsupported by hardware:
- **No Feature Page May Crash**: Missing an optional model or tool must never throw an unhandled exception or block page navigation.
- **Clear User Messaging**: Feature pages render an inline, accessible banner (e.g., `"DirectML Embedding Model not installed. Heuristic search active."`).
- **Direct Navigation Bridge**: The banner includes a direct link button: `[Get Capability in Manager]` that navigates to the Capability Manager with the relevant item pre-focused.
