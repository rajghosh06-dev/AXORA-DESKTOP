namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Contract for the WinRT OCR text extraction service.
/// Backed by Windows.Media.Ocr.OcrEngine with installed OS language packs.
/// </summary>
public interface IOcrService
{
    /// <summary>Whether the OCR engine was successfully initialized with a language pack.</summary>
    bool IsAvailable { get; }

    /// <summary>Active language tag (e.g. "en-US").</summary>
    string ActiveLanguage { get; }

    /// <summary>
    /// Extracts text from the provided image stream using the on-device WinRT OCR engine.
    /// No data leaves the device.
    /// </summary>
    /// <param name="imageStream">Stream of a JPEG, PNG, BMP, or TIFF image.</param>
    /// <returns>Extracted text content, preserving line structure where possible.</returns>
    Task<string> ExtractTextAsync(Stream imageStream, CancellationToken ct = default);

    /// <summary>
    /// Extracts text from a file path directly.
    /// </summary>
    Task<string> ExtractTextFromFileAsync(string filePath, CancellationToken ct = default);
}
