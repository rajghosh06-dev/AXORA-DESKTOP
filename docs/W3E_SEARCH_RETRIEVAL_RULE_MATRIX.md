# Phase W3-E Invariant & Rule Matrix: Search / Retrieval Integration Stage

**Phase**: `W3-E — Search / Retrieval Integration Stage`  
**Status**: `PLANNING REVISED (PASS 2) / AWAITING IMPLEMENTATION AUTHORIZATION`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `96d8531560419eb9e9351575b3231aa27ab4a590` (`feat(winui): complete W3-D vector embedding and hybrid indexing`)  
**Preceding Closed Stages**: `W2-F`, `W3-B`, `W3-C.1..C.7.3`, `W3-D`  
**Downstream Dependents**: `DocumentChatService`, `ScholarKitViewModel`, `StudySynthesisEngine`

---

## 1. Overview & Invariant Taxonomy

This document establishes the 30 formal, deterministic invariants governing Phase **W3-E**. Every invariant is defined with its trigger condition, expected behavior, failure code, and verification test mapping.

The invariants are organized into seven architectural groups:
1. **Query Contract & Request Boundaries** (`INV-W3E-01` .. `INV-W3E-05`)
2. **Scope Resolution & Location Filtering** (`INV-W3E-06` .. `INV-W3E-09`)
3. **Hybrid Retrieval, Global Normalization & Ranking** (`INV-W3E-10` .. `INV-W3E-14`)
4. **Citation Grounding & Provenance Integrity** (`INV-W3E-15` .. `INV-W3E-18`)
5. **Index Lifecycle, Degradation & Quarantine Resiliency** (`INV-W3E-19` .. `INV-W3E-23`)
6. **Privacy, Diagnostic Safety & Remote Egress Guard** (`INV-W3E-24` .. `INV-W3E-26`)
7. **Concurrency, Cancellation & Resource Bounding** (`INV-W3E-27` .. `INV-W3E-30`)

---

## 2. Invariant & Rule Matrix

