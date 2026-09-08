using System;
using System.Collections.Generic;
using System.Text;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Deterministic, source-aware passage chunking engine for Phase W3-C.7.
/// Slices NormalizedText into bounded, semantically coherent passages strictly within physical page boundaries.
/// 
/// Invariants:
/// 1. Substring Equality: pageText.Substring(chunk.StartCharOffset, chunk.CharLength) == chunk.Text unconditionally.
/// 2. Zero Cross-Page Merging: Chunks are strictly page-bounded.
/// 3. Pure Source Text: Zero synthetic headers, tags, or context injected into Text.
/// 4. Deterministic Precedence: Code fence -> Table -> Blockquote -> Heading -> List -> Paragraph.
/// 5. Deterministic & Thread-Safe: Stateless O(N) linear single-pass scanning.
/// </summary>
public sealed class PassageChunker : IPassageChunker
{
    private const int DefaultMaxChunkSizeChars = 600;
    private const int AbsoluteMaxChunkSizeChars = 2000;
    private const int MaxPassagesPerPage = 1000;

    private static readonly HashSet<string> AbbreviationSet = new(StringComparer.OrdinalIgnoreCase)
    {
        // Titles
        "mr.", "mrs.", "ms.", "dr.", "prof.", "rev.", "sr.", "jr.",
        // Academic Latin
        "e.g.", "i.e.", "et al.", "cf.", "vs.", "ibid.", "op. cit.", "cit.",
        // Publication tokens
        "fig.", "figs.", "tab.", "vol.", "no.", "pp.", "p.", "eq.", "ref."
    };

    /// <inheritdoc />
    public IReadOnlyList<DocumentPassageChunk> ChunkPage(
        string documentId,
        int pageNumber,
        string pageText,
        ChunkingOptions? options = null)
    {
        // RULE-C7-SEC-05: Empty / Whitespace-Only Page
        if (string.IsNullOrWhiteSpace(pageText))
        {
            return Array.Empty<DocumentPassageChunk>();
        }

        options ??= new ChunkingOptions();
        options.Validate(); // RULE-C7-SEC-04: Validate options (throws ArgumentOutOfRangeException if invalid)

        try
        {
            return ExecuteChunking(documentId, pageNumber, pageText, options);
        }
        catch (Exception)
        {
            // RULE-C7-ERR-01: Deterministic bounded fallback covering NormalizedText
            return CreateBoundedFallbackChunks(documentId, pageNumber, pageText, options.TargetChunkSizeChars);
        }
    }

