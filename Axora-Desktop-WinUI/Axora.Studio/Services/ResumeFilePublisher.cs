using Axora.Studio.Models;
using Axora.Studio.Services.Contracts;

namespace Axora.Studio.Services;

public sealed class ResumeFilePublisher(ResumeStore store, ResumeCodec codec) : IResumeFilePublisher
{
    // Dormant in normal composition. Tests place barriers/failures at actual production boundaries.
    public Func<string, Task>? BeforePhaseForTest { get; set; }
    public Func<string>? StageIdentityForTest { get; set; }
    private Task Phase(string phase) => BeforePhaseForTest?.Invoke(phase) ?? Task.CompletedTask;
    public async Task PreserveImportAsync(ReadOnlyMemory<byte> capturedBytes, string sha256)
    {
        byte[] owned = capturedBytes.ToArray();
        if (!ResumeCodec.IsHash(sha256) || ResumeCodec.Hash(owned) != sha256) throw new InvalidDataException("Import hash mismatch.");
        await Phase("import-backup").ConfigureAwait(false);
        string folder = Path.Combine(store.Root, "Imports");
        store.EnsureOwnedPath(Path.Combine(folder, sha256 + ".json"));
        Directory.CreateDirectory(folder);
        await PreserveExactAsync(Path.Combine(folder, sha256 + ".json"), owned).ConfigureAwait(false);
    }
    public async Task<ResumePublished> PublishAsync(ResumePublication publication)
    {
        byte[] bytes = codec.Encode(publication.File); // Caller supplied immutable revision; never read live UI state.
        string committedHash = ResumeCodec.Hash(bytes); // No fallible hash work remains after the commit boundary.
        string path = store.DocumentPath(publication.File.DocumentId);
        store.EnsureOwnedPath(path);
        Directory.CreateDirectory(store.Root);
        if (publication.ExpectedSha256 is null && Directory.EnumerateFiles(store.Root, "*.json").Take(ResumeLimits.DashboardDocuments).Count() >= ResumeLimits.DashboardDocuments)
            throw new InvalidDataException("Resume library limit reached.");
        byte[]? previous = await CheckDestinationAsync(path, publication.ExpectedSha256).ConfigureAwait(false);
        ResumeFile? prior = null;
        if (previous is not null)
        {
            try { prior = codec.Decode(previous); }
            catch (Exception ex) when (publication.Recovery && ex is System.Text.Json.JsonException or InvalidDataException)
            {
                // Future envelopes are always protected, including recovery.
                if (ResumeCodec.HasUnsupportedSchemaPrefix(previous)) throw new InvalidDataException("Unsupported Resume schema is protected.");
            }
            if (prior is not null && (prior.DocumentId != publication.File.DocumentId || prior.Revision >= publication.File.Revision
                || prior.CreatedUtc != publication.File.CreatedUtc)) throw new InvalidDataException("Revision/identity changed.");
        }
        string stageIdentity = StageIdentityForTest?.Invoke() ?? Guid.NewGuid().ToString("N");
        if (!ResumeCodec.IsId(stageIdentity)) throw new InvalidDataException("Invalid stage identity.");
        string stage = path + "." + stageIdentity + ".stage";
        bool committed = false;
        bool stageOwned = false;
        try
        {
            await Phase("stage").ConfigureAwait(false);
            await WriteNewAsync(stage, bytes, () => stageOwned = true).ConfigureAwait(false);
            await Phase("reopen").ConfigureAwait(false);
            byte[] reopened = await ResumeStore.CaptureAsync(stage).ConfigureAwait(false);
            codec.Decode(reopened);
            if (!reopened.AsSpan().SequenceEqual(bytes)) throw new InvalidDataException("Staged revision changed.");
            if (previous is not null)
            {
                await Phase("revision-backup").ConfigureAwait(false);
                string folder = Path.Combine(store.Root, prior is null ? "Quarantine" : "Revisions", publication.File.DocumentId);
                store.EnsureOwnedPath(folder);
                Directory.CreateDirectory(folder);
                string name = (prior?.Revision.ToString("D20", System.Globalization.CultureInfo.InvariantCulture) ?? "corrupt") + "_" + ResumeCodec.Hash(previous) + ".json";
                if (prior is not null && !File.Exists(Path.Combine(folder, name))
                    && Directory.EnumerateFiles(folder, "*.json").Take(ResumeLimits.RevisionArtifacts).Count() >= ResumeLimits.RevisionArtifacts)
                    throw new InvalidDataException("Revision artifact budget reached.");
                await PreserveExactAsync(Path.Combine(folder, name), previous).ConfigureAwait(false);
            }
            await Phase("replace").ConfigureAwait(false);
            store.EnsureOwnedPath(path);
            await CheckDestinationAsync(path, publication.ExpectedSha256).ConfigureAwait(false); // Final recheck immediately before atomic publication.
            if (previous is null) File.Move(stage, path, false);
            else File.Replace(stage, path, null); // Unsupported replacement fails safely. Never delete/truncate fallback.
            committed = true;
            bool maintenanceFailed = false;
            try
            {
                await Phase("maintenance").ConfigureAwait(false);
                string folder = Path.Combine(store.Root, "Revisions", publication.File.DocumentId);
                store.EnsureOwnedPath(folder);
                if (Directory.Exists(folder))
                {
                    var backups = Directory.EnumerateFiles(folder, "*.json").Take(ResumeLimits.RevisionArtifacts + 1).ToArray();
                    if (backups.Length > ResumeLimits.RevisionArtifacts) throw new InvalidDataException("Revision library limit exceeded.");
                    int retainedValid = 0;
                    foreach (string old in backups.Length > ResumeLimits.RevisionBackups ? backups.OrderDescending(StringComparer.Ordinal) : Enumerable.Empty<string>())
                    {
                        byte[]? backupBytes = null;
                        bool verified = false;
                        try
                        {
                            store.EnsureOwnedPath(old);
                            backupBytes = await ResumeStore.CaptureAsync(old).ConfigureAwait(false);
                            var backup = codec.Decode(backupBytes);
                            string ownedName = backup.Revision.ToString("D20", System.Globalization.CultureInfo.InvariantCulture) + "_" + ResumeCodec.Hash(backupBytes) + ".json";
                            if (backup.DocumentId != publication.File.DocumentId || backup.CreatedUtc != publication.File.CreatedUtc || Path.GetFileName(old) != ownedName)
                                throw new InvalidDataException("Backup ownership mismatch.");
                            verified = true;
                        }
                        catch (Exception ex) when (ExpectedArtifactFailure(ex)) { maintenanceFailed = true; }
                        if (verified && ++retainedValid <= ResumeLimits.RevisionBackups) continue;
                        try
                        {
                            if (!verified)
                            {
                                if (backupBytes is null) continue; // Unreadable/oversized evidence stays active; never delete without preservation.
                                string quarantine = Path.Combine(store.Root, "Quarantine", publication.File.DocumentId, "revision_" + ResumeCodec.Hash(backupBytes) + ".json");
                                store.EnsureOwnedPath(quarantine);
                                Directory.CreateDirectory(Path.GetDirectoryName(quarantine)!);
                                await Phase("quarantine-preserve").ConfigureAwait(false);
                                await PreserveExactAsync(quarantine, backupBytes).ConfigureAwait(false);
                                await Phase("quarantine-verified").ConfigureAwait(false);
                                store.EnsureOwnedPath(quarantine);
                                byte[] preserved = await ResumeStore.CaptureAsync(quarantine).ConfigureAwait(false);
                                if (!preserved.AsSpan().SequenceEqual(backupBytes)) throw new IOException("Quarantine changed.");
                            }
                            store.EnsureOwnedPath(old);
                            byte[] current = await ResumeStore.CaptureAsync(old).ConfigureAwait(false);
                            if (!current.AsSpan().SequenceEqual(backupBytes)) throw new IOException("Revision changed during maintenance.");
                            File.Delete(old); // Verified valid pruning, or exact suspect preservation completed first.
                        }
                        catch (Exception ex) when (ExpectedArtifactFailure(ex)) { maintenanceFailed = true; }
                    }
                }
            }
            // Destination commit has already succeeded. Expected artifact failures are a typed
            // maintenance warning, never a pre-commit failure. Fatal process exceptions escape.
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                or System.Text.Json.JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException)
            { maintenanceFailed = true; }
            return new(committedHash, maintenanceFailed);
        }
        finally
        {
            if (stageOwned && !committed && File.Exists(stage)) { try { File.Delete(stage); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }
    private static bool ExpectedArtifactFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or InvalidDataException
        or System.Text.Json.JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException;
    private static async Task<byte[]?> CheckDestinationAsync(string path, string? expected)
    {
        if (expected is null)
        {
            if (File.Exists(path)) throw new IOException("Managed identity already exists.");
            return null;
        }
        byte[] bytes = await ResumeStore.CaptureAsync(path).ConfigureAwait(false);
        if (ResumeCodec.Hash(bytes) != expected) throw new IOException("Managed destination changed.");
        return bytes;
    }
    private static async Task PreserveExactAsync(string path, byte[] bytes)
    {
        if (!File.Exists(path)) await WriteNewAsync(path, bytes).ConfigureAwait(false);
        byte[] reopened = await ResumeStore.CaptureAsync(path).ConfigureAwait(false);
        if (!reopened.AsSpan().SequenceEqual(bytes)) throw new IOException("Backup verification/collision failure.");
    }
    private static async Task WriteNewAsync(string path, byte[] bytes, Action? onCreated = null)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous | FileOptions.WriteThrough);
        onCreated?.Invoke(); // Creation success establishes cleanup ownership, before any fallible write/flush.
        await stream.WriteAsync(bytes).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
        stream.Flush(true);
    }
}
