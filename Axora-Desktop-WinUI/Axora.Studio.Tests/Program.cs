using System.Text.Json;

namespace Axora.Studio.Tests;

internal static class Program
{
    private static readonly string[] Manifest = ["STUDIO-H0"];
    public static async Task<int> Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--manifest" }))
        {
            Console.WriteLine(JsonSerializer.Serialize(new { groups = Manifest, timeoutSeconds = 120 }));
            return 0;
        }
        if (args.Length != 0 && !args.SequenceEqual(new[] { "--group=STUDIO-H0" }))
        {
            Console.WriteLine("Unknown arguments/group. No tests ran.");
            return 2;
        }
        var checks = new Checks();
        int blocked = 0;
        try { await HostTests.RunAsync(checks).WaitAsync(TimeSpan.FromSeconds(120)); }
        catch (TimeoutException) { blocked = 1; Console.WriteLine("BLOCKED: STUDIO-H0 exceeded 120 seconds."); }
        catch (Exception ex) { checks.Fail("Unhandled test-group exception", ex.ToString()); }
        bool complete = checks.Executed.SetEquals(HostTests.RequiredCases);
        int missing = HostTests.RequiredCases.Except(checks.Executed).Count();
        int unknown = checks.Executed.Except(HostTests.RequiredCases).Count();
        bool pass = complete && checks.Failed == 0 && blocked == 0 && checks.Duplicate == 0 && unknown == 0;
        Console.WriteLine("STUDIO-LEDGER " + JsonSerializer.Serialize(new
        {
            group = Manifest.Single(), registeredGroups = 1, executedGroups = 1,
            disposition = pass ? "Pass" : blocked != 0 ? "Blocked" : "Fail",
            passedAssertions = checks.Passed, failedAssertions = checks.Failed,
            missing, duplicate = checks.Duplicate, unknown, blocked, complete
        }));
        return pass ? 0 : 1;
    }
}

internal sealed class Checks
{
    public HashSet<string> Executed { get; } = new(StringComparer.Ordinal);
    public int Passed { get; private set; }
    public int Failed { get; private set; }
    public int Duplicate { get; private set; }
    public async Task CaseAsync(string id, Func<Task> run)
    {
        if (!Executed.Add(id)) { Duplicate++; Fail(id, "Duplicate case."); return; }
        int before = Passed + Failed;
        try { await run(); }
        catch (Exception ex) { Fail(id, ex.ToString()); }
        if (before == Passed + Failed) Fail(id, "Case executed no assertions.");
    }
    public void That(bool condition, string name)
    {
        if (condition) { Passed++; Console.WriteLine("PASS: " + name); }
        else Fail(name, "Condition was false.");
    }
    public void Fail(string name, string reason) { Failed++; Console.WriteLine($"FAIL: {name}: {reason}"); }
    public async Task ThrowsAsync<T>(Func<Task> run, string name) where T : Exception
    {
        try { await run(); That(false, name); }
        catch (T) { That(true, name); }
    }
}
