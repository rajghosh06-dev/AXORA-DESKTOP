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
/// Deterministic RFC 4180 parser and extractor for CSV and TSV tabular data files.
/// Preserves source cell content without executing formulas or macros.
/// Enforces strict row, column, and field boundaries.
/// Formats extracted tabular data into structured Markdown table representations under LogicalSection page semantics.
/// </summary>
public sealed class DelimitedTextExtractorEngine : IDocumentExtractorEngine
{
    private const int MaxRowsLimit = 50_000;
    private const int MaxColumnsLimit = 256;
    private const int MaxFieldLengthLimit = 32_768;

    public string EngineIdentifier => "DelimitedTextExtractorEngine";

    public bool CanExtract(DetectedDocumentFormat format)
    {
        return format == DetectedDocumentFormat.DelimitedText;
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

        long startPos = documentStream.CanSeek ? documentStream.Position : 0;
        Encoding encoding = DetectEncoding(documentStream, out int bomLength);

        if (documentStream.CanSeek)
        {
            documentStream.Position = startPos + bomLength;
        }

        var globalWarnings = new List<string>();

        string rawContent;
        using (var reader = new StreamReader(documentStream, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: true))
        {
            rawContent = await reader.ReadToEndAsync(ct);
        }

        ct.ThrowIfCancellationRequested();
        progress?.Report(0.30);

        if (rawContent.Length > options.Security.MaxXmlDocumentChars)
        {
            throw new ExtractionSecurityLimitException(
                "ERR_SECURITY_LIMIT_EXCEEDED",
                "Delimited document exceeds character length limit.",
                $"Extracted {rawContent.Length} characters exceeding threshold {options.Security.MaxXmlDocumentChars}.");
        }

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return new RawExtractionResult
            {
                DocumentTitle = "Empty Delimited Document",
                Format = DetectedDocumentFormat.DelimitedText,
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
                GlobalWarnings = globalWarnings,
                IsPartialSuccess = false
            };
        }

        char delimiter = DetectDelimiter(rawContent);

        // Parse RFC 4180 records
        var rows = ParseDelimitedRecords(rawContent, delimiter, globalWarnings, ct);
        progress?.Report(0.70);

        // Build Markdown Table representation
        string formattedMarkdownTable = FormatAsMarkdownTable(rows, globalWarnings);

        var pages = new List<ExtractedPageRaw>
        {
            new ExtractedPageRaw
            {
                PageNumber = 1,
                PageSemantics = PageSemanticsType.LogicalSection,
                RawText = formattedMarkdownTable,
                NormalizedText = null,
                ExtractedViaOcr = false,
                Confidence = 1.0
            }
        };

        progress?.Report(1.0);
        stopwatch.Stop();

