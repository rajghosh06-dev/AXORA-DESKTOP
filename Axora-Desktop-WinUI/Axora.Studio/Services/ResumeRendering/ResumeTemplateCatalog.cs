using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using Axora.Studio.Models.ResumeRendering;

namespace Axora.Studio.Services.ResumeRendering;

// Resource names and identities are application constants, never derived from Resume values.
internal sealed class ResumeTemplateCatalog
{
    internal const int SourceLimit = 64 * 1024;
    private readonly ImmutableArray<ResumeTemplate> templates;
    internal static ImmutableArray<ResumeTemplateManifest> Manifests { get; } = [
        new("axora.simple", 1, "Simple", "General", "Axora.Studio.Templates.Resume.SimpleV1.typ", 4517,
            "02EB1F658B5BC682A69903941D7B58BBBD8C00E24C09BE251C62055A39254805"),
        new("axora.technical", 1, "Technical", "Technical", "Axora.Studio.Templates.Resume.TechnicalV1.typ", 4545,
            "E4AC679E817B4C9EA87142477525AB19B470980D6F718F3B4A4FB9D48DAB6B26")];
    private static readonly Lazy<ResumeTemplateCatalog> EmbeddedValue = new(() => new(
        typeof(ResumeTemplateCatalog).Assembly.GetManifestResourceNames(),
        name => typeof(ResumeTemplateCatalog).Assembly.GetManifestResourceStream(name)));
    internal static ResumeTemplateCatalog Embedded => EmbeddedValue.Value;
    internal ResumeTemplateCatalog(IEnumerable<string> resourceNames, Func<string, Stream?> open)
    {
        string[] names = resourceNames.Where(n => n.StartsWith("Axora.Studio.Templates.Resume.", StringComparison.Ordinal)).ToArray();
        string[] expected = Manifests.Select(m => m.LogicalResourceName).ToArray();
        if (names.Length != 2 || names.Distinct(StringComparer.Ordinal).Count() != 2 ||
            !names.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)))
            throw Integrity();
        templates = Manifests.Select(m => {
            using var stream = open(m.LogicalResourceName) ?? throw Integrity();
            if (m.SourceByteLength is < 1 or > SourceLimit) throw Integrity();
            byte[] bytes = new byte[m.SourceByteLength];
            try { stream.ReadExactly(bytes); }
            catch (IOException) { throw Integrity(); }
            if (stream.ReadByte() != -1 || Convert.ToHexString(SHA256.HashData(bytes)) != m.TemplateSourceSha256) throw Integrity();
            return new ResumeTemplate(m, bytes);
        }).ToImmutableArray();
    }
    internal ImmutableArray<ResumeTemplateManifest> Entries => Manifests;
    internal ResumeTemplate Resolve(ResumeTemplateChoice choice) => choice switch {
        ResumeTemplateChoice.SimpleV1 => templates[0], ResumeTemplateChoice.TechnicalV1 => templates[1],
        _ => throw new ResumeTemplateException(ResumeTemplateFailure.UnsupportedTemplate, "UnsupportedTemplate") };
    internal ResumeTemplateChoice ResolvePersisted(string? id, int? version)
    {
        if (id is null && version is null) return ResumeTemplateChoice.SimpleV1;
        if (id is null || version is null) throw new ResumeTemplateException(ResumeTemplateFailure.InvalidPresentationSelection, "UnpairedTemplateSelection");
        return (id, version) switch {
            ("axora.simple", 1) => ResumeTemplateChoice.SimpleV1, ("axora.technical", 1) => ResumeTemplateChoice.TechnicalV1,
            _ => throw new ResumeTemplateException(ResumeTemplateFailure.UnsupportedTemplate, "UnsupportedTemplate") };
    }
    private static ResumeTemplateException Integrity() => new(ResumeTemplateFailure.TemplateIntegrityFailure, "TemplateIntegrityFailure");
}
internal sealed class ResumeTemplate
{
    private readonly byte[] source;
    internal ResumeTemplateManifest Manifest { get; }
    internal ResumeTemplate(ResumeTemplateManifest manifest, byte[] bytes) { Manifest = manifest; source = (byte[])bytes.Clone(); }
    internal byte[] CopySource() => (byte[])source.Clone();
}
