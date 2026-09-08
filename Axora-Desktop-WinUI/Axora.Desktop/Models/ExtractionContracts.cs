using System;
using System.Collections.Generic;
using System.Text;

namespace Axora.Desktop.Models;

/// <summary>
/// Specifies the semantic nature of page boundaries within an extracted document.
/// Distinguishes between fixed physical sheets and flow-based logical/virtual sections.
/// </summary>
public enum PageSemanticsType
{
    /// <summary>
    /// Exact 1:1 physical page mapping from fixed-layout source media (e.g. PDF, scanned paper, multi-frame TIFF).
    /// </summary>
    PhysicalPage = 0,

    /// <summary>
    /// Document division derived from author-defined structural markers (e.g. Word sections, explicit page breaks, Markdown headers, HTML articles).
    /// Physical printed sheets are explicitly renderer-dependent and not guaranteed.
    /// </summary>
    LogicalSection = 1,

    /// <summary>
    /// Virtual pagination computed by character or paragraph thresholds for continuous streams lacking structural breaks (e.g. unformatted plain text).
    /// </summary>
    VirtualPage = 2
}

/// <summary>
/// High-level lifecycle and readiness states for local on-device OCR capability.
/// </summary>
public enum OcrCapabilityState
{
    /// <summary>
    /// OCR engine is functional and ready with matching language resources.
    /// </summary>
    OcrAvailable = 0,

    /// <summary>
    /// No local OCR engine runtime or hardware capability is available on the system.
    /// </summary>
    OcrUnavailable = 1,

    /// <summary>
    /// OCR engine is present, but the requested language pack (e.g. "de-DE", "ja-JP") is not installed.
    /// </summary>
    OcrLanguageUnavailable = 2,

    /// <summary>
    /// OCR engine failed during initialization or unrecoverable native runtime execution.
    /// </summary>
    OcrFailed = 3,

    /// <summary>
    /// Batch OCR completed with partial success (some pages extracted, some pages degraded or empty).
    /// </summary>
    OcrPartiallyCompleted = 4
}

/// <summary>
/// Granular format classification produced by content sniffing and magic byte inspection.
/// </summary>
public enum DetectedDocumentFormat
{
    Unknown = 0,
    PdfDigital = 1,
    PdfScanned = 2,
    PdfMixed = 3,
    PlainText = 4,
    Markdown = 5,
    Docx = 6,
    DelimitedText = 7,
    LocalHtml = 8,
    RasterImage = 9,
    MultiPageTiff = 10
}

/// <summary>
/// Status outcome of a document extraction attempt.
/// </summary>
public enum DocumentExtractionStatus
{
    Success = 0,
    PartialSuccess = 1,
    Failed = 2,
    Cancelled = 3,
    OcrRequired = 4
}

/// <summary>
/// Machine-readable diagnostic codes for document normalization operations.
/// </summary>
public static class NormalizationDiagnosticCodes
{
    public const string Ok = "NORM_OK";
    public const string Warning = "NORM_WARNING";
    public const string Failed = "NORM_FAILED";
    public const string Cancelled = "NORM_CANCELLED";
    public const string FallbackRaw = "NORM_FALLBACK_RAW";
}

/// <summary>
/// Status outcome of a document normalization operation.
/// </summary>
public enum NormalizationStatus
{
    NotRun = 0,
    Succeeded = 1,
    SucceededWithWarnings = 2,
    Failed = 3,
    Cancelled = 4,
    FallbackToRaw = 5
}

/// <summary>
/// Scoped diagnostic telemetry for a single page normalization step.
/// Strictly excludes document text, excerpts, and secrets.
/// </summary>
public sealed record PageNormalizationTelemetry
{
    public required int PageNumber { get; init; }
    public required DetectedDocumentFormat Format { get; init; }
    public NormalizationStatus Status { get; init; } = NormalizationStatus.NotRun;
    public string StatusCode { get; init; } = NormalizationDiagnosticCodes.Ok;
    public TimeSpan Elapsed { get; init; } = TimeSpan.Zero;
    public int InputCharacterCount { get; init; }
    public int OutputCharacterCount { get; init; }
    public bool TransformationsApplied => InputCharacterCount != OutputCharacterCount || Status == NormalizationStatus.SucceededWithWarnings;
    public bool FallbackOccurred { get; init; }
    public bool ExtractedViaOcr { get; init; }
    public string? DiagnosticCode { get; init; }
    public string? DiagnosticWarning { get; init; }
}