        return new RawExtractionResult
        {
            DocumentTitle = $"Delimited Table ({rows.Count} rows)",
            Format = DetectedDocumentFormat.DelimitedText,
            Pages = pages,
            Duration = stopwatch.Elapsed,
            EngineIdentifier = EngineIdentifier,
            GlobalWarnings = globalWarnings,
            IsPartialSuccess = false
        };
    }

    private static char DetectDelimiter(string text)
    {
        int newlineIdx = text.IndexOfAny(new[] { '\r', '\n' });
        string firstLine = newlineIdx >= 0 ? text[..newlineIdx] : text;

        int tabCount = CountChar(firstLine, '\t');
        int commaCount = CountChar(firstLine, ',');
        int semicolonCount = CountChar(firstLine, ';');

        if (tabCount > commaCount && tabCount > semicolonCount) return '\t';
        if (semicolonCount > commaCount && semicolonCount > tabCount) return ';';
        return ',';
    }

    private static int CountChar(string str, char c)
    {
        int count = 0;
        for (int i = 0; i < str.Length; i++)
        {
            if (str[i] == c) count++;
        }
        return count;
    }

    private static List<List<string>> ParseDelimitedRecords(
        string text,
        char delimiter,
        List<string> warnings,
        CancellationToken ct)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var currentField = new StringBuilder();

        bool inQuotes = false;
        int i = 0;
        int len = text.Length;

        while (i < len)
        {
            ct.ThrowIfCancellationRequested();
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < len && text[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i += 2;
                        continue;
                    }
                    else
                    {
                        inQuotes = false;
                        i++;
                        continue;
                    }
                }
                else
                {
                    if (currentField.Length >= MaxFieldLengthLimit)
                    {
                        throw new ExtractionSecurityLimitException(
                            "ERR_SECURITY_LIMIT_EXCEEDED",
                            "CSV field exceeds maximum allowed length.",
                            $"Field exceeded limit of {MaxFieldLengthLimit} characters.");
                    }
                    currentField.Append(c);
                    i++;
                    continue;
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                    i++;
                    continue;
                }
                else if (c == delimiter)
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    if (currentRow.Count > MaxColumnsLimit)
                    {
                        throw new ExtractionSecurityLimitException(
                            "ERR_SECURITY_LIMIT_EXCEEDED",
                            "CSV row exceeds maximum column count.",
                            $"Row exceeded column limit of {MaxColumnsLimit}.");
                    }
                    i++;
                    continue;
                }
                else if (c == '\r')
                {
                    if (i + 1 < len && text[i + 1] == '\n')
                    {
                        i++;
                    }
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    rows.Add(currentRow);
                    currentRow = new List<string>();

                    if (rows.Count >= MaxRowsLimit)
                    {
                        throw new PageLimitExceededException(rows.Count, MaxRowsLimit);
                    }
                    i++;
                    continue;
                }
                else if (c == '\n')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    rows.Add(currentRow);
                    currentRow = new List<string>();

                    if (rows.Count >= MaxRowsLimit)
                    {
                        throw new PageLimitExceededException(rows.Count, MaxRowsLimit);
                    }
                    i++;
                    continue;
                }
                else
                {
                    if (currentField.Length >= MaxFieldLengthLimit)
                    {
                        throw new ExtractionSecurityLimitException(
                            "ERR_SECURITY_LIMIT_EXCEEDED",
                            "CSV field exceeds maximum allowed length.",
                            $"Field exceeded limit of {MaxFieldLengthLimit} characters.");
                    }
                    currentField.Append(c);
                    i++;
                    continue;
                }
            }
        }

        if (inQuotes)
        {
            warnings.Add("Unclosed quote encountered at end of delimited file.");
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            rows.Add(currentRow);
        }

        return rows;
    }

    private static string FormatAsMarkdownTable(List<List<string>> rows, List<string> warnings)
    {
        if (rows.Count == 0) return string.Empty;

        int maxCols = 0;
        foreach (var r in rows)
        {
            if (r.Count > maxCols) maxCols = r.Count;
        }

        if (maxCols == 0) return string.Empty;

        var sb = new StringBuilder();

        // Header row
        var header = rows[0];
        sb.Append('|');
        for (int c = 0; c < maxCols; c++)
        {
            string val = c < header.Count ? SanitizeCell(header[c]) : $"Column {c + 1}";
            sb.Append(' ').Append(val).Append(" |");
        }
        sb.AppendLine();

        // Separator row
        sb.Append('|');
        for (int c = 0; c < maxCols; c++)
        {
            sb.Append(" --- |");
        }
        sb.AppendLine();

        // Data rows
        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.Count != maxCols)
            {
                warnings.Add($"Row {r + 1} has {row.Count} columns (expected {maxCols}).");
            }

            sb.Append('|');
            for (int c = 0; c < maxCols; c++)
            {
                string val = c < row.Count ? SanitizeCell(row[c]) : string.Empty;
                sb.Append(' ').Append(val).Append(" |");
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string SanitizeCell(string cell)
    {
        if (string.IsNullOrEmpty(cell)) return string.Empty;
        return cell.Replace("|", "\\|").Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    private static Encoding DetectEncoding(Stream stream, out int bomLength)
    {
        bomLength = 0;
        if (!stream.CanSeek) return Encoding.UTF8;

        long pos = stream.Position;
        byte[] bom = new byte[4];
        int read = stream.Read(bom, 0, 4);
        stream.Position = pos;

        if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
        {
            bomLength = 3;
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }
        if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
        {
            bomLength = 2;
            return Encoding.Unicode;
        }
        if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
        {
            bomLength = 2;
            return Encoding.BigEndianUnicode;
        }

        return Encoding.UTF8;
    }
}
