using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
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
/// Reads and validates dual-file vector indices with SHA-256 payload integrity checks,
/// automatic quarantine of corrupt indices, and dual-mode in-memory/MemoryMappedFile access.
/// </summary>
public sealed class ScholarVectorIndexReader : IDisposable
{
    private static readonly byte[] MagicBytes = Encoding.ASCII.GetBytes("AXORAVEC");

    private readonly string _indexBaseDirectory;
    private readonly string _quarantineBaseDirectory;
    private readonly ILogger<ScholarVectorIndexReader> _logger;

    private readonly Dictionary<string, MemoryMappedFile> _activeMappedFiles = new(StringComparer.Ordinal);
    private readonly object _mappedFileLock = new();
    private bool _disposed;

    /// <summary>
    /// Threshold record count above which MemoryMappedFile is used instead of in-memory array.
    /// Defaults to 5,000 in accordance with INV-W3D-31; configurable for unit testing.
    /// </summary>
    public int MemoryMappedThreshold { get; set; } = 5000;

    public ScholarVectorIndexReader(
        string? indexBaseDirectory = null,
        string? quarantineBaseDirectory = null,
        ILogger<ScholarVectorIndexReader>? logger = null)
    {
        _logger = logger ?? NullLogger<ScholarVectorIndexReader>.Instance;

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _indexBaseDirectory = indexBaseDirectory ?? Path.Combine(appData, "Axora", "Scholar", "indexes");
        _quarantineBaseDirectory = quarantineBaseDirectory ?? Path.Combine(appData, "Axora", "Scholar", "quarantine");

        Directory.CreateDirectory(_indexBaseDirectory);
    }

