using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Core local persistence service for Scholar Kit.
/// Enforces local-first, offline persistence under %APPDATA%\Axora\Scholar\ using staged
/// same-volume replacement semantics, path-traversal protection, corrupt file quarantine,
/// and absolute protection of user source documents.
/// </summary>
public sealed class ScholarLibraryService : IScholarLibraryService
{
    private readonly ILogger<ScholarLibraryService>? _logger;
    private readonly IScholarPersistenceMigrator _migrator;
    private readonly JsonSerializerOptions _jsonOptions;

    public string ScholarDataDirectory { get; }
    public string DocumentsDirectory { get; }
    public string SessionsDirectory { get; }
    public string IndexesDirectory { get; }
    public string MetadataDirectory { get; }
    public string QuarantineDirectory { get; }

    public Exception? LastPersistenceError { get; private set; }

    public ScholarLibraryService(
        ILogger<ScholarLibraryService>? logger = null,
        IScholarPersistenceMigrator? migrator = null,
        string? customRootDirectory = null)
    {
        _logger = logger;
        _migrator = migrator ?? new ScholarPersistenceMigrator();

        string baseDir = !string.IsNullOrWhiteSpace(customRootDirectory)
            ? customRootDirectory
            : Path.Combine(
                Environment.GetEnvironmentVariable("APPDATA") ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Axora");

        if (string.IsNullOrWhiteSpace(baseDir))
        {
            baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Axora");
        }

        ScholarDataDirectory = Path.Combine(baseDir, "Scholar");
        DocumentsDirectory = Path.Combine(ScholarDataDirectory, "documents");
        SessionsDirectory = Path.Combine(ScholarDataDirectory, "sessions");
        IndexesDirectory = Path.Combine(ScholarDataDirectory, "indexes");
        MetadataDirectory = Path.Combine(ScholarDataDirectory, "metadata");
        QuarantineDirectory = Path.Combine(ScholarDataDirectory, "quarantine");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        EnsureDirectories();
    }

    private void EnsureDirectories()
    {
        try
        {
            Directory.CreateDirectory(ScholarDataDirectory);
            Directory.CreateDirectory(DocumentsDirectory);
            Directory.CreateDirectory(SessionsDirectory);
            Directory.CreateDirectory(IndexesDirectory);
            Directory.CreateDirectory(MetadataDirectory);
            Directory.CreateDirectory(QuarantineDirectory);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Scholar persistence directories under {Root}", ScholarDataDirectory);
            LastPersistenceError = ex;
        }
    }

    // ── Document Persistence ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ScholarDocument> SaveDocumentAsync(ScholarDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateIdentifier(document.DocumentId, nameof(document.DocumentId));

        EnsureDirectories();
        var targetPath = Path.Combine(DocumentsDirectory, $"{document.DocumentId}.json");
        ValidateSafePath(targetPath, DocumentsDirectory);

        // Preserve original CreatedAt if updating an existing record
        if (File.Exists(targetPath))
        {
            try
            {
                var existing = await ReadAndValidateJsonFileAsync<ScholarDocument>(targetPath, ct);
                if (existing != null && existing.CreatedAt != default)
                {
                    document.CreatedAt = existing.CreatedAt;
                }
            }
            catch
            {
                // Preserve current document.CreatedAt if existing is unreadable
            }
        }
        else if (document.CreatedAt == default)
        {
            document.CreatedAt = DateTime.UtcNow;
        }

        // Calculate source hash if source file is currently available and not yet hashed
        if (string.IsNullOrWhiteSpace(document.SourceHash) && !string.IsNullOrWhiteSpace(document.SourcePath))
        {
            try
            {
                if (File.Exists(document.SourcePath))
                {
                    await using var sourceStream = new FileStream(document.SourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var hashBytes = await SHA256.HashDataAsync(sourceStream, ct);
                    document.SourceHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Could not calculate source hash for {Path}", document.SourcePath);
            }
        }

        document.SchemaVersion = ScholarPersistenceConstants.CurrentSchemaVersion;

        await WriteStagedJsonFileAsync(targetPath, document, ct);
        _logger?.LogInformation("Persisted ScholarDocument record {DocumentId} ({Pages} pages).", document.DocumentId, document.Pages.Count);
        return document;
    }

    /// <inheritdoc/>
    public async Task<ScholarDocument?> GetDocumentAsync(string documentId, CancellationToken ct = default)
    {
        ValidateIdentifier(documentId, nameof(documentId));
        var targetPath = Path.Combine(DocumentsDirectory, $"{documentId}.json");
        ValidateSafePath(targetPath, DocumentsDirectory);

        return await ReadAndValidateJsonFileAsync<ScholarDocument>(targetPath, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ScholarDocument>> GetAllDocumentsAsync(CancellationToken ct = default)
    {
        EnsureDirectories();
        var files = Directory.GetFiles(DocumentsDirectory, "*.json");
        var documents = new List<ScholarDocument>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var doc = await ReadAndValidateJsonFileAsync<ScholarDocument>(file, ct);
                if (doc != null)
                {
                    documents.Add(doc);
                }
            }
            catch (UnsupportedSchemaVersionException usvEx)
            {
                _logger?.LogWarning("Skipping document '{File}': {Message}", file, usvEx.Message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error loading document from '{File}'.", file);
            }
        }

        return documents.OrderByDescending(d => d.CreatedAt).ToList();
    }

    /// <inheritdoc/>
    public Task<bool> DeleteDocumentAsync(string documentId, CancellationToken ct = default)
    {
        ValidateIdentifier(documentId, nameof(documentId));
        var targetPath = Path.Combine(DocumentsDirectory, $"{documentId}.json");
        ValidateSafePath(targetPath, DocumentsDirectory);

        if (File.Exists(targetPath))
        {
            try
            {
                File.Delete(targetPath);
                _logger?.LogInformation("Deleted ScholarDocument record {DocumentId}. Original source file remains untouched.", documentId);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to delete document record {DocumentId}.", documentId);
                LastPersistenceError = ex;
                throw;
            }
        }

        return Task.FromResult(false);
    }

    // ── Study Session Persistence ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<StudySession> SaveSessionAsync(StudySession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ValidateIdentifier(session.SessionId, nameof(session.SessionId));

        EnsureDirectories();
        var targetPath = Path.Combine(SessionsDirectory, $"{session.SessionId}.json");
        ValidateSafePath(targetPath, SessionsDirectory);

        // Preserve original CreatedAt if updating an existing record
        if (File.Exists(targetPath))
        {
            try
            {
                var existing = await ReadAndValidateJsonFileAsync<StudySession>(targetPath, ct);
                if (existing != null && existing.CreatedAt != default)
                {
                    session.CreatedAt = existing.CreatedAt;
                }
            }
            catch
            {
                // Preserve current session.CreatedAt if existing is unreadable
            }
        }
        else if (session.CreatedAt == default)
        {
            session.CreatedAt = DateTime.UtcNow;
        }

        session.SchemaVersion = ScholarPersistenceConstants.CurrentSchemaVersion;
        session.LastAccessedAt = DateTime.UtcNow;

        await WriteStagedJsonFileAsync(targetPath, session, ct);
        _logger?.LogInformation("Persisted StudySession {SessionId} ('{Title}').", session.SessionId, session.Title);
        return session;
    }

    /// <inheritdoc/>
    public async Task<StudySession?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        ValidateIdentifier(sessionId, nameof(sessionId));
        var targetPath = Path.Combine(SessionsDirectory, $"{sessionId}.json");
        ValidateSafePath(targetPath, SessionsDirectory);

        return await ReadAndValidateJsonFileAsync<StudySession>(targetPath, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StudySession>> GetAllSessionsAsync(CancellationToken ct = default)
    {
        EnsureDirectories();
        var files = Directory.GetFiles(SessionsDirectory, "*.json");
        var sessions = new List<StudySession>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var session = await ReadAndValidateJsonFileAsync<StudySession>(file, ct);
                if (session != null)
                {
                    sessions.Add(session);
                }
            }
            catch (UnsupportedSchemaVersionException usvEx)
            {
                _logger?.LogWarning("Skipping session '{File}': {Message}", file, usvEx.Message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error loading session from '{File}'.", file);
            }
        }

        return sessions.OrderByDescending(s => s.LastAccessedAt).ToList();
    }

    /// <inheritdoc/>
    public Task<bool> DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        ValidateIdentifier(sessionId, nameof(sessionId));
        var targetPath = Path.Combine(SessionsDirectory, $"{sessionId}.json");
        ValidateSafePath(targetPath, SessionsDirectory);

        if (File.Exists(targetPath))
        {
            try
            {
                File.Delete(targetPath);
                _logger?.LogInformation("Deleted StudySession record {SessionId}.", sessionId);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to delete session record {SessionId}.", sessionId);
                LastPersistenceError = ex;
                throw;
            }
        }

        return Task.FromResult(false);
    }

    // ── Recovery & Maintenance ────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<int> QuarantineCorruptFilesAsync(CancellationToken ct = default)
    {
        EnsureDirectories();
        int quarantinedCount = 0;

        var candidateFiles = Directory.GetFiles(DocumentsDirectory, "*.json")
            .Concat(Directory.GetFiles(SessionsDirectory, "*.json"));

        foreach (var file in candidateFiles)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.Length == 0)
                {
                    QuarantineFile(file, "Zero-byte file discovered during maintenance");
                    quarantinedCount++;
                    continue;
                }

                var text = await File.ReadAllTextAsync(file, ct);
                if (string.IsNullOrWhiteSpace(text))
                {
                    QuarantineFile(file, "Whitespace-only file discovered during maintenance");
                    quarantinedCount++;
                    continue;
                }

                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    QuarantineFile(file, "Root element is not a JSON object");
                    quarantinedCount++;
                    continue;
                }

