using System;

namespace Axora.Desktop.Models;

/// <summary>
/// Root domain exception for all document extraction and intake errors in Scholar Kit.
/// Ensures that internal exceptions do not leak sensitive document content into user-facing or logged messages.
/// </summary>
public class ScholarExtractionException : Exception
{
    public string ErrorCode { get; }
    public string SafeUserMessage { get; }

    public ScholarExtractionException(
        string errorCode,
        string safeUserMessage,
        string internalDiagnosticMessage,
        Exception? innerException = null)
        : base(internalDiagnosticMessage, innerException)
    {
        ErrorCode = errorCode ?? "ERR_EXTRACTION_UNKNOWN";
        SafeUserMessage = safeUserMessage ?? "An error occurred while extracting content from the document.";
    }
}

/// <summary>
/// Thrown when a file's format signature (magic bytes) cannot be identified as any supported format.
/// </summary>
public class UnsupportedDocumentFormatException : ScholarExtractionException
{
    public string? DetectedMimeType { get; }

    public UnsupportedDocumentFormatException(string? detectedMimeType = null, string? internalDetails = null)
        : base(
            "ERR_FORMAT_UNRECOGNIZED",
            "Unsupported file format. Please import a supported document format (PDF, Word, Plain Text, Markdown, CSV, or Image).",
            $"File format not recognized. Detected MIME: '{detectedMimeType ?? "unknown"}'. {internalDetails}".Trim())
    {
        DetectedMimeType = detectedMimeType;
    }
}

/// <summary>
/// Thrown when document syntax is broken, corrupted, or cannot be parsed.
/// </summary>
public class DocumentCorruptException : ScholarExtractionException
{
    public DocumentCorruptException(string internalDiagnosticMessage, Exception? innerException = null)
        : base(
            "ERR_FILE_CORRUPTED",
            "The selected document appears damaged or incomplete and cannot be opened.",
            internalDiagnosticMessage,
            innerException)
    {
    }
}

/// <summary>
/// Thrown when a document (such as a PDF) requires a password or encryption key.
/// </summary>
public class DocumentPasswordProtectedException : ScholarExtractionException
{
    public DocumentPasswordProtectedException(string internalDiagnosticMessage = "Document is encrypted.")
        : base(
            "ERR_FILE_PASSWORD_PROTECTED",
            "This document is password-protected or encrypted. AXORA requires an unprotected document to extract text.",
            internalDiagnosticMessage)
    {
    }
}

/// <summary>
/// Thrown when plain text character encoding cannot be decoded or mapped to supported code pages.
/// </summary>
public class InvalidDocumentEncodingException : ScholarExtractionException
{
    public InvalidDocumentEncodingException(string internalDiagnosticMessage, Exception? innerException = null)
        : base(
            "ERR_UNSUPPORTED_ENCODING",
            "The document encoding could not be resolved.",
            internalDiagnosticMessage,
            innerException)
    {
    }
}

/// <summary>
/// Base exception thrown when a document exceeds configured local resource safety boundaries.
/// </summary>
public class ExtractionSecurityLimitException : ScholarExtractionException
{
    public ExtractionSecurityLimitException(string errorCode, string safeUserMessage, string internalDiagnosticMessage)
        : base(errorCode, safeUserMessage, internalDiagnosticMessage)
    {
    }
}

/// <summary>
/// Thrown when a file exceeds the maximum configured file size (default: 250 MB).
/// </summary>
public class FileSizeLimitExceededException : ExtractionSecurityLimitException
{
    public long ActualSizeBytes { get; }
    public long MaxAllowedBytes { get; }

    public FileSizeLimitExceededException(long actualBytes, long maxAllowedBytes)
        : base(
            "ERR_FILE_SIZE_LIMIT_EXCEEDED",
            $"The file size exceeds the local limit ({maxAllowedBytes / (1024 * 1024)} MB) for extraction.",
            $"File size {actualBytes} bytes exceeded threshold of {maxAllowedBytes} bytes.")
    {
        ActualSizeBytes = actualBytes;
        MaxAllowedBytes = maxAllowedBytes;
    }
}

