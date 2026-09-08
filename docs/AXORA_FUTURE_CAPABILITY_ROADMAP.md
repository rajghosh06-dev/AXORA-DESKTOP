# AXORA Desktop — Master Capability Roadmap & Future Modules
## Product Roadmap, Engine Architecture & Emerging Capabilities

**Document Version**: 1.0.0 (W3-A.1 Architectural Baseline)  
**Target Platform**: Windows 11 (x64 / ARM64) · .NET 9.0 · Windows App SDK 1.6  
**Status**: **LONG-RANGE ARCHITECTURAL ROADMAP (PLANNING ONLY)**  

---

## 1. Master Capability Roadmap Overview

AXORA's long-range product trajectory is organized into eight coherent functional pillars. Each pillar addresses a fundamental desktop productivity need while adhering strictly to the local-first, modular, user-controlled philosophy.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          AXORA MASTER ROADMAP                               │
├─────────────────────────────────────────────────────────────────────────────┤
│  1. FOUNDATION (Phases W0, W1, W1.5)                                       │
│     Native lifecycle, theme propagation, safe shutdown, W1.5 extension      │
│     manager, error observability, notification service.                     │
├─────────────────────────────────────────────────────────────────────────────┤
│  2. TRANSFORM (Phase W2 — Universal Converter)                              │
│     Batch media & document conversion, lossy quality tuning, aspect-        │
│     preserving downscaling, metadata governance, real-time queue telemetry. │
├─────────────────────────────────────────────────────────────────────────────┤
│  3. UNDERSTAND (Phase W3 — Scholar Kit)                                     │
│     Academic document ingestion, local OCR, structural PDF parsing,         │
│     deterministic study synthesis, offline vector RAG, spaced repetition.   │
├─────────────────────────────────────────────────────────────────────────────┤
│  4. COMMUNICATE (Phase W4 — AXORA Voice Input)                              │
│     System-wide speech-to-text, voice dictation, text normalization,        │
│     filler-word cleanup, contextual punctuation, accessibility control.     │
├─────────────────────────────────────────────────────────────────────────────┤
│  5. CREATE (Phase W5 — AXORA Image Studio)                                  │
│     On-device image enhancement, super-resolution, background removal,      │
│     object masking, optional generative weights (Stable Diffusion / Flux).  │
├─────────────────────────────────────────────────────────────────────────────┤
│  6. VERIFY (Phase W6 — AXORA Integrity Center)                              │
│     Document similarity, academic text reuse, source code similarity (JPlag)│
│     ZIP student project comparison, citation verification signals.          │
├─────────────────────────────────────────────────────────────────────────────┤
│  7. PRESERVE (Phase W7 — System Tools & Continuity)                         │
│     Local environment snapshots, hardware driver backup, profile migration, │
│     non-destructive system state restoration.                               │
├─────────────────────────────────────────────────────────────────────────────┤
│  8. EXTEND (Phase W8 — Capability Ecosystem & Optional Cloud)               │
│     Capability manifest repository, user-managed model hub, optional        │
│     academic network plugins (arXiv, Crossref, user-configured cloud LLMs). │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Sequencing Rationale

Why does **UNDERSTAND (W3 Scholar Kit)** follow **TRANSFORM (W2 Universal Converter)** and precede **COMMUNICATE**, **CREATE**, and **VERIFY**?
1. **Core Ingestion Dependency**: Understanding documents (W3) reuses the W2 rasterization and format parsing foundations (PDF text extraction, image normalization).
2. **Text Foundation for Voice & Verification**: Semantic document structures and citation mechanisms established in W3 become direct prerequisites for voice dictation notes (W4) and academic integrity comparison corpora (W6).
3. **Storage & Hardware Prudence**: Voice Input and Image Studio introduce larger optional models and complex audio/vision pipelines. Perfecting the modular capability manager and lightweight synthesis in W3 ensures the platform can absorb heavier models safely.

---

## 3. Future Capability Deep Dives

### 3.1. Future Capability: AXORA Integrity Center (W6)
The **Integrity Center** provides local-first, privacy-preserving similarity analysis for students, educators, academic researchers, and software engineers.

