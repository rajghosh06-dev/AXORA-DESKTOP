using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// ATS Optimization Engine — analyzes resume text against a target Job Description
/// to compute match scores, extract missing keywords, and suggest high-impact action verbs.
/// </summary>
public interface IAtsOptimizerService
{
    /// <summary>
    /// Evaluates the resume document against target job description text.
    /// </summary>
    Task<AtsAnalysisResult> AnalyzeAsync(ResumeDocument document, string jobDescriptionText);
}
