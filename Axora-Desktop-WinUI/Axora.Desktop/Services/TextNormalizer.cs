using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// High-performance implementation of <see cref="ITextNormalizer"/>.
/// Implements Phase W3-C.6.2 Unicode normalization, ligature unfolding,
/// character sanitization, and Phase W3-C.6.3 source-aware paragraph assembly,
/// line-wrap rejoining, protected region preservation, and DOCX token reconciliation.
/// This implementation is strictly stateless, deterministic, and thread-safe.
/// </summary>
public sealed class TextNormalizer : ITextNormalizer
{
    private static readonly Regex OcrCommaRegex = new(
        @"\b([a-zA-Z]{2,})\s+,(?=\s|$)",
        RegexOptions.Compiled);

    private static readonly Regex OcrPeriodRegex = new(
        @"\b([a-zA-Z]{2,})\s+\.(?=\s+(?:[A-Z]|\[|""|')|$)",
        RegexOptions.Compiled);

    private static readonly HashSet<string> ProtectedHyphenatedCompounds = new(StringComparer.OrdinalIgnoreCase)
    {
        "well-known",
        "state-of-the-art",
        "cis-trans",
        "file-name",
        "user-defined",
        "built-in",
        "real-time",
        "end-to-end",
        "trade-off",
        "scale-free",
        "peer-to-peer"
    };

