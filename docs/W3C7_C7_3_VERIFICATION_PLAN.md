# Phase W3-C.7 Stage C7.3 Verification Plan: Bounded Context Window Formulation

**Phase**: `W3-C.7 — Passage Chunking & Bounded Context Window Formulation`  
**Stage**: `C7.3 — Bounded Context Window Formulation`  
**Status**: `PLANNING REMEDIATED (PASS 3) / AWAITING THIRD INDEPENDENT PLANNING AUDIT`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Overview & Verification Strategy

Stage **W3-C.7.3** establishes the **Bounded Context Window Formulation** engine (`IBoundedContextWindowBuilder` $\to$ `BoundedContextWindowBuilder`).

The verification suite for C7.3 is designed to mathematically prove:
1. Exact substring provenance against page `NormalizedText` for Mode A focal windows.
2. Unmapped offset semantics (`-1`) for Mode B fallback and Mode C composite windows.
3. 100% elimination of stride overlap duplication (zero text stutter).
4. Inviolable budget compliance under all neighbor expansion scenarios with Target vs. Max decision logic.
5. Strict enforcement of all resource ceiling bounds in `ContextWindowOptions.Validate()`.
6. Correct page boundary clamping at page extremities.
7. Deterministic multi-page prompt packing with both `DocumentReadingOrder` and `PreserveInputOrder`.
8. Cross-document candidate deduplication via `(DocumentId, PageNumber, ChunkIndex)`.
9. Bit-for-bit repeatability including deterministic `WindowId` across 50 repeated runs.
10. Thread safety and concurrent reentrancy across 20 parallel threads.

---

## 2. Test Matrix: Categories A through M (49 Planned Assertions)

| Category | Description | Target Component | Governing Rules |
| :--- | :--- | :--- | :--- |
| **Cat A** | **Focal Window Single Chunk (Radius 0)** | `FormulateFocalWindow` | `RULE-C73-FOC-01`, `RULE-C73-PRV-01` |
| **Cat B** | **Preceding & Succeeding Neighbor Expansion** | `FormulateFocalWindow` | `RULE-C73-FOC-02`, `RULE-C73-FOC-03` |
| **Cat C** | **Stride Overlap Deduplication** | `OverlapDeduplicator` | `RULE-C73-DED-01`, `RULE-C73-DED-02` |
| **Cat D** | **Page Boundary Clamping (Extremities)** | `FocalWindowFormulator` | `RULE-C73-FOC-04`, `RULE-C73-FOC-05`, `06` |
| **Cat E** | **Tri-Modal Provenance Semantics** | `FormulateFocalWindow`, `FormulateCompositeWindow` | `RULE-C73-DED-02`, `03`, Invariant 2 |
| **Cat F** | **Budget, Target vs. Max & Truncation** | `BudgetClamper` | `RULE-C73-BDG-01` through `05` |
| **Cat G** | **Composite Prompt Packing & Ordering** | `FormulateCompositeWindow` | `RULE-C73-CMP-01` through `06` |
| **Cat H** | **Structured Provenance Citations** | `ProvenanceMapper` | `RULE-C73-PRV-01` through `04` |
| **Cat I** | **Determinism & Stable WindowId** | `BoundedContextWindowBuilder` | `RULE-C73-DET-02`, `RULE-C73-DET-04` |
| **Cat J** | **Multi-Threaded Concurrency (20 Threads)** | `BoundedContextWindowBuilder` | `RULE-C73-DET-01`, `RULE-C73-DET-03` |
| **Cat K** | **Adversarial Edge Cases & Security Bounds** | `ContextWindowOptions.Validate`, Builder | `RULE-C73-ERR-01` through `04`, `RULE-C73-SEC-01`, `02` |
| **Cat L** | **Page Window Formulation (`FormulatePageWindows`)** | `FormulatePageWindows` | `RULE-C73-PAGE-01` through `04` |
| **Cat M** | **Downstream Consumer Simulation** | `DocumentChatService` Adapter | End-to-End RAG flow simulation |

---

## 3. Detailed Executable Test Specifications

