using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.Advanced;
using PdfSharpCore.Pdf.IO;

namespace Axora.Desktop.Services;

/// <summary>
/// Deterministic extractor engine for PDF documents with hybrid on-device OCR dispatch.
/// Implements IPdfDocumentExtractorEngine with exact physical page enumeration,
/// CMap/font resolution, evidence-based format classification (Digital, Scanned, Mixed),
/// page-aware OCR routing, strict source immutability, and security bounds enforcement.
/// </summary>
public sealed class PdfDocumentExtractorEngine : IPdfDocumentExtractorEngine
{
    private const int TextBearingThresholdChars = 30;
    private const int TextSparseThresholdChars = 10;

    private readonly IPdfPageRasterizer _rasterizer;
    private readonly IOcrEngine _ocrEngine;
    private readonly IOcrCapabilityStateProvider _capabilityProvider;
    private readonly ILogger<PdfDocumentExtractorEngine>? _logger;

    public string EngineIdentifier => "PdfDocumentExtractorEngine";

    public PdfDocumentExtractorEngine(
        IPdfPageRasterizer? rasterizer = null,
        IOcrEngine? ocrEngine = null,
        IOcrCapabilityStateProvider? capabilityProvider = null,
        ILogger<PdfDocumentExtractorEngine>? logger = null)
    {
        _capabilityProvider = capabilityProvider ?? new WindowsOcrCapabilityStateProvider();
        _ocrEngine = ocrEngine ?? new WindowsMediaOcrEngine(_capabilityProvider);
        _rasterizer = rasterizer ?? new WindowsPdfPageRasterizer();
        _logger = logger;
    }

    public bool CanExtract(DetectedDocumentFormat format)
    {
        return format is DetectedDocumentFormat.PdfDigital
            or DetectedDocumentFormat.PdfScanned
            or DetectedDocumentFormat.PdfMixed;
    }

