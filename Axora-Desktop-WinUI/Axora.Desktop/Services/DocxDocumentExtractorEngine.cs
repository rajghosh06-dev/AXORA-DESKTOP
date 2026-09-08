using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Deterministic extractor engine for Microsoft Word OpenXML (.docx) documents.
/// Pure .NET 9 BCL implementation using System.IO.Compression and System.Xml.Linq.
/// Zero external dependencies, zero Microsoft Office or COM automation.
/// Flow-based pagination: boundaries mapped to PageSemanticsType.LogicalSection on explicit page or section breaks.
/// Two-Tier text invariant: RawText holds ground truth; NormalizedText is strictly null.
/// </summary>
public sealed class DocxDocumentExtractorEngine : IDocumentExtractorEngine
{
    private static readonly XNamespace WNs = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace DcNk = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace CpNs = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    private static readonly XNamespace TypesNs = "http://schemas.openxmlformats.org/package/2006/content-types";

    private const string WordMainContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
    private const string WordMacroContentType = "application/vnd.ms-word.document.macroEnabled.main+xml";
    private const string WordTemplateContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml";

    private const int MaxZipEntryCount = 10_000;

    public string EngineIdentifier => "DocxDocumentExtractorEngine";

    public bool CanExtract(DetectedDocumentFormat format)
    {
        return format == DetectedDocumentFormat.Docx;
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

        // 1. Security Check: Stream File Size Limit
        if (documentStream.CanSeek && documentStream.Length > options.Security.MaxFileSizeBytes)
        {
            throw new FileSizeLimitExceededException(documentStream.Length, options.Security.MaxFileSizeBytes);
        }

        // 2. Open ZIP Archive & Container Security Inspection
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(documentStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            throw new DocumentCorruptException("DOCX file is not a valid ZIP archive.", ex);
        }

        string docTitle = "DOCX Document";
        string docAuthor = string.Empty;
        var pages = new List<ExtractedPageRaw>();
        var globalWarnings = new List<string>();

        using (archive)
        {
            ct.ThrowIfCancellationRequested();

            // 3. Defensive Package Inspection (Entry Count, Path Traversal, Uncompressed Size, Compression Ratio)
            long totalUncompressedBytes = 0;
            long totalCompressedBytes = 0;
            int entryCount = 0;

            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();
                entryCount++;
                if (entryCount > MaxZipEntryCount)
                {
                    throw new ExtractionSecurityLimitException(
                        "ERR_ZIP_ENTRY_LIMIT_EXCEEDED",
                        "DOCX package exceeds safe entry count.",
                        $"Archive entry count {entryCount} exceeded limit of {MaxZipEntryCount}.");
                }

                // Path traversal check
                string normalizedPath = entry.FullName.Replace('\\', '/');
                if (normalizedPath.Contains("../") || normalizedPath.StartsWith("/") || entry.FullName.Contains(".."))
                {
                    throw new DocumentCorruptException($"DOCX package contains illegal path traversal entry: '{entry.FullName}'.");
                }

                totalUncompressedBytes += entry.Length;
                totalCompressedBytes += entry.CompressedLength;
            }

            // Uncompressed size limit check
            if (totalUncompressedBytes > options.Security.MaxDocxUncompressedBytes)
            {
                throw new FileSizeLimitExceededException(totalUncompressedBytes, options.Security.MaxDocxUncompressedBytes);
            }

            // Compression ratio / Zip Bomb check
            if (totalCompressedBytes > 0 && totalUncompressedBytes > 50_000)
            {
                double ratio = (double)totalUncompressedBytes / Math.Max(totalCompressedBytes, 1);
                if (ratio > options.Security.MaxZipCompressionRatio)
                {
                    throw new ZipBombDetectedException(ratio, options.Security.MaxZipCompressionRatio);
                }
            }

            // 4. OpenXML Part Inspection & Validation
            var contentTypesEntry = archive.GetEntry("[Content_Types].xml");
            if (contentTypesEntry == null)
            {
                throw new DocumentCorruptException("DOCX package is missing mandatory [Content_Types].xml part.");
            }

            string? mainDocumentPartPath = null;
            var safeXmlSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = false,
                MaxCharactersInDocument = options.Security.MaxXmlDocumentChars
            };