/// <summary>
/// Aggregate diagnostic telemetry for document normalization across all pages.
/// Strictly excludes document text, excerpts, and secrets.
/// </summary>
public sealed record DocumentNormalizationTelemetry
{
    public required string DocumentId { get; init; }
    public required DetectedDocumentFormat Format { get; init; }
    public NormalizationStatus OverallStatus { get; init; } = NormalizationStatus.NotRun;
    public TimeSpan TotalDuration { get; init; } = TimeSpan.Zero;
    public int TotalPagesProcessed { get; init; }
    public int SuccessfulPagesCount { get; init; }
    public int FailedPagesCount { get; init; }
    public int FallbackPagesCount { get; init; }
    public int TotalInputCharacters { get; init; }
    public int TotalOutputCharacters { get; init; }
    public bool HasOcrPages { get; init; }
    public IReadOnlyList<PageNormalizationTelemetry> PageDiagnostics { get; init; } = [];
}

/// <summary>
/// Configurable options governing page-bounded passage chunk generation.
/// Proposed defaults: 350 target characters, 60 stride overlap, 40 snap window delta.
/// Defaults are configurable and subject to empirical validation rather than hard-coded dogma.
/// </summary>
public sealed record ChunkingOptions
{
    public int TargetChunkSizeChars { get; init; } = 350;
    public int StrideOverlapChars { get; init; } = 60;
    public int SentenceSnapBoundaryDelta { get; init; } = 40;
    public bool SnapToSentenceBoundaries { get; init; } = true;

    public void Validate()
    {
        if (TargetChunkSizeChars <= 0)
            throw new ArgumentOutOfRangeException(nameof(TargetChunkSizeChars), "Target chunk size must be positive.");
        if (StrideOverlapChars < 0)
            throw new ArgumentOutOfRangeException(nameof(StrideOverlapChars), "Stride overlap cannot be negative.");
        if (StrideOverlapChars >= TargetChunkSizeChars)
            throw new ArgumentOutOfRangeException(nameof(StrideOverlapChars), "Stride overlap must be strictly less than target chunk size.");
        if (SentenceSnapBoundaryDelta < 0)
            throw new ArgumentOutOfRangeException(nameof(SentenceSnapBoundaryDelta), "Sentence snap boundary delta cannot be negative.");
    }
}

/// <summary>
/// Configurable options governing deterministic text normalization.
/// Strictly differentiates between lossless text cleanup and potentially semantic glyph transformations.
/// RawText is never modified.
/// </summary>
public sealed record TextNormalizationOptions
{
    // Tier A: Artifact-Preserving Cleanup (Preserves exact academic and semantic meaning)
    public bool NormalizeLineEndings { get; init; } = true;
    public bool StripNonPrintableControlChars { get; init; } = true;
    public bool StripBOMAndZeroWidthChars { get; init; } = true;

    // Tier B: Formatting Standardizations
    public bool CollapseConsecutiveSpaces { get; init; } = true;
    public bool PreserveParagraphBreaks { get; init; } = true;

    // Tier C: Optional Glyphic Transformations
    public bool ApplyUnicodeNfkc { get; init; } = false;
    public bool UnfoldTypesettingLigatures { get; init; } = true;
    public bool RepairLinebreakHyphenation { get; init; } = false;

    public void Validate()
    {
        // All options are boolean flags; structure supports future parameter additions
    }
}

/// <summary>
/// Configurable resource and security bounds protecting against pathological files and resource exhaustion.
/// Values represent configurable baseline defaults for academic workloads.
/// </summary>
public sealed record ExtractionSecurityOptions
{
    public long MaxFileSizeBytes { get; init; } = 250L * 1024 * 1024; // 250 MB
    public int MaxPagesToExtract { get; init; } = 1000;              // 1,000 pages
    public int MaxImageDimensionPx { get; init; } = 16384;           // 16K x 16K pixels
    public long MaxDocxUncompressedBytes { get; init; } = 500L * 1024 * 1024; // 500 MB
    public double MaxZipCompressionRatio { get; init; } = 100.0;     // 100:1 ratio limit
    public int MaxXmlDocumentChars { get; init; } = 50_000_000;      // 50M characters
    public TimeSpan ExtractionTimeout { get; init; } = TimeSpan.FromMinutes(5);

