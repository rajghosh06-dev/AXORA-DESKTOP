namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Contract for the local Windows AI / ONNX embedding service.
/// Provides on-device text generation and semantic embedding without any cloud calls.
/// </summary>
public interface IWindowsAiService
{
    /// <summary>Whether a capable inference runtime is available on this device.</summary>
    bool IsAvailable { get; }

    /// <summary>Human-readable description of the active execution provider (e.g. "DirectML — NVIDIA RTX 4070").</summary>
    string ActiveProviderDescription { get; }

    /// <summary>
    /// Generates a semantic embedding vector for the provided text chunk.
    /// Uses ONNX Runtime DirectML with fallback to CPU.
    /// </summary>
    /// <param name="textChunk">Input text (max ~512 characters for all-MiniLM-L6-v2).</param>
    /// <returns>384-dimensional float embedding vector.</returns>
    Task<float[]> GenerateEmbeddingAsync(string textChunk, CancellationToken ct = default);

    /// <summary>
    /// Generates a free-form text response from the on-device language model.
    /// Requires Phi Silica on Copilot+ PCs; falls back to a stub on other hardware.
    /// </summary>
    Task<string> GenerateResponseAsync(string prompt, CancellationToken ct = default);
}
