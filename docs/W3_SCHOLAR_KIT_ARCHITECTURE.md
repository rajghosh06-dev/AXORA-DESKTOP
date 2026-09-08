# AXORA WinUI — Phase W3 Architecture Specification (Revised)
## Scholar Kit: Modular Layering, Shared Subsystem Boundaries & Graceful Degradation

**Document Version**: 2.0.0 (W3-A.1 Architectural Baseline)  
**Target Framework**: .NET 9.0 (`net9.0-windows10.0.26100.0`) · C# 13 · Windows App SDK 1.6  
**Status**: **PLANNING ARCHITECTURE CONTRACT (ZERO IMPLEMENTATION COMMENCED)**  

---

## 1. System Topology & Modular Service Boundaries

Scholar Kit's architecture is structured to enforce a clean separation between **Core Local Services**, **Optional Capability Services**, and **Shared AXORA Subsystems**:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            PRESENTATION LAYER                               │
│  ScholarKitPage.xaml ── Top Toolbar / Left Ingestion / Right 4-Tab Studio   │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ Data Binding & RelayCommands
┌──────────────────────────────────────▼──────────────────────────────────────┐
│                            APPLICATION LAYER                                │
│  ScholarKitViewModel (State Orchestration / DispatcherQueue Marshalling)    │
└───────────────────┬─────────────────────────────────────┬───────────────────┘
                    │                                     │
┌───────────────────▼──────────────────┐   ┌──────────────▼───────────────────┐
│             DOMAIN LAYER             │   │       PERSISTENCE LAYER          │
│  ScholarDocument, DocumentPage,      │   │  IScholarLibraryService          │
│  StudyConcept, PracticeQuizItem,     │   │  Atomic Json Storage (%APPDATA%) │
│  StudyCitation, StudySession         │   │  Vector Cache (%TEMP%\Axora\)    │
└───────────────────┬──────────────────┘   └──────────────────────────────────┘
                    │
┌───────────────────▼─────────────────────────────────────────────────────────┐
│                               SERVICE LAYER                                 │
│  ┌─────────────────────────────────┐ ┌───────────────────────────────────┐  │
│  │ CORE LOCAL SERVICES (Bundled)   │ │ OPTIONAL SERVICES (User Download) │  │
│  │ - PdfExtractionService          │ │ - DirectMlEmbeddingService        │  │
│  │ - WinRtOcrService (System)      │ │   (all-MiniLM-L6-v2 ONNX Model)   │  │
│  │ - ScholarSynthesisService       │ │ - Optional Local SLM Engine       │  │
│  │ - ScholarLibraryService         │ │   (Phi-3-mini DirectML)           │  │
│  │ - LexicalSearchEngine (Fallback)│ └─────────────────┬─────────────────┘  │
│  └─────────────────────────────────┘                   │                    │
│  ┌─────────────────────────────────────────────────────┴─────────────────┐  │
│  │ SHARED AXORA SUBSYSTEMS (Global Architecture)                         │  │
│  │ - AXORA Voice Input Service (Global dictation & speech recognition)   │  │
│  │ - SpeechSynthesisService (Windows Neural TTS read-aloud)              │  │
│  │ - FlashcardsViewModel Bridge (Spaced Repetition SM-2 Deck Generation) │  │
│  │ - Capability Manager (Model lifecycle, verification, storage meter)   │  │
│  │ - PdfSurgeonService (Shared PDF page extraction, rotation, reorder)   │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Six-Layer Architectural Breakdown

