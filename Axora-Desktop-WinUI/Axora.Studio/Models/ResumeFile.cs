namespace Axora.Studio.Models;

public sealed record ResumeFile
{
    public int SchemaVersion { get; init; } = 1;
    public string DocumentId { get; init; } = Guid.NewGuid().ToString("N");
    public long Revision { get; init; } = 1;
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedUtc { get; init; } = DateTimeOffset.UtcNow;
    public ResumeDocument Document { get; init; } = new();
    public string? TemplateId { get; init; }
    public int? TemplateVersion { get; init; }
    public string? ImportSha256 { get; init; }
    public long? RecoveredFromRevision { get; init; }
}
public enum ResumeResultKind { Saved, Imported, ExistingImport, Opened, Recovered, Busy, Cancelled, Failed, Closed }
public sealed record ResumeResult(ResumeResultKind Kind, string Message, ResumeFile? File = null)
{
    public bool Success => Kind is ResumeResultKind.Saved or ResumeResultKind.Imported or ResumeResultKind.ExistingImport
        or ResumeResultKind.Opened or ResumeResultKind.Recovered;
}
public sealed record ResumeEntry(string DocumentId, string Title, DateTimeOffset? ModifiedUtc, bool CanOpen, string Status);
public sealed record ResumeRead(ResumeFile File, string Sha256);
public sealed record ResumePublication(ResumeFile File, string? ExpectedSha256, bool Recovery = false);
public sealed record ResumePublished(string Sha256, bool BackupMaintenanceFailed = false);
public enum ResumeDeparture { Save, Discard, Cancel }
