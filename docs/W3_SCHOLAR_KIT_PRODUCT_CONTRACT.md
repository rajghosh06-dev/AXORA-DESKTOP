# AXORA WinUI — Phase W3 Product Contract (Revised)
## Scholar Kit: Academic Document Understanding, Research Studio & Study Retention

**Document Version**: 2.0.0 (W3-A.1 Architectural Baseline)  
**Target Platform**: Windows 11 (x64 / ARM64) · .NET 9.0 (`net9.0-windows10.0.26100.0`) · Windows App SDK 1.6  
**Status**: **PLANNING CONTRACT (ZERO IMPLEMENTATION COMMENCED)**  
**Engineering Baseline**: Protected HEAD `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4` + uncommitted W2-F1..W2-F5 working tree  

---

## 1. Product Purpose & Philosophy Re-Anchor

Scholar Kit is an on-device academic study and research workstation within AXORA Desktop. 

Under the revised AXORA product philosophy, **"AI" is never the product objective**. AI is merely an implementation mechanism that is applied selectively where it solves a concrete user problem better than deterministic code. 

Scholar Kit is designed around **Five Essential User Outcomes**:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       FIVE CORE USER OUTCOMES                               │
├─────────────────────────────────────────────────────────────────────────────┤
│  1. UNDERSTAND                                                              │
│     Quickly digest dense academic material, extract readable text from      │
│     unsearchable scans or multi-column PDFs, and grasp core theses.         │
├─────────────────────────────────────────────────────────────────────────────┤
│  2. EXPLORE                                                                 │
│     Navigate complex papers, discover key definitions and formulas, and     │
│     query specific empirical data points with conversational precision.     │
├─────────────────────────────────────────────────────────────────────────────┤
│  3. LEARN                                                                   │
│     Synthesize structured study artifacts: categorized concept glossaries,  │
│     executive summaries, and active-recall practice quizzes.                │
├─────────────────────────────────────────────────────────────────────────────┤
│  4. REMEMBER                                                                │
│     Transform synthesized knowledge into persistent Spaced Repetition       │
│     (SM-2) flashcard decks with zero manual copy-paste friction.            │
├─────────────────────────────────────────────────────────────────────────────┤
│  5. KEEP                                                                    │
│     Maintain an organized local study library with end-to-end source-to-    │
│     study provenance, exporting notes to Markdown, PDF, JSON, or Anki.      │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Ownership & Capability Boundaries

To prevent architectural bloat and duplicate code, features are strictly partitioned into four ownership tiers:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      SCHOLAR KIT CAPABILITY TAXONOMY                        │
├─────────────────────────────────────────────────────────────────────────────┤
│  1. CORE SCHOLAR KIT (Bundled / Out-of-the-Box)                             │
│     - Multi-modal document intake (PDF, images, clipboard, WIA scan).       │
│     - PDF text & structural extraction (PdfSharpCore + font CMap decoding). │
│     - Windows native OCR integration (WinRT OcrEngine).                     │
│     - Monospaced document editor with text hygiene & formatting tools.      │
│     - Deterministic study synthesis (Executive Summary & Key Takeaways).     │
│     - Rule-based terminology & concept extraction with color badge tags.    │
│     - Practice quiz generation with interactive revealable answers.         │
│     - Local study session persistence (%APPDATA%\Axora\Scholar\).           │
│     - Deterministic source-to-study citation tracking (Doc, Page, Offset).  │
│     - Multi-format export (Markdown, TXT, JSON, PDF).                       │
├─────────────────────────────────────────────────────────────────────────────┤
│  2. OPTIONAL SCHOLAR CAPABILITIES (User-Controlled Local Downloads)         │
│     - Dense vector semantic search & RAG (all-MiniLM-L6-v2 ONNX model).     │
│     - Local SLM question generator (Phi-3-mini-4k via DirectML).            │
│     - Additional Windows OCR offline language packs.                        │
├─────────────────────────────────────────────────────────────────────────────┤
│  3. SHARED AXORA CAPABILITIES (Consumed from Global Subsystems)             │
│     - AXORA Voice Input: Global speech dictation engine (W4).               │
│     - Windows Speech Synthesis: Windows Neural TTS read-aloud.              │
│     - PDF Surgery Suite: Shared page extraction, rotation, and reordering.  │
│     - Flashcard Studio: Global spaced repetition engine (FlashcardsView).   │
│     - Capability Manager: Lifecycle manager for models and language packs.  │
├─────────────────────────────────────────────────────────────────────────────┤
│  4. FUTURE CAPABILITIES (Post-W3 Roadmap)                                   │
│     - AXORA Integrity Center: Academic similarity & citation analysis (W6). │
│     - Optional online citation verification (Crossref/arXiv API lookup).    │
│     - True vector PDF text redaction.                                       │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Detailed User Outcomes & Feature Specifications

