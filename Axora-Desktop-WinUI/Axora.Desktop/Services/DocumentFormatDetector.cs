using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Sniffs document formats and encodings via content byte inspection, magic signatures, BOMs, and structural markers.
/// Prohibits misclassifying binary, PDF, or ZIP/DOCX files as plain text.
/// </summary>
public sealed class DocumentFormatDetector : IDocumentFormatDetector
{
    private const int HeaderSampleSize = 4096;

    static DocumentFormatDetector()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch { }
    }

    public async Task<FormatDetectionResult> DetectFormatAsync(string filePath, CancellationToken ct = default)
    {
        return await DetectFormatAsync(filePath, null, ct);
    }

    public async Task<FormatDetectionResult> DetectFormatAsync(string filePath, ExtractionSecurityOptions? securityOptions, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Document file not found for format detection.", filePath);
        }

        var fileInfo = new FileInfo(filePath);
        if (securityOptions != null && fileInfo.Length > securityOptions.MaxFileSizeBytes)
        {
            throw new FileSizeLimitExceededException(fileInfo.Length, securityOptions.MaxFileSizeBytes);
        }

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
        return DetectFormat(stream, Path.GetFileName(filePath));
    }

    public FormatDetectionResult DetectFormatFromExtension(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".txt" => new FormatDetectionResult { Format = DetectedDocumentFormat.PlainText, MimeType = "text/plain", DetectedEncoding = Encoding.UTF8, SuggestedFileName = fileName },
            ".csv" => new FormatDetectionResult { Format = DetectedDocumentFormat.DelimitedText, MimeType = "text/csv", DetectedEncoding = Encoding.UTF8, SuggestedFileName = fileName },
            ".tsv" => new FormatDetectionResult { Format = DetectedDocumentFormat.DelimitedText, MimeType = "text/tab-separated-values", DetectedEncoding = Encoding.UTF8, SuggestedFileName = fileName },
            ".md" or ".markdown" => new FormatDetectionResult { Format = DetectedDocumentFormat.Markdown, MimeType = "text/markdown", DetectedEncoding = Encoding.UTF8, SuggestedFileName = fileName },
            ".html" or ".htm" => new FormatDetectionResult { Format = DetectedDocumentFormat.LocalHtml, MimeType = "text/html", DetectedEncoding = Encoding.UTF8, SuggestedFileName = fileName },
            ".pdf" => new FormatDetectionResult { Format = DetectedDocumentFormat.PdfDigital, MimeType = "application/pdf", SuggestedFileName = fileName },
            ".png" => new FormatDetectionResult { Format = DetectedDocumentFormat.RasterImage, MimeType = "image/png", RequiresOcr = true, SuggestedFileName = fileName },
            ".jpg" or ".jpeg" => new FormatDetectionResult { Format = DetectedDocumentFormat.RasterImage, MimeType = "image/jpeg", RequiresOcr = true, SuggestedFileName = fileName },
            ".bmp" => new FormatDetectionResult { Format = DetectedDocumentFormat.RasterImage, MimeType = "image/bmp", RequiresOcr = true, SuggestedFileName = fileName },
            ".webp" => new FormatDetectionResult { Format = DetectedDocumentFormat.RasterImage, MimeType = "image/webp", RequiresOcr = true, SuggestedFileName = fileName },
            ".tif" or ".tiff" => new FormatDetectionResult { Format = DetectedDocumentFormat.MultiPageTiff, MimeType = "image/tiff", RequiresOcr = true, SuggestedFileName = fileName },
            _ => new FormatDetectionResult { Format = DetectedDocumentFormat.Unknown, MimeType = "application/octet-stream", SuggestedFileName = fileName }
        };
    }

    public FormatDetectionResult DetectFormat(Stream stream, string? fileNameHint = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        long originalPosition = 0;
        bool canSeek = stream.CanSeek;
        if (canSeek)
        {
            originalPosition = stream.Position;
        }

        byte[] headerBuffer = new byte[HeaderSampleSize];
        int bytesRead = 0;

        try
        {
            bytesRead = ReadBlock(stream, headerBuffer, 0, headerBuffer.Length);
        }
        finally
        {
            if (canSeek)
            {
                stream.Position = originalPosition;
            }
        }

        long fileSizeBytes = canSeek ? stream.Length : bytesRead;
        string? ext = !string.IsNullOrWhiteSpace(fileNameHint) ? Path.GetExtension(fileNameHint).ToLowerInvariant() : null;

        // 1. Empty Stream Handling
        if (bytesRead == 0)
        {
            return DetectEmptyFormat(ext, fileSizeBytes, fileNameHint);
        }

        ReadOnlySpan<byte> span = headerBuffer.AsSpan(0, bytesRead);

        // 2. Binary Magic Bytes Checks
        // PDF: "%PDF-" (25 50 44 46)
        if (span.Length >= 4 && span[0] == 0x25 && span[1] == 0x50 && span[2] == 0x44 && span[3] == 0x46)
        {
            return new FormatDetectionResult
            {
                Format = DetectedDocumentFormat.PdfDigital,
                MimeType = "application/pdf",
                DetectedEncoding = null,
                RequiresOcr = false,
                FileSizeBytes = fileSizeBytes,
                SuggestedFileName = fileNameHint
            };
        }

        // ZIP Archive / DOCX: "PK\x03\x04" (50 4B 03 04)
        if (span.Length >= 4 && span[0] == 0x50 && span[1] == 0x4B && span[2] == 0x03 && span[3] == 0x04)
        {
            bool isDocx = ext == ".docx" || ContainsAsciiSequence(span, "[Content_Types].xml");
            return new FormatDetectionResult
            {
                Format = isDocx ? DetectedDocumentFormat.Docx : DetectedDocumentFormat.Unknown,
                MimeType = isDocx ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document" : "application/zip",
                DetectedEncoding = null,
                RequiresOcr = false,
                FileSizeBytes = fileSizeBytes,
                SuggestedFileName = fileNameHint
            };
        }

        // PNG Graphic: 89 50 4E 47 0D 0A 1A 0A
        if (span.Length >= 8 && span[0] == 0x89 && span[1] == 0x50 && span[2] == 0x4E && span[3] == 0x47 &&
            span[4] == 0x0D && span[5] == 0x0A && span[6] == 0x1A && span[7] == 0x0A)
        {
            return new FormatDetectionResult
            {
                Format = DetectedDocumentFormat.RasterImage,
                MimeType = "image/png",
                RequiresOcr = true,
                FileSizeBytes = fileSizeBytes,
                SuggestedFileName = fileNameHint
            };
        }

        // JPEG Graphic: FF D8 FF
        if (span.Length >= 3 && span[0] == 0xFF && span[1] == 0xD8 && span[2] == 0xFF)
        {
            return new FormatDetectionResult
            {
                Format = DetectedDocumentFormat.RasterImage,
                MimeType = "image/jpeg",
                RequiresOcr = true,
                FileSizeBytes = fileSizeBytes,
                SuggestedFileName = fileNameHint
            };
        }

        // BMP Graphic: 42 4D ('BM')
        if (span.Length >= 2 && span[0] == 0x42 && span[1] == 0x4D)
        {
            return new FormatDetectionResult
            {
                Format = DetectedDocumentFormat.RasterImage,
                MimeType = "image/bmp",
                RequiresOcr = true,
                FileSizeBytes = fileSizeBytes,
                SuggestedFileName = fileNameHint
            };
        }

        // WebP Graphic: 'RIFF' .... 'WEBP' (52 49 46 46 ... 57 45 42 50)
        if (span.Length >= 12 &&
            span[0] == 0x52 && span[1] == 0x49 && span[2] == 0x46 && span[3] == 0x46 &&
            span[8] == 0x57 && span[9] == 0x45 && span[10] == 0x42 && span[11] == 0x50)
        {
            return new FormatDetectionResult
            {
                Format = DetectedDocumentFormat.RasterImage,
                MimeType = "image/webp",
                RequiresOcr = true,
                FileSizeBytes = fileSizeBytes,
                SuggestedFileName = fileNameHint
            };
        }

        // TIFF Graphic: 'II*\0' (LE) or 'MM\0*' (BE)
        if (span.Length >= 4 &&
            ((span[0] == 0x49 && span[1] == 0x49 && span[2] == 0x2A && span[3] == 0x00) ||
             (span[0] == 0x4D && span[1] == 0x4D && span[2] == 0x00 && span[3] == 0x2A)))
        {
            return new FormatDetectionResult
            {
                Format = DetectedDocumentFormat.MultiPageTiff,
                MimeType = "image/tiff",
                RequiresOcr = true,
                FileSizeBytes = fileSizeBytes,
                SuggestedFileName = fileNameHint
            };
        }

        // Executable binaries (PE/MZ, ELF, Mach-O, Java Class)
        if (span.Length >= 2 && span[0] == 0x4D && span[1] == 0x5A) // MZ (Windows PE)
        {
            return new FormatDetectionResult
            {
                Format = DetectedDocumentFormat.Unknown,
                MimeType = "application/x-msdownload",
                RequiresOcr = false,
                FileSizeBytes = fileSizeBytes,
                SuggestedFileName = fileNameHint
            };
        }
        if (span.Length >= 4 &&
            ((span[0] == 0x7F && span[1] == 0x45 && span[2] == 0x4C && span[3] == 0x46) || // ELF
             (span[0] == 0xCA && span[1] == 0xFE && span[2] == 0xBA && span[3] == 0xBE)))   // Java class / Mach-O
        {
            return new FormatDetectionResult
            {
                Format = DetectedDocumentFormat.Unknown,
                MimeType = "application/octet-stream",
                RequiresOcr = false,
                FileSizeBytes = fileSizeBytes,
                SuggestedFileName = fileNameHint
            };
        }

        // 3. BOM Inspection
        Encoding? detectedEncoding = null;
        int bomLength = 0;

        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
        {
            detectedEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            bomLength = 3;
        }
        else if (span.Length >= 4 && span[0] == 0xFF && span[1] == 0xFE && span[2] == 0x00 && span[3] == 0x00)
        {
            detectedEncoding = Encoding.UTF32;
            bomLength = 4;
        }
        else if (span.Length >= 2 && span[0] == 0xFF && span[1] == 0xFE)
        {
            detectedEncoding = Encoding.Unicode; // UTF-16 LE
            bomLength = 2;
        }
        else if (span.Length >= 2 && span[0] == 0xFE && span[1] == 0xFF)
        {
            detectedEncoding = Encoding.BigEndianUnicode; // UTF-16 BE
            bomLength = 2;
        }

        // 4. Binary vs Text Heuristic Check (if no BOM)
        if (detectedEncoding == null)
        {
            if (IsBinaryContent(span))
            {
                return new FormatDetectionResult
                {
                    Format = DetectedDocumentFormat.Unknown,
                    MimeType = "application/octet-stream",
                    RequiresOcr = false,
                    FileSizeBytes = fileSizeBytes,
                    SuggestedFileName = fileNameHint
                };
            }

            // Test strict UTF-8
            if (IsValidUtf8(span))
            {
                detectedEncoding = Encoding.UTF8;
            }
            else
            {
                // Fallback to ANSI / Windows-1252 or Latin1
                try
                {
                    detectedEncoding = Encoding.GetEncoding(1252);
                }
                catch
                {
                    detectedEncoding = Encoding.Latin1;
                }
            }
        }

        // 5. Decode Sample Text to Distinguish HTML, Delimited, Markdown, PlainText
        string sampleText;
        try
        {
            sampleText = detectedEncoding.GetString(headerBuffer, bomLength, bytesRead - bomLength);
        }
        catch
        {
            sampleText = Encoding.ASCII.GetString(headerBuffer, bomLength, bytesRead - bomLength);
        }

        string trimmedSample = sampleText.TrimStart();

        // 5a. HTML Detection (Strict and conservative)
        if (IsHtmlSample(trimmedSample, ext))
        {
            return new FormatDetectionResult
            {
                Format = DetectedDocumentFormat.LocalHtml,
                MimeType = "text/html",
                DetectedEncoding = detectedEncoding,
                RequiresOcr = false,
                FileSizeBytes = fileSizeBytes,
                SuggestedFileName = fileNameHint
            };
        }

        // 5b. Delimited Text Detection (CSV / TSV)
        if (IsDelimitedSample(sampleText, ext))
        {
            string mime = ext == ".tsv" || (ext != ".csv" && sampleText.Contains('\t')) ? "text/tab-separated-values" : "text/csv";
            return new FormatDetectionResult
            {
                Format = DetectedDocumentFormat.DelimitedText,
                MimeType = mime,
                DetectedEncoding = detectedEncoding,
                RequiresOcr = false,
                FileSizeBytes = fileSizeBytes,
                SuggestedFileName = fileNameHint
            };
        }

        // 5c. Markdown Detection
        if (IsMarkdownSample(sampleText, ext))
        {
            return new FormatDetectionResult
            {
                Format = DetectedDocumentFormat.Markdown,
                MimeType = "text/markdown",
                DetectedEncoding = detectedEncoding,
                RequiresOcr = false,
                FileSizeBytes = fileSizeBytes,
                SuggestedFileName = fileNameHint
            };
        }

        // 5d. Plain Text Fallback
        return new FormatDetectionResult
        {
            Format = DetectedDocumentFormat.PlainText,
            MimeType = "text/plain",
            DetectedEncoding = detectedEncoding,
            RequiresOcr = false,
            FileSizeBytes = fileSizeBytes,
            SuggestedFileName = fileNameHint
        };
    }

    private static FormatDetectionResult DetectEmptyFormat(string? ext, long fileSizeBytes, string? fileNameHint)
    {
        DetectedDocumentFormat format = ext switch
        {
            ".txt" => DetectedDocumentFormat.PlainText,
            ".csv" or ".tsv" => DetectedDocumentFormat.DelimitedText,
            ".md" or ".markdown" => DetectedDocumentFormat.Markdown,
            ".html" or ".htm" => DetectedDocumentFormat.LocalHtml,
            ".pdf" => DetectedDocumentFormat.PdfDigital,
            ".docx" => DetectedDocumentFormat.Docx,
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" => DetectedDocumentFormat.RasterImage,
            ".tif" or ".tiff" => DetectedDocumentFormat.MultiPageTiff,
            _ => DetectedDocumentFormat.PlainText
        };

        string mime = format switch
        {
            DetectedDocumentFormat.DelimitedText => ext == ".tsv" ? "text/tab-separated-values" : "text/csv",
            DetectedDocumentFormat.Markdown => "text/markdown",
            DetectedDocumentFormat.LocalHtml => "text/html",
            DetectedDocumentFormat.PdfDigital => "application/pdf",
            DetectedDocumentFormat.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            DetectedDocumentFormat.MultiPageTiff => "image/tiff",
            DetectedDocumentFormat.RasterImage => ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "image/png"
            },
            _ => "text/plain"
        };

        return new FormatDetectionResult
        {
            Format = format,
            MimeType = mime,
            DetectedEncoding = Encoding.UTF8,
            RequiresOcr = false,
            FileSizeBytes = fileSizeBytes,
            SuggestedFileName = fileNameHint
        };
    }

    private static bool IsBinaryContent(ReadOnlySpan<byte> bytes)
    {
        int nullCount = 0;
        int controlCount = 0;

        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            if (b == 0x00)
            {
                nullCount++;
            }
            else if (b < 0x20 && b != 0x09 && b != 0x0A && b != 0x0D && b != 0x0C)
            {
                controlCount++;
            }
        }

        if (nullCount > 0) return true;
        if (bytes.Length > 0 && (double)controlCount / bytes.Length > 0.10) return true;
        return false;
    }

    private static bool IsValidUtf8(ReadOnlySpan<byte> bytes)
    {
        int i = 0;
        while (i < bytes.Length)
        {
            byte b = bytes[i++];
            if (b <= 0x7F) continue;

            int remaining;
            if ((b & 0xE0) == 0xC0) remaining = 1;
            else if ((b & 0xF0) == 0xE0) remaining = 2;
            else if ((b & 0xF8) == 0xF0) remaining = 3;
            else return false;

            if (i + remaining > bytes.Length) break;

            for (int j = 0; j < remaining; j++)
            {
                byte next = bytes[i++];
                if ((next & 0xC0) != 0x80) return false;
            }
        }
        return true;
    }

    private static bool IsHtmlSample(string trimmedSample, string? ext)
    {
        if (ext == ".html" || ext == ".htm") return true;

        if (trimmedSample.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
            trimmedSample.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmedSample.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && trimmedSample.Contains("<html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmedSample.StartsWith("<head", StringComparison.OrdinalIgnoreCase) ||
            trimmedSample.StartsWith("<body", StringComparison.OrdinalIgnoreCase) ||
            trimmedSample.StartsWith("<article", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsDelimitedSample(string sampleText, string? ext)
    {
        if (ext == ".csv" || ext == ".tsv") return true;

        var lines = sampleText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length >= 2)
        {
            char[] delimiters = new[] { ',', '\t', ';' };
            foreach (char d in delimiters)
            {
                int count1 = CountDelimiterOutsideQuotes(lines[0], d);
                int count2 = CountDelimiterOutsideQuotes(lines[1], d);
                if (count1 >= 1 && count1 == count2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CountDelimiterOutsideQuotes(string line, char delimiter)
    {
        int count = 0;
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (line[i] == delimiter && !inQuotes)
            {
                count++;
            }
        }
        return count;
    }

    private static bool IsMarkdownSample(string sampleText, string? ext)
    {
        if (ext == ".md" || ext == ".markdown") return true;

        var lines = sampleText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 0 && lines[0].Trim() == "---") return true;

        int mdScore = 0;
        foreach (var line in lines)
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("# ") || trimmed.StartsWith("## ") || trimmed.StartsWith("### ")) mdScore += 2;
            if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~")) mdScore += 3;
            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("> ")) mdScore += 1;
            if (trimmed.StartsWith("|") && trimmed.EndsWith("|")) mdScore += 2;
            if (trimmed.Contains("**") || trimmed.Contains('`')) mdScore += 1;
            if (mdScore >= 2) return true;
        }

        return false;
    }

    private static bool ContainsAsciiSequence(ReadOnlySpan<byte> span, string ascii)
    {
        ReadOnlySpan<byte> target = Encoding.ASCII.GetBytes(ascii);
        return span.IndexOf(target) >= 0;
    }

    private static int ReadBlock(Stream stream, byte[] buffer, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = stream.Read(buffer, offset + total, count - total);
            if (read == 0) break;
            total += read;
        }
        return total;
    }
}
