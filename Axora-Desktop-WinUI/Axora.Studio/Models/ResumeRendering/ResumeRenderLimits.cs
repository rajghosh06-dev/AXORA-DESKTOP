namespace Axora.Studio.Models.ResumeRendering;

public static class ResumeRenderLimits
{
    public const int SnapshotSchemaVersion = 1;
    public const int DerivationVersion = 1;
    public const int RenderDataSchemaVersion = 1;
    public const int ExpectedContentPolicyVersion = 1;
    public const int CanonicalBytes = 4 * 1024 * 1024;
    public const int DerivedItems = ResumeLimits.AggregateText + ResumeLimits.EntriesTotal + 1;
    public const int ExpectedContentItems = 131_072;
}
