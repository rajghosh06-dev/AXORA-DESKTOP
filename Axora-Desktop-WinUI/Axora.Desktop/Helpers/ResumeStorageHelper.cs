using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Helpers;

/// <summary>
/// Helper for managing Resume documents on disk (Documents\Axora\Resumes\),
/// unique title resolution (e.g. Untitled Resume (1), (2)...), renaming, and duplication.
/// </summary>
public static class ResumeStorageHelper
{
    public static string ResumeFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Axora", "Resumes");

    public static void EnsureDirectory()
    {
        try { Directory.CreateDirectory(ResumeFolder); } catch { }
    }

    /// <summary>
    /// Generates a unique, non-colliding title such as "Untitled Resume", "Untitled Resume (1)", "Untitled Resume (2)", etc.
    /// </summary>
    public static string GenerateUniqueResumeTitle(string baseTitle)
    {
        EnsureDirectory();
        var existingTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (Directory.Exists(ResumeFolder))
            {
                foreach (var file in Directory.GetFiles(ResumeFolder, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("ResumeTitle", out var titleProp))
                        {
                            var t = titleProp.GetString();
                            if (!string.IsNullOrWhiteSpace(t))
                                existingTitles.Add(t.Trim());
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        if (!existingTitles.Contains(baseTitle))
            return baseTitle;

        int counter = 1;
        while (existingTitles.Contains($"{baseTitle} ({counter})"))
        {
            counter++;
        }
        return $"{baseTitle} ({counter})";
    }

    /// <summary>
    /// Renames an existing resume file on disk and updates its internal ResumeTitle property.
    /// </summary>
    public static async Task<string> RenameResumeAsync(string currentFilePath, string newTitle)
    {
        EnsureDirectory();
        if (!File.Exists(currentFilePath)) return currentFilePath;

        var json = await File.ReadAllTextAsync(currentFilePath);
        var doc = JsonSerializer.Deserialize<ResumeDocument>(json);
        if (doc == null) return currentFilePath;

        doc.ResumeTitle = newTitle.Trim();
        var safeTitle = string.Concat(doc.ResumeTitle.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Resume";

        var guidShort = Guid.NewGuid().ToString("N")[..8];
        var newPath = Path.Combine(ResumeFolder, $"{safeTitle}_{guidShort}.json");

        var opt = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(newPath, JsonSerializer.Serialize(doc, opt));

        try { File.Delete(currentFilePath); } catch { }
        return newPath;
    }
}
