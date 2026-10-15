using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Axora.PdfValidation;
using Axora.Studio.Services.Rendering;

namespace Axora.Studio.Services.ResumeRendering;

internal sealed record PdfWorkerFile(string Name, long Bytes, string Sha256);

internal sealed class PdfWorkerManifest
{
    internal const string ResourceName = "Axora.PdfValidation.WorkerBundle.v1.json";
    internal const string ExecutableName = "Axora.PdfValidation.Worker.exe";
    internal const int FileLimit = 512, ManifestLimit = 128 * 1024;
    internal const long ByteLimit = 256L * 1024 * 1024;
    internal ImmutableArray<PdfWorkerFile> Files { get; }
    internal string Sha256 { get; }
    private PdfWorkerManifest(byte[] bytes)
    {
        if (bytes.Length is < 1 or > ManifestLimit) throw Invalid();
        using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 6 });
        var root = doc.RootElement;
        RequireFields(root, ["domain", "version", "targetFramework", "runtimeIdentifier", "runtimeFrameworkVersion", "windowsSdkPackageVersion", "fileCount", "totalBytes", "files"]);
        if (root.GetProperty("domain").GetString() != "AXORA.PdfValidation.WorkerBundle" || root.GetProperty("version").GetInt32() != 1 ||
            root.GetProperty("targetFramework").GetString() != "net9.0-windows10.0.26100.0" || root.GetProperty("runtimeIdentifier").GetString() != "win-x64" ||
            root.GetProperty("runtimeFrameworkVersion").GetString() != "9.0.20" || root.GetProperty("windowsSdkPackageVersion").GetString() != "10.0.26100.57") throw Invalid();
        var entries = root.GetProperty("files");
        if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() is < 1 or > FileLimit) throw Invalid();
        var files = ImmutableArray.CreateBuilder<PdfWorkerFile>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? previous = null; long total = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            RequireFields(entry, ["name", "bytes", "sha256"]);
            string name = entry.GetProperty("name").GetString() ?? "", hash = entry.GetProperty("sha256").GetString() ?? "";
            long length = entry.GetProperty("bytes").GetInt64();
            if (!SafeName(name) || !names.Add(name) || previous is not null && StringComparer.Ordinal.Compare(previous, name) >= 0 ||
                length is < 0 or > ByteLimit || !PdfValidationProtocol.IsHash(hash)) throw Invalid();
            total = checked(total + length); previous = name; files.Add(new(name, length, hash));
        }
        if (total > ByteLimit || root.GetProperty("fileCount").GetInt32() != files.Count || root.GetProperty("totalBytes").GetInt64() != total ||
            !names.Contains(ExecutableName) || !names.Contains("Axora.PdfValidation.Worker.dll") || !names.Contains("PdfPig-LICENSE.txt")) throw Invalid();
        Files = files.ToImmutable(); Sha256 = PdfValidationProtocol.Hash(bytes);
    }
    internal static PdfWorkerManifest LoadEmbedded()
    {
        using var stream = typeof(PdfWorkerManifest).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException("PdfWorkerEmbeddedManifestMissing");
        using var bytes = new MemoryStream();
        byte[] buffer = new byte[4096]; int count;
        while ((count = stream.Read(buffer)) != 0)
        { if (bytes.Length + count > ManifestLimit) throw Invalid(); bytes.Write(buffer, 0, count); }
        return new(bytes.ToArray());
    }
    internal static bool SafeName(string name) => Regex.IsMatch(name, @"\A[A-Za-z0-9][A-Za-z0-9._-]{0,127}\z") && !name.EndsWith('.') &&
        !Regex.IsMatch(name.Split('.')[0], @"\A(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static void RequireFields(JsonElement element, string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.EnumerateObject().Select(p => p.Name).SequenceEqual(expected, StringComparer.Ordinal)) throw Invalid();
    }
    private static InvalidDataException Invalid() => new("PdfWorkerEmbeddedManifestRejected");
}

// No scan/PATH/adjacent manifest, no automatic provisioning or repair on startup.
internal sealed class PdfValidationWorkerRuntime
{
    private static readonly Lazy<PdfWorkerManifest> Authority = new(PdfWorkerManifest.LoadEmbedded);
    internal static PdfWorkerManifest Manifest => Authority.Value;
    internal static string DefaultBase => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AXORA", "Studio", "PdfValidation");
    internal string Root { get; }
    internal PdfValidationWorkerRuntime() : this(Path.Combine(DefaultBase, "1", "win-x64", Manifest.Sha256)) { }
    internal PdfValidationWorkerRuntime(string ownedRoot)
    { RendererWorkspace.RejectRedirects(ownedRoot); Root = Path.GetFullPath(ownedRoot); }

