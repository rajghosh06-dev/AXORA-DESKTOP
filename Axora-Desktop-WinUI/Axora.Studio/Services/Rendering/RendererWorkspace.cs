using System.ComponentModel;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Axora.Studio.Models.Rendering;
using Microsoft.Win32.SafeHandles;

namespace Axora.Studio.Services.Rendering;

internal sealed class RendererWorkspace : IDisposable
{
    internal string Root { get; }
    internal string Input => Path.Combine(Root, "input");
    internal string Source => Path.Combine(Input, "main.typ");
    internal string Scratch => Path.Combine(Input, "scratch");
    internal string Packages => Path.Combine(Input, "packages");
    internal string Cache => Path.Combine(Input, "cache");
    private readonly List<(string Path, SafeFileHandle Handle)> _directories = [];
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<RendererWorkspace, byte> Quarantine = new();
    private IDisposable? _parents;
    private SafeFileHandle? _source;
    private readonly string _fixtureHash;
    private readonly WindowsRendererSandbox _sandbox;
    private bool _settled = true, _disposed;
    internal bool DeletePending { get; private set; }
    internal Action<string>? BeforePhaseForTest { get; set; }
    private RendererWorkspace(string root, string hash, WindowsRendererSandbox sandbox) { Root = root; _fixtureHash = hash; _sandbox = sandbox; }
    internal static RendererWorkspace Create(string workspaceBase, Guid id, RendererFixture fixture, WindowsRendererSandbox sandbox)
    {
        if (id == Guid.Empty) throw new InvalidDataException("Invalid workspace identity");
        RejectRedirects(workspaceBase);
        sandbox.EnsureOwnedDirectory(workspaceBase, true);
        var workspace = new RendererWorkspace(Path.Combine(Path.GetFullPath(workspaceBase), id.ToString("N")), TrustedRendererFixture.Hash(fixture), sandbox);
        try
        {
            workspace._parents = PinAncestors(Path.Combine(workspaceBase, "placeholder"));
            sandbox.RequireOwned(workspaceBase, true);
            foreach (string path in new[] { workspace.Root, workspace.Input, workspace.Scratch, workspace.Packages, workspace.Cache })
            {
                workspace._directories.Add((path, sandbox.CreatePinnedDirectory(path, true)));
                sandbox.RequireOwned(path, true);
            }
            // One handle spans creation, hashing, child access, and identity-bound deletion.
            workspace._source = sandbox.CreateFile(workspace.Source, true, 0xC0010000);
            byte[] bytes = TrustedRendererFixture.Bytes(fixture);
            RandomAccess.Write(workspace._source, bytes, 0);
            if (!FlushFileBuffers(workspace._source)) throw new Win32Exception();
            workspace.Validate();
            return workspace;
        }
        catch
        {
            if (!workspace.Cleanup()) { Quarantine.TryAdd(workspace, 0); throw new RendererWorkspaceSetupException(workspace.DeletePending); }
            workspace.Dispose(); throw;
        }
    }
    internal void MarkRunning() => _settled = false;
    internal void MarkSettled() => _settled = true;
    internal void Validate()
    {
        BeforePhaseForTest?.Invoke("validate");
        RejectRedirects(Root);
        foreach (var item in _directories) _sandbox.RequireOwned(item.Path, true);
        _sandbox.RequireOwned(Source, true);
        if (_source is null || _source.IsClosed) throw new InvalidDataException("Fixture handle unavailable");
        long length = RandomAccess.GetLength(_source);
        if (length > 4096 || length < 1) throw new InvalidDataException("Fixture size mismatch");
        byte[] bytes = new byte[(int)length];
        int read = RandomAccess.Read(_source, bytes, 0);
        if (read != bytes.Length || Convert.ToHexString(SHA256.HashData(bytes)) != _fixtureHash) throw new InvalidDataException("Fixture integrity failure");
        var allowed = new HashSet<string>(new[] { Input, Source, Scratch, Packages, Cache }, StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>(); pending.Push(Root); int count = 0;
        while (pending.TryPop(out string? directory))
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                if (++count > RendererLimits.WorkspaceEntries || !allowed.Contains(path)) throw new InvalidDataException("Unexpected workspace artifact");
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Redirected workspace artifact");
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(path);
            }
    }
    internal bool Cleanup()
    {
        if (_disposed) return !DeletePending && NamespaceAbsent(Root);
        if (!_settled) return false;
        try
        {
            BeforePhaseForTest?.Invoke("cleanup");
            // Parent handles remain pinned until every child handle is deleted, closed and checked.
            // No recursive/path-based delete; unexpected content prevents directory removal.
            if (_source is not null && !_source.IsClosed) DeleteOwned(Source, _source);
            _source = null;
            for (int i = _directories.Count - 1; i >= 0; i--)
            {
                DeleteOwned(_directories[i].Path, _directories[i].Handle);
                _directories.RemoveAt(i);
            }
            _disposed = true;
            _parents?.Dispose(); _parents = null;
            Quarantine.TryRemove(this, out _);
            return !DeletePending;
        }
        catch (Exception ex) when (Expected(ex)) { return false; }
    }
    private void DeleteOwned(string path, SafeFileHandle handle)
    {
        byte delete = 1;
        if (!SetFileInformationByHandle(handle, 4, ref delete, 1)) throw new Win32Exception();
        handle.Dispose();
        if (!NamespaceAbsent(path)) { DeletePending = true; throw new IOException("Renderer delete state unresolved"); }
    }
    internal static bool NamespaceAbsent(string path)
    {
        using var handle = CreateFileW(path, 0, 7, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        int error = Marshal.GetLastWin32Error();
        return handle.IsInvalid && error is 2 or 3; // Delete-pending/access denied/unknown are failures.
    }
    public void Dispose()
    {
        if (!_settled || !Cleanup()) { if (!_disposed) Quarantine.TryAdd(this, 0); return; }
        _source?.Dispose(); _source = null;
        foreach (var item in _directories.AsEnumerable().Reverse()) item.Handle.Dispose();
        _directories.Clear(); _parents?.Dispose(); _parents = null;
    }
    internal static bool Expected(Exception ex) => ex is IOException or UnauthorizedAccessException or Win32Exception or ArgumentException or InvalidOperationException or System.Security.SecurityException;
    internal static void RejectRedirects(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || path.Length < 3 ||
            path[1] != ':' || path.AsSpan(2).Contains(':') || path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(x => x is "." or ".." || x.EndsWith(' ') || x.EndsWith('.')))
            throw new InvalidDataException("Local canonical absolute path required");
        for (string? item = Path.GetFullPath(path); item is not null; item = Path.GetDirectoryName(item))
            if ((File.Exists(item) || Directory.Exists(item)) && (File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Redirected path");
    }
    internal static IDisposable PinAncestors(string file)
    {
        RejectRedirects(file);
        var pins = new List<IDisposable>();
        try
        {
            var paths = new Stack<string>();
            for (string? parent = Path.GetDirectoryName(file); parent is not null; parent = Path.GetDirectoryName(parent)) paths.Push(parent);
            foreach (string path in paths) { if (!Directory.Exists(path)) throw new DirectoryNotFoundException(); pins.Add(PinDirectory(path)); }
            RejectRedirects(file);
            return new PinSet(pins);
        }
        catch { foreach (var pin in pins.AsEnumerable().Reverse()) pin.Dispose(); throw; }
    }
    private sealed class PinSet(List<IDisposable> pins) : IDisposable { public void Dispose() { foreach (var pin in pins.AsEnumerable().Reverse()) pin.Dispose(); } }
    internal static SafeFileHandle PinDirectory(string path, bool delete = false)
    {
        var handle = CreateFileW(path, 0x80000000 | (delete ? 0x10000u : 0), 1, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(); }
        if (!GetFileInformationByHandle(handle, out var information) || (information.Attributes & 0x400) != 0)
        { handle.Dispose(); throw new InvalidDataException("Redirected directory"); }
        return handle;
    }
    [StructLayout(LayoutKind.Sequential)] private struct FileInformation { public uint Attributes; public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write; public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool FlushFileBuffers(SafeFileHandle file);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetFileInformationByHandle(SafeFileHandle file, int kind, ref byte information, uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
}
internal sealed class RendererWorkspaceSetupException(bool deletePending) : IOException("Renderer workspace setup cleanup failed")
{
    internal bool DeletePending { get; } = deletePending;
}