/// <summary>
/// Thrown when a multi-page document exceeds the maximum allowable page limit (default: 1,000 pages).
/// </summary>
public class PageLimitExceededException : ExtractionSecurityLimitException
{
    public int ActualPages { get; }
    public int MaxAllowedPages { get; }

    public PageLimitExceededException(int actualPages, int maxAllowedPages)
        : base(
            "ERR_PAGE_LIMIT_EXCEEDED",
            $"The document exceeds the maximum allowable page count ({maxAllowedPages} pages).",
            $"Document contains {actualPages} pages, exceeding the limit of {maxAllowedPages}.")
    {
        ActualPages = actualPages;
        MaxAllowedPages = maxAllowedPages;
    }
}

/// <summary>
/// Thrown when a compressed archive (e.g. DOCX OpenXML) exceeds the safe decompression ratio or total uncompressed size.
/// </summary>
public class ZipBombDetectedException : ExtractionSecurityLimitException
{
    public double ActualRatio { get; }
    public double MaxRatio { get; }

    public ZipBombDetectedException(double actualRatio, double maxRatio)
        : base(
            "ERR_ZIP_BOMB_DETECTED",
            "The compressed document exceeds safe decompression limits and was rejected for safety.",
            $"Compression ratio {actualRatio:F1}:1 exceeded safety limit of {maxRatio:F1}:1.")
    {
        ActualRatio = actualRatio;
        MaxRatio = maxRatio;
    }
}

/// <summary>
/// Thrown when an image's pixel dimensions exceed the maximum allocatable threshold (default: 16,384 x 16,384 px).
/// </summary>
public class ImageDimensionExceededException : ExtractionSecurityLimitException
{
    public int Width { get; }
    public int Height { get; }
    public int MaxDimension { get; }

    public ImageDimensionExceededException(int width, int height, int maxDimension)
        : base(
            "ERR_IMAGE_DIMENSIONS_EXCEEDED",
            "Image resolution exceeds the maximum allowable dimensions for on-device processing.",
            $"Image dimensions {width}x{height} px exceeded max allowable dimension of {maxDimension} px.")
    {
        Width = width;
        Height = height;
        MaxDimension = maxDimension;
    }
}

/// <summary>
/// Thrown when on-device OCR is required by the document but no OCR engine runtime is available.
/// </summary>
public class OcrUnavailableException : ScholarExtractionException
{
    public OcrUnavailableException(string internalDiagnosticMessage = "No local OCR engine available.")
        : base(
            "ERR_OCR_UNAVAILABLE",
            "On-device OCR capability is not available on this system.",
            internalDiagnosticMessage)
    {
    }
}

/// <summary>
/// Thrown when the requested OCR language pack is not installed on the host system.
/// </summary>
public class OcrLanguageUnavailableException : ScholarExtractionException
{
    public string? RequestedLanguageTag { get; }

    public OcrLanguageUnavailableException(string? requestedLanguageTag, string? internalDiagnosticMessage = null)
        : base(
            "ERR_OCR_LANGUAGE_UNAVAILABLE",
            $"The requested OCR language pack '{requestedLanguageTag ?? "default"}' is not installed on this device.",
            $"OCR language '{requestedLanguageTag}' unavailable. {internalDiagnosticMessage}".Trim())
    {
        RequestedLanguageTag = requestedLanguageTag;
    }
}

/// <summary>
/// Thrown when the native OCR engine fails during image recognition execution.
/// </summary>
public class OcrExecutionException : ScholarExtractionException
{
    public OcrExecutionException(string internalDiagnosticMessage, Exception? innerException = null)
        : base(
            "ERR_OCR_INTERNAL_FAILURE",
            "The on-device OCR engine encountered an internal execution error while processing this image.",
            internalDiagnosticMessage,
            innerException)
    {
    }
}

/// <summary>
/// Thrown when a user explicitly cancels an in-flight document extraction operation.
/// </summary>
public class ExtractionCancelledException : ScholarExtractionException
{
    public ExtractionCancelledException(string internalDiagnosticMessage = "Extraction cancelled by user.")
        : base(
            "ERR_EXTRACTION_CANCELLED",
            "Document extraction was cancelled.",
            internalDiagnosticMessage)
    {
    }
}
