using System;
using System.Text;
using System.Text.RegularExpressions;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Deterministic text formatter for voice dictation.
/// Replaces spoken punctuation commands, cleans disfluencies, normalizes spacing,
/// and applies sentence capitalization without non-deterministic or generative rewriting.
/// </summary>
public sealed class VoiceTextFormatter : IVoiceTextFormatter
{
    private static readonly Regex DisfluencyRegex = new(
        @"\b(um|uh|ah|er|erm)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MultipleSpacesRegex = new(
        @"[ \t]+",
        RegexOptions.Compiled);

    private static readonly Regex SpaceBeforePunctuationRegex = new(
        @"\s+([,.:;?!])",
        RegexOptions.Compiled);

    public string FormatSpokenChunk(string rawSpokenText, bool isStartOfSentence = false)
    {
        if (string.IsNullOrWhiteSpace(rawSpokenText))
            return string.Empty;

        // 1. Remove isolated conversational fillers
        string text = CleanDisfluencies(rawSpokenText);

        // 2. Convert spoken punctuation commands
        text = ApplyPunctuationCommands(text);

        // 3. Clean up spacing around punctuation and newlines
        text = SpaceBeforePunctuationRegex.Replace(text, "$1");
        text = Regex.Replace(text, @"[ \t]*\n[ \t]*", "\n");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        text = MultipleSpacesRegex.Replace(text, " ");
        text = text.Trim();

        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // 4. Capitalize sentence start if indicated
        if (isStartOfSentence && char.IsLower(text[0]))
        {
            text = char.ToUpperInvariant(text[0]) + text[1..];
        }

        // 5. Capitalize letters immediately following sentence terminators (. ! ?)
        text = Regex.Replace(text, @"([.?!]\s+)([a-z])", m => m.Groups[1].Value + m.Groups[2].Value.ToUpperInvariant());

        return text;
    }

    public string CleanDisfluencies(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string cleaned = DisfluencyRegex.Replace(text, string.Empty);
        cleaned = MultipleSpacesRegex.Replace(cleaned, " ");
        return cleaned.Trim();
    }

    public string ApplyPunctuationCommands(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Replace multi-word spoken commands first, then single-word commands
        string result = Regex.Replace(text, @"\bnew paragraph\b", "\n\n", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\b(new line|newline)\b", "\n", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\b(full stop|period)\b", ".", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bcomma\b", ",", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\b(question mark)\b", "?", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\b(exclamation mark|exclamation point)\b", "!", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bcolon\b", ":", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bsemicolon\b", ";", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\b(hyphen|dash)\b", "-", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\b(open quote|close quote|quote)\b", "\"", RegexOptions.IgnoreCase);

        return result;
    }
}
