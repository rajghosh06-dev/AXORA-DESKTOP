using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// 100% offline ATS Keyword & Action-Verb Optimizer.
/// Tokenizes Job Descriptions and Resumes to compute keyword match rates and bullet point strengths.
/// </summary>
public sealed class AtsOptimizerService : IAtsOptimizerService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "if", "in", "on", "with", "as", "at", "by", "for", "from",
        "to", "of", "about", "into", "through", "after", "over", "between", "under", "against", "during",
        "without", "before", "is", "am", "are", "was", "were", "be", "been", "being", "have", "has", "had",
        "do", "does", "did", "can", "could", "will", "would", "shall", "should", "may", "might", "must",
        "we", "you", "they", "he", "she", "it", "our", "your", "their", "this", "that", "these", "those",
        "work", "team", "experience", "role", "looking", "candidate", "responsibilities", "requirements",
        "qualifications", "opportunity", "company", "skills", "ability", "strong", "proficient", "knowledge",
        "plus", "years", "degree", "equivalent", "position", "apply", "join", "help", "build", "support"
    };

    private static readonly HashSet<string> HighImpactActionVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Architected", "Spearheaded", "Engineered", "Optimized", "Accelerated", "Orchestrated",
        "Implemented", "Automated", "Delivered", "Pioneered", "Standardized", "Transformed",
        "Formulated", "Revamped", "Synthesized", "Designed", "Scaled", "Streamlined", "Deployed"
    };

    public Task<AtsAnalysisResult> AnalyzeAsync(ResumeDocument document, string jobDescriptionText)
    {
        return Task.Run(() =>
        {
            var result = new AtsAnalysisResult();
            if (string.IsNullOrWhiteSpace(jobDescriptionText))
            {
                result.MatchScore = 75;
                result.Recommendations.Add("Paste a target Job Description on the right to perform full keyword matching.");
                return result;
            }

            // 1. Extract Target Keywords from JD
            var jdTokens = Tokenize(jobDescriptionText);
            var targetKeywords = ExtractTargetKeywords(jdTokens);

            // 2. Extract Keywords from Resume
            var resumeText = ExtractAllResumeText(document);
            var resumeTokens = Tokenize(resumeText);
            var resumeKeywordSet = new HashSet<string>(resumeTokens, StringComparer.OrdinalIgnoreCase);

            // 3. Match Analysis
            var matched = new List<string>();
            var missing = new List<string>();

            foreach (var kw in targetKeywords)
            {
                if (resumeKeywordSet.Contains(kw) || resumeText.Contains(kw, StringComparison.OrdinalIgnoreCase))
                {
                    matched.Add(kw);
                }
                else
                {
                    missing.Add(kw);
                }
            }

            result.MatchedKeywords = matched.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            result.MissingKeywords = missing.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).OrderBy(s => s).ToList();
            result.TotalKeywordsTarget = matched.Count + missing.Count;
            result.TotalKeywordsFound = matched.Count;

            // 4. Action Verb Scoring
            var verbsFound = new List<string>();
            foreach (var verb in HighImpactActionVerbs)
            {
                if (resumeText.Contains(verb, StringComparison.OrdinalIgnoreCase))
                {
                    verbsFound.Add(verb);
                }
            }
            result.StrongActionVerbs = verbsFound.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // 5. Overall Match Score Calculation
            double keywordRatio = result.TotalKeywordsTarget > 0
                ? (double)result.TotalKeywordsFound / result.TotalKeywordsTarget
                : 0.8;

            double verbBonus = Math.Min(result.StrongActionVerbs.Count * 3.0, 15.0);
            int finalScore = (int)Math.Clamp((keywordRatio * 85.0) + verbBonus, 15, 100);
            result.MatchScore = finalScore;

            // 6. Actionable Recommendations
            if (result.MissingKeywords.Count > 0)
            {
                var topMissing = string.Join(", ", result.MissingKeywords.Take(5));
                result.Recommendations.Add($"Incorporate key job terms: {topMissing}.");
            }
            if (result.StrongActionVerbs.Count < 4)
            {
                result.Recommendations.Add("Strengthen bullet points with high-impact action verbs (e.g. 'Architected', 'Accelerated', 'Orchestrated').");
            }
            if (!resumeText.Contains("%") && !Regex.IsMatch(resumeText, @"\d+x"))
            {
                result.Recommendations.Add("Quantify accomplishments using numerical metrics (e.g. 'decreased latency by 40%', 'scaled to 10k users').");
            }
            if (result.Recommendations.Count == 0)
            {
                result.Recommendations.Add("Outstanding keyword alignment! Content is well-optimized for ATS filtering systems.");
            }

            return result;
        });
    }

    private static string ExtractAllResumeText(ResumeDocument doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine(doc.Header.FullName);
        sb.AppendLine(doc.Header.ProfessionalTitle);
        sb.AppendLine(doc.Summary);

        foreach (var exp in doc.Experiences)
        {
            sb.AppendLine(exp.RoleTitle);
            sb.AppendLine(exp.Company);
            sb.AppendLine(exp.BulletsRaw);
        }

        foreach (var proj in doc.Projects)
        {
            sb.AppendLine(proj.Title);
            sb.AppendLine(proj.TechStack);
            sb.AppendLine(proj.BulletsRaw);
        }

        foreach (var sk in doc.SkillCategories)
        {
            sb.AppendLine(sk.CategoryName);
            sb.AppendLine(sk.SkillsCsv);
        }

        foreach (var edu in doc.Education)
        {
            sb.AppendLine(edu.Degree);
            sb.AppendLine(edu.Institution);
        }

        return sb.ToString();
    }

    private static List<string> Tokenize(string text)
    {
        return Regex.Matches(text, @"\b[A-Za-z0-9\+#\.]+\b")
            .Select(m => m.Value)
            .Where(w => w.Length >= 2 && !StopWords.Contains(w))
            .ToList();
    }

    private static List<string> ExtractTargetKeywords(List<string> tokens)
    {
        var freq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tokens)
        {
            if (t.Length <= 2 && !t.Equals("C#", StringComparison.OrdinalIgnoreCase) && !t.Equals("C++", StringComparison.OrdinalIgnoreCase) && !t.Equals("AI", StringComparison.OrdinalIgnoreCase) && !t.Equals("ML", StringComparison.OrdinalIgnoreCase))
                continue;

            freq[t] = freq.GetValueOrDefault(t, 0) + 1;
        }

        return freq
            .OrderByDescending(kv => kv.Value)
            .Take(30)
            .Select(kv => kv.Key)
            .ToList();
    }
}
