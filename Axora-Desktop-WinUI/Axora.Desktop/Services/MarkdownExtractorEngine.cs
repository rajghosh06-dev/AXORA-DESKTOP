using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Deterministic extractor engine for CommonMark and GitHub Flavored Markdown documents.
/// Preserves headings, paragraphs, lists, tables, and fenced code blocks verbatim.
/// Enforces LogicalSection page semantics at structural headings (# / ##).
/// Never fetches remote links or executes embedded content.
/// </summary>
public sealed class MarkdownExtractorEngine : IDocumentExtractorEngine
{
    private readonly IDocumentPageBuilder _pageBuilder;

    public string EngineIdentifier => "MarkdownExtractorEngine";

    public MarkdownExtractorEngine(IDocumentPageBuilder? pageBuilder = null)
    {
        _pageBuilder = pageBuilder ?? new DocumentPageBuilder();
    }

    public bool CanExtract(DetectedDocumentFormat format)
    {
        return format == DetectedDocumentFormat.Markdown;
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

        string rawContent;
        using (var reader = new StreamReader(documentStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true))
        {
            rawContent = await reader.ReadToEndAsync(ct);
        }

        ct.ThrowIfCancellationRequested();
        progress?.Report(0.40);

        if (rawContent.Length > options.Security.MaxXmlDocumentChars)
        {
            throw new ExtractionSecurityLimitException(
                "ERR_SECURITY_LIMIT_EXCEEDED",
                "Markdown document exceeds maximum character length.",
                $"Extracted {rawContent.Length} characters exceeding threshold {options.Security.MaxXmlDocumentChars}.");
        }

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return new RawExtractionResult
            {
                DocumentTitle = "Empty Markdown Document",
                Format = DetectedDocumentFormat.Markdown,
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

        var sectionTexts = SplitIntoLogicalSections(rawContent, ct);
        progress?.Report(0.80);

        var pages = new List<ExtractedPageRaw>(sectionTexts.Count);
        for (int i = 0; i < sectionTexts.Count; i++)
        {
            pages.Add(new ExtractedPageRaw
            {
                PageNumber = i + 1,
                PageSemantics = PageSemanticsType.LogicalSection,
                RawText = sectionTexts[i],
                NormalizedText = null,
                ExtractedViaOcr = false,
                Confidence = 1.0
            });
        }

        string docTitle = ExtractFirstH1Title(rawContent) ?? "Markdown Document";

        progress?.Report(1.0);
        stopwatch.Stop();

        return new RawExtractionResult
        {
            DocumentTitle = docTitle,
            Format = DetectedDocumentFormat.Markdown,
            Pages = pages,
            Duration = stopwatch.Elapsed,
            EngineIdentifier = EngineIdentifier,
            GlobalWarnings = [],
            IsPartialSuccess = false
        };
    }

    private static List<string> SplitIntoLogicalSections(string markdown, CancellationToken ct)
    {
        var sections = new List<string>();
        var currentSection = new StringBuilder();

        var lines = markdown.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        bool inFencedCode = false;

        for (int i = 0; i < lines.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            string line = lines[i];
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
            {
                inFencedCode = !inFencedCode;
            }

            bool isSectionHeading = !inFencedCode &&
                (trimmed.StartsWith("# ") || trimmed.StartsWith("## "));

            if (isSectionHeading && currentSection.Length > 0)
            {
                sections.Add(currentSection.ToString().TrimEnd());
                currentSection.Clear();
            }

            currentSection.AppendLine(line);
        }

        if (currentSection.Length > 0)
        {
            sections.Add(currentSection.ToString().TrimEnd());
        }

        return sections.Count > 0 ? sections : new List<string> { markdown };
    }

    private static string? ExtractFirstH1Title(string markdown)
    {
        var lines = markdown.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        bool inFencedCode = false;
        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
            {
                inFencedCode = !inFencedCode;
                continue;
            }
            if (!inFencedCode && trimmed.StartsWith("# "))
            {
                return trimmed[2..].Trim();
            }
        }
        return null;
    }
}
