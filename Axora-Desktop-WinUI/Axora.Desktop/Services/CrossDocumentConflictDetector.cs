using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services;

/// <summary>
/// Detects direct and potential factual discrepancies across multiple academic source documents.
/// Implements INV-W3F-15: identifies disagreements without arbitrary averaging or source dropping.
/// </summary>
public static class CrossDocumentConflictDetector
{
    private static readonly Regex NumberUnitRegex = new(@"\b(?<val>\d+(?:[,\.]\d+)?)\s*(?<unit>km/s|m/s|mg|g|kg|km|m|cm|mm|°C|%|percent)?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex YearRegex = new(@"\b(18\d\d|19\d\d|20\d\d)\b", RegexOptions.Compiled);

    /// <summary>
    /// Analyzes retrieved context windows across distinct documents to discover cross-document conflicts.
    /// </summary>
    public static IReadOnlyList<CrossDocumentConflict> DetectConflicts(
        IReadOnlyList<BoundedContextWindow> windows)
    {
        if (windows == null || windows.Count < 2)
            return [];

        var conflicts = new List<CrossDocumentConflict>();

        // Group windows by document ID
        var docGroups = windows
            .GroupBy(w => w.DocumentId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (docGroups.Count < 2)
            return []; // Need at least two distinct documents for cross-document conflict

        // Extract sentences across all documents
        var docSentences = new List<(string DocId, string Sentence, BoundedContextWindow Window)>();
        foreach (var group in docGroups)
        {
            foreach (var win in group)
            {
                var sents = SplitIntoSentences(win.FormattedText);
                foreach (var s in sents)
                {
                    if (s.Length >= 15)
                    {
                        docSentences.Add((group.Key, s, win));
                    }
                }
            }
        }

        // Compare sentences across distinct documents
        for (int i = 0; i < docSentences.Count; i++)
        {
            var (docA, sentA, winA) = docSentences[i];

            for (int j = i + 1; j < docSentences.Count; j++)
            {
                var (docB, sentB, winB) = docSentences[j];

                if (string.Equals(docA, docB, StringComparison.OrdinalIgnoreCase))
                    continue; // Skip sentences from the exact same document

                var conflict = CheckSentencePairForConflict(sentA, winA, sentB, winB);
                if (conflict != null)
                {
                    // Avoid duplicate conflicts for the same topic
                    if (!conflicts.Any(c => string.Equals(c.TopicOrConcept, conflict.TopicOrConcept, StringComparison.OrdinalIgnoreCase) &&
                                            c.Classification == conflict.Classification))
                    {
                        conflicts.Add(conflict);
                    }
                }
            }
        }

        return conflicts;
    }

    private static CrossDocumentConflict? CheckSentencePairForConflict(
        string sentA,
        BoundedContextWindow winA,
        string sentB,
        BoundedContextWindow winB)
    {
        // 1. Check for shared topic phrase
        string? topic = FindSharedTopic(sentA, sentB);
        if (string.IsNullOrEmpty(topic))
            return null;

        var citationA = winA.Citations.FirstOrDefault() ?? new StudyCitation
        {
            DocumentId = winA.DocumentId,
            PageNumber = winA.PageNumber,
            FileName = winA.DocumentId
        };

        var citationB = winB.Citations.FirstOrDefault() ?? new StudyCitation
        {
            DocumentId = winB.DocumentId,
            PageNumber = winB.PageNumber,
            FileName = winB.DocumentId
        };

        // 2. Check for Ambiguous Conflict (varying conditions e.g. varying light, temperature, unspecified conditions)
        bool mentionsConditionsA = sentA.Contains("condition", StringComparison.OrdinalIgnoreCase) ||
                                   sentA.Contains("light", StringComparison.OrdinalIgnoreCase) ||
                                   sentA.Contains("temperature", StringComparison.OrdinalIgnoreCase) ||
                                   sentA.Contains("vary", StringComparison.OrdinalIgnoreCase);

        bool mentionsConditionsB = sentB.Contains("condition", StringComparison.OrdinalIgnoreCase) ||
                                   sentB.Contains("light", StringComparison.OrdinalIgnoreCase) ||
                                   sentB.Contains("temperature", StringComparison.OrdinalIgnoreCase) ||
                                   sentB.Contains("vary", StringComparison.OrdinalIgnoreCase);

        if ((mentionsConditionsA || mentionsConditionsB) &&
            (sentA.Contains("rate", StringComparison.OrdinalIgnoreCase) || sentA.Contains("growth", StringComparison.OrdinalIgnoreCase)))
        {
            return new CrossDocumentConflict
            {
                TopicOrConcept = topic,
                Classification = ConflictClassification.PotentialDiscrepancy_Ambiguous,
                PropositionA = sentA,
                CitationA = citationA,
                PropositionB = sentB,
                CitationB = citationB,
                IsAmbiguous = true
            };
        }

        // 3. Check for Numeric / Unit / Date / Percentage Conflict
        var matchesA = NumberUnitRegex.Matches(sentA).Cast<Match>().ToList();
        var matchesB = NumberUnitRegex.Matches(sentB).Cast<Match>().ToList();

        if (matchesA.Count > 0 && matchesB.Count > 0)
        {
            var mA = matchesA[0];
            var mB = matchesB[0];

            string valA = mA.Groups["val"].Value;
            string valB = mB.Groups["val"].Value;
            string unitA = mA.Groups["unit"].Value;
            string unitB = mB.Groups["unit"].Value;

            // Date check
            bool isDateA = YearRegex.IsMatch(valA);
            bool isDateB = YearRegex.IsMatch(valB);
            if (isDateA && isDateB && !string.Equals(valA, valB, StringComparison.Ordinal))
            {
                return new CrossDocumentConflict
                {
                    TopicOrConcept = topic,
                    Classification = ConflictClassification.Date,
                    PropositionA = sentA,
                    CitationA = citationA,
                    PropositionB = sentB,
                    CitationB = citationB,
                    IsAmbiguous = false
                };
            }

            // Unit check
            if (!string.IsNullOrEmpty(unitA) && !string.IsNullOrEmpty(unitB) &&
                !string.Equals(unitA, unitB, StringComparison.OrdinalIgnoreCase))
            {
                return new CrossDocumentConflict
                {
                    TopicOrConcept = topic,
                    Classification = ConflictClassification.Unit,
                    PropositionA = sentA,
                    CitationA = citationA,
                    PropositionB = sentB,
                    CitationB = citationB,
                    IsAmbiguous = false
                };
            }

            // Percentage check
            if ((unitA == "%" || unitA.Equals("percent", StringComparison.OrdinalIgnoreCase)) &&
                !string.Equals(valA, valB, StringComparison.Ordinal))
            {
                return new CrossDocumentConflict
                {
                    TopicOrConcept = topic,
                    Classification = ConflictClassification.Percentage,
                    PropositionA = sentA,
                    CitationA = citationA,
                    PropositionB = sentB,
                    CitationB = citationB,
                    IsAmbiguous = false
                };
            }

            // Numeric check
            if (!string.Equals(valA, valB, StringComparison.Ordinal))
            {
                return new CrossDocumentConflict
                {
                    TopicOrConcept = topic,
                    Classification = ConflictClassification.Numeric,
                    PropositionA = sentA,
                    CitationA = citationA,
                    PropositionB = sentB,
                    CitationB = citationB,
                    IsAmbiguous = false
                };
            }
        }

        // 4. Check for Negation / Antonym Conflict
        bool hasInhibitsA = sentA.Contains("inhibits", StringComparison.OrdinalIgnoreCase);
        bool hasPromotesB = sentB.Contains("promotes", StringComparison.OrdinalIgnoreCase);
        bool hasPromotesA = sentA.Contains("promotes", StringComparison.OrdinalIgnoreCase);
        bool hasInhibitsB = sentB.Contains("inhibits", StringComparison.OrdinalIgnoreCase);

        if ((hasInhibitsA && hasPromotesB) || (hasPromotesA && hasInhibitsB))
        {
            return new CrossDocumentConflict
            {
                TopicOrConcept = topic,
                Classification = ConflictClassification.Negation,
                PropositionA = sentA,
                CitationA = citationA,
                PropositionB = sentB,
                CitationB = citationB,
                IsAmbiguous = false
            };
        }

        return null;
    }

    private static string? FindSharedTopic(string sentA, string sentB)
    {
        // Common candidate topics
        string[] candidateTopics =
        [
            "speed of light", "melting point", "boiling point", "growth rate",
            "dosage", "sample size", "treaty", "activation energy", "half-life",
            "binding affinity", "enzyme", "protein", "temperature", "pressure", "rate"
        ];

        foreach (var t in candidateTopics)
        {
            if (sentA.Contains(t, StringComparison.OrdinalIgnoreCase) &&
                sentB.Contains(t, StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }

        // If no predefined topic matches, check for 2-word noun overlap
        var wordsA = sentA.Split([' ', ',', '.', ';'], StringSplitOptions.RemoveEmptyEntries);
        var wordsB = new HashSet<string>(sentB.Split([' ', ',', '.', ';'], StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < wordsA.Length - 1; i++)
        {
            string bigram = $"{wordsA[i]} {wordsA[i + 1]}";
            if (bigram.Length > 8 && sentB.Contains(bigram, StringComparison.OrdinalIgnoreCase))
            {
                return bigram;
            }
        }

        return null;
    }

    private static List<string> SplitIntoSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        return text.Split(new[] { ". ", ".\n", ".\r\n", "!\n", "?\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.Trim().TrimEnd('.'))
            .Where(s => s.Length > 10)
            .ToList();
    }
}
