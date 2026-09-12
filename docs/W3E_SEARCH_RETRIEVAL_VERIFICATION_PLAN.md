# Phase W3-E Verification Plan: Search / Retrieval Integration Stage

**Phase**: `W3-E — Search / Retrieval Integration Stage`  
**Status**: `PLANNING REVISED (PASS 2) / AWAITING IMPLEMENTATION AUTHORIZATION`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `96d8531560419eb9e9351575b3231aa27ab4a590` (`feat(winui): complete W3-D vector embedding and hybrid indexing`)  
**Preceding Closed Stages**: `W2-F`, `W3-B`, `W3-C.1..C.7.3`, `W3-D`  
**Downstream Dependents**: `DocumentChatService`, `ScholarKitViewModel`, `StudySynthesisEngine`

---

## 1. Test Architecture & Execution Taxonomy

The verification plan for Phase **W3-E** is structured to guarantee mathematically reproducible ranking, strict citation grounding, resilient degradation, and complete thread safety across all Scholar retrieval operations.

Tests are partitioned into two tiers:
- **Tier 1: In-Process Universal Tests (Mandatory CI Gate)**:
  - 100% automated, cross-platform, deterministic.
  - Executes against managed in-memory and disk-persisted vector indexes.
  - Zero GPU hardware dependency; zero external network access.
  - Validates all 30 formal invariants (`INV-W3E-01` .. `INV-W3E-30`).
- **Tier 2: Hardware-Gated & Real-Model Smoke Tests (Optional Local Gate)**:
  - Executes DirectML GPU retrieval and real ONNX weight inference when compatible DirectX 12 hardware and downloaded weights are available.
  - Gracefully skips in headless CI environments without failing the test runner.

---

## 2. Test Specification Matrix

The 35 canonical test specifications are cataloged below across seven functional groups:

### Group 1: Query Validation & Request Bounding (TEST-W3E-01 .. TEST-W3E-05)
- **TEST-W3E-01**: `SearchAsync_WithWhitespaceAndOversizedQuery_ClampsLengthAndTrims`
  - *Invariant*: `INV-W3E-01`
  - *Setup*: Query with 2,500 characters and leading/trailing whitespace.
  - *Assertions*: Clamped to 2,000 characters; whitespace trimmed; `ArgumentNullException` thrown on null query. (4 assertions)
- **TEST-W3E-02**: `SearchAsync_WithInvalidTopK_ClampsToValidRange`
  - *Invariant*: `INV-W3E-02`
  - *Setup*: Requests with $TopK = -5$, $TopK = 0$, $TopK = 150$.
  - *Assertions*: Clamped to 1, 1, and 100 respectively. (3 assertions)
- **TEST-W3E-03**: `SearchAsync_WithMinScoreThreshold_FiltersSubThresholdCandidates`
  - *Invariant*: `INV-W3E-03`
  - *Setup*: Search on populated index with $MinScoreThreshold = 0.65f$.
  - *Assertions*: All returned items satisfy $CombinedScore \ge 0.65f$; lower-scoring hits excluded. (4 assertions)
- **TEST-W3E-04**: `SearchAsync_WithInvalidAlpha_ClampsToUnitInterval`
  - *Invariant*: `INV-W3E-04`
  - *Setup*: Search requests with $lpha = -0.5f$ and $lpha = 1.8f$.
  - *Assertions*: $lpha$ clamped to $0.0f$ and $1.0f$; scores mathematically bounded. (4 assertions)
- **TEST-W3E-05**: `SearchAsync_WithEmptyOrPunctuationOnlyQuery_ReturnsEmptyZeroResultsShortCircuit`
  - *Invariant*: `INV-W3E-05`
  - *Setup*: Query texts: `""`, `"   "`, `",,,???"`.
  - *Assertions*: Returns `ScholarSearchResponse.Empty`; zero disk I/O; `DegradationStatus = ZeroResults`. (4 assertions)

### Group 2: Scope Resolution & Location Filtering (TEST-W3E-06 .. TEST-W3E-10)
- **TEST-W3E-06**: `SearchAsync_WithMultiDocumentSessionScope_ResolvesAllSessionDocuments`
  - *Invariant*: `INV-W3E-06`
  - *Setup*: `StudySession` with 3 enrolled documents.
  - *Assertions*: Hits retrieved across all 3 documents; candidate pool contains items from each. (4 assertions)
