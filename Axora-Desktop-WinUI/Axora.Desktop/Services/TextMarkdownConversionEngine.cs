using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Native text and Markdown conversion engine.
/// Executes high-speed, local-first conversion for Markdown (.md) to semantic HTML (.html)
/// and clean stripped plain text (.txt).
/// Operates with zero network calls and treats all markdown content as untrusted.
/// </summary>
public sealed class TextMarkdownConversionEngine : IConversionEngine
{
    private readonly ILogger<TextMarkdownConversionEngine> _logger;

    public string EngineId => "text-markdown";
    public string DisplayName => "Native Text & Markdown Engine";
    public bool IsAvailable => true;
    public string? RequiredDependencyId => null;

    public EngineResourceProfile ResourceProfile => EngineResourceProfile.CpuBoundDefault;

    public TextMarkdownConversionEngine(ILogger<TextMarkdownConversionEngine>? logger = null)
    {
        _logger = logger ?? NullLogger<TextMarkdownConversionEngine>.Instance;
    }

    /// <inheritdoc/>
    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        string src = NormalizeExtension(sourceExtension);
        string tgt = NormalizeExtension(targetExtension);

        bool isSourceMd = src is ".md" or ".markdown";
        bool isTargetValid = tgt is ".html" or ".htm" or ".txt";

        return isSourceMd && isTargetValid;
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertAsync(
        ConversionJob job,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        var stopwatch = Stopwatch.StartNew();

        if (ct.IsCancellationRequested || job.IsCancellationRequested)
        {
            return ConversionResult.Cancelled(stopwatch.Elapsed);
        }

        string srcExt = NormalizeExtension(job.SourceExtension);
        string tgtExt = NormalizeExtension(job.TargetExtension);

        if (!CanConvert(srcExt, tgtExt))
        {
            return ConversionResult.Failure(
                "ERR_FORMAT_NOT_SUPPORTED",
                $"Format conversion from '{srcExt}' to '{tgtExt}' is not supported by {DisplayName}.",
                $"Supported sources: .md, .markdown; supported targets: .html, .htm, .txt.");
        }

        // Validate source file existence
        if (string.IsNullOrWhiteSpace(job.SourceFilePath) || !File.Exists(job.SourceFilePath))
        {
            return ConversionResult.Failure(
                "ERR_INPUT_NOT_FOUND",
                "The source Markdown file does not exist or cannot be accessed.",
                $"Source file not found at: '{job.SourceFilePath}'");
        }

        // Validate source size
        var fileInfo = new FileInfo(job.SourceFilePath);
        if (fileInfo.Length == 0)
        {
            return ConversionResult.Failure(
                "ERR_INPUT_EMPTY",
                "The source Markdown file is 0 bytes and contains no content to convert.",
                $"File length is 0 bytes for: '{job.SourceFilePath}'");
        }

        // Invariant: Source cannot match output path
        if (string.Equals(
            Path.GetFullPath(job.SourceFilePath),
            Path.GetFullPath(string.IsNullOrWhiteSpace(job.OutputFilePath) ? job.SourceFilePath : job.OutputFilePath),
            StringComparison.OrdinalIgnoreCase))
        {
            return ConversionResult.Failure(
                "ERR_OUTPUT_SAME_AS_SOURCE",
                "The output file path cannot be identical to the source file path.",
                "Non-destructive file invariant prevented in-place overwrite.");
        }

        // Validate source is readable
        try
        {
            using var testStream = new FileStream(job.SourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (IOException ioEx)
        {
            return ConversionResult.Failure(
                "ERR_INPUT_READ_FAILED",
                "The source Markdown file is currently locked or in use by another process.",
                ioEx.Message);
        }
        catch (UnauthorizedAccessException authEx)
        {
            return ConversionResult.Failure(
                "ERR_INPUT_ACCESS_DENIED",
                "Access to the source Markdown file was denied by the operating system.",
                authEx.Message);
        }

        // Ensure output directory exists
        string outputDir = Path.GetDirectoryName(job.OutputFilePath) ?? string.Empty;
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                return ConversionResult.Failure(
                    "ERR_OUTPUT_WRITE_FAILED",
                    "Failed to create the destination directory.",
                    ex.Message);
            }
        }

        try
        {
            progress?.Report(10.0);

            // Read source Markdown with UTF-8 BOM awareness
            string markdownContent;
            using (var stream = new FileStream(job.SourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                markdownContent = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
            }

            if (ct.IsCancellationRequested || job.IsCancellationRequested)
            {
                return ConversionResult.Cancelled(stopwatch.Elapsed);
            }

            progress?.Report(40.0);

            string transformedContent;
            if (tgtExt is ".html" or ".htm")
            {
                transformedContent = MarkdownProcessor.ConvertToHtml(
                    markdownContent,
                    title: Path.GetFileNameWithoutExtension(job.SourceFilePath),
                    ct: ct);
            }
            else // .txt
            {
                transformedContent = MarkdownProcessor.ConvertToPlainText(markdownContent, ct);
            }

            if (ct.IsCancellationRequested || job.IsCancellationRequested)
            {
                return ConversionResult.Cancelled(stopwatch.Elapsed);
            }

            progress?.Report(80.0);

            // Write output as UTF-8 (without BOM)
            var utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            await File.WriteAllTextAsync(job.OutputFilePath, transformedContent, utf8WithoutBom, ct).ConfigureAwait(false);

            var outInfo = new FileInfo(job.OutputFilePath);
            stopwatch.Stop();
            progress?.Report(100.0);

            _logger.LogInformation(
                "Converted Markdown '{Source}' -> '{Target}' ({Bytes} bytes in {Elapsed}ms)",
                job.SourceFilePath, job.OutputFilePath, outInfo.Length, stopwatch.ElapsedMilliseconds);

            return ConversionResult.Success(job.OutputFilePath, outInfo.Length, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            TryCleanupFile(job.OutputFilePath);
            return ConversionResult.Cancelled(stopwatch.Elapsed);
        }
        catch (OutOfMemoryException oom)
        {
            TryCleanupFile(job.OutputFilePath);
            return ConversionResult.Failure(
                "ERR_INSUFFICIENT_MEMORY",
                "Insufficient memory to process this Markdown document.",
                oom.ToString());
        }
        catch (Exception ex)
        {
            TryCleanupFile(job.OutputFilePath);
            return ConversionResult.Failure(
                "ERR_OUTPUT_WRITE_FAILED",
                "An unexpected error occurred while converting the Markdown document.",
                ex.Message);
        }
    }

    private static string NormalizeExtension(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return string.Empty;
        var trimmed = ext.Trim().ToLowerInvariant();
        return trimmed.StartsWith('.') ? trimmed : $".{trimmed}";
    }

    private static void TryCleanupFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { /* best-effort cleanup */ }
    }
}
