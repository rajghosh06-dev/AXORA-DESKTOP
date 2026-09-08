# Phase W3-C.7 Stage C7.3 Rule Matrix: Bounded Context Window Formulation

**Phase**: `W3-C.7 — Passage Chunking & Bounded Context Window Formulation`  
**Stage**: `C7.3 — Bounded Context Window Formulation`  
**Status**: `PLANNING REMEDIATED (PASS 3) / AWAITING THIRD INDEPENDENT PLANNING AUDIT`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Focal Window Formulation Rules (`RULE-C73-FOC`)

| Rule ID | Name | Trigger Condition | Required Action | Verification / Guarantee |
| :--- | :--- | :--- | :--- | :--- |
| **`RULE-C73-FOC-01`** | **Single Chunk Window** | `PrecedingNeighborCount == 0 && SucceedingNeighborCount == 0` | Emit window containing strictly the focal chunk $C_k$. `FormattedText = focalChunk.Text`. | Window char length equals `focalChunk.CharLength`. |
| **`RULE-C73-FOC-02`** | **Preceding Neighbor Expansion** | `PrecedingNeighborCount > 0` | Include up to $N_p$ preceding chunks on the same page: $[\max(0, k - N_p), k]$. | Never includes chunks from prior page ($P-1$). |
| **`RULE-C73-FOC-03`** | **Succeeding Neighbor Expansion** | `SucceedingNeighborCount > 0` | Include up to $N_s$ succeeding chunks on the same page: $[k, \min(M-1, k + N_s)]$. | Never includes chunks from subsequent page ($P+1$). |
| **`RULE-C73-FOC-04`** | **Page Start Boundary Clamping** | Focal chunk is $C_0$ ($k = 0$) | Clamps preceding index to 0. Expands succeeding neighbors only within $[0, k_{\max}]$. | No negative chunk indices; zero exception. |
| **`RULE-C73-FOC-05`** | **Page End Boundary Clamping** | Focal chunk is $C_{M-1}$ (last chunk) | Clamps succeeding index to $M-1$. Expands preceding neighbors only within $[k_{\min}, M-1]$. | No out-of-range chunk indices; zero exception. |
| **`RULE-C73-FOC-06`** | **Isolated Single-Chunk Page** | Page contains exactly 1 chunk ($M = 1$) | Emits window containing $C_0$. Both preceding and succeeding expansions are no-ops ($k_{\min}=0, k_{\max}=0$). | `ConstituentChunks.Count == 1`. |

---

## 2. Overlap Deduplication & Tri-Modal Provenance Rules (`RULE-C73-DED`)

| Rule ID | Name | Trigger Condition | Required Action | Verification / Guarantee |
| :--- | :--- | :--- | :--- | :--- |
| **`RULE-C73-DED-01`** | **Continuous Span Extraction (Mode A)** | `DeduplicateOverlaps == true` and `page.NormalizedText != null` | Extract continuous span from `NormalizedText`: $[C_{k_{\min}}.\operatorname{StartCharOffset}, C_{k_{\max}}.\operatorname{EndCharOffset})$. | Zero duplicate text in stride overlap regions. |
| **`RULE-C73-DED-02`** | **Exact Substring Invariant (Mode A)** | Mode A focal window generated via `NormalizedText` | $P.\operatorname{NormalizedText}.\operatorname{Substring}(\operatorname{StartCharOffset}, \operatorname{CharLength}) == \operatorname{FormattedText}$. | Holds strictly for Mode A page-local focal windows. |
| **`RULE-C73-DED-03`** | **Fallback Concatenation (Mode B)** | `page.NormalizedText == null` | Concatenate constituent chunk texts with delimiter whitespace, trimming duplicate prefixes where detected. Set `StartCharOffset = -1`, `EndCharOffset = -1`. | Substring equality is formally waived; forward progress maintained. |
| **`RULE-C73-DED-04`** | **Delimiter Whitespace Preservation** | Inter-chunk whitespace in `NormalizedText` | Continuous span slicing preserves original inter-chunk formatting and paragraph breaks verbatim. | Clean natural reading flow. |