#### Concrete User Problems Solved:
- **Educator Grading Overload**: Teachers and teaching assistants must cross-compare dozens of student essays or lab submissions to detect unauthorized collaboration without uploading student PII to proprietary third-party cloud services (violating FERPA / GDPR).
- **Source Code Plagiarism in Computer Science Courses**: Students submitting renamed variables or reordered functions. Traditional text diffs fail to catch syntax tree restructuring.
- **Academic Self-Plagiarism & Text Recycling**: Researchers need to ensure their new manuscript does not unintentionally duplicate verbatim text from their prior published works.

#### Functional Modules:
1. **Document & Academic Similarity**: Comparing documents against a local user-defined reference corpus using winnowing n-gram fingerprinting and semantic embeddings.
2. **Student Project / ZIP Batch Comparison**: Ingesting a folder of ZIP submissions, unpacking them in memory/sandbox, and performing pairwise similarity scoring across all submissions.
3. **Source Code Similarity Engine**: Parsing ASTs (Abstract Syntax Trees) to detect semantic code cloning despite renamed identifiers or reordered statements.
4. **Citation & Reference Analysis**: Cross-checking bibliography entries against in-text citation markers (`[1]`, `(Smith et al., 2024)`), flagging orphaned citations or missing references.
5. **Optional Online Verification**: Explicit, user-triggered lookup of un-cited sentences against open scholarly indices (Semantic Scholar, arXiv, Crossref) with mandatory outbound data preview.

#### Critical Invariant: Evidence, Never Automated Verdicts
> [!IMPORTANT]
> **A similarity score is NEVER proof of academic misconduct.**  
> Plagiarism detection software cannot assess intent, legitimate common phrases, boilerplate code, open-source libraries, or approved collaboration.  
> The AXORA Integrity Center will **never** label a document "plagiarized" or assign a "guilt score". It produces **side-by-side textual evidence, highlighted overlaps, and matching signals for human review and academic judgment**.

#### Technology & Licensing Evaluation:
- **JPlag (GPL-3.0)**: Renowned token-based code similarity detection engine supporting Java, C/C++, Python, and C#. If integrated as an optional external process, must be isolated to adhere to licensing boundaries or implemented via a clean-room C# token/tree-matching engine.
- **Local Fingerprinting (Winnowing Algorithm)**: Permissively licensed, highly efficient local algorithm for document shingles and sentence fingerprints (O(N) complexity, zero external dependencies).

---

### 3.2. Future Capability: AXORA Voice Input (W4)
Voice Input is designed as a **global, system-wide input capability** accessible across all AXORA modules (Scholar Kit notes, Converter rename patterns, Integrity annotations, and general text editing).

#### Architectural Pipeline:
```
[ Microphone Audio ] 
        │ 
        ▼
[ Windows Media Speech / Optional Local Whisper.onnx ] (Speech-to-Text)
        │ Raw recognized tokens
        ▼
[ Text Normalization Engine ] (Capitalization, punctuation, numbers: "twenty five" -> "25")
        │
        ▼
[ Optional Local SLM Post-Processor ] (Filler word removal: "um", "ah", "like", grammar polish)
        │
        ▼
[ Active AXORA Control / Focused Text Element ] (Injected into UI via DispatcherQueue)
```

#### Core Capabilities:
- **Continuous Dictation**: Real-time speech transcription with low CPU overhead.
- **Lecture / Meeting Transcription**: Batch processing of long-form audio recordings (`.wav`, `.mp3`, `.m4a`) into structured Markdown transcripts with timestamps.
- **Speech Hygiene**: Automatic suppression of disfluencies and conversational fillers.
- **Contextual Formatting**: Voice-command recognition (`"New paragraph"`, `"Heading 2"`, `"Bullet point"`).
- **Accessibility Dictation**: Hands-free navigation for users with motor impairments.

---

### 3.3. Future Capability: AXORA Image Studio (W5)
An on-device visual editing and transformation studio balancing precision raster tools with optional local neural models.

#### Core Capabilities:
- **Precision Image Transformation**: High-quality aspect-preserving cropping, canvas expansion, rotation, format transposition.
- **AI Upscaling / Super-Resolution**: Reconstructing crisp detail on low-resolution scans or photos using compact ONNX models (Real-ESRGAN / Windows App SDK `ImageScaler`).
- **Background Removal & Subject Masking**: Separating foreground subjects from backgrounds using ONNX segmentation models (RMBG / BiRefNet) or Windows App SDK `Segmenter`.
- **Optional Generative Synthesis (Text-to-Image / Image-to-Image)**:
  - High-end capability requiring dedicated modern GPU (DirectX 12 with 8+ GB VRAM).
  - Delivered strictly as an **Optional Local Capability** (weights downloaded on demand).
  - Never forced onto standard office/academic laptop installations.

