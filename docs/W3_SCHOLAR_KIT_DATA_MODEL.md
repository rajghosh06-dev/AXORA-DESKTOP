# AXORA WinUI — Phase W3 Data Model Specification
## Scholar Kit: Domain Entities, Relationships & Persistence Contracts

**Document Version**: 1.0.0 (Phase W3 Data Model Baseline)  
**Target Framework**: .NET 9.0 · C# 13  
**Status**: **PLANNING / DATA-CONTRACT GATE**  

---

## 1. Entity-Relationship Overview

The Scholar Kit data model defines a deterministic, local-first schema representing ingested academic material, semantic passage indices, extracted knowledge artifacts, and persistent study sessions.

```
┌────────────────────────────────────────────────────────────────────────┐
│                              StudySession                              │
│  SessionId, Title, CreatedAt, LastAccessedAt, UserNotes, Summary       │
└───────┬──────────────────────────────────┬──────────────────────┬──────┘
        │ 1..*                             │ 0..*                 │ 0..*
        ▼                                  ▼                      ▼
┌──────────────────┐             ┌──────────────────┐   ┌──────────────────┐
│ ScholarDocument  │             │   StudyConcept   │   │ PracticeQuizItem │
│  DocumentId      │             │  ConceptId       │   │  QuestionId      │
│  SourcePath      │             │  Term            │   │  QuestionText    │
│  FileName        │             │  Definition      │   │  ExpectedAnswer  │
│  PageCount       │             │  Category        │   │  Difficulty      │
└───────┬──────────┘             └────────┬─────────┘   └────────┬─────────┘
        │ 1..*                            │                      │
        ▼                                 │                      │
┌──────────────────┐                      │                      │
│   DocumentPage   │                      │                      │
│  PageNumber      │                      │                      │
│  Dimensions      │                      │                      │
│  RawText         │                      │                      │
└───────┬──────────┘                      │                      │
        │ 1..*                            │                      │
        ▼                                 │                      │
┌──────────────────────┐                  │                      │
│ DocumentPassageChunk │                  │                      │
│  ChunkId             │                  │                      │
│  Text                │                  │                      │
│  Embedding float[384]│                  │                      │
│  CharSpan            │                  │                      │
└───────┬──────────────┘                  │                      │
        │                                 │                      │
        │ 1                               │ 1                    │ 1
        ▲                                 ▼                      ▼
        └─────────────────────────── StudyCitation ──────────────┘
                                     DocumentId, FileName, PageNumber,
                                     ChunkIndex, Snippet, SimilarityScore
```

---

## 2. Core Domain Entities

### 2.1. `ScholarDocument`
- **Purpose**: Represents an ingested source document (PDF, scanned image, or text file) within the study library.
- **Ownership**: Root entity in document storage; referenced by `StudySession`.
- **Lifecycle**: Created during ingestion; read-only once parsed; deleted when removed from library.
- **Persistence**: Persisted to `%APPDATA%\Axora\Scholar\documents\<DocumentId>.json`.

```csharp
public sealed record ScholarDocument
{
    public required string DocumentId { get; init; } = Guid.NewGuid().ToString("N");
    public required string SourcePath { get; init; }
    public required string FileName { get; init; }
    public required long FileSizeBytes { get; init; }
    public required int PageCount { get; init; }
    public required DocumentFormatType Format { get; init; }
    public required DateTime IngestedAt { get; init; } = DateTime.UtcNow;
    public string Title { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public IReadOnlyList<DocumentPage> Pages { get; init; } = [];
}

public enum DocumentFormatType
{
    Pdf,
    ImageOcr,
    PastedText,
    ScannerWia,
    VoiceDictation,
    Sample
}
```

### 2.2. `DocumentPage`
- **Purpose**: Represents a single page within a multi-page document, preserving physical coordinates and raw page content.
- **Ownership**: Owned by `ScholarDocument`.
- **Lifecycle**: Created during PDF extraction or image OCR; immutable.
- **Persistence**: Serialized as part of `ScholarDocument`.

