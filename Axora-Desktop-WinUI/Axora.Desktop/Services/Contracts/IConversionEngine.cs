using System;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Contract for a specialized conversion engine adapter (e.g. WIC, PdfSharpCore, ImageMagick).
/// </summary>
public interface IConversionEngine
{
    /// <summary>Unique identifier for the engine (e.g. "wic", "pdfsharp", "imagemagick").</summary>
    string EngineId { get; }

    /// <summary>Human-readable display name of the engine.</summary>
    string DisplayName { get; }

    /// <summary>Indicates whether the engine and all required runtime components are available.</summary>
    bool IsAvailable { get; }

    /// <summary>Optional extension ID registered with IDependencyManager if an external dependency is required.</summary>
    string? RequiredDependencyId { get; }

    /// <summary>Resource and concurrency profile for this engine.</summary>
    EngineResourceProfile ResourceProfile { get; }

    /// <summary>Determines whether this engine supports converting from sourceExtension to targetExtension.</summary>
    bool CanConvert(string sourceExtension, string targetExtension);

    /// <summary>
    /// Executes the conversion asynchronously.
    /// Implementations must observe cancellation, report incremental progress, and write atomically to target.
    /// </summary>
    Task<ConversionResult> ConvertAsync(
        ConversionJob job,
        IProgress<double>? progress = null,
        CancellationToken ct = default);
}