                // Check if schema version is at least parsable
                if (!doc.RootElement.TryGetProperty("schemaVersion", out var sv) &&
                    !doc.RootElement.TryGetProperty("SchemaVersion", out sv))
                {
                    QuarantineFile(file, "Missing schemaVersion field");
                    quarantinedCount++;
                }
            }
            catch (JsonException jEx)
            {
                QuarantineFile(file, $"Malformed JSON: {jEx.Message}");
                quarantinedCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error checking file '{File}' during quarantine scan.", file);
            }
        }

        return quarantinedCount;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetQuarantinedFiles()
    {
        EnsureDirectories();
        return Directory.Exists(QuarantineDirectory)
            ? Directory.GetFiles(QuarantineDirectory, "*.json")
            : [];
    }

    // ── Internal Helpers: Staged Persistence & Safe IO ────────────────────────

    /// <summary>
    /// Executes staged persistence using same-volume replacement semantics:
    /// 1. Writes payload to a unique .tmp file in the same directory.
    /// 2. Flushes file buffers completely.
    /// 3. Moves/replaces destination file with File.Move(overwrite: true).
    /// 4. Cleans up temporary file on failure.
    /// </summary>
    private async Task WriteStagedJsonFileAsync<T>(string targetPath, T data, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException($"Invalid target directory for '{targetPath}'.");
        Directory.CreateDirectory(directory);

        var tempFileName = $"{Path.GetFileNameWithoutExtension(targetPath)}.{Guid.NewGuid():N}.tmp";
        var tempPath = Path.Combine(directory, tempFileName);

        try
        {
            await using (var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(fileStream, data, _jsonOptions, ct);
                await fileStream.FlushAsync(ct);
            }

            // Retry with exponential backoff if another process or thread has a transient handle open
            const int maxRetries = 5;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    File.Move(tempPath, targetPath, overwrite: true);
                    break;
                }
                catch (IOException) when (attempt < maxRetries)
                {
                    await Task.Delay(25 * attempt, ct);
                }
            }

            LastPersistenceError = null;
        }
        catch (Exception ex)
        {
            LastPersistenceError = ex;
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (Exception cleanEx)
            {
                _logger?.LogDebug(cleanEx, "Failed to cleanup temporary staged file {TempPath}", tempPath);
            }

            _logger?.LogError(ex, "Failed staged persistence save to '{Path}'. Temporary file cleaned.", targetPath);
            throw;
        }
    }

    /// <summary>
    /// Reads, verifies schema version, and deserializes JSON.
    /// If malformed or 0-byte, safely relocates the corrupted file to quarantine and returns null.
    /// </summary>
    private async Task<T?> ReadAndValidateJsonFileAsync<T>(string filePath, CancellationToken ct) where T : class
    {
        if (!File.Exists(filePath))
            return null;

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            _logger?.LogWarning("Persisted file '{Path}' is zero-byte empty. Quarantining.", filePath);
            QuarantineFile(filePath, "Zero-byte empty file");
            return null;
        }

        string json;
        try
        {
            // Use FileShare.ReadWrite | FileShare.Delete to allow concurrent staged replacement on Windows
            await using var readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
            using var reader = new StreamReader(readStream, System.Text.Encoding.UTF8);
            json = await reader.ReadToEndAsync(ct);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (Exception ex)
        {
            LastPersistenceError = ex;
            _logger?.LogError(ex, "Failed to read file '{Path}'.", filePath);
            throw;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            QuarantineFile(filePath, "Whitespace-only file");
            return null;
        }

        // Schema validation & migration dispatch
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                _logger?.LogWarning("Persisted file '{Path}' root is not a JSON object (ValueKind={Kind}). Quarantining.", filePath, root.ValueKind);
                QuarantineFile(filePath, $"Invalid root JSON element kind: {root.ValueKind}");
                return null;
            }

            int schemaVersion = 0;
            if (root.TryGetProperty("schemaVersion", out var svProp) && svProp.TryGetInt32(out var sv))
            {
                schemaVersion = sv;
            }
            else if (root.TryGetProperty("SchemaVersion", out var svProp2) && svProp2.TryGetInt32(out var sv2))
            {
                schemaVersion = sv2;
            }

            if (schemaVersion > ScholarPersistenceConstants.CurrentSchemaVersion)
            {
                throw new UnsupportedSchemaVersionException(schemaVersion, ScholarPersistenceConstants.CurrentSchemaVersion);
            }

            if (schemaVersion < ScholarPersistenceConstants.MinimumSupportedSchemaVersion && schemaVersion > 0)
            {
                json = _migrator.MigrateJson(json, schemaVersion, typeof(T).Name);
            }
        }
        catch (JsonException jEx)
        {
            _logger?.LogWarning(jEx, "Persisted file '{Path}' contains malformed JSON syntax. Quarantining.", filePath);
            QuarantineFile(filePath, $"Malformed JSON: {jEx.Message}");
            return null;
        }
        catch (UnsupportedSchemaVersionException)
        {
            // Do NOT quarantine unsupported future version files; preserve them in-place and propagate exception
            throw;
        }

        try
        {
            var entity = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            if (entity == null)
            {
                QuarantineFile(filePath, "Deserialization returned null");
                return null;
            }
            LastPersistenceError = null;
            return entity;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to deserialize '{Path}' into {Type}. Quarantining.", filePath, typeof(T).Name);
            QuarantineFile(filePath, $"Deserialization failure: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Preserves a corrupted file by safely renaming/moving it to %APPDATA%\Axora\Scholar\quarantine\
    /// without deleting any user data.
    /// </summary>
    private string? QuarantineFile(string filePath, string reason)
    {
        try
        {
            if (!File.Exists(filePath)) return null;

            EnsureDirectories();
            var fileName = Path.GetFileName(filePath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var uniqueSuffix = Guid.NewGuid().ToString("N")[..6];
            var quarantinedName = $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}_{uniqueSuffix}.corrupt.json";
            var quarantinePath = Path.Combine(QuarantineDirectory, quarantinedName);

            File.Move(filePath, quarantinePath, overwrite: true);
            _logger?.LogWarning("Quarantined corrupt file '{Original}' -> '{QuarantinePath}'. Reason: {Reason}",
                filePath, quarantinePath, reason);
            return quarantinePath;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to move corrupted file '{Path}' to quarantine.", filePath);
            return null;
        }
    }

    private static readonly HashSet<string> WindowsReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static void ValidateIdentifier(string id, string paramName)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Identifier cannot be null, empty, or whitespace.", paramName);

        if (id.Contains("..") || id.Contains('/') || id.Contains('\\') || id.Contains(':'))
            throw new ScholarPathTraversalException($"Identifier '{id}' contains illegal directory traversal or stream characters.");

        if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException($"Identifier '{id}' contains invalid file name characters.", paramName);

        var nameWithoutExt = Path.GetFileNameWithoutExtension(id);
        if (WindowsReservedDeviceNames.Contains(nameWithoutExt))
            throw new ArgumentException($"Identifier '{id}' uses a reserved Windows device name.", paramName);
    }

    private static void ValidateSafePath(string fullPath, string expectedDirectory)
    {
        var fullTargetPath = Path.GetFullPath(fullPath);
        var fullExpectedDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedDirectory));
        if (!fullTargetPath.StartsWith(fullExpectedDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ScholarPathTraversalException($"Path traversal attempt detected: '{fullPath}' escapes '{expectedDirectory}'.");
        }
    }
}
