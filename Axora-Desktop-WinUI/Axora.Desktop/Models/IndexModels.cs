using System;
using System.Collections.Generic;

namespace Axora.Desktop.Models;

/// <summary>
/// Execution provider used for local vector embedding generation.
/// </summary>
public enum EmbeddingExecutionProvider
{
    /// <summary>
    /// Direct3D 12 GPU acceleration via DirectML.
    /// </summary>
    DirectML = 0,

    /// <summary>
    /// Multithreaded CPU execution via ONNX Runtime parallel execution.
    /// </summary>
    Cpu = 1,

    /// <summary>
    /// Lightweight deterministic lexical feature projection (Class A fallback).
    /// </summary>
    LexicalHeuristic = 2,

    /// <summary>
    /// User-configured remote embedding API (Class C opt-in).
    /// </summary>
    RemoteOptIn = 3
}

/// <summary>
/// Status of an index during load and validation checks.
/// </summary>
public enum IndexValidationStatus
{
    /// <summary>
    /// The index is valid, complete, and synchronized with document ground truth.
    /// </summary>
    Valid = 0,

    /// <summary>
    /// The index was generated with a different model or weights version.
    /// </summary>
    Stale_ModelMismatch = 1,

    /// <summary>
    /// The index dimension does not match the active embedding engine dimension.
    /// </summary>
    Stale_DimensionMismatch = 2,

    /// <summary>
    /// The source document has been modified since the index was generated.
    /// </summary>
    Stale_DocumentModified = 3,

    /// <summary>
    /// The manifest schema version is newer than supported by this application version.
    /// </summary>
    Unsupported_FutureSchema = 4,

    /// <summary>
    /// The binary payload checksum does not match the manifest hash (quarantine triggered).
    /// </summary>
    Corrupt_ChecksumMismatch = 5,

    /// <summary>
    /// The binary file length does not match expected (RecordCount * Dimension * 4 + 64).
    /// </summary>
    Corrupt_TruncatedBinary = 6,

    /// <summary>
    /// No index exists for the specified document ID.
    /// </summary>
    Missing = 7
}

/// <summary>
/// Options for configuring vector index creation, incremental indexing, and rebuilds.
/// </summary>
public sealed class IndexOptions
{
    private int _batchSize = 16;

    /// <summary>
    /// Bounded neural inference batch size in [1, 32].
    /// </summary>
    public int BatchSize
    {
        get => _batchSize;
        set
        {
            if (value < 1 || value > 32)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    $"Batch size must be between 1 and 32. Provided: {value}. [Code: ERR_BATCH_SIZE_OUT_OF_RANGE]");
            }
            _batchSize = value;
        }
    }

    /// <summary>
    /// Whether DirectML hardware acceleration is permitted (defaults to true).
    /// </summary>
    public bool AllowDirectMl { get; set; } = true;

    /// <summary>
    /// When true, forces a full rebuild of the index, discarding existing vectors.
    /// </summary>
    public bool ForceRebuild { get; set; } = false;

    /// <summary>
    /// Convex combination weighting factor alpha in [0.0, 1.0] for hybrid search (default 0.70).
    /// </summary>
    public float HybridAlpha { get; set; } = 0.70f;
}

/// <summary>
/// JSON metadata manifest representing a persisted document vector index.
/// </summary>
public sealed class IndexManifest
{
    /// <summary>
    /// Manifest schema version (v1).
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Associated Scholar document identifier.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the source document at index generation time.
    /// </summary>
    public string SourceHash { get; set; } = string.Empty;