    public Task<PdfExtractionCapabilities> GetEngineCapabilitiesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new PdfExtractionCapabilities
        {
            EngineName = "PdfSharpCore-TextExtractor",
            EngineVersion = "1.3.65",
            SupportsDirectRasterization = false, // Rasterization delegated to IPdfPageRasterizer
            SupportsCustomFontCmaps = true,
            SupportsRightToLeftScripts = false
        });
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

        // 1. Check stream length security bound if seekable
        if (documentStream.CanSeek && documentStream.Length > options.Security.MaxFileSizeBytes)
        {
            throw new FileSizeLimitExceededException(documentStream.Length, options.Security.MaxFileSizeBytes);
        }

        // 2. Read bytes safely without mutating caller stream
        long initialStreamPos = documentStream.CanSeek ? documentStream.Position : 0;
        byte[] pdfBytes;
        try
        {
            if (documentStream is MemoryStream ms && ms.TryGetBuffer(out var segment))
            {
                pdfBytes = segment.ToArray();
            }
            else
            {
                using var memStream = new MemoryStream();
                await documentStream.CopyToAsync(memStream, ct);
                pdfBytes = memStream.ToArray();
            }
        }
        finally
        {
            if (documentStream.CanSeek)
            {
                try { documentStream.Position = initialStreamPos; } catch { }
            }
        }

        ct.ThrowIfCancellationRequested();

        // Check byte length against security limit
        if (pdfBytes.Length > options.Security.MaxFileSizeBytes)
        {
            throw new FileSizeLimitExceededException(pdfBytes.Length, options.Security.MaxFileSizeBytes);
        }

        progress?.Report(0.15);

        // 3. Open PDF document with PdfSharpCore in memory
        PdfDocument pdfDoc;
        try
        {
            var readStream = new MemoryStream(pdfBytes, writable: false);
            pdfDoc = PdfReader.Open(readStream, PdfDocumentOpenMode.ReadOnly);
        }
        catch (PdfReaderException ex)
        {
            string msg = ex.Message ?? string.Empty;
            if (msg.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("encrypt", StringComparison.OrdinalIgnoreCase))
            {
                throw new DocumentPasswordProtectedException($"PDF document is password-protected or encrypted: {msg}");
            }
            throw new DocumentCorruptException($"Corrupted or unreadable PDF structure: {msg}", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new DocumentCorruptException($"Malformed PDF document: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not ScholarExtractionException && ex is not OperationCanceledException)
        {
            throw new DocumentCorruptException($"Failed parsing PDF stream: {ex.Message}", ex);
        }

        using (pdfDoc)
        {
            int totalPageCount = pdfDoc.PageCount;

            // 4. Validate page limits
            if (options.MaxPages.HasValue && totalPageCount > options.MaxPages.Value)
            {
                throw new PageLimitExceededException(totalPageCount, options.MaxPages.Value);
            }
            if (totalPageCount > options.Security.MaxPagesToExtract)
            {
                throw new PageLimitExceededException(totalPageCount, options.Security.MaxPagesToExtract);
            }

            int pagesToProcess = options.MaxPages.HasValue
                ? Math.Min(totalPageCount, options.MaxPages.Value)
                : totalPageCount;

            string docTitle = !string.IsNullOrWhiteSpace(pdfDoc.Info.Title)
                ? pdfDoc.Info.Title
                : "PDF Document";
            string author = pdfDoc.Info.Author ?? string.Empty;

            var pages = new List<ExtractedPageRaw>(pagesToProcess);
            var globalWarnings = new List<string>();

            int textBearingPages = 0;
            int imageDominantPages = 0;
            int ocrSuccessPages = 0;
            int totalExtractedChars = 0;

            for (int i = 0; i < pagesToProcess; i++)
            {
                ct.ThrowIfCancellationRequested();

                var pdfPage = pdfDoc.Pages[i];
                double widthPt = pdfPage.Width.Point;
                double heightPt = pdfPage.Height.Point;

                string digitalText = string.Empty;
                string? pageWarning = null;

                try
                {
                    digitalText = PdfTextExtractor.ExtractTextFromPage(pdfPage);
                }
                catch (Exception pageEx)
                {
                    pageWarning = $"Page {i + 1} content stream warning: {pageEx.Message}";
                    globalWarnings.Add(pageWarning);
                }

                digitalText ??= string.Empty;
                string cleanText = digitalText.Trim();
                int cleanLen = cleanText.Length;
                bool hasImages = HasImageXObjects(pdfPage);

                if (cleanLen >= TextBearingThresholdChars)
                {
                    textBearingPages++;
                }
                else if (cleanLen < TextSparseThresholdChars && hasImages)
                {
                    imageDominantPages++;
                }

                // Determine whether this page requires OCR
                bool isDigitalPage = cleanLen >= TextBearingThresholdChars;
                bool requiresOcr = options.ForceOcr ||
                                   (cleanLen < TextSparseThresholdChars && (hasImages || cleanLen == 0)) ||
                                   (cleanLen < TextBearingThresholdChars && hasImages);

                string pageRawText = digitalText;
                bool extractedViaOcr = false;
                double confidence = 1.0;

                if (requiresOcr)
                {
                    ct.ThrowIfCancellationRequested();

                    // Capability & Language verification
                    bool capabilityAvailable = true;
                    string? capabilityError = null;

                    if (_capabilityProvider.State == OcrCapabilityState.OcrUnavailable)
                    {
                        capabilityAvailable = false;
                        capabilityError = "ERR_OCR_UNAVAILABLE: On-device OCR capability is not available on this system.";
                    }
                    else if (_capabilityProvider.State == OcrCapabilityState.OcrFailed)
                    {
                        capabilityAvailable = false;
                        capabilityError = "ERR_OCR_INTERNAL_FAILURE: On-device OCR engine is in a failed state.";
                    }
                    else if (!string.IsNullOrWhiteSpace(options.OcrLanguage))
                    {
                        if (_capabilityProvider is WindowsOcrCapabilityStateProvider winProvider)
                        {
                            var langState = winProvider.CheckLanguageState(options.OcrLanguage);
                            if (langState == OcrCapabilityState.OcrLanguageUnavailable)
                            {
                                capabilityAvailable = false;
                                capabilityError = $"ERR_OCR_LANGUAGE_UNAVAILABLE: Requested OCR language '{options.OcrLanguage}' is not installed.";
                            }
                            else if (langState == OcrCapabilityState.OcrUnavailable)
                            {
                                capabilityAvailable = false;
                                capabilityError = $"ERR_OCR_UNAVAILABLE: OCR capability is unavailable for requested language '{options.OcrLanguage}'.";
                            }
                        }
                        else if (_capabilityProvider.InstalledLanguages != null &&
                                 _capabilityProvider.InstalledLanguages.Count > 0 &&
                                 !_capabilityProvider.InstalledLanguages.Any(l => string.Equals(l, options.OcrLanguage, StringComparison.OrdinalIgnoreCase)))
                        {
                            capabilityAvailable = false;
                            capabilityError = $"ERR_OCR_LANGUAGE_UNAVAILABLE: Requested OCR language '{options.OcrLanguage}' is not installed.";
                        }
                    }

                    if (!capabilityAvailable)
                    {
                        pageWarning = pageWarning != null ? $"{pageWarning}; {capabilityError}" : capabilityError;
                        globalWarnings.Add($"Page {i + 1}: {capabilityError}");
                        if (string.IsNullOrWhiteSpace(digitalText))
                        {
                            confidence = 0.0;
                        }
                    }
                    else if (!_rasterizer.CanRasterize)
                    {
                        string rasterErr = "ERR_RASTERIZATION_UNAVAILABLE: PDF page rasterizer is not available on this platform.";
                        pageWarning = pageWarning != null ? $"{pageWarning}; {rasterErr}" : rasterErr;
                        globalWarnings.Add($"Page {i + 1}: {rasterErr}");
                        if (string.IsNullOrWhiteSpace(digitalText))
                        {
                            confidence = 0.0;
                        }
                    }
                    else
                    {
                        // Sequential single-page raster lifetime
                        try
                        {
                            using var pageRasterStream = new MemoryStream(pdfBytes, writable: false);
                            using var pagePngStream = await _rasterizer.RasterizePageToPngStreamAsync(pageRasterStream, i, 300.0, ct);

                            ct.ThrowIfCancellationRequested();

                            var ocrResult = await _ocrEngine.RecognizeImageAsync(pagePngStream, options.OcrLanguage, ct);

                            if (!string.IsNullOrWhiteSpace(ocrResult.Text))
                            {
                                extractedViaOcr = true;
                                ocrSuccessPages++;
                                confidence = ocrResult.Confidence;

                                // DIGITAL PRESERVATION: Never destroy or discard valid digital text
                                if (!string.IsNullOrWhiteSpace(digitalText))
                                {
                                    pageRawText = $"{digitalText}\n\n[OCR]\n{ocrResult.Text}";
                                    pageWarning = isDigitalPage
                                        ? "OCR forced by user options on text-bearing digital page."
                                        : "Hybrid extraction: Digital text supplemented by on-device OCR.";
                                }
                                else
                                {
                                    pageRawText = ocrResult.Text;
                                }

                                if (ocrResult.Warnings != null && ocrResult.Warnings.Count > 0)
                                {
                                    string ocrWarnStr = string.Join("; ", ocrResult.Warnings);
                                    pageWarning = pageWarning != null ? $"{pageWarning}; {ocrWarnStr}" : ocrWarnStr;
                                }
                            }
                            else
                            {
                                if (string.IsNullOrWhiteSpace(digitalText))
                                {
                                    confidence = 0.0;
                                }
                            }
                        }
                        catch (OcrUnavailableException ocrEx)
                        {
                            string ocrMsg = $"ERR_OCR_UNAVAILABLE: {ocrEx.Message}";
                            pageWarning = pageWarning != null ? $"{pageWarning}; {ocrMsg}" : ocrMsg;
                            globalWarnings.Add($"Page {i + 1}: {ocrMsg}");
                            if (string.IsNullOrWhiteSpace(digitalText)) confidence = 0.0;
                        }
                        catch (OcrLanguageUnavailableException langEx)
                        {
                            string langMsg = $"ERR_OCR_LANGUAGE_UNAVAILABLE: {langEx.Message}";
                            pageWarning = pageWarning != null ? $"{pageWarning}; {langMsg}" : langMsg;
                            globalWarnings.Add($"Page {i + 1}: {langMsg}");
                            if (string.IsNullOrWhiteSpace(digitalText)) confidence = 0.0;
                        }
                        catch (OcrExecutionException execEx)
                        {
                            string execMsg = $"ERR_OCR_INTERNAL_FAILURE: {execEx.Message}";
                            pageWarning = pageWarning != null ? $"{pageWarning}; {execMsg}" : execMsg;
                            globalWarnings.Add($"Page {i + 1}: {execMsg}");
                            if (string.IsNullOrWhiteSpace(digitalText)) confidence = 0.0;
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            string genMsg = $"ERR_OCR_FAILED: Page {i + 1} OCR execution failed: {ex.Message}";
                            pageWarning = pageWarning != null ? $"{pageWarning}; {genMsg}" : genMsg;
                            globalWarnings.Add(genMsg);
                            if (string.IsNullOrWhiteSpace(digitalText)) confidence = 0.0;
                        }
                    }
                }

                totalExtractedChars += pageRawText.Length;
                if (totalExtractedChars > options.Security.MaxXmlDocumentChars)
                {
                    throw new ExtractionSecurityLimitException(
                        "ERR_SECURITY_LIMIT_EXCEEDED",
                        "Extracted PDF character count exceeds local safety threshold.",
                        $"Extracted {totalExtractedChars} characters exceeding threshold {options.Security.MaxXmlDocumentChars}.");
                }

                pages.Add(new ExtractedPageRaw
                {
                    PageNumber = i + 1,
                    PageSemantics = PageSemanticsType.PhysicalPage,
                    WidthPt = widthPt,
                    HeightPt = heightPt,
                    RawText = pageRawText,
                    NormalizedText = null, // Strictly null (Two-Tier text invariant)
                    ExtractedViaOcr = extractedViaOcr,
                    Confidence = confidence,
                    DiagnosticWarning = pageWarning
                });

                double stepProgress = 0.15 + (0.80 * ((double)(i + 1) / Math.Max(1, pagesToProcess)));
                progress?.Report(stepProgress);
            }

            // 5. Evidence-based Document Classification
            DetectedDocumentFormat classifiedFormat;
            int digitalWithTextPages = pages.Count(p => !p.ExtractedViaOcr && !string.IsNullOrWhiteSpace(p.RawText));

            if (pagesToProcess == 0)
            {
                classifiedFormat = DetectedDocumentFormat.PdfDigital;
            }
            else if (digitalWithTextPages > 0 && ocrSuccessPages > 0)
            {
                classifiedFormat = DetectedDocumentFormat.PdfMixed;
            }
            else if (ocrSuccessPages > 0 && digitalWithTextPages == 0)
            {
                classifiedFormat = DetectedDocumentFormat.PdfScanned;
            }
            else if (textBearingPages > 0 && imageDominantPages > 0)
            {
                classifiedFormat = DetectedDocumentFormat.PdfMixed;
            }
            else if (textBearingPages == 0 && (imageDominantPages > 0 || totalExtractedChars == 0))
            {
                classifiedFormat = DetectedDocumentFormat.PdfScanned;
            }
            else
            {
                classifiedFormat = DetectedDocumentFormat.PdfDigital;
            }

            progress?.Report(1.0);
            stopwatch.Stop();

            return new RawExtractionResult
            {
                DocumentTitle = docTitle,
                Author = author,
                Format = classifiedFormat,
                Pages = pages,
                Duration = stopwatch.Elapsed,
                EngineIdentifier = EngineIdentifier,
                GlobalWarnings = globalWarnings,
                IsPartialSuccess = globalWarnings.Count > 0
            };
        }
    }

    private static bool HasImageXObjects(PdfPage page)
    {
        try
        {
            var xObjects = page.Resources?.Elements.GetDictionary("/XObject");
            if (xObjects == null) return false;

            foreach (var keyItem in xObjects.Elements.KeyNames)
            {
                string keyName = keyItem.ToString();
                var xObj = xObjects.Elements.GetDictionary(keyName);
                if (xObj == null) continue;

                string subtype = xObj.Elements.GetName("/Subtype") ?? string.Empty;
                if (subtype.Equals("/Image", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Non-fatal inspection
        }
        return false;
    }
}
