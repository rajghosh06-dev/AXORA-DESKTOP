using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Native Windows Image Conversion Engine (WIC).
/// Implements high-performance, hardware-accelerated local raster image conversions
/// using Windows.Graphics.Imaging (WIC) with seamless SkiaSharp WebP support.
///
/// Supported formats: PNG, JPG/JPEG, WebP, BMP, TIFF/TIF.
/// Fully offline, non-destructive, and thread-safe.
/// </summary>
public sealed class WicImageConversionEngine : IConversionEngine
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".bmp",
        ".tiff",
        ".tif"
    };

    private readonly ILogger<WicImageConversionEngine>? _logger;

    public string EngineId => "wic";

    public string DisplayName => "Native Windows Image Engine (WIC)";

    public bool IsAvailable => true;

    public string? RequiredDependencyId => null;

    public EngineResourceProfile ResourceProfile => EngineResourceProfile.CpuBoundDefault;

    public WicImageConversionEngine(ILogger<WicImageConversionEngine>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Normalizes file extension into lowercase with leading dot, mapping aliases (.jpeg -> .jpg, .tif -> .tiff).
    /// </summary>
    public static string NormalizeExtension(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return string.Empty;
        var normalized = ext.Trim().ToLowerInvariant();
        if (!normalized.StartsWith('.')) normalized = "." + normalized;
        if (normalized == ".jpeg") return ".jpg";
        if (normalized == ".tif") return ".tiff";
        return normalized;
    }

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        var src = NormalizeExtension(sourceExtension);
        var tgt = NormalizeExtension(targetExtension);
        return SupportedExtensions.Contains(src) && SupportedExtensions.Contains(tgt);
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

        // 1. Source existence and basic properties check
        if (string.IsNullOrWhiteSpace(job.SourceFilePath) || !File.Exists(job.SourceFilePath))
        {
            return ConversionResult.Failure(
                "ERR_INPUT_NOT_FOUND",
                $"Source image file '{job.SourceFilePath}' was not found.",
                job.SourceFilePath,
                sw.Elapsed);
        }

        FileInfo fileInfo;
        try
        {
            fileInfo = new FileInfo(job.SourceFilePath);
        }
        catch (Exception ex)
        {
            return ConversionResult.Failure(
                "ERR_INPUT_READ_FAILED",
                $"Failed inspecting source file: {ex.Message}",
                ex.ToString(),
                sw.Elapsed);
        }

        if (fileInfo.Length == 0)
        {
            return ConversionResult.Failure(
                "ERR_INPUT_EMPTY",
                $"Source image '{Path.GetFileName(job.SourceFilePath)}' is empty (0 bytes).",
                job.SourceFilePath,
                sw.Elapsed);
        }

        job.SourceFileSizeBytes = fileInfo.Length;

        // 2. Format validation
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

        // 3. Destination resolution
        var outputPath = job.OutputFilePath;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            if (!string.IsNullOrWhiteSpace(job.DestinationDirectory))
            {
                var baseName = Path.GetFileNameWithoutExtension(job.SourceFilePath);
                outputPath = Path.Combine(job.DestinationDirectory, $"{baseName}{tgtExt}");
                job.OutputFilePath = outputPath;
            }
            else
            {
                return ConversionResult.Failure(
                    "ERR_OUTPUT_PATH_INVALID",
                    "Destination output file path was not provided.",
                    null,
                    sw.Elapsed);
            }
        }

        // Safety invariant: engine MUST NOT overwrite source file
        try
        {
            if (string.Equals(Path.GetFullPath(job.SourceFilePath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
            {
                return ConversionResult.Failure(
                    "ERR_OUTPUT_SAME_AS_SOURCE",
                    "Target output path cannot be identical to the source file path.",
                    outputPath,
                    sw.Elapsed);
            }
        }
        catch (Exception ex)
        {
            return ConversionResult.Failure(
                "ERR_OUTPUT_PATH_INVALID",
                $"Invalid output path format: {ex.Message}",
                ex.ToString(),
                sw.Elapsed);
        }

        var outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outDir))
        {
            try
            {
                Directory.CreateDirectory(outDir);
            }
            catch (Exception ex)
            {
                return ConversionResult.Failure(
                    "ERR_OUTPUT_WRITE_FAILED",
                    $"Failed creating output directory '{outDir}': {ex.Message}",
                    ex.ToString(),
                    sw.Elapsed);
            }
        }

        var scratchPath = Path.Combine(
            outDir ?? string.Empty,
            $".tmp_axora_{Guid.NewGuid():N}{tgtExt}");

        progress?.Report(10.0);
        job.ProgressPercentage = 10.0;
        job.Status = ConversionJobStatus.Running;

        // 4. Test opening source stream safely
        FileStream srcStream;
        try
        {
            srcStream = new FileStream(job.SourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ConversionResult.Failure(
                "ERR_INPUT_ACCESS_DENIED",
                $"Access denied reading '{Path.GetFileName(job.SourceFilePath)}'.",
                ex.ToString(),
                sw.Elapsed);
        }
        catch (IOException ex)
        {
            return ConversionResult.Failure(
                "ERR_INPUT_READ_FAILED",
                $"Failed opening '{Path.GetFileName(job.SourceFilePath)}': {ex.Message}",
                ex.ToString(),
                sw.Elapsed);
        }

        using (srcStream)
        {
            try
            {
                token.ThrowIfCancellationRequested();

                // 5. Decode source image
                DecodedImage decoded;
                try
                {
                    decoded = await DecodeImageAsync(srcStream, job.SourceFilePath, job.Profile, token);
                }
                catch (OperationCanceledException)
                {
                    return ConversionResult.Cancelled(sw.Elapsed);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "WIC decoding failed for {Source}", job.SourceFilePath);
                    return ConversionResult.Failure(
                        "ERR_INPUT_CORRUPT",
                        $"Cannot decode '{Path.GetFileName(job.SourceFilePath)}' — corrupted header or unsupported format.",
                        ex.ToString(),
                        sw.Elapsed);
                }

                progress?.Report(50.0);
                job.ProgressPercentage = 50.0;
                token.ThrowIfCancellationRequested();

                // 6. Encode to target scratch file
                try
                {
                    await EncodeImageAsync(decoded, tgtExt, scratchPath, job.Profile, token);
                }
                catch (OperationCanceledException)
                {
                    return ConversionResult.Cancelled(sw.Elapsed);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Encoding failed for {Target}", outputPath);
                    return ConversionResult.Failure(
                        "ERR_OUTPUT_WRITE_FAILED",
                        $"Failed writing converted image to target: {ex.Message}",
                        ex.ToString(),
                        sw.Elapsed);
                }

                progress?.Report(90.0);
                job.ProgressPercentage = 90.0;
                token.ThrowIfCancellationRequested();

                // 7. Atomic rename to destination
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
                File.Move(scratchPath, outputPath);

                var outInfo = new FileInfo(outputPath);
                job.OutputSizeBytes = outInfo.Length;
                job.ProgressPercentage = 100.0;
                job.Status = ConversionJobStatus.Completed;
                job.ElapsedTime = sw.Elapsed;
                progress?.Report(100.0);

                return ConversionResult.Success(outputPath, outInfo.Length, sw.Elapsed);
            }
            catch (OperationCanceledException)
            {
                return ConversionResult.Cancelled(sw.Elapsed);
            }
            catch (OutOfMemoryException ex)
            {
                _logger?.LogError(ex, "Out of memory processing image {Source}", job.SourceFilePath);
                return ConversionResult.Failure(
                    "ERR_INSUFFICIENT_MEMORY",
                    "Insufficient memory to allocate image buffer for conversion.",
                    ex.ToString(),
                    sw.Elapsed);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error converting {Source}", job.SourceFilePath);
                return ConversionResult.Failure(
                    "ERR_CONVERSION_FAILED",
                    $"Unexpected conversion failure: {ex.Message}",
                    ex.ToString(),
                    sw.Elapsed);
            }
            finally
            {
                try
                {
                    if (File.Exists(scratchPath))
                    {
                        File.Delete(scratchPath);
                    }
                }
                catch { /* Best effort cleanup of temporary staging file */ }
            }
        }
    }

    private static async Task<DecodedImage> DecodeImageAsync(
        Stream srcStream,
        string sourceFilePath,
        ConversionProfile profile,
        CancellationToken token)
    {
        var ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();
        bool isSkiaSupported = ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp";
        if (isSkiaSupported)
        {
            // 1. Try high-performance in-memory SkiaSharp decoder with EXIF orientation normalization
            try
            {
                srcStream.Seek(0, SeekOrigin.Begin);
                using var codec = SKCodec.Create(srcStream);
                if (codec != null)
                {
                    var origin = codec.EncodedOrigin;
                    using var rawBitmap = SKBitmap.Decode(codec);
                    if (rawBitmap != null)
                    {
                        SKBitmap uprightBitmap;
                        bool ownsUpright;
                        if (origin is SKEncodedOrigin.TopLeft or (SKEncodedOrigin)0)
                        {
                            uprightBitmap = rawBitmap;
                            ownsUpright = false;
                        }
                        else
                        {
                            uprightBitmap = ExifOrientationNormalizer.NormalizeOrientation(rawBitmap, origin);
                            ownsUpright = true;
                        }

                        try
                        {
                            uint origW = (uint)uprightBitmap.Width;
                            uint origH = (uint)uprightBitmap.Height;
                            uint targetW = origW;
                            uint targetH = origH;

                            // Apply proportional MaxDimension downscaling to visually upright dimensions
                            if (profile.MaxDimension > 0 && (origW > profile.MaxDimension || origH > profile.MaxDimension))
                            {
                                double scale = Math.Min((double)profile.MaxDimension / origW, (double)profile.MaxDimension / origH);
                                targetW = Math.Max(1, (uint)Math.Round(origW * scale));
                                targetH = Math.Max(1, (uint)Math.Round(origH * scale));
                            }

                            SKBitmap toProcess = uprightBitmap;
                            SKBitmap? resizedBitmap = null;
                            if (targetW != origW || targetH != origH)
                            {
                                var info = new SKImageInfo((int)targetW, (int)targetH, uprightBitmap.ColorType, uprightBitmap.AlphaType);
                                resizedBitmap = uprightBitmap.Resize(info, SKFilterQuality.High);
                                if (resizedBitmap != null)
                                {
                                    toProcess = resizedBitmap;
                                }
                                else
                                {
                                    resizedBitmap = new SKBitmap(info);
                                    using (var canvas = new SKCanvas(resizedBitmap))
                                    {
                                        using var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
                                        canvas.DrawBitmap(uprightBitmap, new SKRect(0, 0, targetW, targetH), paint);
                                        canvas.Flush();
                                    }
                                    toProcess = resizedBitmap;
                                }
                            }

                            try
                            {
                                var bgraInfo = new SKImageInfo((int)targetW, (int)targetH, SKColorType.Bgra8888, SKAlphaType.Premul);
                                using var bgraBitmap = new SKBitmap(bgraInfo);
                                using (var canvas = new SKCanvas(bgraBitmap))
                                {
                                    canvas.DrawBitmap(toProcess, 0, 0);
                                    canvas.Flush();
                                }

                                byte[] pixels = new byte[bgraBitmap.ByteCount];
                                Marshal.Copy(bgraBitmap.GetPixels(), pixels, 0, pixels.Length);

                                double dpiX = profile.TargetDpi > 0 ? profile.TargetDpi : 96.0;
                                double dpiY = profile.TargetDpi > 0 ? profile.TargetDpi : 96.0;

                                return new DecodedImage
                                {
                                    Width = targetW,
                                    Height = targetH,
                                    DpiX = dpiX,
                                    DpiY = dpiY,
                                    Pixels = pixels
                                };
                            }
                            finally
                            {
                                resizedBitmap?.Dispose();
                            }
                        }
                        finally
                        {
                            if (ownsUpright)
                            {
                                uprightBitmap.Dispose();
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fall through to native Windows WIC decoder
            }
        }

        // 2. Native Windows WIC decoder fallback with ConfigureAwait(false)
        try
        {
            srcStream.Seek(0, SeekOrigin.Begin);
            using var raStream = srcStream.AsRandomAccessStream();
            raStream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(raStream).AsTask(token).ConfigureAwait(false);

            uint origW = decoder.OrientedPixelWidth != 0 ? decoder.OrientedPixelWidth : decoder.PixelWidth;
            uint origH = decoder.OrientedPixelHeight != 0 ? decoder.OrientedPixelHeight : decoder.PixelHeight;
            uint targetW = origW;
            uint targetH = origH;

            if (profile.MaxDimension > 0 && (origW > profile.MaxDimension || origH > profile.MaxDimension))
            {
                double scale = Math.Min((double)profile.MaxDimension / origW, (double)profile.MaxDimension / origH);
                targetW = Math.Max(1, (uint)Math.Round(origW * scale));
                targetH = Math.Max(1, (uint)Math.Round(origH * scale));
            }

            var transform = new BitmapTransform();
            if (targetW != origW || targetH != origH)
            {
                transform.ScaledWidth = targetW;
                transform.ScaledHeight = targetH;
                transform.InterpolationMode = BitmapInterpolationMode.Fant;
            }

            var colorMode = profile.MetadataPolicy == MetadataHandling.Strip
                ? ColorManagementMode.DoNotColorManage
                : ColorManagementMode.ColorManageToSRgb;

            var pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                colorMode).AsTask(token).ConfigureAwait(false);

            double dpiX = profile.TargetDpi > 0 ? profile.TargetDpi : (decoder.DpiX > 0 ? decoder.DpiX : 96.0);
            double dpiY = profile.TargetDpi > 0 ? profile.TargetDpi : (decoder.DpiY > 0 ? decoder.DpiY : 96.0);

            return new DecodedImage
            {
                Width = targetW,
                Height = targetH,
                DpiX = dpiX,
                DpiY = dpiY,
                Pixels = pixelData.DetachPixelData()
            };
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Image header could not be decoded by SkiaSharp or WIC: {sourceFilePath}", ex);
        }
    }

    private static async Task EncodeImageAsync(
        DecodedImage image,
        string tgtExt,
        string scratchPath,
        ConversionProfile profile,
        CancellationToken token)
    {
        int quality = Math.Clamp(profile.Quality, 1, 100);

        SKEncodedImageFormat? skFormat = tgtExt switch
        {
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".png"            => SKEncodedImageFormat.Png,
            ".webp"           => SKEncodedImageFormat.Webp,
            _                 => null
        };

        if (skFormat.HasValue)
        {
            var info = new SKImageInfo((int)image.Width, (int)image.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var skBitmap = new SKBitmap(info);
            Marshal.Copy(image.Pixels, 0, skBitmap.GetPixels(), image.Pixels.Length);

            using var skImage = SKImage.FromBitmap(skBitmap);
            using var skData = skImage.Encode(skFormat.Value, quality)
                ?? throw new InvalidOperationException($"SkiaSharp failed to encode {tgtExt} image data.");

            await using var outStream = new FileStream(scratchPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
            skData.SaveTo(outStream);
            await outStream.FlushAsync(token);
        }
        else
        {
            var encoderId = tgtExt switch
            {
                ".bmp"            => BitmapEncoder.BmpEncoderId,
                ".tiff" or ".tif" => BitmapEncoder.TiffEncoderId,
                _                 => BitmapEncoder.PngEncoderId
            };

            await using (var outStream = new FileStream(scratchPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
            using (var outRaStream = outStream.AsRandomAccessStream())
            {
                var encoder = await BitmapEncoder.CreateAsync(encoderId, outRaStream).AsTask(token).ConfigureAwait(false);

                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    image.Width,
                    image.Height,
                    image.DpiX,
                    image.DpiY,
                    image.Pixels);

                await encoder.FlushAsync().AsTask(token).ConfigureAwait(false);
            }
        }
    }

    private static bool IsWicFallbackApplicable(Exception ex)
    {
        return ex.HResult == unchecked((int)0x88982F50) // WINCODEC_ERR_COMPONENTNOTFOUND
            || ex.HResult == unchecked((int)0x88982F61) // WINCODEC_ERR_BADHEADER
            || ex.HResult == unchecked((int)0x88982F60) // WINCODEC_ERR_BADIMAGE
            || ex.HResult == unchecked((int)0x80004005) // E_FAIL
            || ex is NotSupportedException
            || ex is COMException;
    }

    private sealed class DecodedImage
    {
        public uint Width { get; init; }
        public uint Height { get; init; }
        public double DpiX { get; init; }
        public double DpiY { get; init; }
        public byte[] Pixels { get; init; } = Array.Empty<byte>();
    }
}
