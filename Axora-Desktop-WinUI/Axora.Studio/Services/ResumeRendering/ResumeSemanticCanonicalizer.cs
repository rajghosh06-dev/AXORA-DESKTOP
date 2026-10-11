using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Axora.Studio.Models;
using Axora.Studio.Models.ResumeRendering;

namespace Axora.Studio.Services.ResumeRendering;

public static class ResumeSemanticCanonicalizer
{
    public static byte[] EncodeSemantic(ResumeRenderSnapshot snapshot) => snapshot.CopySemanticBytes();
    public static byte[] EncodeSnapshot(ResumeRenderSnapshot snapshot) => snapshot.CopySnapshotBytes();

    internal static byte[] EncodeSemantic(ResumeSnapshotDocument document)
    {
        var w = new ResumeCanonicalJson(ResumeRenderFailure.SnapshotLimitExceeded);
        w.Object(); w.Property("domain", "AXORA.Resume.Semantic"); w.Property("version", 1);
        w.Name("document"); Document(w, document, false); w.EndObject(); return w.Finish();
    }
    internal static byte[] EncodeSnapshot(string id, long revision, ResumeSnapshotDocument document)
    {
        var w = new ResumeCanonicalJson(ResumeRenderFailure.SnapshotLimitExceeded);
        w.Object(); w.Property("domain", "AXORA.Resume.Snapshot");
        w.Property("snapshotSchemaVersion", ResumeRenderLimits.SnapshotSchemaVersion);
        w.Property("derivationVersion", ResumeRenderLimits.DerivationVersion);
        w.Property("documentId", id); w.Property("revision", revision);
        w.Name("document"); Document(w, document, true); w.EndObject(); return w.Finish();
    }

