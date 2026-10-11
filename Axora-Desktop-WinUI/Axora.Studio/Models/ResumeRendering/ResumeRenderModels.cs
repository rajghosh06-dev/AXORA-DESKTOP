using System.Collections.Immutable;
using System.Security.Cryptography;

namespace Axora.Studio.Models.ResumeRendering;

public enum ResumeRenderFailure { InvalidResumeInput, SnapshotLimitExceeded, RenderDataLimitExceeded, UnsupportedFormattingPreference, InvalidFormattingSelection }
public enum ResumeRenderFontChoice { HonorSavedPreference, ExplicitEmbeddedSerifV1 }

public sealed class ResumeRenderException : Exception
{
    public ResumeRenderFailure Failure { get; }
    public string Code { get; }
    internal ResumeRenderException(ResumeRenderFailure failure, string code) : base(code) { Failure = failure; Code = code; }
}

// These are B2-owned values. No persistence record or collection is retained.
public sealed record ResumeSnapshotHeader(string FullName, string ProfessionalTitle, string Email, string Phone,
    string Location, string LinkedIn, string LinkedInUrl, string GitHub, string GitHubUrl, string PortfolioUrl);
public sealed record ResumeSnapshotPreferences(ResumeTargetLength TargetLength, ResumeFontPreference FontFamily,
    ResumeDensity SpacingMode, double MarginInches, bool ShowDividers, bool CenterHeader,
    bool UppercaseSectionTitles, string AccentHexColor);
public sealed record ResumeSnapshotEducation(string Id, string Institution, string ScoreOrPercentage, string Degree, string Specialization, string YearRange);
public sealed record ResumeSnapshotExperience(string Id, string Company, string RoleTitle, string Location, string StartDate,
    string EndDate, bool IsCurrent, string BulletsRaw, string ProjectLink, ImmutableArray<string> BulletLines, string DisplayEndDate);
public sealed record ResumeSnapshotSkill(string Id, string CategoryName, string SkillsCsv, ImmutableArray<string> Entries);
public sealed record ResumeSnapshotProject(string Id, string Title, string TechStack, string DateRange, string BulletsRaw,
    string RepoUrl, ImmutableArray<string> BulletLines);
public sealed record ResumeSnapshotCertification(string Id, string Title, string Issuer, string Date, string GradeOrScore,
    string CredentialId, string Description, string VerificationUrl);
public sealed record ResumeSnapshotAchievement(string Id, string Title, string Category, string Date, string Description, string Link);
public sealed record ResumeSnapshotResponsibility(string Id, string Role, string Organization, string DateRange,
    string BulletsRaw, ImmutableArray<string> BulletLines);
public sealed record ResumeSnapshotVisibility(bool Summary, bool Education, bool Experience, bool Skills, bool Projects,
    bool Certifications, bool Achievements, bool Responsibilities);
public sealed record ResumeSnapshotDocument(string ResumeTitle, ResumeSnapshotHeader Header, string Summary,
    ResumeSnapshotPreferences Formatting, ImmutableArray<ResumeSnapshotEducation> Education,
    ImmutableArray<ResumeSnapshotExperience> Experiences, ImmutableArray<ResumeSnapshotSkill> SkillCategories,
    ImmutableArray<ResumeSnapshotProject> Projects, ImmutableArray<ResumeSnapshotCertification> Certifications,
    ImmutableArray<ResumeSnapshotAchievement> Achievements, ImmutableArray<ResumeSnapshotResponsibility> Responsibilities,
    ResumeSnapshotVisibility Visibility, ImmutableArray<ResumeSection> SectionOrder, ImmutableArray<ResumeSection> VisibleSectionOrder);

// Successful resolution here declares a finite formatting INTENT, never runtime/font availability.
public sealed record ResumeResolvedFormatting(ResumeRenderFontChoice FontChoice, ResumeFontPreference RequestedFont,
    string IntendedEffectiveFamily, bool RequiresFontResolutionProof, int? TargetPages, ResumeDensity SpacingMode,
    double BodyPoints, double LeadingEm, double ItemGapEm, double MarginInches, bool ShowDividers,
    bool CenterHeader, bool UppercaseSectionTitles, byte AccentRed, byte AccentGreen, byte AccentBlue);

public sealed class ResumeCanonicalData
{
    private readonly byte[] bytes;
    public int ByteLength => bytes.Length;
    public string Sha256 { get; }
    internal ResumeCanonicalData(byte[] ownedBytes)
    {
        bytes = ownedBytes; // Only internal bounded writers create this buffer; callers receive copies.
        Sha256 = Convert.ToHexString(SHA256.HashData(bytes));
    }
    public byte[] CopyBytes() => (byte[])bytes.Clone();
}