    public void Validate()
    {
        if (MaxFileSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxFileSizeBytes), "Max file size must be positive.");
        if (MaxPagesToExtract <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxPagesToExtract), "Max pages must be positive.");
        if (MaxImageDimensionPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxImageDimensionPx), "Max image dimension must be positive.");
        if (MaxDocxUncompressedBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxDocxUncompressedBytes), "Max uncompressed bytes must be positive.");
        if (MaxZipCompressionRatio <= 1.0)
            throw new ArgumentOutOfRangeException(nameof(MaxZipCompressionRatio), "Max compression ratio must be greater than 1.0.");
        if (MaxXmlDocumentChars <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxXmlDocumentChars), "Max XML document chars must be positive.");
        if (ExtractionTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ExtractionTimeout), "Extraction timeout must be positive.");
    }
}

/// <summary>
/// Complete options payload provided to the extraction orchestrator and individual engines.
/// </summary>
public sealed record ExtractionOptions
{
    public bool ForceOcr { get; init; } = false;
    public string? OcrLanguage { get; init; } = null;
    public int? MaxPages { get; init; } = null;
    public int MaxDegreeOfParallelism { get; init; } = Environment.ProcessorCount;
    public bool PreserveEmptyPages { get; init; } = true;
    public TextNormalizationOptions Normalization { get; init; } = new();
    public ChunkingOptions Chunking { get; init; } = new();
    public ExtractionSecurityOptions Security { get; init; } = new();

    public void Validate()
    {
        Normalization?.Validate();
        Chunking?.Validate();
        Security?.Validate();

        if (MaxPages.HasValue && MaxPages.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxPages), "Max pages must be positive when specified.");
        if (MaxDegreeOfParallelism <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxDegreeOfParallelism), "Max degree of parallelism must be positive.");
    }
}

/// <summary>
/// Content-based sniffing result produced by IDocumentFormatDetector.
/// </summary>
public sealed record FormatDetectionResult
{
    public required DetectedDocumentFormat Format { get; init; }
    public required string MimeType { get; init; }
    public Encoding? DetectedEncoding { get; init; }
    public bool RequiresOcr { get; init; }
    public long FileSizeBytes { get; init; }
    public string? SuggestedFileName { get; init; }
    public bool IsSupported => Format != DetectedDocumentFormat.Unknown;
}

/// <summary>
/// Intermediate representation of an extracted page preserving unmutated RawText ground truth.
/// </summary>
public sealed record ExtractedPageRaw
{
    public required int PageNumber { get; init; } // 1-indexed
    public PageSemanticsType PageSemantics { get; init; } = PageSemanticsType.PhysicalPage;
    public double WidthPt { get; init; }
    public double HeightPt { get; init; }
    public required string RawText { get; init; }
    public string? NormalizedText { get; init; }
    public bool ExtractedViaOcr { get; init; }
    public double Confidence { get; init; } = 1.0;
    public string? DiagnosticWarning { get; init; }
}

/// <summary>
/// Engine-level extraction outcome containing raw extracted pages and diagnostic metrics.
/// </summary>
public sealed record RawExtractionResult
{
    public required string DocumentTitle { get; init; }
    public string Author { get; init; } = string.Empty;
    public required DetectedDocumentFormat Format { get; init; }
    public required IReadOnlyList<ExtractedPageRaw> Pages { get; init; }
    public required TimeSpan Duration { get; init; }
    public required string EngineIdentifier { get; init; }
    public IReadOnlyList<string> GlobalWarnings { get; init; } = [];
    public bool IsPartialSuccess { get; init; }
}

/// <summary>
/// Diagnostic report summarizing document extraction performance and outcome.
/// </summary>
public sealed record ExtractionReport
{
    public required string DocumentId { get; init; }
    public required string FileName { get; init; }
    public required DetectedDocumentFormat Format { get; init; }
    public required PageSemanticsType PageSemantics { get; init; }
    public required int TotalPages { get; init; }
    public required int TotalChunks { get; init; }
    public required int TotalCharacters { get; init; }
    public required TimeSpan ElapsedTime { get; init; }
    public required bool IsOcrUsed { get; init; }
    public required bool IsFullySuccessful { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DocumentNormalizationTelemetry? NormalizationTelemetry { get; init; }
}

/// <summary>
/// Final orchestrator output uniting the durable ScholarDocument with an ExtractionReport.
/// </summary>
public sealed record ScholarExtractionResult
{
    public required ScholarDocument Document { get; init; }
    public required ExtractionReport Report { get; init; }
    public DocumentNormalizationTelemetry? NormalizationTelemetry { get; init; }
    public bool IsSuccess => Report.IsFullySuccessful;
}

/// <summary>
/// Metadata describing the capabilities and runtime limitations of a PDF extractor engine.
/// </summary>
public sealed record PdfExtractionCapabilities
{
    public required string EngineName { get; init; }
    public required string EngineVersion { get; init; }
    public required bool SupportsDirectRasterization { get; init; }
    public required bool SupportsCustomFontCmaps { get; init; }
    public required bool SupportsRightToLeftScripts { get; init; }
}

/// <summary>
/// Output produced by an isolated OCR recognition request.
/// </summary>
public sealed record OcrResult
{
    public required string Text { get; init; }
    public double Confidence { get; init; } = 1.0;
    public string LanguageTag { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public TimeSpan Elapsed { get; init; } = TimeSpan.Zero;
}

/// <summary>
/// Specifies the ordering policy for multi-passage composite window formulation.
/// </summary>
public enum CompositeOrderingMode
{
    /// <summary>
    /// Chunks are ordered by document reading flow: DocumentId -> PageNumber -> ChunkIndex.
    /// </summary>
    DocumentReadingOrder = 0,

