using System.Security.Cryptography;
using System.Text.Json;
using Axora.Studio.Models.ResumeRendering;
using Axora.Studio.Services.ResumeRendering;

namespace Axora.Studio.Services.Rendering;

// Exactly two roles. Neither a file dictionary nor a general template/source admission API.
internal sealed class TrustedRenderPackage
{
    internal const int DataLimit = 4 * 1024 * 1024;
    private readonly byte[] main, data;
    internal ResumeTemplateManifest Template { get; }
    internal ResumeResolvedFormatting Formatting { get; }
    internal string DocumentId { get; }
    internal long Revision { get; }
    internal string SnapshotSha256 { get; }
    internal string RenderDataSha256 { get; }
    internal string RenderJobSha256 { get; }
    internal int MainByteLength => main.Length;
    internal int DataByteLength => data.Length;
    private TrustedRenderPackage(ResumeRenderSnapshot snapshot, ResumeTemplate template, ResumeRenderFontChoice font)
    {
        Template = template.Manifest;
        Formatting = ResumeRenderFormatting.Resolve(snapshot, font);
        var canonical = ResumeRenderDataWriter.Write(snapshot, font);
        main = template.CopySource(); data = canonical.CopyBytes();
        if (main.Length != Template.SourceByteLength || main.Length is < 1 or > ResumeTemplateCatalog.SourceLimit ||
            Convert.ToHexString(SHA256.HashData(main)) != Template.TemplateSourceSha256 ||
            data.Length is < 1 or > DataLimit || Convert.ToHexString(SHA256.HashData(data)) != canonical.Sha256)
            throw new InvalidDataException("RenderPackageIntegrityFailure");
        DocumentId = snapshot.DocumentId; Revision = snapshot.Revision;
        SnapshotSha256 = snapshot.SnapshotSha256; RenderDataSha256 = canonical.Sha256;
        using var identity = new MemoryStream();
        using (var w = new Utf8JsonWriter(identity))
        {
            w.WriteStartObject(); w.WriteString("domain", "AXORA.Resume.RenderJob"); w.WriteNumber("version", 1);
            w.WriteString("templateId", Template.TemplateId); w.WriteNumber("templateVersion", Template.TemplateVersion);
            w.WriteString("templateSourceSha256", Template.TemplateSourceSha256);
            w.WriteString("snapshotSha256", SnapshotSha256); w.WriteString("renderDataSha256", RenderDataSha256);
            w.WriteString("fontChoice", "explicitEmbeddedSerifV1"); w.WriteString("fontProfile", Template.FontProfile);
            w.WriteEndObject();
        }
        RenderJobSha256 = Convert.ToHexString(SHA256.HashData(identity.ToArray()));
    }
    internal static TrustedRenderPackage Create(ResumeRenderSnapshot snapshot, ResumeTemplateChoice template, ResumeRenderFontChoice font)
        => new(snapshot, ResumeTemplateCatalog.Embedded.Resolve(template), font);
    internal byte[] CopyMain() => (byte[])main.Clone();
    internal byte[] CopyData() => (byte[])data.Clone();
}
