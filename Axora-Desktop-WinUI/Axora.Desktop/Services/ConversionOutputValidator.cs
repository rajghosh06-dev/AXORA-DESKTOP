using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Axora.Desktop.Services;

/// <summary>
/// Validates staged conversion outputs before atomic commit to final destination.
/// Performs existence, size, extension, and magic-byte structural verification.
/// </summary>
public static class ConversionOutputValidator
{
    public sealed record ValidationOutcome
    {
        public bool IsValid { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }

        public static ValidationOutcome Success() => new() { IsValid = true };
        public static ValidationOutcome Failure(string errorCode, string message) =>
            new() { IsValid = false, ErrorCode = errorCode, ErrorMessage = message };
    }

    /// <summary>
    /// Validates that the staged output file is valid according to format expectations.
    /// </summary>
    public static async Task<ValidationOutcome> ValidateStagedOutputAsync(
        string stagingPath,
        string expectedTargetExtension,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(stagingPath) || !File.Exists(stagingPath))
        {
            return ValidationOutcome.Failure(
                "ERR_OUTPUT_NOT_FOUND",
                $"Staged output file was not found at '{stagingPath}'.");
        }

        var fi = new FileInfo(stagingPath);
        if (fi.Length == 0)
        {
            return ValidationOutcome.Failure(
                "ERR_OUTPUT_EMPTY",
                $"Staged output file '{Path.GetFileName(stagingPath)}' is 0 bytes.");
        }

        var ext = expectedTargetExtension.Trim().ToLowerInvariant();
        if (!ext.StartsWith('.')) ext = "." + ext;

        try
        {
            byte[] header = new byte[Math.Min(fi.Length, 32)];
            using (var fs = new FileStream(stagingPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int read = await fs.ReadAsync(header.AsMemory(0, header.Length), ct);
                if (read < 2)
                {
                    return ValidationOutcome.Failure(
                        "ERR_OUTPUT_VALIDATION_FAILED",
                        "Staged output file contains insufficient data for header verification.");
                }
            }

            switch (ext)
            {
                case ".pdf":
                    // PDF magic bytes: %PDF- (0x25, 0x50, 0x44, 0x46)
                    if (header.Length >= 4 &&
                        header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
                    {
                        return ValidationOutcome.Success();
                    }
                    return ValidationOutcome.Failure("ERR_OUTPUT_VALIDATION_FAILED", "Generated PDF has an invalid magic header.");

                case ".png":
                    // PNG magic bytes: \x89PNG\r\n\x1a\n
                    if (header.Length >= 8 &&
                        header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                        header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
                    {
                        return ValidationOutcome.Success();
                    }
                    return ValidationOutcome.Failure("ERR_OUTPUT_VALIDATION_FAILED", "Generated PNG has an invalid signature.");

                case ".jpg":
                case ".jpeg":
                    // JPEG SOI marker: 0xFF, 0xD8
                    if (header.Length >= 2 && header[0] == 0xFF && header[1] == 0xD8)
                    {
                        return ValidationOutcome.Success();
                    }
                    return ValidationOutcome.Failure("ERR_OUTPUT_VALIDATION_FAILED", "Generated JPEG has an invalid SOI marker.");

                case ".webp":
                    // RIFF....WEBP
                    if (header.Length >= 12 &&
                        header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                        header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                    {
                        return ValidationOutcome.Success();
                    }
                    return ValidationOutcome.Failure("ERR_OUTPUT_VALIDATION_FAILED", "Generated WebP has an invalid RIFF/WEBP header.");

                case ".bmp":
                    // BM marker: 0x42, 0x4D
                    if (header.Length >= 2 && header[0] == 0x42 && header[1] == 0x4D)
                    {
                        return ValidationOutcome.Success();
                    }
                    return ValidationOutcome.Failure("ERR_OUTPUT_VALIDATION_FAILED", "Generated BMP has an invalid BM header.");

                case ".tiff":
                case ".tif":
                    // II (0x49, 0x49) or MM (0x4D, 0x4D)
                    if (header.Length >= 2 &&
                        ((header[0] == 0x49 && header[1] == 0x49) || (header[0] == 0x4D && header[1] == 0x4D)))
                    {
                        return ValidationOutcome.Success();
                    }
                    return ValidationOutcome.Failure("ERR_OUTPUT_VALIDATION_FAILED", "Generated TIFF has an invalid header.");

                case ".html":
                case ".htm":
                    string textPreview = Encoding.UTF8.GetString(header);
                    if (textPreview.Contains('<'))
                    {
                        return ValidationOutcome.Success();
                    }
                    return ValidationOutcome.Failure("ERR_OUTPUT_VALIDATION_FAILED", "Generated HTML does not contain markup tags.");

                case ".txt":
                case ".md":
                case ".csv":
                case ".json":
                    // Plain text format: non-zero size verified
                    return ValidationOutcome.Success();

                default:
                    // General format: verified non-zero file
                    return ValidationOutcome.Success();
            }
        }
        catch (Exception ex)
        {
            return ValidationOutcome.Failure(
                "ERR_OUTPUT_VALIDATION_FAILED",
                $"Output validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Computes the SHA-256 hash of a file for immutability assertions.
    /// </summary>
    public static async Task<string> ComputeFileSha256Async(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return string.Empty;
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash);
    }
}
