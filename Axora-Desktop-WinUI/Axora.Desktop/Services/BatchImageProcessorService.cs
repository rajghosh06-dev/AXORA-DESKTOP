using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Dual-Engine Asynchronous Batch Image Processor.
/// Supports both native Windows Hardware WIC acceleration and system ImageMagick Q16-HDRI engine.
///
/// Edge-Case Hardening:
///   - 0-byte files: guarded with pre-flight FileInfo.Length check before WIC decoder.
///   - Locked/hidden files: Directory.EnumerateFiles is wrapped with per-entry UnauthorizedAccessException catch.
///   - Unicode/UNC paths: all path operations use Path.Combine and GetFullPath for normalization.
///   - ImageMagick cancellation: proc.Kill(entireProcessTree) called on OperationCanceledException.
///   - Corrupt image headers: BitmapDecoder COM exception is caught and surfaced as job failure.
/// </summary>
public sealed class BatchImageProcessorService : IBatchImageProcessorService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".webp", ".heic", ".raw", ".gif", ".ico", ".svg"
    };

    private readonly ILogger<BatchImageProcessorService> _logger;

    public BatchImageProcessorService(ILogger<BatchImageProcessorService> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> ScanFolderForImagesAsync(
        string folderPath,
        bool includeSubfolders = true,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(folderPath)) return Array.Empty<string>();

            // FIX W-9: EnumerateFiles on recursive paths can throw UnauthorizedAccessException
            // for system/hidden directories. Collect valid files defensively.
            var files = new List<string>();
            var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            try
            {
                // Normalize path to handle Unicode and UNC paths correctly
                var normalizedPath = Path.GetFullPath(folderPath);

                var enumerated = Directory.EnumerateFiles(normalizedPath, "*.*", searchOption);
                foreach (var f in enumerated)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        if (SupportedExtensions.Contains(Path.GetExtension(f)))
                            files.Add(f);
                    }
                    catch (UnauthorizedAccessException) { /* Skip files we cannot read */ }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied scanning folder {Path} — partial results returned.", folderPath);
            }
            catch (PathTooLongException ex)
            {
                _logger.LogWarning(ex, "Path too long while scanning {Path}.", folderPath);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "I/O error scanning folder {Path}.", folderPath);
            }

            _logger.LogInformation("Scanned folder {Path}: found {Count} image files", folderPath, files.Count);
            return (IReadOnlyList<string>)files;
        }, ct);
    }

    public async Task ProcessBatchAsync(
        IReadOnlyList<BatchImageJob> jobs,
        BatchImageOptions options,
        IProgress<double>? progress = null,
        Action<BatchImageJob>? onItemProcessed = null,
        CancellationToken ct = default)
    {
        if (jobs.Count == 0) return;

        Directory.CreateDirectory(options.OutputDirectory);

        var channel = Channel.CreateBounded<BatchImageJob>(new BoundedChannelOptions(Environment.ProcessorCount * 2)
        {
            SingleWriter = true,
            SingleReader = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        int totalCount = jobs.Count;
        int completedCount = 0;

        var producerTask = Task.Run(async () =>
        {
            foreach (var job in jobs)
            {
                if (ct.IsCancellationRequested) break;
                await channel.Writer.WriteAsync(job, ct);
            }
            channel.Writer.Complete();
        }, ct);

        int workerCount = Math.Max(1, Math.Min(Environment.ProcessorCount, 16));
        var workerTasks = new List<Task>();

        for (int i = 0; i < workerCount; i++)
        {
            workerTasks.Add(Task.Run(async () =>
            {
                while (await channel.Reader.WaitToReadAsync(ct))
                {
                    while (channel.Reader.TryRead(out var job))
                    {
                        ct.ThrowIfCancellationRequested();
                        job.Status = BatchJobStatus.Processing;

                        try
                        {
                            if (options.Engine == ImageProcessingEngine.ImageMagickStudio)
                            {
                                await ProcessWithImageMagickAsync(job, options, ct);
                            }
                            else
                            {
                                await ProcessWithWicAsync(job, options, ct);
                            }
                            job.Status = BatchJobStatus.Completed;
                        }
                        catch (OperationCanceledException)
                        {
                            job.Status = BatchJobStatus.Failed;
                            job.ErrorMessage = "Cancelled";
                            throw; // Re-throw to exit consumer loop cleanly
                        }
                        catch (Exception ex)
                        {
                            job.Status = BatchJobStatus.Failed;
                            job.ErrorMessage = ex.Message;
                            _logger.LogWarning(ex, "Failed to process image {File}", job.SourceFilePath);
                        }
                        finally
                        {
                            int current = Interlocked.Increment(ref completedCount);
                            progress?.Report((double)current / totalCount);
                            onItemProcessed?.Invoke(job);
                        }
                    }
                }
            }, ct));
        }

        await Task.WhenAll(producerTask, Task.WhenAll(workerTasks));
    }

    private static async Task ProcessWithImageMagickAsync(BatchImageJob job, BatchImageOptions options, CancellationToken ct)
    {
        if (!File.Exists(job.SourceFilePath))
            throw new FileNotFoundException("Source image file not found", job.SourceFilePath);

        var fileInfo = new FileInfo(job.SourceFilePath);

        // FIX C-3: Validate file has content before invoking ImageMagick
        if (fileInfo.Length == 0)
            throw new InvalidDataException($"Source image '{Path.GetFileName(job.SourceFilePath)}' is empty (0 bytes).");

        job.OriginalSizeBytes = fileInfo.Length;

        var ext = options.TargetFormat switch
        {
            ImageTargetFormat.Png  => ".png",
            ImageTargetFormat.Jpeg => ".jpg",
            ImageTargetFormat.Bmp  => ".bmp",
            ImageTargetFormat.Tiff => ".tiff",
            _                      => ".png"
        };

        var baseName = Path.GetFileNameWithoutExtension(job.SourceFilePath);
        var outPath = Path.Combine(options.OutputDirectory, $"{baseName}_optimized{ext}");
        job.OutputFilePath = outPath;

        var args = new List<string> { $"\"{job.SourceFilePath}\"" };

        if (options.StripExifMetadata)
            args.Add("-strip");

        switch (options.ResizeMode)
        {
            case ResizeMode.Percentage:
                args.Add($"-resize {options.ScalePercentage:F0}%");
                break;
            case ResizeMode.FixedWidth:
                args.Add($"-resize {options.TargetWidth}x");
                break;
            case ResizeMode.FixedHeight:
                args.Add($"-resize x{options.TargetHeight}");
                break;
            case ResizeMode.FitBounds:
                args.Add($"-resize {options.TargetWidth}x{options.TargetHeight}>");
                break;
        }

        // Target Size vs Quality handling
        if (options.CompressionMode == CompressionSizeMode.TargetFileSize && options.TargetSizeValue > 0)
        {
            var unit = options.TargetSizeUnit.ToUpperInvariant();
            if (unit == "GB")
            {
                // Convert GB to MB for ImageMagick extent syntax
                unit = "MB";
                double mbValue = options.TargetSizeValue * 1024.0;
                args.Add($"-define jpeg:extent={mbValue:F0}{unit}");
            }
            else
            {
                args.Add($"-define jpeg:extent={options.TargetSizeValue:F0}{unit}");
            }
            args.Add("-quality 92");
        }
        else
        {
            args.Add($"-quality {options.QualityLevel}");
        }

        if (!string.IsNullOrWhiteSpace(options.WatermarkText))
        {
            args.Add($"-gravity southeast -pointsize 22 -fill \"white\" -annotate +20+20 \"{options.WatermarkText}\"");
        }

        args.Add("-auto-orient");
        args.Add($"\"{outPath}\"");

        var psi = new ProcessStartInfo
        {
            FileName = "magick",
            Arguments = string.Join(" ", args),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to launch ImageMagick process.");

        // FIX W-7: Kill ImageMagick child process tree when cancellation is requested
        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* Best-effort */ }
            throw;
        }

        if (proc.ExitCode != 0)
        {
            string err = await proc.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"ImageMagick exited with code {proc.ExitCode}: {err}");
        }

        if (File.Exists(outPath))
            job.OutputSizeBytes = new FileInfo(outPath).Length;
    }

    private static async Task ProcessWithWicAsync(BatchImageJob job, BatchImageOptions options, CancellationToken ct)
    {
        if (!File.Exists(job.SourceFilePath))
            throw new FileNotFoundException("Source image file not found", job.SourceFilePath);

        var fileInfo = new FileInfo(job.SourceFilePath);

        // FIX C-3: Guard against 0-byte files — BitmapDecoder.CreateAsync throws COM E_FAIL on empty files
        if (fileInfo.Length == 0)
            throw new InvalidDataException($"Source image '{Path.GetFileName(job.SourceFilePath)}' is empty (0 bytes).");

        job.OriginalSizeBytes = fileInfo.Length;

        var ext = options.TargetFormat switch
        {
            ImageTargetFormat.Png  => ".png",
            ImageTargetFormat.Jpeg => ".jpg",
            ImageTargetFormat.Bmp  => ".bmp",
            ImageTargetFormat.Tiff => ".tiff",
            _                      => ".png"
        };

        var baseName = Path.GetFileNameWithoutExtension(job.SourceFilePath);
        var outPath = Path.Combine(options.OutputDirectory, $"{baseName}_converted{ext}");
        job.OutputFilePath = outPath;

        using var srcStream = File.OpenRead(job.SourceFilePath);
        using var raStream = srcStream.AsRandomAccessStream();

        BitmapDecoder decoder;
        try
        {
            decoder = await BitmapDecoder.CreateAsync(raStream);
        }
        catch (Exception ex) when (ex.HResult == unchecked((int)0x88982F50) // WINCODEC_ERR_COMPONENTNOTFOUND
                                || ex.HResult == unchecked((int)0x80004005) // E_FAIL — corrupt/empty
                                || ex.HResult == unchecked((int)0x88982F61)) // WINCODEC_ERR_BADHEADER
        {
            throw new InvalidDataException(
                $"Cannot decode '{Path.GetFileName(job.SourceFilePath)}' — unsupported format or corrupted header.", ex);
        }

        var transform = new BitmapTransform();
        uint origW = decoder.PixelWidth;
        uint origH = decoder.PixelHeight;

        switch (options.ResizeMode)
        {
            case ResizeMode.Percentage:
                double factor = Math.Clamp(options.ScalePercentage / 100.0, 0.05, 10.0);
                transform.ScaledWidth  = (uint)Math.Max(1, origW * factor);
                transform.ScaledHeight = (uint)Math.Max(1, origH * factor);
                transform.InterpolationMode = BitmapInterpolationMode.Fant;
                break;

            case ResizeMode.FixedWidth:
                uint targetW = Math.Max(1, options.TargetWidth);
                double ratioW = (double)targetW / origW;
                transform.ScaledWidth  = targetW;
                transform.ScaledHeight = (uint)Math.Max(1, origH * ratioW);
                transform.InterpolationMode = BitmapInterpolationMode.Fant;
                break;

            case ResizeMode.FixedHeight:
                uint targetH = Math.Max(1, options.TargetHeight);
                double ratioH = (double)targetH / origH;
                transform.ScaledHeight = targetH;
                transform.ScaledWidth  = (uint)Math.Max(1, origW * ratioH);
                transform.InterpolationMode = BitmapInterpolationMode.Fant;
                break;

            case ResizeMode.FitBounds:
                double scaleFit = Math.Min((double)options.TargetWidth / origW, (double)options.TargetHeight / origH);
                transform.ScaledWidth  = (uint)Math.Max(1, origW * scaleFit);
                transform.ScaledHeight = (uint)Math.Max(1, origH * scaleFit);
                transform.InterpolationMode = BitmapInterpolationMode.Fant;
                break;
        }

        var encoderId = options.TargetFormat switch
        {
            ImageTargetFormat.Jpeg => BitmapEncoder.JpegEncoderId,
            ImageTargetFormat.Png  => BitmapEncoder.PngEncoderId,
            ImageTargetFormat.Bmp  => BitmapEncoder.BmpEncoderId,
            ImageTargetFormat.Tiff => BitmapEncoder.TiffEncoderId,
            _                      => BitmapEncoder.PngEncoderId
        };

        var colorMode = options.StripExifMetadata
            ? ColorManagementMode.DoNotColorManage
            : ColorManagementMode.ColorManageToSRgb;

        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            colorMode);

        var pixels = pixelData.DetachPixelData();
        uint finalWidth  = transform.ScaledWidth  != 0 ? transform.ScaledWidth  : origW;
        uint finalHeight = transform.ScaledHeight != 0 ? transform.ScaledHeight : origH;

        using var outStream = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        using var outRaStream = outStream.AsRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(encoderId, outRaStream);

        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            finalWidth,
            finalHeight,
            decoder.DpiX,
            decoder.DpiY,
            pixels);

        await encoder.FlushAsync();
        job.OutputSizeBytes = new FileInfo(outPath).Length;
    }
}