            try
            {
                using var ctStream = contentTypesEntry.Open();
                using var ctReader = XmlReader.Create(ctStream, safeXmlSettings);
                var ctDoc = XDocument.Load(ctReader);

                // Look for Word main document override
                var overrideElement = ctDoc.Root?.Elements(TypesNs + "Override")
                    .FirstOrDefault(e =>
                    {
                        string ctVal = (string?)e.Attribute("ContentType") ?? string.Empty;
                        return ctVal.Equals(WordMainContentType, StringComparison.OrdinalIgnoreCase) ||
                               ctVal.Equals(WordMacroContentType, StringComparison.OrdinalIgnoreCase) ||
                               ctVal.Equals(WordTemplateContentType, StringComparison.OrdinalIgnoreCase);
                    });

                if (overrideElement != null)
                {
                    string partName = (string?)overrideElement.Attribute("PartName") ?? string.Empty;
                    mainDocumentPartPath = partName.TrimStart('/');
                }
            }
            catch (XmlException ex)
            {
                throw new DocumentCorruptException($"Malformed [Content_Types].xml in DOCX package: {ex.Message}", ex);
            }

            // Fallback to canonical location if not specified in Content_Types
            mainDocumentPartPath ??= "word/document.xml";

            var docEntry = archive.GetEntry(mainDocumentPartPath);
            if (docEntry == null)
            {
                throw new DocumentCorruptException($"DOCX package is missing WordProcessingML main document part: '{mainDocumentPartPath}'.");
            }

            // 5. Core Properties Metadata Extraction (docProps/core.xml)
            var corePropsEntry = archive.GetEntry("docProps/core.xml");
            if (corePropsEntry != null)
            {
                try
                {
                    using var coreStream = corePropsEntry.Open();
                    using var coreReader = XmlReader.Create(coreStream, safeXmlSettings);
                    var coreDoc = XDocument.Load(coreReader);

                    var titleElem = coreDoc.Descendants(DcNk + "title").FirstOrDefault();
                    if (titleElem != null && !string.IsNullOrWhiteSpace(titleElem.Value))
                    {
                        docTitle = titleElem.Value.Trim();
                    }

                    var creatorElem = coreDoc.Descendants(DcNk + "creator").FirstOrDefault();
                    if (creatorElem != null && !string.IsNullOrWhiteSpace(creatorElem.Value))
                    {
                        docAuthor = creatorElem.Value.Trim();
                    }
                }
                catch (XmlException)
                {
                    // Non-fatal: ignore corrupt metadata, retain defaults
                    globalWarnings.Add("DOCX core properties metadata was unreadable and was skipped.");
                }
            }

            progress?.Report(0.20);
            ct.ThrowIfCancellationRequested();

            // 6. Parse word/document.xml
            XDocument docXml;
            try
            {
                using var docStream = docEntry.Open();
                using var docReader = XmlReader.Create(docStream, safeXmlSettings);
                docXml = XDocument.Load(docReader);
            }
            catch (XmlException ex)
            {
                throw new DocumentCorruptException($"Malformed XML in {mainDocumentPartPath}: {ex.Message}", ex);
            }

            var body = docXml.Root?.Element(WNs + "body");
            if (body == null)
            {
                throw new DocumentCorruptException("DOCX document.xml does not contain a <w:body> element.");
            }

            progress?.Report(0.40);
            ct.ThrowIfCancellationRequested();

            // 7. Structural Extraction (Flow-based Logical Sections)
            var currentSectionSb = new StringBuilder();
            long totalExtractedChars = 0;

