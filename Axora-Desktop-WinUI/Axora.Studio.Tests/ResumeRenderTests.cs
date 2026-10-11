namespace Axora.Studio.Tests;

internal static class ResumeRenderTests
{
    public static IReadOnlyList<string> RequiredCases => ResumeRenderSnapshotTests.RequiredCases;
    public static Task RunAsync(Checks checks) => ResumeRenderSnapshotTests.RunAsync(checks);
}
