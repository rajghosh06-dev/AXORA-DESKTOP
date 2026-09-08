# Phase W3-C.7 Verification Plan: Passage Chunking & Bounded Context Window Formulation

**Phase**: `W3-C.7 — Passage Chunking & Bounded Context Window Formulation`  
**Status**: `PLANNING ONLY / RECONCILED POST-AUDIT`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4`  

---

## 1. Overview & Quality Assurance Mandate

The Phase W3-C.7 verification protocol guarantees that the deterministic passage chunker satisfies every behavioral, structural, performance, and security invariant established in the Product Contract and Rule Matrix.

Verification relies on automated, deterministic C# assertions integrated into the WinUI adversarial stress test suite (`Axora-Desktop-WinUI\Axora.Desktop.Tests\Program.cs`) and executed via the official runner:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\qa\run-tests.ps1 -Target WinUI
```

Zero flakiness, zero cloud dependencies, zero external process requirements, and zero thread-scheduling race conditions are permitted. All offsets, lengths, and indices are verified in **UTF-16 code units** (`char`).

---

## 2. 26 Explicit Test Categories

| Cat # | Category Identifier | Target Invariant / Construct | Input Scenario & Test Fixture | Expected Assertions & Acceptance Criteria |
| :---: | :--- | :--- | :--- | :--- |
| **1** | `W3C7_01_EmptyInput` | `RULE-C7-SEC-05` | `""`, `"   "`, `"\r\n\t"` | Returns 0 chunks (`chunks.Count == 0`). Zero exceptions. |
| **2** | `W3C7_02_SingleShortParagraph` | `RULE-C7-STR-07` | Single paragraph of 180 code units ($< 350$) | Exactly 1 chunk emitted. `ChunkIndex == 0`, `Text` equals trimmed paragraph, `StartCharOffset == 0`, `CharLength == 180`. |
| **3** | `W3C7_03_MultipleShortParagraphs` | `RULE-C7-STR-07` | Three small paragraphs (80, 90, 70 chars, total 240 chars $< 350$) | Packed into exactly 1 chunk. `chunks.Count == 1`. Paragraph breaks (`\n\n`) preserved inside chunk. |
| **4** | `W3C7_04_MultipleParagraphsOverflow` | `RULE-C7-STR-07` | Two paragraphs of 250 chars each (total 500 chars $> 350$) | Slices into exactly 2 chunks cleanly along the `\n\n` boundary. Chunk 1 has para 1; Chunk 2 has para 2. Zero paragraph truncation. |
| **5** | `W3C7_05_Headings` | `RULE-C7-STR-01` | `# Introduction\n\nThis is the introductory text of the document.` | Heading retained with the introductory paragraph in Chunk 1. Heading text intact with `# ` marker. **No synthetic prefix injection**. |
| **6** | `W3C7_06_Lists` | `RULE-C7-STR-04` | Bullet list of 6 items (total 450 chars $> 350$) | Slices along list item boundary (`\n- `). Chunk 1 has items 1–4; Chunk 2 has items 5–6. No mid-bullet slicing. |
| **7** | `W3C7_07_TablesIntact` | `RULE-C7-STR-03` | 4-row Markdown table (280 chars $< 600$) | Emitted as a single intact chunk. Header row and data rows unbroken. |
| **8** | `W3C7_08_CodeBlocksIntact` | `RULE-C7-STR-02` | Fenced C# code block (250 chars $< 600$) | Emitted as a single intact chunk. Opening ` ```csharp ` and closing ` ``` ` fences intact. Indentation preserved. Zero synthetic language tags added. |
| **9** | `W3C7_09_Blockquotes` | `RULE-C7-STR-05` | Academic quote block starting with `> ` | Emitted with `> ` markers intact. Bounded within target size. |
| **10** | `W3C7_10_ExactlyAtLimitInput` | `RULE-C7-OVR-01` | Paragraph of exactly 350 code units | Emitted as exactly 1 chunk of length 350. `CharLength == 350`. |
| **11** | `W3C7_11_JustOverLimitInput` | `RULE-C7-OVR-01` | Paragraph of 351 code units containing two sentences (200 chars + 151 chars) | Splits into 2 chunks along sentence boundary. Chunk 1 = Sentence 1; Chunk 2 = Sentence 2. |
| **12** | `W3C7_12_VeryLargeParagraph` | `RULE-C7-OVR-01` | Monolithic 1,200-char paragraph containing 8 academic sentences | Sliced into bounded chunks strictly along sentence boundaries. All sentences accounted for. |
| **13** | `W3C7_13_VeryLongSentence` | `RULE-C7-OVR-05` | Single 500-char legal sentence without periods, containing clauses (`; `, `, `) | Sliced along clause boundaries (`; ` or `, `). No mid-word cuts. |
| **14** | `W3C7_14_OversizedCodeBlock` | `RULE-C7-OVR-02` | 800-char code block with 20 lines | Sliced into chunks strictly along newline boundaries (`\n`). No single line split mid-statement. |
| **15** | `W3C7_15_OversizedTable` | `RULE-C7-OVR-03` | 15-row Markdown table (900 chars $> 600$) | Sliced along row boundaries. **Zero synthetic header replication in Text**. Chunk 2 contains verbatim source rows; `NormalizedText.Substring` equality verified. |
| **16** | `W3C7_16_OversizedList` | `RULE-C7-OVR-04` | 20-item numbered list (1,200 chars $> 600$) | Sliced cleanly between items. No item broken in half. |
| **17** | `W3C7_17_UnicodeMultiByte` | `RULE-C7-SEC-06` | Japanese Kanji, Arabic, Devanagari, and emoji surrogate pairs (`🧪`, `🧬`) | Zero character corruption. No surrogate pair sliced in half (no `\uFFFD`). Offsets accurate. |
| **18** | `W3C7_18_ScientificNotation` | `RULE-C7-SNT-03/05` | Text with `3.14159`, `$12.50`, `et al. (2023)`, `e.g.`, `Dr. Smith` | Abbreviation guards prevent false splits. `et al.` and `3.14` never sliced into sentence breaks. |
| **19** | `W3C7_19_MultiPageDocument` | `RULE-C7-OFF-05` | 3-page document ingested via orchestrator | Chunks generated across all 3 pages. Page numbers strictly match parent pages. `TotalChunks` matches sum. |
| **20** | `W3C7_20_PageBoundaryPreservation` | Invariant 4 | Document with paragraph ending on Page 1 and continuing on Page 2 | Chunks strictly stop at Page 1 end. Page 2 begins with fresh ChunkIndex 0. Zero cross-page chunk fusion. |
| **21** | `W3C7_21_OverlapEnabled` | `RULE-C7-OVL-02` | `StrideOverlapChars = 60`, `SnapToSentenceBoundaries = true` | Chunk 2 begins with overlapping prefix from Chunk 1. Verified that `StartCharOffset` of Chunk 2 snaps to sentence start within $[s^* - 40, s^* + 40]$. Resulting overlap satisfies $0 \le e_1 - s_2 \le 100$. |
| **22** | `W3C7_22_OverlapDisabled` | `RULE-C7-OVL-01` | `StrideOverlapChars = 0` | Chunks form a strictly disjoint partition. Verified $s_{k+1} \ge e_k$ for all adjacent chunks. |
| **23** | `W3C7_23_DeterminismRepeatedRuns` | Invariant 3 | Execute `ChunkPage` 50 times in a loop on identical text | All 50 chunk lists bit-for-bit identical (texts, offsets, lengths, indices). |
| **24** | `W3C7_24_ConcurrentExecution` | Architecture §8.3 | Execute `ChunkPage` across 20 parallel threads concurrently | Zero exceptions, deterministic output per thread, zero state leakage across threads. |
| **25** | `W3C7_25_LosslessCoverage` | Invariant 6 | 5,000-char complex academic paper with tables, code, lists | Disjoint partitioning and content coverage verified using 3-part mathematical model. All non-whitespace indices accounted for. |
| **26** | `W3C7_26_PathologicalInput` | `RULE-C7-SEC-01/03` | 100,000-char unbroken line of `A...A` and 5,000 empty lines | $O(N)$ linear execution. Chunks strictly capped at `MaxChunkSizeChars` (600) and absolute ceiling (2,000). Zero stack overflows. |