### 3.1. Outcome 1: UNDERSTAND
- **Problem**: Academic papers and scanned reading materials often arrive as unsearchable scans, camera snapshots, or complex multi-column PDFs that are painful to read and annotate.
- **Solution**:
  - Ingest PDFs natively via `PdfExtractionService`, extracting font CMaps, composite font glyphs, and kerning arrays.
  - Ingest camera photos and scanned pages via `WinRtOcrService`, running on-device Windows 11 hardware OCR.
  - Provide an inline text hygiene toolkit: `Format Paragraphs` (reflowing hard line wraps from PDF column breaks), `Clean Whitespace` (collapsing redundant spaces/blank lines), and case conversion (`Title Case`, `UPPER`, `lower`).

### 3.2. Outcome 2: EXPLORE
- **Problem**: Locating specific experimental benchmarks, equations, or methodologies in a 40-page journal article requires tedious manual skimming.
- **Solution**:
  - Offline RAG Assistant allowing users to converse directly with their document.
  - **Graceful Degradation Invariant**:
    - If the DirectML embedding model (`all-MiniLM-L6-v2`) is installed: Queries match against 350-character sliding-window passages using SIMD-accelerated dense vector cosine similarity.
    - If the embedding model is not installed: The assistant seamlessly degrades to a deterministic, zero-download BM25/TF-IDF lexical frequency search, informing the user via an unobtrusive badge.
  - Every answer includes direct source passage citations.

### 3.3. Outcome 3: LEARN
- **Problem**: Active learning requires extracting key terminology and self-testing, but manually creating study sheets is time-consuming.
- **Solution**:
  - **Deterministic Study Synthesis**: Generates an executive summary highlighting the document's core thesis and top findings.
  - **Concept Glossary**: Identifies specialized terms and paired definitions, classifying them into colored visual categories ("Core Concept", "Definition", "Formula").
  - **Self-Assessment Quiz**: Formulates practice questions testing comprehension of key assertions, with expandable/revealable answer drawers.

### 3.4. Outcome 4: REMEMBER
- **Problem**: Concepts learned during reading fade quickly without spaced repetition, but importing study notes into external flashcard apps requires tedious copy-pasting.
- **Solution**:
  - 1-Click `Push to Flashcards` bridge.
  - Extracted concepts and quiz questions are automatically mapped into `FlashCard` records (Front = Term/Question, Back = Definition/Answer) with default SM-2 ease factors (2.5).
  - The deck is saved to `%APPDATA%\Axora\Flashcards\` and opened in Flashcard Studio.

### 3.5. Outcome 5: KEEP
- **Problem**: Transient study notes are lost when tabs close; researchers lose the link between a study note and the exact page of the original document.
- **Solution**:
  - **Persistent Study Sessions**: Workspaces are saved to `%APPDATA%\Axora\Scholar\sessions\<SessionId>.json` using atomic write-then-rename file operations.
  - **Source Provenance**: Every concept and quiz question stores `DocumentId`, `FileName`, and `PageNumber`. Clicking a citation badge (`[paper.pdf · p. 4]`) highlights the source text.
  - **Universal Export**: Export study sessions to Markdown (`.md`), formatted PDF (`.pdf`), plain text (`.txt`), structured JSON (`.json`), or Anki Deck (`.txt`).

---

## 4. Privacy, Security & Data Governance

1. **Local-First Baseline**: Core Scholar Kit operates 100% locally with zero network activity.
2. **Source Immutability**: Source documents are opened strictly in read-only mode (`FileAccess.Read` / `FileShare.Read`).
3. **Transparent Network Boundaries**: If future plugins or citation verifiers access the network, they must be explicitly triggered by the user with a mandatory preview of the outbound query.
4. **Log Sanitization**: Logs record operational performance (timings, character counts, error codes), but NEVER raw document content, research text, or user chat queries.

---

## 5. Non-Goals

- **No Mandatory Cloud Dependencies**: Scholar Kit will never require a login, subscription, or cloud API key for core features.
- **No Large Model Installer Bloat**: The base AXORA installer will not include multi-gigabyte neural network weights.
- **No Plagiarism / Misconduct Scoring**: Integrity analysis is reserved for Phase W6 as a human-review evidence tool, not an automated judgment engine.
- **No In-Place PDF Modification**: PDF files are never rewritten in-place.
