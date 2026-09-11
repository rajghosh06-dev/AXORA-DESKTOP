using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services;

/// <summary>
/// Writes dual-file vector indexes (index_manifest.json and vectors.bin) using staged
/// same-volume replacement semantics and Windows NTFS file-lock resilience.
/// Strictly isolates ground-truth source documents and enforces path safety.
/// </summary>
public sealed class ScholarVectorIndexWriter
{
    private static readonly byte[] MagicBytes = Encoding.ASCII.GetBytes("AXORAVEC"); // 8 bytes
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _indexBaseDirectory;
    private readonly string _documentGroundTruthDirectory;
    private readonly ILogger<ScholarVectorIndexWriter> _logger;

    public ScholarVectorIndexWriter(
        string? indexBaseDirectory = null,
        string? documentGroundTruthDirectory = null,
        ILogger<ScholarVectorIndexWriter>? logger = null)
    {
        _logger = logger ?? NullLogger<ScholarVectorIndexWriter>.Instance;

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _indexBaseDirectory = indexBaseDirectory ?? Path.Combine(appData, "Axora", "Scholar", "indexes");
        _documentGroundTruthDirectory = documentGroundTruthDirectory ?? Path.Combine(appData, "Axora", "Scholar", "documents");

        Directory.CreateDirectory(_indexBaseDirectory);
    }

    /// <summary>
    /// Writes the index manifest and binary vector payload to disk using staged same-volume replacement.
    /// </summary>
    public async Task WriteIndexAsync(
        IndexManifest manifest,
        IReadOnlyList<float[]> vectors,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(vectors);
        ct.ThrowIfCancellationRequested();

        SanitizeDocumentId(manifest.DocumentId);

        string targetDirectory = GetIndexDirectory(manifest.DocumentId);

        // Invariant INV-W3D-10: Source Document Isolation
        VerifySourceDocumentIsolation(targetDirectory);

        Directory.CreateDirectory(targetDirectory);

        string targetBinPath = Path.Combine(targetDirectory, "vectors.bin");
        string targetManifestPath = Path.Combine(targetDirectory, "index_manifest.json");

        string tempBinPath = Path.Combine(targetDirectory, $"vectors.bin.{Guid.NewGuid():N}.tmp");
        string tempManifestPath = Path.Combine(targetDirectory, $"index_manifest.json.{Guid.NewGuid():N}.tmp");

        try
        {
            // 1. Stream binary vectors to temp file and compute SHA-256 checksum
            string payloadChecksumHex = await WriteBinaryPayloadAsync(tempBinPath, manifest, vectors, ct);

            manifest.BinaryPayloadHash = payloadChecksumHex;
            manifest.TotalRecords = vectors.Count;
            manifest.LastUpdatedAtUtc = DateTime.UtcNow;

            // 2. Write JSON manifest to temp file
            string manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
            await File.WriteAllTextAsync(tempManifestPath, manifestJson, Encoding.UTF8, ct);

            // 3. Staged replacement with exponential backoff retry for NTFS locks (INV-W3D-08, INV-W3D-31)
            await SafeStagedMoveAsync(tempBinPath, targetBinPath, ct);
            await SafeStagedMoveAsync(tempManifestPath, targetManifestPath, ct);

            _logger.LogInformation("Successfully persisted vector index for document '{DocId}'. Records: {Count}",
                manifest.DocumentId, vectors.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist vector index for document '{DocId}'. Cleaning temp files.", manifest.DocumentId);

            TryDeleteFile(tempBinPath);
            TryDeleteFile(tempManifestPath);

            throw new IOException($"Failed to persist vector index for document '{manifest.DocumentId}'. [Code: ERR_INDEX_SAVE_FAILED]", ex);
        }
    }

    private static async Task<string> WriteBinaryPayloadAsync(
        string filePath,
        IndexManifest manifest,
        IReadOnlyList<float[]> vectors,
        CancellationToken ct)
    {
        int recordCount = vectors.Count;
        int dimension = manifest.VectorDimension;
        long totalExpectedBytes = 64L + ((long)recordCount * dimension * 4L);

        byte[] payloadHash;

        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 65536, useAsync: true))
        {
            // Pre-allocate file size on NTFS to prevent fragmentation
            fs.SetLength(totalExpectedBytes);

            // Write preliminary 64-byte header (payload SHA-256 zeroed)
            using (var bw = new BinaryWriter(fs, Encoding.ASCII, leaveOpen: true))
            {
                bw.Write(MagicBytes);                         // 0..7 (8 bytes)
                bw.Write((ushort)manifest.SchemaVersion);     // 8..9 (2 bytes)
                bw.Write((ushort)dimension);                  // 10..11 (2 bytes)
                bw.Write((uint)recordCount);                  // 12..15 (4 bytes)
                bw.Write(new byte[16]);                       // 16..31 (16 bytes padding)
                bw.Write(new byte[32]);                       // 32..63 (32 bytes placeholder for hash)
            }

            // Write vectors and calculate SHA-256 of payload concurrently
            using (var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var floatBuffer = new byte[dimension * 4];

                for (int r = 0; r < recordCount; r++)
                {
                    ct.ThrowIfCancellationRequested();
                    var vec = vectors[r];

                    if (vec.Length != dimension)
                    {
                        throw new InvalidOperationException(
                            $"Vector at index {r} has dimension {vec.Length}, expected {dimension}. [Code: ERR_SIMD_DIMENSION_MISMATCH]");
                    }

                    Buffer.BlockCopy(vec, 0, floatBuffer, 0, floatBuffer.Length);
                    await fs.WriteAsync(floatBuffer.AsMemory(0, floatBuffer.Length), ct);
                    sha256.AppendData(floatBuffer);
                }

                await fs.FlushAsync(ct);
                payloadHash = sha256.GetHashAndReset();
            }

            // Seek back to header byte 32 and overwrite payload SHA-256 checksum
            fs.Seek(32, SeekOrigin.Begin);
            await fs.WriteAsync(payloadHash.AsMemory(0, 32), ct);
            await fs.FlushAsync(ct);
        }