- **TEST-W3E-07**: `SearchAsync_WithLocationPageRange_FiltersEarlyBeforeScoring`
  - *Invariant*: `INV-W3E-07`
  - *Setup*: Multi-page document (pages 1–10) queried with $LocationFilter.StartPage = 3, EndPage = 5$.
  - *Assertions*: All returned items have $3 \le PageNumber \le 5$; non-matching pages bypassed prior to vector/lexical scoring. (4 assertions)
- **TEST-W3E-08**: `SearchAsync_WithSpecificPagesFilter_FiltersAccurately`
  - *Invariant*: `INV-W3E-07`
  - *Setup*: Query with $SpecificPages = [2, 7]$.
  - *Assertions*: All returned items reside exclusively on page 2 or page 7. (3 assertions)
- **TEST-W3E-09**: `SearchAsync_WithNonExistentDocumentId_RecordsWarningAndContinuesSearch`
  - *Invariant*: `INV-W3E-08`
  - *Setup*: Scope with 2 valid document IDs and 1 non-existent ID (`"doc_missing"`).
  - *Assertions*: Non-existent document emits `SearchWarning(doc_missing, WARN_DOCUMENT_NOT_FOUND)`; valid documents return results; no unhandled crash. (4 assertions)
- **TEST-W3E-10**: `SearchAsync_CandidatePool_ExecutesDeterministicTop250SelectionAndBackfill`
  - *Invariant*: `INV-W3E-10`
  - *Setup*: Document with 400 dense matches and 300 lexical matches.
  - *Assertions*: Top 250 dense and top 250 lexical selected; merged into deduplicated set; total pool bounded to $\le 500$. (4 assertions)

### Group 3: Hybrid Scoring, Normalization & Deterministic Ranking (TEST-W3E-11 .. TEST-W3E-16)
- **TEST-W3E-11**: `SearchAsync_CandidatePool_IncludesUnionOfDenseAndLexicalHits`
  - *Invariant*: `INV-W3E-10`
  - *Setup*: Windows where some match only dense vectors and others match only BM25 keywords.
  - *Assertions*: Both dense-only and lexical-only windows present in candidate pool; zero-scoring windows omitted. (4 assertions)
- **TEST-W3E-12**: `SearchAsync_GlobalMinMaxNormalization_NormalizesBM25AcrossMultiDocuments`
  - *Invariant*: `INV-W3E-11`
  - *Setup*: Query spanning Document A (high raw BM25) and Document B (moderate raw BM25).
  - *Assertions*: Normalized BM25 scores span $[0.0f, 1.0f]$ across the combined pool; relative document ranking preserved. (4 assertions)
- **TEST-W3E-13**: `SearchAsync_ConvexCombination_ComputesExactWeightedSum`
  - *Invariant*: `INV-W3E-12`
  - *Setup*: Known vector score $s_{	ext{vec}} = 0.80f$, normalized lexical score $s_{	ext{lex}} = 0.40f$, $lpha = 0.70f$.
  - *Assertions*: $CombinedScore = 0.70 	imes 0.80 + 0.30 	imes 0.40 = 0.680f \pm 10^{-5}$; bounded in $[0.0, 1.0]$. (3 assertions)
- **TEST-W3E-14**: `SearchAsync_SingleDocumentScope_MaintainsDeterministicParityWithW3D`
  - *Invariant*: `INV-W3E-13`
  - *Setup*: Execute `SearchAsync` on single document scope and compare with `ScholarIndexService.SearchHybridAsync` across standard regression test fixtures.
  - *Assertions*: Identical ranking order of WindowIds; scores equivalent within floating-point precision ($\Delta \le 10^{-6}$); candidate pool identity confirmed. (5 assertions)
- **TEST-W3E-15**: `SearchAsync_SevenLevelTieBreaker_OrdersDeterministicMultiKeyTies`
  - *Invariant*: `INV-W3E-14`
  - *Setup*: Multiple synthetic candidate items with identical $CombinedScore$, $VectorSimilarity$, and $LexicalScore$.
  - *Assertions*: Broken deterministically by DocumentId asc, PageNumber asc, FocalChunkIndex asc, WindowId asc. (6 assertions)
- **TEST-W3E-16**: `SearchAsync_RepeatedInvocations_GuaranteesDeterministicRankingReproducibility`
  - *Invariant*: `INV-W3E-14`
  - *Setup*: Run identical query 50 times across multi-document corpus.
  - *Assertions*: Every run returns identical ranking list, identical IDs, identical scores. (4 assertions)

