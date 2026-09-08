using System.Collections.Generic;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Service contract for deterministic, bounded, and source-aware context window formulation.
/// Transforms discrete, atomic passage chunks into coherent context windows for downstream
/// consumers (dense vector embeddings, grounded SLM synthesis, offline RAG assistant, and study artifact extraction).
/// </summary>
public interface IBoundedContextWindowBuilder
{
    /// <summary>
    /// Formulates a bounded context window around a specific focal passage chunk on a page.
    /// Expands symmetrically or asymmetrically within [kMin, kMax] according to options,
    /// deduplicating stride overlaps using NormalizedText coordinate spans.
    /// </summary>
    BoundedContextWindow FormulateFocalWindow(
        DocumentPassageChunk focalChunk,
        DocumentPage page,
        ContextWindowOptions? options = null);

    /// <summary>
    /// Formulates a sliding sequence of bounded context windows across an entire document page (stride = 1 chunk).
    /// </summary>
    IReadOnlyList<BoundedContextWindow> FormulatePageWindows(
        DocumentPage page,
        string documentId,
        ContextWindowOptions? options = null);

    /// <summary>
    /// Assembles an ordered, deduplicated, and budgeted multi-passage prompt context window
    /// from heterogeneous retrieved candidate chunks across one or more documents or pages.
    /// </summary>
    BoundedContextWindow FormulateCompositeWindow(
        IEnumerable<DocumentPassageChunk> retrievedChunks,
        ScholarDocument? document = null,
        ContextWindowOptions? options = null);
}
