using System.Text;
using Axora.Studio.Models;

namespace Axora.Studio.Services;

public static class ExportFileNameSanitizer
{
    public static string Extension(FlashcardExportFormat format) => format switch
    {
        FlashcardExportFormat.Csv => ".csv", FlashcardExportFormat.AnkiText => ".txt",
        FlashcardExportFormat.AxoraJson => ".json", _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
    public static bool IsReserved(string stem)
    {
        string first = stem.Split('.')[0].TrimEnd(' ', '.').ToUpperInvariant();
        return first is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$"
            || first.Length == 4 && (first.StartsWith("COM") || first.StartsWith("LPT"))
                && "123456789¹²³".Contains(first[3]);
    }
    public static string Suggest(string title, FlashcardExportFormat format)
    {
        ArgumentNullException.ThrowIfNull(title); _ = FlashcardLimits.StrictUtf8.GetByteCount(title);
        string suffix = Extension(format); var clean = new StringBuilder(); bool replacement = false;
        foreach (var rune in title.Normalize(NormalizationForm.FormC).EnumerateRunes())
        {
            bool unsafeRune = Rune.IsControl(rune) || rune.Value < 128 && "<>:\"/\\|?*".Contains((char)rune.Value);
            if (unsafeRune) { if (!replacement) clean.Append('_'); replacement = true; }
            else { clean.Append(rune.ToString()); replacement = false; }
        }
        string stem = clean.ToString().Trim(' ', '.');
        while (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) stem = stem[..^suffix.Length].TrimEnd(' ', '.');
        if (IsReserved(stem)) stem = "_" + stem;
        int budget = 120 - suffix.Length; clean.Clear();
        foreach (var rune in stem.EnumerateRunes())
        { if (clean.Length + rune.Utf16SequenceLength > budget) break; clean.Append(rune.ToString()); }
        stem = clean.ToString().Trim(' ', '.');
        if (stem.Length == 0) stem = "Flashcards";
        if (IsReserved(stem)) stem = "_" + stem;
        return stem + suffix;
    }
}