### Group 4: Citation Grounding, Provenance & Source Safety (TEST-W3E-17 .. TEST-W3E-21)
- **TEST-W3E-17**: `SearchAsync_ResultItems_ContainAuthenticCitations`
  - *Invariant*: `INV-W3E-15`
  - *Setup*: Indexed document with known `BoundedContextWindow.Citations`.
  - *Assertions*: Every result item has non-null `Citations`; matching document ID and page number; zero synthetic citations. (4 assertions)
- **TEST-W3E-18**: `SearchAsync_MissingSourceFileOnDisk_FlagsStatusWithoutFailingSearch`
  - *Invariant*: `INV-W3E-16`
  - *Setup*: Source PDF deleted from disk after indexing was completed.
  - *Assertions*: Result item returned; `SourceStatus = SourceAvailabilityStatus.Missing`; formatted snippet intact; no exception thrown. (4 assertions)
- **TEST-W3E-19**: `SearchAsync_SnippetExtraction_CentersAroundMatchedQueryTerms`
  - *Invariant*: `INV-W3E-17`
  - *Setup*: Context window with 500 characters containing query term in middle.
  - *Assertions*: `FormattedSnippet` contains the query term; length $\le 280$ chars; bounded at word boundary. (4 assertions)
- **TEST-W3E-20**: `SearchAsync_RankAssignment_ProducesSequentialOneBasedRanks`
  - *Invariant*: `INV-W3E-18`
  - *Setup*: Request with $TopK = 10$.
  - *Assertions*: Returned items have ranks $1, 2, 3, \dots, 10$ consecutively without gaps. (3 assertions)
- **TEST-W3E-21**: `SearchAsync_HydratedWindows_IncludedOnlyWhenRequested`
  - *Invariant*: `INV-W3E-15`
  - *Setup*: Search with `IncludeHydratedWindows = false`, then `true`.
  - *Assertions*: `HydratedWindow` is null when false; populated with full window when true. (3 assertions)

### Group 5: Degradation, Corrupted Index & Quarantine Isolation (TEST-W3E-22 .. TEST-W3E-26)
- **TEST-W3E-22**: `SearchAsync_MissingDocumentIndex_SkipsDocumentAndEmitsWarning`
  - *Invariant*: `INV-W3E-19`
  - *Setup*: Multi-document query where Document 1 has index, Document 2 has no index files.
  - *Assertions*: Document 2 excluded; `SearchWarning` emitted; `DegradationStatus = PartialResults_MissingIndexSkipped`; Document 1 results returned. (4 assertions)
- **TEST-W3E-23**: `SearchAsync_EmptyIndex_YieldsZeroCandidatesWithoutDividingByZero`
  - *Invariant*: `INV-W3E-20`
  - *Setup*: Index with $TotalRecords = 0$ and empty inverted index.
  - *Assertions*: Evaluates cleanly; zero candidates; no division by zero; no exception. (3 assertions)
- **TEST-W3E-24**: `SearchAsync_CorruptedIndexChecksum_IsQuarantinedAndSkippedNonFatally`
  - *Invariant*: `INV-W3E-21`
  - *Setup*: Multi-document query where Document 2 has mutated byte in `vectors.bin`.
  - *Assertions*: Checksum mismatch caught; directory quarantined; Document 2 skipped with warning; Document 1 results returned successfully. (5 assertions)
- **TEST-W3E-25**: `SearchAsync_StaleModelFingerprint_EmitsWarningAndServesExistingVectors`
  - *Invariant*: `INV-W3E-22`
  - *Setup*: Index manifest created with obsolete `ModelFingerprint`.
  - *Assertions*: Stale index detected via `ValidateIndexAsync`; structured warning emitted; existing records searched; no crash. (4 assertions)
- **TEST-W3E-26**: `SearchAsync_NeuralModelMissing_EnforcesEffectiveAlphaZeroAndZeroVectorSimilarity`
  - *Invariant*: `INV-W3E-23`
  - *Setup*: Request with $lpha = 0.70f$ executed with embedding engine in `LexicalHeuristic` / uninstalled state.
  - *Assertions*: Effective alpha forced to $0.0f$; `VectorSimilarity` strictly $0.0f$; $CombinedScore == LexicalScore$; `DegradationStatus = LexicalOnly_ModelMissing`; never claims hybrid. (5 assertions)

### Group 6: Concurrency, Cancellation & Performance Bounds (TEST-W3E-27 .. TEST-W3E-31)
- **TEST-W3E-27**: `SearchAsync_ConcurrentQueries_ExecuteSimultaneouslyWithoutLocks`
  - *Invariant*: `INV-W3E-27`
  - *Setup*: 20 simultaneous `SearchAsync` tasks querying same multi-document corpus.
  - *Assertions*: All 20 tasks complete successfully; zero lock contention timeouts; zero data corruption. (4 assertions)