---

## 3. Concrete Verification Assertions

### 3.1 Unconditional Substring Offset Invariant Assertion:
```csharp
// W3C7_UnconditionalOffsetSubstringInvariant
foreach (var chunk in chunks)
{
    string extracted = normalizedText.Substring(chunk.StartCharOffset, chunk.CharLength);
    Assert(extracted == chunk.Text,
        $"Offset substring mismatch: expected '{chunk.Text}', got '{extracted}'");
    Assert(chunk.EndCharOffset == chunk.StartCharOffset + chunk.CharLength,
        "EndCharOffset math mismatch");
    Assert(!char.IsWhiteSpace(chunk.Text[0]),
        "Chunk begins with leading whitespace");
    Assert(!char.IsWhiteSpace(chunk.Text[^1]),
        "Chunk ends with trailing whitespace");
}
```

### 3.2 Three-Part Lossless Coverage Verification (Disjoint Mode):
```csharp
// W3C7_ThreePartLosslessCoverage (StrideOverlapChars == 0)

// Part A: Source-Span Integrity
foreach (var chunk in chunks)
{
    Assert(normalizedText.Substring(chunk.StartCharOffset, chunk.CharLength) == chunk.Text,
        "Part A failed: Source span mismatch");
}

// Part B: Disjoint Monotonicity
for (int i = 1; i < chunks.Count; i++)
{
    Assert(chunks[i].StartCharOffset >= chunks[i - 1].EndCharOffset,
        "Part B failed: Chunks overlap in disjoint mode");
    
    // Inter-chunk text must be purely whitespace/delimiters
    int gapStart = chunks[i - 1].EndCharOffset;
    int gapLength = chunks[i].StartCharOffset - gapStart;
    if (gapLength > 0)
    {
        string gap = normalizedText.Substring(gapStart, gapLength);
        Assert(string.IsNullOrWhiteSpace(gap),
            $"Part B failed: Substantive content dropped in inter-chunk gap: '{gap}'");
    }
}

// Part C: Substantive Character Coverage
var coveredIndices = new HashSet<int>();
foreach (var chunk in chunks)
{
    for (int idx = chunk.StartCharOffset; idx < chunk.EndCharOffset; idx++)
    {
        coveredIndices.Add(idx);
    }
}
for (int i = 0; i < normalizedText.Length; i++)
{
    if (!char.IsWhiteSpace(normalizedText[i]))
    {
        Assert(coveredIndices.Contains(i),
            $"Part C failed: Character at index {i} ('{normalizedText[i]}') not covered by any chunk");
    }
}
```

