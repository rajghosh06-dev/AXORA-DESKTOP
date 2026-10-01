using System.Diagnostics;

namespace Axora.Studio.Services;

public sealed class StudioDiagnostics(StudioPathService paths)
{
    private readonly object _gate = new();
    public void Write(string message)
    {
        Debug.WriteLine(message);
        try
        {
            lock (_gate)
            {
                // Best-effort and bounded. Logging must never mask the primary error.
                if (!Directory.Exists(paths.Root)) return;
                if (File.Exists(paths.Log) && new FileInfo(paths.Log).Length >= 512 * 1024) return;
                File.AppendAllText(paths.Log, $"{DateTimeOffset.UtcNow:O} pid={Environment.ProcessId} {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Debug.WriteLine($"Studio diagnostics unavailable: {ex.GetType().Name}"); }
    }
}
