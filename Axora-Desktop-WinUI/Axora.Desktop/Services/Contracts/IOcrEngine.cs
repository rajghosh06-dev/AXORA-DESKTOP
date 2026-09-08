using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Abstract contract for local on-device OCR recognition.
/// Decoupled from specific platform APIs (WinRT, Tesseract, ONNX).
/// </summary>
public interface IOcrEngine
{
    /// <summary>
    /// Unique identifier for this OCR engine implementation.
    /// </summary>
    string EngineId { get; }

    /// <summary>
    /// Recognizes text from an image stream on-device.
    /// </summary>
    Task<OcrResult> RecognizeImageAsync(
        Stream imageStream,
        string? languageTag = null,
        CancellationToken ct = default);
}
