using System.Security.Cryptography;
using Axora.Studio.Models.Rendering;
using Axora.Studio.Services.Rendering;

namespace Axora.Studio.Models.ResumeRendering;

// Private B2C input seam only. Native compile success confers no independent PDF authority.
internal sealed class UnvalidatedResumePdf
{
    private readonly byte[] bytes;
    internal string ArtifactStatus => "UNVALIDATED PDF CANDIDATE";
    internal Guid OperationId { get; }
    internal string DocumentId { get; }
    internal long Revision { get; }
    internal string SnapshotSha256 { get; }
    internal string TemplateId { get; }
    internal int TemplateVersion { get; }
    internal string TemplateSourceSha256 { get; }
    internal string RenderDataSha256 { get; }
    internal string RenderJobSha256 { get; }
    internal string RendererId => RendererRuntimeManifest.RendererId;
    internal string RendererVersion => RendererRuntimeManifest.Version;
    internal string RendererExecutableSha256 => RendererRuntimeManifest.ExecutableSha256;
    internal ResumeRenderFontChoice FontChoice { get; }
    internal string RequestedFont { get; }
    internal string EffectiveFontProfile { get; }
    internal string PdfSha256 { get; }
    internal int PdfByteCount => bytes.Length;
    internal UnvalidatedResumePdf(Guid operation, TrustedRenderPackage package, byte[] nativeBytes)
    {
        if (nativeBytes.Length is < 1 or > RendererLimits.Pdf) throw new InvalidDataException("CandidateByteLimit");
        bytes = (byte[])nativeBytes.Clone();
        PdfSha256 = Convert.ToHexString(SHA256.HashData(bytes)); OperationId = operation;
        DocumentId = package.DocumentId; Revision = package.Revision; SnapshotSha256 = package.SnapshotSha256;
        TemplateId = package.Template.TemplateId; TemplateVersion = package.Template.TemplateVersion;
        TemplateSourceSha256 = package.Template.TemplateSourceSha256; RenderDataSha256 = package.RenderDataSha256;
        RenderJobSha256 = package.RenderJobSha256; FontChoice = package.Formatting.FontChoice;
        RequestedFont = package.Formatting.RequestedFont.ToString(); EffectiveFontProfile = package.Template.FontProfile;
    }
    internal byte[] CopyBytesForValidation() => (byte[])bytes.Clone();
}
