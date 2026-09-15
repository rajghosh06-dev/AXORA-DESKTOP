using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Primary service contract for grounded local academic study synthesis, concept extraction,
/// practice quiz generation, and cross-document comparative analysis.
/// </summary>
public interface IScholarSynthesisEngine
{
    /// <summary>
    /// Synthesizes an executive study summary from the requested document or session scope.
    /// </summary>
    Task<ExecutiveSummaryResult> GenerateSummaryAsync(
        StudySynthesisRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts key concepts, terminology, definitions, and categories with authentic citations.
    /// </summary>
    Task<StudyConceptExtractionResult> ExtractConceptsAsync(
        StudySynthesisRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Generates practice quiz questions with expected answers, difficulty, and citation provenance.
    /// </summary>
    Task<PracticeQuizGenerationResult> GenerateQuizAsync(
        StudySynthesisRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Performs a cross-document comparative analysis identifying thematic consensus and factual discrepancies.
    /// </summary>
    Task<ComparativeSynthesisResult> CompareDocumentsAsync(
        StudySynthesisRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Performs an all-in-one comprehensive study synthesis (Summary + Concepts + Quiz + Comparative Analysis).
    /// </summary>
    Task<ComprehensiveSynthesisResult> SynthesizeAllAsync(
        StudySynthesisRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates active synthesis engine capability and active hardware/provider status.
    /// </summary>
    Task<SynthesisEngineStatus> GetCapabilityStatusAsync(
        CancellationToken ct = default);
}