            void FlushSection()
            {
                string rawSectionText = currentSectionSb.ToString().TrimEnd();
                currentSectionSb.Clear();

                if (rawSectionText.Length == 0 && !options.PreserveEmptyPages && pages.Count > 0)
                {
                    return;
                }

                int nextPageNum = pages.Count + 1;
                if (options.MaxPages.HasValue && nextPageNum > options.MaxPages.Value)
                {
                    throw new PageLimitExceededException(nextPageNum, options.MaxPages.Value);
                }
                if (nextPageNum > options.Security.MaxPagesToExtract)
                {
                    throw new PageLimitExceededException(nextPageNum, options.Security.MaxPagesToExtract);
                }

                pages.Add(new ExtractedPageRaw
                {
                    PageNumber = nextPageNum,
                    PageSemantics = PageSemanticsType.LogicalSection,
                    WidthPt = 0,
                    HeightPt = 0,
                    RawText = rawSectionText,
                    NormalizedText = null, // Strictly null per Two-Tier invariant
                    ExtractedViaOcr = false,
                    Confidence = 1.0
                });
            }

            var bodyElements = body.Elements().ToList();
            int elementCount = bodyElements.Count;

            for (int i = 0; i < elementCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                var element = bodyElements[i];

                if (element.Name == WNs + "p")
                {
                    ProcessParagraph(element, currentSectionSb, FlushSection, ref totalExtractedChars, options, ct);
                }
                else if (element.Name == WNs + "tbl")
                {
                    ProcessTable(element, currentSectionSb, ref totalExtractedChars, options, ct);
                }
                else if (element.Name == WNs + "sdt")
                {
                    // Structured Document Tag - process content inside sdtContent
                    var sdtContent = element.Element(WNs + "sdtContent");
                    if (sdtContent != null)
                    {
                        foreach (var inner in sdtContent.Elements())
                        {
                            ct.ThrowIfCancellationRequested();
                            if (inner.Name == WNs + "p")
                            {
                                ProcessParagraph(inner, currentSectionSb, FlushSection, ref totalExtractedChars, options, ct);
                            }
                            else if (inner.Name == WNs + "tbl")
                            {
                                ProcessTable(inner, currentSectionSb, ref totalExtractedChars, options, ct);
                            }
                        }
                    }
                }
                else if (element.Name == WNs + "sectPr")
                {
                    // Body-level sectPr marks properties of the final section; do not flush extra empty page at end
                }

                if (i % 50 == 0 && elementCount > 0)
                {
                    progress?.Report(0.40 + (0.55 * (double)i / elementCount));
                }
            }