    /// <summary>
    /// Chunks are assembled in the exact sequence provided by the caller (e.g. relevance rank).
    /// </summary>
    PreserveInputOrder = 1
}

/// <summary>
/// Options governing bounded context window formulation.
/// All bounds are strictly enforced by Validate().
/// </summary>
public sealed record ContextWindowOptions
{
    /// <summary>
    /// Preferred target character budget for greedy window formulation (default: 800 chars ~ 200 tokens).
    /// Bounded in [50, MaxWindowChars].
    /// </summary>
    public int TargetWindowChars { get; init; } = 800;

    /// <summary>
    /// Hard maximum character ceiling for a context window (default: 1,500 chars ~ 375 tokens).
    /// Bounded in [100, 10,000].
    /// </summary>
    public int MaxWindowChars { get; init; } = 1500;

    /// <summary>
    /// Number of preceding neighbor chunks to include when expanding a focal chunk (default: 1).
    /// Bounded in [0, 10].
    /// </summary>
    public int PrecedingNeighborCount { get; init; } = 1;

    /// <summary>
    /// Number of succeeding neighbor chunks to include when expanding a focal chunk (default: 1).
    /// Bounded in [0, 10].
    /// </summary>
    public int SucceedingNeighborCount { get; init; } = 1;

    /// <summary>
    /// If true, adjacent overlapping chunks are merged seamlessly using NormalizedText coordinates
    /// rather than string concatenation, eliminating stride overlap duplication (default: true).
    /// </summary>
    public bool DeduplicateOverlaps { get; init; } = true;

    /// <summary>
    /// If true, multi-passage composite windows format each passage with a citation banner (default: true).
    /// </summary>
    public bool IncludeProvenanceHeaders { get; init; } = true;

    /// <summary>
    /// Maximum character budget for composite multi-page prompt windows (default: 3,000 chars ~ 750 tokens).
    /// Bounded in [200, 50,000].
    /// </summary>
    public int CompositeBudgetChars { get; init; } = 3000;

    /// <summary>
    /// Ordering policy for composite prompt window assembly (default: DocumentReadingOrder).
    /// </summary>
    public CompositeOrderingMode OrderingMode { get; init; } = CompositeOrderingMode.DocumentReadingOrder;

    /// <summary>
    /// Optional default file name used for citation formatting when DocumentPage has no parent reference.
    /// </summary>
    public string DefaultFileName { get; init; } = string.Empty;

    public void Validate()
    {
        if (MaxWindowChars < 100 || MaxWindowChars > 10000)
            throw new ArgumentOutOfRangeException(nameof(MaxWindowChars), "Max window chars must be between 100 and 10,000.");
        if (TargetWindowChars < 50 || TargetWindowChars > MaxWindowChars)
            throw new ArgumentOutOfRangeException(nameof(TargetWindowChars), "Target window chars must be between 50 and MaxWindowChars.");
        if (PrecedingNeighborCount < 0 || PrecedingNeighborCount > 10)
            throw new ArgumentOutOfRangeException(nameof(PrecedingNeighborCount), "Preceding neighbor count must be between 0 and 10.");
        if (SucceedingNeighborCount < 0 || SucceedingNeighborCount > 10)
            throw new ArgumentOutOfRangeException(nameof(SucceedingNeighborCount), "Succeeding neighbor count must be between 0 and 10.");
        if (CompositeBudgetChars < 200 || CompositeBudgetChars > 50000)
            throw new ArgumentOutOfRangeException(nameof(CompositeBudgetChars), "Composite budget chars must be between 200 and 50,000.");
    }
}