### Category A: Focal Window Single Chunk (Radius 0)
- **`W3C7_3_A1_RadiusZeroYieldsFocalText`**: Call `FormulateFocalWindow` with `PrecedingNeighborCount = 0` and `SucceedingNeighborCount = 0`. Assert `window.FormattedText == focalChunk.Text`.
- **`W3C7_3_A2_RadiusZeroOffsetsMatch`**: Assert `window.StartCharOffset == focalChunk.StartCharOffset` and `window.EndCharOffset == focalChunk.EndCharOffset`.
- **`W3C7_3_A3_RadiusZeroConstituentIndex`**: Assert `window.ConstituentChunkIndices` contains exactly `[focalChunk.ChunkIndex]`.
- **`W3C7_3_A4_RadiusZeroFocalPointer`**: Assert `window.FocalChunk` reference equals `focalChunk`.

### Category B: Preceding & Succeeding Neighbor Expansion
- **`W3C7_3_B1_ExpandsBothNeighbors`**: On a 5-chunk page, select chunk index 2 with radius 1 preceding and 1 succeeding. Assert `ConstituentChunkIndices` is `[1, 2, 3]`.
- **`W3C7_3_B2_ExpandsMultipleNeighbors`**: Select chunk index 2 with radius 2 preceding and 2 succeeding. Assert `ConstituentChunkIndices` is `[0, 1, 2, 3, 4]`.
- **`W3C7_3_B3_StartOffsetMatchesFirstConstituent`**: Assert `window.StartCharOffset == page.Chunks[1].StartCharOffset`.
- **`W3C7_3_B4_EndOffsetMatchesLastConstituent`**: Assert `window.EndCharOffset == page.Chunks[3].EndCharOffset`.

### Category C: Stride Overlap Deduplication
- **`W3C7_3_C1_DeduplicatedWindowLacksStutter`**:
  - Ingest text generated with `StrideOverlapChars = 60` where chunk $k$ and chunk $k+1$ share overlapping words.
  - Assert that any word occurring once in that span of `NormalizedText` occurs **exactly once** in `window.FormattedText`.
  - Contrast with naive concatenation: assert that `chunk0.Text + " " + chunk1.Text` contains duplicated text, while `window.FormattedText` does not.
- **`W3C7_3_C2_DeduplicatedLengthShorterThanSum`**:
  - Assert `window.CharLength < chunk0.CharLength + chunk1.CharLength` when overlap is present.
  - Assert `window.CharLength == (chunk0.CharLength + chunk1.CharLength) - actualOverlapLength`.

### Category D: Page Boundary Clamping (Extremities)
- **`W3C7_3_D1_FirstChunkClampsPreceding`**: Focal chunk is index 0 with `PrecedingNeighborCount = 2`. Assert preceding expansion clamps to 0; `ConstituentChunkIndices` starts at 0 with no negative indices.
- **`W3C7_3_D2_LastChunkClampsSucceeding`**: Focal chunk is index $M-1$ with `SucceedingNeighborCount = 2`. Assert succeeding expansion clamps to $M-1$; no index out of bounds.
- **`W3C7_3_D3_SingleChunkPageNoOp`**: On a page with only 1 chunk, expanding neighbors returns a valid window with `ConstituentChunkIndices == [0]`.

### Category E: Tri-Modal Provenance Semantics
- **`W3C7_3_E1_SubstringEqualityHoldsForModeA`**:
  - For all Mode A focal windows:
  $$\operatorname{page.NormalizedText}.\operatorname{Substring}(W.\operatorname{StartCharOffset}, W.\operatorname{CharLength}) == W.\operatorname{FormattedText}$$
- **`W3C7_3_E2_OffsetsWithinPageBounds`**: Assert $0 \le W.\operatorname{StartCharOffset} < W.\operatorname{EndCharOffset} \le \operatorname{page.NormalizedText.Length}$.
- **`W3C7_3_E3_FallbackModeOffsetsUnmapped`**: When `page.NormalizedText == null`, assert `window.StartCharOffset == -1`, `window.EndCharOffset == -1`, and `window.FormattedText` contains concatenated chunk texts.
- **`W3C7_3_E4_CompositeModeOffsetsUnmapped`**: For composite multi-page windows, assert `window.PageNumber == 0`, `window.StartCharOffset == -1`, and `window.EndCharOffset == -1`.