    /// <summary>
    /// Name/identifier of the embedding model (e.g. "all-MiniLM-L6-v2").
    /// </summary>
    public string EmbeddingModelId { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 fingerprint of the model weight artifact used to generate vectors.
    /// </summary>
    public string ModelFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// Embedding dimensionality (e.g. 384).
    /// </summary>
    public int VectorDimension { get; set; } = 384;

    /// <summary>
    /// Total number of indexed vector records.
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// Estimated total token count indexed.
    /// </summary>
    public long TotalTokensEstimate { get; set; }

    /// <summary>
    /// 64-character hex lowercase SHA-256 checksum of the vectors.bin payload.
    /// </summary>
    public string BinaryPayloadHash { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of index creation.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp of last index update.
    /// </summary>
    public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Ordered list of vector index records mapped to context windows.
    /// </summary>
    public List<VectorIndexRecord> Records { get; set; } = [];
}

/// <summary>
/// Record mapping an individual context window to its vector offset and metadata.
/// </summary>
public sealed class VectorIndexRecord
{
    /// <summary>
    /// Canonical context window identifier (win_{DocId}_p{Page}_...).
    /// </summary>
    public string WindowId { get; set; } = string.Empty;

    /// <summary>
    /// Parent document identifier.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// 1-based page number where the focal chunk resides.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// 0-based chunk index of the focal chunk.
    /// </summary>
    public int FocalChunkIndex { get; set; } = -1;

    /// <summary>
    /// 64-hex lowercase SHA-256 content hash of FormattedText.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Absolute byte offset of this vector within vectors.bin (>= 64).
    /// </summary>
    public int VectorOffset { get; set; }

    /// <summary>
    /// Character length of FormattedText.
    /// </summary>
    public int CharLength { get; set; }

    /// <summary>
    /// Estimated token count.
    /// </summary>
    public int EstimatedTokens { get; set; }

    /// <summary>
    /// Ordered constituent chunk indices in this window.
    /// </summary>
    public List<int> ConstituentChunkIndices { get; set; } = [];

    /// <summary>
    /// Formatted context window text preserved for reloaded BM25 lexical retrieval and search result snippets.
    /// </summary>
    public string FormattedText { get; set; } = string.Empty;

    /// <summary>
    /// Citations associated with this context window preserved for cold/reloaded search results (AUD2-W3D-06).
    /// </summary>
    public List<StudyCitation> Citations { get; set; } = [];

    /// <summary>
    /// Computes the unique deterministic composite address key in accordance with INV-W3D-02.
    /// Format: rec_{DocumentId}_{WindowId}_{modelId}_{dimension}
    /// </summary>
    public string ComputeCompositeKey(string modelId, int dimension) =>
        $"rec_{DocumentId}_{WindowId}_{modelId}_{dimension}";
}

/// <summary>
/// In-memory representation of an active vector index for search and inspection.
/// </summary>
public sealed class ScholarVectorIndex
{
    /// <summary>
    /// Index manifest metadata.
    /// </summary>
    public IndexManifest Manifest { get; set; } = new();

    /// <summary>
    /// In-memory contiguous vector buffers (recordIndex -> float[384]), or null if memory-mapped.
    /// </summary>
    public float[][]? Vectors { get; set; }

    /// <summary>
    /// Associated context windows (hydrated if available).
    /// </summary>
    public IReadOnlyList<BoundedContextWindow>? Windows { get; set; }

    /// <summary>
    /// Inverted index mapping terms to window indices and frequencies for BM25 search.
    /// </summary>
    public Dictionary<string, List<(int RecordIndex, int TermFreq)>> LexicalInvertedIndex { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Document lengths (total term counts) per record index.
    /// </summary>
    public int[] DocumentLengths { get; set; } = [];

    /// <summary>
    /// Average document length across all indexed windows.
    /// </summary>
    public double AverageDocumentLength { get; set; }
}

/// <summary>
/// Result of a dense vector cosine similarity search.
/// </summary>
public sealed record VectorSearchResult(
    string WindowId,
    string DocumentId,
    int PageNumber,
    int FocalChunkIndex,
    float SimilarityScore,
    string Snippet,
    IReadOnlyList<StudyCitation> Citations,
    BoundedContextWindow? HydratedWindow = null);

/// <summary>
/// Result of a hybrid (dense vector + normalized BM25 lexical) search.
/// </summary>
public sealed record HybridSearchResult(
    string WindowId,
    string DocumentId,
    int PageNumber,
    int FocalChunkIndex,
    float CombinedScore,
    float VectorSimilarity,
    float LexicalScore,
    string Snippet,
    IReadOnlyList<StudyCitation> Citations,
    BoundedContextWindow? HydratedWindow = null);

/// <summary>
/// Outbound transmission preview for Class C remote embedding provider (INV-W3D-21, AUD2-W3D-07).
/// Mandates preview of destination endpoint, character count, and explicit user confirmation.
/// </summary>
public sealed record RemoteTransmissionPreview(
    string DestinationEndpoint,
    int PayloadCharacterCount,
    int EstimatedTokenCount,
    IReadOnlyList<string> SampleSnippets,
    bool UserConfirmed);

/// <summary>
/// Enforces mandatory preview and user confirmation boundary for Class C remote transmissions (INV-W3D-21).
/// </summary>
public static class RemoteTransmissionGuard
{
    public static RemoteTransmissionPreview GeneratePreview(
        string destinationEndpoint,
        IReadOnlyList<string> texts,
        bool userConfirmed = false)
    {
        ArgumentNullException.ThrowIfNull(destinationEndpoint);
        ArgumentNullException.ThrowIfNull(texts);

        int totalChars = texts.Sum(t => t?.Length ?? 0);
        int totalTokens = texts.Sum(t => (t?.Length ?? 0) / 4);
        var samples = texts
            .Take(3)
            .Select(t => (t?.Length ?? 0) > 80 ? t![..80] + "..." : (t ?? string.Empty))
            .ToList();

        return new RemoteTransmissionPreview(destinationEndpoint, totalChars, totalTokens, samples, userConfirmed);
    }

    public static void ValidateTransmission(RemoteTransmissionPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (!preview.UserConfirmed)
        {
            throw new InvalidOperationException(
                "Cannot transmit outbound data to remote provider without explicit user confirmation. [Code: ERR_UNCONFIRMED_REMOTE_TRANSMISSION]");
        }
    }
}

