using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Pdf.Content;
using PdfSharpCore.Pdf.Content.Objects;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// High-performance local PDF extraction service using PdfSharpCore.
/// Extracts document structure, metadata, and text content with zero-cloud guarantees.
/// </summary>
public sealed class PdfExtractionService : IPdfExtractionService
{
    private readonly ILogger<PdfExtractionService> _logger;

    public PdfExtractionService(ILogger<PdfExtractionService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractPdfContentAsync(Stream pdfStream, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var document = PdfReader.Open(pdfStream, PdfDocumentOpenMode.ReadOnly);
                var sb = new StringBuilder();

                sb.AppendLine("[Document Metadata]");
                sb.AppendLine($"Title: {document.Info.Title}");
                sb.AppendLine($"Author: {document.Info.Author}");
                sb.AppendLine($"Pages: {document.PageCount}");
                sb.AppendLine(new string('─', 40));

                for (int i = 0; i < document.PageCount; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var page = document.Pages[i];
                    sb.AppendLine($"\n--- Page {i + 1} ({page.Width}x{page.Height}pt) ---");

                    try
                    {
                        var content = ContentReader.ReadContent(page);
                        ExtractTextFromContent(content, sb);
                    }
                    catch (Exception pageEx)
                    {
                        _logger.LogWarning(pageEx, "Failed to read content stream on page {Page}", i + 1);
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse PDF stream.");
                return $"[PDF Extraction Error: {ex.Message}]";
            }
        }, ct);
    }

    public async Task<string> ExtractPdfFromFileAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("PDF file not found", filePath);

        await using var stream = File.OpenRead(filePath);
        return await ExtractPdfContentAsync(stream, ct);
    }

    private static void ExtractTextFromContent(CObject obj, StringBuilder sb)
    {
        if (obj is CSequence sequence)
        {
            foreach (var element in sequence)
            {
                ExtractTextFromContent(element, sb);
            }
        }
        else if (obj is CString cString)
        {
            if (!string.IsNullOrWhiteSpace(cString.Value))
            {
                sb.Append(cString.Value).Append(' ');
            }
        }
        else if (obj is CArray cArray)
        {
            foreach (var element in cArray)
            {
                ExtractTextFromContent(element, sb);
            }
        }
    }
}
