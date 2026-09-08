using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Capability provider contract reporting the readiness, installed languages, and status of on-device OCR.
/// </summary>
public interface IOcrCapabilityStateProvider
{
    /// <summary>
    /// Current readiness state of the OCR capability.
    /// </summary>
    OcrCapabilityState State { get; }

    /// <summary>
    /// Active or primary language tag configured on the device.
    /// </summary>
    string? ActiveLanguageTag { get; }

    /// <summary>
    /// List of language tags currently installed and available for OCR on the host system.
    /// </summary>
    IReadOnlyList<string> InstalledLanguages { get; }

    /// <summary>
    /// Re-evaluates on-device OCR capabilities asynchronously.
    /// </summary>
    Task<OcrCapabilityState> RefreshStateAsync(CancellationToken ct = default);
}