    /// <summary>
    /// Evaluates the integrity and validity of an index on disk without necessarily hydrating full vectors into RAM.
    /// In case of payload corruption or truncation, automatically quarantines the directory.
    /// </summary>
    public async Task<IndexValidationStatus> ValidateIndexAsync(
        string documentId,
        string? expectedSourceHash = null,
        string? expectedModelFingerprint = null,
        int expectedDimension = 384,
        CancellationToken ct = default)
    {
        SanitizeDocumentId(documentId);
        ct.ThrowIfCancellationRequested();

        string dir = Path.Combine(_indexBaseDirectory, documentId);
        if (!Directory.Exists(dir))
        {
            return IndexValidationStatus.Missing;
        }

        string manifestPath = Path.Combine(dir, "index_manifest.json");
        string binPath = Path.Combine(dir, "vectors.bin");

        if (!File.Exists(manifestPath) || !File.Exists(binPath))
        {
            return IndexValidationStatus.Missing;
        }

        IndexManifest manifest;
        try
        {
            string json = await File.ReadAllTextAsync(manifestPath, Encoding.UTF8, ct);
            manifest = JsonSerializer.Deserialize<IndexManifest>(json)
                ?? throw new InvalidDataException("Manifest deserialized to null.");
        }
        catch
        {
            QuarantineDirectory(documentId, "corrupt_manifest_json");
            return IndexValidationStatus.Corrupt_ChecksumMismatch;
        }

        // 1. Invariant INV-W3D-15: Future schema version refusal
        if (manifest.SchemaVersion > 1)
        {
            _logger.LogWarning("Index manifest schema version {Ver} is newer than supported v1. Refusing load.", manifest.SchemaVersion);
            return IndexValidationStatus.Unsupported_FutureSchema;
        }

        // 2. Dimension validation
        if (manifest.VectorDimension != expectedDimension)
        {
            return IndexValidationStatus.Stale_DimensionMismatch;
        }

        // 3. Model fingerprint validation
        if (!string.IsNullOrEmpty(expectedModelFingerprint) &&
            !string.Equals(manifest.ModelFingerprint, expectedModelFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return IndexValidationStatus.Stale_ModelMismatch;
        }

        // 4. Source document modification validation
        if (!string.IsNullOrEmpty(expectedSourceHash) &&
            !string.Equals(manifest.SourceHash, expectedSourceHash, StringComparison.OrdinalIgnoreCase))
        {
            return IndexValidationStatus.Stale_DocumentModified;
        }

        // 5. Binary file layout and checksum validation (INV-W3D-09, INV-W3D-11)
        var fileInfo = new FileInfo(binPath);
        long expectedLength = 64L + ((long)manifest.TotalRecords * manifest.VectorDimension * 4L);

        if (fileInfo.Length != expectedLength)
        {
            _logger.LogWarning("Binary vector file length {Actual} != expected {Expected}. Quarantining.",
                fileInfo.Length, expectedLength);
            QuarantineDirectory(documentId, "truncated_binary");
            return IndexValidationStatus.Corrupt_TruncatedBinary;
        }

        // 6. Header and SHA-256 Checksum verification
        string? quarantineReason = null;
        IndexValidationStatus errorStatus = IndexValidationStatus.Valid;

        try
        {
            using (var fs = new FileStream(binPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true))
            using (var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: true))
            {
                byte[] magic = br.ReadBytes(8);
                if (!magic.SequenceEqual(MagicBytes))
                {
                    quarantineReason = "invalid_magic_header";
                    errorStatus = IndexValidationStatus.Corrupt_ChecksumMismatch;
                }
                else
                {
                    ushort version = br.ReadUInt16();
                    ushort dim = br.ReadUInt16();
                    uint count = br.ReadUInt32();
                    br.ReadBytes(16); // padding
                    byte[] recordedPayloadHash = br.ReadBytes(32);

                    if (dim != manifest.VectorDimension || count != manifest.TotalRecords)
                    {
                        quarantineReason = "header_metadata_mismatch";
                        errorStatus = IndexValidationStatus.Corrupt_ChecksumMismatch;
                    }
                    else
                    {
                        // Verify payload SHA-256 hash
                        fs.Seek(64, SeekOrigin.Begin);
                        byte[] computedHash = await SHA256.HashDataAsync(fs, ct);

                        if (!computedHash.SequenceEqual(recordedPayloadHash))
                        {
                            quarantineReason = "payload_checksum_mismatch";
                            errorStatus = IndexValidationStatus.Corrupt_ChecksumMismatch;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception reading vectors.bin during validation for '{DocId}'. Quarantining.", documentId);
            quarantineReason = "io_error";
            errorStatus = IndexValidationStatus.Corrupt_ChecksumMismatch;
        }

        if (quarantineReason != null)
        {
            _logger.LogWarning("Corrupt index detected ({Reason}) for document '{DocId}'. Quarantining.", quarantineReason, documentId);
            QuarantineDirectory(documentId, quarantineReason);
            return errorStatus;
        }

        return IndexValidationStatus.Valid;
    }

    /// <summary>
    /// Loads an index into memory. Uses in-memory float[][] array for <= 5,000 vectors
    /// and MemoryMappedFile for > 5,000 vectors.
    /// </summary>
    public async Task<ScholarVectorIndex?> LoadIndexAsync(
        string documentId,
        CancellationToken ct = default)
    {
        SanitizeDocumentId(documentId);
        ct.ThrowIfCancellationRequested();

        var status = await ValidateIndexAsync(documentId, ct: ct);
        if (status != IndexValidationStatus.Valid)
        {
            return null;
        }

        string dir = Path.Combine(_indexBaseDirectory, documentId);
        string manifestPath = Path.Combine(dir, "index_manifest.json");
        string binPath = Path.Combine(dir, "vectors.bin");

        string json = await File.ReadAllTextAsync(manifestPath, Encoding.UTF8, ct);
        var manifest = JsonSerializer.Deserialize<IndexManifest>(json)!;

        int recordCount = manifest.TotalRecords;
        int dimension = manifest.VectorDimension;

        var index = new ScholarVectorIndex
        {
            Manifest = manifest,
            DocumentLengths = new int[recordCount]
        };

        // Dual-mode memory policy (INV-W3D-31)
        if (recordCount <= MemoryMappedThreshold)
        {
            // Load into managed memory array; closes file handle immediately, holding 0 locks.
            var vectors = new float[recordCount][];
            using (var fs = new FileStream(binPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true))
            {
                fs.Seek(64, SeekOrigin.Begin);
                var byteBuffer = new byte[dimension * 4];

                for (int r = 0; r < recordCount; r++)
                {
                    ct.ThrowIfCancellationRequested();
                    await fs.ReadExactlyAsync(byteBuffer, 0, byteBuffer.Length, ct);
                    var vec = new float[dimension];
                    Buffer.BlockCopy(byteBuffer, 0, vec, 0, byteBuffer.Length);
                    vectors[r] = vec;
                }
            }
            index.Vectors = vectors;
        }
        else
        {
            // Use MemoryMappedFile for large indexes (> MemoryMappedThreshold vectors)
            // Hydrate in-memory vectors from mapped file for search evaluation, then immediately dispose accessor and MMF (AUD2-W3D-04).
            var vectors = new float[recordCount][];
            using (var mmf = MemoryMappedFile.CreateFromFile(binPath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read))
            using (var accessor = mmf.CreateViewAccessor(64, (long)recordCount * dimension * 4, MemoryMappedFileAccess.Read))
            {
                for (int r = 0; r < recordCount; r++)
                {
                    ct.ThrowIfCancellationRequested();
                    var vec = new float[dimension];
                    accessor.ReadArray((long)r * dimension * 4, vec, 0, dimension);
                    vectors[r] = vec;
                }
            }
            index.Vectors = vectors;
        }

        // Synthesize context windows from manifest records to preserve lexical search and text snippets (AUD-W3D-05, AUD2-W3D-06)
        if (manifest.Records != null && manifest.Records.Count > 0)
        {
            var windows = new List<BoundedContextWindow>(manifest.Records.Count);
            foreach (var r in manifest.Records)
            {
                var focal = r.FocalChunkIndex >= 0 ? new DocumentPassageChunk
                {
                    DocumentId = r.DocumentId,
                    PageNumber = r.PageNumber,
                    ChunkIndex = r.FocalChunkIndex,
                    Text = r.FormattedText
                } : null;

                windows.Add(new BoundedContextWindow
                {
                    WindowId = r.WindowId,
                    DocumentId = r.DocumentId,
                    PageNumber = r.PageNumber,
                    FormattedText = r.FormattedText,
                    FocalChunk = focal,
                    ConstituentChunkIndices = r.ConstituentChunkIndices ?? [],
                    Citations = r.Citations ?? []
                });
            }
            index.Windows = windows;
            RebuildLexicalIndex(index);
        }

        return index;
    }

    /// <summary>
    /// Coordinates safe disposal of active MemoryMappedFile handles for a document
    /// before staged same-volume replacement (INV-W3D-31).
    /// </summary>
    public void DisposeActiveMapping(string documentId)
    {
        lock (_mappedFileLock)
        {
            if (_activeMappedFiles.TryGetValue(documentId, out var mmf))
            {
                mmf.Dispose();
                _activeMappedFiles.Remove(documentId);
                _logger.LogInformation("Disposed active MemoryMappedFile handle for document '{DocId}'.", documentId);
            }
        }
    }

    public void QuarantineDirectory(string documentId, string reason)
    {
        try
        {
            DisposeActiveMapping(documentId);

            string sourceDir = Path.Combine(_indexBaseDirectory, documentId);
            if (!Directory.Exists(sourceDir)) return;

            Directory.CreateDirectory(_quarantineBaseDirectory);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string destDir = Path.Combine(_quarantineBaseDirectory, $"indexes_{timestamp}_{documentId}_{reason}");

            Directory.Move(sourceDir, destDir);
            _logger.LogWarning("Quarantined corrupt index directory '{DocId}' to '{Dest}'. Reason: {Reason} [Code: WARN_INDEX_QUARANTINED]",
                documentId, destDir, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to quarantine index directory for '{DocId}'.", documentId);
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

    private static void RebuildLexicalIndex(ScholarVectorIndex index)
    {
        if (index.Windows == null || index.Windows.Count == 0) return;

        int recordCount = index.Windows.Count;
        var invertedIndex = new Dictionary<string, List<(int RecordIndex, int TermFreq)>>(StringComparer.OrdinalIgnoreCase);
        var docLengths = new int[recordCount];
        long totalTerms = 0;

        for (int i = 0; i < recordCount; i++)
        {
            var terms = TokenizeTerms(index.Windows[i].FormattedText);
            docLengths[i] = terms.Count;
            totalTerms += terms.Count;

            var termFreqs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var term in terms)
            {
                termFreqs[term] = termFreqs.TryGetValue(term, out int f) ? f + 1 : 1;
            }

            foreach (var kvp in termFreqs)
            {
                if (!invertedIndex.TryGetValue(kvp.Key, out var list))
                {
                    list = [];
                    invertedIndex[kvp.Key] = list;
                }
                list.Add((i, kvp.Value));
            }
        }

        index.LexicalInvertedIndex = invertedIndex;
        index.DocumentLengths = docLengths;
        index.AverageDocumentLength = recordCount > 0 ? (double)totalTerms / recordCount : 0.0;
    }

    private static List<string> TokenizeTerms(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var words = text.Split(
            [' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '-', '_', '/', '\\'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var terms = new List<string>(words.Length);
        foreach (var word in words)
        {
            if (word.Length >= 2 && word.Length <= 64)
            {
                terms.Add(word.ToLowerInvariant());
            }
        }
        return terms;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_mappedFileLock)
            {
                foreach (var mmf in _activeMappedFiles.Values)
                {
                    mmf.Dispose();
                }
                _activeMappedFiles.Clear();
            }
            _disposed = true;
        }
    }
}