    private static IReadOnlyList<DocumentPassageChunk> ExecuteChunking(
        string documentId,
        int pageNumber,
        string pageText,
        ChunkingOptions options)
    {
        int targetSize = options.TargetChunkSizeChars;
        int maxChunkSize = Math.Max(DefaultMaxChunkSizeChars, targetSize);
        int strideOverlap = options.StrideOverlapChars;
        int snapDelta = options.SentenceSnapBoundaryDelta;
        bool snapToSentence = options.SnapToSentenceBoundaries;

        // 1. Structural Segmentation into granular atomic units
        var units = SegmentPage(pageText, targetSize, maxChunkSize);
        if (units.Count == 0)
        {
            return Array.Empty<DocumentPassageChunk>();
        }

        // 2. Window Packing & Overlap Computation
        var result = new List<DocumentPassageChunk>();
        int unitIndex = 0;
        int chunkIndex = 0;
        int prevChunkStart = -1;
        int prevChunkEnd = -1;

        while (unitIndex < units.Count && result.Count < MaxPassagesPerPage)
        {
            int chunkStart;
            int chunkEnd;

            if (result.Count == 0 || strideOverlap == 0)
            {
                // Disjoint mode or first chunk: starts at the next unit's start
                chunkStart = units[unitIndex].Start;
            }
            else
            {
                // Overlapping mode: calculate nominal start s* = e_prev - O
                int nominalStart = prevChunkEnd - strideOverlap;
                chunkStart = ResolveOverlapStart(
                    pageText,
                    nominalStart,
                    snapDelta,
                    snapToSentence,
                    prevChunkStart,
                    prevChunkEnd);

                // Find the first unit that overlaps with or contains chunkStart
                while (unitIndex < units.Count && units[unitIndex].End <= chunkStart)
                {
                    unitIndex++;
                }

                if (unitIndex >= units.Count)
                {
                    break;
                }

                // If chunkStart falls before or at unitIndex.Start, align to unitIndex.Start
                if (chunkStart < units[unitIndex].Start)
                {
                    chunkStart = units[unitIndex].Start;
                }
            }

            // Ensure chunkStart points to non-whitespace
            while (chunkStart < pageText.Length && char.IsWhiteSpace(pageText[chunkStart]))
            {
                chunkStart++;
            }

            if (chunkStart >= pageText.Length)
            {
                break;
            }

            // Monotonicity guard: ensure chunkStart is strictly increasing
            if (chunkStart <= prevChunkStart)
            {
                chunkStart = prevChunkStart + 1;
                while (chunkStart < pageText.Length && char.IsWhiteSpace(pageText[chunkStart]))
                {
                    chunkStart++;
                }
                if (chunkStart >= pageText.Length)
                {
                    break;
                }
            }

            // Accumulate units into the chunk
            chunkEnd = units[unitIndex].End;

            // RULE-C7-STR-01: Heading Preservation
            // If the first unit is a standalone Heading, check if subsequent block fits
            bool isHeading = units[unitIndex].Kind == StructuralUnitKind.Heading;
            bool isAtomicBlock = units[unitIndex].Kind == StructuralUnitKind.AtomicCode ||
                                 units[unitIndex].Kind == StructuralUnitKind.AtomicTable;

            if (isAtomicBlock)
            {
                // Standalone atomic block (table or code <= 600 chars)
                unitIndex++;
            }
            else if (isHeading)
            {
                // Heading: can combine with next unit if combined size <= targetSize
                unitIndex++;
                if (unitIndex < units.Count)
                {
                    int potentialEnd = units[unitIndex].End;
                    if (potentialEnd - chunkStart <= targetSize)
                    {
                        chunkEnd = potentialEnd;
                        unitIndex++;
                    }
                    // Else: emit heading as standalone chunk
                }
            }
            else
            {
                // Standard accumulation
                unitIndex++;
                while (unitIndex < units.Count)
                {
                    var nextUnit = units[unitIndex];

                    // Do not merge atomic tables or code blocks into flowing text chunks
                    if (nextUnit.Kind == StructuralUnitKind.AtomicCode ||
                        nextUnit.Kind == StructuralUnitKind.AtomicTable ||
                        nextUnit.Kind == StructuralUnitKind.Heading)
                    {
                        break;
                    }

                    int candidateEnd = nextUnit.End;
                    int candidateLength = candidateEnd - chunkStart;

                    if (candidateLength <= targetSize)
                    {
                        chunkEnd = candidateEnd;
                        unitIndex++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // In overlapping mode: ensure chunkEnd makes progress past prevChunkEnd
            if (strideOverlap > 0 && chunkEnd <= prevChunkEnd && unitIndex < units.Count)
            {
                chunkEnd = units[unitIndex].End;
                unitIndex++;
            }

            // Finalize trimmed offsets (RULE-C7-OFF-01 to RULE-C7-OFF-04)
            int finalStart = chunkStart;
            int finalEnd = chunkEnd;

            while (finalStart < finalEnd && char.IsWhiteSpace(pageText[finalStart]))
            {
                finalStart++;
            }
            while (finalEnd > finalStart && char.IsWhiteSpace(pageText[finalEnd - 1]))
            {
                finalEnd--;
            }

            int charLength = finalEnd - finalStart;
            if (charLength > 0)
            {
                // Enforce absolute safety ceiling (RULE-C7-SEC-03)
                if (charLength > AbsoluteMaxChunkSizeChars)
                {
                    finalEnd = finalStart + AbsoluteMaxChunkSizeChars;
                    finalEnd = AdjustForSurrogatePair(pageText, finalEnd);
                    while (finalEnd > finalStart && char.IsWhiteSpace(pageText[finalEnd - 1]))
                    {
                        finalEnd--;
                    }
                    charLength = finalEnd - finalStart;
                }

                string text = pageText.Substring(finalStart, charLength);

                result.Add(new DocumentPassageChunk
                {
                    ChunkId = chunkIndex,
                    DocumentId = documentId ?? string.Empty,
                    PageNumber = pageNumber,
                    ChunkIndex = chunkIndex,
                    Text = text,
                    StartCharOffset = finalStart,
                    EndCharOffset = finalEnd,
                    CharLength = charLength,
                    EmbeddingStatus = PassageEmbeddingStatus.NoEmbedding,
                    Embedding = null
                });

                prevChunkStart = finalStart;
                prevChunkEnd = finalEnd;
                chunkIndex++;
            }
        }

        return result;
    }

    #region Overlap Snapping

    private static int ResolveOverlapStart(
        string pageText,
        int nominalStart,
        int snapDelta,
        bool snapToSentence,
        int prevChunkStart,
        int prevChunkEnd)
    {
        int windowStart = Math.Max(prevChunkStart + 1, nominalStart - snapDelta);
        int windowEnd = Math.Min(prevChunkEnd, nominalStart + snapDelta);

        if (windowStart >= windowEnd)
        {
            return Math.Clamp(nominalStart, prevChunkStart + 1, prevChunkEnd);
        }

        // Priority 1: Sentence Boundary Snapping (RULE-C7-OVL-02)
        if (snapToSentence)
        {
            int bestSentenceCandidate = -1;
            int bestSentenceDist = int.MaxValue;

            for (int i = windowStart; i <= windowEnd; i++)
            {
                if (IsSentenceStart(pageText, i))
                {
                    int dist = Math.Abs(i - nominalStart);
                    if (dist < bestSentenceDist)
                    {
                        bestSentenceDist = dist;
                        bestSentenceCandidate = i;
                    }
                }
            }

            if (bestSentenceCandidate != -1)
            {
                return Math.Clamp(bestSentenceCandidate, prevChunkStart + 1, prevChunkEnd);
            }
        }

        // Priority 2: Word Boundary Snapping (RULE-C7-OVL-03)
        int bestWordCandidate = -1;
        int bestWordDist = int.MaxValue;

        for (int i = windowStart; i <= windowEnd; i++)
        {
            if (i > 0 && i < pageText.Length && !char.IsWhiteSpace(pageText[i]) && char.IsWhiteSpace(pageText[i - 1]))
            {
                int dist = Math.Abs(i - nominalStart);
                if (dist < bestWordDist)
                {
                    bestWordDist = dist;
                    bestWordCandidate = i;
                }
            }
        }

        if (bestWordCandidate != -1)
        {
            return Math.Clamp(bestWordCandidate, prevChunkStart + 1, prevChunkEnd);
        }

        // Priority 3: Stride Fallback (RULE-C7-OVL-04)
        return Math.Clamp(nominalStart, prevChunkStart + 1, prevChunkEnd);
    }

    private static bool IsSentenceStart(string text, int index)
    {
        if (index <= 0 || index >= text.Length || char.IsWhiteSpace(text[index]))
        {
            return false;
        }

        // Must be preceded by whitespace
        if (!char.IsWhiteSpace(text[index - 1]))
        {
            return false;
        }

        // Scan backwards past whitespace to find preceding punctuation
        int p = index - 1;
        while (p >= 0 && char.IsWhiteSpace(text[p]))
        {
            p--;
        }

        if (p < 0)
        {
            return false;
        }

        // Check for closing quote or parenthesis
        if (text[p] == '"' || text[p] == '\'' || text[p] == '”' || text[p] == '’' || text[p] == ')' || text[p] == ']')
        {
            p--;
        }

        if (p < 0)
        {
            return false;
        }

        char punct = text[p];
        if (punct != '.' && punct != '?' && punct != '!')
        {
            return false;
        }

        // If it's a period, verify it's not an abbreviation or decimal number
        if (punct == '.')
        {
            if (IsAbbreviationOrDecimal(text, p))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAbbreviationOrDecimal(string text, int dotIndex)
    {
        // Numerical / decimal guard: digits on both sides
        if (dotIndex > 0 && char.IsDigit(text[dotIndex - 1]) &&
            dotIndex + 1 < text.Length && char.IsDigit(text[dotIndex + 1]))
        {
            return true;
        }

        // Scan backwards to find the word ending at dotIndex
        int wordStart = dotIndex - 1;
        while (wordStart >= 0 && (char.IsLetter(text[wordStart]) || text[wordStart] == '.'))
        {
            wordStart--;
        }
        wordStart++;

        int wordLen = dotIndex + 1 - wordStart;
        if (wordLen <= 0)
        {
            return false;
        }

        string word = text.Substring(wordStart, wordLen);
        if (AbbreviationSet.Contains(word))
        {
            return true;
        }

        // Special handling for "et al."
        if (word.Equals("al.", StringComparison.OrdinalIgnoreCase))
        {
            if (wordStart >= 3 && text.Substring(wordStart - 3, 3).Equals("et ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Special handling for "op. cit."
        if (word.Equals("cit.", StringComparison.OrdinalIgnoreCase))
        {
            if (wordStart >= 4 && text.Substring(wordStart - 4, 4).Equals("op. ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Structural Segmentation

    private enum StructuralUnitKind
    {
        Paragraph,
        Sentence,
        Clause,
        Word,
        Heading,
        AtomicCode,
        CodeLine,
        AtomicTable,
        TableRow,
        ListItem,
        Blockquote
    }

    private sealed record SubBlockUnit(
        StructuralUnitKind Kind,
        int Start,
        int End);

    /// <summary>
    /// Deterministic structural parsing adhering strictly to contract precedence:
    /// Code fence -> Table -> Blockquote -> Heading -> List -> Paragraph.
    /// </summary>
    private static List<SubBlockUnit> SegmentPage(string pageText, int targetSize, int maxChunkSize)
    {
        var lines = SplitIntoLines(pageText);
        var units = new List<SubBlockUnit>();

        int lineIdx = 0;
        while (lineIdx < lines.Count)
        {
            var line = lines[lineIdx];
            string trimmed = line.Content.Trim();

            if (trimmed.Length == 0)
            {
                lineIdx++;
                continue;
            }

            // 1. Code Fence (Priority 1)
            if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
            {
                string fenceMarker = trimmed.Substring(0, 3);
                int codeStart = line.Start;
                int codeEnd = line.ContentEnd;
                var codeLines = new List<TextLine> { line };
                lineIdx++;

                while (lineIdx < lines.Count)
                {
                    var curLine = lines[lineIdx];
                    codeLines.Add(curLine);
                    codeEnd = curLine.ContentEnd;
                    lineIdx++;

                    if (curLine.Content.Trim().StartsWith(fenceMarker))
                    {
                        break;
                    }
                }

                // Trim trailing whitespace from code block
                while (codeEnd > codeStart && char.IsWhiteSpace(pageText[codeEnd - 1]))
                {
                    codeEnd--;
                }

                int codeLength = codeEnd - codeStart;
                if (codeLength <= maxChunkSize)
                {
                    units.Add(new SubBlockUnit(StructuralUnitKind.AtomicCode, codeStart, codeEnd));
                }
                else
                {
                    // RULE-C7-OVR-02: Oversized Code Slicing along line boundaries
                    foreach (var cLine in codeLines)
                    {
                        int lStart = cLine.Start;
                        int lEnd = cLine.ContentEnd;
                        while (lStart < lEnd && char.IsWhiteSpace(pageText[lStart])) lStart++;
                        while (lEnd > lStart && char.IsWhiteSpace(pageText[lEnd - 1])) lEnd--;
                        if (lEnd > lStart)
                        {
                            if (lEnd - lStart > maxChunkSize)
                            {
                                SlicePathologicalToken(pageText, lStart, lEnd, maxChunkSize, units, StructuralUnitKind.CodeLine);
                            }
                            else
                            {
                                units.Add(new SubBlockUnit(StructuralUnitKind.CodeLine, lStart, lEnd));
                            }
                        }
                    }
                }
                continue;
            }

            // 2. Markdown Table (Priority 2)
            if (IsTableLine(trimmed))
            {
                int tableStart = line.Start;
                int tableEnd = line.ContentEnd;
                var tableRows = new List<TextLine> { line };
                lineIdx++;

                while (lineIdx < lines.Count && IsTableLine(lines[lineIdx].Content.Trim()))
                {
                    tableRows.Add(lines[lineIdx]);
                    tableEnd = lines[lineIdx].ContentEnd;
                    lineIdx++;
                }

                while (tableEnd > tableStart && char.IsWhiteSpace(pageText[tableEnd - 1]))
                {
                    tableEnd--;
                }

                int tableLength = tableEnd - tableStart;
                if (tableLength <= maxChunkSize)
                {
                    units.Add(new SubBlockUnit(StructuralUnitKind.AtomicTable, tableStart, tableEnd));
                }
                else
                {
                    // RULE-C7-OVR-03: Oversized Table Slicing strictly along row boundaries
                    foreach (var rLine in tableRows)
                    {
                        int rStart = rLine.Start;
                        int rEnd = rLine.ContentEnd;
                        while (rStart < rEnd && char.IsWhiteSpace(pageText[rStart])) rStart++;
                        while (rEnd > rStart && char.IsWhiteSpace(pageText[rEnd - 1])) rEnd--;
                        if (rEnd > rStart)
                        {
                            units.Add(new SubBlockUnit(StructuralUnitKind.TableRow, rStart, rEnd));
                        }
                    }
                }
                continue;
            }

            // 3. Blockquote (Priority 3)
            if (trimmed.StartsWith(">"))
            {
                int bStart = line.Start;
                int bEnd = line.ContentEnd;
                lineIdx++;

                while (lineIdx < lines.Count && lines[lineIdx].Content.Trim().StartsWith(">"))
                {
                    bEnd = lines[lineIdx].ContentEnd;
                    lineIdx++;
                }

                while (bEnd > bStart && char.IsWhiteSpace(pageText[bEnd - 1]))
                {
                    bEnd--;
                }

                if (bEnd - bStart <= targetSize)
                {
                    units.Add(new SubBlockUnit(StructuralUnitKind.Blockquote, bStart, bEnd));
                }
                else
                {
                    SliceParagraph(pageText, bStart, bEnd, targetSize, maxChunkSize, units);
                }
                continue;
            }

            // 4. Heading (Priority 4)
            if (IsHeadingLine(trimmed, out int hashCount))
            {
                int hStart = line.Start + line.Content.IndexOf('#');
                int hEnd = line.ContentEnd;
                while (hEnd > hStart && char.IsWhiteSpace(pageText[hEnd - 1]))
                {
                    hEnd--;
                }
                units.Add(new SubBlockUnit(StructuralUnitKind.Heading, hStart, hEnd));
                lineIdx++;
                continue;
            }

            // 5. List Block (Priority 5)
            if (IsListItemLine(trimmed))
            {
                int listStart = line.Start;
                int listEnd = line.ContentEnd;
                var listItems = new List<(int Start, int End)>();
                int curItemStart = line.Start;
                int curItemEnd = line.ContentEnd;
                lineIdx++;

                while (lineIdx < lines.Count)
                {
                    var curLine = lines[lineIdx];
                    string curTrimmed = curLine.Content.Trim();

                    if (curTrimmed.Length == 0)
                    {
                        break;
                    }

                    if (IsListItemLine(curTrimmed))
                    {
                        listItems.Add((curItemStart, curItemEnd));
                        curItemStart = curLine.Start;
                        curItemEnd = curLine.ContentEnd;
                    }
                    else
                    {
                        // Continuation line of list item
                        curItemEnd = curLine.ContentEnd;
                    }
                    lineIdx++;
                }
                listItems.Add((curItemStart, curItemEnd));

                foreach (var item in listItems)
                {
                    int iStart = item.Start;
                    int iEnd = item.End;
                    while (iStart < iEnd && char.IsWhiteSpace(pageText[iStart])) iStart++;
                    while (iEnd > iStart && char.IsWhiteSpace(pageText[iEnd - 1])) iEnd--;
                    if (iEnd > iStart)
                    {
                        if (iEnd - iStart > maxChunkSize)
                        {
                            SliceParagraph(pageText, iStart, iEnd, targetSize, maxChunkSize, units);
                        }
                        else
                        {
                            units.Add(new SubBlockUnit(StructuralUnitKind.ListItem, iStart, iEnd));
                        }
                    }
                }
                continue;
            }

            // 6. Standard Body Paragraph (Priority 6)
            {
                int pStart = line.Start;
                int pEnd = line.ContentEnd;
                lineIdx++;

                while (lineIdx < lines.Count)
                {
                    var curLine = lines[lineIdx];
                    string curTrimmed = curLine.Content.Trim();

                    if (curTrimmed.Length == 0 ||
                        curTrimmed.StartsWith("```") ||
                        curTrimmed.StartsWith("~~~") ||
                        IsTableLine(curTrimmed) ||
                        curTrimmed.StartsWith(">") ||
                        IsHeadingLine(curTrimmed, out _) ||
                        IsListItemLine(curTrimmed))
                    {
                        break;
                    }

                    pEnd = curLine.ContentEnd;
                    lineIdx++;
                }

                while (pEnd > pStart && char.IsWhiteSpace(pageText[pEnd - 1]))
                {
                    pEnd--;
                }

                int pLength = pEnd - pStart;
                if (pLength <= targetSize)
                {
                    units.Add(new SubBlockUnit(StructuralUnitKind.Paragraph, pStart, pEnd));
                }
                else
                {
                    // RULE-C7-OVR-01: Oversized Paragraph Slicing into sentences
                    SliceParagraph(pageText, pStart, pEnd, targetSize, maxChunkSize, units);
                }
            }
        }

        return units;
    }

    private static void SliceParagraph(
        string pageText,
        int start,
        int end,
        int targetSize,
        int maxChunkSize,
        List<SubBlockUnit> units)
    {
        // Scan for sentences
        var sentences = ScanSentences(pageText, start, end);

        if (sentences.Count <= 1)
        {
            // Single sentence exceeds target size: slice along clauses (RULE-C7-OVR-05)
            SliceSentence(pageText, start, end, targetSize, maxChunkSize, units);
            return;
        }

        foreach (var s in sentences)
        {
            int sStart = s.Start;
            int sEnd = s.End;

            if (sEnd - sStart > targetSize)
            {
                SliceSentence(pageText, sStart, sEnd, targetSize, maxChunkSize, units);
            }
            else
            {
                units.Add(new SubBlockUnit(StructuralUnitKind.Sentence, sStart, sEnd));
            }
        }
    }

    private static void SliceSentence(
        string pageText,
        int start,
        int end,
        int targetSize,
        int maxChunkSize,
        List<SubBlockUnit> units)
    {
        // RULE-C7-OVR-05: Slice along clause punctuation (; : , —)
        var clauses = ScanClauses(pageText, start, end);

        if (clauses.Count <= 1)
        {
            // Single clause exceeds target size: slice along word boundaries (RULE-C7-OVR-06)
            SliceWords(pageText, start, end, targetSize, maxChunkSize, units);
            return;
        }

        foreach (var c in clauses)
        {
            int cStart = c.Start;
            int cEnd = c.End;

            if (cEnd - cStart > targetSize)
            {
                SliceWords(pageText, cStart, cEnd, targetSize, maxChunkSize, units);
            }
            else
            {
                units.Add(new SubBlockUnit(StructuralUnitKind.Clause, cStart, cEnd));
            }
        }
    }

    private static void SliceWords(
        string pageText,
        int start,
        int end,
        int targetSize,
        int maxChunkSize,
        List<SubBlockUnit> units)
    {
        int cur = start;

        while (cur < end)
        {
            while (cur < end && char.IsWhiteSpace(pageText[cur]))
            {
                cur++;
            }

            if (cur >= end)
            {
                break;
            }

            int wordLimit = Math.Min(end, cur + targetSize);
            if (wordLimit == end)
            {
                units.Add(new SubBlockUnit(StructuralUnitKind.Word, cur, end));
                break;
            }

            // Find last whitespace before wordLimit
            int split = wordLimit;
            while (split > cur && !char.IsWhiteSpace(pageText[split]))
            {
                split--;
            }

            if (split > cur)
            {
                int wEnd = split;
                while (wEnd > cur && char.IsWhiteSpace(pageText[wEnd - 1]))
                {
                    wEnd--;
                }
                units.Add(new SubBlockUnit(StructuralUnitKind.Word, cur, wEnd));
                cur = split;
            }
            else
            {
                // Pathological unbroken token: slice at maxChunkSize (RULE-C7-OVR-07)
                int cut = Math.Min(end, cur + maxChunkSize);
                cut = AdjustForSurrogatePair(pageText, cut);
                units.Add(new SubBlockUnit(StructuralUnitKind.Word, cur, cut));
                cur = cut;
            }
        }
    }

    private static void SlicePathologicalToken(
        string pageText,
        int start,
        int end,
        int maxChunkSize,
        List<SubBlockUnit> units,
        StructuralUnitKind kind)
    {
        int cur = start;
        while (cur < end)
        {
            int cut = Math.Min(end, cur + maxChunkSize);
            cut = AdjustForSurrogatePair(pageText, cut);
            units.Add(new SubBlockUnit(kind, cur, cut));
            cur = cut;
        }
    }

    private static List<(int Start, int End)> ScanSentences(string text, int start, int end)
    {
        var result = new List<(int Start, int End)>();
        int curStart = start;

        for (int i = start; i < end; i++)
        {
            char c = text[i];
            if (c == '.' || c == '?' || c == '!')
            {
                if (c == '.' && IsAbbreviationOrDecimal(text, i))
                {
                    continue;
                }

                // Check for closing quote or parenthesis
                int p = i + 1;
                while (p < end && (text[p] == '"' || text[p] == '\'' || text[p] == '”' || text[p] == '’' || text[p] == ')' || text[p] == ']'))
                {
                    p++;
                }

                // Boundary requires whitespace or end
                if (p >= end || char.IsWhiteSpace(text[p]))
                {
                    int sEnd = p;
                    while (sEnd > curStart && char.IsWhiteSpace(text[sEnd - 1]))
                    {
                        sEnd--;
                    }

                    if (sEnd > curStart)
                    {
                        result.Add((curStart, sEnd));
                    }

                    // Advance to next sentence start
                    curStart = p;
                    while (curStart < end && char.IsWhiteSpace(text[curStart]))
                    {
                        curStart++;
                    }
                    i = curStart - 1;
                }
            }
        }

        if (curStart < end)
        {
            int sEnd = end;
            while (sEnd > curStart && char.IsWhiteSpace(text[sEnd - 1]))
            {
                sEnd--;
            }
            if (sEnd > curStart)
            {
                result.Add((curStart, sEnd));
            }
        }

        return result;
    }

    private static List<(int Start, int End)> ScanClauses(string text, int start, int end)
    {
        var result = new List<(int Start, int End)>();
        int curStart = start;

        for (int i = start; i < end; i++)
        {
            char c = text[i];
            // Clause delimiters: ; : , —
            if (c == ';' || c == ':' || c == ',' || c == '—')
            {
                int p = i + 1;
                if (p < end && char.IsWhiteSpace(text[p]))
                {
                    int cEnd = p;
                    while (cEnd > curStart && char.IsWhiteSpace(text[cEnd - 1]))
                    {
                        cEnd--;
                    }

                    if (cEnd > curStart)
                    {
                        result.Add((curStart, cEnd));
                    }

                    curStart = p;
                    while (curStart < end && char.IsWhiteSpace(text[curStart]))
                    {
                        curStart++;
                    }
                    i = curStart - 1;
                }
            }
        }

        if (curStart < end)
        {
            int cEnd = end;
            while (cEnd > curStart && char.IsWhiteSpace(text[cEnd - 1]))
            {
                cEnd--;
            }
            if (cEnd > curStart)
            {
                result.Add((curStart, cEnd));
            }
        }

        return result;
    }

    private static int AdjustForSurrogatePair(string text, int cut)
    {
        if (cut <= 0 || cut >= text.Length)
        {
            return cut;
        }

        // If cut falls between high and low surrogate, advance +1 to keep pair intact (RULE-C7-SEC-06)
        if (char.IsHighSurrogate(text[cut - 1]) && char.IsLowSurrogate(text[cut]))
        {
            return cut + 1;
        }

        return cut;
    }

    private static bool IsHeadingLine(string trimmed, out int hashCount)
    {
        hashCount = 0;
        while (hashCount < trimmed.Length && trimmed[hashCount] == '#')
        {
            hashCount++;
        }

        if (hashCount >= 1 && hashCount <= 6 && hashCount < trimmed.Length &&
            (trimmed[hashCount] == ' ' || trimmed[hashCount] == '\t'))
        {
            return true;
        }

        return false;
    }

    private static bool IsTableLine(string trimmed)
    {
        return trimmed.Contains('|') &&
               (trimmed.StartsWith("|") || trimmed.EndsWith("|") || trimmed.Split('|').Length >= 3);
    }

    private static bool IsListItemLine(string trimmed)
    {
        if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ "))
        {
            return true;
        }

        if (trimmed.Length >= 3 && char.IsDigit(trimmed[0]))
        {
            int idx = 1;
            while (idx < trimmed.Length && char.IsDigit(trimmed[idx]))
            {
                idx++;
            }
            if (idx < trimmed.Length - 1 && (trimmed[idx] == '.' || trimmed[idx] == ')') && trimmed[idx + 1] == ' ')
            {
                return true;
            }
        }

        return false;
    }

    private sealed record TextLine(
        int Start,
        int ContentEnd,
        int TotalEnd,
        string Content);

    private static List<TextLine> SplitIntoLines(string text)
    {
        var lines = new List<TextLine>();
        int start = 0;
        int len = text.Length;

        while (start < len)
        {
            int i = start;
            while (i < len && text[i] != '\r' && text[i] != '\n')
            {
                i++;
            }

            int contentEnd = i;
            int totalEnd = i;

            if (i < len)
            {
                if (text[i] == '\r')
                {
                    totalEnd++;
                    if (totalEnd < len && text[totalEnd] == '\n')
                    {
                        totalEnd++;
                    }
                }
                else if (text[i] == '\n')
                {
                    totalEnd++;
                }
            }

            lines.Add(new TextLine(start, contentEnd, totalEnd, text.Substring(start, contentEnd - start)));
            start = totalEnd;
        }

        return lines;
    }

    #endregion

    #region Bounded Fallback

    private static IReadOnlyList<DocumentPassageChunk> CreateBoundedFallbackChunks(
        string documentId,
        int pageNumber,
        string pageText,
        int targetSize)
    {
        var fallback = new List<DocumentPassageChunk>();
        int start = 0;
        int len = pageText.Length;
        int index = 0;

        while (start < len && fallback.Count < MaxPassagesPerPage)
        {
            while (start < len && char.IsWhiteSpace(pageText[start]))
            {
                start++;
            }
            if (start >= len)
            {
                break;
            }

            int end = Math.Min(len, start + targetSize);
            end = AdjustForSurrogatePair(pageText, end);
            while (end > start && char.IsWhiteSpace(pageText[end - 1]))
            {
                end--;
            }

            int charLength = end - start;
            if (charLength > 0)
            {
                fallback.Add(new DocumentPassageChunk
                {
                    ChunkId = index,
                    DocumentId = documentId ?? string.Empty,
                    PageNumber = pageNumber,
                    ChunkIndex = index,
                    Text = pageText.Substring(start, charLength),
                    StartCharOffset = start,
                    EndCharOffset = end,
                    CharLength = charLength,
                    EmbeddingStatus = PassageEmbeddingStatus.NoEmbedding,
                    Embedding = null
                });
                index++;
            }
            start = end;
        }

        return fallback;
    }

    #endregion
}
