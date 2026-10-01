using Axora.Studio.Services.Contracts;

namespace Axora.Studio.Services;

public sealed class SettingsFilePublisher : ISettingsFilePublisher
{
    public void Commit(string stagedPath, string destination, string backup)
    {
        if (File.Exists(destination)) File.Replace(stagedPath, destination, backup);
        else File.Move(stagedPath, destination, overwrite: false);
        // No delete/move fallback: unsupported replace fails with old bytes intact.
    }
}