- **TEST-W3E-28**: `SearchAsync_MultiDocumentFanOut_RespectsMaxDegreeOfParallelism`
  - *Invariant*: `INV-W3E-28`
  - *Setup*: Scope with 20 documents.
  - *Assertions*: Concurrency bounded to $\min(	ext{ProcessorCount}, 8)$; no thread pool starvation. (3 assertions)
- **TEST-W3E-29**: `SearchAsync_CandidatePoolBounding_StrictlyClampsPoolAt500PerDoc`
  - *Invariant*: `INV-W3E-29`
  - *Setup*: Synthetic document with 1,500 matching windows.
  - *Assertions*: Evaluated candidate pool strictly clamped to $\le 500$ per document; memory remains bounded. (3 assertions)
- **TEST-W3E-30**: `SearchAsync_CancellationTokenTriggered_AbortsPromptly`
  - *Invariant*: `INV-W3E-30`
  - *Setup*: Pass pre-canceled or quickly canceled `CancellationToken` to `SearchAsync`.
  - *Assertions*: Throws `OperationCanceledException`; aborts in $< 50	ext{ ms}$; zero leaked unmanaged resources. (3 assertions)
- **TEST-W3E-31**: `SearchAsync_PerformanceBenchmark_MeasuresP50AndP95LatencyAcrossStandardFixtures`
  - *Invariant*: `INV-W3E-27`
  - *Setup*: Execute $N=20$ warm queries on Small (100 vectors) and Medium (5 docs, 2,000 vectors) fixtures. Exclude one-time model initialization; include snippet generation.
  - *Assertions*: Records p50 and p95; warm p95 $\le 150	ext{ ms}$ on Medium fixture; logs environment metadata. (4 assertions)

### Group 7: Privacy, Diagnostic Logging & Remote Guard (TEST-W3E-32 .. TEST-W3E-35)
- **TEST-W3E-32**: `SearchAsync_LocalExecution_MakesZeroNetworkSockets`
  - *Invariant*: `INV-W3E-24`
  - *Setup*: Wire mock network listener; execute local hybrid search.
  - *Assertions*: Zero TCP/UDP socket calls; zero HTTP requests; 100% offline. (3 assertions)
- **TEST-W3E-33**: `SearchAsync_DiagnosticLogging_ContainsZeroPrivateQueryOrSnippetText`
  - *Invariant*: `INV-W3E-25`
  - *Setup*: Search query `"SecretProjectX"`; inspect memory log sink.
  - *Assertions*: Query text and snippet content absent from logs; only operation names, elapsed ms, and counts logged. (4 assertions)
- **TEST-W3E-34**: `SearchAsync_ClassCRemoteProvider_RequiresModalPreviewAndConfirmation`
  - *Invariant*: `INV-W3E-26`
  - *Setup*: Remote search provider called without user confirmation.
  - *Assertions*: Throws `InvalidOperationException` with code `ERR_UNCONFIRMED_REMOTE_TRANSMISSION`. (3 assertions)
- **TEST-W3E-35**: `SearchAsync_ClassCRemoteProvider_ProceedsWhenConfirmed`
  - *Invariant*: `INV-W3E-26`
  - *Setup*: Remote preview generated and confirmed via `RemoteTransmissionGuard`.
  - *Assertions*: `ValidateTransmission` succeeds without exception. (3 assertions)

---

## 3. Tier Partitioning & Assertion Accounting

### Tier 1: In-Process Universal Tests
- **Coverage**: All 35 tests (`TEST-W3E-01` through `TEST-W3E-35`).
- **Dependencies**: .NET 9 x64 test host, temporary `%TEMP%\AxoraScholarTests\` directories, mock/in-memory embedding engines, real file I/O.
- **Assertion Count**:
  - Group 1: 19 assertions
  - Group 2: 18 assertions
  - Group 3: 26 assertions
  - Group 4: 18 assertions
  - Group 5: 23 assertions
  - Group 6: 17 assertions
  - Group 7: 13 assertions
  - **Total Planned Tier 1 Assertions**: **134 high-value, non-tautological assertions**.

### Tier 2: Hardware-Gated Optional Tests
- **Coverage**: Real DirectML GPU session queries using live DirectX 12 hardware and physical ONNX weight files.
- **Gating**: Dynamically gated via `DirectMlHardwareDetector.IsDirectMlSupported()`. Skipped automatically on CI runners without compatible GPUs.
