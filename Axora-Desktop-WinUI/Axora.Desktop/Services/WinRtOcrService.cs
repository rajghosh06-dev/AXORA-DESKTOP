using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// WinRT OCR service wrapper routing through <see cref="IOcrEngine"/> and <see cref="IOcrCapabilityStateProvider"/>.
/// Preserves legacy IOcrService caller contracts while delegating execution to the modern decoupled engine.
/// All processing is fully on-device — zero cloud or network calls.
/// </summary>
public sealed class WinRtOcrService : IOcrService
{
    private readonly IOcrEngine _ocrEngine;
    private readonly IOcrCapabilityStateProvider _capabilityProvider;
    private readonly ILogger<WinRtOcrService>? _logger;

    public bool IsAvailable => _capabilityProvider.State == OcrCapabilityState.OcrAvailable;
    public string ActiveLanguage => _capabilityProvider.ActiveLanguageTag ?? "unavailable";

    public WinRtOcrService(
        IOcrEngine ocrEngine,
        IOcrCapabilityStateProvider capabilityProvider,
        ILogger<WinRtOcrService>? logger = null)
    {
        _ocrEngine = ocrEngine ?? throw new ArgumentNullException(nameof(ocrEngine));
        _capabilityProvider = capabilityProvider ?? throw new ArgumentNullException(nameof(capabilityProvider));
        _logger = logger;
    }

    public WinRtOcrService(ILogger<WinRtOcrService>? logger = null)
        : this(new WindowsMediaOcrEngine(), new WindowsOcrCapabilityStateProvider(), logger)
    {
    }

    /// <inheritdoc/>
    public async Task<string> ExtractTextAsync(Stream imageStream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        if (!IsAvailable)
        {
            throw new InvalidOperationException("OCR engine is unavailable. Install a Windows language pack.");
        }

        var result = await _ocrEngine.RecognizeImageAsync(imageStream, null, ct);
        return result.Text;
    }

    /// <inheritdoc/>
    public async Task<string> ExtractTextFromFileAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        using var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return await ExtractTextAsync(fileStream, ct);
    }
}