### 3.3 Overlap Bounds Assertion:
```csharp
// W3C7_OverlapBounds (StrideOverlapChars == 60, Delta == 40)
for (int i = 1; i < chunks.Count; i++)
{
    int overlap = chunks[i - 1].EndCharOffset - chunks[i].StartCharOffset;
    Assert(overlap >= 0, "Negative overlap detected");
    Assert(overlap <= 100, $"Overlap exceeded maximum bound (60 + 40): {overlap}");
    Assert(chunks[i].StartCharOffset > chunks[i - 1].StartCharOffset, "Chunk start offsets not strictly monotonically increasing");
}
```

### 3.4 Pure Source-Derived Table Assertion:
```csharp
// W3C7_PureSourceTableChunks (Oversized table)
var tableChunks = chunker.ChunkPage("doc1", 1, oversizedTableText, new ChunkingOptions { TargetChunkSizeChars = 200 });
foreach (var chunk in tableChunks)
{
    // Must NOT contain duplicate synthetic headers
    Assert(normalizedText.Substring(chunk.StartCharOffset, chunk.CharLength) == chunk.Text,
        "Table chunk text must strictly match source substring");
}
```

---

## 4. Pass / Fail Acceptance Gates

Progression from planning to implementation, and subsequent closure audit, requires:

1. **Compilation Cleanliness**:
   - `dotnet build Axora.Desktop.sln` on `x64 Debug`: **0 Errors, 0 Warnings**.
   - `dotnet build Axora.Desktop.Tests.csproj` on `x64 Debug`: **0 Errors, 0 Warnings**.
2. **Automated Test Gate**:
   - 100% of all existing baseline tests (1,230 assertions) continue to pass green.
   - 100% of all newly introduced Phase W3-C.7 tests (at least 26 test categories, ~50+ assertions) pass green.
3. **Git Purity**:
   - `d0e64ce8b447ea863cb5bebad4402829ba1f1aa4` protected baseline intact.
   - `Axora-Desktop-MaterialUI` completely untouched.
   - Zero commits, zero pushes without explicit user authorization.