    internal static void Document(ResumeCanonicalJson w, ResumeSnapshotDocument d, bool derived)
    {
        w.Object(); w.Property("resumeTitle", d.ResumeTitle);
        w.Name("header"); w.Object();
        var h = d.Header;
        w.Property("fullName", h.FullName); w.Property("professionalTitle", h.ProfessionalTitle);
        w.Property("email", h.Email); w.Property("phone", h.Phone); w.Property("location", h.Location);
        w.Property("linkedIn", h.LinkedIn); w.Property("linkedInUrl", h.LinkedInUrl);
        w.Property("gitHub", h.GitHub); w.Property("gitHubUrl", h.GitHubUrl); w.Property("portfolioUrl", h.PortfolioUrl); w.EndObject();
        w.Property("summary", d.Summary);
        w.Name("formatting"); w.Object(); var f = d.Formatting;
        w.Property("targetLength", TargetToken(f.TargetLength)); w.Property("fontFamily", FontToken(f.FontFamily));
        w.Property("spacingMode", DensityToken(f.SpacingMode)); w.Property("marginInches", f.MarginInches);
        w.Property("showDividers", f.ShowDividers); w.Property("centerHeader", f.CenterHeader);
        w.Property("uppercaseSectionTitles", f.UppercaseSectionTitles); w.Property("accentHexColor", f.AccentHexColor); w.EndObject();
        w.Name("education"); w.Array(); foreach (var x in d.Education)
        {
            w.Object(); w.Property("id", x.Id); w.Property("institution", x.Institution); w.Property("scoreOrPercentage", x.ScoreOrPercentage);
            w.Property("degree", x.Degree); w.Property("specialization", x.Specialization); w.Property("yearRange", x.YearRange); w.EndObject();
        } w.EndArray();
        w.Name("experiences"); w.Array(); foreach (var x in d.Experiences)
        {
            w.Object(); w.Property("id", x.Id); w.Property("company", x.Company); w.Property("roleTitle", x.RoleTitle); w.Property("location", x.Location);
            w.Property("startDate", x.StartDate); w.Property("endDate", x.EndDate); w.Property("isCurrent", x.IsCurrent);
            w.Property("bulletsRaw", x.BulletsRaw); w.Property("projectLink", x.ProjectLink);
            if (derived) { Strings(w, "bulletLines", x.BulletLines); w.Property("displayEndDate", x.DisplayEndDate); }
            w.EndObject();
        } w.EndArray();
        w.Name("skillCategories"); w.Array(); foreach (var x in d.SkillCategories)
        {
            w.Object(); w.Property("id", x.Id); w.Property("categoryName", x.CategoryName); w.Property("skillsCsv", x.SkillsCsv);
            if (derived) Strings(w, "entries", x.Entries); w.EndObject();
        } w.EndArray();
        w.Name("projects"); w.Array(); foreach (var x in d.Projects)
        {
            w.Object(); w.Property("id", x.Id); w.Property("title", x.Title); w.Property("techStack", x.TechStack);
            w.Property("dateRange", x.DateRange); w.Property("bulletsRaw", x.BulletsRaw); w.Property("repoUrl", x.RepoUrl);
            if (derived) Strings(w, "bulletLines", x.BulletLines); w.EndObject();
        } w.EndArray();
        w.Name("certifications"); w.Array(); foreach (var x in d.Certifications)
        {
            w.Object(); w.Property("id", x.Id); w.Property("title", x.Title); w.Property("issuer", x.Issuer); w.Property("date", x.Date);
            w.Property("gradeOrScore", x.GradeOrScore); w.Property("credentialId", x.CredentialId); w.Property("description", x.Description);
            w.Property("verificationUrl", x.VerificationUrl); w.EndObject();
        } w.EndArray();
        w.Name("achievements"); w.Array(); foreach (var x in d.Achievements)
        {
            w.Object(); w.Property("id", x.Id); w.Property("title", x.Title); w.Property("category", x.Category); w.Property("date", x.Date);
            w.Property("description", x.Description); w.Property("link", x.Link); w.EndObject();
        } w.EndArray();
        w.Name("responsibilities"); w.Array(); foreach (var x in d.Responsibilities)
        {
            w.Object(); w.Property("id", x.Id); w.Property("role", x.Role); w.Property("organization", x.Organization);
            w.Property("dateRange", x.DateRange); w.Property("bulletsRaw", x.BulletsRaw);
            if (derived) Strings(w, "bulletLines", x.BulletLines); w.EndObject();
        } w.EndArray();
        w.Name("visibility"); w.Object(); var v = d.Visibility;
        w.Property("summary", v.Summary); w.Property("education", v.Education); w.Property("experience", v.Experience);
        w.Property("skills", v.Skills); w.Property("projects", v.Projects); w.Property("certifications", v.Certifications);
        w.Property("achievements", v.Achievements); w.Property("responsibilities", v.Responsibilities); w.EndObject();
        Sections(w, "sectionOrder", d.SectionOrder);
        if (derived) Sections(w, "visibleSectionOrder", d.VisibleSectionOrder);
        w.EndObject();
    }
    private static void Strings(ResumeCanonicalJson w, string name, ImmutableArray<string> values)
    { w.Name(name); w.Array(); foreach (string value in values) w.String(value); w.EndArray(); }
    private static void Sections(ResumeCanonicalJson w, string name, ImmutableArray<ResumeSection> values)
    { w.Name(name); w.Array(); foreach (var value in values) w.String(SectionToken(value)); w.EndArray(); }
    internal static string SectionToken(ResumeSection value) => value switch
    {
        ResumeSection.Summary => "summary", ResumeSection.Education => "education", ResumeSection.Experience => "experience",
        ResumeSection.Skills => "skills", ResumeSection.Projects => "projects", ResumeSection.Certifications => "certifications",
        ResumeSection.Achievements => "achievements", ResumeSection.Responsibilities => "responsibilities", _ => throw new InvalidOperationException("Validated section required.")
    };
    internal static string TargetToken(ResumeTargetLength value) => value switch
    {
        ResumeTargetLength.OnePage => "onePage", ResumeTargetLength.TwoPages => "twoPages", ResumeTargetLength.ThreePages => "threePages",
        ResumeTargetLength.FourPlusPages => "fourPlusPages", _ => throw new InvalidOperationException("Validated target required.")
    };
    internal static string FontToken(ResumeFontPreference value) => value switch
    {
        ResumeFontPreference.SegoeUI => "segoeUI", ResumeFontPreference.Calibri => "calibri", ResumeFontPreference.Arial => "arial",
        ResumeFontPreference.TimesNewRoman => "timesNewRoman", ResumeFontPreference.Georgia => "georgia", _ => throw new InvalidOperationException("Validated font required.")
    };
    internal static string DensityToken(ResumeDensity value) => value switch
    {
        ResumeDensity.Compact => "compact", ResumeDensity.Standard => "standard", ResumeDensity.Relaxed => "relaxed",
        _ => throw new InvalidOperationException("Validated density required.")
    };
}

