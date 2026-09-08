using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Deterministic extractor engine for local standalone HTML documents.
/// Strictly local-only: zero network requests, zero remote stylesheet/image fetching, zero script execution.
/// Completely removes scripts, stylesheets, and iframes before extracting structured text.
/// Emits logical sections at article/section/h1 boundaries under LogicalSection page semantics.
/// </summary>
public sealed class LocalHtmlExtractorEngine : IDocumentExtractorEngine
{
    public string EngineIdentifier => "LocalHtmlExtractorEngine";

    public bool CanExtract(DetectedDocumentFormat format)
    {
        return format == DetectedDocumentFormat.LocalHtml;
    }

    public async Task<RawExtractionResult> ExtractAsync(
        Stream documentStream,
        ExtractionOptions options,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(documentStream);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var stopwatch = Stopwatch.StartNew();
        progress?.Report(0.05);
        ct.ThrowIfCancellationRequested();

        if (documentStream.CanSeek && documentStream.Length > options.Security.MaxFileSizeBytes)
        {
            throw new FileSizeLimitExceededException(documentStream.Length, options.Security.MaxFileSizeBytes);
        }

        string rawHtml;
        using (var reader = new StreamReader(documentStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true))
        {
            rawHtml = await reader.ReadToEndAsync(ct);
        }

        ct.ThrowIfCancellationRequested();
        progress?.Report(0.30);

        if (rawHtml.Length > options.Security.MaxXmlDocumentChars)
        {
            throw new ExtractionSecurityLimitException(
                "ERR_SECURITY_LIMIT_EXCEEDED",
                "HTML document exceeds maximum character limit.",
                $"Extracted {rawHtml.Length} characters exceeding threshold {options.Security.MaxXmlDocumentChars}.");
        }

        if (string.IsNullOrWhiteSpace(rawHtml))
        {
            return new RawExtractionResult
            {
                DocumentTitle = "Empty HTML Document",
                Format = DetectedDocumentFormat.LocalHtml,
                Pages =
                [
                    new ExtractedPageRaw
                    {
                        PageNumber = 1,
                        PageSemantics = PageSemanticsType.LogicalSection,
                        RawText = string.Empty,
                        NormalizedText = null,
                        ExtractedViaOcr = false,
                        Confidence = 1.0
                    }
                ],
                Duration = stopwatch.Elapsed,
                EngineIdentifier = EngineIdentifier,
                GlobalWarnings = [],
                IsPartialSuccess = false
            };
        }

        string docTitle = ExtractTitle(rawHtml) ?? "Local HTML Document";
        var sectionHtmls = PartitionHtmlSections(rawHtml);
        progress?.Report(0.60);

        var pages = new List<ExtractedPageRaw>();
        for (int i = 0; i < sectionHtmls.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            string cleanText = ExtractCleanTextFromHtml(sectionHtmls[i]);
            pages.Add(new ExtractedPageRaw
            {
                PageNumber = i + 1,
                PageSemantics = PageSemanticsType.LogicalSection,
                RawText = cleanText,
                NormalizedText = null,
                ExtractedViaOcr = false,
                Confidence = 1.0
            });
        }

        if (pages.Count == 0)
        {
            pages.Add(new ExtractedPageRaw
            {
                PageNumber = 1,
                PageSemantics = PageSemanticsType.LogicalSection,
                RawText = string.Empty,
                NormalizedText = null,
                ExtractedViaOcr = false,
                Confidence = 1.0
            });
        }

        progress?.Report(1.0);
        stopwatch.Stop();

        return new RawExtractionResult
        {
            DocumentTitle = docTitle,
            Format = DetectedDocumentFormat.LocalHtml,
            Pages = pages,
            Duration = stopwatch.Elapsed,
            EngineIdentifier = EngineIdentifier,
            GlobalWarnings = [],
            IsPartialSuccess = false
        };
    }

    private static string? ExtractTitle(string html)
    {
        var match = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match.Success)
        {
            return WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
        }
        return null;
    }

    private static List<string> PartitionHtmlSections(string html)
    {
        var articleMatches = Regex.Matches(html, @"<article[^>]*>([\s\S]*?)</article>", RegexOptions.IgnoreCase);
        if (articleMatches.Count > 1)
        {
            var sections = new List<string>(articleMatches.Count);
            foreach (Match m in articleMatches)
            {
                sections.Add(m.Groups[1].Value);
            }
            return sections;
        }

        var h1Matches = Regex.Split(html, @"(?=<h1[^>]*>)", RegexOptions.IgnoreCase);
        if (h1Matches.Length > 1)
        {
            var sections = new List<string>();
            foreach (var part in h1Matches)
            {
                if (!string.IsNullOrWhiteSpace(ExtractCleanTextFromHtml(part)))
                {
                    sections.Add(part);
                }
            }
            if (sections.Count > 0) return sections;
        }

        return new List<string> { html };
    }

    public static string ExtractCleanTextFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        string text = Regex.Replace(html, @"<!--[\s\S]*?-->", string.Empty);
        text = Regex.Replace(text, @"<script[^>]*>[\s\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<style[^>]*>[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<noscript[^>]*>[\s\S]*?</noscript>", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<iframe[^>]*>[\s\S]*?</iframe>", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<object[^>]*>[\s\S]*?</object>", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<embed[^>]*>", string.Empty, RegexOptions.IgnoreCase);

        text = Regex.Replace(text, @"<(h[1-6]|p|div|blockquote|pre|tr|li)[^>]*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<(td|th)[^>]*>", " | ", RegexOptions.IgnoreCase);

        text = Regex.Replace(text, @"<[^>]+>", string.Empty);
        text = WebUtility.HtmlDecode(text);

        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        return text.Trim();
    }
}
