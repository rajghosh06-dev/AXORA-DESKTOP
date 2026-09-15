using System.Threading;
using System.Threading.Tasks;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Abstraction seam for optional local Small Language Model (SLM) inference.
/// Enables plug-in SLM execution (e.g. Microsoft Phi-3-mini-4k-instruct ONNX DirectML/CPU)
/// without coupling core synthesis workflows to specific neural runtimes or weight files.
/// </summary>
public interface IScholarSlmModelDriver
{
    /// <summary>
    /// Indicates whether model weights are installed and the execution provider is ready.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Display name of the configured model.
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Active execution provider ("DirectML", "CPU", "None").
    /// </summary>
    string ExecutionProvider { get; }

    /// <summary>
    /// Ensures model weights and ONNX runtime sessions are loaded into memory/VRAM.
    /// </summary>
    Task<bool> EnsureLoadedAsync(CancellationToken ct = default);

    /// <summary>
    /// Executes deterministic text generation using greedy sampling (temperature=0.0f, top_p=1.0f).
    /// </summary>
    Task<string> GenerateTextAsync(
        string prompt,
        float temperature = 0.0f,
        float topP = 1.0f,
        CancellationToken ct = default);

    /// <summary>
    /// Unloads model weights and reclaims memory/VRAM.
    /// </summary>
    Task UnloadAsync(CancellationToken ct = default);
}
