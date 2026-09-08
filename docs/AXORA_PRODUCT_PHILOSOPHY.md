# AXORA Desktop — Product Philosophy & Architectural Principles

**Document Version**: 2.0.0 (W3-A.1 Architectural Baseline)  
**Target Platform**: Windows 11 (x64 / ARM64) · .NET 9.0 · Windows App SDK 1.6  
**Status**: **ACTIVE PRODUCT ARCHITECTURE CONTRACT**  

---

## 1. The Core Paradigm Shift

AXORA Desktop originally began under a strict **"LOCAL-ONLY"** premise. While local execution remains a vital strength, treating "local-only" as an absolute dogma creates severe product blindspots:
- It either excludes high-value user workflows that inherently require network access (such as web-wide academic citation verification or reference resolution), OR
- It forces multi-gigabyte neural models into the base installer, bloatware-style, even for users who only want simple document tools.

Therefore, the product direction for AXORA formally evolves from **"LOCAL-ONLY"** to:

> ### **"LOCAL-FIRST, OFFLINE-CAPABLE, MODULAR, USER-CONTROLLED"**

Every feature in AXORA must be grounded in solving a concrete, demonstrable user problem rather than chasing technology or AI hype. Technology must serve human agency, privacy, and desktop productivity.

---

## 2. The Ten Core AXORA Principles

### 1. Local-First
All core workflows—document parsing, media conversion, text editing, notes synthesis, data indexing, and file management—execute on the user's local workstation whenever technically practical. The desktop machine is the primary computing engine, not merely a thin client for a remote server.

### 2. Offline-Capable
Core features must never fail, lock up, or degrade because an Internet connection is absent. If a user is on an airplane, in a secure academic laboratory, or experiencing a broadband outage, AXORA remains fully functional for all core workflows.

### 3. Optional Network Capabilities
Network-dependent functionality is permissible and welcomed when it delivers clear, genuine utility to the user (e.g., cross-referencing published academic papers, verifying sources against open academic indexes, or querying an optional cloud model provider). However, network access must **always be user-initiated, explicitly labeled, and opt-in**.

### 4. Modular Capabilities (Lean Base Architecture)
Heavy components—such as dense vector embedding models, large language models (SLMs/LLMs), specialized speech models, generative image weights, and massive OCR language packs—must **never** bloat the base AXORA installer. The core application installer remains lightweight, fast, and agile. Specialized capabilities are delivered as modular, decoupled packages.

### 5. User-Controlled Installation
The user retains absolute sovereignty over their storage and computing resources. AXORA will never silently download, install, update, or activate heavy models or external dependencies in the background without explicit user authorization.

### 6. Storage & Hardware Awareness
Before any optional download or model initialization, AXORA must transparently communicate:
- Exact download size (compressed archive bytes).
- Exact installed footprint on disk (unpacked weights, cache, binaries).
- RAM and VRAM requirements.
- Hardware prerequisites (e.g., minimum DirectX 12 feature level, AVX2 support, or NPU TOPS requirement).

### 7. Transparent Capability States
At every point in the user journey, AXORA must provide unambiguous, non-technical feedback regarding the availability of any capability:
- **Available Locally**: Installed, verified, and ready for instant offline execution.
- **Not Installed**: Available for download; user can install on demand.
- **Download Required**: Requires a one-time download, after which it runs 100% offline.
- **Network Required**: Inherently requires an active network connection (e.g., live web verification).
- **Hardware Unsupported**: Device lacks the required GPU, VRAM, instruction set, or NPU.
- **Temporarily Unavailable**: Busy, damaged, or pending user repair.

### 8. No Silent Network Activity
AXORA must never make background telemetry calls, ping tracking endpoints, or phone home on startup. Every outbound network packet must map directly to an explicit, deliberate user action (e.g., clicking "Check for Updates" or "Verify Citations Online").

### 9. No Silent Data Upload
User documents, research notes, source code, photos, and voice recordings must **never** be transmitted over the network merely because a feature exists. If an optional network feature requires sending data (such as querying an external API), the UI must explicitly show:
- Exactly what data will leave the machine.
- Where it is being sent.
- A mandatory confirmation dialog before transmission.

### 10. User Problem First (No "AI for AI's Sake")
AI is an implementation mechanism, never a product objective. Every feature proposal must justify itself by defining:
1. The concrete user problem.
2. The target audience.
3. The current manual workaround.
4. The quantifiable AXORA benefit.
5. The deployment footprint and storage impact.
6. The verification and testing strategy.

---

## 3. The Feature Acceptance Gate

Before any feature enters the AXORA implementation roadmap, it must successfully pass the **Six-Point Acceptance Gate**:

```
┌────────────────────────────────────────────────────────────────────────┐
│                   FEATURE ACCEPTANCE GATE CHECKLIST                    │
├────────────────────────────────────────────────────────────────────────┤
│  1. Problem Justification : What real friction does this resolve?      │
│  2. Capability Class      : Is it Core, Optional Local, or Network?   │
│  3. Storage & Footprint   : How many megabytes does it demand?         │
│  4. Privacy & Data Flow   : Does any user content leave the machine?   │
│  5. Graceful Degradation  : How does the UI behave when missing?       │
│  6. Objective Verification: Can it be deterministically tested?        │
└────────────────────────────────────────────────────────────────────────┘
```

Features that cannot demonstrate clear answers to all six criteria are deferred to exploration or rejected.
