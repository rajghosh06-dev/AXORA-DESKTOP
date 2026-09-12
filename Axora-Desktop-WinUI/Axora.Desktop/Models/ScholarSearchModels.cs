using System;
using System.Collections.Generic;
using System.Linq;

namespace Axora.Desktop.Models;

/// <summary>
/// Categorization of search scopes across the user's Scholar library and sessions.
/// </summary>
public enum SearchScopeKind
{
    AllDocuments = 0,
    SessionDocuments = 1,
    SingleDocument = 2,
    ExplicitDocuments = 3
}

/// <summary>
/// Defines the target document scope for a search request.
/// </summary>
public sealed class SearchScope
{
    public SearchScopeKind Kind { get; set; } = SearchScopeKind.AllDocuments;
    public string? TargetId { get; set; }
    public IReadOnlyList<string> DocumentIds { get; set; } = [];

    public static SearchScope All() => new() { Kind = SearchScopeKind.AllDocuments };
    public static SearchScope Session(string sessionId) => new() { Kind = SearchScopeKind.SessionDocuments, TargetId = sessionId };
    public static SearchScope Single(string documentId) => new() { Kind = SearchScopeKind.SingleDocument, TargetId = documentId, DocumentIds = [documentId] };
    public static SearchScope Explicit(IEnumerable<string> documentIds) => new() { Kind = SearchScopeKind.ExplicitDocuments, DocumentIds = documentIds?.Distinct(StringComparer.Ordinal).ToList() ?? [] };
}

/// <summary>
/// Page-level location filter evaluated early prior to candidate scoring.
/// </summary>
public sealed class LocationFilter
{
    public int? StartPage { get; set; }
    public int? EndPage { get; set; }
    public IReadOnlyList<int>? SpecificPages { get; set; }

    public bool Matches(int pageNumber)
    {
        if (SpecificPages != null && SpecificPages.Count > 0 && !SpecificPages.Contains(pageNumber))
            return false;
        if (StartPage.HasValue && pageNumber < StartPage.Value)
            return false;
        if (EndPage.HasValue && pageNumber > EndPage.Value)
            return false;
        return true;
    }
}

/// <summary>
/// Search degradation state informing the consumer of runtime fallback or partial skips.
/// </summary>
public enum SearchDegradationStatus
{
    FullHybrid = 0,
    LexicalOnly_ModelMissing = 1,
    LexicalOnly_HardwareFallback = 2,
    PartialResults_CorruptedIndexSkipped = 3,
    PartialResults_MissingIndexSkipped = 4,
    ZeroResults = 5,
    NoIndexedDocuments = 6
}

/// <summary>
/// Structured diagnostic warning emitted during multi-document retrieval without failing the overall query.
/// </summary>
public sealed class SearchWarning
{
    public string DocumentId { get; set; } = string.Empty;
    public string WarningCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// High-level query request consumed by IScholarSearchService.
/// </summary>
public sealed class ScholarSearchRequest
{
    /// <summary>
    /// Raw query text. Clamped to maximum 2,000 characters.
    /// </summary>
    public string QueryText { get; set; } = string.Empty;

    /// <summary>
    /// Document scope governing which documents to search. Default is AllDocuments.
    /// </summary>
    public SearchScope Scope { get; set; } = SearchScope.All();

    /// <summary>
    /// Number of top ranked results to return. Bounded in [1, 100]. Default is 10.
    /// </summary>
    public int TopK { get; set; } = 10;

    /// <summary>
    /// Optional page/location filter evaluated prior to candidate scoring.
    /// </summary>
    public LocationFilter? LocationScope { get; set; }

    /// <summary>
    /// Minimum combined score threshold in [0.0, 1.0].
    /// </summary>
    public float MinScoreThreshold { get; set; } = 0.0f;

    /// <summary>
    /// Convex combination weighting factor alpha in [0.0, 1.0] (default 0.70f).
    /// If dense retrieval is unavailable, effective alpha is strictly forced to 0.0f.
    /// </summary>
    public float HybridAlpha { get; set; } = 0.70f;

    /// <summary>
    /// When true, includes the underlying BoundedContextWindow instances on result items.
    /// </summary>
    public bool IncludeHydratedWindows { get; set; } = false;

    /// <summary>
    /// Optional remote preview validation for Class C remote providers (INV-W3E-26).
    /// </summary>
    public RemoteTransmissionPreview? RemotePreview { get; set; }
}

/// <summary>
/// Individual grounded search hit item with full citation provenance and scores.
/// </summary>
public sealed class ScholarSearchResultItem
{
    public int Rank { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string WindowId { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public int FocalChunkIndex { get; set; } = -1;
    public IReadOnlyList<int> ConstituentChunkIndices { get; set; } = [];

    public float CombinedScore { get; set; }
    public float VectorSimilarity { get; set; }
    public float LexicalScore { get; set; }

    public string FormattedSnippet { get; set; } = string.Empty;
    public IReadOnlyList<StudyCitation> Citations { get; set; } = [];
    public SourceAvailabilityStatus SourceStatus { get; set; } = SourceAvailabilityStatus.None;
    public BoundedContextWindow? HydratedWindow { get; set; }
}

/// <summary>
/// Complete retrieval response returned by IScholarSearchService.
/// </summary>
public sealed class ScholarSearchResponse
{
    public static readonly ScholarSearchResponse Empty = new()
    {
        Items = [],
        TotalCandidatesEvaluated = 0,
        Elapsed = TimeSpan.Zero,
        DegradationStatus = SearchDegradationStatus.ZeroResults
    };

    public IReadOnlyList<ScholarSearchResultItem> Items { get; set; } = [];
    public int TotalCandidatesEvaluated { get; set; }
    public TimeSpan Elapsed { get; set; }
    public SearchDegradationStatus DegradationStatus { get; set; } = SearchDegradationStatus.FullHybrid;
    public IReadOnlyList<SearchWarning> Warnings { get; set; } = [];
}

/// <summary>
/// Represents current search hardware, model presence, and capability states.
/// </summary>
public sealed class SearchCapabilityStatus
{
    public bool IsNeuralModelInstalled { get; set; }
    public bool IsDirectMlSupported { get; set; }
    public EmbeddingExecutionProvider ActiveProvider { get; set; } = EmbeddingExecutionProvider.LexicalHeuristic;
    public string StatusBadgeText { get; set; } = "Ready (Lexical Only)";
    public string ActiveProviderDescription { get; set; } = "Class A Lexical Feature Projector";
}
