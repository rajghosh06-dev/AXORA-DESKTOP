using System.Collections.Immutable;
using Axora.Studio.Models;
using Axora.Studio.Models.ResumeRendering;

namespace Axora.Studio.Services.ResumeRendering;

public static class ResumeSnapshotBuilder
{
    public static ResumeRenderSnapshot Capture(ResumeFile file)
    {
        if (file is null) throw new ResumeRenderException(ResumeRenderFailure.InvalidResumeInput, "InvalidResumeInput");
        try { new ResumeCodec().Validate(file); }
        catch (InvalidDataException) { throw new ResumeRenderException(ResumeRenderFailure.InvalidResumeInput, "InvalidResumeInput"); }
        var d = file.Document;
        var h = d.Header;
        var f = d.Formatting;
        int remaining = ResumeRenderLimits.DerivedItems;
        var visibility = new ResumeSnapshotVisibility(d.ShowSummary, d.ShowEducation, d.ShowExperience, d.ShowSkills,
            d.ShowProjects, d.ShowCertifications, d.ShowAchievements, d.ShowResponsibilities);
        var document = new ResumeSnapshotDocument(d.ResumeTitle,
            new(h.FullName, h.ProfessionalTitle, h.Email, h.Phone, h.Location, h.LinkedIn, h.LinkedInUrl, h.GitHub, h.GitHubUrl, h.PortfolioUrl),
            d.Summary, new(f.TargetLength, f.FontFamily, f.SpacingMode, f.MarginInches, f.ShowDividers, f.CenterHeader, f.UppercaseSectionTitles, f.AccentHexColor),
            d.Education.Select(x => new ResumeSnapshotEducation(x.Id, x.Institution, x.ScoreOrPercentage, x.Degree, x.Specialization, x.YearRange)).ToImmutableArray(),
            d.Experiences.Select(x => new ResumeSnapshotExperience(x.Id, x.Company, x.RoleTitle, x.Location, x.StartDate, x.EndDate, x.IsCurrent,
                x.BulletsRaw, x.ProjectLink, Split(x.BulletsRaw, false, ref remaining), x.IsCurrent ? "Present" : x.EndDate)).ToImmutableArray(),
            d.SkillCategories.Select(x => new ResumeSnapshotSkill(x.Id, x.CategoryName, x.SkillsCsv, Split(x.SkillsCsv, true, ref remaining))).ToImmutableArray(),
            d.Projects.Select(x => new ResumeSnapshotProject(x.Id, x.Title, x.TechStack, x.DateRange, x.BulletsRaw, x.RepoUrl,
                Split(x.BulletsRaw, false, ref remaining))).ToImmutableArray(),
            d.Certifications.Select(x => new ResumeSnapshotCertification(x.Id, x.Title, x.Issuer, x.Date, x.GradeOrScore, x.CredentialId, x.Description, x.VerificationUrl)).ToImmutableArray(),
            d.Achievements.Select(x => new ResumeSnapshotAchievement(x.Id, x.Title, x.Category, x.Date, x.Description, x.Link)).ToImmutableArray(),
            d.Responsibilities.Select(x => new ResumeSnapshotResponsibility(x.Id, x.Role, x.Organization, x.DateRange, x.BulletsRaw,
                Split(x.BulletsRaw, false, ref remaining))).ToImmutableArray(), visibility,
            ImmutableArray.CreateRange(d.SectionOrder.AsSpan().ToArray()),
            d.SectionOrder.Where(x => IsVisible(visibility, x)).ToImmutableArray());
        byte[] semantic = ResumeSemanticCanonicalizer.EncodeSemantic(document);
        byte[] snapshot = ResumeSemanticCanonicalizer.EncodeSnapshot(file.DocumentId, file.Revision, document);
        return new(file.DocumentId, file.Revision, document, semantic, snapshot);
    }

    private static ImmutableArray<string> Split(string raw, bool skills, ref int remaining)
    {
        // Count before allocating: <= one item per UTF-16 unit plus one; CRLF is one delimiter.
        int count = 1;
        for (int i = 0; i < raw.Length; i++)
            if (skills ? raw[i] == ',' : raw[i] is '\r' or '\n')
            {
                count++;
                if (!skills && raw[i] == '\r' && i + 1 < raw.Length && raw[i + 1] == '\n') i++;
            }
        if (count > remaining) throw new ResumeRenderException(ResumeRenderFailure.SnapshotLimitExceeded, "DerivedItemLimitExceeded");
        remaining -= count;
        var result = ImmutableArray.CreateBuilder<string>(count);
        int start = 0;
        for (int i = 0; i < raw.Length; i++)
            if (skills ? raw[i] == ',' : raw[i] is '\r' or '\n')
            {
                Add(start, i);
                if (!skills && raw[i] == '\r' && i + 1 < raw.Length && raw[i + 1] == '\n') i++;
                start = i + 1;
            }
        Add(start, raw.Length);
        return result.MoveToImmutable();

        void Add(int first, int end)
        {
            if (skills)
            {
                while (first < end && raw[first] is ' ' or '\t') first++;
                while (end > first && raw[end - 1] is ' ' or '\t') end--;
            }
            result.Add(raw[first..end]);
        }
    }

    private static bool IsVisible(ResumeSnapshotVisibility v, ResumeSection section) => section switch
    {
        ResumeSection.Summary => v.Summary, ResumeSection.Education => v.Education, ResumeSection.Experience => v.Experience,
        ResumeSection.Skills => v.Skills, ResumeSection.Projects => v.Projects, ResumeSection.Certifications => v.Certifications,
        ResumeSection.Achievements => v.Achievements, ResumeSection.Responsibilities => v.Responsibilities,
        _ => throw new InvalidOperationException("Validated section required.")
    };
}