---

## 3. Budget, Target vs. Max & Truncation Rules (`RULE-C73-BDG`)

| Rule ID | Name | Trigger Condition | Required Action | Verification / Guarantee |
| :--- | :--- | :--- | :--- | :--- |
| **`RULE-C73-BDG-01`** | **Target Accumulation Boundary** | Candidate expansion in alternating sequence $(k-1), (k+1), \dots$ within $[k_{\min}, k_{\max}]$ | If continuous span $\le \text{TargetWindowChars}$, accept neighbor and continue alternating evaluation. | Target threshold respected; bounded strictly by $[k_{\min}, k_{\max}]$. |
| **`RULE-C73-BDG-02`** | **Max Ceiling Acceptance & Final Stop** | Adding candidate neighbor causes span to exceed `TargetWindowChars` but $\le \text{MaxWindowChars}$ | Accept neighbor chunk as **FINAL expansion**. Immediately halt expansion. | Target threshold satisfied; hard ceiling respected; immediate stop. |
| **`RULE-C73-BDG-03`** | **Max Ceiling Back-off & Final Stop** | Adding candidate neighbor causes span to exceed `MaxWindowChars` | Reject neighbor chunk. Immediately halt expansion. Never evaluate farther candidate neighbors. | Emitted window length never exceeds `MaxWindowChars`. |
| **`RULE-C73-BDG-04`** | **Focal Chunk Truncation Tiers** | Focal chunk alone exceeds `MaxWindowChars` | 3-tier fallback: (1) nearest sentence boundary $\le \text{MaxWindowChars}$, (2) nearest word boundary $\le \text{MaxWindowChars}$, (3) hard UTF-16-safe character slice at `MaxWindowChars` with surrogate protection. Set `IsTruncated = true`, `ConstituentChunks = [focalChunk]`. | Sentence integrity prioritized; surrogate-pair safe; length strictly enclosed. |
| **`RULE-C73-BDG-05`** | **Surrogate Pair Protection** | Truncation index falls between high (`\uD800..\uDBFF`) and low (`\uDC00..\uDFFF`) surrogates | Decrement split index by 1 to keep surrogate pair intact or omit severed high surrogate. | Emits valid UTF-16 code unit sequence. |

---

## 4. Composite Multi-Passage Prompt Packing Rules (`RULE-C73-CMP`)

| Rule ID | Name | Trigger Condition | Required Action | Verification / Guarantee |
| :--- | :--- | :--- | :--- | :--- |
| **`RULE-C73-CMP-01`** | **Ordering Policy Dispatch** | `options.OrderingMode` | `DocumentReadingOrder`: Sort by `DocumentId -> PageNumber -> ChunkIndex`. `PreserveInputOrder`: Keep caller sequence. | Predictable ordering for both reading flow and RAG rank. |
| **`RULE-C73-CMP-02`** | **Provenance Header Injection** | `IncludeProvenanceHeaders == true` | Prepend formatted banner before each passage: `\n\n--- [Page {P}, Passage {C}] ---\n`. | Transparent source attribution in LLM prompt. |
| **`RULE-C73-CMP-03`** | **Global Budget & Delimiter Enforcement** | Candidate passage accumulation | With headers, header + text counts. Without headers, `\n\n` delimiter counts. If candidate #1 exceeds budget: if available text budget $\ge 50$, truncate #1 via 3-tier fallback and set `IsTruncated = true`; if $< 50$, emit empty window with `IsTruncated = true`. For subsequent passages, halt accumulation immediately on budget breach with `IsTruncated = true`. | Emitted `FormattedText.Length <= CompositeBudgetChars` unconditionally. |
| **`RULE-C73-CMP-04`** | **Empty Candidates Handling** | Candidate chunk list is empty or all null | Return empty `BoundedContextWindow` with `FormattedText = string.Empty`, `WindowId = "comp_empty"`, `DocumentId = document?.DocumentId ?? "composite"`, and 0 citations. | Zero crash, clean empty result. |
| **`RULE-C73-CMP-05`** | **Multi-Document Deduplication** | Identical `(DocumentId, PageNumber, ChunkIndex)` passed multiple times | Keep first occurrence only; ignore subsequent duplicate candidate entries. Under `PreserveInputOrder`, earliest rank is preserved. | Zero duplicate passages; cross-document collisions prevented. |
| **`RULE-C73-CMP-06`** | **Composite DocumentId Resolution** | `FormulateCompositeWindow` invocation | If `document != null`: use `document.DocumentId`. If `document == null`: if all chunks have same `DocumentId`, use that ID; else use exactly `"composite"`. | Authoritative multi-doc identity preserved in chunks & citations. |

