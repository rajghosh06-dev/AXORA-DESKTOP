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
/// Deterministic extractor engine for plain text documents supporting UTF-8, UTF-16 LE/BE, ASCII, and Windows-1252.
/// Preserves exact RawText, line breaks, and whitespace without performing normalization.
/// Virtual pagination maps on form-feed characters (\f) or represents unsegmented text as VirtualPage.
/// </summary>
public sealed class PlainTextExtractorEngine : IDocumentExtractorEngine
{
    private readonly IDocumentPageBuilder _pageBuilder;

    static PlainTextExtractorEngine()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch { }
    }

    public string EngineIdentifier => "PlainTextExtractorEngine";

    public PlainTextExtractorEngine(IDocumentPageBuilder? pageBuilder = null)
    {
        _pageBuilder = pageBuilder ?? new DocumentPageBuilder();
    }

    public bool CanExtract(DetectedDocumentFormat format)
    {
        return format == DetectedDocumentFormat.PlainText;
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

        // 1. Security check: File size
        if (documentStream.CanSeek && documentStream.Length > options.Security.MaxFileSizeBytes)
        {
            throw new FileSizeLimitExceededException(documentStream.Length, options.Security.MaxFileSizeBytes);
        }

        // 2. Encoding detection and stream reading
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
        progress?.Report(0.50);

        // 3. Security check: Character count limit
        if (rawContent.Length > options.Security.MaxXmlDocumentChars)
        {
            throw new ExtractionSecurityLimitException(
                "ERR_SECURITY_LIMIT_EXCEEDED",
                "Document exceeds maximum character length.",
                $"Extracted {rawContent.Length} characters exceeding limit of {options.Security.MaxXmlDocumentChars}.");
        }

        // 4. Virtual Page Slicing (form feed \f check)
        var pages = new List<ExtractedPageRaw>();

        if (rawContent.Contains('\f'))
        {
            string[] rawSections = rawContent.Split('\f');
            for (int i = 0; i < rawSections.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                pages.Add(new ExtractedPageRaw
                {
                    PageNumber = i + 1,
                    PageSemantics = PageSemanticsType.VirtualPage,
                    RawText = rawSections[i],
                    NormalizedText = null,
                    ExtractedViaOcr = false,
                    Confidence = 1.0
                });
            }
        }
        else
        {
            pages.Add(new ExtractedPageRaw
            {
                PageNumber = 1,
                PageSemantics = PageSemanticsType.VirtualPage,
                RawText = rawContent,
                NormalizedText = null,
                ExtractedViaOcr = false,
                Confidence = 1.0
            });
        }

        progress?.Report(1.0);
        stopwatch.Stop();

        return new RawExtractionResult
        {
            DocumentTitle = "Plain Text Document",
            Format = DetectedDocumentFormat.PlainText,
            Pages = pages,
            Duration = stopwatch.Elapsed,
            EngineIdentifier = EngineIdentifier,
            GlobalWarnings = globalWarnings,
            IsPartialSuccess = false
        };
    }

    private static Encoding DetectEncoding(Stream stream, out int bomLength)
    {
        bomLength = 0;
        if (!stream.CanSeek)
        {
            return Encoding.UTF8;
        }

        long pos = stream.Position;
        byte[] bom = new byte[4];
        int read = stream.Read(bom, 0, 4);
        stream.Position = pos;

        if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
        {
            bomLength = 3;
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }
        if (read >= 4 && bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00)
        {
            bomLength = 4;
            return Encoding.UTF32;
        }
        if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
        {
            bomLength = 2;
            return Encoding.Unicode; // UTF-16 LE
        }
        if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
        {
            bomLength = 2;
            return Encoding.BigEndianUnicode; // UTF-16 BE
        }

        // Test UTF-8 validity
        byte[] sample = new byte[Math.Min(stream.Length, 4096)];
        int sampleRead = stream.Read(sample, 0, sample.Length);
        stream.Position = pos;

        try
        {
            var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            utf8Strict.GetString(sample, 0, sampleRead);
            return Encoding.UTF8;
        }
        catch (DecoderFallbackException)
        {
            // Fallback to ANSI / Windows-1252 or Latin1
            try
            {
                return Encoding.GetEncoding(1252);
            }
            catch
            {
                return Encoding.Latin1;
            }
        }
    }
}
