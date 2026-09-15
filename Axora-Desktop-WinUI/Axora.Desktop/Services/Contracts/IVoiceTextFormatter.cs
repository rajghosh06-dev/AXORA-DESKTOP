namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Formats raw speech-to-text tokens into natural written prose with punctuation,
/// capitalization, formatting commands, and disfluency suppression.
/// </summary>
public interface IVoiceTextFormatter
{
    /// <summary>
    /// Transforms raw spoken text into clean, punctuated prose with spoken commands applied.
    /// </summary>
    string FormatSpokenChunk(string rawSpokenText, bool isStartOfSentence = false);

    /// <summary>
    /// Removes conversational filler tokens ("um", "uh", "ah") that occur as isolated words.
    /// </summary>
    string CleanDisfluencies(string text);

    /// <summary>
    /// Replaces spoken punctuation commands ("period", "comma", "new line", "new paragraph", etc.)
    /// with actual punctuation symbols and line breaks.
    /// </summary>
    string ApplyPunctuationCommands(string text);
}
