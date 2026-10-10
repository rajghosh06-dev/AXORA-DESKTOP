namespace Axora.Studio.Models;

public static class ResumeLimits
{
    public const int JsonBytes = 1024 * 1024;
    public const int AggregateText = 100_000;
    public const int Title = 200;
    public const int Contact = 512;
    public const int Scalar = 2048;
    public const int Narrative = 16_384;
    public const int EntriesPerCollection = 50;
    public const int EntriesTotal = 200;
    public const int UndoCount = 30;
    public const int UndoBytes = 16 * 1024 * 1024;
    public const int DashboardDocuments = 1000;
    public const int DashboardPage = 100;
    public const int RecentDocuments = 20;
    public const int RevisionBackups = 5;
    public const int RevisionArtifacts = 32; // Finite degraded-state scan/admission budget, not the retention target.
}