### 2.1. Presentation Layer (View)
- **Primary View**: [`ScholarKitPage.xaml`](file:///d:/RAJ/GITHUB_REPOSITORY/PROJECTS/AXORA-DESKTOP/Axora-Desktop-WinUI/Axora.Desktop/Views/ScholarKitPage.xaml).
- **Layout Architecture**:
  - Top Action Bar: Status banner, Voice dictation toggle, Read-aloud playback, Push to Flashcards, Multi-format Export flyout.
  - Left Ingestion Panel (Fixed 340px): Drag-and-drop drop zone, native file picker, flatbed scanner trigger, clipboard paste, sample paper loader, OCR engine options, and speech rate/pitch sliders.
  - Right Multi-View Studio (Grid width `*`): 4 specialized studio views:
    - **Tab 0: Extracted Editor** (Text hygiene, line wrap reflow, whitespace cleaner, case conversion).
    - **Tab 1: Markdown & Structured Schema** (Formatted document preview, JSON schema inspector).
    - **Tab 2: AI Study Synthesizer** (Executive summary, colored concept tags, interactive practice quiz).
    - **Tab 3: Offline RAG Assistant** (Conversational search, confidence badge, source passage citations, read-aloud).

### 2.2. Application Layer (ViewModel)
- **Primary ViewModel**: [`ScholarKitViewModel`](file:///d:/RAJ/GITHUB_REPOSITORY/PROJECTS/AXORA-DESKTOP/Axora-Desktop-WinUI/Axora.Desktop/ViewModels/ScholarKitViewModel.cs).
- **Thread Marshalling**: All background tasks (OCR, PDF decoding, embedding indexing, synthesis) execute on the thread pool via `Task.Run` and marshal updates to the UI thread strictly through `DispatcherQueue.TryEnqueue()`.
- **Cancellation Governance**: Long operations accept a `CancellationToken` and exit cleanly without leaving orphaned background threads.

### 2.3. Domain Model Layer
- Pure C# records and classes defined in [`docs/W3_SCHOLAR_KIT_DATA_MODEL.md`](file:///d:/RAJ/GITHUB_REPOSITORY/PROJECTS/AXORA-DESKTOP/docs/W3_SCHOLAR_KIT_DATA_MODEL.md):
  - `ScholarDocument`, `DocumentPage`, `DocumentPassageChunk`, `StudyConcept`, `PracticeQuizItem`, `StudyCitation`, `StudySession`.

### 2.4. Service Layer (Decoupled Contracts)
1. **Core Local Services**:
   - `IPdfExtractionService`: Extracts text, font CMaps, and page dimensions using `PdfSharpCore`.
   - `IOcrService`: Native Windows 11 OCR using `Windows.Media.Ocr.OcrEngine`.
   - `IScholarSynthesisService`: Deterministic extractive synthesis for summaries, concept terms, and practice questions.
   - `IScholarLibraryService`: Manages local study session persistence in `%APPDATA%\Axora\Scholar\`.
2. **Optional Capability Services**:
   - `IWindowsAiService`: Implemented by `DirectMlEmbeddingService` for dense vector generation.
   - `IDocumentChatService`: Orchestrates RAG search with graceful fallback.
3. **Shared AXORA Subsystems**:
   - `IVoiceInputService`: Consumed from global AXORA Voice Input subsystem (W4).
   - `ISpeechSynthesisService`: Windows Neural TTS read-aloud.
   - `IPdfSurgeonService`: Shared document manipulation utility.

### 2.5. Persistence Layer
- **Location**: `%APPDATA%\Axora\Scholar\`.
- **Atomic File Operations**: Prevents file corruption during unexpected power loss:
  ```csharp
  string tempPath = targetPath + ".tmp";
  await File.WriteAllTextAsync(tempPath, jsonContent, Encoding.UTF8);
  File.Move(tempPath, targetPath, overwrite: true);
  ```

---

## 3. Graceful Degradation & AI Fallback Architecture

To ensure Scholar Kit is never blocked by missing neural models, the Document Chat Assistant implements a **Two-Tier Search Strategy**:

```
[ User Query ]
      │
      ▼
Is DirectML Embedding Model Installed?
      │
      ├── YES ──> [ Tier 1: Dense Vector RAG ]
      │           Generate 384-d query embedding via DirectML (GPU/NPU).
      │           Rank 350-char sliding-window chunks with SimdVectorHelper (AVX2/NEON).
      │           Return top matches with confidence percentage.
      │
      └── NO  ──> [ Tier 2: Deterministic Lexical Search (Fallback) ]
                  Tokenize query into stem/keyword tokens.
                  Rank passages using BM25 / TF-IDF frequency scoring.
                  Display badge: "Lexical Search Active (Embedding Model Available in Manager)".
                  Return top matches with zero external download required.
```

---

## 4. Shared Subsystems vs. Local Scholar Ownership

| Capability | Initial Prototype Ownership | Revised Architectural Boundary | Justification |
|---|---|---|---|
| **Voice Dictation** | Embedded in `ScholarKitViewModel` | **Global AXORA Voice Input (W4)** | Voice dictation is needed across the entire app (notes, rename patterns, search). Scholar Kit should consume `IVoiceInputService`. |
| **PDF Surgery** | Proposed for Scholar Kit | **Shared Document Utility (W3-H)** | Page reordering, rotation, and extraction are equally useful in Universal Converter and Scholar Kit. |
| **Model Download** | Custom download logic | **AXORA Capability Manager (W1.5)** | Reuses established cryptographic verification, staging, cache management, and progress reporting. |
| **Flashcards** | Transient UI list | **Global FlashcardsView (SM-2)** | Persists generated decks to `%APPDATA%\Axora\Flashcards\` so study progress survives across sessions. |
