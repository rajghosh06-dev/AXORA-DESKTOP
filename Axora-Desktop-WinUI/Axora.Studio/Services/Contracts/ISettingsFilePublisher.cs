namespace Axora.Studio.Services.Contracts;

// Narrow commit seam for fault injection; not a suite-wide transaction framework.
public interface ISettingsFilePublisher
{
    void Commit(string stagedPath, string destination, string backup);
}
