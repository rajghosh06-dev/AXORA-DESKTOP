using Axora.Studio.Models;

namespace Axora.Studio.Services;

public sealed class ResumeStore
{
    private readonly ResumeCodec _codec;
    private readonly string _base;
    public string Root { get; }
    public int Enumerations { get; private set; }
    public ResumeStore(StudioPathService paths, ResumeCodec codec)
    { Root = Path.Combine(paths.Root, "Resume"); _base = Path.GetDirectoryName(Path.GetDirectoryName(paths.Root))!; _codec = codec; }
    public void EnsureOwnedPath(string path)
    {
        string full = Path.GetFullPath(path);
        if (full != Root && !full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Path is outside Resume ownership.");
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Resume ownership path is redirected.");
            if (string.Equals(current, _base, StringComparison.OrdinalIgnoreCase)) return;
        }
        throw new InvalidDataException("Resume ownership base unavailable.");
    }
    public string DocumentPath(string id)
    {
        if (!ResumeCodec.IsId(id)) throw new InvalidDataException("Invalid managed identity.");
        return Path.Combine(Root, id + ".json");
    }
    public static async Task<byte[]> CaptureAsync(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous);
        if (stream.Length == 0 || stream.Length > ResumeLimits.JsonBytes) throw new InvalidDataException("Invalid JSON size.");
        using var memory = new MemoryStream((int)stream.Length);
        byte[] buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer).ConfigureAwait(false)) != 0)
        {
            if (memory.Length + count > ResumeLimits.JsonBytes) throw new InvalidDataException("JSON limit exceeded.");
            memory.Write(buffer, 0, count);
        }
        return memory.ToArray();
    }
    public async Task<ResumeRead> ReadAsync(string id)
    {
        EnsureOwnedPath(DocumentPath(id));
        byte[] bytes = await CaptureAsync(DocumentPath(id)).ConfigureAwait(false);
        var file = _codec.Decode(bytes);
        if (file.DocumentId != id) throw new InvalidDataException("Filename/document identity mismatch.");
        return new(file, ResumeCodec.Hash(bytes));
    }
    public async Task<IReadOnlyList<ResumeEntry>> ListAsync(int page = 0)
    {
        if (page < 0 || page >= ResumeLimits.DashboardDocuments / ResumeLimits.DashboardPage) throw new ArgumentOutOfRangeException(nameof(page));
        Enumerations++;
        EnsureOwnedPath(Root);
        if (!Directory.Exists(Root)) return [];
        var paths = Directory.EnumerateFiles(Root, "*.json").Take(ResumeLimits.DashboardDocuments + 1).ToList();
        string revisions = Path.Combine(Root, "Revisions"); EnsureOwnedPath(revisions);
        if (Directory.Exists(revisions))
        {
            int folders = 0;
            foreach (string folder in Directory.EnumerateDirectories(revisions))
            {
                if (++folders > ResumeLimits.DashboardDocuments) throw new InvalidDataException("Revision library limit reached.");
                string id = Path.GetFileName(folder);
                if (ResumeCodec.IsId(id) && !File.Exists(DocumentPath(id))) paths.Add(DocumentPath(id));
            }
        }
        if (paths.Count > ResumeLimits.DashboardDocuments) throw new InvalidDataException("Resume library limit reached.");
        var entries = new List<ResumeEntry>();
        foreach (string path in paths.Order(StringComparer.Ordinal).Skip(page * ResumeLimits.DashboardPage).Take(ResumeLimits.DashboardPage))
        {
            string id = Path.GetFileNameWithoutExtension(path);
            try { var read = await ReadAsync(id).ConfigureAwait(false); entries.Add(new(id, read.File.Document.ResumeTitle, read.File.ModifiedUtc, true, "Ready")); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
            { entries.Add(new(id, "Unavailable document", null, false, "Invalid, unsupported or unreadable")); }
        }
        return entries;
    }
    public async Task<ResumeRead?> FindImportAsync(string hash)
    {
        EnsureOwnedPath(Root);
        if (!Directory.Exists(Root)) return null;
        int count = 0;
        foreach (string path in Directory.EnumerateFiles(Root, "*.json"))
        {
            if (++count > ResumeLimits.DashboardDocuments) throw new InvalidDataException("Resume library limit reached.");
            try { var read = await ReadAsync(Path.GetFileNameWithoutExtension(path)).ConfigureAwait(false); if (read.File.ImportSha256 == hash) return read; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or ArgumentException or InvalidOperationException) { }
        }
        return null;
    }
    public IReadOnlyList<string> Backups(string id)
    {
        _ = DocumentPath(id);
        EnsureOwnedPath(Path.Combine(Root, "Revisions", id));
        if (!Directory.Exists(Path.Combine(Root, "Revisions", id))) return [];
        // Retention targets five valid revisions. A separate finite artifact budget keeps
        // recovery available during a temporary preservation failure without unbounded scans.
        var files = Directory.EnumerateFiles(Path.Combine(Root, "Revisions", id), "*.json").Take(ResumeLimits.RevisionArtifacts + 1).ToArray();
        if (files.Length > ResumeLimits.RevisionArtifacts) throw new InvalidDataException("Revision library limit exceeded.");
        return files.OrderDescending(StringComparer.Ordinal).ToArray();
    }
    public async Task<ResumeFile> ReadBackupAsync(string id, string backup)
    {
        if (!ResumeCodec.IsId(id) || !Backups(id).Contains(backup, StringComparer.Ordinal)) throw new InvalidDataException("Unknown recovery revision.");
        EnsureOwnedPath(backup);
        byte[] bytes = await CaptureAsync(backup).ConfigureAwait(false);
        var file = _codec.Decode(bytes);
        string name = file.Revision.ToString("D20", System.Globalization.CultureInfo.InvariantCulture) + "_" + ResumeCodec.Hash(bytes) + ".json";
        if (file.DocumentId != id || Path.GetFileName(backup) != name) throw new InvalidDataException("Recovery identity/hash mismatch.");
        return file;
    }
}