| Invariant ID | Rule Name | Category | Trigger Condition | Expected Behavior | Edge Cases & Violations | Failure Code | Verification Test ID |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **INV-W3E-01** | Query Text Bounding & Sanitization | Request | `SearchAsync` called with query text | Trim whitespace; clamp maximum query length to 2,000 characters. Reject null query with `ArgumentNullException`. | Query > 2,000 chars exceeding buffer or unhandled null string. | `ERR_INVALID_QUERY_TEXT` | `TEST-W3E-01` |
| **INV-W3E-02** | Top-K Parameter Clamping | Request | `TopK` parameter specified in request | Clamp `TopK` to integer range $[1, 100]$. Default to 10 if unspecified. | $TopK \le 0$ or $TopK > 100$ causing unbounded allocations. | `ERR_TOPK_OUT_OF_RANGE` | `TEST-W3E-02` |
| **INV-W3E-03** | Min-Score Threshold Clamping | Request | `MinScoreThreshold` parameter specified | Clamp threshold to $[0.0f, 1.0f]$. Filter out all candidates where $CombinedScore < threshold$. | Threshold $< 0.0f$ or $> 1.0f$ or NaN. | `ERR_INVALID_SCORE_THRESHOLD` | `TEST-W3E-03` |
| **INV-W3E-04** | Hybrid Alpha Weight Clamping & Forcing | Request | `HybridAlpha` parameter specified | Clamp $lpha$ to $[0.0f, 1.0f]$. If dense retrieval is unavailable, $lpha_{	ext{eff}}$ MUST be forced to $0.0f$. | Negative $lpha$ or claiming hybrid score when dense engine was unavailable. | `ERR_INVALID_HYBRID_ALPHA` | `TEST-W3E-04` |
| **INV-W3E-05** | Empty / Pure-Punctuation Query Short-Circuit | Request | Query produces zero alphanumeric tokens | Immediately return `ScholarSearchResponse.Empty` with `DegradationStatus = ZeroResults` without disk I/O. | Calling neural models or reading indexes for empty string or `???`. | `INFO_EMPTY_QUERY_SHORT_CIRCUIT` | `TEST-W3E-05` |
| **INV-W3E-06** | Scope Resolution Invariant | Scope | Resolving `SearchScope` | Correctly resolve `AllDocuments`, `SessionDocuments`, `SingleDocument`, or `ExplicitDocuments` to document ID list. | Malformed scope kind or null scope object. | `ERR_INVALID_SEARCH_SCOPE` | `TEST-W3E-06` |
| **INV-W3E-07** | Location Page Filtering | Scope | `LocationFilter` specified in request | Exclude any record whose `PageNumber` fails `Matches(page)` early before computing vector or lexical scores. | Missing page range checks or scoring records outside location filter. | `ERR_LOCATION_FILTER_MISMATCH` | `TEST-W3E-07` |
| **INV-W3E-08** | Non-Existent Document Scope Isolation | Scope | Scope contains a non-existent document ID | Record `SearchWarning(docId, "WARN_DOCUMENT_NOT_FOUND")` and continue searching remaining valid documents. | Aborting search across all documents when one ID is invalid. | `WARN_DOCUMENT_NOT_FOUND` | `TEST-W3E-08` |
| **INV-W3E-09** | Empty Session Scope Handling | Scope | Session has 0 enrolled documents | Return `ScholarSearchResponse.Empty` with `DegradationStatus = NoIndexedDocuments`. | Crashing or throwing `IndexOutOfRangeException` on empty session. | `INFO_EMPTY_SESSION_SCOPE` | `TEST-W3E-09` |
| **INV-W3E-10** | Candidate Pool Selection & Deduplication | Scoring | Formulating candidate hits for a document | Select top 250 dense hits ($s_{	ext{vec}} > 0$) and top 250 lexical hits ($s_{	ext{bm25}} > 0$); merge into deduplicated set; backfill up to 500. | Duplicate window IDs or unranked candidate accumulation. | `ERR_INVALID_CANDIDATE_POOL` | `TEST-W3E-10` |
| **INV-W3E-11** | Global Cross-Document Min-Max Normalization | Scoring | Aggregating candidates across multi-doc scope | Min-Max normalize raw BM25 scores across the combined global candidate pool into $[0.0f, 1.0f]$. | Using document-local BM25 ranges that distort cross-document relevance. | `ERR_SCORE_NORMALIZATION_FAILURE` | `TEST-W3E-11` |
| **INV-W3E-12** | Convex Combination Hybrid Scoring | Scoring | Computing combined score for candidate $c$ | $S_c = lpha_{	ext{eff}} \cdot s_{	ext{vec}, c} + (1 - lpha_{	ext{eff}}) \cdot s_{	ext{lex}, c}$. Protect against NaN / Infinity. | Score exceeding $1.0f$ or NaN propagating into rank list. | `ERR_HYBRID_FUSION_INVALID` | `TEST-W3E-12` |
| **INV-W3E-13** | Single-Document Parity Contract | Scoring | Scope contains exactly 1 document | Reuse authoritative W3-D scoring path directly; maintain deterministic ranking sequence verified via regression fixtures. | Unsupported absolute float equality claims or diverging ranking order. | `ERR_W3D_PARITY_VIOLATION` | `TEST-W3E-13` |
| **INV-W3E-14** | Deterministic 7-Level Multi-Key Tie-Breaking | Ranking | Ordering candidate hits | Sort by: 1. $S$ desc, 2. $s_{	ext{vec}}$ desc, 3. $s_{	ext{lex}}$ desc, 4. DocId asc, 5. Page asc, 6. Chunk asc, 7. WindowId asc. | Non-deterministic order on tied relevance scores across runs. | `ERR_NON_DETERMINISTIC_RANKING` | `TEST-W3E-14` |
| **INV-W3E-15** | Strict Citation Provenance Grounding | Citation | Assembling `ScholarSearchResultItem` | Citations MUST directly reference authentic `StudyCitation` instances from the window. Zero hallucination. | Generating synthetic citations or empty `DocumentId`/`PageNumber`. | `ERR_UNGROUNDED_CITATION` | `TEST-W3E-15` |
| **INV-W3E-16** | Source File Disk Availability Tracking | Citation | Preparing search result items | Inspect `ScholarDocument.CheckSourceAvailability()`. Flag `SourceStatus` as `Available`, `Missing`, or `Inaccessible`. | Throwing `FileNotFoundException` when source document was moved. | `WARN_SOURCE_UNAVAILABLE` | `TEST-W3E-16` |
| **INV-W3E-17** | Deterministic Snippet Extraction & Bounding | Citation | Generating `FormattedSnippet` | Center snippet around matched query term cluster; clamp length to $\le 280$ chars; bound at word boundaries. | Runaway snippet length or arbitrary truncation splitting words. | `ERR_SNIPPET_EXTRACTION_FAILURE` | `TEST-W3E-17` |
| **INV-W3E-18** | Sequential 1-Based Rank Assignment | Ranking | Constructing `ScholarSearchResponse.Items` | Items MUST have sequential 1-based ranks ($1, 2, \dots, K$) matching their deterministic sort position. | 0-based rank or gaps in rank numbering. | `ERR_INVALID_RANK_ASSIGNMENT` | `TEST-W3E-18` |
| **INV-W3E-19** | Missing Index Non-Fatal Degradation | Lifecycle | Document in scope has no persisted index | Record `SearchWarning(docId, "WARN_INDEX_MISSING")`, set `DegradationStatus`, and search remaining valid documents. | Halting multi-document retrieval due to one unindexed document. | `WARN_INDEX_MISSING` | `TEST-W3E-19` |
| **INV-W3E-20** | Empty Index Clean Zero Yield | Lifecycle | Index exists but contains 0 records | Safely yield 0 candidate hits without throwing `ArgumentException` or dividing by zero in BM25 avgdl. | Division by zero in BM25 average document length calculation. | `ERR_EMPTY_INDEX_CALCULATION` | `TEST-W3E-20` |
| **INV-W3E-21** | Corrupted Index Exclusion & Quarantine | Lifecycle | Index validation fails checksum | Catch quarantine status, record `SearchWarning(docId, "WARN_INDEX_QUARANTINED")`, and continue search. | Unhandled exception crash when reading corrupted binary index. | `WARN_INDEX_QUARANTINED` | `TEST-W3E-21` |
| **INV-W3E-22** | Stale Index Detection & Warning | Lifecycle | Index model fingerprint or document modified | Detect stale status via `ValidateIndexAsync`, record structured warning, and search existing vectors. | Silently dropping stale documents without user notification. | `WARN_INDEX_STALE` | `TEST-W3E-22` |
| **INV-W3E-23** | Explicit Lexical-Only Degradation Invariant | Capability | Neural model weights not installed or failed | Force $lpha_{	ext{eff}} = 0.0f$, set $VectorSimilarity = 0.0f$, and expose `DegradationStatus = LexicalOnly_*`. | Presenting search as hybrid or reporting non-zero vector score when dense engine was unavailable. | `ERR_INVALID_DEGRADATION_STATE` | `TEST-W3E-23` |
| **INV-W3E-24** | Zero External Network Sockets (Class A/B) | Privacy | Executing local retrieval requests | Retrieval operations MUST NOT create sockets or make HTTP/TCP calls. 100% offline execution. | Unintended telemetry or external API calls during search. | `ERR_UNAUTHORIZED_NETWORK_CALL` | `TEST-W3E-24` |
| **INV-W3E-25** | Diagnostic Log Text Sanitization | Privacy | Writing log entries | Log records MUST NOT include user query text, snippet text, extracted page content, or study notes. | Logging user search queries or document passages to disk logs. | `ERR_PRIVACY_LEAK_IN_LOGS` | `TEST-W3E-25` |
| **INV-W3E-26** | Class C Outbound Transmission Guard | Privacy | Remote search provider requested | Enforce `RemoteTransmissionGuard.ValidateTransmission()` requiring modal preview and explicit user confirmation. | Transmitting search queries to remote endpoint without confirmation. | `ERR_UNCONFIRMED_REMOTE_TRANSMISSION` | `TEST-W3E-26` |
| **INV-W3E-27** | Lock-Free Concurrent Search Queries | Concurrency | Multiple simultaneous query tasks | Searching vector indexes MUST NOT acquire blocking file locks or write locks. Concurrent readers execute safely. | Thread contention or UI thread freeze during background search. | `ERR_READER_LOCK_CONTENTION` | `TEST-W3E-27` |
| **INV-W3E-28** | Bounded Multi-Document Parallel Fan-Out | Concurrency | Searching multi-document collection | Degree of parallelism MUST be bounded to $\min(	ext{ProcessorCount}, 8)$. Zero unbounded thread creation. | Thread pool starvation from unbounded task spawning. | `ERR_UNBOUNDED_CONCURRENCY` | `TEST-W3E-28` |
| **INV-W3E-29** | Per-Document Candidate Pool Bound (<= 500) | Resources | Forming candidate list for document | Strictly enforce $|\mathcal{C}_{	ext{doc}}| \le 500$ via deterministic top-250 cuts and priority backfill. | Accumulating unbounded candidates causing memory pressure. | `ERR_CANDIDATE_POOL_EXCEEDED` | `TEST-W3E-29` |
| **INV-W3E-30** | Responsive Cooperative Cancellation | Concurrency | Cancellation token triggered during search | Query execution MUST check `ct.ThrowIfCancellationRequested()` at document and candidate processing boundaries. | Hanging background query task ignoring user cancellation. | `ERR_SEARCH_CANCELLED` | `TEST-W3E-30` |

---

## 3. Normative Enforcement Principles

1. **Deterministic Reproducibility**: Given an identical index state, query string, and search request parameters, `SearchAsync` MUST return identical ranking positions on every invocation.
2. **Strict Grounding Guarantee**: No `ScholarSearchResultItem` may ever be produced without authentic provenance anchored in an existing `StudyCitation`. Hallucinated or synthetic citations are an absolute violation.
3. **Graceful Partial Degradation**: A failure in one document's index (whether missing, stale, or corrupted) must NEVER fail the entire search operation across other valid documents.
4. **Transparent Degradation Honesty**: In lexical-only mode, $lpha_{	ext{eff}} = 0.0f$, $VectorSimilarity = 0.0f$, and the response is explicitly labeled as lexical-only; no hybrid appearance is permitted.
5. **Local-First Privacy Invariant**: All local searches (Classes A and B) execute completely offline with zero external network egress and zero private user text written to diagnostic logs.
