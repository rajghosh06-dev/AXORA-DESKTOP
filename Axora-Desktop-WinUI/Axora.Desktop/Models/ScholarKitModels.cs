using System;
using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axora.Desktop.Models;

/// <summary>
/// Status of passage vector embeddings.
/// Ensures documents can be persisted and indexed without requiring neural models.
/// </summary>
public enum PassageEmbeddingStatus
{
    NoEmbedding = 0,
    EmbeddingAvailable = 1,
    EmbeddingStale = 2
}

/// <summary>
/// Source format category of an ingested academic document.
/// </summary>
public enum DocumentFormatType
{
    Pdf = 0,
    ImageOcr = 1,
    PastedText = 2,
    ScannerWia = 3,
    VoiceDictation = 4,
    Sample = 5,
    Other = 6
}

/// <summary>
/// Lifecycle and accessibility state of a user's original source document file.
/// </summary>
public enum SourceAvailabilityStatus
{
    Available = 0,
    Missing = 1,
    Inaccessible = 2,
    None = 3
}

/// <summary>
/// Persistence schema versioning constants for Scholar Kit local storage.
/// </summary>
public static class ScholarPersistenceConstants
{
    public const int CurrentSchemaVersion = 1;
    public const int MinimumSupportedSchemaVersion = 1;
}

/// <summary>
/// Thrown when attempting to load a persisted document or session with an unsupported future schema version.
/// </summary>
public class UnsupportedSchemaVersionException : Exception
{
    public int AttemptedVersion { get; }
    public int SupportedVersion { get; }

    public UnsupportedSchemaVersionException(int attemptedVersion, int supportedVersion)
        : base($"Unsupported schema version {attemptedVersion}. Maximum supported version is {supportedVersion}.")
    {
        AttemptedVersion = attemptedVersion;
        SupportedVersion = supportedVersion;
    }
}

/// <summary>
/// Thrown or recorded when a persisted JSON file is malformed, truncated, or zero-byte empty.
/// </summary>
public class CorruptedPersistenceException : Exception
{
    public string FilePath { get; }

    public CorruptedPersistenceException(string filePath, string message, Exception? innerException = null)
        : base($"Corrupted persistence file '{filePath}': {message}", innerException)
    {
        FilePath = filePath;
    }
}

/// <summary>
/// Thrown when an identifier contains directory traversal sequences attempting to escape the Scholar storage root.
/// </summary>
public class ScholarPathTraversalException : ArgumentException
{
    public ScholarPathTraversalException(string message) : base(message) { }
}