            // Flush remaining text or emit single empty section if document was completely empty
            if (currentSectionSb.Length > 0 || pages.Count == 0)
            {
                FlushSection();
            }
        }

        progress?.Report(1.0);

        return new RawExtractionResult
        {
            DocumentTitle = docTitle,
            Author = docAuthor,
            Format = DetectedDocumentFormat.Docx,
            Pages = pages,
            Duration = stopwatch.Elapsed,
            EngineIdentifier = EngineIdentifier,
            GlobalWarnings = globalWarnings,
            IsPartialSuccess = false
        };
    }

    private static void ProcessParagraph(
        XElement pElem,
        StringBuilder currentSectionSb,
        Action flushSection,
        ref long totalExtractedChars,
        ExtractionOptions options,
        CancellationToken ct)
    {
        var pPr = pElem.Element(WNs + "pPr");

        // 1. Check for pageBreakBefore
        if (pPr?.Element(WNs + "pageBreakBefore") != null)
        {
            if (currentSectionSb.Length > 0)
            {
                flushSection();
            }
        }

        // 2. Identify Heading Style
        int headingLevel = 0;
        string? styleVal = (string?)pPr?.Element(WNs + "pStyle")?.Attribute(WNs + "val");
        if (!string.IsNullOrWhiteSpace(styleVal))
        {
            headingLevel = ResolveHeadingLevel(styleVal);
        }

        if (headingLevel == 0)
        {
            // Check outline level if pStyle was not a recognised heading
            var outlineLvl = (string?)pPr?.Element(WNs + "outlineLvl")?.Attribute(WNs + "val");
            if (int.TryParse(outlineLvl, out int lvl) && lvl >= 0 && lvl <= 5)
            {
                headingLevel = lvl + 1;
            }
        }

        // 3. Identify List Item
        bool isListItem = pPr?.Element(WNs + "numPr") != null;

        // 4. Extract Text & Intra-Paragraph Breaks
        var paragraphSb = new StringBuilder();
        bool sectionBreakOnParagraph = pPr?.Element(WNs + "sectPr") != null;

        foreach (var child in pElem.Elements())
        {
            ct.ThrowIfCancellationRequested();

            if (child.Name == WNs + "r")
            {
                ProcessRun(child, paragraphSb, currentSectionSb, flushSection, headingLevel, isListItem);
            }
            else if (child.Name == WNs + "hyperlink")
            {
                // Local extraction only: iterate child runs without resolving external relationship targets
                foreach (var rChild in child.Elements(WNs + "r"))
                {
                    ProcessRun(rChild, paragraphSb, currentSectionSb, flushSection, headingLevel, isListItem);
                }
            }
        }

        string pText = paragraphSb.ToString();

        // Apply heading markdown prefix if appropriate
        if (headingLevel > 0 && !string.IsNullOrWhiteSpace(pText))
        {
            string prefix = new string('#', headingLevel) + " ";
            if (!pText.TrimStart().StartsWith("#"))
            {
                pText = prefix + pText.TrimStart();
            }
        }
        else if (isListItem && !string.IsNullOrWhiteSpace(pText))
        {
            if (!pText.TrimStart().StartsWith("-") && !pText.TrimStart().StartsWith("*"))
            {
                pText = "- " + pText.TrimStart();
            }
        }

        totalExtractedChars += pText.Length;
        if (totalExtractedChars > options.Security.MaxXmlDocumentChars)
        {
            throw new ExtractionSecurityLimitException(
                "ERR_SECURITY_LIMIT_EXCEEDED",
                "Document character count exceeds security threshold.",
                $"Extracted {totalExtractedChars} chars exceeding threshold {options.Security.MaxXmlDocumentChars}.");
        }

        if (!string.IsNullOrEmpty(pText))
        {
            currentSectionSb.Append(pText);
            currentSectionSb.Append("\n\n");
        }

        // If paragraph contained a sectPr, flush logical section
        if (sectionBreakOnParagraph)
        {
            flushSection();
        }
    }

    private static void ProcessRun(
        XElement rElem,
        StringBuilder paragraphSb,
        StringBuilder currentSectionSb,
        Action flushSection,
        int headingLevel,
        bool isListItem)
    {
        foreach (var node in rElem.Elements())
        {
            if (node.Name == WNs + "t")
            {
                paragraphSb.Append(node.Value);
            }
            else if (node.Name == WNs + "tab")
            {
                paragraphSb.Append('\t');
            }
            else if (node.Name == WNs + "br")
            {
                string? brType = (string?)node.Attribute(WNs + "type");
                if (string.Equals(brType, "page", StringComparison.OrdinalIgnoreCase))
                {
                    // Explicit page break inside run
                    string beforeBreak = paragraphSb.ToString();
                    paragraphSb.Clear();

                    if (!string.IsNullOrEmpty(beforeBreak))
                    {
                        currentSectionSb.Append(beforeBreak);
                    }

                    flushSection();
                }
                else
                {
                    paragraphSb.Append('\n');
                }
            }
            else if (node.Name == WNs + "lastRenderedPageBreak")
            {
                // Word saved explicit page break
                string beforeBreak = paragraphSb.ToString();
                paragraphSb.Clear();

                if (!string.IsNullOrEmpty(beforeBreak))
                {
                    currentSectionSb.Append(beforeBreak);
                }

                flushSection();
            }
        }
    }

    private static void ProcessTable(
        XElement tblElem,
        StringBuilder currentSectionSb,
        ref long totalExtractedChars,
        ExtractionOptions options,
        CancellationToken ct)
    {
        var rows = tblElem.Elements(WNs + "tr").ToList();
        if (rows.Count == 0) return;

        var tableGrid = new List<List<string>>();
        int maxCols = 0;

        foreach (var tr in rows)
        {
            ct.ThrowIfCancellationRequested();
            var rowCells = new List<string>();

            foreach (var tc in tr.Elements(WNs + "tc"))
            {
                var tcPr = tc.Element(WNs + "tcPr");
                int gridSpan = 1;
                string? gridSpanStr = (string?)tcPr?.Element(WNs + "gridSpan")?.Attribute(WNs + "val");
                if (int.TryParse(gridSpanStr, out int gs) && gs > 1)
                {
                    gridSpan = gs;
                }

                // Extract all paragraph text in cell
                var cellSb = new StringBuilder();
                foreach (var p in tc.Elements(WNs + "p"))
                {
                    foreach (var t in p.Descendants(WNs + "t"))
                    {
                        cellSb.Append(t.Value);
                    }
                    cellSb.Append(' ');
                }

                string cellText = cellSb.ToString().Trim();
                // Escape pipe characters and normalize newlines inside markdown cells
                cellText = cellText.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

                rowCells.Add(cellText);
                // For merged columns (gridSpan > 1), insert empty placeholder cells to maintain column count
                for (int s = 1; s < gridSpan; s++)
                {
                    rowCells.Add(string.Empty);
                }
            }

            if (rowCells.Count > maxCols)
            {
                maxCols = rowCells.Count;
            }

            tableGrid.Add(rowCells);
        }

        if (maxCols == 0) return;

        // Normalize row lengths
        foreach (var row in tableGrid)
        {
            while (row.Count < maxCols)
            {
                row.Add(string.Empty);
            }
        }

        // Format as Markdown table
        var mdTableSb = new StringBuilder();

        // Header Row
        mdTableSb.Append("| ");
        mdTableSb.Append(string.Join(" | ", tableGrid[0]));
        mdTableSb.Append(" |\n");

        // Separator Row
        mdTableSb.Append("| ");
        mdTableSb.Append(string.Join(" | ", Enumerable.Repeat("---", maxCols)));
        mdTableSb.Append(" |\n");

        // Data Rows
        for (int r = 1; r < tableGrid.Count; r++)
        {
            mdTableSb.Append("| ");
            mdTableSb.Append(string.Join(" | ", tableGrid[r]));
            mdTableSb.Append(" |\n");
        }

        string tableMd = mdTableSb.ToString();
        totalExtractedChars += tableMd.Length;
        if (totalExtractedChars > options.Security.MaxXmlDocumentChars)
        {
            throw new ExtractionSecurityLimitException(
                "ERR_SECURITY_LIMIT_EXCEEDED",
                "Document character count exceeds security threshold.",
                $"Extracted {totalExtractedChars} chars exceeding threshold {options.Security.MaxXmlDocumentChars}.");
        }

        currentSectionSb.Append(tableMd);
        currentSectionSb.Append("\n\n");
    }

    private static int ResolveHeadingLevel(string styleVal)
    {
        string norm = styleVal.Replace(" ", "").ToLowerInvariant();
        return norm switch
        {
            "heading1" or "title" => 1,
            "heading2" or "subtitle" => 2,
            "heading3" => 3,
            "heading4" => 4,
            "heading5" => 5,
            "heading6" => 6,
            _ => 0
        };
    }
}
