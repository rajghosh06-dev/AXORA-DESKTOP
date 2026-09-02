using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Axora.Desktop.Helpers;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.ViewModels;

public sealed partial class ResumeStudioViewModel : ObservableObject
{
    private readonly IResumePdfCompilerService _pdfCompiler;
    private readonly IAtsOptimizerService _atsOptimizer;
    private readonly IAppSettingsService _settings;
    private readonly DispatcherQueue? _dispatcher;

    // ── Document ─────────────────────────────────────────────────────────────
    [ObservableProperty]
    private ResumeDocument _document;

    // ── Undo / Redo History ───────────────────────────────────────────────────
    private bool _isRestoringUndo = false;
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private bool _canRedo;

    // ── Tab state ─────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditorTabActive))]
    [NotifyPropertyChangedFor(nameof(IsFormattingTabActive))]
    [NotifyPropertyChangedFor(nameof(IsAtsTabActive))]
    private int _activeRightTabIndex;

    public bool IsEditorTabActive    => ActiveRightTabIndex == 0;
    public bool IsFormattingTabActive => ActiveRightTabIndex == 1;
    public bool IsAtsTabActive        => ActiveRightTabIndex == 2;

    // ── Target Page Length & Budget ───────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetPageLengthDescription))]
    [NotifyPropertyChangedFor(nameof(IsPage2Visible))]
    [NotifyPropertyChangedFor(nameof(IsPage3Visible))]
    [NotifyPropertyChangedFor(nameof(IsPage4Visible))]
    [NotifyPropertyChangedFor(nameof(TotalPageCount))]
    [NotifyPropertyChangedFor(nameof(IsTarget1PageSelected))]
    [NotifyPropertyChangedFor(nameof(IsTarget2PagesSelected))]
    [NotifyPropertyChangedFor(nameof(IsTarget3PagesSelected))]
    [NotifyPropertyChangedFor(nameof(IsTarget4PlusPagesSelected))]
    private PageTargetLength _selectedTargetLength = PageTargetLength.TwoPages;

    // Called by source generator when _selectedTargetLength changes
    partial void OnSelectedTargetLengthChanged(PageTargetLength value)
    {
        Document.Formatting.TargetLength = value;
        RefreshPageLayout();
        RecalculatePageBudget();
    }

    public string TargetPageLengthDescription => SelectedTargetLength switch
    {
        PageTargetLength.OnePage      => "1-Page Target · Entry-level, new grads & career changers (< 5 yrs experience).",
        PageTargetLength.TwoPages     => "2-Page Target · Mid-level professionals & technical roles (5–15 yrs experience).",
        PageTargetLength.ThreePages   => "3-Page Target · Senior executives & managers with extensive project portfolios (> 15 yrs).",
        PageTargetLength.FourPlusPages=> "4+ Page CV · Academic researchers, medical specialists & federal government positions.",
        _ => ""
    };

    // Page visibility for multi-page preview cards
    public bool IsPage2Visible => SelectedTargetLength >= PageTargetLength.TwoPages;
    public bool IsPage3Visible => SelectedTargetLength >= PageTargetLength.ThreePages;
    public bool IsPage4Visible => SelectedTargetLength == PageTargetLength.FourPlusPages;
    public int  TotalPageCount => (int)SelectedTargetLength + 1;

    // ToggleButton selection state for page-target buttons in toolbar
    public bool IsTarget1PageSelected     => SelectedTargetLength == PageTargetLength.OnePage;
    public bool IsTarget2PagesSelected    => SelectedTargetLength == PageTargetLength.TwoPages;
    public bool IsTarget3PagesSelected    => SelectedTargetLength == PageTargetLength.ThreePages;
    public bool IsTarget4PlusPagesSelected=> SelectedTargetLength == PageTargetLength.FourPlusPages;

    [ObservableProperty] private string _pageBudgetStatus = "—";
    [ObservableProperty] private double _pageBudgetProgressValue;

    // ── Preview Zoom ──────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomLabelText))]
    private double _previewMaxWidth = 620.0;

    public string ZoomLabelText => $"{(int)Math.Round(PreviewMaxWidth / 620.0 * 100.0)}%";

