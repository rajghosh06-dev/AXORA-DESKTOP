namespace Axora.Studio.Services;

public sealed class StudioWriterLease(StudioPathService paths) : IDisposable
{
    private readonly object _gate = new();
    private FileStream? _handle;
    private bool _disposed;
    public bool IsAcquired { get { lock (_gate) return _handle is not null; } }

    public void Acquire()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_handle is not null) return;
            // Ownership is the live exclusive handle, never the file's existence.
            _handle = new FileStream(paths.Lease, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _handle?.Dispose();
            _handle = null;
        }
    }
}