```csharp
public sealed record DocumentPage
{
    public required int PageNumber { get; init; } // 1-indexed
    public double WidthPt { get; init; }
    public double HeightPt { get; init; }
    public required string RawText { get; init; }
    public IReadOnlyList<DocumentPassageChunk> Chunks { get; init; } = [];
}
```

### 2.3. `DocumentPassageChunk`
- **Purpose**: A semantic text segment used for dense vector indexing, similarity search, and citation grounding.
- **Size Specification**: Target length: 350 characters; stride overlap: 60 characters; preserves sentence boundaries.
- **Ownership**: Owned by `DocumentPage`.
- **Lifecycle**: Generated during document indexing; embeddings generated by `DirectMlEmbeddingService`.
- **Persistence**: Serialized with document or cached in `%TEMP%\Axora\Scholar\vectors\`.

```csharp
public sealed class DocumentPassageChunk
{
    public required int ChunkId { get; init; }
    public required string DocumentId { get; init; }
    public required int PageNumber { get; init; }
    public required int StartCharOffset { get; init; }
    public required int EndCharOffset { get; init; }
    public required string Text { get; init; }
    public float[] Embedding { get; set; } = [];
    public int CharLength => Text.Length;
}
```

### 2.4. `StudyCitation`
- **Purpose**: Implements end-to-end source provenance, connecting generated study artifacts to exact source locations.
- **Ownership**: Embedded inside `StudyConcept`, `PracticeQuizItem`, and `ScholarChatMessage`.
- **Lifecycle**: Instantiated when an artifact is synthesized or a RAG query is answered.
- **Persistence**: Persisted within its parent entity.

```csharp
public sealed record StudyCitation
{
    public required string DocumentId { get; init; }
    public required string FileName { get; init; }
    public required int PageNumber { get; init; } // 1-indexed
    public required int ChunkIndex { get; init; }
    public required string MatchedSnippet { get; init; }
    public double SimilarityScore { get; init; }