// Version 1 encoding: printable ASCII literal except quote/backslash; every other UTF-16
// unit is uppercase \uXXXX (valid surrogate pairs retained). No BOM/whitespace. Numeric
// doubles use invariant .NET "R"; integer decimal has no leading zero. Not RFC 8785.
internal sealed class ResumeCanonicalJson
{
    private readonly MemoryStream buffer = new();
    private readonly ResumeRenderFailure failure;
    private readonly Stack<Frame> frames = new();
    private bool rootWritten;
    private sealed class Frame(bool array) { public bool Array = array; public int Count; public bool Pending; }
    internal ResumeCanonicalJson(ResumeRenderFailure failure) { this.failure = failure; }
    private void Raw(string value)
    {
        if (value.Length > ResumeRenderLimits.CanonicalBytes - buffer.Length)
            throw new ResumeRenderException(failure, failure == ResumeRenderFailure.RenderDataLimitExceeded ? "RenderDataLimitExceeded" : "SnapshotLimitExceeded");
        foreach (char c in value) buffer.WriteByte(checked((byte)c)); // All emitted syntax is ASCII.
    }
    private void Prefix()
    {
        if (frames.Count == 0) { if (rootWritten) throw new InvalidOperationException(); rootWritten = true; return; }
        var f = frames.Peek();
        if (f.Array) { if (f.Count++ > 0) Raw(","); }
        else { if (!f.Pending) throw new InvalidOperationException(); f.Pending = false; }
    }
    internal void Object() { Prefix(); Raw("{"); frames.Push(new(false)); }
    internal void Array() { Prefix(); Raw("["); frames.Push(new(true)); }
    internal void EndObject() { var f = frames.Pop(); if (f.Array || f.Pending) throw new InvalidOperationException(); Raw("}"); }
    internal void EndArray() { if (!frames.Pop().Array) throw new InvalidOperationException(); Raw("]"); }
    internal void Name(string name)
    {
        var f = frames.Peek(); if (f.Array || f.Pending) throw new InvalidOperationException();
        if (f.Count++ > 0) Raw(","); Quoted(name); Raw(":"); f.Pending = true;
    }
    private void Quoted(string value)
    {
        Raw("\"");
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsHighSurrogate(c) && (i + 1 == value.Length || !char.IsLowSurrogate(value[i + 1])) ||
                char.IsLowSurrogate(c) && (i == 0 || !char.IsHighSurrogate(value[i - 1])))
                throw new ResumeRenderException(ResumeRenderFailure.InvalidResumeInput, "InvalidUnicode");
            if (c == '"') Raw("\\\"");
            else if (c == '\\') Raw("\\\\");
            else if (c < 0x20 || c > 0x7E) Raw("\\u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture));
            else Raw(c.ToString());
        }
        Raw("\"");
    }
    internal void String(string value) { Prefix(); Quoted(value); }
    internal void Property(string name, string value) { Name(name); String(value); }
    internal void Property(string name, long value) { Name(name); Prefix(); Raw(value.ToString(CultureInfo.InvariantCulture)); }
    internal void Property(string name, double value)
    { if (!double.IsFinite(value)) throw new InvalidOperationException(); Name(name); Prefix(); Raw(value.ToString("R", CultureInfo.InvariantCulture)); }
    internal void Property(string name, bool value) { Name(name); Prefix(); Raw(value ? "true" : "false"); }
    internal void Null(string name) { Name(name); Prefix(); Raw("null"); }
    internal byte[] Finish() { if (!rootWritten || frames.Count != 0) throw new InvalidOperationException(); return buffer.ToArray(); }
}
