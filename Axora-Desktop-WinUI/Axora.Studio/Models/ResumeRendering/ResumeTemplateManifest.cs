using System.Collections.Immutable;

namespace Axora.Studio.Models.ResumeRendering;

internal enum ResumeTemplateChoice { SimpleV1, TechnicalV1 }
internal enum ResumeTemplateFailure { UnsupportedTemplate, InvalidPresentationSelection, TemplateIntegrityFailure, UnsupportedRenderContent }
internal sealed class ResumeTemplateException(ResumeTemplateFailure failure, string code) : Exception(code)
{
    internal ResumeTemplateFailure Failure { get; } = failure;
    internal string Code { get; } = code;
}
internal sealed record ResumeTemplateManifest(string TemplateId, int TemplateVersion, string DisplayName, string Category,
    string LogicalResourceName, int SourceByteLength, string TemplateSourceSha256)
{
    internal int RenderDataSchemaVersion => 1;
    internal string MinimumTypst => "0.15.1";
    internal int RendererCompatibilityContract => 1;
    internal string FontProfile => "LibertinusSerifRegularBoldV1";
    internal ImmutableArray<string> FormattingCapabilities => ["A4", "marginInches", "spacingMode", "accentRgb", "showDividers",
        "centerHeader", "uppercaseSectionTitles", "targetPagesHint", "sectionOrder", "visibility", "collectionOrder"];
}