---

## 4. Microsoft Windows AI & Hardware Opportunity Analysis

An evaluation of current official Microsoft Windows AI infrastructure was conducted to determine what is production-ready versus preview:

| Technology | Status (2026) | Execution Layer | Hardware Requirements | AXORA Architecture Position |
|---|:---:|---|---|---|
| **Windows Runtime OCR** (`Windows.Media.Ocr`) | **STABLE / PRODUCTION** | In-box OS WinRT API | Universal (Any Windows 10/11 x64 or ARM64 PC) | **Class D Core Component** in Scholar Kit. Zero bundle overhead; 100% offline. |
| **DirectML** (`Microsoft.AI.DirectML`) | **STABLE / PRODUCTION** | Low-level DirectX 12 API | Any DirectX 12 GPU (NVIDIA, AMD, Intel, Qualcomm) | **Primary Hardware AI Backbone** for all local model inferencing (embeddings, vision, SLMs). |
| **ONNX Runtime** (`Microsoft.ML.OnnxRuntime`) | **STABLE / PRODUCTION** | High-performance inference engine | CPU (AVX2/AVX-512) or GPU (DirectML) | **Primary Model Execution Layer**. Extremely stable, cross-architecture, zero cloud dependencies. |
| **Windows Media Speech** (`Windows.Media.SpeechRecognition`) | **STABLE / PRODUCTION** | In-box OS WinRT API | Universal | **Class D System Component** for basic speech recognition and Neural TTS read-aloud. |
| **Microsoft.Windows.AI.Imaging** (`TextRecognizer`, `ImageScaler`) | **PREVIEW / HARDWARE GATED** | Windows App SDK 1.6+ | Windows 11 24H2 + Copilot+ PC (40+ TOPS NPU) | **Optional Acceleration Path**. Must degrade gracefully to WinRT OCR / SkiaSharp on standard hardware. |
| **Microsoft Foundry on Windows** (`Foundry Local`) | **EMERGING / PREVIEW** | ONNX Runtime + Native Library | Windows 11 with 8+ GB RAM | **Candidate for W8 Model Manager**. Provides clean OpenAI-compatible local endpoints for open-source SLMs. |

### Architectural Conclusion on Windows AI:
AXORA will **not bind its core capabilities exclusively to Copilot+ PC / NPU-only APIs**. Doing so would alienate the vast majority of desktop users on Intel Core, AMD Ryzen, and discrete NVIDIA GPUs. 

Instead, AXORA utilizes **ONNX Runtime + DirectML as its universal baseline**, which guarantees hardware acceleration across any DirectX 12 GPU while retaining multi-threaded CPU fallbacks, with opportunistic NPU acceleration where present.

---

## 5. Library Strategy: Beyond the "Single Replacement" Fallacy

When evaluating alternatives to Android/iOS ML Kit for Windows desktop, searching for a single monolithic replacement library is an architectural anti-pattern. Windows desktop development succeeds by orchestrating a cohesive **Windows Capability Stack**:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       AXORA WINDOWS CAPABILITY STACK                        │
├─────────────────────────────────────────────────────────────────────────────┤
│  Level 1: Native Windows APIs (WinRT OcrEngine, Windows.Media.Speech)        │
│           - Zero installation footprint, instant startup, OS maintained.   │
├─────────────────────────────────────────────────────────────────────────────┤
│  Level 2: Universal Native Graphics (WIC + SkiaSharp)                       │
│           - Ultra-fast rasterization, codec decoding, aspect downscaling.   │
├─────────────────────────────────────────────────────────────────────────────┤
│  Level 3: Hardware AI Inference (ONNX Runtime + DirectML)                   │
│           - Open, multi-vendor GPU/NPU acceleration for custom models.      │
├─────────────────────────────────────────────────────────────────────────────┤
│  Level 4: Windows App SDK AI Innovations (Opportunistic NPU Super-Res)      │
│           - Activated conditionally when running on Copilot+ PC hardware.   │
├─────────────────────────────────────────────────────────────────────────────┤
│  Level 5: Specialized Open-Source Engines (PdfSharpCore, Winnowing, JPlag)  │
│           - Domain-specific logic encapsulated in clean service boundaries. │
└─────────────────────────────────────────────────────────────────────────────┘
```
