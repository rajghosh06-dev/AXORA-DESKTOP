using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Authoritative extraction orchestrator coordinating format detection, engine selection,
/// raw extraction, single-point text normalization, page building, passage chunking, and diagnostic reporting.
/// Enforces strict Two-Tier text representation (RawText ground truth vs. NormalizedText representation),
/// page boundary isolation, one-page failure resilience, and privacy-preserving scoped telemetry.
/// </summary>
public sealed class ScholarExtractionOrchestrator : IScholarExtractionOrchestrator
{
    private readonly IDocumentFormatDetector _formatDetector;
    private readonly IReadOnlyList<IDocumentExtractorEngine> _extractorEngines;
    private readonly ITextNormalizer _normalizer;
    private readonly IDocumentPageBuilder _pageBuilder;
    private readonly IPassageChunker? _passageChunker;
    private readonly IScholarLibraryService? _libraryService;
    private readonly ILogger<ScholarExtractionOrchestrator>? _logger;

    public ScholarExtractionOrchestrator(
        IDocumentFormatDetector formatDetector,
        IEnumerable<IDocumentExtractorEngine> extractorEngines,
        ITextNormalizer normalizer,
        IDocumentPageBuilder pageBuilder,
        IPassageChunker? passageChunker = null,
        IScholarLibraryService? libraryService = null,
        ILogger<ScholarExtractionOrchestrator>? logger = null)
    {
        _formatDetector = formatDetector ?? throw new ArgumentNullException(nameof(formatDetector));
        _extractorEngines = extractorEngines?.ToList() ?? throw new ArgumentNullException(nameof(extractorEngines));
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        _pageBuilder = pageBuilder ?? throw new ArgumentNullException(nameof(pageBuilder));
        _passageChunker = passageChunker;
        _libraryService = libraryService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ScholarExtractionResult> IngestAndProcessFileAsync(
        string sourceFilePath,
        ExtractionOptions options,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        options ??= new ExtractionOptions();
        options.Validate();

        ct.ThrowIfCancellationRequested();

        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"Source document file not found: '{sourceFilePath}'", sourceFilePath);
        }

        var fileInfo = new FileInfo(sourceFilePath);
        if (options.Security.MaxFileSizeBytes > 0 && fileInfo.Length > options.Security.MaxFileSizeBytes)
        {
            throw new FileSizeLimitExceededException(fileInfo.Length, options.Security.MaxFileSizeBytes);
        }

