using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Axora.Studio.Tests")]

namespace Axora.Studio.Models.Rendering;

// Reviewed identity anchored to the official release API asset digest. An adjacent JSON file is never authority.
public static class RendererRuntimeManifest
{
    public const string RendererId = "typst";
    public const string Version = "0.15.1";
    public const string Platform = "win-x64";
    public const string ReleaseCommit = "9dfd3a08500b7896045f907433cf7b4b02434fad";
    public const long AssetId = 480303335;
    public const long ArchiveBytes = 22463684;
    public const long ExecutableBytes = 52487680;
    public const string ArchiveSha256 = "19CE3551153C2FE7EE9FA2F95208310C8F4D3209FEDB699E0333FAF8913F6736";
    public const string ExecutableSha256 = "081217A463ADB006F8894B44227FE4B9C9E91FC85F5463D5948E8370DB9BB31E";
    public const string VersionOutput = "typst 0.15.1 (9dfd3a08)";
    public const string AssetUrl = "https://github.com/typst/typst/releases/download/v0.15.1/typst-x86_64-pc-windows-msvc.zip";
    public const string License = "Apache-2.0";
    public const int TemplateCompatibilityVersion = 1;
    public const string UpdatePolicy = "Explicit provision only; no download, PATH discovery or self-update";
    public static string DefaultProvisioningRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Axora", "Studio", "Renderers");
    public static string ExecutablePath(string provisioningRoot) => Path.Combine(provisioningRoot, RendererId, Version, Platform, ExecutableSha256, "typst.exe");
}
