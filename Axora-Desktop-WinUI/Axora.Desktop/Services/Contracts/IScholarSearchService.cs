using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Primary high-level service contract for grounded multi-document Scholar search,
/// session retrieval, query normalization, and hybrid fusion.
/// </summary>
public interface IScholarSearchService
{
    /// <summary>
    /// Executes a fully-configured search request across the requested document scope,
    /// applying hybrid scoring, location filtering, score thresholds, and tie-breaking.
    /// </summary>
    Task<ScholarSearchResponse> SearchAsync(
        ScholarSearchRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Convenience method for targeted single-document queries.
    /// Reuses the authoritative W3-D scoring path directly for deterministic parity.
    /// </summary>
    Task<ScholarSearchResponse> SearchDocumentAsync(
        string documentId,
        string queryText,
        int topK = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Convenience method for querying all documents enrolled in an active study session.
    /// </summary>
    Task<ScholarSearchResponse> SearchSessionAsync(
        string sessionId,
        string queryText,
        int topK = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates current search capability and active hardware/provider status.
    /// </summary>
    Task<SearchCapabilityStatus> GetCapabilityStatusAsync(
        CancellationToken ct = default);
}
