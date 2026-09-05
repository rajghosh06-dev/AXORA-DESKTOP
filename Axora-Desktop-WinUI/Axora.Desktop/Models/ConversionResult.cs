using System;

namespace Axora.Desktop.Models;

/// <summary>
/// Structured outcome of a conversion operation.
/// Separates user-facing diagnostics from raw internal exception details.
/// </summary>
public sealed record ConversionResult
{
    public bool IsSuccess { get; init; }
    public ConversionJobStatus Status { get; init; }
    public string? OutputPath { get; init; }
    public long? OutputSizeBytes { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? DiagnosticDetails { get; init; }
    public TimeSpan ElapsedTime { get; init; } = TimeSpan.Zero;

    public static ConversionResult Success(string outputPath, long outputSizeBytes, TimeSpan elapsed) => new()
    {
        IsSuccess = true,
        Status = ConversionJobStatus.Completed,
        OutputPath = outputPath,
        OutputSizeBytes = outputSizeBytes,
        ElapsedTime = elapsed
    };

    public static ConversionResult Failure(string errorCode, string userMessage, string? diagnosticDetails = null, TimeSpan elapsed = default) => new()
    {
        IsSuccess = false,
        Status = ConversionJobStatus.Failed,
        ErrorCode = errorCode,
        ErrorMessage = userMessage,
        DiagnosticDetails = diagnosticDetails,
        ElapsedTime = elapsed
    };

    public static ConversionResult Cancelled(TimeSpan elapsed = default) => new()
    {
        IsSuccess = false,
        Status = ConversionJobStatus.Cancelled,
        ErrorMessage = "Operation was cancelled.",
        ElapsedTime = elapsed
    };

    public static ConversionResult Skipped(string reason, string? existingPath = null) => new()
    {
        IsSuccess = false,
        Status = ConversionJobStatus.Skipped,
        OutputPath = existingPath,
        ErrorMessage = reason
    };
}
