namespace Axora.Studio.Services;

public sealed class StudioPathService
{
    public string Root { get; }
    public string Settings => Path.Combine(Root, "settings.json");
    public string Backup => Path.Combine(Root, "settings.json.bak");
    public string Lease => Path.Combine(Root, ".writer.lock");
    // A failed second writer must not append to the first process's diagnostics.
    public string Log => Path.Combine(Root, $"startup.{Environment.ProcessId}.log");

    // An injected APPDATA base makes tests independent of the user's real stores.
    public StudioPathService(string? applicationDataBase = null)
    {
        string value = applicationDataBase ?? Environment.GetEnvironmentVariable("APPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
            throw new InvalidOperationException("Studio requires an absolute application-data location.");
        Root = Path.Combine(Path.GetFullPath(value), "Axora", "Studio");
    }
}
