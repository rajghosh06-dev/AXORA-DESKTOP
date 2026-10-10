using System.Collections.Immutable;

namespace Axora.Studio.Models;

public enum ResumeTargetLength { OnePage, TwoPages, ThreePages, FourPlusPages }
public enum ResumeFontPreference { SegoeUI, Calibri, Arial, TimesNewRoman, Georgia }
public enum ResumeDensity { Compact, Standard, Relaxed }
public enum ResumeSection { Summary, Education, Experience, Skills, Projects, Certifications, Achievements, Responsibilities }

// Persistence records contain semantic data only. ImmutableArray deserializes with populated entries.
public sealed record ResumeDocument
{
    public string ResumeTitle { get; init; } = "Untitled Resume";
    public ResumeHeader Header { get; init; } = new();
    public string Summary { get; init; } = "";
    public ResumePreferences Formatting { get; init; } = new();
    public ImmutableArray<ResumeEducation> Education { get; init; } = [];
    public ImmutableArray<ResumeExperience> Experiences { get; init; } = [];
    public ImmutableArray<ResumeSkill> SkillCategories { get; init; } = [];
    public ImmutableArray<ResumeProject> Projects { get; init; } = [];
    public ImmutableArray<ResumeCertification> Certifications { get; init; } = [];
    public ImmutableArray<ResumeAchievement> Achievements { get; init; } = [];
    public ImmutableArray<ResumeResponsibility> Responsibilities { get; init; } = [];
    public bool ShowSummary { get; init; } = true;
    public bool ShowEducation { get; init; } = true;
    public bool ShowExperience { get; init; } = true;
    public bool ShowSkills { get; init; } = true;
    public bool ShowProjects { get; init; } = true;
    public bool ShowCertifications { get; init; } = true;
    public bool ShowAchievements { get; init; } = true;
    public bool ShowResponsibilities { get; init; } = true;
    public ImmutableArray<ResumeSection> SectionOrder { get; init; } = [.. Enum.GetValues<ResumeSection>()];
}
public sealed record ResumeHeader
{
    public string FullName { get; init; } = "";
    public string ProfessionalTitle { get; init; } = "";
    public string Email { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Location { get; init; } = "";
    public string LinkedIn { get; init; } = "";
    public string LinkedInUrl { get; init; } = "";
    public string GitHub { get; init; } = "";
    public string GitHubUrl { get; init; } = "";
    public string PortfolioUrl { get; init; } = "";
}
public sealed record ResumePreferences
{
    public ResumeTargetLength TargetLength { get; init; } = ResumeTargetLength.OnePage;
    public ResumeFontPreference FontFamily { get; init; } = ResumeFontPreference.SegoeUI;
    public ResumeDensity SpacingMode { get; init; } = ResumeDensity.Standard;
    public double MarginInches { get; init; } = .65;
    public bool ShowDividers { get; init; } = true;
    public bool CenterHeader { get; init; } = true;
    public bool UppercaseSectionTitles { get; init; } = true;
    public string AccentHexColor { get; init; } = "#000000";
}
public sealed record ResumeEducation
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Institution { get; init; } = "";
    public string ScoreOrPercentage { get; init; } = "";
    public string Degree { get; init; } = "";
    public string Specialization { get; init; } = "";
    public string YearRange { get; init; } = "";
}
public sealed record ResumeExperience
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Company { get; init; } = "";
    public string RoleTitle { get; init; } = "";
    public string Location { get; init; } = "";
    public string StartDate { get; init; } = "";
    public string EndDate { get; init; } = "";
    public bool IsCurrent { get; init; }
    public string BulletsRaw { get; init; } = "";
    public string ProjectLink { get; init; } = "";
}
public sealed record ResumeSkill
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string CategoryName { get; init; } = "";
    public string SkillsCsv { get; init; } = "";
}
public sealed record ResumeProject
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = "";
    public string TechStack { get; init; } = "";
    public string DateRange { get; init; } = "";
    public string BulletsRaw { get; init; } = "";
    public string RepoUrl { get; init; } = "";
}
public sealed record ResumeCertification
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = "";
    public string Issuer { get; init; } = "";
    public string Date { get; init; } = "";
    public string GradeOrScore { get; init; } = "";
    public string CredentialId { get; init; } = "";
    public string Description { get; init; } = "";
    public string VerificationUrl { get; init; } = "";
}
public sealed record ResumeAchievement
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = "";
    public string Category { get; init; } = "";
    public string Date { get; init; } = "";
    public string Description { get; init; } = "";
    public string Link { get; init; } = "";
}
public sealed record ResumeResponsibility
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Role { get; init; } = "";
    public string Organization { get; init; } = "";
    public string DateRange { get; init; } = "";
    public string BulletsRaw { get; init; } = "";
}
