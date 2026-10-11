using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace Axora.Studio.Services.Rendering;

// Windows requires a registered profile on the tested native launch path. Its complete
// filesystem/registry surface is read-only to the renderer, including outside operations.
internal sealed class WindowsRendererSandbox : IDisposable
{
    internal const string ProfileName = "Axora.Studio.Renderer.Typst.ReadOnly.V2";
    private const string DisplayName = "AXORA Studio renderer";
    private static readonly object ProfileGate = new();
    private readonly List<IDisposable> _profilePins = [];
    private string _profileState = "";
    internal string ProfileDirectory { get; private set; } = "";
    internal IntPtr Sid { get; private set; }
    internal string SidText { get; }
    internal WindowsRendererSandbox()
    {
        lock (ProfileGate)
        {
            int hr = DeriveAppContainerSidFromAppContainerName(ProfileName, out var sid);
            if (hr < 0) { if (sid != IntPtr.Zero) FreeSid(sid); throw new Win32Exception(hr); }
            Sid = sid;
            try
            {
                SidText = new SecurityIdentifier(sid).Value;
                string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", ProfileName.ToLowerInvariant(), "AC");
                bool packageAbsent = RendererWorkspace.NamespaceAbsent(Path.GetDirectoryName(expected)!);
                using var mapping = OpenProfileKey(RegistryPath("Mappings"), false, true);
                using var storage = OpenProfileKey(RegistryPath("Storage"), false, true);
                bool created = false;
                if (packageAbsent && mapping is null && storage is null)
                {
                    hr = CreateAppContainerProfile(ProfileName, DisplayName, "Offline renderer", IntPtr.Zero, 0, out var createdSid);
                    try
                    {
                        created = hr == 0;
                        if (hr < 0 && hr != unchecked((int)0x800700B7)) throw new Win32Exception(hr);
                        if (created && (createdSid == IntPtr.Zero || new SecurityIdentifier(createdSid).Value != SidText)) throw new RendererProvisioningException();
                    }
                    finally { if (createdSid != IntPtr.Zero) FreeSid(createdSid); }
                }
                else if (packageAbsent || mapping is null || storage is null) throw new RendererProvisioningException();
                hr = GetAppContainerFolderPath(SidText, out var folder);
                try { if (hr < 0 || folder == IntPtr.Zero) throw new Win32Exception(hr); ProfileDirectory = Marshal.PtrToStringUni(folder)!; }
                finally { if (folder != IntPtr.Zero) Marshal.FreeCoTaskMem(folder); }
                if (!string.Equals(ProfileDirectory, expected, StringComparison.OrdinalIgnoreCase)) throw new RendererProvisioningException();
                _profilePins.Add(RendererWorkspace.PinAncestors(ProfileDirectory));
                foreach (string path in ProfilePaths()) _profilePins.Add(RendererWorkspace.PinDirectory(path));
                // Only the resources just created by this call may be hardened. Existing
                // profiles are validated without repair, deletion, or ownership changes.
                if (created) HardenFreshProfile();
                _profileState = CaptureProfileState();
            }
            catch { Dispose(); throw; }
        }
    }
    private string[] ProfilePaths()
    {
        string package = Path.GetDirectoryName(ProfileDirectory)!;
        var paths = new[] { package, ProfileDirectory, Path.Combine(ProfileDirectory, "INetCache"), Path.Combine(ProfileDirectory, "INetCookies"), Path.Combine(ProfileDirectory, "INetHistory"), Path.Combine(ProfileDirectory, "Temp") };
        foreach (string path in paths)
        {
            RendererWorkspace.RejectRedirects(path);
            string[] expected = path == package ? [ProfileDirectory] : path == ProfileDirectory ? paths[2..] : [];
            var entries = Directory.EnumerateFileSystemEntries(path).Take(7).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            if (!entries.SequenceEqual(expected.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase)) throw new RendererProvisioningException();
        }
        return paths;
    }
    private string RegistryPath(string kind) => @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppContainer\" + kind + @"\" + (kind == "Mappings" ? SidText : ProfileName.ToLowerInvariant());
    private string RegistryDescriptor()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return $"D:P(A;;KA;;;SY)(A;;KA;;;{identity.User!.Value})(A;;KR;;;{SidText})";
    }
    private void HardenFreshProfile()
    {
        using var identity = WindowsIdentity.GetCurrent();
        foreach (string path in ProfilePaths())
        {
            var actual = new DirectoryInfo(path).GetAccessControl(AccessControlSections.Owner);
            if (actual.GetOwner(typeof(SecurityIdentifier)) != identity.User) throw new RendererProvisioningException();
            using var descriptor = new NativeDescriptor(Descriptor(true));
            if (!SetFileSecurityW(path, 0x80000004, descriptor.Attributes.Descriptor)) throw new Win32Exception();
        }
        foreach (string kind in new[] { "Mappings", "Storage" })
        {
            using var key = OpenProfileKey(RegistryPath(kind), true)!;
            RequireRegistryShape(key, kind, false);
            using var child = OpenProfileKey(RegistryPath(kind) + @"\Children", true)!;
            RequireRegistryShape(child, kind, true);
            foreach (var owned in new[] { child, key })
            {
                var current = new RawSecurityDescriptor(RegistrySecurity(owned), 0);
                if (current.Owner != identity.User) throw new RendererProvisioningException();
                using var descriptor = new NativeDescriptor(RegistryDescriptor());
                int error = RegSetKeySecurity(owned.Handle, 0x80000004, descriptor.Attributes.Descriptor);
                if (error != 0) throw new Win32Exception(error);
            }
        }
    }
    private void RequireRegistryShape(RegistryKey key, string kind, bool child)
    {
        string[] values = !child && kind == "Mappings" ? ["DisplayName", "Moniker"] : [];
        if (!key.GetValueNames().Order(StringComparer.Ordinal).SequenceEqual(values.Order(StringComparer.Ordinal)) ||
            !key.GetSubKeyNames().SequenceEqual(child ? [] : new[] { "Children" })) throw new RendererProvisioningException();
        if (!child && kind == "Mappings" &&
            (key.GetValueKind("DisplayName") != RegistryValueKind.String || key.GetValueKind("Moniker") != RegistryValueKind.String ||
             !string.Equals(key.GetValue("DisplayName") as string, DisplayName, StringComparison.Ordinal) ||
             !string.Equals(key.GetValue("Moniker") as string, ProfileName, StringComparison.OrdinalIgnoreCase))) throw new RendererProvisioningException();
    }
    internal string CaptureProfileState()
    {
        var state = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in ProfilePaths())
        {
            RequireOwned(path, true);
            state["directory:" + path] = new DirectoryInfo(path).GetAccessControl(AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access).GetSecurityDescriptorSddlForm(AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access);
        }
        using var identity = WindowsIdentity.GetCurrent();
        var expected = new RawSecurityDescriptor(RegistryDescriptor());
        foreach (string kind in new[] { "Mappings", "Storage" })
        {
            using var key = OpenProfileKey(RegistryPath(kind), false)!;
            using var child = OpenProfileKey(RegistryPath(kind) + @"\Children", false)!;
            foreach (var entry in new[] { (Key: key, Child: false), (Key: child, Child: true) })
            {
                RequireRegistryShape(entry.Key, kind, entry.Child);
                byte[] security = RegistrySecurity(entry.Key);
                var observed = new RawSecurityDescriptor(security, 0);
                if (observed.Owner != identity.User || (observed.ControlFlags & ControlFlags.DiscretionaryAclProtected) == 0 || observed.DiscretionaryAcl is null ||
                    !AclBytes(observed.DiscretionaryAcl).SequenceEqual(AclBytes(expected.DiscretionaryAcl!))) throw new RendererProvisioningException();
                state[$"registry:{kind}:{entry.Child}"] = Convert.ToHexString(security);
                foreach (string value in entry.Key.GetValueNames()) state[$"value:{kind}:{value}"] = (string)entry.Key.GetValue(value)!;
            }
        }
        return JsonSerializer.Serialize(state); // Fixed empty directory tree and known mapping metadata only; no file/user payload reads.
    }
    internal void ValidateProfileBoundary()
    {
        if (!string.Equals(_profileState, CaptureProfileState(), StringComparison.Ordinal)) throw new RendererProvisioningException();
    }
    private static byte[] RegistrySecurity(RegistryKey key)
    {
        uint size = 0; int error = RegGetKeySecurity(key.Handle, 7, null, ref size);
        if (error != 122 || size is 0 or > 65536) throw new Win32Exception(error);
        byte[] security = new byte[size]; error = RegGetKeySecurity(key.Handle, 7, security, ref size);
        if (error != 0) throw new Win32Exception(error);
        return security;
    }
    private static RegistryKey? OpenProfileKey(string path, bool writableSecurity, bool allowAbsent = false)
    {
        // Open the link itself. A registry symbolic-link collision exposes unexpected
        // values/layout and is rejected; hardening must never follow it to another key.
        int error = RegOpenKeyExW(new IntPtr(unchecked((int)0x80000001)), path, 8, 0x20019u | (writableSecurity ? 0x40000u : 0), out var handle);
        if (error != 0)
        {
            handle?.Dispose();
            if (allowAbsent && error is 2 or 3) return null;
            throw new RendererProvisioningException();
        }
        try { return RegistryKey.FromHandle(handle, RegistryView.Default); }
        catch { handle.Dispose(); throw; }
    }
    internal string Descriptor(bool readable)
    {
        using var identity = WindowsIdentity.GetCurrent();
        string user = identity.User!.Value;
        return $"O:{user}G:{user}D:P(A;;FA;;;SY)(A;;FA;;;{user})" + (readable ? $"(A;;0x1200a9;;;{SidText})" : "");
    }
    internal void RequireOwned(string path, bool readable)
    {
        RendererWorkspace.RejectRedirects(path);
        FileSystemSecurity actual = Directory.Exists(path)
            ? new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group)
            : new FileInfo(path).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group);
        var expected = new RawSecurityDescriptor(Descriptor(readable));
        var observed = new RawSecurityDescriptor(actual.GetSecurityDescriptorBinaryForm(), 0);
        if (observed.Owner != expected.Owner || observed.Group != expected.Group ||
            (observed.ControlFlags & ControlFlags.DiscretionaryAclProtected) == 0 ||
            observed.DiscretionaryAcl is null || !AclBytes(observed.DiscretionaryAcl).SequenceEqual(AclBytes(expected.DiscretionaryAcl!)))
            throw new RendererProvisioningException();
    }
    private static byte[] AclBytes(RawAcl acl) { var bytes = new byte[acl.BinaryLength]; acl.GetBinaryForm(bytes, 0); return bytes; }
    internal void EnsureOwnedDirectory(string path, bool readable)
    {
        RendererWorkspace.RejectRedirects(path);
        using var parents = RendererWorkspace.PinAncestors(path);
        using var descriptor = new NativeDescriptor(Descriptor(readable));
        var sa = descriptor.Attributes;
        if (!CreateDirectoryW(path, ref sa) && Marshal.GetLastWin32Error() != 183) throw new Win32Exception();
        using var pin = RendererWorkspace.PinDirectory(path);
        RequireOwned(path, readable); // Validate existing resources, NEVER repair their ACLs.
    }
    internal SafeFileHandle CreateFile(string path, bool readable, uint access)
    {
        using var descriptor = new NativeDescriptor(Descriptor(readable));
        var sa = descriptor.Attributes;
        var file = CreateFileW(path, access, 1, ref sa, 1, 0x00200000, IntPtr.Zero);
        if (file.IsInvalid) { file.Dispose(); throw new Win32Exception(); }
        return file;
    }
    internal SafeFileHandle CreatePinnedDirectory(string path, bool readable)
    {
        using var descriptor = new NativeDescriptor(Descriptor(readable));
        string nativePath = @"\??\" + path;
        IntPtr text = Marshal.StringToHGlobalUni(nativePath), name = IntPtr.Zero;
        try
        {
            var unicode = new UnicodeString { Length = checked((ushort)(nativePath.Length * 2)), MaximumLength = checked((ushort)(nativePath.Length * 2 + 2)), Buffer = text };
            name = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>()); Marshal.StructureToPtr(unicode, name, false);
            var attributes = new ObjectAttributes { Length = Marshal.SizeOf<ObjectAttributes>(), ObjectName = name, Flags = 0x40, SecurityDescriptor = descriptor.Attributes.Descriptor };
            int status = NtCreateFile(out var file, 0x80110000, ref attributes, out _, IntPtr.Zero, 0x10, 1, 2, 0x00200021, IntPtr.Zero, 0);
            if (status < 0) { file?.Dispose(); throw new Win32Exception((int)RtlNtStatusToDosError(status)); }
            return file;
        }
        finally { if (name != IntPtr.Zero) Marshal.FreeHGlobal(name); Marshal.FreeHGlobal(text); }
    }
    public void Dispose()
    {
        foreach (var pin in _profilePins.AsEnumerable().Reverse()) pin.Dispose();
        _profilePins.Clear();
        if (Sid != IntPtr.Zero) { FreeSid(Sid); Sid = IntPtr.Zero; }
    }
    private sealed class NativeDescriptor : IDisposable
    {
        private IntPtr _value;
        internal SecurityAttributes Attributes => new() { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = _value };
        internal NativeDescriptor(string sddl)
        {
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, 1, out _value, out _)) throw new Win32Exception();
        }
        public void Dispose() { if (_value != IntPtr.Zero) LocalFree(_value); _value = IntPtr.Zero; }
    }
    [StructLayout(LayoutKind.Sequential)] internal struct SecurityAttributes { public int Length; public IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] public bool Inherit; }
    [StructLayout(LayoutKind.Sequential)] private struct UnicodeString { public ushort Length, MaximumLength; public IntPtr Buffer; }
    [StructLayout(LayoutKind.Sequential)] private struct ObjectAttributes { public int Length; public IntPtr RootDirectory, ObjectName; public uint Flags; public IntPtr SecurityDescriptor, SecurityQualityOfService; }
    [StructLayout(LayoutKind.Sequential)] private struct IoStatusBlock { public IntPtr Status; public nuint Information; }
    [DllImport("ntdll.dll")] private static extern int NtCreateFile(out SafeFileHandle file, uint access, ref ObjectAttributes attributes, out IoStatusBlock status, IntPtr allocationSize, uint fileAttributes, uint share, uint disposition, uint options, IntPtr ea, uint eaBytes);
    [DllImport("ntdll.dll")] private static extern uint RtlNtStatusToDosError(int status);
    [DllImport("userenv.dll", CharSet = CharSet.Unicode)] private static extern int CreateAppContainerProfile(string name, string display, string description, IntPtr capabilities, uint count, out IntPtr sid);
    [DllImport("userenv.dll", CharSet = CharSet.Unicode)] private static extern int DeriveAppContainerSidFromAppContainerName(string name, out IntPtr sid);
    [DllImport("userenv.dll", CharSet = CharSet.Unicode)] private static extern int GetAppContainerFolderPath(string sid, out IntPtr path);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetFileSecurityW(string path, uint information, IntPtr descriptor);
    [DllImport("advapi32.dll")] private static extern int RegSetKeySecurity(SafeRegistryHandle key, uint information, IntPtr descriptor);
    [DllImport("advapi32.dll")] private static extern int RegGetKeySecurity(SafeRegistryHandle key, uint information, byte[]? descriptor, ref uint size);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern int RegOpenKeyExW(IntPtr parent, string path, uint options, uint access, out SafeRegistryHandle key);
    [DllImport("advapi32.dll")] private static extern IntPtr FreeSid(IntPtr sid);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string text, uint revision, out IntPtr descriptor, out uint size);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateDirectoryW(string path, ref SecurityAttributes security);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, ref SecurityAttributes security, uint disposition, uint flags, IntPtr template);
}
internal sealed class RendererProvisioningException : IOException
{
    internal RendererProvisioningException() : base("Renderer resources require explicit provisioning") { }
}
