using System.Collections.Generic;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Constructs DocumentPage instances preserving page semantics and boundary invariants.
/// Enforces zero fabricated physical page numbers.
/// </summary>
public sealed class DocumentPageBuilder : IDocumentPageBuilder
{
    public DocumentPage BuildPage(
        int pageNumber,
        string rawText,
        PageSemanticsType semantics = PageSemanticsType.PhysicalPage,
        double width = 0.0,
        double height = 0.0,
        string? normalizedText = null)
    {
        return new DocumentPage
        {
            PageNumber = pageNumber,
            Width = width,
            Height = height,
            RawText = rawText ?? string.Empty,
            PageSemantics = semantics,
            NormalizedText = normalizedText,
            Chunks = []
        };
    }

    public IReadOnlyList<DocumentPage> BuildPagesFromLogicalSections(
        IReadOnlyList<string> sectionTexts,
        PageSemanticsType semantics = PageSemanticsType.LogicalSection)
    {
        if (sectionTexts == null || sectionTexts.Count == 0)
        {
            return [BuildPage(1, string.Empty, semantics)];
        }

        var pages = new List<DocumentPage>(sectionTexts.Count);
        for (int i = 0; i < sectionTexts.Count; i++)
        {
            pages.Add(BuildPage(i + 1, sectionTexts[i] ?? string.Empty, semantics));
        }

        return pages;
    }
}