---

## 5. Provenance & Attribution Rules (`RULE-C73-PRV`)

| Rule ID | Name | Trigger Condition | Required Action | Verification / Guarantee |
| :--- | :--- | :--- | :--- | :--- |
| **`RULE-C73-PRV-01`** | **Constituent Index Preservation (Informational)** | Any formulated window | Populate `ConstituentChunkIndices` with ordered integers of all chunks in the window. Marked explicitly as informational only; not a globally unique provenance key. | Downstream UI convenience; authoritative mapping via `Citations`. |
| **`RULE-C73-PRV-02`** | **Focal Chunk Pointer** | `FormulateFocalWindow` invocation | Set `FocalChunk` property pointing to the target passage. | Downstream consumers know the primary match. |
| **`RULE-C73-PRV-03`** | **Structured Study Citations** | Any formulated window | Populate `Citations` list with a `StudyCitation` for each included chunk. Populate `FileName` from document or options. | UI citation badges link to exact page & passage. |
| **`RULE-C73-PRV-04`** | **Heuristic Token Estimation** | Window formulation | Set `EstimatedTokens = (int)Math.Ceiling(CharLength / 4.0)`. | Coarse planning metric; exact tokenizer output not claimed. |

---

## 6. Error & Failure Containment Rules (`RULE-C73-ERR`)

| Rule ID | Name | Trigger Condition | Required Action | Verification / Guarantee |
| :--- | :--- | :--- | :--- | :--- |
| **`RULE-C73-ERR-01`** | **Null Argument Validation** | `focalChunk == null` or `page == null` | Throw `ArgumentNullException` with parameter name. | Fail-fast on invalid programmatic contract use. |
| **`RULE-C73-ERR-02`** | **Orphan Focal Chunk** | `focalChunk` is not in `page.Chunks` | Treat `focalChunk` as a standalone single-chunk window without neighbor expansion (`ConstituentChunks = [focalChunk]`). | Safe graceful fallback without crashing. |
| **`RULE-C73-ERR-03`** | **Synchronous Execution Guarantee** | Method invocation | Execute synchronously in-memory without background thread dispatch or blocking locks. | Deterministic synchronous return with nominal sub-millisecond engineering target. |
| **`RULE-C73-ERR-04`** | **Privacy in Diagnostic Logs** | Exception thrown during internal processing | Log exception type only (`{ex.GetType().Name}`); never log document text or user query. | Telemetry privacy preserved. |

---

## 7. Security & Resource Bounds Rules (`RULE-C73-SEC`)

