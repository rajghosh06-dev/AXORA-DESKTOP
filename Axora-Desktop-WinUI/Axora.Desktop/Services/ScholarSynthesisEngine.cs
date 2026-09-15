using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;
using Microsoft.Extensions.Logging;

namespace Axora.Desktop.Services;

/// <summary>
/// Primary study synthesis engine for local-first, privacy-respecting academic study workflows.
/// Implements IScholarSynthesisEngine using deterministic Class A extractive heuristics
/// and optional Class B local Small Language Models with six-layer grounding verification.
/// </summary>
public sealed class ScholarSynthesisEngine : IScholarSynthesisEngine
{
    private readonly IScholarSearchService _searchService;
    private readonly IScholarLibraryService? _libraryService;
    private readonly IScholarSlmModelDriver? _slmDriver;
    private readonly ILogger<ScholarSynthesisEngine>? _logger;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SessionLocks = new();

    private static readonly string[] BadgeColors = ["#5B7DE8", "#7C4DFF", "#00B0FF", "#00C853", "#FF9100"];

    public ScholarSynthesisEngine(
        IScholarSearchService searchService,
        IScholarLibraryService? libraryService = null,
        IScholarSlmModelDriver? slmDriver = null,
        ILogger<ScholarSynthesisEngine>? logger = null)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _libraryService = libraryService;
        _slmDriver = slmDriver;
        _logger = logger;
    }

    // ── Public Interface Methods ──────────────────────────────────────────────

    public async Task<ExecutiveSummaryResult> GenerateSummaryAsync(
        StudySynthesisRequest request,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        ct.ThrowIfCancellationRequested();

        var sanitized = SanitizeRequest(request, SynthesisWorkflowKind.ExecutiveSummary);

        // Scope check for empty session or empty library
        if (await IsScopeEmptyAsync(sanitized.Scope, ct))
        {
            return ExecutiveSummaryResult.Empty;
        }

        // Retrieve context windows via IScholarSearchService
        var retrieval = await RetrieveContextAsync(sanitized, ct);
        if (retrieval.PassingWindows.Count == 0)
        {
            return new ExecutiveSummaryResult
            {
                DegradationStatus = SynthesisDegradationStatus.ZeroResults_NoEvidence,
                Warnings = retrieval.Warnings,
                Elapsed = sw.Elapsed
            };
        }

        ct.ThrowIfCancellationRequested();

        // Cross-document conflict detection
        var conflicts = CrossDocumentConflictDetector.DetectConflicts(retrieval.PassingWindows);

        // Multi-engine execution
        var engineKind = ResolveEngineKind(sanitized.EnginePreference);
        var degradation = ResolveDegradation(retrieval.SearchDegradation, sanitized.EnginePreference);

        // Synthesize executive summary
        var (thesis, thesisCitation, keyPoints) = SynthesizeSummaryPoints(
            retrieval.PassingWindows,
            sanitized.TargetItemCount,
            conflicts);

        // Grounding verification
        var thesisStatus = SixLayerGroundingVerifier.VerifyClaim(thesis, thesisCitation, retrieval.PassingWindows, conflicts);
        var verifiedPoints = new List<GroundedSummaryPoint>();

        for (int i = 0; i < keyPoints.Count; i++)
        {
            var pt = keyPoints[i];
            var firstCit = pt.Citations.FirstOrDefault();
            var status = SixLayerGroundingVerifier.VerifyClaim(pt.Text, firstCit, retrieval.PassingWindows, conflicts);

            if (status != ItemGroundingStatus.Unsupported)
            {
                verifiedPoints.Add(new GroundedSummaryPoint
                {
                    PointNumber = verifiedPoints.Count + 1,
                    Text = pt.Text,
                    GroundingStatus = status,
                    Citations = SortCitationsDeterministically(pt.Citations)
                });
            }
        }

        // Collect all distinct citations across thesis and verified points
        var allCitations = new List<StudyCitation>();
        if (thesisCitation != null) allCitations.Add(thesisCitation);
        foreach (var pt in verifiedPoints) allCitations.AddRange(pt.Citations);
        var primaryCitations = SortCitationsDeterministically(allCitations.DistinctBy(c => $"{c.DocumentId}_{c.PageNumber}_{c.ChunkIndex}"));

        // Format Markdown
        var md = FormatSummaryMarkdown(thesis, thesisStatus, verifiedPoints, primaryCitations, conflicts);

        var result = new ExecutiveSummaryResult
        {
            CoreThesis = thesis,
            ThesisGroundingStatus = thesisStatus,
            KeyPoints = verifiedPoints,
            FormattedMarkdown = md,
            PrimaryCitations = primaryCitations,
            DetectedConflicts = conflicts,
            DegradationStatus = degradation,
            EngineUsed = engineKind,
            Warnings = retrieval.Warnings,
            Elapsed = sw.Elapsed
        };

        // Staged persistence if applicable
        await PersistSessionArtifactsAsync(sanitized, session =>
        {
            session.ExecutiveSummary = result.FormattedMarkdown;
        }, ct);

        LogSafeDiagnostic("Generated Executive Summary", verifiedPoints.Count, sw.ElapsedMilliseconds, degradation);
        return result;
    }

    public async Task<StudyConceptExtractionResult> ExtractConceptsAsync(
        StudySynthesisRequest request,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        ct.ThrowIfCancellationRequested();

        var sanitized = SanitizeRequest(request, SynthesisWorkflowKind.ConceptExtraction);

        if (await IsScopeEmptyAsync(sanitized.Scope, ct))
        {
            return StudyConceptExtractionResult.Empty;
        }

        var retrieval = await RetrieveContextAsync(sanitized, ct);
        if (retrieval.PassingWindows.Count == 0)
        {
            return new StudyConceptExtractionResult
            {
                DegradationStatus = SynthesisDegradationStatus.ZeroResults_NoEvidence,
                Warnings = retrieval.Warnings,
                Elapsed = sw.Elapsed
            };
        }

        ct.ThrowIfCancellationRequested();

        var conflicts = CrossDocumentConflictDetector.DetectConflicts(retrieval.PassingWindows);
        var engineKind = ResolveEngineKind(sanitized.EnginePreference);
        var degradation = ResolveDegradation(retrieval.SearchDegradation, sanitized.EnginePreference);

        var rawConcepts = ExtractRawConcepts(retrieval.PassingWindows, sanitized.TargetItemCount);
        var verifiedConcepts = new List<StudyConcept>();

        int colorIdx = 0;
        foreach (var c in rawConcepts)
        {
            var status = SixLayerGroundingVerifier.VerifyClaim(c.Definition, c.Citation, retrieval.PassingWindows, conflicts);
            if (status != ItemGroundingStatus.Unsupported)
            {
                c.GroundingStatus = status;
                c.BadgeColor = BadgeColors[colorIdx % BadgeColors.Length];
                colorIdx++;
                verifiedConcepts.Add(c);
            }
        }

        var result = new StudyConceptExtractionResult
        {
            Concepts = verifiedConcepts,
            DegradationStatus = degradation,
            EngineUsed = engineKind,
            Warnings = retrieval.Warnings,
            Elapsed = sw.Elapsed
        };

        // Persistence
        await PersistSessionArtifactsAsync(sanitized, session =>
        {
            if (sanitized.PersistenceMode == SynthesisPersistenceMode.ReplaceAll)
            {
                var preserved = sanitized.OverwriteUserModifiedItems
                    ? []
                    : session.Concepts.Where(item => item.IsUserModified).ToList();

                session.Concepts = preserved.Concat(verifiedConcepts).ToList();
            }
            else if (sanitized.PersistenceMode == SynthesisPersistenceMode.AppendNew)
            {
                var existingTerms = new HashSet<string>(session.Concepts.Select(item => item.Term.Trim().ToLowerInvariant()));
                foreach (var novel in verifiedConcepts)
                {
                    if (!existingTerms.Contains(novel.Term.Trim().ToLowerInvariant()))
                    {
                        session.Concepts.Add(novel);
                        existingTerms.Add(novel.Term.Trim().ToLowerInvariant());
                    }
                }
            }
        }, ct);

        LogSafeDiagnostic("Extracted Concepts", verifiedConcepts.Count, sw.ElapsedMilliseconds, degradation);
        return result;
    }

    public async Task<PracticeQuizGenerationResult> GenerateQuizAsync(
        StudySynthesisRequest request,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        ct.ThrowIfCancellationRequested();

        var sanitized = SanitizeRequest(request, SynthesisWorkflowKind.PracticeQuiz);

        if (await IsScopeEmptyAsync(sanitized.Scope, ct))
        {
            return PracticeQuizGenerationResult.Empty;
        }

        var retrieval = await RetrieveContextAsync(sanitized, ct);
        if (retrieval.PassingWindows.Count == 0)
        {
            return new PracticeQuizGenerationResult
            {
                DegradationStatus = SynthesisDegradationStatus.ZeroResults_NoEvidence,
                Warnings = retrieval.Warnings,
                Elapsed = sw.Elapsed
            };
        }

        ct.ThrowIfCancellationRequested();

        var conflicts = CrossDocumentConflictDetector.DetectConflicts(retrieval.PassingWindows);
        var engineKind = ResolveEngineKind(sanitized.EnginePreference);
        var degradation = ResolveDegradation(retrieval.SearchDegradation, sanitized.EnginePreference);

        var rawQuestions = GenerateRawQuizQuestions(retrieval.PassingWindows, sanitized.TargetItemCount);
        var verifiedQuestions = new List<PracticeQuizItem>();

        int qNum = 1;
        foreach (var q in rawQuestions)
        {
            var status = SixLayerGroundingVerifier.VerifyClaim(q.ExpectedAnswer, q.Citation, retrieval.PassingWindows, conflicts);
            if (status != ItemGroundingStatus.Unsupported)
            {
                q.QuestionNumber = qNum++;
                q.GroundingStatus = status;
                verifiedQuestions.Add(q);
            }
        }

        var result = new PracticeQuizGenerationResult
        {
            Questions = verifiedQuestions,
            DegradationStatus = degradation,
            EngineUsed = engineKind,
            Warnings = retrieval.Warnings,
            Elapsed = sw.Elapsed
        };

        // Persistence
        await PersistSessionArtifactsAsync(sanitized, session =>
        {
            if (sanitized.PersistenceMode == SynthesisPersistenceMode.ReplaceAll)
            {
                var preserved = sanitized.OverwriteUserModifiedItems
                    ? []
                    : session.QuizQuestions.Where(item => item.IsUserModified).ToList();

                session.QuizQuestions = preserved.Concat(verifiedQuestions).ToList();
            }
            else if (sanitized.PersistenceMode == SynthesisPersistenceMode.AppendNew)
            {
                var existingQuestions = new HashSet<string>(session.QuizQuestions.Select(item => item.QuestionText.Trim().ToLowerInvariant()));
                foreach (var novel in verifiedQuestions)
                {
                    if (!existingQuestions.Contains(novel.QuestionText.Trim().ToLowerInvariant()))
                    {
                        session.QuizQuestions.Add(novel);
                        existingQuestions.Add(novel.QuestionText.Trim().ToLowerInvariant());
                    }
                }
            }
        }, ct);

        LogSafeDiagnostic("Generated Practice Quiz", verifiedQuestions.Count, sw.ElapsedMilliseconds, degradation);
        return result;
    }

    public async Task<ComparativeSynthesisResult> CompareDocumentsAsync(
        StudySynthesisRequest request,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        ct.ThrowIfCancellationRequested();

        var sanitized = SanitizeRequest(request, SynthesisWorkflowKind.CrossDocumentComparison);

        if (await IsScopeEmptyAsync(sanitized.Scope, ct))
        {
            return ComparativeSynthesisResult.Empty;
        }

        var retrieval = await RetrieveContextAsync(sanitized, ct);
        if (retrieval.PassingWindows.Count == 0)
        {
            return new ComparativeSynthesisResult
            {
                DegradationStatus = SynthesisDegradationStatus.ZeroResults_NoEvidence,
                Warnings = retrieval.Warnings,
                Elapsed = sw.Elapsed
            };
        }

        ct.ThrowIfCancellationRequested();

        var conflicts = CrossDocumentConflictDetector.DetectConflicts(retrieval.PassingWindows);
        var engineKind = ResolveEngineKind(sanitized.EnginePreference);
        var degradation = ResolveDegradation(retrieval.SearchDegradation, sanitized.EnginePreference);

        var thematicPoints = ExtractThematicPoints(retrieval.PassingWindows, sanitized.TargetItemCount);
        var verifiedThematic = new List<ComparativePoint>();

        foreach (var tp in thematicPoints)
        {
            var firstCit = tp.SupportingCitations.FirstOrDefault();
            var status = SixLayerGroundingVerifier.VerifyClaim(tp.ConsensusStatement, firstCit, retrieval.PassingWindows, conflicts);
            if (status != ItemGroundingStatus.Unsupported)
            {
                tp.GroundingStatus = status;
                tp.SupportingCitations = SortCitationsDeterministically(tp.SupportingCitations);
                verifiedThematic.Add(tp);
            }
        }

        var md = FormatComparativeMarkdown(verifiedThematic, conflicts);

        var result = new ComparativeSynthesisResult
        {
            ThematicPoints = verifiedThematic,
            Discrepancies = conflicts,
            FormattedMarkdown = md,
            DegradationStatus = degradation,
            EngineUsed = engineKind,
            Warnings = retrieval.Warnings,
            Elapsed = sw.Elapsed
        };

        LogSafeDiagnostic("Compared Documents", verifiedThematic.Count, sw.ElapsedMilliseconds, degradation);
        return result;
    }

    public async Task<ComprehensiveSynthesisResult> SynthesizeAllAsync(
        StudySynthesisRequest request,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        ct.ThrowIfCancellationRequested();

        // 1. Generate Summary
        var summary = await GenerateSummaryAsync(request, ct);

        // 2. Extract Concepts
        var concepts = await ExtractConceptsAsync(request, ct);

        // 3. Generate Quiz
        var quiz = await GenerateQuizAsync(request, ct);

        // 4. Multi-doc Comparison (if scope contains > 1 document)
        ComparativeSynthesisResult? comparison = null;
        int docCount = await CountDocumentsInScopeAsync(request.Scope, ct);
        if (docCount > 1)
        {
            comparison = await CompareDocumentsAsync(request, ct);
        }

        var overallStatus = summary.DegradationStatus;
        if (concepts.DegradationStatus > overallStatus) overallStatus = concepts.DegradationStatus;
        if (quiz.DegradationStatus > overallStatus) overallStatus = quiz.DegradationStatus;

        return new ComprehensiveSynthesisResult
        {
            Summary = summary,
            Concepts = concepts,
            Quiz = quiz,
            Comparison = comparison,
            OverallStatus = overallStatus,
            TotalElapsed = sw.Elapsed
        };
    }

    public Task<SynthesisEngineStatus> GetCapabilityStatusAsync(CancellationToken ct = default)
    {
        var isSlmAvail = _slmDriver?.IsAvailable ?? false;
        var status = new SynthesisEngineStatus
        {
            IsClassAAvailable = true,
            IsClassBAvailable = isSlmAvail,
            ClassBModelName = _slmDriver?.ModelName ?? "Microsoft Phi-3-mini-4k-instruct (Uninstalled)",
            ClassBExecutionProvider = _slmDriver?.ExecutionProvider ?? "None",
            IsClassCAvailable = false,
            RecommendedEngine = isSlmAvail ? "ClassB_LocalSlm" : "ClassA_ExtractiveHeuristic"
        };
        return Task.FromResult(status);
    }

    // ── Validation & Sanitization (Stage 1) ───────────────────────────────────

    public static StudySynthesisRequest SanitizeRequest(StudySynthesisRequest request, SynthesisWorkflowKind workflow)
    {
        var sanitized = new StudySynthesisRequest
        {
            Scope = request.Scope ?? SearchScope.All(),
            LocationScope = request.LocationScope,
            PersistenceMode = request.PersistenceMode,
            OverwriteUserModifiedItems = request.OverwriteUserModifiedItems,
            EnginePreference = request.EnginePreference
        };

        // INV-W3F-01: Focus instruction bounding & sanitization
        if (!string.IsNullOrWhiteSpace(request.UserFocusInstruction))
        {
            var cleaned = CleanControlChars(request.UserFocusInstruction.Trim());
            if (cleaned.Length > 500)
            {
                cleaned = cleaned[..500];
            }
            sanitized.UserFocusInstruction = cleaned;
        }

        // INV-W3F-02: TargetItemCount clamping
        if (workflow == SynthesisWorkflowKind.ConceptExtraction)
        {
            sanitized.TargetItemCount = Math.Clamp(request.TargetItemCount, 1, 20);
        }
        else if (workflow == SynthesisWorkflowKind.PracticeQuiz)
        {
            sanitized.TargetItemCount = Math.Clamp(request.TargetItemCount, 1, 10);
        }
        else
        {
            sanitized.TargetItemCount = Math.Clamp(request.TargetItemCount, 1, 15);
        }

        // INV-W3F-03: MaxContextWindows clamping [1, 10]
        sanitized.MaxContextWindows = Math.Clamp(request.MaxContextWindows <= 0 ? 6 : request.MaxContextWindows, 1, 10);

        // INV-W3F-04: MinRelevanceThreshold clamping [0.0f, 1.0f]
        float thresh = request.MinRelevanceThreshold;
        if (float.IsNaN(thresh)) thresh = 0.25f;
        sanitized.MinRelevanceThreshold = Math.Clamp(thresh, 0.0f, 1.0f);

        return sanitized;
    }

    private static string CleanControlChars(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c >= ' ' || c == '\t' || c == '\n' || c == '\r')
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    // ── Retrieval Fan-Out (Stage 2) ───────────────────────────────────────────

    private sealed record RetrievalContextResult(
        IReadOnlyList<BoundedContextWindow> PassingWindows,
        SearchDegradationStatus SearchDegradation,
        IReadOnlyList<SynthesisWarning> Warnings);

    private async Task<RetrievalContextResult> RetrieveContextAsync(
        StudySynthesisRequest request,
        CancellationToken ct)
    {
        bool hasFocusInstruction = !string.IsNullOrWhiteSpace(request.UserFocusInstruction);
        string queryText;
        float minSearchThreshold;

        if (hasFocusInstruction)
        {
            // Untrusted prompt-injection defense (INV-W3F-05)
            // Structural isolation: focus text is isolated and treated strictly as search filter
            queryText = request.UserFocusInstruction!;
            minSearchThreshold = request.MinRelevanceThreshold;
        }
        else
        {
            // Broad synthesis across scoped documents
            queryText = await BuildBroadSynthesisQueryAsync(request.Scope, ct);
            minSearchThreshold = 0.0f;
        }

        var searchReq = new ScholarSearchRequest
        {
            QueryText = queryText,
            Scope = request.Scope,
            TopK = Math.Max(request.MaxContextWindows, 10),
            LocationScope = request.LocationScope,
            MinScoreThreshold = minSearchThreshold,
            IncludeHydratedWindows = true // INV-W3F-06
        };

        var searchResp = await _searchService.SearchAsync(searchReq, ct);
        var warnings = new List<SynthesisWarning>();

        // Forward search warnings (INV-W3F-09)
        foreach (var w in searchResp.Warnings)
        {
            warnings.Add(new SynthesisWarning
            {
                WarningCode = w.WarningCode,
                Message = w.Message,
                AffectedDocumentId = w.DocumentId
            });
        }

        // Collect passing hydrated context windows
        var passingWindows = new List<BoundedContextWindow>();
        int totalChars = 0;
        const int maxTotalChars = 16000; // INV-W3F-03 context budget

        float effectivePassingThreshold = hasFocusInstruction ? request.MinRelevanceThreshold : 0.0f;

        foreach (var item in searchResp.Items)
        {
            if (item.CombinedScore >= effectivePassingThreshold && item.HydratedWindow != null)
            {
                var win = item.HydratedWindow;
                if (totalChars + win.CharLength > maxTotalChars && passingWindows.Count > 0)
                {
                    break; // Budget reached
                }

                // Preserve item source status on window citations if missing (INV-W3F-27)
                if (item.SourceStatus == SourceAvailabilityStatus.Missing)
                {
                    foreach (var cit in win.Citations)
                    {
                        cit.SourceStatus = SourceAvailabilityStatus.Missing;
                    }
                }

                passingWindows.Add(win);
                totalChars += win.CharLength;
            }
        }

        // INV-W3F-10: Minimum evidence sufficiency check
        if (passingWindows.Count == 0)
        {
            warnings.Add(new SynthesisWarning
            {
                WarningCode = "WARN_INSUFFICIENT_EVIDENCE",
                Message = "No retrieved context windows met the minimum relevance threshold."
            });
        }

        return new RetrievalContextResult(passingWindows, searchResp.DegradationStatus, warnings);
    }

    private async Task<string> BuildBroadSynthesisQueryAsync(SearchScope scope, CancellationToken ct)
    {
        var docIds = new List<string>();
        switch (scope.Kind)
        {
            case SearchScopeKind.SingleDocument:
                string single = scope.TargetId ?? scope.DocumentIds.FirstOrDefault() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(single)) docIds.Add(single);
                break;
            case SearchScopeKind.ExplicitDocuments:
                if (scope.DocumentIds != null) docIds.AddRange(scope.DocumentIds);
                break;
            case SearchScopeKind.SessionDocuments:
                if (!string.IsNullOrWhiteSpace(scope.TargetId) && _libraryService != null)
                {
                    var session = await _libraryService.GetSessionAsync(scope.TargetId, ct);
                    if (session?.DocumentIds != null) docIds.AddRange(session.DocumentIds);
                }
                break;
            case SearchScopeKind.AllDocuments:
                if (_libraryService != null)
                {
                    var allDocs = await _libraryService.GetAllDocumentsAsync(ct);
                    docIds.AddRange(allDocs.Select(d => d.DocumentId));
                }
                break;
        }

        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_libraryService != null)
        {
            foreach (var docId in docIds)
            {
                var doc = await _libraryService.GetDocumentAsync(docId, ct);
                if (doc != null)
                {
                    if (!string.IsNullOrWhiteSpace(doc.Title))
                    {
                        foreach (var w in doc.Title.Split([' ', '_', '-', '.'], StringSplitOptions.RemoveEmptyEntries))
                            if (w.Length > 2) terms.Add(w.ToLowerInvariant());
                    }
                    if (!string.IsNullOrWhiteSpace(doc.FileName))
                    {
                        string fn = Path.GetFileNameWithoutExtension(doc.FileName);
                        foreach (var w in fn.Split([' ', '_', '-', '.'], StringSplitOptions.RemoveEmptyEntries))
                            if (w.Length > 2) terms.Add(w.ToLowerInvariant());
                    }
                }
                else
                {
                    foreach (var w in docId.Split([' ', '_', '-', '.'], StringSplitOptions.RemoveEmptyEntries))
                        if (w.Length > 2) terms.Add(w.ToLowerInvariant());
                }
            }
        }

        // Add universal terms indexed across English sentences
        terms.Add("the");
        terms.Add("is");
        terms.Add("in");
        terms.Add("of");
        terms.Add("and");
        terms.Add("that");
        terms.Add("to");

        return string.Join(" ", terms);
    }

    private async Task<bool> IsScopeEmptyAsync(SearchScope scope, CancellationToken ct)
    {
        if (scope.Kind == SearchScopeKind.SessionDocuments && !string.IsNullOrWhiteSpace(scope.TargetId) && _libraryService != null)
        {
            var session = await _libraryService.GetSessionAsync(scope.TargetId, ct);
            if (session != null && session.DocumentIds.Count == 0)
            {
                return true; // INV-W3F-08: empty session scope
            }
        }
        else if (scope.Kind == SearchScopeKind.ExplicitDocuments && scope.DocumentIds.Count == 0)
        {
            return true;
        }

        return false;
    }

    private async Task<int> CountDocumentsInScopeAsync(SearchScope scope, CancellationToken ct)
    {
        if (scope.Kind == SearchScopeKind.SingleDocument)
            return 1;
        if (scope.Kind == SearchScopeKind.ExplicitDocuments)
            return scope.DocumentIds.Count;
        if (scope.Kind == SearchScopeKind.SessionDocuments && !string.IsNullOrWhiteSpace(scope.TargetId) && _libraryService != null)
        {
            var session = await _libraryService.GetSessionAsync(scope.TargetId, ct);
            return session?.DocumentIds.Count ?? 0;
        }
        if (_libraryService != null)
        {
            var docs = await _libraryService.GetAllDocumentsAsync(ct);
            return docs.Count;
        }
        return 0;
    }

    // ── Class A Extractive Synthesizer (Stage 4A) ─────────────────────────────

    private static (string CoreThesis, StudyCitation? ThesisCitation, List<GroundedSummaryPoint> KeyPoints) SynthesizeSummaryPoints(
        IReadOnlyList<BoundedContextWindow> windows,
        int targetCount,
        IReadOnlyList<CrossDocumentConflict> conflicts)
    {
        var candidates = new List<(string Sentence, double Score, StudyCitation Citation)>();

        for (int wIdx = 0; wIdx < windows.Count; wIdx++)
        {
            var win = windows[wIdx];
            var sentences = SplitSentences(win.FormattedText);
            var citation = win.Citations.FirstOrDefault() ?? new StudyCitation
            {
                DocumentId = win.DocumentId,
                PageNumber = win.PageNumber,
                FileName = win.DocumentId
            };

            for (int sIdx = 0; sIdx < sentences.Count; sIdx++)
            {
                var s = sentences[sIdx];
                if (s.Length >= 25)
                {
                    double score = CalculateSentenceSalience(s, sIdx, sentences.Count);
                    candidates.Add((s, score, citation));
                }
            }
        }

        if (candidates.Count == 0)
        {
            return (string.Empty, null, []);
        }

        // Sort candidates by score descending
        var ordered = candidates.OrderByDescending(c => c.Score).ToList();

        // Highest scoring sentence as CoreThesis
        var (thesis, _, thesisCitation) = ordered[0];

        // Next N non-redundant sentences
        var keyPoints = new List<GroundedSummaryPoint>();
        var selectedSentences = new List<string> { thesis };

        for (int i = 1; i < ordered.Count && keyPoints.Count < targetCount; i++)
        {
            var (candidateText, _, candCitation) = ordered[i];

            // Redundancy check (pairwise Jaccard overlap <= 0.40)
            bool isRedundant = selectedSentences.Any(sel => CalculateJaccardOverlap(sel, candidateText) > 0.40);
            if (!isRedundant)
            {
                keyPoints.Add(new GroundedSummaryPoint
                {
                    PointNumber = keyPoints.Count + 1,
                    Text = candidateText,
                    GroundingStatus = ItemGroundingStatus.Grounded,
                    Citations = [candCitation]
                });
                selectedSentences.Add(candidateText);
            }
        }

        if (keyPoints.Count == 0 && ordered.Count > 0)
        {
            keyPoints.Add(new GroundedSummaryPoint
            {
                PointNumber = 1,
                Text = thesis,
                GroundingStatus = ItemGroundingStatus.Grounded,
                Citations = thesisCitation != null ? [thesisCitation] : []
            });
        }

        return (thesis, thesisCitation, keyPoints);
    }

    private static List<StudyConcept> ExtractRawConcepts(
        IReadOnlyList<BoundedContextWindow> windows,
        int targetCount)
    {
        var concepts = new List<StudyConcept>();
        var seenTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var defRegex1 = new Regex(@"^([A-Z][A-Za-z0-9\s\-]{2,35}):\s+(.+)", RegexOptions.Compiled);
        var defRegex2 = new Regex(@"^([A-Z][A-Za-z0-9\s\-]{2,35})\s+(?:is defined as|refers to|denotes|is described as|is called|means)\s+(.+)", RegexOptions.Compiled);
        var defRegex3 = new Regex(@"([A-Z][A-Za-z0-9\s\-]{2,35})\s+(?:states that|is a principle that|is a method for)\s+(.+)", RegexOptions.Compiled);

        foreach (var win in windows)
        {
            var citation = win.Citations.FirstOrDefault() ?? new StudyCitation
            {
                DocumentId = win.DocumentId,
                PageNumber = win.PageNumber,
                FileName = win.DocumentId
            };

            var sentences = SplitSentences(win.FormattedText);
            foreach (var sent in sentences)
            {
                if (concepts.Count >= targetCount) break;

                Match? m = null;
                string category = "Core Concept";

                if (defRegex1.IsMatch(sent))
                {
                    m = defRegex1.Match(sent);
                    category = "Definition";
                }
                else if (defRegex2.IsMatch(sent))
                {
                    m = defRegex2.Match(sent);
                    category = "Definition";
                }
                else if (defRegex3.IsMatch(sent))
                {
                    m = defRegex3.Match(sent);
                    category = "Methodology";
                }

                if (m != null && m.Success)
                {
                    string term = m.Groups[1].Value.Trim();
                    string def = m.Groups[2].Value.Trim();

                    if (def.Length > 300) def = def[..297] + "…";

                    if (!seenTerms.Contains(term) && term.Length >= 3 && def.Length >= 10)
                    {
                        concepts.Add(new StudyConcept
                        {
                            Term = term,
                            Definition = def,
                            Category = category,
                            Citation = citation,
                            GroundingStatus = ItemGroundingStatus.Grounded
                        });
                        seenTerms.Add(term);
                    }
                }
            }
        }

        // Fallback: extract prominent capitalized noun phrases if needed
        if (concepts.Count < targetCount)
        {
            foreach (var win in windows)
            {
                if (concepts.Count >= targetCount) break;

                var citation = win.Citations.FirstOrDefault() ?? new StudyCitation
                {
                    DocumentId = win.DocumentId,
                    PageNumber = win.PageNumber,
                    FileName = win.DocumentId
                };

                var sentences = SplitSentences(win.FormattedText);
                foreach (var sent in sentences)
                {
                    if (concepts.Count >= targetCount) break;
                    if (sent.Length < 35 || sent.Length > 200) continue;

                    var words = sent.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length >= 4 && char.IsUpper(words[0][0]))
                    {
                        string term = string.Join(" ", words.Take(Math.Min(words.Length, 3)));
                        if (!seenTerms.Contains(term) && term.Length >= 4)
                        {
                            concepts.Add(new StudyConcept
                            {
                                Term = term,
                                Definition = sent,
                                Category = "Core Concept",
                                Citation = citation,
                                GroundingStatus = ItemGroundingStatus.Grounded
                            });
                            seenTerms.Add(term);
                        }
                    }
                }
            }
        }

        return concepts;
    }

    private static List<PracticeQuizItem> GenerateRawQuizQuestions(
        IReadOnlyList<BoundedContextWindow> windows,
        int targetCount)
    {
        var questions = new List<PracticeQuizItem>();
        var concepts = ExtractRawConcepts(windows, targetCount);

        int qNum = 1;
        foreach (var c in concepts)
        {
            if (questions.Count >= targetCount) break;

            questions.Add(new PracticeQuizItem
            {
                QuestionNumber = qNum++,
                QuestionText = $"What is the primary definition and role of '{c.Term}'?",
                ExpectedAnswer = c.Definition,
                Difficulty = c.Definition.Length > 150 ? "Hard" : (c.Definition.Length > 70 ? "Medium" : "Easy"),
                Citation = c.Citation,
                GroundingStatus = ItemGroundingStatus.Grounded
            });
        }

        // If more needed, generate from causal and informative sentences
        if (questions.Count < targetCount)
        {
            foreach (var win in windows)
            {
                if (questions.Count >= targetCount) break;

                var citation = win.Citations.FirstOrDefault() ?? new StudyCitation
                {
                    DocumentId = win.DocumentId,
                    PageNumber = win.PageNumber,
                    FileName = win.DocumentId
                };

                var sentences = SplitSentences(win.FormattedText);
                foreach (var s in sentences)
                {
                    if (questions.Count >= targetCount) break;
                    if (s.Contains("causes", StringComparison.OrdinalIgnoreCase) ||
                        s.Contains("results in", StringComparison.OrdinalIgnoreCase) ||
                        s.Contains("leads to", StringComparison.OrdinalIgnoreCase))
                    {
                        var words = s.Split(' ');
                        string subject = string.Join(" ", words.Take(Math.Min(words.Length, 4)));

                        questions.Add(new PracticeQuizItem
                        {
                            QuestionNumber = qNum++,
                            QuestionText = $"Explain the underlying mechanism and consequence of '{subject}'.",
                            ExpectedAnswer = s,
                            Difficulty = "Medium",
                            Citation = citation,
                            GroundingStatus = ItemGroundingStatus.Grounded
                        });
                    }
                }
            }
        }

        return questions;
    }

    private static List<ComparativePoint> ExtractThematicPoints(
        IReadOnlyList<BoundedContextWindow> windows,
        int targetCount)
    {
        var points = new List<ComparativePoint>();
        var candidateThemes = new[] { "Methodology", "Theoretical Foundations", "Experimental Findings", "Core Conclusions", "Observations" };

        var sentences = windows.SelectMany(w => SplitSentences(w.FormattedText).Select(s => (Sentence: s, Window: w))).ToList();

        for (int i = 0; i < candidateThemes.Length && i < sentences.Count && points.Count < targetCount; i++)
        {
            var theme = candidateThemes[i];
            var matching = sentences[i];
            if (!string.IsNullOrEmpty(matching.Sentence))
            {
                var cit = matching.Window.Citations.FirstOrDefault() ?? new StudyCitation
                {
                    DocumentId = matching.Window.DocumentId,
                    PageNumber = matching.Window.PageNumber,
                    FileName = matching.Window.DocumentId
                };

                points.Add(new ComparativePoint
                {
                    Theme = theme,
                    ConsensusStatement = matching.Sentence,
                    SupportingCitations = [cit],
                    GroundingStatus = ItemGroundingStatus.Grounded
                });
            }
        }

        return points;
    }

    // ── Persistence Integration (Stage 6) ─────────────────────────────────────

    private async Task PersistSessionArtifactsAsync(
        StudySynthesisRequest request,
        Action<StudySession> mutateSession,
        CancellationToken ct)
    {
        if (request.PersistenceMode == SynthesisPersistenceMode.PreviewOnly_DoNotSave ||
            request.Scope.Kind != SearchScopeKind.SessionDocuments ||
            string.IsNullOrWhiteSpace(request.Scope.TargetId) ||
            _libraryService == null)
        {
            return;
        }

        string sessionId = request.Scope.TargetId;
        var sem = SessionLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1)); // INV-W3F-28

        await sem.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            var session = await _libraryService.GetSessionAsync(sessionId, ct);
            if (session != null)
            {
                mutateSession(session);
                session.LastAccessedAt = DateTime.UtcNow;
                await _libraryService.SaveSessionAsync(session, ct); // INV-W3F-21
            }
        }
        finally
        {
            sem.Release();
        }
    }

    // ── Citation Sorting & Formatting (INV-W3F-25) ────────────────────────────

    public static IReadOnlyList<StudyCitation> SortCitationsDeterministically(IEnumerable<StudyCitation> citations)
    {
        if (citations == null) return [];
        return citations
            .OrderBy(c => c.DocumentId, StringComparer.Ordinal)
            .ThenBy(c => c.PageNumber)
            .ThenBy(c => c.ChunkIndex)
            .ThenByDescending(c => c.SimilarityScore)
            .ToList();
    }

    private static string FormatSummaryMarkdown(
        string thesis,
        ItemGroundingStatus thesisStatus,
        IReadOnlyList<GroundedSummaryPoint> keyPoints,
        IReadOnlyList<StudyCitation> citations,
        IReadOnlyList<CrossDocumentConflict> conflicts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Executive Study Summary\n");

        if (!string.IsNullOrWhiteSpace(thesis))
        {
            sb.AppendLine("## Core Thesis");
            sb.AppendLine($"> {thesis}");
            sb.AppendLine($"*(Status: {thesisStatus})*\n");
        }

        if (keyPoints.Count > 0)
        {
            sb.AppendLine("## Key Findings & Takeaways");
            foreach (var pt in keyPoints)
            {
                string badge = pt.Citations.Count > 0 ? $" [{pt.Citations[0].FormattedBadge}]" : string.Empty;
                sb.AppendLine($"- {pt.Text}{badge}");
            }
            sb.AppendLine();
        }

        if (conflicts.Count > 0)
        {
            sb.AppendLine("## Detected Cross-Document Discrepancies");
            foreach (var c in conflicts)
            {
                sb.AppendLine($"- ⚠️ {c.FormattedDescription}");
            }
            sb.AppendLine();
        }

        if (citations.Count > 0)
        {
            sb.AppendLine("## References");
            foreach (var c in citations)
            {
                sb.AppendLine($"- {c.FormattedBadge}");
            }
        }

        return sb.ToString();
    }

    private static string FormatComparativeMarkdown(
        IReadOnlyList<ComparativePoint> thematicPoints,
        IReadOnlyList<CrossDocumentConflict> conflicts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Cross-Document Comparative Analysis\n");

        if (thematicPoints.Count > 0)
        {
            sb.AppendLine("## Thematic Consensus");
            foreach (var tp in thematicPoints)
            {
                string cit = tp.SupportingCitations.Count > 0 ? $" [{tp.SupportingCitations[0].FormattedBadge}]" : string.Empty;
                sb.AppendLine($"### {tp.Theme}");
                sb.AppendLine($"{tp.ConsensusStatement}{cit}\n");
            }
        }

        if (conflicts.Count > 0)
        {
            sb.AppendLine("## Factual Discrepancies & Contradictions");
            foreach (var c in conflicts)
            {
                sb.AppendLine($"- {c.FormattedDescription}");
            }
        }

        return sb.ToString();
    }

    // ── Diagnostic Logging & Helpers ──────────────────────────────────────────

    private static ActiveSynthesisEngineKind ResolveEngineKind(SynthesisEnginePreference pref)
    {
        return ActiveSynthesisEngineKind.ClassA_ExtractiveHeuristic;
    }

    private static SynthesisDegradationStatus ResolveDegradation(
        SearchDegradationStatus searchDegradation,
        SynthesisEnginePreference enginePref)
    {
        if (searchDegradation == SearchDegradationStatus.LexicalOnly_ModelMissing ||
            searchDegradation == SearchDegradationStatus.LexicalOnly_HardwareFallback)
        {
            return SynthesisDegradationStatus.Completed_LexicalGroundedOnly; // INV-W3F-19
        }

        if (enginePref == SynthesisEnginePreference.PreferClassB)
        {
            return SynthesisDegradationStatus.Completed_ExtractiveFallback; // INV-W3F-17
        }

        return SynthesisDegradationStatus.Completed_FullGrounding;
    }

    private void LogSafeDiagnostic(string operation, int count, long elapsedMs, SynthesisDegradationStatus status)
    {
        // INV-W3F-30: Strictly never log document text, summaries, concepts, or queries
        _logger?.LogInformation("ScholarSynthesisEngine: {Operation} produced {Count} items in {Elapsed}ms. Status: {Status}",
            operation, count, elapsedMs, status);
    }

    private static List<string> SplitSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        return text.Split(new[] { ". ", ".\n", ".\r\n", "!\n", "?\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 10)
            .ToList();
    }

    private static double CalculateSentenceSalience(string sentence, int positionIndex, int totalSentences)
    {
        int wordCount = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        double lengthWeight = Math.Min(1.0, wordCount / 15.0);
        double positionWeight = 1.0 + (0.2 / (1.0 + positionIndex));
        return lengthWeight * positionWeight;
    }

    private static double CalculateJaccardOverlap(string s1, string s2)
    {
        var words1 = new HashSet<string>(s1.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
        var words2 = new HashSet<string>(s2.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

        int intersection = words1.Count(w => words2.Contains(w));
        int union = words1.Count + words2.Count - intersection;
        return union == 0 ? 0.0 : (double)intersection / union;
    }
}
