using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Axora.Studio.Services.Rendering;
using Microsoft.Win32.SafeHandles;

namespace Axora.Studio.Services.ResumeRendering;

// No PDF/source file is staged. Only empty, owned directories cross the native boundary.
internal sealed class PdfValidationWorkspace : IDisposable
{
    private static readonly ConcurrentDictionary<PdfValidationWorkspace, byte> Quarantine = new();
    private readonly WindowsRendererSandbox sandbox;
    private readonly string profileState;
    private readonly List<(string Path, SafeFileHandle Handle)> directories = [];
    private IDisposable? parents;
    private bool settled = true, disposed;
    internal string Root { get; }
    internal string Scratch => Path.Combine(Root, "scratch");
    internal bool DeletePending { get; private set; }
    private PdfValidationWorkspace(string root, WindowsRendererSandbox sandbox)
    { Root = root; this.sandbox = sandbox; profileState = sandbox.CaptureProfileState(); }

    internal static PdfValidationWorkspace Create(string ownedBase, WindowsRendererSandbox sandbox, Action<string>? afterDirectoryForTest = null)
    {
        RendererWorkspace.RejectRedirects(ownedBase);
        sandbox.RequireOwned(ownedBase, true);
        var workspace = new PdfValidationWorkspace(Path.Combine(ownedBase, Guid.NewGuid().ToString("N")), sandbox);
        try
        {
            workspace.parents = RendererWorkspace.PinAncestors(workspace.Root);
            foreach (string path in new[] { workspace.Root, workspace.Scratch })
            {
                workspace.directories.Add((path, sandbox.CreatePinnedDirectory(path, true)));
                afterDirectoryForTest?.Invoke(path);
            }
            workspace.Validate();
            return workspace;
        }
        catch
        {
            workspace.Dispose();
            if (!RendererWorkspace.NamespaceAbsent(workspace.Root)) throw new PdfFoundationSetupException();
            throw;
        }
    }
    internal void MarkRunning() => settled = false;
    internal void MarkSettled() => settled = true;
    internal void Validate()
    {
        if (disposed) throw new ObjectDisposedException(nameof(PdfValidationWorkspace));
        foreach (string path in new[] { Root, Scratch })
        {
            sandbox.RequireOwned(path, true);
            string[] entries = Directory.EnumerateFileSystemEntries(path).Take(2).ToArray();
            if (path == Root ? entries.Length != 1 || entries[0] != Scratch : entries.Length != 0)
                throw new InvalidDataException("PdfFoundationUnexpectedWorkspaceEntry");
        }
        if (profileState != sandbox.CaptureProfileState()) throw new InvalidDataException("PdfFoundationProfileChanged");
    }
    internal bool Cleanup()
    {
        if (disposed) return !DeletePending && RendererWorkspace.NamespaceAbsent(Root);
        if (!settled) return false;
        try
        {
            // Unexpected entries are preserved, never recursively removed.
            if (directories.Count == 2) Validate();
            while (directories.Count != 0)
            {
                var entry = directories[^1];
                byte delete = 1;
                if (!SetFileInformationByHandle(entry.Handle, 4, ref delete, 1)) throw new Win32Exception();
                entry.Handle.Dispose(); directories.RemoveAt(directories.Count - 1);
                if (!RendererWorkspace.NamespaceAbsent(entry.Path)) { DeletePending = true; throw new IOException("PdfFoundationDeletePending"); }
            }
            parents?.Dispose(); parents = null; disposed = true;
            Quarantine.TryRemove(this, out _);
            return RendererWorkspace.NamespaceAbsent(Root);
        }
        catch (Exception ex) when (RendererWorkspace.Expected(ex)) { return false; }
    }
    public void Dispose()
    {
        if (!Cleanup() && !disposed) Quarantine.TryAdd(this, 0);
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetFileInformationByHandle(SafeFileHandle file, int kind, ref byte information, uint size);
}

internal sealed class PdfFoundationSetupException : IOException
{
    internal PdfFoundationSetupException() : base("PdfFoundationSetupCleanupFailed") { }
}
