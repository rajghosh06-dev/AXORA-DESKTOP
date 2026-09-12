using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Helpers;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Production-oriented local Scholar retrieval service implementing multi-document fan-out,
/// global Min-Max BM25 normalization, 7-level ranking determinism, citation grounding,
/// and graceful degradation.
/// </summary>
public sealed class ScholarSearchService : IScholarSearchService
{
    private readonly IScholarIndexService _indexService;
    private readonly IScholarLibraryService _libraryService;
    private readonly IEmbeddingEngine _embeddingEngine;
    private readonly ILogger<ScholarSearchService> _logger;
    private readonly ConcurrentDictionary<string, IndexValidationStatus> _validatedDocuments = new(StringComparer.Ordinal);

    public ScholarSearchService(
        IScholarIndexService indexService,
        IScholarLibraryService libraryService,
        IEmbeddingEngine embeddingEngine,
        ILogger<ScholarSearchService> logger)
    {
        _indexService = indexService ?? throw new ArgumentNullException(nameof(indexService));
        _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
        _embeddingEngine = embeddingEngine ?? throw new ArgumentNullException(nameof(embeddingEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private async Task<IndexValidationStatus> GetOrValidateIndexStatusAsync(string documentId, CancellationToken ct)
    {
        if (_validatedDocuments.TryGetValue(documentId, out var cachedStatus))
        {
            return cachedStatus;
        }

        var status = await _indexService.ValidateIndexAsync(documentId, ct);
        _validatedDocuments[documentId] = status;
        return status;
    }

    /// <inheritdoc/>
    public Task<SearchCapabilityStatus> GetCapabilityStatusAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var capState = _embeddingEngine as IEmbeddingCapabilityStateProvider;

        var status = new SearchCapabilityStatus
        {
            IsNeuralModelInstalled = capState?.IsNeuralModelInstalled ?? (_embeddingEngine.ActiveProvider != EmbeddingExecutionProvider.LexicalHeuristic),
            IsDirectMlSupported = capState?.IsDirectMlSupported ?? (_embeddingEngine.ActiveProvider == EmbeddingExecutionProvider.DirectML),
            ActiveProvider = _embeddingEngine.ActiveProvider,
            StatusBadgeText = capState?.GetCapabilityStatusBadgeText() ?? "Ready (Lexical Only)",
            ActiveProviderDescription = capState?.GetActiveProviderDescription() ?? "Class A Lexical Feature Projector"
        };

        return Task.FromResult(status);
    }

    /// <inheritdoc/>
    public Task<ScholarSearchResponse> SearchDocumentAsync(
        string documentId,
        string queryText,
        int topK = 5,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        var request = new ScholarSearchRequest
        {
            QueryText = queryText,
            Scope = SearchScope.Single(documentId),
            TopK = topK
        };
        return SearchAsync(request, ct);
    }

    /// <inheritdoc/>
    public Task<ScholarSearchResponse> SearchSessionAsync(
        string sessionId,
        string queryText,
        int topK = 10,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        var request = new ScholarSearchRequest
        {
            QueryText = queryText,
            Scope = SearchScope.Session(sessionId),
            TopK = topK
        };
        return SearchAsync(request, ct);
    }

    /// <inheritdoc/>
    public async Task<ScholarSearchResponse> SearchAsync(
        ScholarSearchRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.QueryText == null)
            throw new ArgumentNullException(nameof(request.QueryText), "Query text cannot be null. [Code: ERR_INVALID_QUERY_TEXT]");

        long startTimestamp = Stopwatch.GetTimestamp();

        // 0. Remote Transmission Guard (INV-W3E-26)
        if (request.RemotePreview != null)
        {
            RemoteTransmissionGuard.ValidateTransmission(request.RemotePreview);
        }

        // 1. Query Normalization & Validation (INV-W3E-01 .. INV-W3E-05)
        string query = request.QueryText.Trim();
        if (query.Length > 2000)
        {
            query = query[..2000];
        }

        int topK = Math.Clamp(request.TopK, 1, 100);
        float alpha = Math.Clamp(request.HybridAlpha, 0.0f, 1.0f);
        float minThreshold = Math.Clamp(request.MinScoreThreshold, 0.0f, 1.0f);

        // Reject empty / whitespace / pure-punctuation queries without disk I/O (INV-W3E-05)
        if (string.IsNullOrWhiteSpace(query) || !query.Any(char.IsLetterOrDigit))
        {
            return new ScholarSearchResponse
            {
                Items = [],
                TotalCandidatesEvaluated = 0,
                Elapsed = Stopwatch.GetElapsedTime(startTimestamp),
                DegradationStatus = SearchDegradationStatus.ZeroResults,
                Warnings = []
            };
        }

        // Check neural capability state (INV-W3E-23)
        var capState = _embeddingEngine as IEmbeddingCapabilityStateProvider;
        bool isDenseAvailable = _embeddingEngine.ActiveProvider != EmbeddingExecutionProvider.LexicalHeuristic &&
                               (capState == null || capState.IsNeuralModelInstalled);
        float effectiveAlpha = isDenseAvailable ? alpha : 0.0f;
        var baseDegradation = isDenseAvailable
            ? SearchDegradationStatus.FullHybrid
            : SearchDegradationStatus.LexicalOnly_ModelMissing;

        // 2. Scope Resolution (INV-W3E-06 .. INV-W3E-09)
        var docIds = new List<string>();
        var warnings = new ConcurrentBag<SearchWarning>();

        switch (request.Scope.Kind)
        {
            case SearchScopeKind.AllDocuments:
                var allDocs = await _libraryService.GetAllDocumentsAsync(ct);
                docIds.AddRange(allDocs.Select(d => d.DocumentId));
                break;

            case SearchScopeKind.SessionDocuments:
                if (!string.IsNullOrWhiteSpace(request.Scope.TargetId))
                {
                    var session = await _libraryService.GetSessionAsync(request.Scope.TargetId, ct);
                    if (session != null && session.DocumentIds != null)
                    {
                        docIds.AddRange(session.DocumentIds);
                    }
                }
                break;

            case SearchScopeKind.SingleDocument:
                string singleId = request.Scope.TargetId ?? request.Scope.DocumentIds.FirstOrDefault() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(singleId))
                {
                    docIds.Add(singleId);
                }
                break;

            case SearchScopeKind.ExplicitDocuments:
                if (request.Scope.DocumentIds != null)
                {
                    docIds.AddRange(request.Scope.DocumentIds);
                }
                break;

            default:
                throw new ArgumentException($"Unsupported search scope kind: {request.Scope.Kind}. [Code: ERR_INVALID_SEARCH_SCOPE]");
        }

        docIds = docIds.Distinct(StringComparer.Ordinal).ToList();

        if (docIds.Count == 0)
        {
            return new ScholarSearchResponse
            {
                Items = [],
                TotalCandidatesEvaluated = 0,
                Elapsed = Stopwatch.GetElapsedTime(startTimestamp),
                DegradationStatus = SearchDegradationStatus.NoIndexedDocuments,
                Warnings = []
            };
        }

        // 3. Single-Document Parity Fast Path (INV-W3E-13)
        // When exactly one document is in scope and no page filter is active, reuse the authoritative W3-D path directly.
        if (docIds.Count == 1 && request.LocationScope == null)
        {
            string targetDocId = docIds[0];
            ct.ThrowIfCancellationRequested();

            var doc = await _libraryService.GetDocumentAsync(targetDocId, ct);
            if (doc == null)
            {
                warnings.Add(new SearchWarning
                {
                    DocumentId = targetDocId,
                    WarningCode = "WARN_DOCUMENT_NOT_FOUND",
                    Message = $"Document '{targetDocId}' was not found in library."
                });
                return new ScholarSearchResponse
                {
                    Items = [],
                    TotalCandidatesEvaluated = 0,
                    Elapsed = Stopwatch.GetElapsedTime(startTimestamp),
                    DegradationStatus = SearchDegradationStatus.ZeroResults,
                    Warnings = warnings.ToList()
                };
            }

            var valStatus = await GetOrValidateIndexStatusAsync(targetDocId, ct);
            if (valStatus == IndexValidationStatus.Missing)
            {
                warnings.Add(new SearchWarning
                {
                    DocumentId = targetDocId,
                    WarningCode = "WARN_INDEX_MISSING",
                    Message = $"Document '{targetDocId}' has no vector index."
                });
                return new ScholarSearchResponse
                {
                    Items = [],
                    TotalCandidatesEvaluated = 0,
                    Elapsed = Stopwatch.GetElapsedTime(startTimestamp),
                    DegradationStatus = SearchDegradationStatus.PartialResults_MissingIndexSkipped,
                    Warnings = warnings.ToList()
                };
            }

            if (valStatus == IndexValidationStatus.Corrupt_ChecksumMismatch || valStatus == IndexValidationStatus.Corrupt_TruncatedBinary)
            {
                warnings.Add(new SearchWarning
                {
                    DocumentId = targetDocId,
                    WarningCode = "WARN_INDEX_QUARANTINED",
                    Message = $"Index for document '{targetDocId}' was corrupted and quarantined ({valStatus})."
                });
                return new ScholarSearchResponse
                {
                    Items = [],
                    TotalCandidatesEvaluated = 0,
                    Elapsed = Stopwatch.GetElapsedTime(startTimestamp),
                    DegradationStatus = SearchDegradationStatus.PartialResults_CorruptedIndexSkipped,
                    Warnings = warnings.ToList()
                };
            }

            if (valStatus == IndexValidationStatus.Stale_ModelMismatch || valStatus == IndexValidationStatus.Stale_DocumentModified)
            {
                warnings.Add(new SearchWarning
                {
                    DocumentId = targetDocId,
                    WarningCode = "WARN_INDEX_STALE",
                    Message = $"Index for document '{targetDocId}' is stale ({valStatus})."
                });
            }

            var w3dResults = await _indexService.SearchHybridAsync(targetDocId, query, effectiveAlpha, topK, ct);
            var queryTokens = TokenizeTerms(query);
            var sourceStatus = doc.CheckSourceAvailability();

            var mappedItems = new List<ScholarSearchResultItem>(w3dResults.Count);
            int rank = 1;

            foreach (var r in w3dResults)
            {
                if (r.CombinedScore < minThreshold)
                    continue;

                float finalVectorSim = isDenseAvailable ? r.VectorSimilarity : 0.0f;
                float finalCombined = isDenseAvailable ? r.CombinedScore : r.LexicalScore;

                string snippet = !string.IsNullOrWhiteSpace(r.Snippet)
                    ? GenerateSnippet(r.Snippet, queryTokens)
                    : string.Empty;

                mappedItems.Add(new ScholarSearchResultItem
                {
                    Rank = rank++,
                    DocumentId = r.DocumentId,
                    DocumentTitle = doc.FileName,
                    WindowId = r.WindowId,
                    PageNumber = r.PageNumber,
                    FocalChunkIndex = r.FocalChunkIndex,
                    ConstituentChunkIndices = r.HydratedWindow?.ConstituentChunkIndices ?? [],
                    CombinedScore = finalCombined,
                    VectorSimilarity = finalVectorSim,
                    LexicalScore = r.LexicalScore,
                    FormattedSnippet = snippet,
                    Citations = r.Citations ?? [],
                    SourceStatus = sourceStatus,
                    HydratedWindow = request.IncludeHydratedWindows ? r.HydratedWindow : null
                });
            }

            var finalStatus = baseDegradation;
            if (warnings.Any(w => w.WarningCode == "WARN_INDEX_QUARANTINED"))
                finalStatus = SearchDegradationStatus.PartialResults_CorruptedIndexSkipped;
            else if (warnings.Any(w => w.WarningCode == "WARN_INDEX_MISSING"))
                finalStatus = SearchDegradationStatus.PartialResults_MissingIndexSkipped;
            else if (mappedItems.Count == 0)
                finalStatus = SearchDegradationStatus.ZeroResults;

            var response = new ScholarSearchResponse
            {
                Items = mappedItems,
                TotalCandidatesEvaluated = w3dResults.Count,
                Elapsed = Stopwatch.GetElapsedTime(startTimestamp),
                DegradationStatus = finalStatus,
                Warnings = warnings.ToList()
            };

            _logger.LogInformation(
                "Scholar single-doc search completed: Scope={ScopeKind}, DocId={DocId}, CandidatesEvaluated={CandidatesEvaluated}, ResultsReturned={ResultsReturned}, Degradation={Degradation}, ElapsedMs={ElapsedMs:F2}",
                request.Scope.Kind,
                targetDocId,
                w3dResults.Count,
                mappedItems.Count,
                finalStatus,
                response.Elapsed.TotalMilliseconds);

            return response;
        }

        // 4. Multi-Document Fan-Out & Candidate Selection (INV-W3E-10, INV-W3E-28, INV-W3E-29)
        float[]? queryVector = null;
        if (isDenseAvailable)
        {
            queryVector = await _embeddingEngine.GenerateEmbeddingAsync(query, ct);
        }

        var queryTerms = TokenizeTerms(query);
        var docCandidates = new ConcurrentBag<List<IntermediateCandidate>>();
        var docLookup = new ConcurrentDictionary<string, ScholarDocument>(StringComparer.Ordinal);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 8),
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(docIds, parallelOptions, async (docId, token) =>
        {
            token.ThrowIfCancellationRequested();

            var doc = await _libraryService.GetDocumentAsync(docId, token);
            if (doc == null)
            {
                warnings.Add(new SearchWarning
                {
                    DocumentId = docId,
                    WarningCode = "WARN_DOCUMENT_NOT_FOUND",
                    Message = $"Document '{docId}' was not found in library."
                });
                return;
            }
            docLookup[docId] = doc;

            var valStatus = await GetOrValidateIndexStatusAsync(docId, token);
            if (valStatus == IndexValidationStatus.Missing)
            {
                warnings.Add(new SearchWarning
                {
                    DocumentId = docId,
                    WarningCode = "WARN_INDEX_MISSING",
                    Message = $"Document '{docId}' has no vector index."
                });
                return;
            }

            if (valStatus == IndexValidationStatus.Corrupt_ChecksumMismatch || valStatus == IndexValidationStatus.Corrupt_TruncatedBinary)
            {
                warnings.Add(new SearchWarning
                {
                    DocumentId = docId,
                    WarningCode = "WARN_INDEX_QUARANTINED",
                    Message = $"Index for document '{docId}' was corrupted and quarantined ({valStatus})."
                });
                return;
            }

            if (valStatus == IndexValidationStatus.Stale_ModelMismatch || valStatus == IndexValidationStatus.Stale_DocumentModified)
            {
                warnings.Add(new SearchWarning
                {
                    DocumentId = docId,
                    WarningCode = "WARN_INDEX_STALE",
                    Message = $"Index for document '{docId}' is stale ({valStatus})."
                });
            }

            var index = await _indexService.GetIndexAsync(docId, token);

            if (index == null || index.Vectors == null || index.Vectors.Length == 0 || index.Manifest.Records == null || index.Manifest.Records.Count == 0)
            {
                return;
            }

            int recordCount = index.Manifest.Records.Count;

            // Step 1: Early Location Filtering (INV-W3E-07)
            var validRecordIndices = new List<int>();
            for (int i = 0; i < recordCount; i++)
            {
                var r = index.Manifest.Records[i];
                if (request.LocationScope == null || request.LocationScope.Matches(r.PageNumber))
                {
                    validRecordIndices.Add(i);
                }
            }

            if (validRecordIndices.Count == 0)
                return;

            // Step 2: Dense Candidate Selection (Top 250) (INV-W3E-10)
            var denseScores = new Dictionary<int, float>();
            if (isDenseAvailable && queryVector != null && index.Vectors != null)
            {
                foreach (int idx in validRecordIndices)
                {
                    token.ThrowIfCancellationRequested();
                    float dot = SimdVectorHelper.DotProduct(queryVector, index.Vectors[idx]);
                    float sVec = Math.Clamp(dot, 0.0f, 1.0f);
                    if (sVec > 0.0f)
                    {
                        denseScores[idx] = sVec;
                    }
                }
            }

            var topDense = denseScores
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .Take(250)
                .Select(kv => kv.Key)
                .ToHashSet();

            // Step 3: Lexical Candidate Selection (Top 250) (INV-W3E-10)
            var rawBm25Scores = new Dictionary<int, float>();
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
                            if (!validRecordIndices.Contains(docIdx))
                                continue;

                            int docLen = index.DocumentLengths[docIdx];
                            double numerator = tf * (k1 + 1.0);
                            double denominator = tf + k1 * (1.0 - b + b * (docLen / avgdl));
                            float score = (float)(idf * (numerator / denominator));

                            rawBm25Scores[docIdx] = rawBm25Scores.TryGetValue(docIdx, out float cur) ? cur + score : score;
                        }
                    }
                }
            }

            var topLexical = rawBm25Scores
                .Where(kv => kv.Value > 0.0f)
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .Take(250)
                .Select(kv => kv.Key)
                .ToHashSet();

            // Step 4: Deduplicated Union & Capacity Backfill up to 500 (INV-W3E-29)
            var merged = new HashSet<int>(topDense);
            merged.UnionWith(topLexical);

            if (merged.Count < 500)
            {
                var remaining = validRecordIndices
                    .Where(idx => !merged.Contains(idx))
                    .Where(idx => denseScores.ContainsKey(idx) || rawBm25Scores.ContainsKey(idx))
                    .OrderByDescending(idx =>
                    {
                        denseScores.TryGetValue(idx, out float d);
                        rawBm25Scores.TryGetValue(idx, out float l);
                        return Math.Max(d, l);
                    })
                    .ThenBy(idx => idx)
                    .Take(500 - merged.Count);

                foreach (var r in remaining)
                {
                    merged.Add(r);
                }
            }

            var candidatesForDoc = new List<IntermediateCandidate>(merged.Count);
            foreach (int idx in merged)
            {
                denseScores.TryGetValue(idx, out float vScore);
                rawBm25Scores.TryGetValue(idx, out float lRaw);
                var rec = index.Manifest.Records[idx];
                var win = (index.Windows != null && idx < index.Windows.Count) ? index.Windows[idx] : null;

                candidatesForDoc.Add(new IntermediateCandidate
                {
                    DocumentId = docId,
                    RecordIndex = idx,
                    WindowId = rec.WindowId,
                    PageNumber = rec.PageNumber,
                    FocalChunkIndex = rec.FocalChunkIndex,
                    ConstituentChunkIndices = rec.ConstituentChunkIndices ?? [],
                    VectorScore = isDenseAvailable ? vScore : 0.0f,
                    RawBm25Score = lRaw,
                    Record = rec,
                    HydratedWindow = win
                });
            }

            docCandidates.Add(candidatesForDoc);
        });

        // 5. Global Cross-Document Min-Max Normalization & Convex Fusion (INV-W3E-11, INV-W3E-12)
        var allCandidates = docCandidates.SelectMany(c => c).ToList();

        if (allCandidates.Count == 0)
        {
            var emptyStatus = baseDegradation;
            if (warnings.Any(w => w.WarningCode == "WARN_INDEX_QUARANTINED"))
                emptyStatus = SearchDegradationStatus.PartialResults_CorruptedIndexSkipped;
            else if (warnings.Any(w => w.WarningCode == "WARN_INDEX_MISSING"))
                emptyStatus = SearchDegradationStatus.PartialResults_MissingIndexSkipped;
            else
                emptyStatus = SearchDegradationStatus.ZeroResults;

            return new ScholarSearchResponse
            {
                Items = [],
                TotalCandidatesEvaluated = 0,
                Elapsed = Stopwatch.GetElapsedTime(startTimestamp),
                DegradationStatus = emptyStatus,
                Warnings = warnings.ToList()
            };
        }

        // Global Min-Max Lexical Normalization
        float minBm25 = float.MaxValue;
        float maxBm25 = float.MinValue;

        foreach (var c in allCandidates)
        {
            if (c.RawBm25Score < minBm25) minBm25 = c.RawBm25Score;
            if (c.RawBm25Score > maxBm25) maxBm25 = c.RawBm25Score;
        }

        bool singleItem = allCandidates.Count == 1;
        bool zeroRange = (maxBm25 - minBm25) < 1e-7f;

        foreach (var c in allCandidates)
        {
            float normLexical;
            if (singleItem)
            {
                normLexical = c.RawBm25Score > 0.0f ? 1.0f : 0.0f;
            }
            else if (zeroRange)
            {
                normLexical = maxBm25 > 0.0f ? 1.0f : 0.0f;
            }
            else
            {
                normLexical = Math.Clamp((c.RawBm25Score - minBm25) / (maxBm25 - minBm25), 0.0f, 1.0f);
            }

            c.LexicalScore = normLexical;
            float combined = (effectiveAlpha * c.VectorScore) + ((1.0f - effectiveAlpha) * normLexical);

            if (float.IsNaN(combined) || float.IsInfinity(combined)) combined = 0.0f;
            c.CombinedScore = combined;
        }

        // 6. Deterministic 7-Level Multi-Key Tie-Breaking & Top-K Slicing (INV-W3E-14)
        var sortedCandidates = allCandidates
            .Where(c => c.CombinedScore >= minThreshold)
            .OrderByDescending(c => c.CombinedScore)
            .ThenByDescending(c => c.VectorScore)
            .ThenByDescending(c => c.LexicalScore)
            .ThenBy(c => c.DocumentId, StringComparer.Ordinal)
            .ThenBy(c => c.PageNumber)
            .ThenBy(c => c.FocalChunkIndex)
            .ThenBy(c => c.WindowId, StringComparer.Ordinal)
            .Take(topK)
            .ToList();

        // 7. Snippet Generation, Citations & Source Checks (INV-W3E-15 .. INV-W3E-18)
        var finalItems = new List<ScholarSearchResultItem>(sortedCandidates.Count);
        int itemRank = 1;

        foreach (var c in sortedCandidates)
        {
            docLookup.TryGetValue(c.DocumentId, out var doc);
            string docTitle = doc?.FileName ?? c.DocumentId;
            var sourceStatus = doc?.CheckSourceAvailability() ?? SourceAvailabilityStatus.None;

            string fullText = c.HydratedWindow?.FormattedText ?? c.Record.FormattedText ?? string.Empty;
            string snippet = GenerateSnippet(fullText, queryTerms);
            var citations = c.HydratedWindow?.Citations ?? c.Record.Citations ?? [];

            finalItems.Add(new ScholarSearchResultItem
            {
                Rank = itemRank++,
                DocumentId = c.DocumentId,
                DocumentTitle = docTitle,
                WindowId = c.WindowId,
                PageNumber = c.PageNumber,
                FocalChunkIndex = c.FocalChunkIndex,
                ConstituentChunkIndices = c.ConstituentChunkIndices,
                CombinedScore = c.CombinedScore,
                VectorSimilarity = c.VectorScore,
                LexicalScore = c.LexicalScore,
                FormattedSnippet = snippet,
                Citations = citations,
                SourceStatus = sourceStatus,
                HydratedWindow = request.IncludeHydratedWindows ? c.HydratedWindow : null
            });
        }

        var degradation = baseDegradation;
        if (warnings.Any(w => w.WarningCode == "WARN_INDEX_QUARANTINED"))
            degradation = SearchDegradationStatus.PartialResults_CorruptedIndexSkipped;
        else if (warnings.Any(w => w.WarningCode == "WARN_INDEX_MISSING"))
            degradation = SearchDegradationStatus.PartialResults_MissingIndexSkipped;
        else if (finalItems.Count == 0)
            degradation = SearchDegradationStatus.ZeroResults;

        var finalResponse = new ScholarSearchResponse
        {
            Items = finalItems,
            TotalCandidatesEvaluated = allCandidates.Count,
            Elapsed = Stopwatch.GetElapsedTime(startTimestamp),
            DegradationStatus = degradation,
            Warnings = warnings.ToList()
        };

        _logger.LogInformation(
            "Scholar search completed: Scope={ScopeKind}, DocsCount={DocsCount}, CandidatesEvaluated={CandidatesEvaluated}, ResultsReturned={ResultsReturned}, Degradation={Degradation}, ElapsedMs={ElapsedMs:F2}",
            request.Scope.Kind,
            docIds.Count,
            allCandidates.Count,
            finalItems.Count,
            degradation,
            finalResponse.Elapsed.TotalMilliseconds);

        return finalResponse;
    }

    private static string GenerateSnippet(string text, IReadOnlyList<string> queryTerms)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        if (text.Length <= 240) return text.Trim();

        int bestIndex = -1;
        foreach (var term in queryTerms)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2) continue;
            int idx = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && (bestIndex < 0 || idx < bestIndex))
            {
                bestIndex = idx;
            }
        }

        if (bestIndex < 0)
        {
            int end = Math.Min(220, text.Length);
            while (end > 180 && end < text.Length && !char.IsWhiteSpace(text[end])) end--;
            return text[..end].Trim() + (end < text.Length ? "..." : "");
        }

        int start = Math.Max(0, bestIndex - 80);
        if (start > 0)
        {
            while (start < bestIndex && !char.IsWhiteSpace(text[start])) start++;
            if (char.IsWhiteSpace(text[start])) start++;
        }

        int snippetLen = Math.Min(220, text.Length - start);
        int snippetEnd = start + snippetLen;
        if (snippetEnd < text.Length)
        {
            while (snippetEnd > start + 180 && snippetEnd < text.Length && !char.IsWhiteSpace(text[snippetEnd])) snippetEnd--;
        }

        string result = text[start..Math.Min(snippetEnd, text.Length)].Trim();
        if (start > 0) result = "..." + result;
        if (snippetEnd < text.Length) result += "...";
        return result;
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

    private sealed class IntermediateCandidate
    {
        public string DocumentId { get; set; } = string.Empty;
        public int RecordIndex { get; set; }
        public string WindowId { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public int FocalChunkIndex { get; set; }
        public IReadOnlyList<int> ConstituentChunkIndices { get; set; } = [];
        public float VectorScore { get; set; }
        public float RawBm25Score { get; set; }
        public float LexicalScore { get; set; }
        public float CombinedScore { get; set; }
        public VectorIndexRecord Record { get; set; } = new();
        public BoundedContextWindow? HydratedWindow { get; set; }
    }
}
