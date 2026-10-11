using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Axora.Studio.Models.Rendering;
using Microsoft.Win32.SafeHandles;

namespace Axora.Studio.Services.Rendering;

public sealed class TypstRuntimeVerifier
{
    internal string Root { get; }
    public TypstRuntimeVerifier() : this(RendererRuntimeManifest.DefaultProvisioningRoot) { }
    internal TypstRuntimeVerifier(string provisioningRoot)
    {
        RendererWorkspace.RejectRedirects(provisioningRoot);
        Root = Path.GetFullPath(provisioningRoot);
    }
    internal RuntimeLease Verify(WindowsRendererSandbox sandbox)
    {
        string path = RendererRuntimeManifest.ExecutablePath(Root);
        var pins = RendererWorkspace.PinAncestors(path);
        FileStream? file = null;
        try
        {
            var handle = OpenPinnedFile(path);
            file = new FileStream(handle, FileAccess.Read);
            var lease = new RuntimeLease(path, file, pins, RendererRuntimeManifest.ExecutableSha256, () =>
            {
                foreach (string owned in OwnedDirectories(Root)) sandbox.RequireOwned(owned, true);
                sandbox.RequireOwned(path, true);
            });
            lease.VerifyAgain();
            if (file.Length != RendererRuntimeManifest.ExecutableBytes) throw new InvalidDataException("Runtime size mismatch");
            using var reader = new BinaryReader(file, System.Text.Encoding.UTF8, leaveOpen: true);
            file.Position = 0;
            if (reader.ReadUInt16() != 0x5A4D) throw new InvalidDataException("Invalid executable");
            file.Position = 0x3c; int pe = reader.ReadInt32();
            if (pe < 0 || pe > file.Length - 24) throw new InvalidDataException("Invalid PE offset");
            file.Position = pe;
            if (reader.ReadUInt32() != 0x4550 || reader.ReadUInt16() != 0x8664) throw new InvalidDataException("Wrong executable architecture");
            foreach (string owned in OwnedDirectories(Root)) sandbox.RequireOwned(owned, true);
            sandbox.RequireOwned(path, true);
            return lease;
        }
        catch { file?.Dispose(); pins.Dispose(); throw; }
    }
    internal static IEnumerable<string> OwnedDirectories(string root)
    {
        yield return root;
        string current = root;
        foreach (string part in new[] { RendererRuntimeManifest.RendererId, RendererRuntimeManifest.Version, RendererRuntimeManifest.Platform, RendererRuntimeManifest.ExecutableSha256 })
        { current = Path.Combine(current, part); yield return current; }
    }
    // Explicit provisioning utility for test-owned/installer-owned resources. Never called on startup.
    // No existing runtime file is replaced, and no existing ACL is changed.
    internal static void Provision(string source, string root, WindowsRendererSandbox sandbox)
    {
        RendererWorkspace.RejectRedirects(source);
        using var sourcePins = RendererWorkspace.PinAncestors(source);
        using var input = new FileStream(OpenPinnedFile(source), FileAccess.Read);
        if (input.Length != RendererRuntimeManifest.ExecutableBytes || Convert.ToHexString(SHA256.HashData(input)) != RendererRuntimeManifest.ExecutableSha256)
            throw new InvalidDataException("Provisioning source identity mismatch");
        foreach (string dir in OwnedDirectories(root)) sandbox.EnsureOwnedDirectory(dir, true);
        string path = RendererRuntimeManifest.ExecutablePath(root);
        using var pins = RendererWorkspace.PinAncestors(path);
        using (var output = new FileStream(sandbox.CreateFile(path, true, 0x40000000), FileAccess.Write))
        { input.Position = 0; input.CopyTo(output); output.Flush(true); }
        using var verified = new TypstRuntimeVerifier(root).Verify(sandbox);
    }
    internal static SafeFileHandle OpenPinnedFile(string path)
    {
        RendererWorkspace.RejectRedirects(path);
        var handle = CreateFileW(path, 0x80000000, 1, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
        if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); if (error is 2 or 3) throw new FileNotFoundException(); throw new Win32Exception(error); }
        if (!GetFileInformationByHandle(handle, out var info) || (info.Attributes & 0x410) != 0 || info.Links != 1)
        { handle.Dispose(); throw new InvalidDataException("Runtime file identity rejected"); }
        return handle;
    }
    [StructLayout(LayoutKind.Sequential)] private struct FileInformation { public uint Attributes; public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write; public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
}
internal sealed class RuntimeLease(string path, FileStream file, IDisposable ancestors, string hash, Action? validateSecurity = null) : IDisposable
{
    internal string Path { get; } = path;
    internal void VerifyAgain()
    {
        RendererWorkspace.RejectRedirects(Path);
        validateSecurity?.Invoke();
        file.Position = 0;
        if (Convert.ToHexString(SHA256.HashData(file)) != hash) throw new InvalidDataException("Runtime integrity failure");
        file.Position = 0;
    }
    public void Dispose() { file.Dispose(); ancestors.Dispose(); }
}
