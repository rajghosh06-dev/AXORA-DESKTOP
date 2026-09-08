using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using OcrResult = Axora.Desktop.Models.OcrResult;

namespace Axora.Desktop.Services;

/// <summary>
/// Concrete on-device OCR engine implementation utilizing Windows.Media.Ocr.
/// Fully on-device, zero-cloud, and hardened with memory safety, orientation normalization,
/// alpha compositing over white, dimension clamping, and stream ownership preservation.
/// </summary>
public sealed class WindowsMediaOcrEngine : IOcrEngine
{
    private readonly IOcrCapabilityStateProvider? _capabilityProvider;
    private readonly ILogger<WindowsMediaOcrEngine>? _logger;

    /// <inheritdoc/>
    public string EngineId => "WindowsMediaOcrEngine";

    public WindowsMediaOcrEngine(
        IOcrCapabilityStateProvider? capabilityProvider = null,
        ILogger<WindowsMediaOcrEngine>? logger = null)
    {
        _capabilityProvider = capabilityProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<OcrResult> RecognizeImageAsync(
        Stream imageStream,
        string? languageTag = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        ct.ThrowIfCancellationRequested();

        long initialStreamPos = imageStream.CanSeek ? imageStream.Position : 0;

        try
        {
            var sw = Stopwatch.StartNew();
            var warnings = new List<string>();

            // 1. Resolve target Windows OCR engine
            var engine = ResolveEngine(languageTag);

            // 2. Read caller stream into an isolated buffer
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream, ct);
            if (memoryStream.Length == 0)
            {
                throw new DocumentCorruptException("Cannot perform OCR on empty image stream (0 bytes).");
            }
            byte[] imageBytes = memoryStream.ToArray();

            // 3. Decode raster format and extract EXIF orientation via SkiaSharp
            SKEncodedOrigin origin = SKEncodedOrigin.TopLeft;
            using (var codecStream = new MemoryStream(imageBytes))
            using (var codec = SKCodec.Create(codecStream))
            {
                if (codec == null)
                {
                    throw new DocumentCorruptException("Failed to decode image. Corrupted stream or unsupported raster format.");
                }
                origin = codec.EncodedOrigin;
            }

            using var rawBitmap = SKBitmap.Decode(imageBytes);
            if (rawBitmap == null)
            {
                throw new DocumentCorruptException("Failed to decode image bitmap from stream.");
            }

            // 4. Normalize EXIF orientation if needed
            SKBitmap orientedBitmap = rawBitmap;
            bool ownsOriented = false;
            try
            {
                if (origin != SKEncodedOrigin.TopLeft && origin != (SKEncodedOrigin)0)
                {
                    orientedBitmap = ExifOrientationNormalizer.NormalizeOrientation(rawBitmap, origin);
                    ownsOriented = true;
                    warnings.Add($"Applied EXIF orientation normalization: {origin}.");
                }

                // 5. Composite transparent pixels over opaque white background to avoid black-on-black OCR failure
                SKBitmap opaqueBitmap = orientedBitmap;
                bool ownsOpaque = false;
                try
                {
                    if (orientedBitmap.AlphaType != SKAlphaType.Opaque)
                    {
                        var opaqueInfo = new SKImageInfo(
                            orientedBitmap.Width,
                            orientedBitmap.Height,
                            SKColorType.Bgra8888,
                            SKAlphaType.Opaque);
                        opaqueBitmap = new SKBitmap(opaqueInfo);
                        using (var canvas = new SKCanvas(opaqueBitmap))
                        {
                            canvas.Clear(SKColors.White);
                            canvas.DrawBitmap(orientedBitmap, 0, 0);
                            canvas.Flush();
                        }
                        ownsOpaque = true;
                    }

                    // 6. Verify minimum dimensions (Windows.Media.Ocr requires width and height >= 40 px)
                    if (opaqueBitmap.Width < 40 || opaqueBitmap.Height < 40)
                    {
                        throw new ArgumentException(
                            $"Image dimensions ({opaqueBitmap.Width}x{opaqueBitmap.Height}) are below the minimum required 40x40 pixels for Windows.Media.Ocr.");
                    }

                    // 7. Clamp maximum dimensions (proportionally downscale if > OcrEngine.MaxImageDimension)
                    uint maxDimension = OcrEngine.MaxImageDimension;
                    SKBitmap finalBitmap = opaqueBitmap;
                    bool ownsFinal = false;
                    try
                    {
                        if (opaqueBitmap.Width > maxDimension || opaqueBitmap.Height > maxDimension)
                        {
                            double scale = Math.Min(
                                (double)maxDimension / opaqueBitmap.Width,
                                (double)maxDimension / opaqueBitmap.Height);
                            int targetWidth = Math.Max(40, (int)Math.Floor(opaqueBitmap.Width * scale));
                            int targetHeight = Math.Max(40, (int)Math.Floor(opaqueBitmap.Height * scale));

                            warnings.Add($"Image dimensions ({opaqueBitmap.Width}x{opaqueBitmap.Height}) exceeded maximum OCR dimension ({maxDimension}). Proportionally downscaled to ({targetWidth}x{targetHeight}).");

                            var downscaleInfo = new SKImageInfo(
                                targetWidth,
                                targetHeight,
                                opaqueBitmap.ColorType,
                                opaqueBitmap.AlphaType);
                            finalBitmap = opaqueBitmap.Resize(downscaleInfo, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)) ?? opaqueBitmap;
                            if (!ReferenceEquals(finalBitmap, opaqueBitmap))
                            {
                                ownsFinal = true;
                            }
                        }

                        // 8. Convert to WinRT SoftwareBitmap (Bgra8, Premultiplied)
                        using var encodedStream = new MemoryStream();
                        finalBitmap.Encode(encodedStream, SKEncodedImageFormat.Png, 100);
                        byte[] pngBytes = encodedStream.ToArray();

                        using var ras = new InMemoryRandomAccessStream();
                        using (var writer = new DataWriter(ras))
                        {
                            writer.WriteBytes(pngBytes);
                            await writer.StoreAsync();
                            await writer.FlushAsync();
                            writer.DetachStream();
                        }
                        ras.Seek(0);

                        var decoder = await BitmapDecoder.CreateAsync(ras);
                        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                            BitmapPixelFormat.Bgra8,
                            BitmapAlphaMode.Premultiplied);

                        SoftwareBitmap? convertedBitmap = null;
                        SoftwareBitmap targetBitmap = softwareBitmap;
                        try
                        {
                            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                                softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                            {
                                convertedBitmap = SoftwareBitmap.Convert(
                                    softwareBitmap,
                                    BitmapPixelFormat.Bgra8,
                                    BitmapAlphaMode.Premultiplied);
                                targetBitmap = convertedBitmap;
                            }

                            ct.ThrowIfCancellationRequested();

                            // 9. Execute on-device OCR
                            var ocrResult = await engine.RecognizeAsync(targetBitmap);

                            var lines = ocrResult.Lines.Select(l => l.Text);
                            string text = string.Join("\n", lines);

                            if (ocrResult.TextAngle.HasValue && Math.Abs(ocrResult.TextAngle.Value) > 0.01)
                            {
                                warnings.Add($"Detected text tilt angle: {ocrResult.TextAngle.Value:F2}°");
                            }

                            sw.Stop();
                            return new OcrResult
                            {
                                Text = text,
                                Confidence = string.IsNullOrWhiteSpace(text) ? 0.0 : 1.0,
                                LanguageTag = engine.RecognizerLanguage.LanguageTag,
                                Warnings = warnings.AsReadOnly(),
                                Elapsed = sw.Elapsed
                            };
                        }
                        finally
                        {
                            convertedBitmap?.Dispose();
                        }
                    }
                    finally
                    {
                        if (ownsFinal) finalBitmap.Dispose();
                    }
                }
                finally
                {
                    if (ownsOpaque) opaqueBitmap.Dispose();
                }
            }
            finally
            {
                if (ownsOriented) orientedBitmap.Dispose();
            }
        }
        finally
        {
            if (imageStream.CanSeek)
            {
                try
                {
                    imageStream.Position = initialStreamPos;
                }
                catch
                {
                    // Non-fatal stream position reset
                }
            }
        }
    }

    private OcrEngine ResolveEngine(string? languageTag)
    {
        if (!string.IsNullOrWhiteSpace(languageTag))
        {
            bool isWellFormed = false;
            try
            {
                isWellFormed = Language.IsWellFormed(languageTag);
            }
            catch
            {
                isWellFormed = false;
            }

            if (!isWellFormed)
            {
                throw new OcrLanguageUnavailableException(languageTag, $"Language tag '{languageTag}' is not well-formed BCP-47.");
            }

            var lang = new Language(languageTag);
            if (!OcrEngine.IsLanguageSupported(lang))
            {
                throw new OcrLanguageUnavailableException(languageTag, $"Language '{languageTag}' is not supported by Windows.Media.Ocr on this device.");
            }

            var engine = OcrEngine.TryCreateFromLanguage(lang);
            if (engine == null)
            {
                throw new OcrLanguageUnavailableException(languageTag, $"Failed to instantiate Windows.Media.Ocr engine for '{languageTag}'.");
            }

            return engine;
        }

        // Try user profile languages first
        var profileEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (profileEngine != null)
        {
            return profileEngine;
        }

        // Fallback: en-US
        var en = new Language("en-US");
        if (OcrEngine.IsLanguageSupported(en))
        {
            var enEngine = OcrEngine.TryCreateFromLanguage(en);
            if (enEngine != null)
            {
                return enEngine;
            }
        }

        // Fallback: first available installed language
        var available = OcrEngine.AvailableRecognizerLanguages;
        if (available != null && available.Count > 0)
        {
            foreach (var l in available)
            {
                if (OcrEngine.IsLanguageSupported(l))
                {
                    var fallbackEngine = OcrEngine.TryCreateFromLanguage(l);
                    if (fallbackEngine != null)
                    {
                        return fallbackEngine;
                    }
                }
            }
        }

        throw new OcrUnavailableException("No supported Windows OCR language pack is available on this system.");
    }
}
