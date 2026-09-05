using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Native Windows PDF Rasterizer Engine.
/// Uses Windows.Data.Pdf via PdfRendererPocService to render PDF pages into PNG or JPG images.
/// 100% offline, local-first, hardware-accelerated, and approved for W2 Core.
/// </summary>
public sealed class WindowsPdfRendererConversionEngine : IConversionEngine
{
    private readonly ILogger<WindowsPdfRendererConversionEngine>? _logger;

    public string EngineId => "win-pdf-renderer";

    public string DisplayName => "Windows Native PDF Rasterizer (Windows.Data.Pdf)";

    public bool IsAvailable => true;

    public string? RequiredDependencyId => null;

    public EngineResourceProfile ResourceProfile => new()
    {
        Affinity = EngineExecutionAffinity.MemoryBound,
        MaxConcurrentJobs = 3,
        EstimatedMemoryPerJobBytes = 128 * 1024 * 1024
    };

    public WindowsPdfRendererConversionEngine(ILogger<WindowsPdfRendererConversionEngine>? logger = null)
    {
        _logger = logger;
    }

    public static string NormalizeExtension(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return string.Empty;
        var normalized = ext.Trim().ToLowerInvariant();
        if (!normalized.StartsWith('.')) normalized = "." + normalized;
        if (normalized == ".jpeg") return ".jpg";
        return normalized;
    }

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        var src = NormalizeExtension(sourceExtension);
        var tgt = NormalizeExtension(targetExtension);
        return src == ".pdf" && (tgt == ".png" || tgt == ".jpg");
    }

    public async Task<ConversionResult> ConvertAsync(
        ConversionJob job,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        var sw = Stopwatch.StartNew();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, job.CancellationToken);
        var token = linkedCts.Token;

        if (token.IsCancellationRequested)
        {
            return ConversionResult.Cancelled(sw.Elapsed);
        }

        // Validate source file
        if (string.IsNullOrWhiteSpace(job.SourceFilePath) || !File.Exists(job.SourceFilePath))
        {
            return ConversionResult.Failure(
                "ERR_INPUT_NOT_FOUND",
                $"Source PDF file '{job.SourceFilePath}' was not found.",
                job.SourceFilePath,
                sw.Elapsed);
        }

        var fileInfo = new FileInfo(job.SourceFilePath);
        if (fileInfo.Length == 0)
        {
            return ConversionResult.Failure(
                "ERR_INPUT_EMPTY",
                $"Source PDF file '{Path.GetFileName(job.SourceFilePath)}' is empty (0 bytes).",
                job.SourceFilePath,
                sw.Elapsed);
        }

        job.SourceFileSizeBytes = fileInfo.Length;

        var srcExt = NormalizeExtension(!string.IsNullOrWhiteSpace(job.SourceExtension) ? job.SourceExtension : Path.GetExtension(job.SourceFilePath));
        var tgtExt = NormalizeExtension(job.TargetExtension);

        if (!CanConvert(srcExt, tgtExt))
        {
            return ConversionResult.Failure(
                "ERR_FORMAT_NOT_SUPPORTED",
                $"Format conversion from '{srcExt}' to '{tgtExt}' is not supported by {DisplayName}.",
                $"Source: {srcExt}, Target: {tgtExt}",
                sw.Elapsed);
        }

        var outputPath = job.OutputFilePath;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return ConversionResult.Failure(
                "ERR_OUTPUT_PATH_INVALID",
                "Destination output file path was not provided.",
                null,
                sw.Elapsed);
        }

        var outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outDir) && !Directory.Exists(outDir))
        {
            try
            {
                Directory.CreateDirectory(outDir);
            }
            catch (Exception ex)
            {
                return ConversionResult.Failure(
                    "ERR_OUTPUT_WRITE_FAILED",
                    $"Failed creating output directory: {ex.Message}",
                    ex.ToString(),
                    sw.Elapsed);
            }
        }

        progress?.Report(10.0);
        job.ProgressPercentage = 10.0;

        try
        {
            token.ThrowIfCancellationRequested();

            using var handle = await PdfRendererPocService.OpenDocumentAsync(job.SourceFilePath, null, token);
            if (handle.PageCount == 0)
            {
                return ConversionResult.Failure(
                    "ERR_PDF_EMPTY_PAGES",
                    $"The PDF document '{Path.GetFileName(job.SourceFilePath)}' contains 0 pages.",
                    job.SourceFilePath,
                    sw.Elapsed);
            }

            progress?.Report(40.0);
            job.ProgressPercentage = 40.0;
            token.ThrowIfCancellationRequested();

            bool isJpeg = tgtExt == ".jpg";
            double dpi = job.Profile.TargetDpi > 0 ? job.Profile.TargetDpi : 150.0;
            uint? maxDim = job.Profile.MaxDimension > 0 ? (uint)job.Profile.MaxDimension : null;

            var renderResult = await PdfRendererPocService.RenderPageAsync(
                handle,
                pageIndex: 0,
                dpi: dpi,
                destinationWidth: maxDim,
                destinationHeight: null,
                isJpeg: isJpeg,
                ct: token);

            if (!renderResult.Success || renderResult.ImageBytes == null || renderResult.ImageBytes.Length == 0)
            {
                return ConversionResult.Failure(
                    renderResult.ErrorCode ?? "ERR_RENDER_FAILED",
                    renderResult.ErrorMessage ?? "Failed to rasterize PDF page.",
                    job.SourceFilePath,
                    sw.Elapsed);
            }

            progress?.Report(80.0);
            job.ProgressPercentage = 80.0;
            token.ThrowIfCancellationRequested();

            await File.WriteAllBytesAsync(outputPath, renderResult.ImageBytes, token);

            progress?.Report(100.0);
            job.ProgressPercentage = 100.0;
            job.OutputSizeBytes = renderResult.ImageBytes.Length;
            job.ElapsedTime = sw.Elapsed;

            return ConversionResult.Success(outputPath, renderResult.ImageBytes.Length, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ConversionResult.Cancelled(sw.Elapsed);
        }
        catch (InvalidDataException idEx)
        {
            return ConversionResult.Failure(
                "ERR_PDF_CORRUPT",
                $"Corrupted PDF document: {idEx.Message}",
                idEx.ToString(),
                sw.Elapsed);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed rendering PDF {Source} to {Target}", job.SourceFilePath, outputPath);
            return ConversionResult.Failure(
                "ERR_CONVERSION_FAILED",
                $"Failed rendering PDF: {ex.Message}",
                ex.ToString(),
                sw.Elapsed);
        }
    }
}
