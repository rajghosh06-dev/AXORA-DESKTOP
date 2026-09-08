using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Media.Ocr;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;
using Microsoft.Extensions.Logging;

namespace Axora.Desktop.Services;

/// <summary>
/// On-device OCR capability state provider backed by Windows.Media.Ocr.OcrEngine.
/// Queries installed Windows language packs and manages OCR readiness states without cloud dependencies.
/// </summary>
public sealed class WindowsOcrCapabilityStateProvider : IOcrCapabilityStateProvider
{
    private readonly ILogger<WindowsOcrCapabilityStateProvider>? _logger;
    private readonly object _syncLock = new();

    private OcrCapabilityState _state;
    private string? _activeLanguageTag;
    private IReadOnlyList<string> _installedLanguages = [];

    /// <inheritdoc/>
    public OcrCapabilityState State
    {
        get { lock (_syncLock) return _state; }
        private set { lock (_syncLock) _state = value; }
    }

    /// <inheritdoc/>
    public string? ActiveLanguageTag
    {
        get { lock (_syncLock) return _activeLanguageTag; }
        private set { lock (_syncLock) _activeLanguageTag = value; }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> InstalledLanguages
    {
        get { lock (_syncLock) return _installedLanguages; }
        private set { lock (_syncLock) _installedLanguages = value; }
    }

    public WindowsOcrCapabilityStateProvider(ILogger<WindowsOcrCapabilityStateProvider>? logger = null)
    {
        _logger = logger;
        DetectStateInternal();
    }

    /// <inheritdoc/>
    public async Task<OcrCapabilityState> RefreshStateAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            lock (_syncLock)
            {
                DetectStateInternal();
                return _state;
            }
        }, ct);
    }

    /// <summary>
    /// Checks readiness state for a specific requested language tag.
    /// </summary>
    public OcrCapabilityState CheckLanguageState(string? languageTag)
    {
        lock (_syncLock)
        {
            if (_state == OcrCapabilityState.OcrUnavailable)
            {
                return OcrCapabilityState.OcrUnavailable;
            }

            if (string.IsNullOrWhiteSpace(languageTag))
            {
                return _state;
            }

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
                return OcrCapabilityState.OcrLanguageUnavailable;
            }

            var lang = new Language(languageTag);
            if (OcrEngine.IsLanguageSupported(lang))
            {
                return OcrCapabilityState.OcrAvailable;
            }

            return OcrCapabilityState.OcrLanguageUnavailable;
        }
    }

    private void DetectStateInternal()
    {
        try
        {
            var availableLanguages = OcrEngine.AvailableRecognizerLanguages;
            if (availableLanguages == null || availableLanguages.Count == 0)
            {
                _state = OcrCapabilityState.OcrUnavailable;
                _activeLanguageTag = null;
                _installedLanguages = Array.Empty<string>();
                _logger?.LogWarning("Windows OCR capability unavailable: 0 recognizer language packs installed.");
                return;
            }

            var tags = availableLanguages.Select(l => l.LanguageTag).ToList();
            _installedLanguages = tags.AsReadOnly();

            // Try user profile languages first
            var engine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (engine != null)
            {
                _state = OcrCapabilityState.OcrAvailable;
                _activeLanguageTag = engine.RecognizerLanguage.LanguageTag;
                _logger?.LogInformation("Windows OCR capability ready. Active user profile language: {Lang}", _activeLanguageTag);
                return;
            }

            // Fallback: en-US
            var en = new Language("en-US");
            if (OcrEngine.IsLanguageSupported(en))
            {
                var enEngine = OcrEngine.TryCreateFromLanguage(en);
                if (enEngine != null)
                {
                    _state = OcrCapabilityState.OcrAvailable;
                    _activeLanguageTag = enEngine.RecognizerLanguage.LanguageTag;
                    _logger?.LogInformation("Windows OCR capability ready. Active fallback language: {Lang}", _activeLanguageTag);
                    return;
                }
            }

            // Fallback: first available installed language
            foreach (var lang in availableLanguages)
            {
                if (OcrEngine.IsLanguageSupported(lang))
                {
                    var fallbackEngine = OcrEngine.TryCreateFromLanguage(lang);
                    if (fallbackEngine != null)
                    {
                        _state = OcrCapabilityState.OcrAvailable;
                        _activeLanguageTag = fallbackEngine.RecognizerLanguage.LanguageTag;
                        _logger?.LogInformation("Windows OCR capability ready. Active first-available language: {Lang}", _activeLanguageTag);
                        return;
                    }
                }
            }

            _state = OcrCapabilityState.OcrFailed;
            _activeLanguageTag = null;
            _logger?.LogError("Windows OCR capability failed: Available language packs detected but no engine could be initialized.");
        }
        catch (Exception ex)
        {
            _state = OcrCapabilityState.OcrFailed;
            _activeLanguageTag = null;
            _installedLanguages = Array.Empty<string>();
            _logger?.LogError(ex, "Unexpected error during Windows OCR capability detection.");
        }
    }
}
