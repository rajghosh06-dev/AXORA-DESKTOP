namespace Axora.Studio.Tests;

internal static class ResumeRenderTests
{
    public static IReadOnlyList<string> RequiredCases { get; } = Array.AsReadOnly(
        ResumeRenderSnapshotTests.RequiredCases.Concat(ResumeTemplateTests.RequiredCases).Concat(ResumeRenderPackageTests.RequiredCases).ToArray());
    public static async Task RunAsync(Checks checks)
    {
        await ResumeRenderSnapshotTests.RunAsync(checks);
        await ResumeTemplateTests.RunAsync(checks);
        await ResumeRenderPackageTests.RunAsync(checks);
    }
}
