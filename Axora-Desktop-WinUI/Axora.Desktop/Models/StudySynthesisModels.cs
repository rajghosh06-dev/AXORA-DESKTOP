using System;
using System.Collections.Generic;

namespace Axora.Desktop.Models;

/// <summary>
/// Supported study synthesis workflows.
/// </summary>
public enum SynthesisWorkflowKind
{
    ExecutiveSummary = 0,
    ConceptExtraction = 1,
    PracticeQuiz = 2,
    CrossDocumentComparison = 3,
    Comprehensive = 4
}

/// <summary>
/// Strategy for persisting generated study artifacts into a local study session.
/// </summary>
public enum SynthesisPersistenceMode
{
    /// <summary>
    /// Replaces unmodified generated items; preserves user-edited items unless explicitly overridden.
    /// </summary>
    ReplaceAll = 0,

    /// <summary>
    /// Preserves all existing items; appends deduplicated novel items.
    /// </summary>
    AppendNew = 1,

    /// <summary>
    /// In-memory generation only; does not mutate persisted session on disk.
    /// </summary>
    PreviewOnly_DoNotSave = 2
}

/// <summary>
/// Preferred synthesis engine tier.
/// </summary>
public enum SynthesisEnginePreference
{
    /// <summary>
    /// Uses Class B if installed and hardware capable, otherwise falls back to Class A.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Forces deterministic Class A Extractive Heuristic synthesizer.
    /// </summary>
    ForceClassA = 1,

    /// <summary>
    /// Requests Class B SLM; falls back to Class A on missing weights or failure.
    /// </summary>
    PreferClassB = 2
}

/// <summary>
/// Explicit verification state of an individual synthesized statement, concept, or question.
/// </summary>
public enum ItemGroundingStatus
{
    /// <summary>
    /// Passed all verification layers (valid citation, lemma overlap >= 60%, entity/numeric consistency, polarity).
    /// </summary>
    Grounded = 0,

    /// <summary>
    /// Conceptually supported by evidence, but contains minor phrasing extrapolation; flagged with visual notice.
    /// </summary>
    PartiallyGrounded = 1,

    /// <summary>
    /// Evidence across documents contradicts this point; surfaced as a cross-document discrepancy.
    /// </summary>
    ConflictDetected = 2,

    /// <summary>
    /// Failed one or more verification layers; presented only with explicit warning or excluded from main view.
    /// </summary>
    Unverified = 3,

    /// <summary>
    /// Fails basic citation or containment checks; rejected and omitted from grounded response collections.
    /// </summary>
    Unsupported = 4
}

/// <summary>
/// Synthesis degradation status informing the caller of fallback, insufficient evidence, or skips.
/// </summary>
public enum SynthesisDegradationStatus
{
    Completed_FullGrounding = 0,
    Completed_ExtractiveFallback = 1,
    Completed_LexicalGroundedOnly = 2,
    Partial_InsufficientEvidence = 3,
    Partial_CorruptedIndexSkipped = 4,
    ZeroResults_NoEvidence = 5,
    Failed_ModelError = 6
}

/// <summary>
/// Active synthesis engine utilized to generate the result.
/// </summary>
public enum ActiveSynthesisEngineKind
{
    ClassA_ExtractiveHeuristic = 0,
    ClassB_LocalSlm_DirectMl = 1,
    ClassB_LocalSlm_Cpu = 2,
    ClassC_GuardedRemote = 3
}

/// <summary>
/// Classification of factual discrepancies detected across multiple source documents.
/// </summary>
public enum ConflictClassification
{
    Numeric = 0,
    Unit = 1,
    Date = 2,
    Percentage = 3,
    Negation = 4,
    OpposingCategory = 5,
    ContradictoryCausality = 6,
    PotentialDiscrepancy_Ambiguous = 7
}

/// <summary>
/// Structured diagnostic warning emitted during study synthesis without failing the overall operation.
/// </summary>
public sealed class SynthesisWarning
{
    public string WarningCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? AffectedDocumentId { get; set; }
}

/// <summary>
/// High-level request consumed by IScholarSynthesisEngine.
/// </summary>
public sealed class StudySynthesisRequest
{
    /// <summary>
    /// Document or session scope to draw context from. Default is all documents.
    /// </summary>
    public SearchScope Scope { get; set; } = SearchScope.All();