        return Convert.ToHexString(payloadHash).ToLowerInvariant();
    }

    private static async Task SafeStagedMoveAsync(string sourcePath, string destinationPath, CancellationToken ct)
    {
        int[] delays = [50, 100, 200];
        for (int attempt = 0; attempt <= delays.Length; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                File.Move(sourcePath, destinationPath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < delays.Length)
            {
                await Task.Delay(delays[attempt], ct);
            }
        }
    }

    public string GetIndexDirectory(string documentId)
    {
        SanitizeDocumentId(documentId);
        return Path.Combine(_indexBaseDirectory, documentId);
    }

    private void VerifySourceDocumentIsolation(string targetDirectory)
    {
        string normalizedTarget = Path.GetFullPath(targetDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedGroundTruth = Path.GetFullPath(_documentGroundTruthDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (normalizedTarget.Equals(normalizedGroundTruth, StringComparison.OrdinalIgnoreCase) ||
            normalizedTarget.StartsWith(normalizedGroundTruth + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Security violation: Index directory '{targetDirectory}' collides with protected user document ground truth. " +
                $"[Code: ERR_SOURCE_DOCUMENT_VIOLATION]");
        }
    }

    private static void SanitizeDocumentId(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("DocumentId cannot be null or whitespace. [Code: ERR_INVALID_DOCUMENT_ID]", nameof(documentId));
        }

        if (documentId.Contains("..", StringComparison.Ordinal) ||
            documentId.Contains('/', StringComparison.Ordinal) ||
            documentId.Contains('\\', StringComparison.Ordinal) ||
            documentId.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"DocumentId '{documentId}' contains path traversal or separator characters. [Code: ERR_PATH_TRAVERSAL_DETECTED]",
                nameof(documentId));
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (documentId.IndexOfAny(invalidChars) >= 0)
        {
            throw new ArgumentException(
                $"DocumentId '{documentId}' contains invalid filename characters. [Code: ERR_INVALID_DOCUMENT_ID]",
                nameof(documentId));
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch { }
    }
}
