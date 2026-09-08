using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Deterministic, stateless, and thread-safe engine for bounded context window formulation.
/// Implements Mode A (page-local NormalizedText span slicing), Mode B (raw text fallback),
/// and Mode C (multi-passage composite prompt packing with cryptographic SHA-256 identity).
/// </summary>
public sealed class BoundedContextWindowBuilder : IBoundedContextWindowBuilder
{
    /// <inheritdoc/>
    public BoundedContextWindow FormulateFocalWindow(
        DocumentPassageChunk focalChunk,
        DocumentPage page,
        ContextWindowOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(focalChunk);
        ArgumentNullException.ThrowIfNull(page);

        var effectiveOptions = options ?? new ContextWindowOptions();
        effectiveOptions.Validate();

        return FormulateFocalWindowInternal(focalChunk, page, effectiveOptions, overrideDocId: null);
    }

    /// <inheritdoc/>
    public IReadOnlyList<BoundedContextWindow> FormulatePageWindows(
        DocumentPage page,
        string documentId,
        ContextWindowOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(documentId);

        var effectiveOptions = options ?? new ContextWindowOptions();
        effectiveOptions.Validate();

        if (page.Chunks == null || page.Chunks.Count == 0)
        {
            return [];
        }

        var result = new List<BoundedContextWindow>(page.Chunks.Count);
        for (int k = 0; k < page.Chunks.Count; k++)
        {
            var chunk = page.Chunks[k];
            var window = FormulateFocalWindowInternal(chunk, page, effectiveOptions, overrideDocId: documentId);
            result.Add(window);
        }

        return result;
    }