    /// <summary>
    /// Optional user focus topic or instruction (e.g. "Focus on enzyme kinetics").
    /// Strictly clamped to &lt;= 500 characters. Null or whitespace means broad synthesis.
    /// Treated as untrusted input; isolated structurally from system instructions.
    /// </summary>
    public string? UserFocusInstruction { get; set; }

    /// <summary>
    /// Target number of items to generate (e.g. number of concepts or quiz questions).
    /// Clamped to [1, 20] for concepts, [1, 10] for quiz questions. Default is 5.
    /// </summary>
    public int TargetItemCount { get; set; } = 5;

    /// <summary>
    /// Maximum context windows to retrieve from IScholarSearchService.
    /// Clamped to [1, 10]. Default is 6 windows (~2,400 tokens / 9,600 chars).
    /// </summary>
    public int MaxContextWindows { get; set; } = 6;

    /// <summary>
    /// Minimum relevance threshold for retrieved context windows in [0.0, 1.0].
    /// Windows with scores below this threshold are omitted from synthesis context. Default is 0.25f.
    /// </summary>
    public float MinRelevanceThreshold { get; set; } = 0.25f;

    /// <summary>
    /// Optional location filter to restrict synthesis to specific pages of a document.
    /// </summary>
    public LocationFilter? LocationScope { get; set; }

    /// <summary>
    /// Strategy for handling existing persisted artifacts in the study session.
    /// </summary>
    public SynthesisPersistenceMode PersistenceMode { get; set; } = SynthesisPersistenceMode.ReplaceAll;

    /// <summary>
    /// When true, allows overwriting user-modified items during ReplaceAll.
    /// Defaults to false to prevent accidental data loss.
    /// </summary>
    public bool OverwriteUserModifiedItems { get; set; } = false;

    /// <summary>
    /// Preferred engine class. If ClassB is requested but unavailable, engine falls back to ClassA.
    /// </summary>
    public SynthesisEnginePreference EnginePreference { get; set; } = SynthesisEnginePreference.Auto;
}

/// <summary>
/// Individual grounded summary key point with citation attribution.
/// </summary>
public sealed class GroundedSummaryPoint
{
    public int PointNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public ItemGroundingStatus GroundingStatus { get; set; } = ItemGroundingStatus.Grounded;
    public IReadOnlyList<StudyCitation> Citations { get; set; } = [];
}

/// <summary>
/// Result of an executive study summary synthesis.
/// </summary>
public sealed class ExecutiveSummaryResult
{
    public static readonly ExecutiveSummaryResult Empty = new()
    {
        DegradationStatus = SynthesisDegradationStatus.ZeroResults_NoEvidence
    };

    public string CoreThesis { get; set; } = string.Empty;
    public ItemGroundingStatus ThesisGroundingStatus { get; set; } = ItemGroundingStatus.Grounded;
    public IReadOnlyList<GroundedSummaryPoint> KeyPoints { get; set; } = [];
    public string FormattedMarkdown { get; set; } = string.Empty;
    public IReadOnlyList<StudyCitation> PrimaryCitations { get; set; } = [];
    public IReadOnlyList<CrossDocumentConflict> DetectedConflicts { get; set; } = [];
    public SynthesisDegradationStatus DegradationStatus { get; set; } = SynthesisDegradationStatus.Completed_FullGrounding;
    public ActiveSynthesisEngineKind EngineUsed { get; set; } = ActiveSynthesisEngineKind.ClassA_ExtractiveHeuristic;
    public IReadOnlyList<SynthesisWarning> Warnings { get; set; } = [];
    public TimeSpan Elapsed { get; set; }
}

/// <summary>
/// Result of a concept and terminology extraction synthesis.
/// </summary>
public sealed class StudyConceptExtractionResult
{
    public static readonly StudyConceptExtractionResult Empty = new()
    {
        DegradationStatus = SynthesisDegradationStatus.ZeroResults_NoEvidence
    };

    public IReadOnlyList<StudyConcept> Concepts { get; set; } = [];
    public SynthesisDegradationStatus DegradationStatus { get; set; } = SynthesisDegradationStatus.Completed_FullGrounding;
    public ActiveSynthesisEngineKind EngineUsed { get; set; } = ActiveSynthesisEngineKind.ClassA_ExtractiveHeuristic;
    public IReadOnlyList<SynthesisWarning> Warnings { get; set; } = [];
    public TimeSpan Elapsed { get; set; }
}

/// <summary>
/// Result of a practice quiz question generation synthesis.
/// </summary>
public sealed class PracticeQuizGenerationResult
{
    public static readonly PracticeQuizGenerationResult Empty = new()
    {
        DegradationStatus = SynthesisDegradationStatus.ZeroResults_NoEvidence
    };