### Category F: Budget, Target vs. Max & Truncation Handling
- **`W3C7_3_F1_TargetVsMaxAcceptance`**: Add neighbor pushing window past `TargetWindowChars` (800) but below `MaxWindowChars` (1500). Assert neighbor is accepted and `IsTruncated == false`.
- **`W3C7_3_F2_MaxCeilingBackoff`**: Add neighbor pushing window past `MaxWindowChars` (1500). Assert neighbor is rejected, previous neighbor span preserved, and `IsTruncated == false`.
- **`W3C7_3_F3_FocalOversizedSentenceClamping`**: Focal chunk alone exceeds `MaxWindowChars`. Assert window is truncated along sentence boundary, `CharLength <= MaxWindowChars`, and `IsTruncated == true`.
- **`W3C7_3_F4_TruncationDoesNotSeverSurrogates`**: Test truncation on text containing multi-byte emoji/math symbols; assert high and low surrogates are never severed.

### Category G: Composite Prompt Packing & Ordering
- **`W3C7_3_G1_PacksInDocumentReadingOrder`**: Pass candidate chunks with `OrderingMode = DocumentReadingOrder`. Assert composite window orders by `DocumentId -> PageNumber -> ChunkIndex`.
- **`W3C7_3_G2_PreservesCallerRankOrder`**: Pass candidate chunks with `OrderingMode = PreserveInputOrder`. Assert composite window maintains exact caller order.
- **`W3C7_3_G3_AttachesProvenanceHeaders`**: Assert composite window contains `--- [Page 1, Passage 2] ---`.
- **`W3C7_3_G4_EnforcesCompositeBudget`**: Pass 10 passages totaling 4,000 chars with `CompositeBudgetChars = 2000`. Assert `window.CharLength <= 2000` and `window.IsTruncated == true`. Also verify that if candidate passage #1 exceeds `CompositeBudgetChars`, it is truncated to fit the available text budget (if $\ge 50$ chars) with `IsTruncated = true`, and if available text budget $< 50$ chars, emits an empty window with `IsTruncated = true`.
- **`W3C7_3_G5_CrossDocumentDeduplication`**: Pass chunks from Document A and Document B with identical `(PageNumber = 1, ChunkIndex = 0)`. Assert both are retained (no false collision). Pass true duplicate chunk twice; assert second occurrence is dropped.
- **`W3C7_3_G6_MultiDocumentCompositeDocumentId`**: Assert `window.DocumentId == document.DocumentId` when document is provided. When null: assert `window.DocumentId` matches chunk `DocumentId` if all chunks share one document; assert `window.DocumentId == "composite"` if chunks span multiple documents.
- **`W3C7_3_G7_CompositeBudgetEnforcedWithoutHeaders`**: Call `FormulateCompositeWindow` with `IncludeProvenanceHeaders = false`. Assert passages are separated by `\n\n` (no leading delimiter on first passage), delimiters count toward budget, and `window.CharLength <= options.CompositeBudgetChars`.

### Category H: Structured Provenance Citations
- **`W3C7_3_H1_CitationCountMatchesConstituents`**: Assert `window.Citations.Count == window.ConstituentChunks.Count`.
- **`W3C7_3_H2_CitationFieldsAccurate`**: Verify each `StudyCitation` has correct `DocumentId`, `PageNumber`, `ChunkIndex`, and non-empty `MatchedSnippet`. `FileName` defaults to empty or provided value.
- **`W3C7_3_H3_HeuristicTokenEstimateMatches`**: Assert `window.EstimatedTokens == (int)Math.Ceiling(window.CharLength / 4.0)`.

### Category I: Determinism & Stable WindowId
- **`W3C7_3_I1_BitForBitIdenticalAcrossRuns`**: Run `FormulateFocalWindow` and `FormulateCompositeWindow` 50 times. Assert every property, including `WindowId`, is bit-for-bit identical across all runs.
- **`W3C7_3_I2_DeterministicWindowIdPattern`**: Assert `WindowId` matches `win_{docId}_p{page}_f{k}_c{min}_{max}` for Mode A (scoped as spatial coordinate identity), and `comp_{sha256_64}` (full 64-hex SHA-256 digest) for Mode C (or `comp_empty`).
- **`W3C7_3_I3_CompositeWindowIdCollisionResistance`**: Formulate composite windows for Candidate Set 1 (chunks [0, 1] on Page 1) and Candidate Set 2 (chunks [10, 11] on Page 1). Assert that despite identical page and count characteristics, both windows yield distinct `WindowId` values consisting of `comp_` followed by exactly 64 lowercase hexadecimal characters, with zero random GUIDs.

