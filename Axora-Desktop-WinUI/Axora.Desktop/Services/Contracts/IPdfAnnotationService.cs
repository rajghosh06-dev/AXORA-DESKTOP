using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// High-performance hardware-accelerated PDF annotation and digital redaction studio service.
/// Provides ink annotations, digital signature stamping, vector text overlays, and permanent
/// DoD/HIPAA-compliant blackout redaction burning where underlying text streams are completely purged.
/// </summary>
public interface IPdfAnnotationService
{
    /// <summary>
    /// Renders a specific page of a PDF document into a stream with applied annotation overlays.
    /// </summary>
    Task<Stream> RenderAnnotatedPagePreviewAsync(
        Stream pdfStream,
        int pageIndex,
        IReadOnlyList<AnnotationItem> annotations,
        float scale = 1.5f,
        CancellationToken ct = default);

    /// <summary>
    /// Permanently burns annotations and blackout redactions into the target PDF document.
    /// Purges all underlying text streams, glyphs, and raster pixels within redaction bounding boxes
    /// to guarantee zero data leakage.
    /// </summary>
    Task<Stream> BurnAndPurgeRedactionsAsync(
        Stream pdfStream,
        IReadOnlyList<RedactionRegion> redactions,
        IReadOnlyList<AnnotationItem> annotations,
        RedactionExportOptions options,
        CancellationToken ct = default);
}