/// <summary>
/// Represents a searchable passage chunk within an academic document page.
/// Supports deterministic offsets and optional vector embeddings.
/// </summary>
public sealed class DocumentPassageChunk
{
    public int ChunkId { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public int PageNumber { get; set; } = 1;
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public int StartCharOffset { get; set; }
    public int EndCharOffset { get; set; }
    public int StartOffset { get => StartCharOffset; set => StartCharOffset = value; }
    public int EndOffset { get => EndCharOffset; set => EndCharOffset = value; }
    public PassageEmbeddingStatus EmbeddingStatus { get; set; } = PassageEmbeddingStatus.NoEmbedding;
    public float[]? Embedding { get; set; }
    public int CharLength { get; set; }

    public int Length => CharLength > 0 ? CharLength : (Text?.Length ?? 0);
}

/// <summary>
/// Explicit page representation preserving page boundaries and coordinates.
/// </summary>
public sealed class DocumentPage
{
    public int PageNumber { get; set; } = 1;
    public double Width { get; set; }
    public double Height { get; set; }
    public string RawText { get; set; } = string.Empty;
    public PageSemanticsType PageSemantics { get; set; } = PageSemanticsType.PhysicalPage;
    public string? NormalizedText { get; set; }
    public List<DocumentPassageChunk> Chunks { get; set; } = [];
}

/// <summary>
/// Represents the user's logical academic source in the Scholar library.
/// SourcePath is stored purely as metadata; original user documents are never modified or deleted.
/// </summary>
public sealed class ScholarDocument
{
    public int SchemaVersion { get; set; } = ScholarPersistenceConstants.CurrentSchemaVersion;
    public string DocumentId { get; set; } = Guid.NewGuid().ToString("N");
    public string SourcePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DocumentFormatType Format { get; set; } = DocumentFormatType.Pdf;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastIndexedAt { get; set; }
    public int PageCount { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? SourceHash { get; set; }
    public List<DocumentPage> Pages { get; set; } = [];

    /// <summary>
    /// Evaluates whether the original source file still exists and is accessible.
    /// If missing or moved, the persisted record remains fully loadable.
    /// </summary>
    public SourceAvailabilityStatus CheckSourceAvailability()
    {
        if (string.IsNullOrWhiteSpace(SourcePath))
            return SourceAvailabilityStatus.None;
        try
        {
            if (!File.Exists(SourcePath))
                return SourceAvailabilityStatus.Missing;
            using var stream = File.OpenRead(SourcePath);
            return SourceAvailabilityStatus.Available;
        }
        catch (UnauthorizedAccessException)
        {
            return SourceAvailabilityStatus.Inaccessible;
        }
        catch (IOException)
        {
            return SourceAvailabilityStatus.Inaccessible;
        }
        catch
        {
            return SourceAvailabilityStatus.Missing;
        }
    }
}

/// <summary>
/// Connects generated study artifacts to exact source document locations (provenance).
/// </summary>
public sealed class StudyCitation
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int PageNumber { get; set; } = 1;
    public int ChunkIndex { get; set; }
    public string MatchedSnippet { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
    public SourceAvailabilityStatus SourceStatus { get; set; } = SourceAvailabilityStatus.None;

    public string FormattedBadge => SourceStatus == SourceAvailabilityStatus.Missing
        ? $"{FileName} (Source file moved) · p. {PageNumber}"
        : $"{FileName} · p. {PageNumber}";
}

/// <summary>
/// Durable domain model representing a key definition, scientific law, or concept.
/// </summary>
public sealed partial class StudyConcept : ObservableObject
{
    public string ConceptId { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _term = string.Empty;

    [ObservableProperty]
    private string _definition = string.Empty;

    [ObservableProperty]
    private string _category = "Core Concept";

    [ObservableProperty]
    private string _badgeColor = "#5B7DE8";

    public StudyCitation? Citation { get; set; }

    public ItemGroundingStatus GroundingStatus { get; set; } = ItemGroundingStatus.Grounded;

    public bool IsUserModified { get; set; }

    public DateTime? LastModifiedAt { get; set; }

    public static StudyConcept FromItem(StudyConceptItem item, StudyCitation? citation = null) => new()
    {
        ConceptId = !string.IsNullOrWhiteSpace(item.ConceptId) ? item.ConceptId : Guid.NewGuid().ToString("N"),
        Term = item.Term,
        Definition = item.Definition,
        Category = item.Category,
        BadgeColor = item.BadgeColor,
        Citation = citation ?? item.Citation,
        GroundingStatus = item.GroundingStatus,
        IsUserModified = item.IsUserModified,
        LastModifiedAt = item.LastModifiedAt
    };

    public StudyConceptItem ToItem() => new()
    {
        ConceptId = ConceptId,
        Term = Term,
        Definition = Definition,
        Category = Category,
        BadgeColor = BadgeColor,
        Citation = Citation,
        GroundingStatus = GroundingStatus,
        IsUserModified = IsUserModified,
        LastModifiedAt = LastModifiedAt
    };
}

/// <summary>
/// Transient / UI model representing an extracted key concept or definition.
/// Kept for UI template and binding compatibility. Retains ID and provenance across roundtrips.
/// </summary>
public sealed class StudyConceptItem
{
    public string ConceptId { get; set; } = string.Empty;
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string Category { get; set; } = "Core Concept";
    public string BadgeColor { get; set; } = "#5B7DE8";
    public StudyCitation? Citation { get; set; }
    public ItemGroundingStatus GroundingStatus { get; set; } = ItemGroundingStatus.Grounded;
    public bool IsUserModified { get; set; }
    public DateTime? LastModifiedAt { get; set; }
}

/// <summary>
/// Durable domain model representing an auto-generated study / practice question with answer and provenance.
/// </summary>
public sealed partial class PracticeQuizItem : ObservableObject
{
    public string QuestionId { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private int _questionNumber;

    [ObservableProperty]
    private string _questionText = string.Empty;

    [ObservableProperty]
    private string _expectedAnswer = string.Empty;

    [ObservableProperty]
    private string _difficulty = "Medium";

    [ObservableProperty]
    private bool _isAnswerRevealed;

    public StudyCitation? Citation { get; set; }

    public ItemGroundingStatus GroundingStatus { get; set; } = ItemGroundingStatus.Grounded;

    public bool IsUserModified { get; set; }

    public DateTime? LastModifiedAt { get; set; }

    // Compatibility aliases for UI binding & StudyQuestionItem parity
    public int Number { get => QuestionNumber; set => QuestionNumber = value; }
    public string Question { get => QuestionText; set => QuestionText = value; }
    public string Answer { get => ExpectedAnswer; set => ExpectedAnswer = value; }
    public bool IsAnswerVisible { get => IsAnswerRevealed; set => IsAnswerRevealed = value; }

    public static PracticeQuizItem FromItem(StudyQuestionItem item, StudyCitation? citation = null) => new()
    {
        QuestionId = !string.IsNullOrWhiteSpace(item.QuestionId) ? item.QuestionId : Guid.NewGuid().ToString("N"),
        QuestionNumber = item.Number,
        QuestionText = item.Question,
        ExpectedAnswer = item.Answer,
        Difficulty = item.Difficulty,
        IsAnswerRevealed = item.IsAnswerVisible,
        Citation = citation ?? item.Citation,
        GroundingStatus = item.GroundingStatus,
        IsUserModified = item.IsUserModified,
        LastModifiedAt = item.LastModifiedAt
    };

    public StudyQuestionItem ToItem() => new()
    {
        QuestionId = QuestionId,
        Number = QuestionNumber,
        Question = QuestionText,
        Answer = ExpectedAnswer,
        Difficulty = Difficulty,
        IsAnswerVisible = IsAnswerRevealed,
        Citation = Citation,
        GroundingStatus = GroundingStatus,
        IsUserModified = IsUserModified,
        LastModifiedAt = LastModifiedAt
    };
}

/// <summary>
/// UI model representing an auto-generated study question item.
/// Kept for UI template and binding compatibility. Retains ID and provenance across roundtrips.
/// </summary>
public sealed partial class StudyQuestionItem : ObservableObject
{
    public string QuestionId { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Medium";
    public StudyCitation? Citation { get; set; }
    public ItemGroundingStatus GroundingStatus { get; set; } = ItemGroundingStatus.Grounded;
    public bool IsUserModified { get; set; }
    public DateTime? LastModifiedAt { get; set; }

    [ObservableProperty]
    private bool _isAnswerVisible;
}

/// <summary>
/// Model representing a chat turn in the Offline Document RAG Assistant stream.
/// </summary>
public sealed partial class ScholarChatMessage : ObservableObject
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsUser { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public double Confidence { get; set; }
    public IReadOnlyList<string> CitedPassages { get; set; } = [];
    public IReadOnlyList<StudyCitation> Citations { get; set; } = [];

    [ObservableProperty]
    private bool _isSpeaking;

    public bool HasCitations => (Citations != null && Citations.Count > 0) || (CitedPassages != null && CitedPassages.Count > 0);
    public string FormattedConfidence => $"{Confidence * 100:F0}% Match";
    public string FormattedTime => Timestamp.ToString("t");
}

/// <summary>
/// Durable aggregate root representing a user's study workspace session.
/// Clearly separates source data references (DocumentIds) from derived study artifacts
/// (ExecutiveSummary, Concepts, QuizQuestions, ChatHistory).
/// </summary>
public sealed class StudySession
{
    public int SchemaVersion { get; set; } = ScholarPersistenceConstants.CurrentSchemaVersion;
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Untitled Study Session";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public List<string> DocumentIds { get; set; } = [];
    public string RawEditorText { get; set; } = string.Empty;
    public string ExecutiveSummary { get; set; } = string.Empty;
    public List<StudyConcept> Concepts { get; set; } = [];
    public List<PracticeQuizItem> QuizQuestions { get; set; } = [];
    public List<ScholarChatMessage> ChatHistory { get; set; } = [];
}

/// <summary>
/// Represents a bounded, semantically coherent, and traceable context window
/// assembled from one or more DocumentPassageChunk instances.
/// Used by downstream consumers (embeddings, RAG synthesis, chat assistant).
/// </summary>
public sealed class BoundedContextWindow
{
    /// <summary>
    /// Deterministic identifier for this context window formulation (e.g. win_doc1_p1_c0_2 or comp_{hash64}).
    /// </summary>
    public string WindowId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the parent document (e.g. doc_1, or "composite" for multi-document prompt windows).
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// 1-based page number where the focal chunk resides. 0 for multi-page composite windows.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// The primary focal passage that anchored this window (null for unanchored/composite windows).
    /// </summary>
    public DocumentPassageChunk? FocalChunk { get; set; }

