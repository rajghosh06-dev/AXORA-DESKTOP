# Phase W3-F Invariant & Rule Matrix: Study Synthesis Engine

**Phase**: `W3-F — Study Synthesis Engine`  
**Status**: `PLANNING HARDENED (PASS 2) / AWAITING IMPLEMENTATION AUTHORIZATION`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `a3dad9325f3fcf30ab4b71b088a2047e44a216ce` (`feat(winui): complete W3-E search and retrieval integration`)  
**Preceding Closed Stages**: `W2-F`, `W3-B`, `W3-C.1..C.7.3`, `W3-D`, `W3-E`  
**Downstream Dependents**: `ScholarKitViewModel`, `FlashcardsViewModel`, `DocumentChatService`

---

## 1. Overview & Invariant Taxonomy

This document establishes the 35 formal, deterministic invariants governing Phase **W3-F**. Every invariant defines an unambiguous trigger condition, expected behavior, failure code, and verification test mapping.

The invariants are organized into seven architectural categories:
1. **Request Validation & Input Boundaries** (`INV-W3F-01` .. `INV-W3F-05`)
2. **Scope Resolution & Retrieval Dependency** (`INV-W3F-06` .. `INV-W3F-09`)
3. **Six-Layer Grounding & Factual Verification** (`INV-W3F-10` .. `INV-W3F-15`)
4. **Engine Execution, Fallback & Model Contract** (`INV-W3F-16` .. `INV-W3F-20`)
5. **Persistence Safety & User Edit Preservation** (`INV-W3F-21` .. `INV-W3F-24`)
6. **Citation Ordering & Output Attribution** (`INV-W3F-25` .. `INV-W3F-27`)
7. **Concurrency, Cancellation & Privacy Integrity** (`INV-W3F-28` .. `INV-W3F-30`)

---

## 2. Invariant & Rule Matrix

