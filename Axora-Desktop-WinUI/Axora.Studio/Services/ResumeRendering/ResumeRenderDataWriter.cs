using Axora.Studio.Models.ResumeRendering;

namespace Axora.Studio.Services.ResumeRendering;

public static class ResumeRenderDataWriter
{
    public static ResumeCanonicalData Write(ResumeRenderSnapshot snapshot,
        ResumeRenderFontChoice fontChoice = ResumeRenderFontChoice.HonorSavedPreference)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var f = ResumeRenderFormatting.Resolve(snapshot, fontChoice);
        var w = new ResumeCanonicalJson(ResumeRenderFailure.RenderDataLimitExceeded);
        w.Object(); w.Property("domain", "AXORA.Resume.RenderData");
        w.Property("renderDataSchemaVersion", ResumeRenderLimits.RenderDataSchemaVersion);
        w.Property("expectedContentPolicyVersion", ResumeRenderLimits.ExpectedContentPolicyVersion);
        w.Property("snapshotSchemaVersion", snapshot.SnapshotSchemaVersion); w.Property("derivationVersion", snapshot.DerivationVersion);
        w.Property("documentId", snapshot.DocumentId); w.Property("revision", snapshot.Revision);
        w.Property("semanticSha256", snapshot.SemanticSha256); w.Property("snapshotSha256", snapshot.SnapshotSha256);
        w.Name("formattingIntent"); w.Object();
        w.Property("fontChoice", "explicitEmbeddedSerifV1");
        w.Property("requestedFont", ResumeSemanticCanonicalizer.FontToken(f.RequestedFont));
        w.Property("intendedEffectiveFamily", f.IntendedEffectiveFamily); w.Property("requiresFontResolutionProof", f.RequiresFontResolutionProof);
        if (f.TargetPages.HasValue) w.Property("targetPages", (long)f.TargetPages.Value); else w.Null("targetPages");
        w.Property("spacingMode", ResumeSemanticCanonicalizer.DensityToken(f.SpacingMode));
        w.Property("bodyPoints", f.BodyPoints); w.Property("leadingEm", f.LeadingEm); w.Property("itemGapEm", f.ItemGapEm);
        w.Property("marginInches", f.MarginInches); w.Property("showDividers", f.ShowDividers); w.Property("centerHeader", f.CenterHeader);
        w.Property("uppercaseSectionTitles", f.UppercaseSectionTitles);
        w.Property("accentRed", (long)f.AccentRed); w.Property("accentGreen", (long)f.AccentGreen); w.Property("accentBlue", (long)f.AccentBlue);
        w.EndObject(); w.Name("document"); ResumeSemanticCanonicalizer.Document(w, snapshot.Document, true); w.EndObject();
        return new(w.Finish());
    }
}