    public IReadOnlyList<PracticeQuizItem> Questions { get; set; } = [];
    public SynthesisDegradationStatus DegradationStatus { get; set; } = SynthesisDegradationStatus.Completed_FullGrounding;
    public ActiveSynthesisEngineKind EngineUsed { get; set; } = ActiveSynthesisEngineKind.ClassA_ExtractiveHeuristic;
    public IReadOnlyList<SynthesisWarning> Warnings { get; set; } = [];
    public TimeSpan Elapsed { get; set; }
}

/// <summary>
/// Individual thematic point in a cross-document comparative analysis.
/// </summary>
public sealed class ComparativePoint
{
    public string Theme { get; set; } = string.Empty;
    public string ConsensusStatement { get; set; } = string.Empty;
    public IReadOnlyList<StudyCitation> SupportingCitations { get; set; } = [];
    public ItemGroundingStatus GroundingStatus { get; set; } = ItemGroundingStatus.Grounded;
}

/// <summary>
/// Represents a direct or potential factual conflict detected across multiple documents.
/// </summary>
public sealed class CrossDocumentConflict
{
    public string TopicOrConcept { get; set; } = string.Empty;
    public ConflictClassification Classification { get; set; } = ConflictClassification.Numeric;
    public string PropositionA { get; set; } = string.Empty;
    public StudyCitation CitationA { get; set; } = new();
    public string PropositionB { get; set; } = string.Empty;
    public StudyCitation CitationB { get; set; } = new();
    public bool IsAmbiguous { get; set; }

    public string FormattedDescription =>
        IsAmbiguous
            ? $"Potential discrepancy on '{TopicOrConcept}': {CitationA.FormattedBadge} suggests \"{PropositionA}\", whereas {CitationB.FormattedBadge} suggests \"{PropositionB}\" (conditions may differ)."
            : $"Direct discrepancy ({Classification}) detected on '{TopicOrConcept}': {CitationA.FormattedBadge} states \"{PropositionA}\", whereas {CitationB.FormattedBadge} states \"{PropositionB}\".";
}

/// <summary>
/// Result of a cross-document comparative analysis.
/// </summary>
public sealed class ComparativeSynthesisResult
{
    public static readonly ComparativeSynthesisResult Empty = new()
    {
        DegradationStatus = SynthesisDegradationStatus.ZeroResults_NoEvidence
    };

    public IReadOnlyList<ComparativePoint> ThematicPoints { get; set; } = [];
    public IReadOnlyList<CrossDocumentConflict> Discrepancies { get; set; } = [];
    public string FormattedMarkdown { get; set; } = string.Empty;
    public SynthesisDegradationStatus DegradationStatus { get; set; } = SynthesisDegradationStatus.Completed_FullGrounding;
    public ActiveSynthesisEngineKind EngineUsed { get; set; } = ActiveSynthesisEngineKind.ClassA_ExtractiveHeuristic;
    public IReadOnlyList<SynthesisWarning> Warnings { get; set; } = [];
    public TimeSpan Elapsed { get; set; }
}

/// <summary>
/// Result of an all-in-one comprehensive study synthesis.
/// </summary>
public sealed class ComprehensiveSynthesisResult
{
    public ExecutiveSummaryResult Summary { get; set; } = ExecutiveSummaryResult.Empty;
    public StudyConceptExtractionResult Concepts { get; set; } = StudyConceptExtractionResult.Empty;
    public PracticeQuizGenerationResult Quiz { get; set; } = PracticeQuizGenerationResult.Empty;
    public ComparativeSynthesisResult? Comparison { get; set; }
    public SynthesisDegradationStatus OverallStatus { get; set; } = SynthesisDegradationStatus.Completed_FullGrounding;
    public TimeSpan TotalElapsed { get; set; }
}

/// <summary>
/// Evaluates active synthesis engine capability and active hardware/provider status.
/// </summary>
public sealed class SynthesisEngineStatus
{
    public bool IsClassAAvailable { get; set; } = true;
    public bool IsClassBAvailable { get; set; }
    public string ClassBModelName { get; set; } = string.Empty;
    public string ClassBExecutionProvider { get; set; } = "None"; // "DirectML", "CPU", "None"
    public bool IsClassCAvailable { get; set; }
    public string RecommendedEngine { get; set; } = "ClassA_ExtractiveHeuristic";
}
