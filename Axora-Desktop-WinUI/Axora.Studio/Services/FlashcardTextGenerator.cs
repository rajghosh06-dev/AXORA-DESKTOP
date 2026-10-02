using System.Text;
using Axora.Studio.Models;

namespace Axora.Studio.Services;

public sealed record FlashcardGeneration(FlashcardDeck? Deck, int IgnoredLines);

/// <summary>Bounded colon/adjacent-line transformation. No model, file access, or semantic summarization.</summary>
public sealed class FlashcardTextGenerator
{
    public FlashcardGeneration Generate(string text, string sourceLabel, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        token.ThrowIfCancellationRequested();
        if (text.Length > FlashcardLimits.InputBytes || FlashcardLimits.StrictUtf8.GetByteCount(text) > FlashcardLimits.InputBytes)
            throw new ArgumentException("Notes exceed the 1 MiB UTF-8 input limit.");
        if (string.IsNullOrWhiteSpace(text)) return new(null, 0);
        var lines = new List<string>();
        for (int start = 0, i = 0; i <= text.Length; i++)
        {
            if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
            if (i != text.Length && text[i] is not '\r' and not '\n') continue;
            if (i > start) lines.Add(text[start..i].Trim());
            start = i + 1;
        }
        var cards = new List<FlashCard>();
        int ignored = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            string line = lines[i];
            if (line.Length < 10) { ignored++; continue; }
            int colon = line.IndexOf(':');
            if (colon >= 0 && colon < 40)
            {
                string front = line[..colon].Trim(), back = line[(colon + 1)..].Trim();
                if (front.Length == 0 || back.Length == 0) { ignored++; continue; }
                Add(front, back);
            }
            else if (i + 1 < lines.Count && lines[i + 1].Length > 5) Add(line, lines[++i]);
            else ignored++;
        }
        if (cards.Count == 0)
        {
            var excerpt = new StringBuilder();
            int count = 0;
            foreach (var rune in text.EnumerateRunes())
            {
                if (count++ == 200) { excerpt.Append('…'); break; }
                excerpt.Append(rune.ToString());
            }
            Add("Document excerpt", excerpt.ToString());
        }
        string label = FlashcardLimits.Text(sourceLabel ?? "", 256).Replace('\\', '/').Split('/')[^1];
        label = new string(label.Where(c => !char.IsControl(c)).ToArray()).Trim();
        string title = label.Length == 0 ? "Structured notes" : "Notes: " + label;
        // The title has its own bound; never silently truncate source metadata.
        FlashcardLimits.Text(title, 256);
        token.ThrowIfCancellationRequested();
        return new(new FlashcardDeck(title, cards, "Rule-based cards from structured notes"), ignored);

        void Add(string front, string back)
        {
            if (cards.Count == FlashcardLimits.CardsPerDeck) throw new ArgumentException("Notes exceed 500 generated cards.");
            cards.Add(new FlashCard(front, back));
        }
    }
}