| Rule ID | Name | Trigger Condition | Required Action | Verification / Guarantee |
| :--- | :--- | :--- | :--- | :--- |
| **`RULE-C73-SEC-01`** | **Mandatory Method-Entry Options Validation** | Any public builder method invocation (`FormulateFocalWindow`, `FormulatePageWindows`, `FormulateCompositeWindow`) | Execute `(options ?? new ContextWindowOptions()).Validate()`. Enforce: `MaxWindowChars in [100, 10000]`, `TargetWindowChars in [50, MaxWindowChars]`, `PrecedingNeighborCount in [0, 10]`, `SucceedingNeighborCount in [0, 10]`, `CompositeBudgetChars in [200, 50000]`. | Throws `ArgumentOutOfRangeException` on invalid property; zero validation bypass. |
| **`RULE-C73-SEC-02`** | **Bounded Neighbor Count** | Neighbor counts specified | `PrecedingNeighborCount <= 10` and `SucceedingNeighborCount <= 10` guarantees constituent chunks $K \le 21$. | Prevents unbounded expansion and algorithmic blowup. |
| **`RULE-C73-SEC-03`** | **In-Memory Bounded Allocation** | Window string construction | Allocate exactly one `StringBuilder` sized to budget; zero intermediate string thrashing. | Low GC pressure during high-throughput search. |

---

## 8. Determinism & Concurrency Rules (`RULE-C73-DET`)

| Rule ID | Name | Trigger Condition | Required Action | Verification / Guarantee |
| :--- | :--- | :--- | :--- | :--- |
| **`RULE-C73-DET-01`** | **Stateless Execution** | Multiple invocations | `BoundedContextWindowBuilder` holds zero mutable instance state. | 100% thread-safe across concurrent queries. |
| **`RULE-C73-DET-02`** | **Repeat Run Determinism** | 50 repeated runs with identical inputs | Yield bit-for-bit identical `WindowId`, `FormattedText`, offsets, token estimates, and citation lists. | Deterministic testable behavior. |
| **`RULE-C73-DET-03`** | **Concurrent Reentrancy** | 20 threads executing simultaneously | Zero cross-thread interference or data corruption. | Reliable multi-core desktop performance. |
| **`RULE-C73-DET-04`** | **Cryptographically Collision-Resistant Composite WindowId** | `FormulateCompositeWindow` invocation | Hash canonical ordered coordinate string with full 64-hex SHA-256 digest: `$"comp_{SHA256(canonicalCoords)}"`. Empty emits `$"comp_empty"`. | Cryptographically collision-resistant via full 256-bit SHA-256 digest across distinct chunk sets. |

---

## 9. Page Window Formulation Rules (`RULE-C73-PAGE`)

| Rule ID | Name | Trigger Condition | Required Action | Verification / Guarantee |
| :--- | :--- | :--- | :--- | :--- |
| **`RULE-C73-PAGE-01`** | **Empty Page Window Handling** | `page.Chunks == null || page.Chunks.Count == 0` | Return empty list `[]` without error. | Safe graceful return on blank/empty pages. |
| **`RULE-C73-PAGE-02`** | **Sliding Sequence Formulation** | Page contains $M$ chunks ($M \ge 1$) | Emit strictly $M$ bounded focal windows sequentially centered on each chunk $C_k$ ($k = 0 \dots M-1$) with stride = 1, strictly respecting $[k_{\min}, k_{\max}]$ neighbor limits. | Complete passage coverage for dense vector search indexing. |
| **`RULE-C73-PAGE-03`** | **Page Window Identity & Anchor** | Window $k$ formulation | Set `window.FocalChunk = page.Chunks[k]`, `window.DocumentId = documentId`, `WindowId = $"win_{documentId}_p{page.PageNumber}_f{k}_c{minChunk}_{maxChunk}"` (or `_fb`). Scoped as spatial coordinate identity. | Stable unambiguous spatial window identification and anchor tracking. |
| **`RULE-C73-PAGE-04`** | **Oversized Page Chunk Clamping** | Any chunk $C_k$ on page exceeds `MaxWindowChars` | Truncate chunk along sentence/word/surrogate boundaries within `MaxWindowChars`. Set `IsTruncated = true`. | Adheres strictly to `RULE-C73-BDG-04`; never overflows max budget. |
