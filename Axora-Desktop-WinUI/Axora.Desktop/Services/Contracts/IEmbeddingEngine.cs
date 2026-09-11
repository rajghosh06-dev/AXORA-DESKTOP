using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Service abstraction for local vector embedding generation with hardware acceleration
/// and resilient multithreaded CPU and Class A lexical fallback.
/// </summary>
public interface IEmbeddingEngine
{
    /// <summary>
    /// Unique identifier of the embedding model (e.g. "all-MiniLM-L6-v2" or "class_a_lexical").
    /// </summary>
    string ModelId { get; }

    /// <summary>
    /// Cryptographic SHA-256 fingerprint of the model weight artifact, or "class_a_lexical_hashing" for fallback.
    /// </summary>
    string ModelFingerprint { get; }

    /// <summary>
    /// Embedding vector dimensionality (mandated 384 for all-MiniLM-L6-v2 and Class A projection).
    /// </summary>
    int Dimension { get; }

    /// <summary>
    /// Currently active hardware or software execution provider.
    /// </summary>
    EmbeddingExecutionProvider ActiveProvider { get; }

    /// <summary>
    /// Generates a normalized 384-dimensional dense vector embedding for a single text input.
    /// Output vector MUST satisfy L2 unit normalization: ||v||2 = 1.0 +/- 10^-5.
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Generates normalized 384-dimensional dense vector embeddings for a bounded batch of text inputs in [1, 32].
    /// Texts list count outside [1, 32] MUST throw ArgumentOutOfRangeException with code ERR_BATCH_SIZE_OUT_OF_RANGE.
    /// </summary>
    Task<IReadOnlyList<float[]>> GenerateBatchEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