    private static readonly HashSet<string> DanglingConnectors = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or", "of", "the", "in", "by", "to", "for", "with", "from",
        "that", "which", "as", "at", "on", "between", "into", "through", "during", "under"
    };

    private static readonly HashSet<string> KnownAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "et al.", "i.e.", "e.g.", "fig.", "dr.", "prof.", "vs.", "al.", "etc."
    };

    /// <inheritdoc/>
    public string Normalize(string rawText, TextNormalizationOptions? options = null)
    {
        return Normalize(rawText, DetectedDocumentFormat.PlainText, options);
    }

    /// <inheritdoc/>
    public string Normalize(string rawText, DetectedDocumentFormat format, TextNormalizationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(rawText);
        options ??= new TextNormalizationOptions();
        options.Validate();

        if (rawText.Length == 0)
        {
            return string.Empty;
        }

        bool shouldUnfoldLigatures = options.UnfoldTypesettingLigatures
            && format != DetectedDocumentFormat.DelimitedText;

        var sb = new StringBuilder(rawText.Length);

        for (int i = 0; i < rawText.Length; i++)
        {
            char c = rawText[i];

            // Stage 1a: BOM and Zero-Width Character Stripping (Tier A)
            if (options.StripBOMAndZeroWidthChars && IsZeroWidthOrBom(c))
            {
                continue;
            }

            // Stage 1b: Non-Printable Control Character Stripping (Tier A)
            if (options.StripNonPrintableControlChars && IsNonPrintableControl(c))
            {
                continue;
            }

            // Stage 2: Line Ending Canonicalization (CRLF/CR -> LF, Vertical Tab, FormFeed)
            if (options.NormalizeLineEndings)
            {
                if (c == '\r')
                {
                    sb.Append('\n');
                    // If part of \r\n, skip the \n so we don't produce two newlines
                    if (i + 1 < rawText.Length && rawText[i + 1] == '\n')
                    {
                        i++;
                    }
                    continue;
                }
                if (c == '\x0B' || c == '\x0C')
                {
                    sb.Append('\n');
                    continue;
                }
            }

            // Stage 3a: Typesetting Ligature Unfolding (Tier C)
            if (shouldUnfoldLigatures)
            {
                string? unfolded = TryUnfoldLigature(c);
                if (unfolded != null)
                {
                    sb.Append(unfolded);
                    continue;
                }
            }

            // Stage 3b: Non-Breaking Space Normalization (Tier B)
            if (options.CollapseConsecutiveSpaces && IsNbsp(c))
            {
                sb.Append(' ');
                continue;
            }

            sb.Append(c);
        }

        string result = sb.ToString();

        // Stage 3c: Unicode Normalization Form KC (Opt-in only; strictly disabled by default)
        if (options.ApplyUnicodeNfkc)
        {
            result = result.Normalize(NormalizationForm.FormKC);
        }

        if (string.IsNullOrWhiteSpace(result))
        {
            return string.Empty;
        }

        // Stage 4 & 5: Source-Aware Structural Normalization & Paragraph Assembly
        result = NormalizeStructural(result, format, options);

        return result;
    }

    /// <inheritdoc/>
    public ExtractedPageRaw NormalizePage(
        ExtractedPageRaw rawPage,
        DetectedDocumentFormat format,
        TextNormalizationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(rawPage);
        options ??= new TextNormalizationOptions();
        options.Validate();

        // Strict Invariant: RawText is strictly preserved and never mutated.
        string rawText = rawPage.RawText;

        try
        {
            // For Mixed PDF, resolve page-specific format using ExtractedViaOcr metadata
            DetectedDocumentFormat pageFormat = format;
            if (format == DetectedDocumentFormat.PdfMixed)
            {
                pageFormat = rawPage.ExtractedViaOcr ? DetectedDocumentFormat.PdfScanned : DetectedDocumentFormat.PdfDigital;
            }

            string normalized = Normalize(rawText, pageFormat, options);

            return new ExtractedPageRaw
            {
                PageNumber = rawPage.PageNumber,
                PageSemantics = rawPage.PageSemantics,
                WidthPt = rawPage.WidthPt,
                HeightPt = rawPage.HeightPt,
                RawText = rawPage.RawText, // Preserved exactly
                NormalizedText = normalized,
                ExtractedViaOcr = rawPage.ExtractedViaOcr,
                Confidence = rawPage.Confidence,
                DiagnosticWarning = rawPage.DiagnosticWarning
            };
        }
        catch (OperationCanceledException)
        {
            // Do NOT catch or swallow cancellation
            throw;
        }
        catch (Exception ex)
        {
            // Resilience fallback: preserve RawText, fall back to RawText for NormalizedText,
            // and append a non-sensitive diagnostic warning.
            string warning = rawPage.DiagnosticWarning != null
                ? $"{rawPage.DiagnosticWarning}; ERR_NORMALIZATION_FAILED: {ex.GetType().Name}"
                : $"ERR_NORMALIZATION_FAILED: {ex.GetType().Name}";

            return new ExtractedPageRaw
            {
                PageNumber = rawPage.PageNumber,
                PageSemantics = rawPage.PageSemantics,
                WidthPt = rawPage.WidthPt,
                HeightPt = rawPage.HeightPt,
                RawText = rawPage.RawText, // Preserved exactly
                NormalizedText = rawText,   // Fallback
                ExtractedViaOcr = rawPage.ExtractedViaOcr,
                Confidence = rawPage.Confidence,
                DiagnosticWarning = warning
            };
        }
    }

    #region Structural Normalization Dispatch

    private static string NormalizeStructural(string text, DetectedDocumentFormat format, TextNormalizationOptions options)
    {
        return format switch
        {
            DetectedDocumentFormat.PlainText =>
                AssemblePreservedLineStructure(text, options),

            DetectedDocumentFormat.Markdown =>
                AssemblePreservedLineStructure(text, options),

            DetectedDocumentFormat.DelimitedText =>
                text, // Table records preserved 1:1; no line joining or collapsing

            DetectedDocumentFormat.LocalHtml =>
                AssemblePreservedLineStructure(text, options),

            DetectedDocumentFormat.Docx =>
                ReconcileDocxStructural(text, options),

            DetectedDocumentFormat.PdfDigital =>
                ProcessFlowingDocument(text, options, isOcrFormat: false),

            DetectedDocumentFormat.PdfScanned or
            DetectedDocumentFormat.RasterImage or
            DetectedDocumentFormat.MultiPageTiff =>
                ProcessFlowingDocument(text, options, isOcrFormat: true),

            DetectedDocumentFormat.PdfMixed =>
                // Mixed PDF: page-aware dispatch based on [OCR] provenance marker
                ProcessFlowingDocument(text, options, isOcrFormat: text.StartsWith("[OCR]", StringComparison.Ordinal)),

            _ => AssemblePreservedLineStructure(text, options)
        };
    }

    /// <summary>
    /// Preserves existing line breaks for PlainText, Markdown, and LocalHtml while collapsing
    /// multiple blank lines to canonical \n\n boundaries and protecting fenced code blocks.
    /// </summary>
    private static string AssemblePreservedLineStructure(string text, TextNormalizationOptions options)
    {
        string[] rawLines = text.Split('\n');
        bool inFencedCode = false;
        string? activeFenceMarker = null;
        bool pendingBlank = false;

        var sb = new StringBuilder(text.Length);
        bool firstElement = true;

        for (int i = 0; i < rawLines.Length; i++)
        {
            string line = rawLines[i];
            string trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                if (inFencedCode)
                {
                    sb.Append('\n').Append(line);
                }
                else
                {
                    pendingBlank = true;
                }
                continue;
            }

            if (inFencedCode)
            {
                if (activeFenceMarker != null && IsClosingCodeFence(line, activeFenceMarker))
                {
                    inFencedCode = false;
                    activeFenceMarker = null;
                }
                sb.Append('\n').Append(line);
                continue;
            }

            if (IsCodeFence(line, out string? fenceMarker))
            {
                inFencedCode = true;
                activeFenceMarker = fenceMarker;
                if (!firstElement)
                {
                    sb.Append(options.PreserveParagraphBreaks ? "\n\n" : " ");
                }
                sb.Append(line);
                firstElement = false;
                pendingBlank = false;
                continue;
            }

            if (!firstElement)
            {
                if (pendingBlank)
                {
                    sb.Append(options.PreserveParagraphBreaks ? "\n\n" : " ");
                }
                else
                {
                    sb.Append('\n');
                }
            }

            string processed = line;
            if (options.CollapseConsecutiveSpaces)
            {
                processed = CollapseInternalSpaces(processed);
            }

            sb.Append(processed);
            firstElement = false;
            pendingBlank = false;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Reconciles DOCX structural scaffolding: compacts consecutive bullet/numbered lists to single newlines,
    /// ensures proper \n\n boundaries around headings and tables, and normalizes ###Heading markers.
    /// Author-authored Markdown tokens inside paragraphs are preserved.
    /// </summary>
    private static string ReconcileDocxStructural(string text, TextNormalizationOptions options)
    {
        string[] rawLines = text.Split('\n');
        bool inFencedCode = false;
        string? activeFenceMarker = null;
        bool pendingBlank = false;

        var classified = new List<ClassifiedLine>(rawLines.Length);

        for (int i = 0; i < rawLines.Length; i++)
        {
            string line = rawLines[i];
            string trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                if (inFencedCode)
                {
                    classified.Add(new ClassifiedLine(line, line, LineRegionType.ProtectedCode, false));
                }
                else
                {
                    pendingBlank = true;
                }
                continue;
            }

            LineRegionType region;
            string content = line;

            if (inFencedCode)
            {
                if (activeFenceMarker != null && IsClosingCodeFence(line, activeFenceMarker))
                {
                    inFencedCode = false;
                    activeFenceMarker = null;
                }
                region = LineRegionType.ProtectedCode;
            }
            else if (IsCodeFence(line, out string? fenceMarker))
            {
                inFencedCode = true;
                activeFenceMarker = fenceMarker;
                region = LineRegionType.ProtectedCode;
            }
            else if (IsTableRow(line))
            {
                region = LineRegionType.ProtectedTable;
            }
            else if (IsHeadingLine(line, out int lvl, out string hText))
            {
                region = LineRegionType.ProtectedHeading;
                content = new string('#', lvl) + " " + hText;
            }
            else if (IsListItem(line))
            {
                region = LineRegionType.ProtectedList;
            }
            else if (IsBlockquote(line))
            {
                region = LineRegionType.ProtectedBlockquote;
            }
            else
            {
                region = LineRegionType.FlowingBodyText;
                if (options.CollapseConsecutiveSpaces)
                {
                    content = CollapseInternalSpaces(content);
                }
            }

            classified.Add(new ClassifiedLine(line, content, region, pendingBlank));
            pendingBlank = false;
        }

        if (classified.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(text.Length);

        for (int i = 0; i < classified.Count; i++)
        {
            var cur = classified[i];

            if (i > 0)
            {
                var prev = classified[i - 1];

                if (cur.RegionType == LineRegionType.ProtectedCode && prev.RegionType == LineRegionType.ProtectedCode)
                {
                    sb.Append('\n');
                }
                else if (cur.RegionType == LineRegionType.ProtectedTable && prev.RegionType == LineRegionType.ProtectedTable)
                {
                    sb.Append('\n');
                }
                else if (cur.RegionType == LineRegionType.ProtectedList && prev.RegionType == LineRegionType.ProtectedList)
                {
                    // DOCX rule: compact consecutive list items to single newline
                    sb.Append('\n');
                }
                else if (cur.RegionType == LineRegionType.ProtectedBlockquote && prev.RegionType == LineRegionType.ProtectedBlockquote)
                {
                    sb.Append('\n');
                }
                else if (cur.RegionType == LineRegionType.FlowingBodyText && prev.RegionType == LineRegionType.FlowingBodyText && !cur.HadBlankLineBefore)
                {
                    // Soft line breaks within the same DOCX paragraph (e.g. <w:br/>) preserve single newline
                    sb.Append('\n');
                }
                else
                {
                    // Transition between blocks or distinct paragraphs separated by blank lines in DOCX
                    sb.Append(options.PreserveParagraphBreaks ? "\n\n" : " ");
                }
            }

            sb.Append(cur.Content);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Processes flowing documents (PDF Digital, PDF Scanned, Mixed, Raster Image, TIFF)
    /// using evidence-based soft-wrap rejoining, protected region preservation, and restricted OCR cleanup.
    /// </summary>
    private static string ProcessFlowingDocument(string text, TextNormalizationOptions options, bool isOcrFormat)
    {
        string[] rawLines = text.Split('\n');
        bool inFencedCode = false;
        string? activeFenceMarker = null;
        bool pendingBlank = false;

        var lines = new List<ClassifiedLine>(rawLines.Length);

        for (int i = 0; i < rawLines.Length; i++)
        {
            string raw = rawLines[i];
            string trimmed = raw.Trim();

            if (trimmed.Length == 0)
            {
                if (inFencedCode)
                {
                    lines.Add(new ClassifiedLine(raw, raw, LineRegionType.ProtectedCode, false));
                }
                else
                {
                    pendingBlank = true;
                }
                continue;
            }

            LineRegionType region;
            if (trimmed.Equals("[OCR]", StringComparison.OrdinalIgnoreCase))
            {
                region = LineRegionType.ProtectedBlockquote;
            }
            else if (inFencedCode)
            {
                if (activeFenceMarker != null && IsClosingCodeFence(raw, activeFenceMarker))
                {
                    inFencedCode = false;
                    activeFenceMarker = null;
                }
                region = LineRegionType.ProtectedCode;
            }
            else if (IsCodeFence(raw, out string? fenceMarker))
            {
                inFencedCode = true;
                activeFenceMarker = fenceMarker;
                region = LineRegionType.ProtectedCode;
            }
            else if (IsTableRow(raw))
            {
                region = LineRegionType.ProtectedTable;
            }
            else if (IsHeadingLine(raw, out _, out _))
            {
                region = LineRegionType.ProtectedHeading;
            }
            else if (IsListItem(raw))
            {
                region = LineRegionType.ProtectedList;
            }
            else if (IsBlockquote(raw))
            {
                region = LineRegionType.ProtectedBlockquote;
            }
            else
            {
                region = LineRegionType.FlowingBodyText;
            }

            string processed = raw;
            if (region == LineRegionType.FlowingBodyText)
            {
                if (isOcrFormat)
                {
                    processed = CleanOcrPunctuationSpacing(processed);
                }
                if (options.CollapseConsecutiveSpaces)
                {
                    processed = CollapseInternalSpaces(processed);
                }
            }

            lines.Add(new ClassifiedLine(raw, processed, region, pendingBlank));
            pendingBlank = false;
        }

        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(text.Length);
        int idx = 0;

        while (idx < lines.Count)
        {
            var cur = lines[idx];

            if (cur.RegionType == LineRegionType.ProtectedCode)
            {
                if (sb.Length > 0)
                {
                    sb.Append(idx > 0 && lines[idx - 1].RegionType == LineRegionType.ProtectedCode
                        ? "\n"
                        : (options.PreserveParagraphBreaks ? "\n\n" : " "));
                }
                sb.Append(cur.OriginalLine);
                idx++;
                continue;
            }

            if (cur.RegionType == LineRegionType.ProtectedTable)
            {
                if (sb.Length > 0)
                {
                    sb.Append(idx > 0 && lines[idx - 1].RegionType == LineRegionType.ProtectedTable
                        ? "\n"
                        : (options.PreserveParagraphBreaks ? "\n\n" : " "));
                }
                sb.Append(cur.Content);
                idx++;
                continue;
            }

            if (cur.RegionType == LineRegionType.ProtectedHeading)
            {
                if (sb.Length > 0)
                {
                    sb.Append(options.PreserveParagraphBreaks ? "\n\n" : " ");
                }
                if (IsHeadingLine(cur.Content, out int lvl, out string hText))
                {
                    sb.Append(new string('#', lvl)).Append(' ').Append(hText);
                }
                else
                {
                    sb.Append(cur.Content);
                }
                idx++;
                continue;
            }

            if (cur.RegionType == LineRegionType.ProtectedList)
            {
                if (sb.Length > 0)
                {
                    sb.Append(idx > 0 && lines[idx - 1].RegionType == LineRegionType.ProtectedList
                        ? "\n"
                        : (options.PreserveParagraphBreaks ? "\n\n" : " "));
                }
                sb.Append(cur.Content);
                idx++;
                continue;
            }

            if (cur.RegionType == LineRegionType.ProtectedBlockquote)
            {
                if (sb.Length > 0)
                {
                    sb.Append(idx > 0 && lines[idx - 1].RegionType == LineRegionType.ProtectedBlockquote
                        ? "\n"
                        : (options.PreserveParagraphBreaks ? "\n\n" : " "));
                }
                sb.Append(cur.Content);
                idx++;
                continue;
            }

            // FlowingBodyText
            string currentSegment = cur.Content;
            int nextIdx = idx + 1;

            while (nextIdx < lines.Count)
            {
                var next = lines[nextIdx];
                if (next.RegionType != LineRegionType.FlowingBodyText)
                {
                    break;
                }

                // Blank line before next -> definite paragraph break, do not join
                if (next.HadBlankLineBefore)
                {
                    break;
                }

                // Terminal punctuation + paragraph indentation on next line -> paragraph break, do not join
                if (EndsWithTerminalPunctuation(currentSegment) && HasParagraphIndentation(next.Content))
                {
                    break;
                }

                // Check positive evidence for soft-wrap continuation
                if (TryEvaluateSoftWrapJoin(currentSegment, next.Content, options, out string? joinedSegment))
                {
                    currentSegment = joinedSegment!;
                    nextIdx++;
                }
                else
                {
                    // Ambiguous or unevidenced -> preserve newline
                    break;
                }
            }

            if (sb.Length > 0)
            {
                if (cur.HadBlankLineBefore)
                {
                    sb.Append(options.PreserveParagraphBreaks ? "\n\n" : " ");
                }
                else if (idx > 0 && lines[idx - 1].RegionType != LineRegionType.FlowingBodyText)
                {
                    sb.Append(options.PreserveParagraphBreaks ? "\n\n" : " ");
                }
                else
                {
                    if (idx > 0 && EndsWithTerminalPunctuation(lines[idx - 1].Content) && HasParagraphIndentation(cur.Content))
                    {
                        sb.Append(options.PreserveParagraphBreaks ? "\n\n" : " ");
                    }
                    else
                    {
                        sb.Append('\n');
                    }
                }
            }

            sb.Append(currentSegment);
            idx = nextIdx;
        }

        return sb.ToString();
    }

    #endregion

    #region Helper Methods

    private static bool TryEvaluateSoftWrapJoin(
        string segment,
        string nextLine,
        TextNormalizationOptions options,
        out string? joined)
    {
        joined = null;
        string trimmedSeg = segment.TrimEnd();
        string trimmedNext = nextLine.TrimStart();

        if (trimmedSeg.Length == 0 || trimmedNext.Length == 0)
        {
            return false;
        }

        // 1. Line-end Hyphenation
        if (trimmedSeg.EndsWith('-'))
        {
            // Check if hyphen is preceded by whitespace (e.g. subtraction operator "x -" or dash " -")
            if (trimmedSeg.Length >= 2 && char.IsWhiteSpace(trimmedSeg[^2]))
            {
                // Math operator or dash: join with a space
                joined = trimmedSeg + " " + trimmedNext;
                return true;
            }

            if (options.RepairLinebreakHyphenation)
            {
                if (TryRepairHyphenation(trimmedSeg, trimmedNext, out string repaired))
                {
                    joined = repaired;
                    return true;
                }
            }

            // Default or gate failed: preserve hyphen, join without artificial space
            joined = trimmedSeg + trimmedNext;
            return true;
        }

        // 2. Clause continuation punctuation: comma or semicolon
        if (EndsWithClausePunctuation(trimmedSeg))
        {
            joined = trimmedSeg + " " + trimmedNext;
            return true;
        }

        // 3. Abbreviation period followed by lowercase
        if (EndsWithAbbreviationPeriod(trimmedSeg) && char.IsLower(trimmedNext[0]))
        {
            joined = trimmedSeg + " " + trimmedNext;
            return true;
        }

        // 4. Lacks terminal punctuation
        if (!EndsWithTerminalPunctuation(trimmedSeg))
        {
            // Lowercase start: definite mid-sentence soft wrap
            if (char.IsLower(trimmedNext[0]))
            {
                joined = trimmedSeg + " " + trimmedNext;
                return true;
            }

            // Dangling preposition/conjunction followed by uppercase: proper noun wrap
            if (EndsWithDanglingConnector(trimmedSeg) && char.IsUpper(trimmedNext[0]))
            {
                joined = trimmedSeg + " " + trimmedNext;
                return true;
            }

            // Otherwise ambiguous -> preserve newline
            return false;
        }

        // Terminal punctuation on segment -> preserve newline
        return false;
    }

    private static bool TryRepairHyphenation(
        string segment,
        string nextLine,
        out string repaired)
    {
        repaired = string.Empty;

        int lastHyphen = segment.LastIndexOf('-');
        if (lastHyphen < 0 || lastHyphen != segment.Length - 1)
        {
            return false;
        }

        // Extract last word stem before hyphen
        int wordStart = lastHyphen - 1;
        while (wordStart >= 0 && (char.IsLetter(segment[wordStart]) || segment[wordStart] == 'α' || segment[wordStart] == 'β' || segment[wordStart] == 'γ'))
        {
            wordStart--;
        }
        wordStart++;

        string stem1 = segment[wordStart..lastHyphen];
        if (stem1.Length < 3)
        {
            return false; // Gate 1 & 4 & 5: single-letter (p-, x-), Greek (α-, β-), or 2-letter (Na-)
        }

        // Gate 2: nextLine begins with at least 2 lowercase ASCII letters
        int stem2End = 0;
        while (stem2End < nextLine.Length && char.IsAsciiLetterLower(nextLine[stem2End]))
        {
            stem2End++;
        }
        if (stem2End < 2)
        {
            return false;
        }
        string stem2 = nextLine[..stem2End];

        // Gate 3: Alphabetic purity (ASCII only, no digits/math)
        if (!stem1.All(char.IsAsciiLetter) || !stem2.All(char.IsAsciiLetterLower))
        {
            return false;
        }

        // Gate 7: Proper name exclusion (neither part is capitalized proper noun)
        if (char.IsUpper(stem1[0]))
        {
            return false;
        }

        // Gate 6: Chemical stereoisomer prefix exclusion (cis-, trans-)
        if (stem1.Equals("cis", StringComparison.OrdinalIgnoreCase) ||
            stem1.Equals("trans", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Gate 6 & 8: Known compound word exclusion (handles multi-part compounds like state-of-the-art, end-to-end, peer-to-peer)
        foreach (var compound in ProtectedHyphenatedCompounds)
        {
            if (compound.StartsWith(stem1 + "-", StringComparison.OrdinalIgnoreCase))
            {
                string remainder = compound[(stem1.Length + 1)..];
                if (nextLine.StartsWith(remainder, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        // All gates passed! Strip hyphen and join without space
        repaired = segment[..lastHyphen] + nextLine;
        return true;
    }

    private static string CleanOcrPunctuationSpacing(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return line;
        }

        string withCommas = OcrCommaRegex.Replace(line, match =>
        {
            if (IsInsideProtectedOcrContext(line, match.Index))
            {
                return match.Value;
            }
            return match.Groups[1].Value + ",";
        });

        string withPeriods = OcrPeriodRegex.Replace(withCommas, match =>
        {
            if (IsInsideProtectedOcrContext(withCommas, match.Index))
            {
                return match.Value;
            }
            return match.Groups[1].Value + ".";
        });

        return withPeriods;
    }

    private static bool IsInsideProtectedOcrContext(string line, int index)
    {
        // 1. Check if inside brackets [ ... ]
        int openBrackets = 0;
        int closeBrackets = 0;
        for (int i = 0; i < index; i++)
        {
            if (line[i] == '[') openBrackets++;
            else if (line[i] == ']') closeBrackets++;
        }
        if (openBrackets > closeBrackets)
        {
            return true;
        }

        // 2. Check if inside parentheses ( ... )
        int openParens = 0;
        int closeParens = 0;
        for (int i = 0; i < index; i++)
        {
            if (line[i] == '(') openParens++;
            else if (line[i] == ')') closeParens++;
        }
        if (openParens > closeParens)
        {
            return true;
        }

        // 3. Check if inside URL / protocol (http://, https://)
        string before = line[..index];
        int lastHttp = Math.Max(before.LastIndexOf("http://", StringComparison.OrdinalIgnoreCase),
                                before.LastIndexOf("https://", StringComparison.OrdinalIgnoreCase));
        if (lastHttp >= 0)
        {
            if (!before[lastHttp..].Contains(' '))
            {
                return true;
            }
        }

        return false;
    }

    private static string CollapseInternalSpaces(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return line;
        }

        int leadingLen = 0;
        while (leadingLen < line.Length && (line[leadingLen] == ' ' || line[leadingLen] == '\t'))
        {
            leadingLen++;
        }

        string leading = line[..leadingLen];
        string rest = line[leadingLen..];

        // Check if rest contains any runs of >= 2 whitespace characters (spaces or tabs)
        bool hasConsecutiveWhitespace = false;
        for (int i = 0; i < rest.Length - 1; i++)
        {
            if ((rest[i] == ' ' || rest[i] == '\t') && (rest[i + 1] == ' ' || rest[i + 1] == '\t'))
            {
                hasConsecutiveWhitespace = true;
                break;
            }
        }

        if (!hasConsecutiveWhitespace)
        {
            return line;
        }

        var sb = new StringBuilder(rest.Length);
        int j = 0;
        while (j < rest.Length)
        {
            char c = rest[j];
            if (c == ' ' || c == '\t')
            {
                int wsCount = 0;
                while (j + wsCount < rest.Length && (rest[j + wsCount] == ' ' || rest[j + wsCount] == '\t'))
                {
                    wsCount++;
                }

                if (wsCount >= 2)
                {
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(c);
                }
                j += wsCount;
            }
            else
            {
                sb.Append(c);
                j++;
            }
        }

        return leading + sb.ToString();
    }

    private static bool IsCodeFence(string line, out string? fenceMarker)
    {
        string trimmed = line.TrimStart();
        if (trimmed.StartsWith("```"))
        {
            int count = 0;
            while (count < trimmed.Length && trimmed[count] == '`')
            {
                count++;
            }
            fenceMarker = trimmed[..count];
            return true;
        }
        if (trimmed.StartsWith("~~~"))
        {
            int count = 0;
            while (count < trimmed.Length && trimmed[count] == '~')
            {
                count++;
            }
            fenceMarker = trimmed[..count];
            return true;
        }
        fenceMarker = null;
        return false;
    }

    private static bool IsClosingCodeFence(string line, string activeFenceMarker)
    {
        string trimmed = line.Trim();
        char markerChar = activeFenceMarker[0];
        int minLength = activeFenceMarker.Length;
        return trimmed.Length >= minLength && trimmed.All(c => c == markerChar);
    }

    private static bool IsTableRow(string line)
    {
        string trimmed = line.Trim();
        return trimmed.Length >= 2 && trimmed.StartsWith('|') && trimmed.EndsWith('|');
    }

    private static bool IsHeadingLine(string line, out int level, out string headingText)
    {
        string trimmed = line.Trim();
        level = 0;
        headingText = string.Empty;

        if (trimmed.Length == 0 || trimmed[0] != '#')
        {
            return false;
        }

        int count = 0;
        while (count < trimmed.Length && trimmed[count] == '#')
        {
            count++;
        }

        if (count >= 1 && count <= 6)
        {
            if (count < trimmed.Length && trimmed[count] == ' ')
            {
                level = count;
                headingText = trimmed[(count + 1)..].Trim();
                return true;
            }

            // Normalizes "###Heading" while excluding preprocessor directives (#include, #define, #region, etc.) and numbers (#1, #42)
            if (count < trimmed.Length && char.IsLetter(trimmed[count]))
            {
                string rest = trimmed[count..];
                if (count == 1 && (rest.StartsWith("include", StringComparison.OrdinalIgnoreCase) ||
                                   rest.StartsWith("define", StringComparison.OrdinalIgnoreCase) ||
                                   rest.StartsWith("undef", StringComparison.OrdinalIgnoreCase) ||
                                   rest.StartsWith("pragma", StringComparison.OrdinalIgnoreCase) ||
                                   rest.StartsWith("ifdef", StringComparison.OrdinalIgnoreCase) ||
                                   rest.StartsWith("ifndef", StringComparison.OrdinalIgnoreCase) ||
                                   rest.StartsWith("if", StringComparison.OrdinalIgnoreCase) && (rest.Length == 2 || !char.IsLetter(rest[2])) ||
                                   rest.StartsWith("elif", StringComparison.OrdinalIgnoreCase) ||
                                   rest.StartsWith("else", StringComparison.OrdinalIgnoreCase) ||
                                   rest.StartsWith("endif", StringComparison.OrdinalIgnoreCase) ||
                                   rest.StartsWith("import", StringComparison.OrdinalIgnoreCase) ||
                                   rest.StartsWith("region", StringComparison.OrdinalIgnoreCase) ||
                                   rest.StartsWith("endregion", StringComparison.OrdinalIgnoreCase) ||
                                   rest.StartsWith("error", StringComparison.OrdinalIgnoreCase) ||
                                   rest.StartsWith("warning", StringComparison.OrdinalIgnoreCase) ||
                                   rest.StartsWith("nullable", StringComparison.OrdinalIgnoreCase) ||
                                   rest.StartsWith("line", StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                level = count;
                headingText = rest.Trim();
                return true;
            }
        }

        return false;
    }

    private static bool IsListItem(string line)
    {
        string trimmed = line.TrimStart();
        if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ "))
        {
            return true;
        }

        int i = 0;
        while (i < trimmed.Length && char.IsAsciiDigit(trimmed[i]))
        {
            i++;
        }
        return i > 0 && i + 1 < trimmed.Length && trimmed[i] == '.' && trimmed[i + 1] == ' ';
    }

    private static bool IsBlockquote(string line)
    {
        string trimmed = line.TrimStart();
        return trimmed.StartsWith("> ") || trimmed == ">";
    }

    private static bool HasParagraphIndentation(string line)
    {
        return !string.IsNullOrEmpty(line) && (line.StartsWith('\t') || line.StartsWith("  "));
    }

    private static bool EndsWithTerminalPunctuation(string line)
    {
        string trimmed = line.TrimEnd();
        if (trimmed.Length == 0)
        {
            return false;
        }
        char last = trimmed[^1];
        return last == '.' || last == '!' || last == '?';
    }

    private static bool EndsWithClausePunctuation(string line)
    {
        string trimmed = line.TrimEnd();
        if (trimmed.Length == 0)
        {
            return false;
        }
        char last = trimmed[^1];
        return last == ',' || last == ';' || last == ':';
    }

    private static bool EndsWithAbbreviationPeriod(string line)
    {
        string trimmed = line.TrimEnd();
        if (!trimmed.EndsWith('.'))
        {
            return false;
        }
        int lastSpace = trimmed.LastIndexOf(' ');
        string lastToken = lastSpace >= 0 ? trimmed[(lastSpace + 1)..] : trimmed;
        return KnownAbbreviations.Contains(lastToken);
    }

    private static bool EndsWithDanglingConnector(string line)
    {
        string trimmed = line.TrimEnd();
        int lastSpace = trimmed.LastIndexOf(' ');
        string lastWord = lastSpace >= 0 ? trimmed[(lastSpace + 1)..] : trimmed;
        return DanglingConnectors.Contains(lastWord);
    }

    /// <summary>
    /// Checks for UTF Byte Order Marks, invisible zero-width characters, directional marks, and soft hyphens.
    /// </summary>
    private static bool IsZeroWidthOrBom(char c)
    {
        return c switch
        {
            '\uFEFF' => true, // Byte Order Mark / Zero-Width No-Break Space
            '\u200B' => true, // Zero-Width Space
            '\u200C' => true, // Zero-Width Non-Joiner (ZWNJ)
            '\u200D' => true, // Zero-Width Joiner (ZWJ)
            '\u2060' => true, // Word Joiner
            '\u200E' => true, // Left-to-Right Mark (LRM)
            '\u200F' => true, // Right-to-Left Mark (RLM)
            '\u00AD' => true, // Soft Hyphen (discretionary hyphen artifact)
            _ => false
        };
    }

    /// <summary>
    /// Checks for ASCII C0 control characters (0x00-0x08, 0x0E-0x1F) and DEL (0x7F).
    /// Explicitly preserves Tab (0x09), LF (0x0A), and CR (0x0D).
    /// </summary>
    private static bool IsNonPrintableControl(char c)
    {
        return (c >= '\x00' && c <= '\x08') || (c >= '\x0E' && c <= '\x1F') || c == '\x7F';
    }

    /// <summary>
    /// Unfolds standard Latin typesetting ligatures to ASCII graphemes.
    /// </summary>
    private static string? TryUnfoldLigature(char c)
    {
        return c switch
        {
            '\uFB00' => "ff",
            '\uFB01' => "fi",
            '\uFB02' => "fl",
            '\uFB03' => "ffi",
            '\uFB04' => "ffl",
            '\uFB05' => "ft",
            '\uFB06' => "st",
            _ => null
        };
    }

    /// <summary>
    /// Checks for non-breaking space variants (U+00A0 and narrow U+202F).
    /// </summary>
    private static bool IsNbsp(char c) => c == '\u00A0' || c == '\u202F';

    #endregion

    #region Internal Region Model

    private enum LineRegionType
    {
        FlowingBodyText,
        ProtectedCode,
        ProtectedTable,
        ProtectedHeading,
        ProtectedList,
        ProtectedBlockquote
    }

    private sealed class ClassifiedLine
    {
        public ClassifiedLine(string originalLine, string content, LineRegionType regionType, bool hadBlankLineBefore)
        {
            OriginalLine = originalLine;
            Content = content;
            RegionType = regionType;
            HadBlankLineBefore = hadBlankLineBefore;
        }

        public string OriginalLine { get; }
        public string Content { get; set; }
        public LineRegionType RegionType { get; }
        public bool HadBlankLineBefore { get; }
    }

    #endregion
}
