using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axora.Desktop.Models;

public enum PageTargetLength
{
    OnePage,
    TwoPages,
    ThreePages,
    FourPlusPages
}

public enum ResumeFontFamily
{
    SegoeUI,
    Calibri,
    Arial,
    TimesNewRoman,
    Georgia
}

public enum ResumeSpacingMode
{
    Compact,
    Standard,
    Relaxed
}

public sealed partial class ResumeFormattingOptions : ObservableObject
{
    [ObservableProperty]
    private PageTargetLength _targetLength = PageTargetLength.OnePage;

    // ── int binds directly to ComboBox.SelectedIndex in WinUI 3 ─────────────
    // 0 = SegoeUI, 1 = Calibri, 2 = Arial, 3 = TimesNewRoman, 4 = Georgia
    [ObservableProperty]
    private int _fontFamily = 0;

    // 0 = Compact, 1 = Standard, 2 = Relaxed
    [ObservableProperty]
    private int _spacingMode = 1;

    [ObservableProperty]
    private double _marginInches = 0.65;

    [ObservableProperty]
    private bool _showDividers = true;

    [ObservableProperty]
    private bool _centerHeader = true;

    [ObservableProperty]
    private bool _uppercaseSectionTitles = true;

    [ObservableProperty]
    private string _accentHexColor = "#000000"; // Pure ATS Black or Custom Accent
}

public sealed partial class ResumeHeader : ObservableObject
{
    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _professionalTitle = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private string _location = string.Empty;

    [ObservableProperty]
    private string _linkedIn = string.Empty;

    [ObservableProperty]
    private string _linkedInUrl = string.Empty;

    [ObservableProperty]
    private string _gitHub = string.Empty;

    [ObservableProperty]
    private string _gitHubUrl = string.Empty;

    [ObservableProperty]
    private string _portfolioUrl = string.Empty;
}

public sealed partial class ExperienceItem : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _company = string.Empty;

    [ObservableProperty]
    private string _roleTitle = string.Empty;

    [ObservableProperty]
    private string _location = string.Empty;

    [ObservableProperty]
    private string _startDate = string.Empty;

    [ObservableProperty]
    private string _endDate = string.Empty;

    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BulletsLines))]
    private string _bulletsRaw = string.Empty;

    [ObservableProperty]
    private string _projectLink = string.Empty;

    /// <summary>Returns each non-empty bullet point from BulletsRaw as a trimmed string list.</summary>
    public IEnumerable<string> BulletsLines =>
        string.IsNullOrWhiteSpace(BulletsRaw)
        ? []
        : BulletsRaw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimStart('\u2022', '-', ' ', '*').Trim())
            .Where(l => !string.IsNullOrEmpty(l));
}

public sealed partial class EducationItem : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _institution = string.Empty;

    [ObservableProperty]
    private string _scoreOrPercentage = string.Empty; // e.g. "82%" or "9.4 CGPA"

    [ObservableProperty]
    private string _degree = string.Empty; // e.g. "B. TECH | Computer Science Engineering"

    [ObservableProperty]
    private string _specialization = string.Empty; // e.g. "Artificial Intelligence and Machine Learning"

    [ObservableProperty]
    private string _yearRange = string.Empty; // e.g. "2024 - 2028"
}

public sealed partial class ProjectItem : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _techStack = string.Empty;

    [ObservableProperty]
    private string _dateRange = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BulletsLines))]
    private string _bulletsRaw = string.Empty;

    [ObservableProperty]
    private string _repoUrl = string.Empty;

    /// <summary>Returns each non-empty bullet point from BulletsRaw as a trimmed string list.</summary>
    public IEnumerable<string> BulletsLines =>
        string.IsNullOrWhiteSpace(BulletsRaw)
        ? []
        : BulletsRaw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimStart('\u2022', '-', ' ', '*').Trim())
            .Where(l => !string.IsNullOrEmpty(l));
}

public sealed partial class SkillCategory : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _categoryName = string.Empty; // e.g. "Languages", "Frameworks", "Developer Tools", "Soft Skills"

    [ObservableProperty]
    private string _skillsCsv = string.Empty;
}

public sealed partial class CertificationItem : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _issuer = string.Empty; // e.g. "NPTEL - IIT Kharagpur", "TCS iON", "Microsoft Elevate"

    [ObservableProperty]
    private string _date = string.Empty;

    [ObservableProperty]
    private string _gradeOrScore = string.Empty; // e.g. "Elite Certificate (72%)"

    [ObservableProperty]
    private string _credentialId = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _verificationUrl = string.Empty;
}

public sealed partial class AchievementItem : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty; // e.g. "Workshop", "Award", "Course Completion"

    [ObservableProperty]
    private string _date = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _link = string.Empty;
}

public sealed partial class ResponsibilityItem : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _role = string.Empty; // e.g. "Secretary | IEEE SSIT Student Branch"

    [ObservableProperty]
    private string _organization = string.Empty;

    [ObservableProperty]
    private string _dateRange = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BulletsLines))]
    private string _bulletsRaw = string.Empty;

    /// <summary>Returns each non-empty bullet point from BulletsRaw as a trimmed string list.</summary>
    public IEnumerable<string> BulletsLines =>
        string.IsNullOrWhiteSpace(BulletsRaw)
        ? []
        : BulletsRaw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimStart('\u2022', '-', ' ', '*').Trim())
            .Where(l => !string.IsNullOrEmpty(l));
}

public sealed partial class ResumeDocument : ObservableObject
{
    [ObservableProperty]
    private string _resumeTitle = "Untitled Resume";

    [ObservableProperty]
    private ResumeHeader _header = new();

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private ResumeFormattingOptions _formatting = new();

    public ObservableCollection<EducationItem> Education { get; } = new();
    public ObservableCollection<ExperienceItem> Experiences { get; } = new();
    public ObservableCollection<SkillCategory> SkillCategories { get; } = new();
    public ObservableCollection<ProjectItem> Projects { get; } = new();
    public ObservableCollection<CertificationItem> Certifications { get; } = new();
    public ObservableCollection<AchievementItem> Achievements { get; } = new();
    public ObservableCollection<ResponsibilityItem> Responsibilities { get; } = new();

    // Section Visibility Toggles
    [ObservableProperty]
    private bool _showSummary = true;

    [ObservableProperty]
    private bool _showEducation = true;

    [ObservableProperty]
    private bool _showExperience = true;

    [ObservableProperty]
    private bool _showSkills = true;

    [ObservableProperty]
    private bool _showProjects = true;

    [ObservableProperty]
    private bool _showCertifications = true;

    [ObservableProperty]
    private bool _showAchievements = true;

    [ObservableProperty]
    private bool _showResponsibilities = true;
}

public sealed class AtsAnalysisResult
{
    public int MatchScore { get; set; }
    public int WordCount { get; set; }
    public int CharacterCount { get; set; }
    public int TotalKeywordsTarget { get; set; }
    public int TotalKeywordsFound { get; set; }
    public List<string> MatchedKeywords { get; set; } = new();
    public List<string> MissingKeywords { get; set; } = new();
    public List<string> StrongActionVerbs { get; set; } = new();
    public List<string> ActionVerbsUsed { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}