    /// <inheritdoc/>
    public BoundedContextWindow FormulateCompositeWindow(
        IEnumerable<DocumentPassageChunk> retrievedChunks,
        ScholarDocument? document = null,
        ContextWindowOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(retrievedChunks);

        var effectiveOptions = options ?? new ContextWindowOptions();
        effectiveOptions.Validate();

        var rawList = retrievedChunks.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Text)).ToList();
        if (rawList.Count == 0)
        {
            return new BoundedContextWindow
            {
                WindowId = "comp_empty",
                DocumentId = document?.DocumentId ?? "composite",
                PageNumber = 0,
                FocalChunk = null,
                ConstituentChunks = [],
                ConstituentChunkIndices = [],
                FormattedText = string.Empty,
                StartCharOffset = -1,
                EndCharOffset = -1,
                Citations = [],
                IsTruncated = false
            };
        }

        // Deduplicate using composite key: (DocumentId, PageNumber, ChunkIndex)
        var seen = new HashSet<(string, int, int)>();
        var distinctList = new List<DocumentPassageChunk>();
        foreach (var chunk in rawList)
        {
            var key = (chunk.DocumentId ?? string.Empty, chunk.PageNumber, chunk.ChunkIndex);
            if (seen.Add(key))
            {
                distinctList.Add(chunk);
            }
        }

        // Ordering policy
        List<DocumentPassageChunk> orderedList;
        if (effectiveOptions.OrderingMode == CompositeOrderingMode.DocumentReadingOrder)
        {
            orderedList = distinctList
                .OrderBy(c => c.DocumentId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(c => c.PageNumber)
                .ThenBy(c => c.ChunkIndex)
                .ToList();
        }
        else
        {
            orderedList = distinctList;
        }

        // Greedy budget accumulation
        var sb = new StringBuilder();
        var constituents = new List<DocumentPassageChunk>();
        var citations = new List<StudyCitation>();
        bool isTruncated = false;

        for (int i = 0; i < orderedList.Count; i++)
        {
            var c = orderedList[i];
            string prefix;
            if (effectiveOptions.IncludeProvenanceHeaders)
            {
                prefix = $"\n\n--- [Page {c.PageNumber}, Passage {c.ChunkIndex}] ---\n";
            }
            else
            {
                prefix = (i == 0) ? string.Empty : "\n\n";
            }

            if (i == 0)
            {
                if (prefix.Length + c.Text.Length <= effectiveOptions.CompositeBudgetChars)
                {
                    sb.Append(prefix);
                    sb.Append(c.Text);
                    constituents.Add(c);
                    citations.Add(CreateCitation(c, effectiveOptions.DefaultFileName));
                }
                else
                {
                    int availableTextBudget = effectiveOptions.CompositeBudgetChars - prefix.Length;
                    if (availableTextBudget >= 50)
                    {
                        string truncatedText = TruncateOversizedText(c.Text, effectiveOptions.TargetWindowChars, availableTextBudget);
                        sb.Append(prefix);
                        sb.Append(truncatedText);
                        constituents.Add(c);
                        citations.Add(CreateCitation(c, effectiveOptions.DefaultFileName));
                        isTruncated = true;
                    }
                    else
                    {
                        // Omit passage #1, return empty bounded window
                        isTruncated = true;
                    }
                    break;
                }
            }
            else
            {
                int addedLength = prefix.Length + c.Text.Length;
                if (sb.Length + addedLength <= effectiveOptions.CompositeBudgetChars)
                {
                    sb.Append(prefix);
                    sb.Append(c.Text);
                    constituents.Add(c);
                    citations.Add(CreateCitation(c, effectiveOptions.DefaultFileName));
                }
                else
                {
                    isTruncated = true;
                    break;
                }
            }
        }

        // Composite WindowId formulation: full 64-hex lowercase SHA-256 digest
        string windowId;
        if (constituents.Count == 0)
        {
            windowId = "comp_empty";
        }
        else
        {
            string canonicalCoords = string.Join(";", constituents.Select(c => $"{c.DocumentId}:p{c.PageNumber}:c{c.ChunkIndex}"));
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalCoords));
            string hex = Convert.ToHexString(hashBytes).ToLowerInvariant();
            windowId = $"comp_{hex}";
        }

        // DocumentId resolution
        string resolvedDocId;
        if (document != null && !string.IsNullOrEmpty(document.DocumentId))
        {
            resolvedDocId = document.DocumentId;
        }
        else
        {
            if (constituents.Count > 0 && constituents.All(c => c.DocumentId == constituents[0].DocumentId && !string.IsNullOrEmpty(c.DocumentId)))
            {
                resolvedDocId = constituents[0].DocumentId;
            }
            else
            {
                resolvedDocId = "composite";
            }
        }

        return new BoundedContextWindow
        {
            WindowId = windowId,
            DocumentId = resolvedDocId,
            PageNumber = 0,
            FocalChunk = null,
            ConstituentChunks = constituents,
            ConstituentChunkIndices = constituents.Select(c => c.ChunkIndex).ToList(),
            FormattedText = sb.ToString(),
            StartCharOffset = -1,
            EndCharOffset = -1,
            Citations = citations,
            IsTruncated = isTruncated
        };
    }

    private static BoundedContextWindow FormulateFocalWindowInternal(
        DocumentPassageChunk focalChunk,
        DocumentPage page,
        ContextWindowOptions effectiveOptions,
        string? overrideDocId)
    {
        string docId = !string.IsNullOrEmpty(overrideDocId)
            ? overrideDocId
            : (!string.IsNullOrEmpty(focalChunk.DocumentId) ? focalChunk.DocumentId : "doc");

        var chunks = page.Chunks;
        int k = -1;
        if (chunks != null && chunks.Count > 0)
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                if (ReferenceEquals(chunks[i], focalChunk) ||
                    (chunks[i].ChunkIndex == focalChunk.ChunkIndex &&
                     chunks[i].PageNumber == focalChunk.PageNumber &&
                     (string.IsNullOrEmpty(focalChunk.DocumentId) || chunks[i].DocumentId == focalChunk.DocumentId)))
                {
                    k = i;
                    break;
                }
            }
        }

        bool isModeA = page.NormalizedText != null;

        // Orphan chunk handling
        if (k == -1 || chunks == null || chunks.Count == 0)
        {
            bool isTrunc = false;
            string text = focalChunk.Text;
            int startOffset = isModeA ? focalChunk.StartCharOffset : -1;
            int endOffset = isModeA ? focalChunk.EndCharOffset : -1;

            if (focalChunk.CharLength > effectiveOptions.MaxWindowChars)
            {
                text = TruncateOversizedText(focalChunk.Text, effectiveOptions.TargetWindowChars, effectiveOptions.MaxWindowChars);
                isTrunc = true;
                if (isModeA)
                {
                    endOffset = startOffset + text.Length;
                }
            }

            string winId = isModeA
                ? $"win_{docId}_p{page.PageNumber}_f{focalChunk.ChunkIndex}_c{focalChunk.ChunkIndex}_{focalChunk.ChunkIndex}"
                : $"win_{docId}_p{page.PageNumber}_f{focalChunk.ChunkIndex}_c{focalChunk.ChunkIndex}_{focalChunk.ChunkIndex}_fb";

            return new BoundedContextWindow
            {
                WindowId = winId,
                DocumentId = docId,
                PageNumber = page.PageNumber,
                FocalChunk = focalChunk,
                ConstituentChunks = [focalChunk],
                ConstituentChunkIndices = [focalChunk.ChunkIndex],
                FormattedText = text,
                StartCharOffset = startOffset,
                EndCharOffset = endOffset,
                Citations = [CreateCitation(focalChunk, effectiveOptions.DefaultFileName)],
                IsTruncated = isTrunc
            };
        }

        int M = chunks.Count;
        int kMin = Math.Max(0, k - effectiveOptions.PrecedingNeighborCount);
        int kMax = Math.Min(M - 1, k + effectiveOptions.SucceedingNeighborCount);

        // Oversized focal chunk alone
        if (focalChunk.CharLength > effectiveOptions.MaxWindowChars)
        {
            string truncatedText = TruncateOversizedText(focalChunk.Text, effectiveOptions.TargetWindowChars, effectiveOptions.MaxWindowChars);
            int startOffset = isModeA ? focalChunk.StartCharOffset : -1;
            int endOffset = isModeA ? focalChunk.StartCharOffset + truncatedText.Length : -1;
            string winId = isModeA
                ? $"win_{docId}_p{page.PageNumber}_f{k}_c{k}_{k}"
                : $"win_{docId}_p{page.PageNumber}_f{k}_c{k}_{k}_fb";

            return new BoundedContextWindow
            {
                WindowId = winId,
                DocumentId = docId,
                PageNumber = page.PageNumber,
                FocalChunk = focalChunk,
                ConstituentChunks = [focalChunk],
                ConstituentChunkIndices = [focalChunk.ChunkIndex],
                FormattedText = truncatedText,
                StartCharOffset = startOffset,
                EndCharOffset = endOffset,
                Citations = [CreateCitation(focalChunk, effectiveOptions.DefaultFileName)],
                IsTruncated = true
            };
        }

        // Alternating greedy expansion sequence: (k - 1), (k + 1), (k - 2), (k + 2), ...
        int curMin = k;
        int curMax = k;
        int maxDist = Math.Max(effectiveOptions.PrecedingNeighborCount, effectiveOptions.SucceedingNeighborCount);

        for (int d = 1; d <= maxDist; d++)
        {
            int prec = k - d;
            if (prec >= kMin)
            {
                if (!EvaluateCandidate(prec, ref curMin, ref curMax, chunks, isModeA, page.NormalizedText, effectiveOptions))
                {
                    break;
                }
            }

            int succ = k + d;
            if (succ <= kMax)
            {
                if (!EvaluateCandidate(succ, ref curMin, ref curMax, chunks, isModeA, page.NormalizedText, effectiveOptions))
                {
                    break;
                }
            }
        }

        var constituentChunks = new List<DocumentPassageChunk>();
        var constituentIndices = new List<int>();
        var citations = new List<StudyCitation>();
        for (int ci = curMin; ci <= curMax; ci++)
        {
            constituentChunks.Add(chunks[ci]);
            constituentIndices.Add(chunks[ci].ChunkIndex);
            citations.Add(CreateCitation(chunks[ci], effectiveOptions.DefaultFileName));
        }

        if (isModeA)
        {
            int startOffset = chunks[curMin].StartCharOffset;
            int endOffset = chunks[curMax].EndCharOffset;
            string formattedText;
            if (effectiveOptions.DeduplicateOverlaps)
            {
                formattedText = page.NormalizedText!.Substring(startOffset, endOffset - startOffset);
            }
            else
            {
                formattedText = string.Join(" ", constituentChunks.Select(c => c.Text));
            }

            string windowId = $"win_{docId}_p{page.PageNumber}_f{k}_c{curMin}_{curMax}";

            return new BoundedContextWindow
            {
                WindowId = windowId,
                DocumentId = docId,
                PageNumber = page.PageNumber,
                FocalChunk = focalChunk,
                ConstituentChunks = constituentChunks,
                ConstituentChunkIndices = constituentIndices,
                FormattedText = formattedText,
                StartCharOffset = startOffset,
                EndCharOffset = endOffset,
                Citations = citations,
                IsTruncated = false
            };
        }
        else
        {
            string formattedText = ConcatenateModeBChunks(constituentChunks);
            string windowId = $"win_{docId}_p{page.PageNumber}_f{k}_c{curMin}_{curMax}_fb";

            return new BoundedContextWindow
            {
                WindowId = windowId,
                DocumentId = docId,
                PageNumber = page.PageNumber,
                FocalChunk = focalChunk,
                ConstituentChunks = constituentChunks,
                ConstituentChunkIndices = constituentIndices,
                FormattedText = formattedText,
                StartCharOffset = -1,
                EndCharOffset = -1,
                Citations = citations,
                IsTruncated = false
            };
        }
    }

    private static bool EvaluateCandidate(
        int candidateIndex,
        ref int curMin,
        ref int curMax,
        IReadOnlyList<DocumentPassageChunk> chunks,
        bool isModeA,
        string? normalizedText,
        ContextWindowOptions options)
    {
        int newMin = Math.Min(curMin, candidateIndex);
        int newMax = Math.Max(curMax, candidateIndex);

        int spanLength;
        if (isModeA && normalizedText != null)
        {
            spanLength = chunks[newMax].EndCharOffset - chunks[newMin].StartCharOffset;
        }
        else
        {
            spanLength = 0;
            for (int i = newMin; i <= newMax; i++)
            {
                spanLength += chunks[i].CharLength;
            }
            spanLength += (newMax - newMin); // Delimiter whitespace
        }

        if (spanLength <= options.TargetWindowChars)
        {
            // Case A: Accept candidate and continue
            curMin = newMin;
            curMax = newMax;
            return true;
        }
        else if (spanLength <= options.MaxWindowChars)
        {
            // Case B: Accept candidate as FINAL expansion and STOP immediately
            curMin = newMin;
            curMax = newMax;
            return false;
        }
        else
        {
            // Case C: spanLength > MaxWindowChars
            // Reject candidate and STOP immediately
            return false;
        }
    }

    private static string TruncateOversizedText(string text, int targetChars, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
        {
            return text ?? string.Empty;
        }

        // Tier 1: Search backwards from maxChars for sentence boundary ('.', '?', '!') followed by whitespace or end of string
        int bestSentenceCut = -1;
        for (int i = maxChars - 1; i >= 0; i--)
        {
            char ch = text[i];
            if (ch == '.' || ch == '?' || ch == '!')
            {
                if (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1]))
                {
                    bestSentenceCut = i + 1; // Include punctuation
                    break;
                }
            }
        }

        if (bestSentenceCut >= targetChars && bestSentenceCut <= maxChars)
        {
            return text[..bestSentenceCut].TrimEnd();
        }

        // Tier 2: Search backwards from maxChars for word boundary (whitespace)
        int bestWordCut = -1;
        for (int i = maxChars - 1; i >= 0; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                bestWordCut = i;
                break;
            }
        }

        if (bestWordCut > 0 && bestWordCut > bestSentenceCut)
        {
            return text[..bestWordCut].TrimEnd();
        }

        if (bestSentenceCut > 0)
        {
            return text[..bestSentenceCut].TrimEnd();
        }

        // Tier 3: Hard UTF-16-safe character slice at maxChars
        int hardSliceLength = maxChars;
        if (hardSliceLength > 0 && hardSliceLength < text.Length)
        {
            if (char.IsHighSurrogate(text[hardSliceLength - 1]))
            {
                hardSliceLength--;
            }
        }

        return text[..hardSliceLength];
    }

    private static string ConcatenateModeBChunks(IReadOnlyList<DocumentPassageChunk> chunks)
    {
        if (chunks.Count == 0) return string.Empty;
        if (chunks.Count == 1) return chunks[0].Text;

        var sb = new StringBuilder(chunks[0].Text);
        for (int i = 1; i < chunks.Count; i++)
        {
            string prev = chunks[i - 1].Text;
            string curr = chunks[i].Text;

            int overlap = 0;
            int maxOverlap = Math.Min(60, Math.Min(prev.Length, curr.Length));
            for (int len = maxOverlap; len > 0; len--)
            {
                if (prev.EndsWith(curr[..len], StringComparison.Ordinal))
                {
                    overlap = len;
                    break;
                }
            }

            if (overlap > 0)
            {
                sb.Append(curr[overlap..]);
            }
            else
            {
                if (sb.Length > 0 && !char.IsWhiteSpace(sb[^1]) && (curr.Length > 0 && !char.IsWhiteSpace(curr[0])))
                {
                    sb.Append(' ');
                }
                sb.Append(curr);
            }
        }

        return sb.ToString();
    }

    private static StudyCitation CreateCitation(DocumentPassageChunk chunk, string defaultFileName)
    {
        return new StudyCitation
        {
            DocumentId = chunk.DocumentId ?? string.Empty,
            FileName = defaultFileName ?? string.Empty,
            PageNumber = chunk.PageNumber,
            ChunkIndex = chunk.ChunkIndex,
            MatchedSnippet = chunk.Text?.Length > 120 ? chunk.Text[..120] : (chunk.Text ?? string.Empty)
        };
    }
}
