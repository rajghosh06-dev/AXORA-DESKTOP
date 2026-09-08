using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Persistence service contract for Scholar Kit local study sessions and ingested documents.
/// Guarantees offline, local-first storage in %APPDATA%\Axora\Scholar\ with staged persistence,
/// safe deletion, corrupt file preservation, and path traversal protection.
/// </summary>
public interface IScholarLibraryService
{
    string ScholarDataDirectory { get; }
    string DocumentsDirectory { get; }
    string SessionsDirectory { get; }
    string QuarantineDirectory { get; }

    Exception? LastPersistenceError { get; }

    // ── Document Persistence ──────────────────────────────────────────────────

    /// <summary>
    /// Persists or updates a ScholarDocument record using staged same-volume replacement.
    /// Does NOT modify, rewrite, or copy the original user source document.
    /// </summary>
    Task<ScholarDocument> SaveDocumentAsync(ScholarDocument document, CancellationToken ct = default);

    /// <summary>
    /// Loads a ScholarDocument by its unique DocumentId.
    /// Returns null if not found or if the file was corrupted and quarantined.
    /// </summary>
    Task<ScholarDocument?> GetDocumentAsync(string documentId, CancellationToken ct = default);

    /// <summary>
    /// Enumerates all valid persisted ScholarDocument records in the library.
    /// Corrupt files are automatically quarantined without failing the entire enumeration.
    /// </summary>
    Task<IReadOnlyList<ScholarDocument>> GetAllDocumentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes a persisted ScholarDocument record from disk.
    /// Under NO circumstance is the user's original source file deleted.
    /// </summary>
    Task<bool> DeleteDocumentAsync(string documentId, CancellationToken ct = default);

    // ── Study Session Persistence ─────────────────────────────────────────────

    /// <summary>
    /// Persists or updates a user study workspace session using staged same-volume replacement.
    /// </summary>
    Task<StudySession> SaveSessionAsync(StudySession session, CancellationToken ct = default);

    /// <summary>
    /// Loads a StudySession by its unique SessionId.
    /// Returns null if not found or if the file was corrupted and quarantined.
    /// </summary>
    Task<StudySession?> GetSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Enumerates all valid persisted StudySession records in the library.
    /// Corrupt files are automatically quarantined without failing the entire enumeration.
    /// </summary>
    Task<IReadOnlyList<StudySession>> GetAllSessionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes a persisted StudySession record from disk.
    /// </summary>
    Task<bool> DeleteSessionAsync(string sessionId, CancellationToken ct = default);

    // ── Recovery & Maintenance ────────────────────────────────────────────────

    /// <summary>
    /// Scans the documents and sessions directories for malformed or zero-byte files,
    /// safely preserving and relocating them to the quarantine directory.
    /// </summary>
    Task<int> QuarantineCorruptFilesAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves file paths of all preserved quarantined corrupted persistence files.
    /// </summary>
    IReadOnlyList<string> GetQuarantinedFiles();
}