        // Guarantees read-only shared access: original user source file is never modified or locked exclusively
        await using var stream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
        return await IngestAndProcessCoreAsync(stream, Path.GetFileName(sourceFilePath), sourceFilePath, options, progress, ct);
    }

    /// <inheritdoc/>
    public async Task<ScholarExtractionResult> IngestAndProcessAsync(
        Stream sourceStream,
        string fileName,
        ExtractionOptions options,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        options ??= new ExtractionOptions();
        options.Validate();

        return await IngestAndProcessCoreAsync(sourceStream, fileName, null, options, progress, ct);
    }

    private async Task<ScholarExtractionResult> IngestAndProcessCoreAsync(
        Stream sourceStream,
        string fileName,
        string? sourceFilePath,
        ExtractionOptions options,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var totalSw = Stopwatch.StartNew();
        string documentId = Guid.NewGuid().ToString("N");

        progress?.Report(0.05);

        // ── Stage 1: Format Sniffing & Detection ──
        long initialPos = sourceStream.CanSeek ? sourceStream.Position : 0;
        var detection = _formatDetector.DetectFormat(sourceStream, fileName);
        if (sourceStream.CanSeek)
        {
            sourceStream.Position = initialPos;
        }

        if (detection.Format == DetectedDocumentFormat.Unknown || !detection.IsSupported)
        {
            throw new UnsupportedDocumentFormatException(detection.MimeType, $"Document format '{detection.Format}' for file '{fileName}' is not supported.");
        }

        progress?.Report(0.15);

        // ── Stage 2: Engine Selection & Dispatch ──
        var engine = _extractorEngines.FirstOrDefault(e => e.CanExtract(detection.Format));
        if (engine == null)
        {
            throw new UnsupportedDocumentFormatException(detection.MimeType, $"No extraction engine registered to extract format '{detection.Format}'.");
        }

        progress?.Report(0.25);

        // ── Stage 3: Raw Extraction Execution ──
        RawExtractionResult rawResult;
        try
        {
            rawResult = await engine.ExtractAsync(sourceStream, options, progress, ct);
        }
        catch (OperationCanceledException)
        {
            // Transparent cancellation propagation
            throw;
        }
        catch (ScholarExtractionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DocumentCorruptException($"Extractor engine '{engine.EngineIdentifier}' failed on '{fileName}': {ex.Message}", ex);
        }

        ct.ThrowIfCancellationRequested();

        // Enforce MaxPages security bound
        if (options.Security.MaxPagesToExtract > 0 && rawResult.Pages.Count > options.Security.MaxPagesToExtract)
        {
            throw new PageLimitExceededException(rawResult.Pages.Count, options.Security.MaxPagesToExtract);
        }

        progress?.Report(0.55);

        // ── Stage 4: Single Authoritative Normalization Entry Point ──
        // Extractor engines strictly do not normalize text. Normalization is derived exactly once here.
        var normSw = Stopwatch.StartNew();
        var normalizedPages = new List<ExtractedPageRaw>(rawResult.Pages.Count);
        var pageDiagnostics = new List<PageNormalizationTelemetry>(rawResult.Pages.Count);

        int successfulPages = 0;
        int failedPages = 0;
        int fallbackPages = 0;
        int totalInputChars = 0;
        int totalOutputChars = 0;
        bool hasOcr = false;

        foreach (var rawPage in rawResult.Pages)
        {
            ct.ThrowIfCancellationRequested();
            var pageSw = Stopwatch.StartNew();

            int inChars = rawPage.RawText?.Length ?? 0;
            totalInputChars += inChars;
            if (rawPage.ExtractedViaOcr) hasOcr = true;

            ExtractedPageRaw normPage;
            NormalizationStatus pageStatus;
            string pageStatusCode;

            try
            {
                normPage = _normalizer.NormalizePage(rawPage, rawResult.Format, options.Normalization);
                pageSw.Stop();

                bool isFallback = normPage.DiagnosticWarning?.Contains("ERR_NORMALIZATION_FAILED") == true
                    || (normPage.NormalizedText == rawPage.RawText && rawPage.DiagnosticWarning != normPage.DiagnosticWarning);

                if (isFallback)
                {
                    pageStatus = NormalizationStatus.FallbackToRaw;
                    pageStatusCode = NormalizationDiagnosticCodes.FallbackRaw;
                    fallbackPages++;
                }
                else if (!string.IsNullOrEmpty(normPage.DiagnosticWarning))
                {
                    pageStatus = NormalizationStatus.SucceededWithWarnings;
                    pageStatusCode = NormalizationDiagnosticCodes.Warning;
                    successfulPages++;
                }
                else
                {
                    pageStatus = NormalizationStatus.Succeeded;
                    pageStatusCode = NormalizationDiagnosticCodes.Ok;
                    successfulPages++;
                }
            }
            catch (OperationCanceledException)
            {
                // Transparent cancellation propagation: do not swallow
                throw;
            }
            catch (Exception ex)
            {
                pageSw.Stop();
                failedPages++;
                pageStatus = NormalizationStatus.Failed;
                pageStatusCode = NormalizationDiagnosticCodes.Failed;

                // One-page isolation: fallback to RawText so the remainder of the document is preserved
                normPage = new ExtractedPageRaw
                {
                    PageNumber = rawPage.PageNumber,
                    PageSemantics = rawPage.PageSemantics,
                    WidthPt = rawPage.WidthPt,
                    HeightPt = rawPage.HeightPt,
                    RawText = rawPage.RawText ?? string.Empty, // Ground truth preserved
                    NormalizedText = rawPage.RawText ?? string.Empty, // Safe fallback
                    ExtractedViaOcr = rawPage.ExtractedViaOcr,
                    Confidence = rawPage.Confidence,
                    DiagnosticWarning = rawPage.DiagnosticWarning != null
                        ? $"{rawPage.DiagnosticWarning}; ERR_NORMALIZATION_FAILED: {ex.GetType().Name}"
                        : $"ERR_NORMALIZATION_FAILED: {ex.GetType().Name}"
                };
            }

            int outChars = normPage.NormalizedText?.Length ?? 0;
            totalOutputChars += outChars;

            // Diagnostic Telemetry: strictly NO document text, excerpts, or user secrets logged
            pageDiagnostics.Add(new PageNormalizationTelemetry
            {
                PageNumber = normPage.PageNumber,
                Format = rawResult.Format == DetectedDocumentFormat.PdfMixed
                    ? (normPage.ExtractedViaOcr ? DetectedDocumentFormat.PdfScanned : DetectedDocumentFormat.PdfDigital)
                    : rawResult.Format,
                Status = pageStatus,
                StatusCode = pageStatusCode,
                Elapsed = pageSw.Elapsed,
                InputCharacterCount = inChars,
                OutputCharacterCount = outChars,
                FallbackOccurred = pageStatus == NormalizationStatus.FallbackToRaw || pageStatus == NormalizationStatus.Failed,
                ExtractedViaOcr = normPage.ExtractedViaOcr,
                DiagnosticCode = pageStatusCode,
                DiagnosticWarning = normPage.DiagnosticWarning
            });

            normalizedPages.Add(normPage);
        }

        normSw.Stop();

        NormalizationStatus overallNormStatus;
        if (failedPages > 0 && successfulPages == 0)
        {
            overallNormStatus = NormalizationStatus.Failed;
        }
        else if (fallbackPages > 0 || failedPages > 0)
        {
            overallNormStatus = NormalizationStatus.FallbackToRaw;
        }
        else if (pageDiagnostics.Any(p => p.Status == NormalizationStatus.SucceededWithWarnings))
        {
            overallNormStatus = NormalizationStatus.SucceededWithWarnings;
        }
        else
        {
            overallNormStatus = NormalizationStatus.Succeeded;
        }

        var normTelemetry = new DocumentNormalizationTelemetry
        {
            DocumentId = documentId,
            Format = rawResult.Format,
            OverallStatus = overallNormStatus,
            TotalDuration = normSw.Elapsed,
            TotalPagesProcessed = normalizedPages.Count,
            SuccessfulPagesCount = successfulPages,
            FailedPagesCount = failedPages,
            FallbackPagesCount = fallbackPages,
            TotalInputCharacters = totalInputChars,
            TotalOutputCharacters = totalOutputChars,
            HasOcrPages = hasOcr,
            PageDiagnostics = pageDiagnostics
        };

        progress?.Report(0.75);

        // ── Stage 5: Page Building & Bounded Passage Chunking ──
        var documentPages = new List<DocumentPage>(normalizedPages.Count);
        var chunkingWarnings = new List<string>();

        foreach (var normPage in normalizedPages)
        {
            var docPage = _pageBuilder.BuildPage(
                pageNumber: normPage.PageNumber,
                rawText: normPage.RawText, // Ground truth preserved
                semantics: normPage.PageSemantics,
                width: normPage.WidthPt,
                height: normPage.HeightPt,
                normalizedText: normPage.NormalizedText // Normalized representation
            );

            if (_passageChunker != null && !string.IsNullOrWhiteSpace(docPage.NormalizedText))
            {
                try
                {
                    var chunks = _passageChunker.ChunkPage(documentId, docPage.PageNumber, docPage.NormalizedText, options.Chunking);
                    docPage.Chunks.AddRange(chunks);
                }
                catch (OperationCanceledException)
                {
                    // Transparent cancellation propagation: do not swallow
                    throw;
                }
                catch (Exception ex)
                {
                    // Privacy-preserving failure isolation: record exception TYPE only, never ex.Message or raw text
                    chunkingWarnings.Add($"ERR_CHUNKING_FAILED: {ex.GetType().Name}");
                }
            }

            documentPages.Add(docPage);
        }

        progress?.Report(0.90);

        // ── Stage 6: ScholarDocument & ExtractionReport Assembly ──
        long streamSize = 0;
        try { if (sourceStream.CanSeek) streamSize = sourceStream.Length; } catch { }

        var document = new ScholarDocument
        {
            DocumentId = documentId,
            FileName = fileName,
            SourcePath = sourceFilePath ?? string.Empty,
            FileSizeBytes = streamSize,
            Format = MapDocumentFormat(rawResult.Format),
            CreatedAt = DateTime.UtcNow,
            PageCount = documentPages.Count,
            Pages = documentPages
        };

        totalSw.Stop();

        var combinedWarnings = new List<string>(rawResult.GlobalWarnings);
        if (chunkingWarnings.Count > 0)
        {
            combinedWarnings.AddRange(chunkingWarnings);
        }

        var report = new ExtractionReport
        {
            DocumentId = documentId,
            FileName = fileName,
            Format = rawResult.Format,
            PageSemantics = documentPages.Count > 0 ? documentPages[0].PageSemantics : PageSemanticsType.PhysicalPage,
            TotalPages = documentPages.Count,
            TotalChunks = documentPages.Sum(p => p.Chunks.Count),
            TotalCharacters = totalOutputChars,
            ElapsedTime = totalSw.Elapsed,
            IsOcrUsed = hasOcr,
            IsFullySuccessful = fallbackPages == 0 && failedPages == 0 && chunkingWarnings.Count == 0 && !rawResult.IsPartialSuccess,
            Warnings = combinedWarnings,
            ErrorCode = failedPages > 0
                ? NormalizationDiagnosticCodes.Failed
                : (fallbackPages > 0 ? NormalizationDiagnosticCodes.FallbackRaw : null),
            ErrorMessage = null,
            NormalizationTelemetry = normTelemetry
        };

        progress?.Report(1.0);

        return new ScholarExtractionResult
        {
            Document = document,
            Report = report,
            NormalizationTelemetry = normTelemetry
        };
    }

    private static DocumentFormatType MapDocumentFormat(DetectedDocumentFormat format) => format switch
    {
        DetectedDocumentFormat.PdfDigital or
        DetectedDocumentFormat.PdfScanned or
        DetectedDocumentFormat.PdfMixed => DocumentFormatType.Pdf,

        DetectedDocumentFormat.RasterImage or
        DetectedDocumentFormat.MultiPageTiff => DocumentFormatType.ImageOcr,

        _ => DocumentFormatType.Other
    };
}
