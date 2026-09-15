using System;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Default uninstalled/unavailable capability seam for Class B Small Language Models.
/// Ensures that core Class A deterministic extractive synthesis operates 100% offline
/// with 0 MB model weight footprint without requiring neural runtimes.
/// </summary>
public sealed class NullScholarSlmModelDriver : IScholarSlmModelDriver
{
    public bool IsAvailable => false;

    public string ModelName => "Microsoft Phi-3-mini-4k-instruct (Uninstalled)";

    public string ExecutionProvider => "None";

    public Task<bool> EnsureLoadedAsync(CancellationToken ct = default)
    {
        return Task.FromResult(false);
    }

    public Task<string> GenerateTextAsync(
        string prompt,
        float temperature = 0.0f,
        float topP = 1.0f,
        CancellationToken ct = default)
    {
        throw new InvalidOperationException("Class B Local SLM model is not installed. Use Class A Extractive Heuristic or install Phi-3 via Download Manager.");
    }

    public Task UnloadAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
