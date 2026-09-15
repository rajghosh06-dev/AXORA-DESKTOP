using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services;

/// <summary>
/// Implements the Six-Layer Grounding & Factual Verification pipeline (Layers A through F).
/// Verifies synthesized statements against authentic retrieved context windows to prevent
/// hallucination, altered numbers/units/dates, inverted polarity, and ungrounded claims.
/// </summary>
public static class SixLayerGroundingVerifier
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "about", "above", "after", "again", "against", "all", "am", "an", "and",
        "any", "are", "as", "at", "be", "because", "been", "before", "being", "below",
        "between", "both", "but", "by", "can", "cannot", "could", "did", "do", "does",
        "doing", "down", "during", "each", "few", "for", "from", "further", "had", "has",
        "have", "having", "he", "her", "here", "hers", "herself", "him", "himself", "his",
        "how", "i", "if", "in", "into", "is", "it", "its", "itself", "me", "more",
        "most", "my", "myself", "no", "nor", "not", "of", "off", "on", "once", "only",
        "or", "other", "ought", "our", "ours", "ourselves", "out", "over", "own", "same",
        "she", "should", "so", "some", "such", "than", "that", "the", "their", "theirs",
        "them", "themselves", "then", "there", "these", "they", "this", "those", "through",
        "to", "too", "under", "until", "up", "very", "was", "we", "were", "what", "when",
        "where", "which", "while", "who", "whom", "why", "with", "would", "you", "your",
        "yours", "yourself", "yourselves"
    };

    private static readonly HashSet<string> SpeculativeIntensifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "catastrophically", "catastrophic", "unprecedentedly", "unprecedented",
        "infinitely", "miraculously", "astonishingly", "mind-bogglingly",
        "inconceivably", "dangerously", "dangerous", "disastrously", "astronomically"
    };

    private static readonly (string Term1, string Term2)[] AntonymPairs =
    [
        ("inhibits", "promotes"),
        ("promotes", "inhibits"),
        ("increases", "decreases"),
        ("decreases", "increases"),
        ("accelerates", "decelerates"),
        ("decelerates", "accelerates"),
        ("activates", "deactivates"),
        ("deactivates", "activates"),
        ("causes", "prevents"),
        ("prevents", "causes"),
        ("improves", "worsens"),
        ("worsens", "improves")
    ];

    private static readonly Regex NumberRegex = new(@"\b\d+(?:[,\.]\d+)?\b", RegexOptions.Compiled);
    private static readonly Regex UnitRegex = new(@"\b(?:mg|g|kg|km/s|m/s|km|m|cm|mm|°C|%|percent)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NegationRegex = new(@"\b(?:not|no|never|cannot|neither|nor|without|fails to|does not|did not|is not|was not)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CausalRegex = new(@"([A-Za-z0-9\s\-]+?)\s+(?:causes|leads to|results in|induces)\s+([A-Za-z0-9\s\-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ComparisonRegex = new(@"\b(?:\d+\s+times\s+more|much\s+better\s+than|far\s+superior\s+to|significantly\s+worse\s+than|infinitely\s+greater\s+than)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Evaluates a synthesized claim across all six grounding layers against retrieved context windows.
    /// </summary>
    public static ItemGroundingStatus VerifyClaim(
        string claimText,
        StudyCitation? citation,
        IReadOnlyList<BoundedContextWindow> contextWindows,
        IReadOnlyList<CrossDocumentConflict>? knownConflicts = null)
    {
        if (string.IsNullOrWhiteSpace(claimText))
            return ItemGroundingStatus.Unsupported;

        // Layer A: Citation / Reference Validity
        if (citation == null || string.IsNullOrWhiteSpace(citation.DocumentId))
            return ItemGroundingStatus.Unsupported;

        // Check if citation ID or (DocumentId, PageNumber) exists in authentic context windows
        var matchingWindow = contextWindows.FirstOrDefault(w =>
            (string.Equals(w.DocumentId, citation.DocumentId, StringComparison.OrdinalIgnoreCase) &&
             (w.PageNumber == citation.PageNumber || citation.PageNumber <= 0)) ||
            (!string.IsNullOrWhiteSpace(w.WindowId) && string.Equals(w.WindowId, citation.DocumentId, StringComparison.OrdinalIgnoreCase)));

        if (matchingWindow == null)
        {
            // Fabricated citation ID or non-existent document in scope (INV-W3F-12, TEST-W3F-ADV-09)
            return ItemGroundingStatus.Unsupported;
        }

        string sourceText = matchingWindow.FormattedText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sourceText))
            return ItemGroundingStatus.Unsupported;

        // Check if there is an active cross-document conflict for this topic
        if (knownConflicts != null && knownConflicts.Count > 0)
        {
            foreach (var conflict in knownConflicts)
            {
                if (!string.IsNullOrWhiteSpace(conflict.TopicOrConcept) &&
                    claimText.Contains(conflict.TopicOrConcept, StringComparison.OrdinalIgnoreCase))
                {
                    return ItemGroundingStatus.ConflictDetected;
                }
            }
        }

        // Layer B: Source-Span Containment / Substring Check
        // If the claim is a direct verbatim substring, it immediately satisfies Layer B.
        bool hasSpanContainment = sourceText.Contains(claimText.Trim(), StringComparison.OrdinalIgnoreCase);

        // Layer C: Lexical / Lemma Overlap (>= 60%)
        var claimTokens = ExtractContentTokens(claimText);
        var sourceTokens = new HashSet<string>(ExtractContentTokens(sourceText), StringComparer.OrdinalIgnoreCase);

        if (claimTokens.Count > 0)
        {
            int matchingTokens = claimTokens.Count(t => sourceTokens.Contains(t));
            float overlapRatio = (float)matchingTokens / claimTokens.Count;

            if (overlapRatio < 0.60f && !hasSpanContainment)
            {
                // Failed lemma overlap / wrong citation attached to claim (INV-W3F-14, TEST-W3F-ADV-10)
                return ItemGroundingStatus.Unsupported;
            }
        }

        // Layer D: Numeric, Entity & Unit Consistency
        // Check numbers
        var claimNumbers = NumberRegex.Matches(claimText).Cast<Match>().Select(m => m.Value).ToList();
        var sourceNumbers = new HashSet<string>(NumberRegex.Matches(sourceText).Cast<Match>().Select(m => m.Value), StringComparer.Ordinal);

        foreach (var num in claimNumbers)
        {
            if (!sourceNumbers.Contains(num))
            {
                // Altered number (e.g. 42 -> 45, 1914 -> 1917) (INV-W3F-13, TEST-W3F-ADV-01, TEST-W3F-ADV-03)
                return ItemGroundingStatus.Unsupported;
            }
        }

        // Check units
        var claimUnits = UnitRegex.Matches(claimText).Cast<Match>().Select(m => m.Value.ToLowerInvariant()).ToList();
        var sourceUnits = new HashSet<string>(UnitRegex.Matches(sourceText).Cast<Match>().Select(m => m.Value.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);

        foreach (var unit in claimUnits)
        {
            if (!sourceUnits.Contains(unit))
            {
                // Altered unit (e.g. mg -> g) (INV-W3F-13, TEST-W3F-ADV-02)
                return ItemGroundingStatus.Unsupported;
            }
        }

        // Check named entities (capitalized scientific words or specific nouns not in stopwords)
        var entityMatches = Regex.Matches(claimText, @"\b[A-Z][a-z0-9\-]+\b")
            .Cast<Match>()
            .Select(m => m.Value)
            .Where(e => !StopWords.Contains(e))
            .ToList();

        foreach (var entity in entityMatches)
        {
            if (!sourceText.Contains(entity, StringComparison.OrdinalIgnoreCase))
            {
                // Entity substitution (e.g. hemoglobin -> myoglobin) (INV-W3F-13, TEST-W3F-ADV-06)
                return ItemGroundingStatus.Unsupported;
            }
        }

        // Check uncited substantive tokens (not in source and not speculative intensifiers)
        var uncitedTokens = claimTokens
            .Where(t => !sourceTokens.Contains(t) && !SpeculativeIntensifiers.Contains(t))
            .ToList();

        if (uncitedTokens.Count > 0 && !hasSpanContainment)
        {
            // Uncited entity / substantive token substitution (INV-W3F-13, TEST-W3F-ADV-06)
            return ItemGroundingStatus.Unsupported;
        }

        // Layer E: Negation & Causal Direction Verification
        bool claimHasNegation = NegationRegex.IsMatch(claimText);
        bool sourceHasNegation = NegationRegex.IsMatch(sourceText);

        if (claimHasNegation && !sourceHasNegation)
        {
            // Claim introduced negation not found in source (INV-W3F-14, TEST-W3F-ADV-04)
            return ItemGroundingStatus.Unsupported;
        }
        else if (!claimHasNegation && sourceHasNegation)
        {
            var negMatch = Regex.Match(sourceText, @"\b(?:not|never|cannot|neither|nor|without|fails to|does not|did not|is not|was not|no)\s+([A-Za-z0-9\-]+)", RegexOptions.IgnoreCase);
            if (negMatch.Success)
            {
                string negatedWord = negMatch.Groups[1].Value.ToLowerInvariant();
                if (claimTokens.Contains(negatedWord))
                {
                    return ItemGroundingStatus.Unsupported;
                }
            }
        }

        // Check antonymic predicate inversion (e.g. inhibits vs promotes)
        foreach (var (term1, term2) in AntonymPairs)
        {
            if (sourceText.Contains(term1, StringComparison.OrdinalIgnoreCase) &&
                claimText.Contains(term2, StringComparison.OrdinalIgnoreCase))
            {
                // Inverted antonym (INV-W3F-14, TEST-W3F-ADV-04)
                return ItemGroundingStatus.Unsupported;
            }
        }

        // Check reversed causal direction
        var claimCausal = CausalRegex.Match(claimText);
        var sourceCausal = CausalRegex.Match(sourceText);
        if (claimCausal.Success && sourceCausal.Success)
        {
            var claimCauseTokens = ExtractContentTokens(claimCausal.Groups[1].Value);
            var claimEffectTokens = ExtractContentTokens(claimCausal.Groups[2].Value);
            var sourceCauseTokens = ExtractContentTokens(sourceCausal.Groups[1].Value);
            var sourceEffectTokens = ExtractContentTokens(sourceCausal.Groups[2].Value);

            bool causeMatchesEffect = claimCauseTokens.Any(t => sourceEffectTokens.Contains(t));
            bool effectMatchesCause = claimEffectTokens.Any(t => sourceCauseTokens.Contains(t));

            if (causeMatchesEffect && effectMatchesCause)
            {
                // Reversed causal arrow (INV-W3F-14, TEST-W3F-ADV-05)
                return ItemGroundingStatus.Unsupported;
            }
        }

        // Layer F: Speculative Intensifiers, Comparisons & Unsupported Rejection
        if (ComparisonRegex.IsMatch(claimText) && !ComparisonRegex.IsMatch(sourceText))
        {
            // Unsupported comparison (e.g. 10 times more active than) (TEST-W3F-ADV-08)
            return ItemGroundingStatus.Unsupported;
        }

        var words = claimText.Split([' ', ',', '.', '!', '?', ';', ':'], StringSplitOptions.RemoveEmptyEntries);
        bool hasSpeculativeIntensifier = words.Any(w => SpeculativeIntensifiers.Contains(w));
        if (hasSpeculativeIntensifier)
        {
            bool sourceHasIntensifier = words.Where(w => SpeculativeIntensifiers.Contains(w))
                .All(w => sourceText.Contains(w, StringComparison.OrdinalIgnoreCase));

            if (!sourceHasIntensifier)
            {
                // Speculative intensifier present only in claim (INV-W3F-26, TEST-W3F-ADV-07)
                return ItemGroundingStatus.PartiallyGrounded;
            }
        }

        return ItemGroundingStatus.Grounded;
    }

    private static List<string> ExtractContentTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        return Regex.Matches(text, @"\b[A-Za-z0-9\-]+\b")
            .Cast<Match>()
            .Select(m => m.Value.ToLowerInvariant())
            .Where(t => t.Length > 1 && !StopWords.Contains(t))
            .ToList();
    }
}