    /// <summary>
    /// All constituent chunks assembled within this bounded context window, ordered by ChunkIndex.
    /// </summary>
    public IReadOnlyList<DocumentPassageChunk> ConstituentChunks { get; set; } = [];

    /// <summary>
    /// Ordered integer indices of all constituent chunks.
    /// Informational only for UI display; not a globally unique provenance key.
    /// </summary>
    public IReadOnlyList<int> ConstituentChunkIndices { get; set; } = [];

    /// <summary>
    /// Formatted, deduplicated textual content ready for embedding or prompt ingestion.
    /// </summary>
    public string FormattedText { get; set; } = string.Empty;

    /// <summary>
    /// Starting character offset in page.NormalizedText (inclusive, 0-based).
    /// -1 for Mode B (fallback) or Mode C (composite).
    /// </summary>
    public int StartCharOffset { get; set; } = -1;

    /// <summary>
    /// Ending character offset in page.NormalizedText (exclusive, 0-based).
    /// -1 for Mode B (fallback) or Mode C (composite).
    /// </summary>
    public int EndCharOffset { get; set; } = -1;

    /// <summary>
    /// Character length of the formatted window text.
    /// </summary>
    public int CharLength => FormattedText?.Length ?? 0;

    /// <summary>
    /// Heuristic token estimate based on standard heuristic (ceil(chars / 4)).
    /// Coarse planning metric for prompt budgeting; not guaranteed exact tokenizer output.
    /// </summary>
    public int EstimatedTokens => (int)Math.Ceiling(CharLength / 4.0);

    /// <summary>
    /// Structured citations for every passage included in this window.
    /// </summary>
    public IReadOnlyList<StudyCitation> Citations { get; set; } = [];

    /// <summary>
    /// True if the window was truncated due to hard MaxWindowChars budget constraints.
    /// </summary>
    public bool IsTruncated { get; set; }
}
