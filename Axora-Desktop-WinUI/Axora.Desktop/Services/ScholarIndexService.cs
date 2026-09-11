using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Axora.Desktop.Helpers;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Production service orchestrating document vector indexing, incremental deduplication,
/// dense vector search, and hybrid BM25 retrieval across AXORA Scholar documents.
/// </summary>
public sealed class ScholarIndexService : IScholarIndexService
{
    private readonly IEmbeddingEngine _embeddingEngine;
    private readonly ScholarVectorIndexWriter _writer;
    private readonly ScholarVectorIndexReader _reader;
    private readonly ILogger<ScholarIndexService> _logger;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _documentLocks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ScholarVectorIndex> _indexCache = new(StringComparer.Ordinal);

    public ScholarIndexService(
        IEmbeddingEngine embeddingEngine,
        ScholarVectorIndexWriter writer,
        ScholarVectorIndexReader reader,
        ILogger<ScholarIndexService>? logger = null)
    {
        _embeddingEngine = embeddingEngine ?? throw new ArgumentNullException(nameof(embeddingEngine));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _logger = logger ?? NullLogger<ScholarIndexService>.Instance;
    }

    /// <inheritdoc/>
    public async Task<ScholarVectorIndex> IndexDocumentAsync(
        ScholarDocument document,
        IReadOnlyList<BoundedContextWindow> windows,
        IndexOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(windows);
        ct.ThrowIfCancellationRequested();

        // 1. Invariant INV-W3D-27: Comprehensive Composite Ingestion Exclusion
        ValidateCanonicalWindows(windows);

        var effectiveOptions = options ?? new IndexOptions();

        var sem = _documentLocks.GetOrAdd(document.DocumentId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        try
        {
            return await IndexDocumentInternalAsync(document, windows, effectiveOptions, ct);
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<ScholarVectorIndex> IndexDocumentInternalAsync(
        ScholarDocument document,
        IReadOnlyList<BoundedContextWindow> windows,
        IndexOptions options,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        int recordCount = windows.Count;
        int dimension = _embeddingEngine.Dimension;

        // Check for existing index for incremental re-embedding (INV-W3D-14)
        Dictionary<string, float[]> existingVectorsByContentHash = new(StringComparer.Ordinal);
        if (!options.ForceRebuild)
        {
            var existingIndex = await _reader.LoadIndexAsync(document.DocumentId, ct);
            if (existingIndex != null &&
                string.Equals(existingIndex.Manifest.ModelFingerprint, _embeddingEngine.ModelFingerprint, StringComparison.OrdinalIgnoreCase) &&
                existingIndex.Manifest.VectorDimension == dimension &&
                existingIndex.Vectors != null)
            {
                for (int i = 0; i < existingIndex.Manifest.Records.Count; i++)
                {
                    var rec = existingIndex.Manifest.Records[i];
                    if (!string.IsNullOrEmpty(rec.ContentHash) && i < existingIndex.Vectors.Length)
                    {
                        existingVectorsByContentHash[rec.ContentHash] = existingIndex.Vectors[i];
                    }
                }
            }
        }

        var records = new List<VectorIndexRecord>(recordCount);
        var finalVectors = new float[recordCount][];
        var textToEmbedIndices = new List<int>();
        var textsToEmbed = new List<string>();

        long totalTokens = 0;

        for (int i = 0; i < recordCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            var win = windows[i];
            string contentHash = ComputeSha256(win.FormattedText);
            totalTokens += win.EstimatedTokens;

            var rec = new VectorIndexRecord
            {
                WindowId = win.WindowId,
                DocumentId = document.DocumentId,
                PageNumber = win.PageNumber,
                FocalChunkIndex = win.FocalChunk?.ChunkIndex ?? -1,
                ContentHash = contentHash,
                VectorOffset = 64 + (i * dimension * 4),
                CharLength = win.CharLength,
                EstimatedTokens = win.EstimatedTokens,
                ConstituentChunkIndices = win.ConstituentChunkIndices?.ToList() ?? [],
                FormattedText = win.FormattedText,
                Citations = win.Citations?.ToList() ?? []
            };
            records.Add(rec);

            // Check if existing vector can be reused without neural re-computation
            if (existingVectorsByContentHash.TryGetValue(contentHash, out var cachedVector))
            {
                finalVectors[i] = cachedVector;
            }
            else
            {
                textToEmbedIndices.Add(i);
                textsToEmbed.Add(win.FormattedText);
            }
        }

        // Bounded batch neural execution (INV-W3D-22, INV-W3D-23)
        int batchSize = options.BatchSize;
        for (int b = 0; b < textsToEmbed.Count; b += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            int currentBatchCount = Math.Min(batchSize, textsToEmbed.Count - b);
            var batchSlice = textsToEmbed.GetRange(b, currentBatchCount);

            var batchVectors = await _embeddingEngine.GenerateBatchEmbeddingsAsync(batchSlice, ct);

            for (int k = 0; k < currentBatchCount; k++)
            {
                int targetIndex = textToEmbedIndices[b + k];
                finalVectors[targetIndex] = batchVectors[k];
            }
        }

        // Build inverted lexical index for BM25 hybrid search
        var invertedIndex = new Dictionary<string, List<(int RecordIndex, int TermFreq)>>(StringComparer.OrdinalIgnoreCase);
        var docLengths = new int[recordCount];
        long totalTerms = 0;

        for (int i = 0; i < recordCount; i++)
        {
            var terms = TokenizeTerms(windows[i].FormattedText);
            docLengths[i] = terms.Count;
            totalTerms += terms.Count;

            var termFreqs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var term in terms)
            {
                termFreqs[term] = termFreqs.TryGetValue(term, out int f) ? f + 1 : 1;
            }

            foreach (var kvp in termFreqs)
            {
                if (!invertedIndex.TryGetValue(kvp.Key, out var list))
                {
                    list = [];
                    invertedIndex[kvp.Key] = list;
                }
                list.Add((i, kvp.Value));
            }
        }

        double avgDocLength = recordCount > 0 ? (double)totalTerms / recordCount : 0.0;

        var manifest = new IndexManifest
        {
            SchemaVersion = 1,
            DocumentId = document.DocumentId,
            SourceHash = document.SourceHash ?? string.Empty,
            EmbeddingModelId = _embeddingEngine.ModelId,
            ModelFingerprint = _embeddingEngine.ModelFingerprint,
            VectorDimension = dimension,
            TotalRecords = recordCount,
            TotalTokensEstimate = totalTokens,
            Records = records,
            CreatedAtUtc = DateTime.UtcNow,
            LastUpdatedAtUtc = DateTime.UtcNow
        };

        // Release active MMF mapping before overwriting files to prevent NTFS file lock errors (AUD-W3D-06)
        _reader.DisposeActiveMapping(document.DocumentId);

        // Persist to disk using staged same-volume replacement semantics
        await _writer.WriteIndexAsync(manifest, finalVectors, ct);

        var activeIndex = new ScholarVectorIndex
        {
            Manifest = manifest,
            Vectors = finalVectors,
            Windows = windows,
            LexicalInvertedIndex = invertedIndex,
            DocumentLengths = docLengths,
            AverageDocumentLength = avgDocLength
        };

        _indexCache[document.DocumentId] = activeIndex;
        return activeIndex;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<VectorSearchResult>> SearchVectorAsync(
        string documentId,
        string queryText,
        int topK = 5,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(queryText) || topK <= 0)
        {
            return [];
        }

        var index = await GetOrLoadIndexAsync(documentId, ct);
        if (index == null || index.Vectors == null || index.Vectors.Length == 0)
        {
            return [];
        }

        float[] queryVector = await _embeddingEngine.GenerateEmbeddingAsync(queryText, ct);

        int count = index.Vectors.Length;
        var scores = new (int Index, float Score)[count];

        for (int i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            float rawDot = SimdVectorHelper.DotProduct(queryVector, index.Vectors[i]);
            // Invariant INV-W3D-07: Bounded similarity clamping
            float clamped = Math.Clamp(rawDot, -1.0f, 1.0f);
            scores[i] = (i, clamped);
        }

        var topHits = scores
            .OrderByDescending(s => s.Score)
            .Take(topK)
            .ToList();

        var results = new List<VectorSearchResult>(topHits.Count);
        for (int h = 0; h < topHits.Count; h++)
        {
            int r = topHits[h].Index;
            var rec = index.Manifest.Records[r];
            var window = (index.Windows != null && r < index.Windows.Count) ? index.Windows[r] : null;

            results.Add(new VectorSearchResult(
                rec.WindowId,
                rec.DocumentId,
                rec.PageNumber,
                rec.FocalChunkIndex,
                topHits[h].Score,
                window?.FormattedText ?? string.Empty,
                window?.Citations ?? [],
                window));
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<HybridSearchResult>> SearchHybridAsync(
        string documentId,
        string queryText,
        float alpha = 0.70f,
        int topK = 5,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(queryText) || topK <= 0)
        {
            return [];
        }

        var index = await GetOrLoadIndexAsync(documentId, ct);
        if (index == null || index.Vectors == null || index.Vectors.Length == 0)
        {
            return [];
        }

        alpha = Math.Clamp(alpha, 0.0f, 1.0f);

        // 1. Vector similarity scores
        float[] queryVector = await _embeddingEngine.GenerateEmbeddingAsync(queryText, ct);
        int recordCount = index.Vectors.Length;
        var vectorScores = new float[recordCount];

        for (int i = 0; i < recordCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            float dot = SimdVectorHelper.DotProduct(queryVector, index.Vectors[i]);
            // Re-scale / clamp cosine similarity to [0, 1] for convex hybrid combination
            vectorScores[i] = Math.Clamp(dot, 0.0f, 1.0f);
        }

        // 2. Lexical BM25 scores
        var queryTerms = TokenizeTerms(queryText);
        var rawBm25Scores = new float[recordCount];

        if (queryTerms.Count > 0 && index.LexicalInvertedIndex.Count > 0)
        {
            const double k1 = 1.2;
            const double b = 0.75;
            double avgdl = Math.Max(index.AverageDocumentLength, 1.0);
            int N = recordCount;

            foreach (var term in queryTerms.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (index.LexicalInvertedIndex.TryGetValue(term, out var postings))
                {
                    int docFreq = postings.Count;
                    double idf = Math.Log((N - docFreq + 0.5) / (docFreq + 0.5) + 1.0);
                    if (idf < 0) idf = 0;

                    foreach (var (docIdx, tf) in postings)
                    {
                        int docLen = index.DocumentLengths[docIdx];
                        double numerator = tf * (k1 + 1.0);
                        double denominator = tf + k1 * (1.0 - b + b * (docLen / avgdl));
                        rawBm25Scores[docIdx] += (float)(idf * (numerator / denominator));
                    }
                }
            }
        }

        // 3. Candidate pool identification (matches with vectorScore > 0 or rawBM25 > 0)
        var candidateIndices = new List<int>();
        for (int i = 0; i < recordCount; i++)
        {
            if (vectorScores[i] > 0.0f || rawBm25Scores[i] > 0.0f)
            {
                candidateIndices.Add(i);
            }
        }

        if (candidateIndices.Count == 0)
        {
            return [];
        }

        // 4. Min-Max Lexical Normalization across Candidate Pool (INV-W3D-30)
        var normalizedLexicalScores = new float[recordCount];

        if (candidateIndices.Count == 1)
        {
            int idx = candidateIndices[0];
            normalizedLexicalScores[idx] = rawBm25Scores[idx] > 0.0f ? 1.0f : 0.0f;
        }
        else
        {
            float minBm25 = float.MaxValue;
            float maxBm25 = float.MinValue;

            foreach (int idx in candidateIndices)
            {
                float score = rawBm25Scores[idx];
                if (score < minBm25) minBm25 = score;
                if (score > maxBm25) maxBm25 = score;
            }

            if (maxBm25 - minBm25 < 1e-7f)
            {
                float uniformScore = maxBm25 > 0.0f ? 1.0f : 0.0f;
                foreach (int idx in candidateIndices)
                {
                    normalizedLexicalScores[idx] = uniformScore;
                }
            }
            else
            {
                float range = maxBm25 - minBm25;
                foreach (int idx in candidateIndices)
                {
                    normalizedLexicalScores[idx] = Math.Clamp((rawBm25Scores[idx] - minBm25) / range, 0.0f, 1.0f);
                }
            }
        }

        // 5. Convex combination & Deterministic 7-Level Multi-Key Tie-Breaking (INV-W3D-30)
        var candidateHits = new List<(
            int Index,
            float HybridScore,
            float VectorScore,
            float LexicalScore,
            VectorIndexRecord Record)>(candidateIndices.Count);

        foreach (int idx in candidateIndices)
        {
            float vScore = vectorScores[idx];
            float lScore = normalizedLexicalScores[idx];
            float hybrid = (alpha * vScore) + ((1.0f - alpha) * lScore);

            // Handle NaN / Infinity protection
            if (float.IsNaN(hybrid) || float.IsInfinity(hybrid)) hybrid = 0.0f;
            if (float.IsNaN(vScore) || float.IsInfinity(vScore)) vScore = 0.0f;
            if (float.IsNaN(lScore) || float.IsInfinity(lScore)) lScore = 0.0f;

            candidateHits.Add((idx, hybrid, vScore, lScore, index.Manifest.Records[idx]));
        }

        var sortedHits = candidateHits
            .OrderByDescending(c => c.HybridScore)
            .ThenByDescending(c => c.VectorScore)
            .ThenByDescending(c => c.LexicalScore)
            .ThenBy(c => c.Record.DocumentId, StringComparer.Ordinal)
            .ThenBy(c => c.Record.PageNumber)
            .ThenBy(c => c.Record.FocalChunkIndex)
            .ThenBy(c => c.Record.WindowId, StringComparer.Ordinal)
            .Take(topK)
            .ToList();

        var results = new List<HybridSearchResult>(sortedHits.Count);
        foreach (var hit in sortedHits)
        {
            var window = (index.Windows != null && hit.Index < index.Windows.Count) ? index.Windows[hit.Index] : null;

            results.Add(new HybridSearchResult(
                hit.Record.WindowId,
                hit.Record.DocumentId,
                hit.Record.PageNumber,
                hit.Record.FocalChunkIndex,
                hit.HybridScore,
                hit.VectorScore,
                hit.LexicalScore,
                window?.FormattedText ?? string.Empty,
                window?.Citations ?? [],
                window));
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<IndexValidationStatus> ValidateIndexAsync(string documentId, CancellationToken ct = default)
    {
        var status = await _reader.ValidateIndexAsync(
            documentId,
            expectedModelFingerprint: _embeddingEngine.ModelFingerprint,
            expectedDimension: _embeddingEngine.Dimension,
            ct: ct);

        // If the index on disk is corrupted, quarantined, or mismatched, evict any cached representation (AUD-W3D-08)
        if (status != IndexValidationStatus.Valid)
        {
            _indexCache.TryRemove(documentId, out _);
            _reader.DisposeActiveMapping(documentId);
        }

        return status;
    }

    /// <inheritdoc/>
    public async Task<bool> RebuildIndexAsync(
        ScholarDocument document,
        IReadOnlyList<BoundedContextWindow> windows,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(windows);

        await DeleteIndexAsync(document.DocumentId, ct);
        var options = new IndexOptions { ForceRebuild = true };
        var index = await IndexDocumentAsync(document, windows, options, ct);
        return index != null;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteIndexAsync(string documentId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var sem = _documentLocks.GetOrAdd(documentId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        try
        {
            _indexCache.TryRemove(documentId, out _);
            _reader.DisposeActiveMapping(documentId);

            string dir = _writer.GetIndexDirectory(documentId);
            if (Directory.Exists(dir))
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                    _logger.LogInformation("Deleted index directory for document '{DocId}'.", documentId);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete index directory for document '{DocId}'.", documentId);
                    return false;
                }
            }

            return true;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<ScholarVectorIndex?> GetIndexAsync(string documentId, CancellationToken ct = default)
    {
        return await GetOrLoadIndexAsync(documentId, ct);
    }

    private async Task<ScholarVectorIndex?> GetOrLoadIndexAsync(string documentId, CancellationToken ct)
    {
        if (_indexCache.TryGetValue(documentId, out var cached))
        {
            return cached;
        }

        var loaded = await _reader.LoadIndexAsync(documentId, ct);
        if (loaded != null)
        {
            // Rebuild lexical index in memory for loaded index
            RebuildLexicalIndexFromLoaded(loaded);
            _indexCache[documentId] = loaded;
        }
        return loaded;
    }

    private static void RebuildLexicalIndexFromLoaded(ScholarVectorIndex index)
    {
        if (index.Windows == null || index.Windows.Count == 0) return;

        int recordCount = index.Windows.Count;
        var invertedIndex = new Dictionary<string, List<(int RecordIndex, int TermFreq)>>(StringComparer.OrdinalIgnoreCase);
        var docLengths = new int[recordCount];
        long totalTerms = 0;

        for (int i = 0; i < recordCount; i++)
        {
            var terms = TokenizeTerms(index.Windows[i].FormattedText);
            docLengths[i] = terms.Count;
            totalTerms += terms.Count;

            var termFreqs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var term in terms)
            {
                termFreqs[term] = termFreqs.TryGetValue(term, out int f) ? f + 1 : 1;
            }

            foreach (var kvp in termFreqs)
            {
                if (!invertedIndex.TryGetValue(kvp.Key, out var list))
                {
                    list = [];
                    invertedIndex[kvp.Key] = list;
                }
                list.Add((i, kvp.Value));
            }
        }

        index.LexicalInvertedIndex = invertedIndex;
        index.DocumentLengths = docLengths;
        index.AverageDocumentLength = recordCount > 0 ? (double)totalTerms / recordCount : 0.0;
    }

    private static void ValidateCanonicalWindows(IReadOnlyList<BoundedContextWindow> windows)
    {
        for (int i = 0; i < windows.Count; i++)
        {
            var window = windows[i];
            if (window == null)
            {
                throw new ArgumentNullException(nameof(windows), $"Window at index {i} is null.");
            }

            bool isComposite = window.DocumentId.Equals("composite", StringComparison.OrdinalIgnoreCase)
                || window.PageNumber == 0
                || window.WindowId.StartsWith("comp_", StringComparison.Ordinal)
                || window.FocalChunk == null;

            if (isComposite)
            {
                throw new ArgumentException(
                    $"Composite context window '{window.WindowId}' cannot be indexed into a per-document index. " +
                    $"[Code: ERR_COMPOSITE_WINDOW_NOT_INDEXABLE]", nameof(windows));
            }
        }
    }

    private static List<string> TokenizeTerms(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var words = text.Split(
            [' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '-', '(', ')', '[', ']', '\"', '\''],
            StringSplitOptions.RemoveEmptyEntries);

        var terms = new List<string>(words.Length);
        for (int i = 0; i < words.Length; i++)
        {
            string clean = words[i].Trim().ToLowerInvariant();
            if (clean.Length > 0)
            {
                terms.Add(clean);
            }
        }

        return terms;
    }

    private static string ComputeSha256(string text)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