### Category J: Multi-Threaded Concurrency (20 Threads)
- **`W3C7_3_J1_ConcurrentExecutionSafety`**: Execute 20 concurrent threads formulating windows simultaneously across distinct pages. Assert zero exceptions, zero state corruption, and identical results across threads.

### Category K: Adversarial Edge Cases & Security Bounds
- **`W3C7_3_K1_NullFocalChunkThrows`**: Assert `ArgumentNullException` when `focalChunk` is null.
- **`W3C7_3_K2_NullPageThrows`**: Assert `ArgumentNullException` when `page` is null.
- **`W3C7_3_K3_OrphanChunkFallsBackSafely`**: Provide a chunk not in `page.Chunks`. Assert graceful fallback to single-chunk window without throwing.
- **`W3C7_3_K4a_MaxWindowCeilingThrows`**: `MaxWindowChars > 10000` or `< 100` throws `ArgumentOutOfRangeException`.
- **`W3C7_3_K4b_TargetWindowBoundsThrow`**: `TargetWindowChars < 50` or `> MaxWindowChars` throws `ArgumentOutOfRangeException`.
- **`W3C7_3_K4c_NeighborCountBoundsThrow`**: `PrecedingNeighborCount > 10` or `SucceedingNeighborCount > 10` throws `ArgumentOutOfRangeException`.
- **`W3C7_3_K4d_CompositeBudgetBoundsThrow`**: `CompositeBudgetChars < 200` or `> 50000` throws `ArgumentOutOfRangeException`.
- **`W3C7_3_K4e_ServiceMethodEntryValidatesOptions`**: Call `FormulateFocalWindow`, `FormulatePageWindows`, and `FormulateCompositeWindow` passing `new ContextWindowOptions { MaxWindowChars = -1 }`. Assert each immediately throws `ArgumentOutOfRangeException`.
- **`W3C7_3_K5_SynchronousExecutionCompletesInProcess`**: Verify execution completes synchronously in-memory without async dispatch or background task spawning.

### Category L: Page Window Formulation (`FormulatePageWindows`)
- **`W3C7_3_L1_FormulatePageWindowsSlidingSequence`**: On a page with 5 chunks, call `FormulatePageWindows`. Assert exactly 5 windows are returned, ordered by focal chunk index $k = 0 \dots 4$, with each `window.FocalChunk` equal to `page.Chunks[k]`.
- **`W3C7_3_L2_FormulatePageWindowsEmptyAndSingleChunk`**: Assert calling with empty chunks returns an empty list `[]`. Assert calling with 1 chunk returns exactly 1 window.
- **`W3C7_3_L3_FormulatePageWindowsDeterministicIds`**: Assert each window has `WindowId == $"win_{documentId}_p{page.PageNumber}_f{k}_c{minChunk}_{maxChunk}"` and that 50 repeated runs yield bit-for-bit identical results.
- **`W3C7_3_L4_FormulatePageWindowsOversizedChunk`**: On a page containing an oversized chunk exceeding `MaxWindowChars`, assert the window corresponding to that focal chunk has `IsTruncated == true` and `CharLength <= MaxWindowChars`.

### Category M: Downstream Consumer Simulation
- **`W3C7_3_M1_DocumentChatSimulation`**: Simulate query ranking producing candidate passages; formulate composite window; assert assembled prompt context matches expected prompt budget and formatting without mutating `DocumentChatService.cs`.

---

## 4. Verification Execution Commands (For Future Execution)

When implementation is authorized, verification will be run via:
```powershell
# 1. Build Verification
dotnet build Axora-Desktop-WinUI\Axora.Desktop.sln -p:Platform=x64 -c Debug --no-restore
dotnet build Axora-Desktop-WinUI\Axora.Desktop.Tests\Axora.Desktop.Tests.csproj -p:Platform=x64 -c Debug --no-restore

# 2. Test Runner Execution
powershell -ExecutionPolicy Bypass -File scripts\qa\run-tests.ps1 -Target WinUI
```

### Expected Test Assertions Summary:
- **Baseline passing assertions**: 1,278
- **New Stage C7.3 planned assertions**: 49
- **Expected post-C7.3 passing total**: 1,327 assertions (100% pass)
