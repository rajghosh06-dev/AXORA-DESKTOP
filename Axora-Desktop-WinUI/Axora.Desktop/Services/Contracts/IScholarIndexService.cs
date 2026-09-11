using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Primary service contract for document vector indexing, incremental deduplication,
/// dense vector search, and hybrid BM25 retrieval across AXORA Scholar documents.
/// </summary>
public interface IScholarIndexService
{
    /// <summary>
    /// Ingests canonical BoundedContextWindow instances from C7.3, performs incremental deduplication,
    /// generates vector embeddings, and persists the dual-file vector index (vectors.bin and index_manifest.json).
    /// Rejects composite windows (DocumentId == "composite" || PageNumber == 0 || WindowId.StartsWith("comp_") || FocalChunk == null)
    /// with ArgumentException and code ERR_COMPOSITE_WINDOW_NOT_INDEXABLE.
    /// </summary>
    Task<ScholarVectorIndex> IndexDocumentAsync(
        ScholarDocument document,
        IReadOnlyList<BoundedContextWindow> windows,
        IndexOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes dense vector cosine similarity search against an indexed document using hardware SIMD dot product.
    /// Scores are bounded in [-1.0, 1.0].
    /// </summary>
    Task<IReadOnlyList<VectorSearchResult>> SearchVectorAsync(
        string documentId,
        string queryText,
        int topK = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Executes hybrid retrieval combining dense vector similarity and Min-Max normalized lexical BM25 scores:
    /// CombinedScore = alpha * VectorSimilarity + (1 - alpha) * LexicalScore.
    /// Uses deterministic 7-level multi-key tie-breaking.
    /// </summary>
    Task<IReadOnlyList<HybridSearchResult>> SearchHybridAsync(
        string documentId,
        string queryText,
        float alpha = 0.70f,
        int topK = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates the integrity, schema version, model fingerprint, and source hash of an existing index.
    /// If payload checksum fails, quarantines the corrupted index directory and returns Corrupt_ChecksumMismatch.
    /// </summary>
    Task<IndexValidationStatus> ValidateIndexAsync(
        string documentId,
        CancellationToken ct = default);

    /// <summary>
    /// Performs a full rebuild of a document's vector index, bypassing cached vectors while
    /// preserving original ground-truth source documents intact.
    /// </summary>
    Task<bool> RebuildIndexAsync(
        ScholarDocument document,
        IReadOnlyList<BoundedContextWindow> windows,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes the derived vector index directory (%APPDATA%\Axora\Scholar\indexes\{DocumentId}\).
    /// Ground-truth document records (%APPDATA%\Axora\Scholar\documents\) are strictly preserved.
    /// </summary>
    Task<bool> DeleteIndexAsync(
        string documentId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the loaded or cached in-memory representation of an active document vector index.
    /// Returns null if the index does not exist or fails validation.
    /// </summary>
    Task<ScholarVectorIndex?> GetIndexAsync(
        string documentId,
        CancellationToken ct = default);
}
