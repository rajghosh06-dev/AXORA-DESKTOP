namespace Axora.Desktop.Models;

/// <summary>
/// Immutable specification of optimization and compression capabilities for a specific file format.
/// Grounded in the factual capabilities of the native WIC, SkiaSharp, and document rendering pipelines.
/// </summary>
public sealed record FormatOptimizationCapabilities
{
    /// <summary>
    /// File extension representing the format, normalized with leading dot (e.g., ".jpg", ".png").
    /// </summary>
    public string FormatExtension { get; init; } = string.Empty;

    /// <summary>
    /// User-friendly display name of the target format (e.g., "JPEG Image (*.jpg)").
    /// </summary>
    public string FormatDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether the format supports variable lossy quality compression control.
    /// </summary>
    public bool SupportsQuality { get; init; }

    /// <summary>
    /// Factual explanation of how quality compression applies (or why it does not apply) to this format.
    /// </summary>
    public string QualityExplanation { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether the format supports maximum bounding box dimension downscaling.
    /// </summary>
    public bool SupportsMaxDimension { get; init; }

    /// <summary>
    /// Factual explanation of bounding box pixel downscaling for this format.
    /// </summary>
    public string DimensionExplanation { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether the format supports setting target raster DPI resolution metadata.
    /// </summary>
    public bool SupportsTargetDpi { get; init; }

    /// <summary>
    /// Factual explanation of DPI resolution metadata vs physical pixel dimensions.
    /// </summary>
    public string DpiExplanation { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether the container format supports metadata stripping or preservation.
    /// </summary>
    public bool SupportsMetadataPolicy { get; init; }

    /// <summary>
    /// Factual explanation of metadata handling boundaries and limitations for this format.
    /// </summary>
    public string MetadataExplanation { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether this format is strictly lossless-only (e.g., PNG, BMP, TIFF in our engine).
    /// </summary>
    public bool IsLosslessOnly { get; init; }

    /// <summary>
    /// High-level summary note of this format's optimization profile.
    /// </summary>
    public string SummaryNote { get; init; } = string.Empty;
}

/// <summary>
/// Static catalog providing format-specific optimization capabilities.
/// Enforces single-source-of-truth capability mappings across the UI and ViewModel layers.
/// </summary>
public static class FormatCapabilityCatalog
{
    private static readonly FormatOptimizationCapabilities JpegCapabilities = new()
    {
        FormatExtension = ".jpg",
        FormatDisplayName = "JPEG Image (*.jpg, *.jpeg)",
        SupportsQuality = true,
        QualityExplanation = "Quality controls lossy DCT quantization (1–100) for JPEG output.",
        SupportsMaxDimension = true,
        DimensionExplanation = "Maximum Dimension limits the output pixel bounding box while preserving aspect ratio. Upscaling is never performed.",
        SupportsTargetDpi = true,
        DpiExplanation = "Target DPI controls container resolution metadata (print density); it does not alter pixel count.",
        SupportsMetadataPolicy = true,
        MetadataExplanation = "Preserve Supported Properties copies supported EXIF/XMP items. Raw vendor block passthrough is not guaranteed.",
        IsLosslessOnly = false,
        SummaryNote = "Supports lossy DCT compression, proportional downscaling, DPI metadata, and metadata stripping/preservation."
    };

    private static readonly FormatOptimizationCapabilities WebpCapabilities = new()
    {
        FormatExtension = ".webp",
        FormatDisplayName = "WebP Image (*.webp)",
        SupportsQuality = true,
        QualityExplanation = "Quality 1–99 selects lossy VP8 compression; Quality 100 selects lossless VP8L compression (SkiaSharp runtime).",
        SupportsMaxDimension = true,
        DimensionExplanation = "Maximum Dimension limits the output pixel bounding box while preserving aspect ratio. Upscaling is never performed.",
        SupportsTargetDpi = true,
        DpiExplanation = "Target DPI controls container resolution metadata; it does not alter pixel count.",
        SupportsMetadataPolicy = true,
        MetadataExplanation = "Preserve Supported Properties copies standard image properties. Raw vendor block passthrough is not guaranteed.",
        IsLosslessOnly = false,
        SummaryNote = "Supports lossy (1–99) and lossless (100) compression, proportional downscaling, and DPI metadata."
    };

    private static readonly FormatOptimizationCapabilities PngCapabilities = new()
    {
        FormatExtension = ".png",
        FormatDisplayName = "PNG Image (*.png)",
        SupportsQuality = false,
        QualityExplanation = "Quality does not apply to PNG output (PNG uses lossless DEFLATE encoding).",
        SupportsMaxDimension = true,
        DimensionExplanation = "Maximum Dimension limits the output pixel bounding box while preserving aspect ratio. Upscaling is never performed.",
        SupportsTargetDpi = true,
        DpiExplanation = "Target DPI controls pHYs chunk resolution metadata; it does not alter pixel count.",
        SupportsMetadataPolicy = true,
        MetadataExplanation = "Preserve Supported Properties copies standard text chunks. Raw vendor block passthrough is not guaranteed.",
        IsLosslessOnly = true,
        SummaryNote = "Lossless DEFLATE raster encoding. Quality slider is non-applicable."
    };

    private static readonly FormatOptimizationCapabilities BmpCapabilities = new()
    {
        FormatExtension = ".bmp",
        FormatDisplayName = "Bitmap Image (*.bmp)",
        SupportsQuality = false,
        QualityExplanation = "Quality does not apply to BMP output (BMP is an uncompressed raster bitmap).",
        SupportsMaxDimension = true,
        DimensionExplanation = "Maximum Dimension limits the output pixel bounding box while preserving aspect ratio. Upscaling is never performed.",
        SupportsTargetDpi = true,
        DpiExplanation = "Target DPI controls BITMAPINFOHEADER resolution metadata; it does not alter pixel count.",
        SupportsMetadataPolicy = false,
        MetadataExplanation = "Standard metadata blocks (EXIF/XMP) are not supported by the BMP container format.",
        IsLosslessOnly = true,
        SummaryNote = "Uncompressed device-independent bitmap. Quality and metadata blocks are non-applicable."
    };

    private static readonly FormatOptimizationCapabilities TiffCapabilities = new()
    {
        FormatExtension = ".tiff",
        FormatDisplayName = "TIFF Image (*.tiff, *.tif)",
        SupportsQuality = false,
        QualityExplanation = "Quality slider does not apply to TIFF output (TIFF uses lossless compression in WIC).",
        SupportsMaxDimension = true,
        DimensionExplanation = "Maximum Dimension limits the output pixel bounding box while preserving aspect ratio. Upscaling is never performed.",
        SupportsTargetDpi = true,
        DpiExplanation = "Target DPI controls TIFF resolution tags; it does not alter pixel count.",
        SupportsMetadataPolicy = true,
        MetadataExplanation = "Preserve Supported Properties copies supported baseline TIFF tags. Raw vendor block passthrough is not guaranteed.",
        IsLosslessOnly = true,
        SummaryNote = "Lossless tagged image file format. Quality slider is non-applicable."
    };

    private static readonly FormatOptimizationCapabilities PdfCapabilities = new()
    {
        FormatExtension = ".pdf",
        FormatDisplayName = "PDF Document (*.pdf)",
        SupportsQuality = false,
        QualityExplanation = "Raster quality settings do not apply directly to PDF document generation.",
        SupportsMaxDimension = false,
        DimensionExplanation = "Document page dimensions are determined by document source or layout configuration.",
        SupportsTargetDpi = true,
        DpiExplanation = "Target DPI controls rasterization density when rendering PDF pages to images.",
        SupportsMetadataPolicy = true,
        MetadataExplanation = "Document metadata fields (Title, Author, Subject) are handled independently from image EXIF.",
        IsLosslessOnly = false,
        SummaryNote = "Fixed-layout document format. Raster quality and dimension limits are non-applicable."
    };

    private static readonly FormatOptimizationCapabilities AutoCapabilities = new()
    {
        FormatExtension = string.Empty,
        FormatDisplayName = "Automatic (Source Format)",
        SupportsQuality = true,
        QualityExplanation = "Quality applies when target or source format uses lossy compression (e.g. JPEG, WebP).",
        SupportsMaxDimension = true,
        DimensionExplanation = "Maximum Dimension limits the output pixel bounding box while preserving aspect ratio. Upscaling is never performed.",
        SupportsTargetDpi = true,
        DpiExplanation = "Target DPI controls container resolution metadata (print density); it does not alter pixel count.",
        SupportsMetadataPolicy = true,
        MetadataExplanation = "Preserve Supported Properties copies supported image metadata. Raw vendor block passthrough is not guaranteed.",
        IsLosslessOnly = false,
        SummaryNote = "Parameters adapt to source format. Select an explicit target format for full optimization control."
    };

    private static readonly FormatOptimizationCapabilities DefaultCapabilities = new()
    {
        FormatExtension = string.Empty,
        FormatDisplayName = "Default / Generic Format",
        SupportsQuality = false,
        QualityExplanation = "Quality settings do not apply to this target format.",
        SupportsMaxDimension = false,
        DimensionExplanation = "Dimension scaling is not supported for this target format.",
        SupportsTargetDpi = false,
        DpiExplanation = "Target DPI is not applicable to this target format.",
        SupportsMetadataPolicy = false,
        MetadataExplanation = "Metadata configuration is not applicable to this target format.",
        IsLosslessOnly = true,
        SummaryNote = "Standard conversion without raster optimization settings."
    };

    public static FormatOptimizationCapabilities GetCapabilities(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return AutoCapabilities;
        }

        var normalized = format.Trim().ToLowerInvariant();
        if (normalized.StartsWith('.'))
        {
            normalized = normalized[1..];
        }

        if (normalized == "auto")
        {
            return AutoCapabilities;
        }

        return normalized switch
        {
            "jpg" or "jpeg" => JpegCapabilities,
            "webp" => WebpCapabilities,
            "png" => PngCapabilities,
            "bmp" => BmpCapabilities,
            "tif" or "tiff" => TiffCapabilities,
            "pdf" => PdfCapabilities,
            _ => DefaultCapabilities with { FormatExtension = "." + normalized, FormatDisplayName = $"{normalized.ToUpperInvariant()} Format" }
        };
    }
}
