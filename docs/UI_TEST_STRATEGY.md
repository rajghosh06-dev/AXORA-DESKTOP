# AXORA Desktop — UI Testing Strategy & Quality Gate

This document establishes the practical testing strategy, automation boundaries, and regression verification protocols across both desktop implementations: `Axora-Desktop-WinUI` and `Axora-Desktop-MaterialUI`.

---

## 1. Automation Boundaries & Feasibility

| Verification Layer | WinUI Implementation | MaterialUI Implementation | Automation Level | Current Tooling / Mechanism |
|---|---|---|---|---|
| **Layer 1: Static & Compilation** | .NET 9 C# / XAML | TypeScript / Vite / Rust | **100% Automated** | `MSBuild.exe`, `dotnet build`, `tsc`, `cargo check` |
| **Layer 2: Logic & Adversarial Stress** | 59 automated test assertions | Rust Tokio unit tests | **100% Automated** | `Axora.Desktop.Tests.exe`, `cargo test` |
| **Layer 3: Runtime Process Smoke** | Process launch, PID, `startup.log` | Process launch, WebView2 PID | **100% Automated** | `smoke-test.ps1 -Target WinUI / MaterialUI` |
| **Layer 4: Interaction & Navigation** | WinUI 3 Navigation, hotkeys | React Router, hotkeys, toasts | **Semi-Automated** | UIA3 / FlaUI (WinUI roadmap) / Chrome DevTools MCP |
| **Layer 5: Visual Layout & Hierarchy** | XAML rendering, spacing, Mica | MD3 token surfaces, Tailwind | **Visual Protocol** | Manual inspection against 25-Point Checklist / Screenshots |
| **Layer 6: Accessibility (a11y)** | Windows Narrator, Tab focus | ARIA roles, Tab focus | **Standards Aligned** | Keyboard navigation audit, contrast ratio calculation |

---

## 2. Six-Question Boundary Matrix

### 1. What can be automatically tested?
- **Zero-Error Compilation**: Compiler exit codes for C#, TypeScript, and Rust.
- **Model & Service Logic**: Vector PDF byte generation, markdown token sanitization, 75 font/margin layouts, SM-2 retention curves, DateTimeOffset overflow boundaries, batch image sizing and 0-byte error handling.
- **Runtime Startup Lifecycle**: Process spawn, PID verification, ComWrappers initialization, DI AppHost container construction, `MainWindow` instantiation, and clean process termination.
- **Security Invariants**: Automated regex scanning for credentials, tokens, and private keys.
- **Repository Hygiene**: Automated `.gitignore` enforcement and Git tracking verification.

### 2. What requires runtime testing?
- **Window Activation & Titlebar Integration**: Verifying that Windows App SDK successfully enables Mica Alt backdrop (`MicaKind.BaseAlt`) and custom non-client drag regions without titlebar overlap.
- **Hardware COM Interop**: WIA 2.0 flatbed scanner detection and hardware capability negotiation.
- **Local Network Sockets**: P2P mobile sync TCP socket binding, mDNS advertisements, and QR code generation.
- **DirectML Acceleration**: DirectML ONNX GPU execution fallback when compatible NPU/GPU drivers are present.

### 3. What requires visual inspection?
- **Layout Grid Alignment**: Visual confirmation that card columns, icon margins, and typography align cleanly to the 4px/8px layout grid.
- **Visual State Completeness**: Observing tactile feedback on hover, pointer press, and keyboard focus across custom `ControlTemplate` styles.
- **Text Clipping at Minimum Window Bounds**: Observing view rendering at minimum window dimensions (1000x620 DIP for WinUI, 960x600 px for MaterialUI) to confirm zero text truncation or overlapping panels.
- **Theme Transitions**: Verifying high-contrast readability when toggling between Dark and Light themes.

### 4. What requires human verification?
- **Physical Mobile Pairing**: Scanning generated QR codes with an actual Android phone running the Axora Companion app.
- **Physical Scanner Feeder**: Running multi-page batch scanning on physical WIA-compliant scanner hardware.
- **Speech Synthesis Fidelity**: Listening to Windows Speech TTS pronunciation of flashcard notes.

---

## 3. Decision & Classification Standards

Every test and QA assertion in AXORA Desktop must be classified into one of four unambiguous states:

```
┌───────────┐
│   PASS    │ -> Execution succeeded with empirical proof (exit code 0, assertions valid, log verified).
├───────────┤
│   FAIL    │ -> Execution failed or defect observed (record exact error code, log snippet, or visual fault).
├───────────┤
│  BLOCKED  │ -> Execution could not proceed due to missing host toolchain, physical hardware, or dependency.
├───────────┤
│   MANUAL  │ -> Requires human visual or physical inspection according to the 25-Point Checklist.
└───────────┘
```

> [!WARNING]
> **Strict Anti-Fabrication Rule**: Under no circumstances may an agent mark a check as `PASS` based solely on code reading, theoretical correctness, or past documentation. If a tool is missing, mark `BLOCKED`. If visual inspection was not performed, mark `MANUAL VERIFICATION REQUIRED`.

---

## 4. The Vibe Coding Quality Gate

To stop the cycle of *"Agent says done -> User finds broken UI -> Agent fixes -> Repeat"*, every UI-affecting change must traverse the 9-stage **Vibe Coding Quality Gate**:

```
[1. IMPLEMENT] ──► [2. STATIC CHECK] ──► [3. COMPILE] ──► [4. LOGIC TEST]
                                                                │
┌───────────────────────────────────────────────────────────────┘
▼
[5. RUNTIME SMOKE] ──► [6. INTERACTION] ──► [7. VISUAL AUDIT]
                                                   │
┌──────────────────────────────────────────────────┘
▼
[8. REGRESSION] ────► [9. EVIDENCE REPORT] ──► [TASK DECLARED COMPLETE]
```

### Stage Gates:
1. **IMPLEMENT**: Author minimal, targeted code modifications.
2. **STATIC CHECK**: Lint syntax and ensure theme tokens are used instead of hardcoded hex colors.
3. **COMPILE**: Rebuild target application (`.\scripts\qa\build-all.ps1`). Exit code must be 0.
4. **LOGIC TEST**: Execute automated test harness (`.\scripts\qa\run-tests.ps1`). 0 failures.
5. **RUNTIME SMOKE**: Launch application (`.\scripts\qa\smoke-test.ps1`). Verify PID alive and inspect `startup.log`.
6. **INTERACTION**: Verify target buttons, routes, inputs, and accelerators respond without exceptions.
7. **VISUAL AUDIT**: Check layout against the 25-Point Checklist (margins, typography, no clipping at minimum window bounds).
8. **REGRESSION**: Run full automated test suite to guarantee adjacent features remain unbroken.
9. **EVIDENCE REPORT**: Record exact execution logs and output in the final summary.
