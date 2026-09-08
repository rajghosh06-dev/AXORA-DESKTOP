using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Contract for deterministic, non-destructive text normalization and paragraph assembly.
/// Transforms raw extracted text into canonical, clean, and searchable normalized text
/// without mutating original RawText or altering scientific notation.
/// </summary>
public interface ITextNormalizer
{
    /// <summary>
    /// Normalizes raw text according to the specified options using general rules.
    /// Does not alter the author's meaning, vocabulary, or mathematical/chemical notation.
    /// </summary>
    string Normalize(string rawText, TextNormalizationOptions? options = null);

    /// <summary>
    /// Normalizes raw text with source-format-specific awareness (e.g. PDF wrapping, OCR fragments, DOCX markers).
    /// </summary>
    string Normalize(string rawText, DetectedDocumentFormat format, TextNormalizationOptions? options = null);

    /// <summary>
    /// Normalizes an extracted page, returning a new page record with NormalizedText populated
    /// while leaving RawText and all page metadata (PageNumber, Semantics, Geometry, Provenance) exactly preserved.
    /// </summary>
    ExtractedPageRaw NormalizePage(ExtractedPageRaw rawPage, DetectedDocumentFormat format, TextNormalizationOptions? options = null);
}