    // ── ATS Scanner ───────────────────────────────────────────────────────────
    [ObservableProperty] private string _jobDescriptionInput = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAtsResults))]
    private AtsAnalysisResult _atsResult = new AtsAnalysisResult { MatchScore = 0 };
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotAnalyzingAts))]
    private bool _isAnalyzingAts;

    [ObservableProperty] private string _atsValidationMessage = string.Empty;
    [ObservableProperty] private bool _showAtsValidation;

    /// <summary>Inverse of IsAnalyzingAts — used for Button.IsEnabled binding.</summary>
    public bool IsNotAnalyzingAts => !IsAnalyzingAts;

    /// <summary>True once the user has run at least one ATS scan (score &gt; 0 or keywords exist).</summary>
    public bool HasAtsResults => AtsResult.MatchScore > 0 || AtsResult.MatchedKeywords.Count > 0 || AtsResult.MissingKeywords.Count > 0;

    // ── Export ────────────────────────────────────────────────────────────────
    /// <summary>0 = PDF, 1 = Plain Text (ATS-safe)</summary>
    [ObservableProperty] private int _selectedExportFormatIndex;

    // ── Dashboard: active file path ───────────────────────────────────────────
    /// <summary>Path of the JSON file currently open in the editor. Null = unsaved new resume.</summary>
    public string? ActiveFilePath { get; set; }

    // ── Formatting Display ────────────────────────────────────────────────────
    /// <summary>Formatted margin label for display in the Style tab.</summary>
    public string MarginInchesLabel => $"{Document.Formatting.MarginInches:F2} in";

    // ── Section Page-Distribution Visibility (Reactive) ───────────────────────
    // Strategy:
    //   1-Page  → P1: ALL sections
    //   2-Page  → P1: Header+Summary+Edu+Exp   P2: Skills+Projects+Certs+Achievements+Leadership
    //   3-Page  → P1: Header+Summary+Edu+Exp   P2: Skills+Projects   P3: Certs+Achievements+Leadership
    //   4-Page  → P1: Header+Summary+Edu+Exp   P2: Skills+Projects   P3: Certs+Achievements   P4: Leadership

    private Visibility SectionVis(bool sectionEnabled, bool onThisPage)
        => (sectionEnabled && onThisPage) ? Visibility.Visible : Visibility.Collapsed;

    // Summary — always P1
    public Visibility SummaryPage1Vis  => SectionVis(Document.ShowSummary,       true);

    // Education — always P1
    public Visibility EducationPage1Vis => SectionVis(Document.ShowEducation,    true);

    // Experience — always P1
    public Visibility ExperiencePage1Vis => SectionVis(Document.ShowExperience,  true);

    // Skills
    public Visibility SkillsPage1Vis => SectionVis(Document.ShowSkills, SelectedTargetLength == PageTargetLength.OnePage);
    public Visibility SkillsPage2Vis => SectionVis(Document.ShowSkills, SelectedTargetLength >= PageTargetLength.TwoPages);

    // Projects
    public Visibility ProjectsPage1Vis => SectionVis(Document.ShowProjects, SelectedTargetLength == PageTargetLength.OnePage);
    public Visibility ProjectsPage2Vis => SectionVis(Document.ShowProjects, SelectedTargetLength >= PageTargetLength.TwoPages);

    // Certifications
    public Visibility CertsPage1Vis => SectionVis(Document.ShowCertifications, SelectedTargetLength == PageTargetLength.OnePage);
    public Visibility CertsPage2Vis => SectionVis(Document.ShowCertifications, SelectedTargetLength == PageTargetLength.TwoPages);
    public Visibility CertsPage3Vis => SectionVis(Document.ShowCertifications, SelectedTargetLength >= PageTargetLength.ThreePages);

    // Achievements
    public Visibility AchievementsPage1Vis => SectionVis(Document.ShowAchievements, SelectedTargetLength == PageTargetLength.OnePage);
    public Visibility AchievementsPage2Vis => SectionVis(Document.ShowAchievements, SelectedTargetLength == PageTargetLength.TwoPages);
    public Visibility AchievementsPage3Vis => SectionVis(Document.ShowAchievements, SelectedTargetLength >= PageTargetLength.ThreePages);

    // Leadership / Responsibilities
    public Visibility LeadershipPage1Vis => SectionVis(Document.ShowResponsibilities, SelectedTargetLength == PageTargetLength.OnePage);
    public Visibility LeadershipPage2Vis => SectionVis(Document.ShowResponsibilities, SelectedTargetLength == PageTargetLength.TwoPages);
    public Visibility LeadershipPage3Vis => SectionVis(Document.ShowResponsibilities, SelectedTargetLength == PageTargetLength.ThreePages);
    public Visibility LeadershipPage4Vis => SectionVis(Document.ShowResponsibilities, SelectedTargetLength == PageTargetLength.FourPlusPages);

    // ── Constructor ───────────────────────────────────────────────────────────
    public ResumeStudioViewModel(
        IResumePdfCompilerService pdfCompiler,
        IAtsOptimizerService atsOptimizer,
        IAppSettingsService settings)
    {
        _pdfCompiler  = pdfCompiler;
        _atsOptimizer = atsOptimizer;
        _settings     = settings;
        _dispatcher   = DispatcherQueue.GetForCurrentThread();

        // Partial properties cannot have initializers (CS8050) — set defaults in constructor
        Document = new ResumeDocument();
        SelectedTargetLength = PageTargetLength.TwoPages;
        PageBudgetStatus = "—";
        PageBudgetProgressValue = 0.0;
        PreviewMaxWidth = 620.0;
        JobDescriptionInput = string.Empty;
        AtsResult = new AtsAnalysisResult { MatchScore = 0 };
        AtsValidationMessage = string.Empty;

        LoadPreset("rishit-ghosh");
    }

    // Called by source generator when _document changes
    partial void OnDocumentChanged(ResumeDocument? oldValue, ResumeDocument newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged                  -= OnDocumentPropertyChanged;
            oldValue.Formatting.PropertyChanged       -= OnFormattingPropertyChanged;
        }
        if (newValue is not null)
        {
            newValue.PropertyChanged                  += OnDocumentPropertyChanged;
            newValue.Formatting.PropertyChanged       += OnFormattingPropertyChanged;
        }
        RefreshPageLayout();
        RecalculatePageBudget();
    }

    private void OnFormattingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ResumeFormattingOptions.MarginInches))
            OnPropertyChanged(nameof(MarginInchesLabel));
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is
            nameof(ResumeDocument.ShowSummary)      or
            nameof(ResumeDocument.ShowEducation)    or
            nameof(ResumeDocument.ShowExperience)   or
            nameof(ResumeDocument.ShowSkills)       or
            nameof(ResumeDocument.ShowProjects)     or
            nameof(ResumeDocument.ShowCertifications) or
            nameof(ResumeDocument.ShowAchievements) or
            nameof(ResumeDocument.ShowResponsibilities))
        {
            RefreshPageLayout();
            RecalculatePageBudget();
        }
    }

    private void RefreshPageLayout()
    {
        OnPropertyChanged(nameof(SummaryPage1Vis));
        OnPropertyChanged(nameof(EducationPage1Vis));
        OnPropertyChanged(nameof(ExperiencePage1Vis));
        OnPropertyChanged(nameof(SkillsPage1Vis));
        OnPropertyChanged(nameof(SkillsPage2Vis));
        OnPropertyChanged(nameof(ProjectsPage1Vis));
        OnPropertyChanged(nameof(ProjectsPage2Vis));
        OnPropertyChanged(nameof(CertsPage1Vis));
        OnPropertyChanged(nameof(CertsPage2Vis));
        OnPropertyChanged(nameof(CertsPage3Vis));
        OnPropertyChanged(nameof(AchievementsPage1Vis));
        OnPropertyChanged(nameof(AchievementsPage2Vis));
        OnPropertyChanged(nameof(AchievementsPage3Vis));
        OnPropertyChanged(nameof(LeadershipPage1Vis));
        OnPropertyChanged(nameof(LeadershipPage2Vis));
        OnPropertyChanged(nameof(LeadershipPage3Vis));
        OnPropertyChanged(nameof(LeadershipPage4Vis));
    }

    // ── Commands: Tab & Target ────────────────────────────────────────────────

    [RelayCommand]
    public void SetTargetLength(string lengthKey)
    {
        SelectedTargetLength = lengthKey.ToLowerInvariant() switch
        {
            "1" or "one"   => PageTargetLength.OnePage,
            "2" or "two"   => PageTargetLength.TwoPages,
            "3" or "three" => PageTargetLength.ThreePages,
            _              => PageTargetLength.FourPlusPages
        };
    }

    [RelayCommand]
    public void SetRightTab(int tabIndex) => ActiveRightTabIndex = tabIndex;

    // ── Commands: Zoom ────────────────────────────────────────────────────────

    [RelayCommand]
    public void ZoomIn()  => PreviewMaxWidth = Math.Min(PreviewMaxWidth + 62,  930);

    [RelayCommand]
    public void ZoomOut() => PreviewMaxWidth = Math.Max(PreviewMaxWidth - 62,  310);

    [RelayCommand]
    public void ZoomReset() => PreviewMaxWidth = 620;

    // ── Commands: Presets ─────────────────────────────────────────────────────

    [RelayCommand]
    public void LoadPreset(string presetKey)
    {
        var doc = new ResumeDocument();

        switch (presetKey.ToLowerInvariant())
        {
            // ── Blank (dashboard "New Resume") ─────────────────────────────
            case "blank":
                SelectedTargetLength = PageTargetLength.TwoPages;
                doc.Formatting.TargetLength = PageTargetLength.TwoPages;
                doc.ResumeTitle = ResumeStorageHelper.GenerateUniqueResumeTitle("Untitled Resume");
                doc.Header.FullName = "YOUR FULL NAME";
                doc.Header.ProfessionalTitle = "Your Professional Title";
                doc.Header.Location = "City, State";
                doc.Header.Phone = "+1 (555) 000-0000";
                doc.Header.Email = "you@email.com";
                break;

            case "rishit-ghosh":
            default:
                SelectedTargetLength = PageTargetLength.TwoPages;
                doc.Formatting.TargetLength = PageTargetLength.TwoPages;
                doc.ResumeTitle = ResumeStorageHelper.GenerateUniqueResumeTitle("Tech ATS Resume");

                doc.Header.FullName          = "RISHIT GHOSH";
                doc.Header.ProfessionalTitle = "Computer Science Engineering · AI & ML Specialist";
                doc.Header.Location          = "Hyderabad, Telangana, India";
                doc.Header.Phone             = "+91 80198 13896";
                doc.Header.Email             = "rishitghosh06@gmail.com";
                doc.Header.LinkedIn          = "linkedin.com/in/rajghosh06";
                doc.Header.LinkedInUrl       = "https://linkedin.com/in/rajghosh06";
                doc.Header.GitHub            = "github.com/rajghosh06-dev";
                doc.Header.GitHubUrl         = "https://github.com/rajghosh06-dev";
                doc.Header.PortfolioUrl      = "";

                doc.Summary = "CSE-AI & ML student with interest in designing modular systems using Java, Python, and automation workflows, driven by precision debugging and clean code hygiene. Develops scalable AI/ML solutions, workflow-optimized environments, and expressive documentation for open-source communities.";

                doc.Education.Add(new EducationItem
                {
                    Institution = "Geethanjali College of Engineering and Technology",
                    ScoreOrPercentage = "82%", YearRange = "2024 - 2028",
                    Degree = "B. TECH",
                    Specialization = "Computer Science Engineering with Specialization in Artificial Intelligence and Machine Learning"
                });
                doc.Education.Add(new EducationItem
                {
                    Institution = "Sri Chaitanya Junior Kalasala",
                    ScoreOrPercentage = "81.3%", YearRange = "2022 - 2024",
                    Degree = "INTERMEDIATE", Specialization = "MPC [Math, Physics, Chemistry]"
                });
                doc.Education.Add(new EducationItem
                {
                    Institution = "Little Flower School",
                    ScoreOrPercentage = "88%", YearRange = "2011 - 2022",
                    Degree = "SCHOOLING", Specialization = "CBSE [Central Board of Secondary Education]"
                });

                doc.Experiences.Add(new ExperienceItem
                {
                    Company = "AICTE - Edunet Foundation & Shell", RoleTitle = "Intern",
                    Location = "Virtual", StartDate = "Jun 2025", EndDate = "Jul 2025",
                    BulletsRaw = "Completed a 4-week internship under AICTE - Edunet & Shell (Skills4Future), focusing on Green Skills and Artificial Intelligence.\nExecuted a project to predict pollution drift patterns utilizing advanced data analysis and modeling techniques.\nIntegrated geospatial data with predictive models to formulate sustainable pollution mitigation strategies.\nAcquired Green Skills aligned with circular economy principles to promote sustainability in environmental practices.",
                    ProjectLink = "github.com/rajghosh06-dev/Pollution-Drift-Predictor"
                });
                doc.Experiences.Add(new ExperienceItem
                {
                    Company = "AICTE - Microsoft Elevate", RoleTitle = "Intern",
                    Location = "Virtual", StartDate = "Dec 2025", EndDate = "Jan 2026",
                    BulletsRaw = "Successfully completed a 4-week internship at AICTE - Microsoft Elevate, specializing in Emerging Technologies including Power BI, Azure, Copilot-Enabled Development, and Automation.\nAzure Internship Project: Developed and tested cloud-based applications with a focus on scalable infrastructure design.\nCopilot Internship Project: Implemented Copilot integration within real-world academic workflows.\nPower BI Internship Project: Created interactive dashboards and BI solutions for analyzing student performance."
                });

                doc.SkillCategories.Add(new SkillCategory { CategoryName = "Languages",       SkillsCsv = "Python, Java, C, HTML, JS, CSS" });
                doc.SkillCategories.Add(new SkillCategory { CategoryName = "Frameworks",      SkillsCsv = "Pandas, NumPy, Scikit-Learn, TensorFlow, PyTorch, Matplotlib" });
                doc.SkillCategories.Add(new SkillCategory { CategoryName = "Developer Tools", SkillsCsv = "Microsoft Azure, Power BI, GitHub, Microsoft Copilot, Android Studio" });
                doc.SkillCategories.Add(new SkillCategory { CategoryName = "Soft Skills",     SkillsCsv = "Professional Communication, Interpersonal Skills, Adaptability, Problem-solving, Time Management" });

                doc.Projects.Add(new ProjectItem
                {
                    Title = "Pollution Drift Predictor",
                    TechStack = "Predictive Modeling, Geospatial Analysis, Machine Learning",
                    DateRange = "Jun 2025 - Jul 2025",
                    BulletsRaw = "Led a predictive modeling initiative to forecast pollution drift patterns with precision.\nIntegrated complex geospatial datasets with advanced predictive algorithms to develop effective and sustainable mitigation strategies.\nApplied and enhanced Green Skills in alignment with circular economy principles to promote environmental sustainability.",
                    RepoUrl = "github.com/rajghosh06-dev/Pollution-Drift-Predictor"
                });
                doc.Projects.Add(new ProjectItem
                {
                    Title = "Personal Notes Manager", TechStack = "Microsoft Azure, Cloud Computing, Scalable Infrastructure",
                    DateRange = "Dec 2025 - Jan 2026",
                    BulletsRaw = "Developed and thoroughly tested a cloud-based application leveraging Microsoft Azure services.\nDesigned and implemented a scalable infrastructure to ensure high availability and optimal performance for note-management operations.",
                    RepoUrl = "github.com/rajghosh06-dev/personal-notes-manager"
                });
                doc.Projects.Add(new ProjectItem
                {
                    Title = "Student AI Workflow", TechStack = "Microsoft Copilot, Generative AI, Workflow Automation",
                    DateRange = "Dec 2025 - Jan 2026",
                    BulletsRaw = "Developed a productivity solution by integrating Microsoft Copilot into practical academic workflows.\nOptimized complex student tasks and automated routine processes to improve overall academic efficiency.\nEnhanced time management capabilities, enabling significant gains in productivity for academic environments.",
                    RepoUrl = "github.com/rajghosh06-dev/student-ai-workflow"
                });
                doc.Projects.Add(new ProjectItem
                {
                    Title = "Student Performance Dashboard", TechStack = "Power BI, Data Analytics, Business Intelligence",
                    DateRange = "Dec 2025 - Jan 2026",
                    BulletsRaw = "Developed and implemented interactive dashboards and end-to-end BI solutions utilizing Power BI.\nApplied advanced data analytics to present student performance metrics, facilitating clear and actionable insights for academic assessment.",
                    RepoUrl = "github.com/rajghosh06-dev/student-performance-dashboard"
                });

                doc.Certifications.Add(new CertificationItem
                {
                    Title = "Programming for Problem Solving in C",
                    Issuer = "NPTEL - Indian Institute of Technology, Kharagpur",
                    Date = "May 2025",
                    Description = "Successfully completed the NPTEL course on Programming for Problem Solving in C, strengthening expertise in control structures, arrays, functions, and file handling."
                });
                doc.Certifications.Add(new CertificationItem
                {
                    Title = "Python for Data Science",
                    Issuer = "NPTEL - Indian Institute of Technology, Madras",
                    Date = "Feb 2026",
                    GradeOrScore = "Elite Certificate (72%)",
                    Description = "Strengthened skills in Python programming, data analysis, and visualization. Applied concepts in projects like the Smart Visualization Agent."
                });

                doc.Achievements.Add(new AchievementItem
                {
                    Category = "Workshop",
                    Title = "IKS-TKDL Workshop - Traditional Knowledge & IPR",
                    Date = "Dec 2024",
                    Description = "Successfully completed 5-day online workshop on Traditional Knowledge Systems and IPR Frameworks with 75%+ score and merit certificate.",
                    Link = "linkedin.com/in/rajghosh06/details/certifications/"
                });
                doc.Achievements.Add(new AchievementItem
                {
                    Category = "Course Completion",
                    Title = "IBM SkillsBuild - AI Fundamentals & Personality Dynamics",
                    Date = "Sep 2025",
                    Description = "Acquired foundational expertise in supervised learning, neural networks, ethical AI, structured problem solving, and process controls."
                });

                doc.Responsibilities.Add(new ResponsibilityItem
                {
                    Role = "Secretary | IEEE SSIT Student Branch",
                    Organization = "Geethanjali College of Engineering and Technology",
                    DateRange = "Feb 2026 - Present",
                    BulletsRaw = "Managed administrative operations and executive communications for the IEEE SSIT Student Branch.\nCoordinated committee meetings, oversaw official documentation, and facilitated the seamless implementation of chapter initiatives."
                });
                break;

            // ── Software Engineer — 1-Page ATS Template ──────────────────
            case "software-engineer":
                SelectedTargetLength = PageTargetLength.OnePage;
                doc.Formatting.TargetLength = PageTargetLength.OnePage;
                doc.ResumeTitle = ResumeStorageHelper.GenerateUniqueResumeTitle("Software Engineer Resume");

                doc.Header.FullName          = "YOUR FULL NAME";
                doc.Header.ProfessionalTitle = "Software Engineer";
                doc.Header.Location          = "City, State";
                doc.Header.Phone             = "+1 (555) 000-0000";
                doc.Header.Email             = "you@email.com";
                doc.Header.LinkedIn          = "linkedin.com/in/yourhandle";
                doc.Header.LinkedInUrl       = "https://linkedin.com/in/yourhandle";
                doc.Header.GitHub            = "github.com/yourhandle";
                doc.Header.GitHubUrl         = "https://github.com/yourhandle";

                doc.Summary = "Software Engineer with X years of experience designing and shipping production-grade backend services, REST APIs, and distributed systems. Proven track record of reducing latency by 40%+ and scaling platforms to 100k+ concurrent users. Strong in Java, Python, Go, and cloud-native architectures.";

                doc.Education.Add(new EducationItem
                {
                    Institution = "University of Technology", ScoreOrPercentage = "3.8 GPA",
                    YearRange = "2018 - 2022", Degree = "B.S. COMPUTER SCIENCE",
                    Specialization = "Software Engineering Track"
                });

                doc.Experiences.Add(new ExperienceItem
                {
                    Company = "Tech Company Inc.", RoleTitle = "Software Engineer II",
                    Location = "Remote", StartDate = "Jan 2023", EndDate = "Present",
                    BulletsRaw = "Architected and deployed a high-throughput event streaming pipeline processing 500k events/day using Apache Kafka and Go.\nReduced API response latency by 45% through query optimization, Redis caching, and connection pooling.\nLed migration of 3 microservices from monolith, improving deployment frequency from monthly to daily."
                });
                doc.Experiences.Add(new ExperienceItem
                {
                    Company = "Startup Co.", RoleTitle = "Junior Software Engineer",
                    Location = "On-site", StartDate = "Jun 2022", EndDate = "Dec 2022",
                    BulletsRaw = "Built RESTful APIs serving 10k+ daily active users using FastAPI and PostgreSQL.\nImplemented automated CI/CD pipeline with GitHub Actions, reducing deployment time by 60%."
                });

                doc.SkillCategories.Add(new SkillCategory { CategoryName = "Languages",       SkillsCsv = "Python, Java, Go, TypeScript, SQL" });
                doc.SkillCategories.Add(new SkillCategory { CategoryName = "Frameworks",      SkillsCsv = "FastAPI, Spring Boot, React, Node.js, Django" });
                doc.SkillCategories.Add(new SkillCategory { CategoryName = "Cloud & Tools",   SkillsCsv = "AWS (EC2, S3, RDS, Lambda), Docker, Kubernetes, Terraform, GitHub Actions" });
                doc.SkillCategories.Add(new SkillCategory { CategoryName = "Databases",       SkillsCsv = "PostgreSQL, MySQL, MongoDB, Redis, Elasticsearch" });

                doc.Projects.Add(new ProjectItem
                {
                    Title = "Open Source CLI Tool", TechStack = "Python, Click, PyPI",
                    DateRange = "2024", RepoUrl = "github.com/yourhandle/project",
                    BulletsRaw = "Built a developer productivity CLI tool downloaded 5k+ times on PyPI.\nImplemented plugin architecture supporting 20+ community-contributed extensions."
                });

                doc.ShowCertifications  = false;
                doc.ShowAchievements    = false;
                doc.ShowResponsibilities= false;
                break;

            // ── Executive & Leadership — 2-Page Template ────────────────
            case "executive-leadership":
                SelectedTargetLength = PageTargetLength.TwoPages;
                doc.Formatting.TargetLength = PageTargetLength.TwoPages;
                doc.ResumeTitle = ResumeStorageHelper.GenerateUniqueResumeTitle("Executive Leadership Resume");

                doc.Header.FullName          = "ALEXANDER M. VANCE";
                doc.Header.ProfessionalTitle = "VP of Engineering & Technology Strategy";
                doc.Header.Location          = "San Francisco, CA";
                doc.Header.Phone             = "+1 (415) 555-0199";
                doc.Header.Email             = "a.vance@execleader.com";
                doc.Header.LinkedIn          = "linkedin.com/in/alex-vance-tech";
                doc.Header.LinkedInUrl       = "https://linkedin.com/in/alex-vance-tech";

                doc.Summary = "Visionary Engineering Executive with 14+ years of experience leading multi-disciplinary engineering organizations of 120+ engineers across 4 global regions. Drove $45M+ revenue acceleration through modern cloud-native architectures, enterprise SaaS scalability, and high-retention talent strategy.";

                doc.Education.Add(new EducationItem
                {
                    Institution = "Stanford Graduate School of Business", ScoreOrPercentage = "Executive Program",
                    YearRange = "2019", Degree = "EXECUTIVE LEADERSHIP", Specialization = "Technology Innovation & General Management"
                });
                doc.Education.Add(new EducationItem
                {
                    Institution = "University of California, Berkeley", ScoreOrPercentage = "Honors",
                    YearRange = "2008 - 2012", Degree = "B.S. ELECTRICAL ENGINEERING & COMPUTER SCIENCE", Specialization = "Distributed Systems"
                });

                doc.Experiences.Add(new ExperienceItem
                {
                    Company = "Apex Enterprise Cloud Inc.", RoleTitle = "VP of Engineering",
                    Location = "San Francisco, CA", StartDate = "2021", EndDate = "Present",
                    BulletsRaw = "Spearheaded organizational scaling from 45 to 140 engineers across frontend, backend, platform infrastructure, and SRE.\nDelivered zero-downtime multi-cloud platform migration achieving 99.999% SLA across $120M ARR portfolio.\nDecreased engineering turnover from 18% to 4.2% by instituting mentorship ladders and merit-based career frameworks."
                });
                doc.Experiences.Add(new ExperienceItem
                {
                    Company = "Nexus Digital Systems", RoleTitle = "Director of Software Engineering",
                    Location = "San Jose, CA", StartDate = "2016", EndDate = "2021",
                    BulletsRaw = "Managed $18M annual engineering budget and directed 6 engineering teams building high-throughput microservices.\nPioneered AI-assisted automated testing infrastructure reducing defect escape rate by 65%."
                });

                doc.SkillCategories.Add(new SkillCategory { CategoryName = "Executive Leadership", SkillsCsv = "Org Design & Scaling, Strategic Roadmapping, Cross-Functional Alignment, P&L Budgeting" });
                doc.SkillCategories.Add(new SkillCategory { CategoryName = "Technical Architecture", SkillsCsv = "Multi-Cloud Governance, Microservices, Event-Driven Systems, SOC2 / ISO 27001 Compliance" });

                doc.Responsibilities.Add(new ResponsibilityItem
                {
                    Role = "Advisory Board Member", Organization = "Silicon Valley Tech Leaders Alliance",
                    DateRange = "2022 - Present", BulletsRaw = "Advise early-stage AI startups on enterprise engineering scaling, fundraising diligence, and architecture robustness."
                });
                break;

            // ── Academic & Research CV — 3-Page Template ────────────────
            case "academic-research":
                SelectedTargetLength = PageTargetLength.ThreePages;
                doc.Formatting.TargetLength = PageTargetLength.ThreePages;
                doc.ResumeTitle = ResumeStorageHelper.GenerateUniqueResumeTitle("Academic Research CV");

                doc.Header.FullName          = "DR. ELENA ROSTOVA, PH.D.";
                doc.Header.ProfessionalTitle = "Associate Professor of Computational Biophysics";
                doc.Header.Location          = "Boston, MA";
                doc.Header.Phone             = "+1 (617) 555-0142";
                doc.Header.Email             = "e.rostova@university.edu";

                doc.Summary = "Computational Biophysicist and Principal Investigator researching molecular dynamics simulations of protein-ligand interactions and quantum chemical modeling. Author of 28 peer-reviewed publications (h-index: 19, 2,400+ citations) with $3.2M in NIH and NSF grant funding.";

                doc.Education.Add(new EducationItem
                {
                    Institution = "Harvard University", ScoreOrPercentage = "Ph.D. with Distinction",
                    YearRange = "2014 - 2019", Degree = "PH.D. IN COMPUTATIONAL BIOPHYSICS", Specialization = "Dissertation: Free Energy Landscapes in Membrane Transport"
                });
                doc.Education.Add(new EducationItem
                {
                    Institution = "Massachusetts Institute of Technology", ScoreOrPercentage = "Summa Cum Laude",
                    YearRange = "2010 - 2014", Degree = "B.S. IN PHYSICS & MATHEMATICS", Specialization = "Minor in Computer Science"
                });

                doc.Experiences.Add(new ExperienceItem
                {
                    Company = "Department of Physics, New England University", RoleTitle = "Associate Professor (Tenured)",
                    Location = "Boston, MA", StartDate = "2023", EndDate = "Present",
                    BulletsRaw = "Lead Biomolecular Simulation Laboratory consisting of 4 postdoctoral fellows and 6 doctoral researchers.\nSecured $1.8M NIH R01 grant as Principal Investigator for modeling membrane allosteric regulators.\nTeach graduate-level Statistical Mechanics and Advanced Quantum Simulation courses."
                });

                doc.Projects.Add(new ProjectItem
                {
                    Title = "OpenBioSim Quantum Molecular Dynamics Engine", TechStack = "C++20, CUDA, OpenMP, Python bindings",
                    DateRange = "2020 - Present", BulletsRaw = "Developed open-source GPU-accelerated MD engine utilized by over 85 academic institutions globally."
                });

                doc.Certifications.Add(new CertificationItem
                {
                    Title = "NSF CAREER Award", Issuer = "National Science Foundation", Date = "2024",
                    Description = "Prestigious early-career award recognizing integration of research and undergraduate education in molecular mechanics."
                });
                break;
        }

        Document = doc;
        _undoStack.Clear();
        _redoStack.Clear();
        PushUndoSnapshot();
    }

    // ── Page Budget Calculation ───────────────────────────────────────────────

    public void RecalculatePageBudget()
    {
        // Weighted character estimation (accounts for section headings, spacing, line-height)
        double total = 0;

        // Header block (fixed weight regardless of char count)
        total += 180;

        // Summary: ~13.5pt line-height, ~70 chars per line
        total += Math.Ceiling(Document.Summary.Length / 65.0) * 14 + 25;

        // Education entries
        foreach (var edu in Document.Education)
            total += 30 + Math.Ceiling((edu.Degree.Length + edu.Specialization.Length) / 70.0) * 12;

        // Experience entries (bullet-heavy)
        foreach (var exp in Document.Experiences)
        {
            total += 35; // header rows
            var bulletLines = exp.BulletsRaw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
            total += bulletLines * 13;
        }

        // Skills: compact key-value rows
        foreach (var sk in Document.SkillCategories)
            total += 14 + Math.Ceiling((sk.CategoryName.Length + sk.SkillsCsv.Length) / 72.0) * 12;

        // Projects
        foreach (var prj in Document.Projects)
        {
            total += 28;
            var bulletLines = prj.BulletsRaw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
            total += bulletLines * 13;
        }

        // Certifications
        foreach (var cert in Document.Certifications)
            total += 25 + Math.Ceiling(cert.Description.Length / 70.0) * 12;

        // Achievements
        foreach (var ach in Document.Achievements)
            total += 25 + Math.Ceiling(ach.Description.Length / 70.0) * 12;

        // Leadership
        foreach (var rsp in Document.Responsibilities)
        {
            total += 30;
            var bulletLines = rsp.BulletsRaw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
            total += bulletLines * 13;
        }

        // Section heading overhead (26pt each, one per visible section)
        int visibleSections = 0;
        if (Document.ShowSummary && !string.IsNullOrWhiteSpace(Document.Summary)) visibleSections++;
        if (Document.ShowEducation && Document.Education.Count > 0) visibleSections++;
        if (Document.ShowExperience && Document.Experiences.Count > 0) visibleSections++;
        if (Document.ShowSkills && Document.SkillCategories.Count > 0) visibleSections++;
        if (Document.ShowProjects && Document.Projects.Count > 0) visibleSections++;
        if (Document.ShowCertifications && Document.Certifications.Count > 0) visibleSections++;
        if (Document.ShowAchievements && Document.Achievements.Count > 0) visibleSections++;
        if (Document.ShowResponsibilities && Document.Responsibilities.Count > 0) visibleSections++;
        total += visibleSections * 30;

        // A4 page printable area capacity in "units" (842pt - 2*46pt margin ≈ 750pt per page)
        double pageCapacity = 750.0;
        double targetCapacity = (int)SelectedTargetLength * pageCapacity + pageCapacity;

        PageBudgetProgressValue = Math.Min(120.0, Math.Round(total / targetCapacity * 100.0, 1));

        string targetLabel = SelectedTargetLength switch
        {
            PageTargetLength.OnePage       => "1-Page",
            PageTargetLength.TwoPages      => "2-Page",
            PageTargetLength.ThreePages    => "3-Page",
            _                              => "4+ Page CV"
        };

        PageBudgetStatus = PageBudgetProgressValue <= 100.0
            ? $"Fits within {targetLabel} Target ({PageBudgetProgressValue:F0}% capacity used)"
            : $"Exceeds {targetLabel} Target by {PageBudgetProgressValue - 100:F0}% - consider trimming content";
    }

    // ── History & Refresh Commands ────────────────────────────────────────────

    public void PushUndoSnapshot()
    {
        if (_isRestoringUndo) return;
        try
        {
            var json = JsonSerializer.Serialize(Document);
            _undoStack.Push(json);
            if (_undoStack.Count > 30)
            {
                var list = _undoStack.ToList();
                list.RemoveAt(list.Count - 1);
                _undoStack.Clear();
                for (int i = list.Count - 1; i >= 0; i--) _undoStack.Push(list[i]);
            }
            _redoStack.Clear();
            CanUndo = _undoStack.Count > 1;
            CanRedo = false;
        }
        catch { }
    }

    [RelayCommand]
    public void Undo()
    {
        if (_undoStack.Count <= 1) return;
        try
        {
            _isRestoringUndo = true;
            var current = _undoStack.Pop();
            _redoStack.Push(current);
            var prev = _undoStack.Peek();
            RestoreFromJson(prev);
            CanUndo = _undoStack.Count > 1;
            CanRedo = _redoStack.Count > 0;
        }
        finally
        {
            _isRestoringUndo = false;
        }
    }

    [RelayCommand]
    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        try
        {
            _isRestoringUndo = true;
            var next = _redoStack.Pop();
            _undoStack.Push(next);
            RestoreFromJson(next);
            CanUndo = _undoStack.Count > 1;
            CanRedo = _redoStack.Count > 0;
        }
        finally
        {
            _isRestoringUndo = false;
        }
    }

    [RelayCommand]
    public void RefreshPreview()
    {
        RefreshPageLayout();
        RecalculatePageBudget();
        OnPropertyChanged(nameof(Document));
    }

    /// <summary>
    /// Saves the current document to the resume library (Documents\Axora\Resumes\).
    /// If ActiveFilePath is set, overwrites that file. Otherwise creates a new file.
    public async Task SaveToLibraryAsync()
    {
        try
        {
            var folder = ResumeStorageHelper.ResumeFolder;
            ResumeStorageHelper.EnsureDirectory();

            var titleBase = !string.IsNullOrWhiteSpace(Document.ResumeTitle)
                ? Document.ResumeTitle
                : (!string.IsNullOrWhiteSpace(Document.Header.FullName) ? Document.Header.FullName : "Untitled Resume");
            var safeName = string.Concat(titleBase.Split(System.IO.Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "Resume";

            if (string.IsNullOrEmpty(ActiveFilePath))
            {
                var guidShort = Guid.NewGuid().ToString("N")[..8];
                ActiveFilePath = System.IO.Path.Combine(folder, $"{safeName}_{guidShort}.json");
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Document, options);
            await File.WriteAllTextAsync(ActiveFilePath, json);
            Debug.WriteLine($"[ResumeStudio] Successfully saved resume to: {ActiveFilePath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ResumeStudio] CRITICAL SaveToLibrary error: {ex}");
        }
    }

    public void RestoreFromJson(string json)
    {
        try
        {
            var options = new JsonSerializerOptions();
            var imported = JsonSerializer.Deserialize<ResumeDocument>(json, options);
            if (imported is null) return;

            var fresh = new ResumeDocument
            {
                ResumeTitle = string.IsNullOrWhiteSpace(imported.ResumeTitle)
                    ? (!string.IsNullOrWhiteSpace(imported.Header.FullName) ? $"{imported.Header.FullName} Resume" : "Untitled Resume")
                    : imported.ResumeTitle,
                Summary = imported.Summary,
                ShowSummary = imported.ShowSummary,
                ShowEducation = imported.ShowEducation,
                ShowExperience = imported.ShowExperience,
                ShowSkills = imported.ShowSkills,
                ShowProjects = imported.ShowProjects,
                ShowCertifications = imported.ShowCertifications,
                ShowAchievements = imported.ShowAchievements,
                ShowResponsibilities = imported.ShowResponsibilities
            };

            fresh.Header.FullName = imported.Header.FullName;
            fresh.Header.ProfessionalTitle = imported.Header.ProfessionalTitle;
            fresh.Header.Location = imported.Header.Location;
            fresh.Header.Phone = imported.Header.Phone;
            fresh.Header.Email = imported.Header.Email;
            fresh.Header.LinkedIn = imported.Header.LinkedIn;
            fresh.Header.LinkedInUrl = imported.Header.LinkedInUrl;
            fresh.Header.GitHub = imported.Header.GitHub;
            fresh.Header.GitHubUrl = imported.Header.GitHubUrl;
            fresh.Header.PortfolioUrl = imported.Header.PortfolioUrl;

            fresh.Formatting.TargetLength = imported.Formatting.TargetLength;
            fresh.Formatting.FontFamily = imported.Formatting.FontFamily;
            fresh.Formatting.SpacingMode = imported.Formatting.SpacingMode;
            fresh.Formatting.ShowDividers = imported.Formatting.ShowDividers;
            fresh.Formatting.CenterHeader = imported.Formatting.CenterHeader;
            fresh.Formatting.UppercaseSectionTitles = imported.Formatting.UppercaseSectionTitles;
            fresh.Formatting.MarginInches = imported.Formatting.MarginInches;

            foreach (var edu in imported.Education) fresh.Education.Add(edu);
            foreach (var exp in imported.Experiences) fresh.Experiences.Add(exp);
            foreach (var sk in imported.SkillCategories) fresh.SkillCategories.Add(sk);
            foreach (var p in imported.Projects) fresh.Projects.Add(p);
            foreach (var c in imported.Certifications) fresh.Certifications.Add(c);
            foreach (var a in imported.Achievements) fresh.Achievements.Add(a);
            foreach (var r in imported.Responsibilities) fresh.Responsibilities.Add(r);

            Document = fresh;
            SelectedTargetLength = fresh.Formatting.TargetLength;
            RefreshPageLayout();
            RecalculatePageBudget();
        }
        catch { }
    }

    // ── Item Mutation Commands ────────────────────────────────────────────────

    [RelayCommand] public void AddEducation()
    {
        PushUndoSnapshot();
        Document.Education.Add(new EducationItem { Institution = "Institution Name", Degree = "Degree", YearRange = "2024 - 2028" });
        RecalculatePageBudget();
    }
    [RelayCommand] public void RemoveEducation(EducationItem item) { PushUndoSnapshot(); Document.Education.Remove(item); RecalculatePageBudget(); }

    [RelayCommand] public void AddExperience()
    {
        PushUndoSnapshot();
        Document.Experiences.Add(new ExperienceItem { Company = "Company Name", RoleTitle = "Role Title", StartDate = "2025", EndDate = "Present", BulletsRaw = "Key accomplishment description." });
        RecalculatePageBudget();
    }
    [RelayCommand] public void RemoveExperience(ExperienceItem item) { PushUndoSnapshot(); Document.Experiences.Remove(item); RecalculatePageBudget(); }

    [RelayCommand] public void AddSkillCategory()
    {
        PushUndoSnapshot();
        Document.SkillCategories.Add(new SkillCategory { CategoryName = "New Category", SkillsCsv = "Skill 1, Skill 2, Skill 3" });
        RecalculatePageBudget();
    }
    [RelayCommand] public void RemoveSkillCategory(SkillCategory item) { PushUndoSnapshot(); Document.SkillCategories.Remove(item); RecalculatePageBudget(); }

    [RelayCommand] public void AddProject()
    {
        PushUndoSnapshot();
        Document.Projects.Add(new ProjectItem { Title = "Project Title", TechStack = "Tech Stack", BulletsRaw = "Built project architecture.\nOptimized execution pipeline." });
        RecalculatePageBudget();
    }
    [RelayCommand] public void RemoveProject(ProjectItem item) { PushUndoSnapshot(); Document.Projects.Remove(item); RecalculatePageBudget(); }

    [RelayCommand] public void AddCertification()
    {
        PushUndoSnapshot();
        Document.Certifications.Add(new CertificationItem { Title = "Certification Name", Issuer = "Issuing Body", Date = "2026" });
        RecalculatePageBudget();
    }
    [RelayCommand] public void RemoveCertification(CertificationItem item) { PushUndoSnapshot(); Document.Certifications.Remove(item); RecalculatePageBudget(); }

    [RelayCommand] public void AddAchievement()
    {
        PushUndoSnapshot();
        Document.Achievements.Add(new AchievementItem { Category = "Award / Workshop", Title = "Achievement Title", Date = "2026", Description = "Details of accomplishment." });
        RecalculatePageBudget();
    }
    [RelayCommand] public void RemoveAchievement(AchievementItem item) { PushUndoSnapshot(); Document.Achievements.Remove(item); RecalculatePageBudget(); }

    [RelayCommand] public void AddResponsibility()
    {
        PushUndoSnapshot();
        Document.Responsibilities.Add(new ResponsibilityItem { Role = "Leadership Role", Organization = "Organization Name", DateRange = "2026 - Present", BulletsRaw = "Managed team operations." });
        RecalculatePageBudget();
    }
    [RelayCommand] public void RemoveResponsibility(ResponsibilityItem item) { PushUndoSnapshot(); Document.Responsibilities.Remove(item); RecalculatePageBudget(); }

    // ── Export / Import ───────────────────────────────────────────────────────

    [RelayCommand]
    public async Task ExportPdfAsync()
    {
        try
        {
            var savePath = await NativeFilePickerHelper.PickSaveFileAsync(
                title: "Export ATS Vector PDF",
                defaultExt: "pdf",
                suggestedFileName: $"{(Document.Header.FullName ?? "Resume").Replace(' ', '_')}_Resume.pdf",
                filter: "PDF Document (*.pdf)\0*.pdf\0");

            if (string.IsNullOrEmpty(savePath)) return;

            var bytes = await _pdfCompiler.CompileToBytesAsync(Document);
            await File.WriteAllBytesAsync(savePath, bytes);
            Process.Start(new ProcessStartInfo(savePath) { UseShellExecute = true });
        }
        catch (Exception ex) { Debug.WriteLine($"[ResumeStudio] Export PDF Error: {ex.Message}"); throw; }
    }

    [RelayCommand]
    public async Task ExportPlainTextAsync()
    {
        try
        {
            var savePath = await NativeFilePickerHelper.PickSaveFileAsync(
                title: "Export ATS-Safe Plain Text",
                defaultExt: "txt",
                suggestedFileName: $"{(Document.Header.FullName ?? "Resume").Replace(' ', '_')}_Resume_ATS.txt",
                filter: "Plain Text (*.txt)\0*.txt\0");

            if (string.IsNullOrEmpty(savePath)) return;

            var text = BuildPlainTextResume();
            await File.WriteAllTextAsync(savePath, text);
            Process.Start(new ProcessStartInfo(savePath) { UseShellExecute = true });
        }
        catch (Exception ex) { Debug.WriteLine($"[ResumeStudio] Export TXT Error: {ex.Message}"); throw; }
    }

    [RelayCommand]
    public async Task ExportSmartAsync()
    {
        if (SelectedExportFormatIndex == 1)
            await ExportPlainTextAsync();
        else
            await ExportPdfAsync();
    }

    private string BuildPlainTextResume()
    {
        var sb = new StringBuilder();
        var h = Document.Header;

        sb.AppendLine(h.FullName.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(h.ProfessionalTitle)) sb.AppendLine(h.ProfessionalTitle);
        var contacts = new[] { h.Location, h.Phone, h.Email, h.LinkedIn, h.GitHub }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        sb.AppendLine(string.Join(" | ", contacts));
        sb.AppendLine();

        if (Document.ShowSummary && !string.IsNullOrWhiteSpace(Document.Summary))
        {
            sb.AppendLine("SUMMARY");
            sb.AppendLine(new string('-', 60));
            sb.AppendLine(Document.Summary);
            sb.AppendLine();
        }

        if (Document.ShowEducation && Document.Education.Count > 0)
        {
            sb.AppendLine("EDUCATION");
            sb.AppendLine(new string('-', 60));
            foreach (var edu in Document.Education)
            {
                sb.AppendLine($"{edu.Institution}{(string.IsNullOrWhiteSpace(edu.ScoreOrPercentage) ? "" : $" | {edu.ScoreOrPercentage}")}   {edu.YearRange}");
                sb.AppendLine($"{edu.Degree}{(string.IsNullOrWhiteSpace(edu.Specialization) ? "" : $" | {edu.Specialization}")}");
            }
            sb.AppendLine();
        }

        if (Document.ShowExperience && Document.Experiences.Count > 0)
        {
            sb.AppendLine("PROFESSIONAL EXPERIENCE");
            sb.AppendLine(new string('-', 60));
            foreach (var exp in Document.Experiences)
            {
                sb.AppendLine($"{exp.Company}   {exp.StartDate} - {exp.EndDate}");
                sb.AppendLine($"{exp.RoleTitle}   {exp.Location}");
                foreach (var bullet in exp.BulletsLines) sb.AppendLine($"  • {bullet}");
                if (!string.IsNullOrWhiteSpace(exp.ProjectLink)) sb.AppendLine($"  Link: {exp.ProjectLink}");
                sb.AppendLine();
            }
        }

        if (Document.ShowSkills && Document.SkillCategories.Count > 0)
        {
            sb.AppendLine("TECHNICAL SKILLS");
            sb.AppendLine(new string('-', 60));
            foreach (var sk in Document.SkillCategories)
                sb.AppendLine($"{sk.CategoryName}: {sk.SkillsCsv}");
            sb.AppendLine();
        }

        if (Document.ShowProjects && Document.Projects.Count > 0)
        {
            sb.AppendLine("KEY PROJECTS");
            sb.AppendLine(new string('-', 60));
            foreach (var prj in Document.Projects)
            {
                sb.AppendLine($"{prj.Title}{(string.IsNullOrWhiteSpace(prj.TechStack) ? "" : $" | {prj.TechStack}")}   {prj.DateRange}");
                foreach (var bullet in prj.BulletsLines) sb.AppendLine($"  • {bullet}");
                if (!string.IsNullOrWhiteSpace(prj.RepoUrl)) sb.AppendLine($"  GitHub: {prj.RepoUrl}");
                sb.AppendLine();
            }
        }

        if (Document.ShowCertifications && Document.Certifications.Count > 0)
        {
            sb.AppendLine("CERTIFICATIONS");
            sb.AppendLine(new string('-', 60));
            foreach (var cert in Document.Certifications)
            {
                sb.AppendLine($"{cert.Title} | {cert.Issuer}   {cert.Date}");
                if (!string.IsNullOrWhiteSpace(cert.GradeOrScore)) sb.AppendLine($"  Grade: {cert.GradeOrScore}");
                if (!string.IsNullOrWhiteSpace(cert.Description)) sb.AppendLine($"  {cert.Description}");
            }
            sb.AppendLine();
        }

        if (Document.ShowAchievements && Document.Achievements.Count > 0)
        {
            sb.AppendLine("ACHIEVEMENTS & WORKSHOPS");
            sb.AppendLine(new string('-', 60));
            foreach (var ach in Document.Achievements)
            {
                sb.AppendLine($"{ach.Category} | {ach.Title}   {ach.Date}");
                if (!string.IsNullOrWhiteSpace(ach.Description)) sb.AppendLine($"  {ach.Description}");
            }
            sb.AppendLine();
        }

        if (Document.ShowResponsibilities && Document.Responsibilities.Count > 0)
        {
            sb.AppendLine("POSITION OF RESPONSIBILITY");
            sb.AppendLine(new string('-', 60));
            foreach (var rsp in Document.Responsibilities)
            {
                sb.AppendLine($"{rsp.Role}   {rsp.DateRange}");
                sb.AppendLine($"{rsp.Organization}");
                foreach (var bullet in rsp.BulletsLines) sb.AppendLine($"  • {bullet}");
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    [RelayCommand]
    public async Task ExportJsonAsync()
    {
        try
        {
            var savePath = await NativeFilePickerHelper.PickSaveFileAsync(
                title: "Export Resume JSON Schema",
                defaultExt: "json",
                suggestedFileName: $"{(Document.Header.FullName ?? "Resume").Replace(' ', '_')}_Resume.json",
                filter: "JSON Schema (*.json)\0*.json\0");

            if (string.IsNullOrEmpty(savePath)) return;

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Document, options);
            await File.WriteAllTextAsync(savePath, json);
        }
        catch (Exception ex) { Debug.WriteLine($"[ResumeStudio] Export JSON Error: {ex.Message}"); throw; }
    }

    [RelayCommand]
    public async Task ImportJsonAsync()
    {
        try
        {
            var files = await NativeFilePickerHelper.PickFilesAsync(
                title: "Import Resume JSON Data",
                filter: "JSON Schema (*.json)\0*.json\0All Files (*.*)\0*.*\0",
                allowMultiple: false);

            if (files is { Count: > 0 })
            {
                var json = await File.ReadAllTextAsync(files[0]);
                var options = new JsonSerializerOptions();
                var imported = JsonSerializer.Deserialize<ResumeDocument>(json, options);
                if (imported is not null)
                {
                    // Re-populate observable collections (they're get-only so JSON can't replace them)
                    var fresh = new ResumeDocument
                    {
                        ResumeTitle = string.IsNullOrWhiteSpace(imported.ResumeTitle)
                            ? (!string.IsNullOrWhiteSpace(imported.Header.FullName) ? $"{imported.Header.FullName} Resume" : "Untitled Resume")
                            : imported.ResumeTitle,
                        Summary = imported.Summary,
                        ShowSummary = imported.ShowSummary,
                        ShowEducation = imported.ShowEducation,
                        ShowExperience = imported.ShowExperience,
                        ShowSkills = imported.ShowSkills,
                        ShowProjects = imported.ShowProjects,
                        ShowCertifications = imported.ShowCertifications,
                        ShowAchievements = imported.ShowAchievements,
                        ShowResponsibilities = imported.ShowResponsibilities
                    };
                    // Header
                    fresh.Header.FullName = imported.Header.FullName;
                    fresh.Header.ProfessionalTitle = imported.Header.ProfessionalTitle;
                    fresh.Header.Location = imported.Header.Location;
                    fresh.Header.Phone = imported.Header.Phone;
                    fresh.Header.Email = imported.Header.Email;
                    fresh.Header.LinkedIn = imported.Header.LinkedIn;
                    fresh.Header.LinkedInUrl = imported.Header.LinkedInUrl;
                    fresh.Header.GitHub = imported.Header.GitHub;
                    fresh.Header.GitHubUrl = imported.Header.GitHubUrl;
                    fresh.Header.PortfolioUrl = imported.Header.PortfolioUrl;
                    // Collections
                    foreach (var e in imported.Education) fresh.Education.Add(e);
                    foreach (var e in imported.Experiences) fresh.Experiences.Add(e);
                    foreach (var e in imported.SkillCategories) fresh.SkillCategories.Add(e);
                    foreach (var e in imported.Projects) fresh.Projects.Add(e);
                    foreach (var e in imported.Certifications) fresh.Certifications.Add(e);
                    foreach (var e in imported.Achievements) fresh.Achievements.Add(e);
                    foreach (var e in imported.Responsibilities) fresh.Responsibilities.Add(e);

                    Document = fresh;
                }
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[ResumeStudio] Import JSON Error: {ex.Message}"); throw; }
    }

    // ── ATS Scanner ───────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task RunAtsCheckAsync()
    {
        // Guard against double-invocation
        if (IsAnalyzingAts) return;

        // Validate: require a job description
        if (string.IsNullOrWhiteSpace(JobDescriptionInput))
        {
            AtsValidationMessage = "Please paste a job description above before running the scan.";
            ShowAtsValidation    = true;
            return;
        }

        ShowAtsValidation = false;
        AtsValidationMessage = string.Empty;
        IsAnalyzingAts = true;
        try
        {
            AtsResult = await _atsOptimizer.AnalyzeAsync(Document, JobDescriptionInput);
        }
        finally
        {
            IsAnalyzingAts = false;
        }
    }
}
