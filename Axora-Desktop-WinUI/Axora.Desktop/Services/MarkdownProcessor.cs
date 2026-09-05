using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services;

/// <summary>
/// Types of Markdown structural block elements.
/// </summary>
public enum MarkdownBlockType
{
    Heading,
    Paragraph,
    UnorderedListItem,
    OrderedListItem,
    CodeBlock,
    HorizontalRule,
    Blockquote,
    EmptyLine
}

/// <summary>
/// Represents a parsed Markdown block item.
/// </summary>
public sealed class MarkdownBlock
{
    public MarkdownBlockType Type { get; init; }
    public int Level { get; init; } = 1; // Heading 1-6, or blockquote nesting
    public int OrderNumber { get; init; } = 1; // Ordered list item index
    public string RawText { get; set; } = string.Empty;
    public string CodeLanguage { get; init; } = string.Empty;
    public List<string> Lines { get; } = new();
}

/// <summary>
/// Security-first, self-contained Markdown processor.
/// Parses Markdown structural blocks, converts to sanitized semantic HTML,
/// strips formatting to clean plain text, and compiles vector PDF layouts via PdfSharpCore.
/// Treats all Markdown input as untrusted.
/// </summary>
public static class MarkdownProcessor
{
    private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex UnorderedListRegex = new(@"^(\s*)[-*+]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new(@"^(\s*)(\d+)\.\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex HorizontalRuleRegex = new(@"^(\s*[-*_]\s*){3,}$", RegexOptions.Compiled);
    private static readonly Regex BlockquoteRegex = new(@"^>\s?(.*)$", RegexOptions.Compiled);
    private static readonly Regex CodeBlockFenceRegex = new(@"^(```|~~~)(.*)$", RegexOptions.Compiled);

    private static readonly Regex LinkRegex = new(@"\[(.*?)\]\((.*?)\)", RegexOptions.Compiled);
    private static readonly Regex BoldRegex = new(@"(\*\*|__)(.*?)\1", RegexOptions.Compiled);
    private static readonly Regex ItalicRegex = new(@"(?<!\*|\w)(\*|_)(.*?)\1(?!\*|\w)", RegexOptions.Compiled);
    private static readonly Regex InlineCodeRegex = new(@"`([^`]+)`", RegexOptions.Compiled);

    /// <summary>
    /// Parses Markdown source text into structural blocks.
    /// </summary>
    public static List<MarkdownBlock> ParseBlocks(string markdownText, CancellationToken ct = default)
    {
        var blocks = new List<MarkdownBlock>();
        if (string.IsNullOrEmpty(markdownText))
            return blocks;

        var rawLines = markdownText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        bool inCodeBlock = false;
        MarkdownBlock? currentCodeBlock = null;

        for (int i = 0; i < rawLines.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var line = rawLines[i];

            // Code block fence check
            var fenceMatch = CodeBlockFenceRegex.Match(line.Trim());
            if (fenceMatch.Success)
            {
                if (!inCodeBlock)
                {
                    inCodeBlock = true;
                    currentCodeBlock = new MarkdownBlock
                    {
                        Type = MarkdownBlockType.CodeBlock,
                        CodeLanguage = fenceMatch.Groups[2].Value.Trim()
                    };
                    blocks.Add(currentCodeBlock);
                }
                else
                {
                    inCodeBlock = false;
                    currentCodeBlock = null;
                }
                continue;
            }

            if (inCodeBlock && currentCodeBlock != null)
            {
                currentCodeBlock.Lines.Add(line);
                continue;
            }

            // Empty line
            if (string.IsNullOrWhiteSpace(line))
            {
                blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.EmptyLine });
                continue;
            }

            // Horizontal rule
            if (HorizontalRuleRegex.IsMatch(line))
            {
                blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.HorizontalRule });
                continue;
            }

            // Heading
            var headingMatch = HeadingRegex.Match(line);
            if (headingMatch.Success)
            {
                blocks.Add(new MarkdownBlock
                {
                    Type = MarkdownBlockType.Heading,
                    Level = headingMatch.Groups[1].Value.Length,
                    RawText = headingMatch.Groups[2].Value.Trim()
                });
                continue;
            }

            // Blockquote
            var bqMatch = BlockquoteRegex.Match(line);
            if (bqMatch.Success)
            {
                blocks.Add(new MarkdownBlock
                {
                    Type = MarkdownBlockType.Blockquote,
                    RawText = bqMatch.Groups[1].Value.Trim()
                });
                continue;
            }

            // Unordered list
            var ulMatch = UnorderedListRegex.Match(line);
            if (ulMatch.Success)
            {
                blocks.Add(new MarkdownBlock
                {
                    Type = MarkdownBlockType.UnorderedListItem,
                    RawText = ulMatch.Groups[2].Value.Trim()
                });
                continue;
            }

            // Ordered list
            var olMatch = OrderedListRegex.Match(line);
            if (olMatch.Success)
            {
                int.TryParse(olMatch.Groups[2].Value, out int orderNum);
                blocks.Add(new MarkdownBlock
                {
                    Type = MarkdownBlockType.OrderedListItem,
                    OrderNumber = orderNum > 0 ? orderNum : 1,
                    RawText = olMatch.Groups[3].Value.Trim()
                });
                continue;
            }

            // Regular paragraph
            blocks.Add(new MarkdownBlock
            {
                Type = MarkdownBlockType.Paragraph,
                RawText = line.Trim()
            });
        }

        return blocks;
    }

    /// <summary>
    /// Converts Markdown to semantic, sanitized, standalone HTML5 document.
    /// Strictly sanitizes URLs (blocking javascript:, vbscript:, data:).
    /// Escapes all user text against script injection.
    /// </summary>
    public static string ConvertToHtml(string markdownText, string? title = null, CancellationToken ct = default)
    {
        var blocks = ParseBlocks(markdownText, ct);
        var sb = new StringBuilder();

        string docTitle = WebUtility.HtmlEncode(title ?? "Axora Markdown Document");

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.AppendLine($"  <title>{docTitle}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; color: #1f2937; max-width: 820px; margin: 0 auto; padding: 2rem 1.5rem; background: #ffffff; }");
        sb.AppendLine("    h1, h2, h3, h4, h5, h6 { color: #111827; margin-top: 1.6em; margin-bottom: 0.6em; font-weight: 600; line-height: 1.25; }");
        sb.AppendLine("    h1 { font-size: 2em; border-bottom: 1px solid #e5e7eb; padding-bottom: 0.3em; }");
        sb.AppendLine("    h2 { font-size: 1.5em; border-bottom: 1px solid #e5e7eb; padding-bottom: 0.3em; }");
        sb.AppendLine("    h3 { font-size: 1.25em; }");
        sb.AppendLine("    p { margin-top: 0; margin-bottom: 1em; }");
        sb.AppendLine("    code { font-family: 'Cascadia Code', Consolas, Monaco, monospace; background: #f3f4f6; padding: 0.2em 0.4em; border-radius: 4px; font-size: 0.875em; }");
        sb.AppendLine("    pre { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 1rem; overflow-x: auto; margin-bottom: 1.2em; }");
        sb.AppendLine("    pre code { background: none; padding: 0; border-radius: 0; font-size: 0.875em; color: #0f172a; }");
        sb.AppendLine("    blockquote { border-left: 4px solid #3b82f6; margin: 1.2em 0; padding: 0.5em 1em; background: #eff6ff; color: #1e40af; }");
        sb.AppendLine("    blockquote p { margin: 0; }");
        sb.AppendLine("    hr { border: 0; height: 1px; background: #e5e7eb; margin: 2em 0; }");
        sb.AppendLine("    ul, ol { padding-left: 1.75rem; margin-top: 0; margin-bottom: 1em; }");
        sb.AppendLine("    li { margin-bottom: 0.35em; }");
        sb.AppendLine("    a { color: #2563eb; text-decoration: underline; text-underline-offset: 2px; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        bool inUl = false;
        bool inOl = false;

        void CloseLists()
        {
            if (inUl) { sb.AppendLine("</ul>"); inUl = false; }
            if (inOl) { sb.AppendLine("</ol>"); inOl = false; }
        }

        foreach (var block in blocks)
        {
            ct.ThrowIfCancellationRequested();

            if (block.Type != MarkdownBlockType.UnorderedListItem && inUl)
            {
                sb.AppendLine("</ul>");
                inUl = false;
            }
            if (block.Type != MarkdownBlockType.OrderedListItem && inOl)
            {
                sb.AppendLine("</ol>");
                inOl = false;
            }

            switch (block.Type)
            {
                case MarkdownBlockType.Heading:
                    int lvl = Math.Clamp(block.Level, 1, 6);
                    sb.AppendLine($"<h{lvl}>{FormatInlineHtml(block.RawText)}</h{lvl}>");
                    break;

                case MarkdownBlockType.Paragraph:
                    sb.AppendLine($"<p>{FormatInlineHtml(block.RawText)}</p>");
                    break;

                case MarkdownBlockType.UnorderedListItem:
                    if (!inUl)
                    {
                        sb.AppendLine("<ul>");
                        inUl = true;
                    }
                    sb.AppendLine($"  <li>{FormatInlineHtml(block.RawText)}</li>");
                    break;

                case MarkdownBlockType.OrderedListItem:
                    if (!inOl)
                    {
                        sb.AppendLine("<ol>");
                        inOl = true;
                    }
                    sb.AppendLine($"  <li>{FormatInlineHtml(block.RawText)}</li>");
                    break;

                case MarkdownBlockType.CodeBlock:
                    string langAttr = !string.IsNullOrWhiteSpace(block.CodeLanguage)
                        ? $" class=\"language-{WebUtility.HtmlEncode(block.CodeLanguage)}\""
                        : string.Empty;
                    sb.Append($"<pre><code{langAttr}>");
                    for (int j = 0; j < block.Lines.Count; j++)
                    {
                        sb.Append(WebUtility.HtmlEncode(block.Lines[j]));
                        if (j < block.Lines.Count - 1)
                            sb.Append('\n');
                    }
                    sb.AppendLine("</code></pre>");
                    break;

                case MarkdownBlockType.Blockquote:
                    sb.AppendLine($"<blockquote><p>{FormatInlineHtml(block.RawText)}</p></blockquote>");
                    break;

                case MarkdownBlockType.HorizontalRule:
                    sb.AppendLine("<hr />");
                    break;

                case MarkdownBlockType.EmptyLine:
                    // Handled implicitly by paragraph margins
                    break;
            }
        }

        CloseLists();

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    /// <summary>
    /// Converts Markdown to clean, readable plain text with formatting markers stripped.
    /// Does not perform HTML rendering.
    /// </summary>
    public static string ConvertToPlainText(string markdownText, CancellationToken ct = default)
    {
        var blocks = ParseBlocks(markdownText, ct);
        var sb = new StringBuilder();

        foreach (var block in blocks)
        {
            ct.ThrowIfCancellationRequested();

            switch (block.Type)
            {
                case MarkdownBlockType.Heading:
                    sb.AppendLine(StripInlineFormatting(block.RawText));
                    break;

                case MarkdownBlockType.Paragraph:
                    sb.AppendLine(StripInlineFormatting(block.RawText));
                    break;

                case MarkdownBlockType.UnorderedListItem:
                    sb.AppendLine($"• {StripInlineFormatting(block.RawText)}");
                    break;

                case MarkdownBlockType.OrderedListItem:
                    sb.AppendLine($"{block.OrderNumber}. {StripInlineFormatting(block.RawText)}");
                    break;

                case MarkdownBlockType.CodeBlock:
                    foreach (var line in block.Lines)
                    {
                        sb.AppendLine(line);
                    }
                    break;

                case MarkdownBlockType.Blockquote:
                    sb.AppendLine(StripInlineFormatting(block.RawText));
                    break;

                case MarkdownBlockType.HorizontalRule:
                    sb.AppendLine(new string('─', 40));
                    break;

                case MarkdownBlockType.EmptyLine:
                    sb.AppendLine();
                    break;
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Compiles Markdown into a vector PDF document using PdfSharpCore.
    /// </summary>
    public static void RenderToPdf(
        string markdownText,
        PdfDocument document,
        ConversionProfile? profile = null,
        CancellationToken ct = default)
    {
        var blocks = ParseBlocks(markdownText, ct);

        const double pageWidth = 595.28; // A4 pt
        const double pageHeight = 841.89;
        const double margin = 45.0;
        const double printableWidth = pageWidth - (2 * margin);
        const double maxY = pageHeight - margin;

        var heading1Font = new XFont("Arial", 18, XFontStyle.Bold);
        var heading2Font = new XFont("Arial", 14, XFontStyle.Bold);
        var heading3Font = new XFont("Arial", 12, XFontStyle.Bold);
        var heading4Font = new XFont("Arial", 10.5, XFontStyle.Bold);
        var bodyFont = new XFont("Arial", 10, XFontStyle.Regular);
        var boldFont = new XFont("Arial", 10, XFontStyle.Bold);
        var italicFont = new XFont("Arial", 10, XFontStyle.Italic);
        var codeFont = new XFont("Courier New", 8.5, XFontStyle.Regular);

        var primaryBrush = XBrushes.Black;
        var h1Brush = new XSolidBrush(XColor.FromArgb(255, 20, 35, 60));
        var bqBrush = new XSolidBrush(XColor.FromArgb(255, 30, 64, 175));
        var codeBgBrush = new XSolidBrush(XColor.FromArgb(255, 245, 247, 250));
        var codeTextBrush = new XSolidBrush(XColor.FromArgb(255, 15, 23, 42));
        var linePen = new XPen(XColor.FromArgb(255, 210, 215, 220), 0.75);
        var bqPen = new XPen(XColor.FromArgb(255, 59, 130, 246), 2.5);

        var currentPage = document.AddPage();
        currentPage.Size = PdfSharpCore.PageSize.A4;
        var gfx = XGraphics.FromPdfPage(currentPage);
        double currentY = margin;

        void EnsureSpace(double requiredHeight)
        {
            if (currentY + requiredHeight > maxY)
            {
                gfx.Dispose();
                currentPage = document.AddPage();
                currentPage.Size = PdfSharpCore.PageSize.A4;
                gfx = XGraphics.FromPdfPage(currentPage);
                currentY = margin;
            }
        }

        void DrawWrapped(string text, XFont font, XBrush brush, double x, double maxW, double lineHeight)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            string cleanText = StripInlineFormatting(text);
            var words = cleanText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string line = "";

            foreach (var word in words)
            {
                ct.ThrowIfCancellationRequested();

                // If word itself is wider than maxW, slice it
                var wordMeasure = gfx.MeasureString(word, font);
                if (wordMeasure.Width > maxW)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        EnsureSpace(lineHeight);
                        SafeDrawString(gfx, line, font, brush, new XPoint(x, currentY + font.Size));
                        currentY += lineHeight;
                        line = "";
                    }

                    int charIdx = 0;
                    while (charIdx < word.Length)
                    {
                        ct.ThrowIfCancellationRequested();
                        int take = 1;
                        while (charIdx + take <= word.Length && gfx.MeasureString(word.Substring(charIdx, take), font).Width <= maxW)
                        {
                            take++;
                        }
                        take = Math.Max(1, take - 1);
                        var chunk = word.Substring(charIdx, take);
                        EnsureSpace(lineHeight);
                        SafeDrawString(gfx, chunk, font, brush, new XPoint(x, currentY + font.Size));
                        currentY += lineHeight;
                        charIdx += take;
                    }
                    continue;
                }

                var testLine = string.IsNullOrEmpty(line) ? word : $"{line} {word}";
                var size = gfx.MeasureString(testLine, font);

                if (size.Width > maxW && !string.IsNullOrEmpty(line))
                {
                    EnsureSpace(lineHeight);
                    SafeDrawString(gfx, line, font, brush, new XPoint(x, currentY + font.Size));
                    currentY += lineHeight;
                    line = word;
                }
                else
                {
                    line = testLine;
                }
            }

            if (!string.IsNullOrEmpty(line))
            {
                EnsureSpace(lineHeight);
                SafeDrawString(gfx, line, font, brush, new XPoint(x, currentY + font.Size));
                currentY += lineHeight;
            }
        }

        try
        {
            foreach (var block in blocks)
            {
                ct.ThrowIfCancellationRequested();

                switch (block.Type)
                {
                    case MarkdownBlockType.Heading:
                        XFont hFont;
                        XBrush hBrush;
                        double spaceBefore, spaceAfter;

                        switch (block.Level)
                        {
                            case 1:
                                hFont = heading1Font;
                                hBrush = h1Brush;
                                spaceBefore = 16;
                                spaceAfter = 8;
                                break;
                            case 2:
                                hFont = heading2Font;
                                hBrush = h1Brush;
                                spaceBefore = 14;
                                spaceAfter = 6;
                                break;
                            case 3:
                                hFont = heading3Font;
                                hBrush = primaryBrush;
                                spaceBefore = 10;
                                spaceAfter = 4;
                                break;
                            default:
                                hFont = heading4Font;
                                hBrush = primaryBrush;
                                spaceBefore = 8;
                                spaceAfter = 4;
                                break;
                        }

                        EnsureSpace(hFont.Size + spaceBefore + spaceAfter);
                        currentY += spaceBefore;
                        DrawWrapped(block.RawText, hFont, hBrush, margin, printableWidth, hFont.Size * 1.3);
                        currentY += spaceAfter;
                        break;

                    case MarkdownBlockType.Paragraph:
                        DrawWrapped(block.RawText, bodyFont, primaryBrush, margin, printableWidth, 14);
                        currentY += 6;
                        break;

                    case MarkdownBlockType.UnorderedListItem:
                        EnsureSpace(14);
                        SafeDrawString(gfx, "•", bodyFont, primaryBrush, new XPoint(margin + 4, currentY + bodyFont.Size));
                        DrawWrapped(block.RawText, bodyFont, primaryBrush, margin + 16, printableWidth - 16, 14);
                        currentY += 2;
                        break;

                    case MarkdownBlockType.OrderedListItem:
                        EnsureSpace(14);
                        string numStr = $"{block.OrderNumber}.";
                        SafeDrawString(gfx, numStr, bodyFont, primaryBrush, new XPoint(margin + 4, currentY + bodyFont.Size));
                        DrawWrapped(block.RawText, bodyFont, primaryBrush, margin + 20, printableWidth - 20, 14);
                        currentY += 2;
                        break;

                    case MarkdownBlockType.CodeBlock:
                        if (block.Lines.Count == 0) break;
                        double codeLineH = 12;
                        double codePad = 6;
                        double totalCodeH = (block.Lines.Count * codeLineH) + (2 * codePad);

                        EnsureSpace(Math.Min(totalCodeH, 120));
                        double startY = currentY;

                        // Draw background
                        gfx.DrawRoundedRectangle(codeBgBrush, margin, startY, printableWidth, totalCodeH, 4, 4);

                        currentY += codePad;
                        foreach (var codeLine in block.Lines)
                        {
                            ct.ThrowIfCancellationRequested();
                            EnsureSpace(codeLineH);
                            SafeDrawString(gfx, codeLine, codeFont, codeTextBrush, new XPoint(margin + 8, currentY + codeFont.Size));
                            currentY += codeLineH;
                        }
                        currentY += codePad + 6;
                        break;

                    case MarkdownBlockType.Blockquote:
                        EnsureSpace(20);
                        double bqStartY = currentY;
                        DrawWrapped(block.RawText, italicFont, bqBrush, margin + 14, printableWidth - 14, 14);
                        gfx.DrawLine(bqPen, margin + 4, bqStartY, margin + 4, currentY);
                        currentY += 6;
                        break;

                    case MarkdownBlockType.HorizontalRule:
                        EnsureSpace(16);
                        currentY += 8;
                        gfx.DrawLine(linePen, margin, currentY, margin + printableWidth, currentY);
                        currentY += 8;
                        break;

                    case MarkdownBlockType.EmptyLine:
                        currentY += 4;
                        break;
                }
            }
        }
        finally
        {
            gfx.Dispose();
        }
    }

    /// <summary>
    /// Safe string drawing with character fallback to prevent font metric crashes on unmapped glyphs.
    /// </summary>
    public static void SafeDrawString(XGraphics gfx, string text, XFont font, XBrush brush, XPoint point)
    {
        if (string.IsNullOrEmpty(text)) return;

        try
        {
            gfx.DrawString(text, font, brush, point);
        }
        catch
        {
            // Font rendering exception on unmappable glyphs: sanitize characters
            var sanitized = SanitizeForFont(text);
            try
            {
                gfx.DrawString(sanitized, font, brush, point);
            }
            catch
            {
                // Last-resort fallback to plain ASCII representation
                var ascii = Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(sanitized));
                gfx.DrawString(ascii, font, brush, point);
            }
        }
    }

    /// <summary>
    /// Sanitizes non-printable and high surrogate characters for stable TrueType font rendering.
    /// </summary>
    public static string SanitizeForFont(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (char.IsControl(c) && c != '\t' && c != '\r' && c != '\n')
            {
                sb.Append(' ');
            }
            else if (char.IsSurrogate(c))
            {
                sb.Append('?');
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static string FormatInlineHtml(string rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return "";

        // First escape plain text against HTML injection
        string text = WebUtility.HtmlEncode(rawText);

        // Safe link extraction: [text](url)
        text = LinkRegex.Replace(text, match =>
        {
            string linkText = match.Groups[1].Value;
            string rawUrl = WebUtility.HtmlDecode(match.Groups[2].Value).Trim();

            // Validate URL scheme - strictly allow only safe schemes
            if (IsSafeUrl(rawUrl))
            {
                string safeUrl = WebUtility.HtmlEncode(rawUrl);
                return $"<a href=\"{safeUrl}\" target=\"_blank\" rel=\"noopener noreferrer\">{linkText}</a>";
            }
            else
            {
                // Disallowed scheme (javascript:, vbscript:, data:, etc.) -> render as sanitized text
                return $"{linkText} ({WebUtility.HtmlEncode(rawUrl)})";
            }
        });

        // Bold: **text** or __text__
        text = BoldRegex.Replace(text, "<strong>$2</strong>");

        // Italic: *text* or _text_
        text = ItalicRegex.Replace(text, "<em>$2</em>");

        // Inline code: `code`
        text = InlineCodeRegex.Replace(text, "<code>$1</code>");

        return text;
    }

    private static bool IsSafeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        // Block dangerous pseudo-protocols
        if (url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Allow relative anchors
        if (url.StartsWith("#") || url.StartsWith("/"))
            return true;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.Scheme == Uri.UriSchemeHttp ||
                   uri.Scheme == Uri.UriSchemeHttps ||
                   uri.Scheme == Uri.UriSchemeMailto;
        }

        return false;
    }

    private static string StripInlineFormatting(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        // Replace links [text](url) with text (url)
        string result = LinkRegex.Replace(input, "$1 ($2)");

        // Strip bold and italic
        result = BoldRegex.Replace(result, "$2");
        result = ItalicRegex.Replace(result, "$2");

        // Strip inline code backticks
        result = InlineCodeRegex.Replace(result, "$1");

        return result;
    }
}
