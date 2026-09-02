using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Professional ATS Multi-Page Vector PDF Compiler using PdfSharpCore.
/// Accurately formats sections with vector horizontal rules, page budget pagination,
/// clickable hyperlink annotations, and Page X of Y footers.
/// </summary>
public sealed class ResumePdfCompilerService : IResumePdfCompilerService
{
    public async Task CompileToPdfAsync(ResumeDocument document, string destinationFilePath)
    {
        var bytes = await CompileToBytesAsync(document);
        await File.WriteAllBytesAsync(destinationFilePath, bytes);
    }

    public Task<byte[]> CompileToBytesAsync(ResumeDocument document)
    {
        return Task.Run(() =>
        {
            using var pdfDoc = new PdfDocument();
            pdfDoc.Info.Title   = $"{document.Header.FullName} - Resume";
            pdfDoc.Info.Author  = document.Header.FullName;
            pdfDoc.Info.Subject = "Curriculum Vitae / Professional Resume";

            // A4 page dimensions (in points: 1 in = 72 pt)
            const double pageWidth  = 595.28;
            const double pageHeight = 841.89;
            double margin        = Math.Max(28.0, document.Formatting.MarginInches * 72.0); // default ~46.8 pt
            double printableWidth= pageWidth - (2 * margin);
            double maxY          = pageHeight - margin - 18; // reserve 18 pt for footer

            // Spacing multiplier from SpacingMode (0=Compact, 1=Standard, 2=Relaxed)
            double spacingMult = document.Formatting.SpacingMode switch
            {
                0 => 0.85,  // Compact
                2 => 1.20,  // Relaxed
                _ => 1.00   // Standard (default)
            };

            // Typography  (0=SegoeUI, 1=Calibri, 2=Arial, 3=TimesNewRoman, 4=Georgia)
            string fontName = document.Formatting.FontFamily switch
            {
                3 => "Times New Roman",
                2 => "Arial",
                1 => "Calibri",
                4 => "Georgia",
                _ => "Segoe UI"
            };

            var titleFont         = new XFont(fontName, 18,   XFontStyle.Bold);
            var sectionHeaderFont = new XFont(fontName, 10.5, XFontStyle.Bold);
            var itemHeaderFont    = new XFont(fontName, 9.5,  XFontStyle.Bold);
            var itemSubFont       = new XFont(fontName, 9.0,  XFontStyle.Bold);
            var regularFont       = new XFont(fontName, 9.0,  XFontStyle.Regular);
            var italicFont        = new XFont(fontName, 8.5,  XFontStyle.Italic);
            var linkFont          = new XFont(fontName, 8.5,  XFontStyle.Regular);
            var footerFont        = new XFont(fontName, 7.5,  XFontStyle.Regular);

            var primaryBrush  = XBrushes.Black;
            var subTextBrush  = new XSolidBrush(XColor.FromArgb(255,  60,  60,  60));
            var linkBrush     = new XSolidBrush(XColor.FromArgb(255,   0,  70, 160));
            var footerBrush   = new XSolidBrush(XColor.FromArgb(255, 140, 140, 140));
            var linePen       = new XPen(XColor.FromArgb(255, 180, 180, 180), 0.75);

            // Track all pages and per-page annotations for the 2-pass footer approach
            var pageList = new List<PdfPage>();

            PdfPage currentPage = AddPage(pdfDoc, pageList, pageWidth, pageHeight);
            XGraphics gfx       = XGraphics.FromPdfPage(currentPage);
            double currentY     = margin;

            // ── EnsureSpace: adds a new page when needed ─────────────────────
            void EnsureSpace(double requiredHeight)
            {
                if (currentY + requiredHeight > maxY)
                {
                    gfx.Dispose();
                    currentPage = AddPage(pdfDoc, pageList, pageWidth, pageHeight);
                    gfx         = XGraphics.FromPdfPage(currentPage);
                    currentY    = margin;
                }
            }

            // ── Helper: draw a URL hyperlink annotation ───────────────────────
            void AddLink(string url, double x, double y, double width, double height)
            {
                if (string.IsNullOrWhiteSpace(url)) return;
                if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    url = "https://" + url;

                currentPage.AddWebLink(new PdfRectangle(new XRect(x, y, width, height)), url);
            }

            // ── Helper: strip formatting markers for clean vector PDF rendering ──────
            string StripFormatting(string input)
            {
                if (string.IsNullOrEmpty(input)) return "";
                var clean = System.Text.RegularExpressions.Regex.Replace(input, @"\[(.*?)\]\(.*?\)", "$1");
                clean = clean.Replace("**", "").Replace("__", "");
                return clean;
            }

            // ── DrawWrappedText: word-wraps text, calls EnsureSpace BEFORE first line ─
            double DrawWrappedText(string rawText, XFont font, XBrush brush, double x, double y,
                                   double maxW, bool callEnsureBeforeFirst = true)
            {
                if (string.IsNullOrWhiteSpace(rawText)) return y;
                string text = StripFormatting(rawText);
                var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string line = "";
                double lh = font.Size * 1.25 * spacingMult;
                bool isFirstLine = true;
                currentY = y;

                foreach (var word in words)
                {
                    var testLine = string.IsNullOrEmpty(line) ? word : $"{line} {word}";
                    var size = gfx.MeasureString(testLine, font);

                    if (size.Width > maxW && !string.IsNullOrEmpty(line))
                    {
                        if (!isFirstLine || callEnsureBeforeFirst)
                        {
                            currentY = y;
                            EnsureSpace(lh);
                            y = currentY;
                        }
                        gfx.DrawString(line, font, brush, x, y + font.Size);
                        y += lh;
                        currentY = y;
                        line = word;
                        isFirstLine = false;
                    }
                    else
                    {
                        line = testLine;
                    }
                }

                if (!string.IsNullOrEmpty(line))
                {
                    if (!isFirstLine || callEnsureBeforeFirst)
                    {
                        currentY = y;
                        EnsureSpace(lh);
                        y = currentY;
                    }
                    gfx.DrawString(line, font, brush, x, y + font.Size);
                    y += lh;
                    currentY = y;
                }
                return y;
            }

            // ── Section Heading ───────────────────────────────────────────────
            void DrawSectionHeading(string title)
            {
                double headingH = 26 * spacingMult;
                EnsureSpace(headingH);
                currentY += 4 * spacingMult;
                gfx.DrawString(title.ToUpperInvariant(), sectionHeaderFont, primaryBrush, margin, currentY + 10);
                currentY += 13 * spacingMult;
                if (document.Formatting.ShowDividers)
                    gfx.DrawLine(linePen, margin, currentY, margin + printableWidth, currentY);
                currentY += 6 * spacingMult;
            }

            // ═══════════════════════════════════════════════════════════════
            //  PASS 1 — RENDER ALL CONTENT
            // ═══════════════════════════════════════════════════════════════

            // ── 1. HEADER ─────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(document.Header.FullName))
            {
                var nameSize = gfx.MeasureString(document.Header.FullName, titleFont);
                double nameX = margin + (printableWidth - nameSize.Width) / 2;
                gfx.DrawString(document.Header.FullName, titleFont, primaryBrush,
                    nameX, currentY + nameSize.Height);
                currentY += nameSize.Height + 3;

                if (!string.IsNullOrWhiteSpace(document.Header.ProfessionalTitle))
                {
                    var titleSize = gfx.MeasureString(document.Header.ProfessionalTitle, italicFont);
                    gfx.DrawString(document.Header.ProfessionalTitle, italicFont, subTextBrush,
                        margin + (printableWidth - titleSize.Width) / 2, currentY + italicFont.Size);
                    currentY += italicFont.Size + 3;
                }

                if (!string.IsNullOrWhiteSpace(document.Header.Location))
                {
                    var locSize = gfx.MeasureString(document.Header.Location, regularFont);
                    gfx.DrawString(document.Header.Location, regularFont, subTextBrush,
                        margin + (printableWidth - locSize.Width) / 2, currentY + regularFont.Size);
                    currentY += regularFont.Size + 2;
                }

                // Contact line: Phone • Email • LinkedIn • GitHub
                var contactParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(document.Header.Phone))    contactParts.Add(document.Header.Phone);
                if (!string.IsNullOrWhiteSpace(document.Header.Email))    contactParts.Add(document.Header.Email);
                if (!string.IsNullOrWhiteSpace(document.Header.LinkedIn)) contactParts.Add($"LinkedIn: {document.Header.LinkedIn}");
                if (!string.IsNullOrWhiteSpace(document.Header.GitHub))   contactParts.Add($"GitHub: {document.Header.GitHub}");
                if (!string.IsNullOrWhiteSpace(document.Header.PortfolioUrl)) contactParts.Add($"Portfolio: {document.Header.PortfolioUrl}");

                if (contactParts.Count > 0)
                {
                    string contactLine = string.Join("   •   ", contactParts);
                    var cSize = gfx.MeasureString(contactLine, regularFont);
                    double contactX = margin + (printableWidth - cSize.Width) / 2;
                    gfx.DrawString(contactLine, regularFont, subTextBrush,
                        contactX, currentY + regularFont.Size);

                    // Add hyperlinks for LinkedIn and GitHub in the contact line
                    if (!string.IsNullOrWhiteSpace(document.Header.LinkedInUrl))
                        AddLink(document.Header.LinkedInUrl, contactX, currentY, cSize.Width, regularFont.Size + 2);
                    currentY += regularFont.Size + 10;
                }
            }

            // ── 2. SUMMARY ────────────────────────────────────────────────
            if (document.ShowSummary && !string.IsNullOrWhiteSpace(document.Summary))
            {
                DrawSectionHeading("SUMMARY");
                currentY = DrawWrappedText(document.Summary, regularFont, subTextBrush, margin, currentY, printableWidth);
                currentY += 4 * spacingMult;
            }

            // ── 3. EDUCATION ──────────────────────────────────────────────
            if (document.ShowEducation && document.Education.Count > 0)
            {
                DrawSectionHeading("EDUCATION");
                foreach (var edu in document.Education)
                {
                    EnsureSpace(24 * spacingMult);
                    string leftLine1 = string.IsNullOrWhiteSpace(edu.ScoreOrPercentage)
                        ? edu.Institution
                        : $"{edu.Institution} | {edu.ScoreOrPercentage}";
                    gfx.DrawString(leftLine1, itemHeaderFont, primaryBrush, margin, currentY + 9);

                    if (!string.IsNullOrWhiteSpace(edu.YearRange))
                    {
                        var ds = gfx.MeasureString(edu.YearRange, regularFont);
                        gfx.DrawString(edu.YearRange, regularFont, subTextBrush, margin + printableWidth - ds.Width, currentY + 9);
                    }
                    currentY += 12 * spacingMult;

                    string leftLine2 = string.IsNullOrWhiteSpace(edu.Specialization)
                        ? edu.Degree
                        : $"{edu.Degree} | {edu.Specialization}";
                    gfx.DrawString(leftLine2, itemSubFont, subTextBrush, margin, currentY + 9);
                    currentY += 14 * spacingMult;
                }
            }

            // ── 4. PROFESSIONAL EXPERIENCE ────────────────────────────────
            if (document.ShowExperience && document.Experiences.Count > 0)
            {
                DrawSectionHeading("PROFESSIONAL EXPERIENCE");
                foreach (var exp in document.Experiences)
                {
                    EnsureSpace(30 * spacingMult);
                    gfx.DrawString(exp.Company, itemHeaderFont, primaryBrush, margin, currentY + 9);
                    string dates = string.IsNullOrWhiteSpace(exp.EndDate) ? exp.StartDate : $"{exp.StartDate} - {exp.EndDate}";
                    if (!string.IsNullOrWhiteSpace(dates))
                    {
                        var ds = gfx.MeasureString(dates, regularFont);
                        gfx.DrawString(dates, regularFont, subTextBrush, margin + printableWidth - ds.Width, currentY + 9);
                    }
                    currentY += 12 * spacingMult;

                    gfx.DrawString(exp.RoleTitle, italicFont, subTextBrush, margin, currentY + 9);
                    if (!string.IsNullOrWhiteSpace(exp.Location))
                    {
                        var ls = gfx.MeasureString(exp.Location, regularFont);
                        gfx.DrawString(exp.Location, regularFont, subTextBrush, margin + printableWidth - ls.Width, currentY + 9);
                    }
                    currentY += 12 * spacingMult;

                    if (!string.IsNullOrWhiteSpace(exp.BulletsRaw))
                    {
                        foreach (var bullet in exp.BulletsRaw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                        {
                            string cb = bullet.TrimStart('•', '-', ' ', '*').Trim();
                            if (!string.IsNullOrEmpty(cb))
                            {
                                EnsureSpace(14 * spacingMult);
                                gfx.DrawString("•", regularFont, primaryBrush, margin + 4, currentY + 9);
                                currentY = DrawWrappedText(cb, regularFont, subTextBrush, margin + 14, currentY, printableWidth - 14, callEnsureBeforeFirst: false);
                                currentY += 2 * spacingMult;
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(exp.ProjectLink))
                    {
                        EnsureSpace(12);
                        double linkY = currentY;
                        gfx.DrawString($"Link: [{exp.ProjectLink}]", linkFont, linkBrush, margin + 14, currentY + 8);
                        AddLink(exp.ProjectLink, margin + 14, linkY, printableWidth - 14, 10);
                        currentY += 11 * spacingMult;
                    }
                    currentY += 4 * spacingMult;
                }
            }

            // ── 5. TECHNICAL SKILLS ───────────────────────────────────────
            if (document.ShowSkills && document.SkillCategories.Count > 0)
            {
                DrawSectionHeading("TECHNICAL SKILLS");
                foreach (var cat in document.SkillCategories)
                {
                    EnsureSpace(14 * spacingMult);
                    // Render category name BOLD, skills list Regular on same line
                    string catLabel = $"{cat.CategoryName}: ";
                    var catLabelSize = gfx.MeasureString(catLabel, itemSubFont);
                    gfx.DrawString(catLabel, itemSubFont, primaryBrush, margin, currentY + 9);
                    currentY = DrawWrappedText(cat.SkillsCsv, regularFont, subTextBrush,
                                               margin + catLabelSize.Width, currentY, printableWidth - catLabelSize.Width,
                                               callEnsureBeforeFirst: false);
                    currentY += 2 * spacingMult;
                }
                currentY += 4 * spacingMult;
            }

            // ── 6. KEY PROJECTS ───────────────────────────────────────────
            if (document.ShowProjects && document.Projects.Count > 0)
            {
                DrawSectionHeading("KEY PROJECTS");
                foreach (var proj in document.Projects)
                {
                    EnsureSpace(26 * spacingMult);
                    string projLeft = string.IsNullOrWhiteSpace(proj.TechStack) ? proj.Title : $"{proj.Title} | {proj.TechStack}";
                    gfx.DrawString(projLeft, itemHeaderFont, primaryBrush, margin, currentY + 9);
                    if (!string.IsNullOrWhiteSpace(proj.DateRange))
                    {
                        var ds = gfx.MeasureString(proj.DateRange, regularFont);
                        gfx.DrawString(proj.DateRange, regularFont, subTextBrush, margin + printableWidth - ds.Width, currentY + 9);
                    }
                    currentY += 12 * spacingMult;

                    if (!string.IsNullOrWhiteSpace(proj.BulletsRaw))
                    {
                        foreach (var bullet in proj.BulletsRaw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                        {
                            string cb = bullet.TrimStart('•', '-', ' ', '*').Trim();
                            if (!string.IsNullOrEmpty(cb))
                            {
                                EnsureSpace(14 * spacingMult);
                                gfx.DrawString("•", regularFont, primaryBrush, margin + 4, currentY + 9);
                                currentY = DrawWrappedText(cb, regularFont, subTextBrush, margin + 14, currentY, printableWidth - 14, callEnsureBeforeFirst: false);
                                currentY += 2 * spacingMult;
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(proj.RepoUrl))
                    {
                        EnsureSpace(12);
                        double linkY = currentY;
                        gfx.DrawString($"GitHub Repository - [{proj.RepoUrl}]", linkFont, linkBrush, margin + 14, currentY + 8);
                        AddLink(proj.RepoUrl, margin + 14, linkY, printableWidth - 14, 10);
                        currentY += 11 * spacingMult;
                    }
                    currentY += 4 * spacingMult;
                }
            }

            // ── 7. CERTIFICATIONS ─────────────────────────────────────────
            if (document.ShowCertifications && document.Certifications.Count > 0)
            {
                DrawSectionHeading("CERTIFICATIONS");
                foreach (var cert in document.Certifications)
                {
                    EnsureSpace(22 * spacingMult);
                    string certLeft = string.IsNullOrWhiteSpace(cert.Issuer) ? cert.Title : $"{cert.Title} | {cert.Issuer}";
                    gfx.DrawString(certLeft, itemHeaderFont, primaryBrush, margin, currentY + 9);
                    if (!string.IsNullOrWhiteSpace(cert.Date))
                    {
                        var ds = gfx.MeasureString(cert.Date, regularFont);
                        gfx.DrawString(cert.Date, regularFont, subTextBrush, margin + printableWidth - ds.Width, currentY + 9);
                    }
                    currentY += 12 * spacingMult;

                    if (!string.IsNullOrWhiteSpace(cert.GradeOrScore))
                    {
                        gfx.DrawString($"Grade: {cert.GradeOrScore}", italicFont, subTextBrush, margin + 8, currentY + 8);
                        currentY += 11 * spacingMult;
                    }
                    if (!string.IsNullOrWhiteSpace(cert.Description))
                    {
                        currentY = DrawWrappedText(cert.Description, regularFont, subTextBrush, margin + 8, currentY, printableWidth - 8);
                        currentY += 2 * spacingMult;
                    }
                    if (!string.IsNullOrWhiteSpace(cert.VerificationUrl))
                    {
                        EnsureSpace(12);
                        double linkY = currentY;
                        gfx.DrawString($"Verification Link - [{cert.VerificationUrl}]", linkFont, linkBrush, margin + 8, currentY + 8);
                        AddLink(cert.VerificationUrl, margin + 8, linkY, printableWidth - 8, 10);
                        currentY += 11 * spacingMult;
                    }
                    currentY += 3 * spacingMult;
                }
            }

            // ── 8. ACHIEVEMENTS ───────────────────────────────────────────
            if (document.ShowAchievements && document.Achievements.Count > 0)
            {
                DrawSectionHeading("ACHIEVEMENTS");
                foreach (var ach in document.Achievements)
                {
                    EnsureSpace(22 * spacingMult);
                    string achLeft = string.IsNullOrWhiteSpace(ach.Category) ? ach.Title : $"{ach.Category} | {ach.Title}";
                    gfx.DrawString(achLeft, itemHeaderFont, primaryBrush, margin, currentY + 9);
                    if (!string.IsNullOrWhiteSpace(ach.Date))
                    {
                        var ds = gfx.MeasureString(ach.Date, regularFont);
                        gfx.DrawString(ach.Date, regularFont, subTextBrush, margin + printableWidth - ds.Width, currentY + 9);
                    }
                    currentY += 12 * spacingMult;

                    if (!string.IsNullOrWhiteSpace(ach.Description))
                    {
                        currentY = DrawWrappedText(ach.Description, regularFont, subTextBrush, margin + 8, currentY, printableWidth - 8);
                        currentY += 2 * spacingMult;
                    }
                    if (!string.IsNullOrWhiteSpace(ach.Link))
                    {
                        EnsureSpace(12);
                        double linkY = currentY;
                        gfx.DrawString($"Achievement Link - [{ach.Link}]", linkFont, linkBrush, margin + 8, currentY + 8);
                        AddLink(ach.Link, margin + 8, linkY, printableWidth - 8, 10);
                        currentY += 11 * spacingMult;
                    }
                    currentY += 3 * spacingMult;
                }
            }

            // ── 9. POSITIONS OF RESPONSIBILITY ────────────────────────────
            if (document.ShowResponsibilities && document.Responsibilities.Count > 0)
            {
                DrawSectionHeading("POSITION OF RESPONSIBILITY");
                foreach (var resp in document.Responsibilities)
                {
                    EnsureSpace(22 * spacingMult);
                    gfx.DrawString(resp.Role, itemHeaderFont, primaryBrush, margin, currentY + 9);
                    if (!string.IsNullOrWhiteSpace(resp.DateRange))
                    {
                        var ds = gfx.MeasureString(resp.DateRange, regularFont);
                        gfx.DrawString(resp.DateRange, regularFont, subTextBrush, margin + printableWidth - ds.Width, currentY + 9);
                    }
                    currentY += 12 * spacingMult;

                    if (!string.IsNullOrWhiteSpace(resp.Organization))
                    {
                        gfx.DrawString(resp.Organization, italicFont, subTextBrush, margin, currentY + 9);
                        currentY += 11 * spacingMult;
                    }

                    if (!string.IsNullOrWhiteSpace(resp.BulletsRaw))
                    {
                        foreach (var bullet in resp.BulletsRaw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                        {
                            string cb = bullet.TrimStart('•', '-', ' ', '*').Trim();
                            if (!string.IsNullOrEmpty(cb))
                            {
                                EnsureSpace(14 * spacingMult);
                                gfx.DrawString("•", regularFont, primaryBrush, margin + 4, currentY + 9);
                                currentY = DrawWrappedText(cb, regularFont, subTextBrush, margin + 14, currentY, printableWidth - 14, callEnsureBeforeFirst: false);
                                currentY += 2 * spacingMult;
                            }
                        }
                    }
                    currentY += 3 * spacingMult;
                }
            }

            gfx.Dispose();

            // ═══════════════════════════════════════════════════════════════
            //  PASS 2 — RENDER PAGE X OF Y FOOTERS ON EVERY PAGE
            // ═══════════════════════════════════════════════════════════════
            int totalPages = pageList.Count;
            for (int i = 0; i < totalPages; i++)
            {
                using var footerGfx = XGraphics.FromPdfPage(pageList[i]);
                string footerText = $"Page {i + 1} of {totalPages}";
                var footerSize = footerGfx.MeasureString(footerText, footerFont);
                footerGfx.DrawString(footerText, footerFont, footerBrush,
                    margin + (printableWidth - footerSize.Width) / 2,
                    pageHeight - margin / 2 + footerFont.Size);
            }

            using var ms = new MemoryStream();
            pdfDoc.Save(ms, false);
            return ms.ToArray();
        });
    }

    private static PdfPage AddPage(PdfDocument doc, List<PdfPage> list, double w, double h)
    {
        var page = doc.AddPage();
        page.Width  = w;
        page.Height = h;
        list.Add(page);
        return page;
    }
}