    internal PdfWorkerBundleLease Verify(WindowsRendererSandbox sandbox) => PdfWorkerBundleLease.Open(Root, sandbox, requireOwned: true);

    // Explicit installer/test-owned operation; production host never invokes it.
    // An existing destination, even byte-identical, is never adopted or repaired.
    internal static void Provision(string source, string destination, WindowsRendererSandbox sandbox)
    {
        RendererWorkspace.RejectRedirects(destination);
        using var parents = RendererWorkspace.PinAncestors(destination);
        if (!RendererWorkspace.NamespaceAbsent(destination)) throw new IOException("PdfWorkerProvisioningRequiresFreshDestination");
        using var input = PdfWorkerBundleLease.Open(source, sandbox, requireOwned: false);
        using (sandbox.CreatePinnedDirectory(destination, true))
        {
            foreach (var entry in Manifest.Files)
            {
                using var output = new FileStream(sandbox.CreateFile(Path.Combine(destination, entry.Name), true, 0x40000000), FileAccess.Write);
                input.CopyFile(entry.Name, output); output.Flush(flushToDisk: true);
            }
        }
        using var verified = new PdfValidationWorkerRuntime(destination).Verify(sandbox);
    }
}

internal sealed class PdfWorkerBundleLease : IDisposable
{
    private readonly WindowsRendererSandbox sandbox;
    private readonly bool requireOwned;
    private readonly Dictionary<string, FileStream> files = new(StringComparer.Ordinal);
    private IDisposable? parents;
    internal string Root { get; }
    internal string Executable => Path.Combine(Root, PdfWorkerManifest.ExecutableName);
    private PdfWorkerBundleLease(string root, WindowsRendererSandbox sandbox, bool requireOwned)
    { Root = root; this.sandbox = sandbox; this.requireOwned = requireOwned; }
    internal static PdfWorkerBundleLease Open(string root, WindowsRendererSandbox sandbox, bool requireOwned)
    {
        RendererWorkspace.RejectRedirects(root);
        var lease = new PdfWorkerBundleLease(Path.GetFullPath(root), sandbox, requireOwned);
        try
        {
            // Pin root as well as every ancestor; deny file writes/deletes until settlement.
            lease.parents = RendererWorkspace.PinAncestors(Path.Combine(lease.Root, PdfWorkerManifest.ExecutableName));
            lease.RequireSet();
            foreach (var entry in PdfValidationWorkerRuntime.Manifest.Files)
                lease.files.Add(entry.Name, new FileStream(TypstRuntimeVerifier.OpenPinnedFile(Path.Combine(lease.Root, entry.Name)), FileAccess.Read));
            lease.VerifyAgain();
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }
    private void RequireSet()
    {
        RendererWorkspace.RejectRedirects(Root);
        if (requireOwned) sandbox.RequireOwned(Root, true);
        string[] paths = Directory.EnumerateFileSystemEntries(Root).Take(PdfWorkerManifest.FileLimit + 1).ToArray();
        if (paths.Length != PdfValidationWorkerRuntime.Manifest.Files.Length ||
            !paths.Select(Path.GetFileName).Order(StringComparer.Ordinal).SequenceEqual(PdfValidationWorkerRuntime.Manifest.Files.Select(f => f.Name), StringComparer.Ordinal))
            throw new InvalidDataException("PdfWorkerBundleFileSetRejected");
        foreach (string path in paths)
        {
            RendererWorkspace.RejectRedirects(path);
            if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) throw new InvalidDataException("PdfWorkerBundleEntryRejected");
        }
    }
    internal void VerifyAgain()
    {
        RequireSet();
        foreach (var entry in PdfValidationWorkerRuntime.Manifest.Files)
        {
            string path = Path.Combine(Root, entry.Name);
            if (requireOwned) sandbox.RequireOwned(path, true);
            var file = files[entry.Name]; file.Position = 0;
            if (file.Length != entry.Bytes || Convert.ToHexString(SHA256.HashData(file)) != entry.Sha256) throw new InvalidDataException("PdfWorkerBundleIdentityRejected");
            file.Position = 0;
        }
    }
    internal void CopyFile(string name, Stream destination)
    {
        var file = files[name]; file.Position = 0; file.CopyTo(destination); file.Position = 0;
    }
    public void Dispose()
    { foreach (var file in files.Values) file.Dispose(); files.Clear(); parents?.Dispose(); parents = null; }
}
