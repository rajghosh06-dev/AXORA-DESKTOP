using Microsoft.Extensions.Logging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// WinRT OCR service using <see cref="OcrEngine"/> with installed OS language packs.
/// All processing is fully on-device — zero cloud or network calls.
/// </summary>
public sealed class WinRtOcrService : IOcrService
{
    private readonly ILogger<WinRtOcrService> _logger;
    private readonly OcrEngine? _engine;

    public bool IsAvailable => _engine is not null;
    public string ActiveLanguage { get; }

    public WinRtOcrService(ILogger<WinRtOcrService> logger)
    {
        _logger = logger;

        // Try to initialise with the user's profile language first
        _engine = OcrEngine.TryCreateFromUserProfileLanguages();

        if (_engine is not null)
        {
            ActiveLanguage = _engine.RecognizerLanguage.LanguageTag;
            _logger.LogInformation("WinRT OCR engine initialised. Language: {Lang}", ActiveLanguage);
        }
        else
        {
            // Fallback: English
            var en = new Language("en-US");
            if (OcrEngine.IsLanguageSupported(en))
            {
                _engine = OcrEngine.TryCreateFromLanguage(en);
                ActiveLanguage = "en-US";
                _logger.LogWarning("Profile language not supported by OCR. Falling back to en-US.");
            }
            else
            {
                ActiveLanguage = "unavailable";
                _logger.LogError("No supported OCR language found on this device.");
            }
        }
    }

    /// <inheritdoc/>
    public async Task<string> ExtractTextAsync(Stream imageStream, CancellationToken ct = default)
    {
        if (_engine is null)
            throw new InvalidOperationException("OCR engine is unavailable. Install a Windows language pack.");

        return await Task.Run(async () =>
        {
            ct.ThrowIfCancellationRequested();

            // Convert Stream → IRandomAccessStream for WinRT APIs
            using var raStream = imageStream.AsRandomAccessStream();
            raStream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(raStream);
            // FIX-2: Dispose SoftwareBitmap after OCR to prevent unmanaged WinRT memory leak on repeated scans
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            // WinRT OCR requires Bgra8 format — convert if needed
            SoftwareBitmap? convertedBitmap = null;
            SoftwareBitmap targetBitmap = softwareBitmap;
            try
            {
                if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
                {
                    convertedBitmap = SoftwareBitmap.Convert(softwareBitmap,
                        BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                    targetBitmap = convertedBitmap;
                }

                ct.ThrowIfCancellationRequested();

                var ocrResult = await _engine.RecognizeAsync(targetBitmap);

                // Reconstruct text preserving line breaks
                var lines = ocrResult.Lines.Select(l => l.Text);
                return string.Join(Environment.NewLine, lines);
            }
            finally
            {
                convertedBitmap?.Dispose();
            }
        }, ct);
    }

    /// <inheritdoc/>
    public async Task<string> ExtractTextFromFileAsync(string filePath, CancellationToken ct = default)
    {
        using var fileStream = File.OpenRead(filePath);
        return await ExtractTextAsync(fileStream, ct);
    }
}