    public string FormattedBadge => $"{FileName} · p. {PageNumber}";
}
```

### 2.5. `StudyConcept`
- **Purpose**: Represents a key terminology definition, scientific law, formula, or core concept extracted from material.
- **Ownership**: Owned by `StudySession`.
- **Lifecycle**: Created by `IScholarSynthesisService` or manually by user; editable in UI.
- **Persistence**: Persisted within `StudySession`.

```csharp
public sealed partial class StudyConcept : ObservableObject
{
    public string ConceptId { get; init; } = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string _term = string.Empty;
    [ObservableProperty] private string _definition = string.Empty;
    [ObservableProperty] private string _category = "Core Concept";
    [ObservableProperty] private string _badgeColor = "#5B7DE8";
    public StudyCitation? Citation { get; init; }
}
```

### 2.6. `PracticeQuizItem`
- **Purpose**: A self-assessment question generated to test comprehension of key document findings.
- **Ownership**: Owned by `StudySession`.
- **Lifecycle**: Generated by `IScholarSynthesisService`; interactive visibility toggle in UI.
- **Persistence**: Persisted within `StudySession`.

```csharp
public sealed partial class PracticeQuizItem : ObservableObject
{
    public string QuestionId { get; init; } = Guid.NewGuid().ToString("N");
    public int QuestionNumber { get; init; }
    [ObservableProperty] private string _questionText = string.Empty;
    [ObservableProperty] private string _expectedAnswer = string.Empty;
    [ObservableProperty] private string _difficulty = "Medium"; // Easy, Medium, Hard
    [ObservableProperty] private bool _isAnswerRevealed;
    public StudyCitation? Citation { get; init; }
}
```

### 2.7. `ScholarChatMessage`
- **Purpose**: Represents a single conversation turn in the Offline Document RAG Assistant.
- **Ownership**: Owned by `StudySession`.
- **Lifecycle**: Created when user queries or assistant responds; transient or session-persisted.
- **Persistence**: Serialized within `StudySession` chat history.

```csharp
public sealed partial class ScholarChatMessage : ObservableObject
{
    public string MessageId { get; init; } = Guid.NewGuid().ToString("N");
    public bool IsUser { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public double Confidence { get; init; }
    public IReadOnlyList<StudyCitation> Citations { get; init; } = [];
    [ObservableProperty] private bool _isSpeaking;

    public bool HasCitations => Citations.Count > 0;
    public string FormattedConfidence => $"{Confidence * 100:F0}% Match";
    public string FormattedTime => Timestamp.ToLocalTime().ToString("t");
}
```

### 2.8. `StudySession`
- **Purpose**: Aggregate root representing a user's study workspace, linking documents, notes, extracted concepts, quizzes, and chat turns.
- **Ownership**: Managed by `IScholarLibraryService`.
- **Lifecycle**: Created when user opens or imports documents; persisted periodically and on page unload; deleted on user command.
- **Persistence**: Persisted to `%APPDATA%\Axora\Scholar\sessions\<SessionId>.json`.

```csharp
public sealed class StudySession
{
    public required string SessionId { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Untitled Study Session";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public List<string> DocumentIds { get; init; } = [];
    public string RawEditorText { get; set; } = string.Empty;
    public string ExecutiveSummary { get; set; } = string.Empty;
    public List<StudyConcept> Concepts { get; init; } = [];
    public List<PracticeQuizItem> QuizQuestions { get; init; } = [];
    public List<ScholarChatMessage> ChatHistory { get; init; } = [];
}
```

---

## 3. Flashcard Studio Integration Contract

Scholar Kit integrates directly with the existing `FlashcardDeck` and `FlashCard` domain models:
- When the user triggers `PushToFlashcardsCommand`, `IScholarSynthesisService` transforms `StudyConcept` and `PracticeQuizItem` objects into `FlashCard` instances:
  - `Front`: Concept `Term` or Quiz `QuestionText`.
  - `Back`: Concept `Definition` or Quiz `ExpectedAnswer`.
  - `Difficulty`: Mapped to `CardDifficulty.Easy` / `Medium` / `Hard`.
  - `EaseFactor`: Initialized to standard SM-2 default `2.5`.
  - `IntervalDays`: Initialized to `1`.
- The resulting `FlashcardDeck` is added to `FlashcardsViewModel.Decks` and persisted to `%APPDATA%\Axora\Flashcards\<DeckId>.json`.

---

## 4. JSON Serialization Sample

### Sample `StudySession` Schema:
```json
{
  "sessionId": "b78a9c4210e34fa6852bc00912345678",
  "title": "Quantum Tensor Networks Study",
  "createdAt": "2026-09-06T10:00:00Z",
  "lastAccessedAt": "2026-09-06T10:30:00Z",
  "documentIds": ["d4f1e09a32c44aa290123456789abcde"],
  "executiveSummary": "## Executive Study Summary\nMatrix Product State decomposition reduces convolutional model weight complexity by 78.4%...",
  "concepts": [
    {
      "conceptId": "c1a2b3c4d5",
      "term": "Matrix Product State (MPS)",
      "definition": "A tensor network decomposition that factorizes high-order tensors into a chain of rank-3 tensors.",
      "category": "Core Concept",
      "badgeColor": "#5B7DE8",
      "citation": {
        "documentId": "d4f1e09a32c44aa290123456789abcde",
        "fileName": "quantum_neural_computing_2026.pdf",
        "pageNumber": 1,
        "chunkIndex": 2,
        "matchedSnippet": "MPS tensor network decomposition reduces deep convolutional model weight complexity...",
        "similarityScore": 0.92
      }
    }
  ],
  "quizQuestions": [
    {
      "questionId": "q1a2b3c4d5",
      "questionNumber": 1,
      "questionText": "What accuracy degradation was observed after MPS tensor decomposition?",
      "expectedAnswer": "Less than 0.3% loss in top-1 accuracy on standard vision benchmarks.",
      "difficulty": "Easy",
      "isAnswerRevealed": false,
      "citation": {
        "documentId": "d4f1e09a32c44aa290123456789abcde",
        "fileName": "quantum_neural_computing_2026.pdf",
        "pageNumber": 1,
        "chunkIndex": 2,
        "matchedSnippet": "with less than 0.3% loss in top-1 accuracy on standard vision benchmarks",
        "similarityScore": 0.88
      }
    }
  ]
}
```
