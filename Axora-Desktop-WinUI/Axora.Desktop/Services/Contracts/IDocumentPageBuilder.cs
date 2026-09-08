using System.Collections.Generic;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Builder contract for constructing DocumentPage instances with explicit page semantics and boundary enforcement.
/// Ensures zero fabricated physical page numbers.
/// </summary>
public interface IDocumentPageBuilder
{
    /// <summary>
    /// Constructs a single DocumentPage preserving unmutated RawText ground truth and page semantics.
    /// </summary>
    DocumentPage BuildPage(
        int pageNumber,
        string rawText,
        PageSemanticsType semantics = PageSemanticsType.PhysicalPage,
        double width = 0.0,
        double height = 0.0,
        string? normalizedText = null);

    /// <summary>
    /// Groups logical text sections into a sequence of DocumentPage instances with LogicalSection semantics.
    /// </summary>
    IReadOnlyList<DocumentPage> BuildPagesFromLogicalSections(
        IReadOnlyList<string> sectionTexts,
        PageSemanticsType semantics = PageSemanticsType.LogicalSection);
}