| Invariant ID | Rule Name | Category | Trigger Condition | Expected Behavior | Edge Cases & Violations | Failure Code | Verification Test ID |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **INV-W3F-01** | Focus Instruction Bounding & Sanitization | Request | `UserFocusInstruction` provided | Trim whitespace; clamp length to $\le 500$ chars; strip control characters (`\0`, `\b`). | Length $> 500$ chars or unhandled null string. | `ERR_INVALID_FOCUS_INSTRUCTION` | `TEST-W3F-01` |
| **INV-W3F-02** | Target Item Count Clamping | Request | `TargetItemCount` specified | Clamp to $[1, 20]$ for concepts, $[1, 10]$ for quiz questions. Default is 5. | Count $\le 0$ or $> 20$ causing runaway loops. | `ERR_TARGET_COUNT_OUT_OF_RANGE` | `TEST-W3F-02` |
| **INV-W3F-03** | Context Window Budget Clamping | Request | `MaxContextWindows` specified | Clamp to $[1, 10]$. Total context characters capped at $\le 16,000$. Default is 6. | Excessive windows causing OOM or SLM context overflow. | `ERR_CONTEXT_BUDGET_EXCEEDED` | `TEST-W3F-03` |
| **INV-W3F-04** | Relevance Score Threshold Clamping | Request | `MinRelevanceThreshold` specified | Clamp threshold to $[0.0f, 1.0f]$. Default is $0.25f$. Exclude windows below threshold. | Negative threshold, NaN, or threshold $> 1.0f$. | `ERR_INVALID_RELEVANCE_THRESHOLD` | `TEST-W3F-04` |
| **INV-W3F-05** | Prompt Injection Structural Defense | Request | Focus instruction contains adversarial phrasing | Treat focus text as untrusted data; isolate in `<user_focus_topic>`; enforce schema and grounding verification. | System instructions hijacked or persona altered. | `WARN_PROMPT_INJECTION_CONTAINED` | `TEST-W3F-05`, `TEST-W3F-ADV-13` |
| **INV-W3F-06** | Retrieval Invocation Invariant | Retrieval | Synthesis operation triggered | Call `IScholarSearchService.SearchAsync` with `IncludeHydratedWindows = true`. Never bypass W3-E. | Reading binary indexes or disk files directly. | `ERR_RETRIEVAL_BYPASS_VIOLATION` | `TEST-W3F-06` |
| **INV-W3F-07** | Location Page Scope Enforcement | Retrieval | `LocationFilter` specified in request | Forward filter to `ScholarSearchRequest`. Exclude pages outside range prior to synthesis. | Generating summary points from unrequested pages. | `ERR_LOCATION_SCOPE_MISMATCH` | `TEST-W3F-07` |
| **INV-W3F-08** | Empty Scope Clean Zero Yield | Scope | Scope targets session with 0 docs or empty library | Return `ExecutiveSummaryResult.Empty` with `DegradationStatus = ZeroResults_NoEvidence` without throwing. | Throwing exception or crashing on empty scope. | `INFO_EMPTY_SCOPE_SHORT_CIRCUIT` | `TEST-W3F-08` |
| **INV-W3F-09** | Missing/Corrupted Index Non-Fatal Continuation | Scope | Scope contains corrupted or missing indexes | Skip corrupted indexes, record `SynthesisWarning("WARN_INDEX_QUARANTINED")`, synthesize from valid docs. | Entire synthesis aborts due to one unreadable document. | `WARN_INDEX_QUARANTINED` | `TEST-W3F-09` |
| **INV-W3F-10** | Minimum Evidence Sufficiency Threshold | Grounding | Retrieved passing windows $< 1$ | Set `DegradationStatus = ZeroResults_NoEvidence`, emit `WARN_INSUFFICIENT_EVIDENCE`, return empty collections. | Emitting ungrounded content when no evidence was retrieved. | `WARN_INSUFFICIENT_EVIDENCE` | `TEST-W3F-10` |
| **INV-W3F-11** | Strict Closed-Book Grounding | Grounding | Synthesis emitting factual statements | Claims must be verifiable against retrieved context. External pre-training facts are strictly forbidden. | Introducing outside dates, figures, or claims. | `ERR_CLOSED_BOOK_VIOLATION` | `TEST-W3F-11` |
| **INV-W3F-12** | Authentic Citation Provenance Binding | Grounding | Assembling `StudyConcept` or `PracticeQuizItem` | Every artifact must inherit an authentic `StudyCitation` from a retrieved `BoundedContextWindow`. | Emitting null or fabricated citation with invalid IDs. | `ERR_UNGROUNDED_CITATION` | `TEST-W3F-12`, `TEST-W3F-ADV-09` |
| **INV-W3F-13** | Numeric, Entity & Unit Consistency | Grounding | Validating generated claims against cited passage | Numbers, dates, percentages, units, and named entities in claim MUST match cited text exactly. | Number changed from 42 to 45, or unit changed from mg to g. | `ERR_ENTITY_NUMERIC_MISMATCH` | `TEST-W3F-ADV-01`..`03`, `06` |
| **INV-W3F-14** | Negation & Causal Direction Consistency | Grounding | Validating claim polarity and causal structure | Claim cannot introduce negation if source is affirmative; claim cannot reverse cause and effect. | Reversing causal direction or inverting truth polarity. | `ERR_POLARITY_CAUSALITY_VIOLATION` | `TEST-W3F-ADV-04`, `TEST-W3F-ADV-05` |
| **INV-W3F-15** | Cross-Document Discrepancy & Ambiguity Surfacing | Grounding | Multi-doc context has conflicting facts | Emit `CrossDocumentConflict`. If ambiguous, mark `IsAmbiguous = true` (potential discrepancy); never average numbers. | Masking contradiction by averaging or dropping a source. | `INFO_CROSS_DOCUMENT_CONFLICT_DETECTED` | `TEST-W3F-15`, `TEST-W3F-ADV-11`..`12` |
| **INV-W3F-16** | Class A Extractive Completeness | Capability | Base application installation (0 MB download) | Class A extractive engine must be 100% operational offline without external model weights. | Requiring network or missing models for basic synthesis. | `ERR_CLASS_A_UNAVAILABLE` | `TEST-W3F-18` |
| **INV-W3F-17** | Class B Dynamic Fallback to Class A | Capability | Class B model missing, disabled, or OOM | Automatically fall back to Class A; set `DegradationStatus = Completed_ExtractiveFallback`. | Crash or blocking dialog when optional model is absent. | `INFO_FALLBACK_TO_CLASS_A` | `TEST-W3F-19` |
| **INV-W3F-18** | Class B Deterministic Greedy Sampling | Capability | Executing local SLM inference | Enforce `temperature = 0.0f`, `top_p = 1.0f` to ensure reproducibility across runs. | Random outputs on identical input context. | `ERR_NON_DETERMINISTIC_SAMPLING` | `TEST-W3F-20` |
| **INV-W3F-19** | Lexical-Only Mode Degradation Tagging | Capability | W3-E retrieval ran in lexical-only mode | Set `DegradationStatus = Completed_LexicalGroundedOnly`; emit `WARN_LEXICAL_ONLY_RETRIEVAL`. | Presenting keyword-retrieved synthesis as dense semantic. | `WARN_LEXICAL_ONLY_RETRIEVAL` | `TEST-W3F-21` |
| **INV-W3F-20** | Class C Remote Guard Confirmation | Capability | User configures remote endpoint | Force `RemoteTransmissionGuard.ValidateTransmission()` modal confirmation. Zero silent network egress. | Data transmitted across network without user confirmation. | `ERR_UNAUTHORIZED_NETWORK_CALL` | `TEST-W3F-22` |
| **INV-W3F-21** | Staged Session Persistence | Persistence | `PersistenceMode != PreviewOnly` | Persist artifacts in `StudySession` via `IScholarLibraryService.SaveSessionAsync()`. Same-volume atomic move. | In-memory state lost on app restart. | `ERR_PERSISTENCE_FAILURE` | `TEST-W3F-24` |
| **INV-W3F-22** | User-Edited Item Preservation | Persistence | Regenerating existing session | Items marked `IsUserModified = true` MUST NOT be overwritten unless `OverwriteUserModifiedItems = true`. | User-authored notes or custom edits wiped out. | `ERR_USER_MODIFICATION_OVERWRITTEN` | `TEST-W3F-26` |
| **INV-W3F-23** | Append Mode Deduplication | Persistence | `PersistenceMode = AppendNew` | Deduplicate against existing items by `Term` or question similarity; append only novel items. | Creating duplicate concepts or identical questions. | `INFO_DUPLICATE_ITEM_SKIPPED` | `TEST-W3F-25` |
| **INV-W3F-24** | Source File Immutability | Persistence | Executing synthesis and saving session | User original source files (PDFs, images) MUST NOT be modified, renamed, or deleted. | Mutating user source documents. | `ERR_SOURCE_MUTATION_VIOLATION` | `TEST-W3F-27` |
| **INV-W3F-25** | Deterministic 4-Key Citation Sorting | Attribution | Assembling citation lists on artifacts | Sort citations by: 1. DocId $\uparrow$, 2. Page $\uparrow$, 3. Chunk $\uparrow$, 4. Score $\downarrow$. | Citation order varying across runs. | `ERR_NON_DETERMINISTIC_CITATION_SORT` | `TEST-W3F-28` |
| **INV-W3F-26** | Explicit Item Grounding Status Exposure | Attribution | Formatting synthesized items | Every item MUST expose `ItemGroundingStatus`. Only `Grounded` items shown as verified factual synthesis. | Presenting unverified or conflicting items as verified facts. | `ERR_UNVERIFIED_PRESENTATION_VIOLATION` | `TEST-W3F-ADV-07`, `TEST-W3F-ADV-08` |
| **INV-W3F-27** | Missing Source File Badge Decoration | Attribution | Source file missing on disk | Format badge as `{FileName} (Source file moved) · p. {PageNumber}`. | Crashing on missing file or hiding moved status. | `WARN_SOURCE_UNAVAILABLE` | `TEST-W3F-30` |
| **INV-W3F-28** | Session-Level Synthesis Concurrency Lock | Concurrency | Concurrent synthesis requests on same session | Serialize execution via session-level `SemaphoreSlim(1, 1)`. Concurrent calls queue safely. | Race condition corrupting session JSON. | `ERR_CONCURRENCY_VIOLATION` | `TEST-W3F-31` |
| **INV-W3F-29** | Cancellation Token Responsiveness | Concurrency | Cancellation requested during synthesis | Halt loop within $\le 50\text{ ms}$; throw `OperationCanceledException`; release memory buffers. | UI freeze or background task continuing after cancel. | `INFO_SYNTHESIS_CANCELLED` | `TEST-W3F-32` |
| **INV-W3F-30** | Privacy & Diagnostic Log Text Sanitization | Privacy | Writing diagnostic log messages | Disk logs MUST NOT record focus instructions, document text, summaries, concepts, or quiz text. | User notes or private textbook content leaked in logs. | `ERR_PRIVACY_LEAK_IN_LOGS` | `TEST-W3F-33` |
