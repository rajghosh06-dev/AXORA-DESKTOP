using System;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Capability inspection contract integrating with the AXORA Extension/Download Manager
/// to expose current local AI embedding availability without silent downloads or egress.
/// </summary>
public interface IEmbeddingCapabilityStateProvider
{
    /// <summary>
    /// True if the optional Class B neural model (all-MiniLM-L6-v2) is present and verified on disk.
    /// </summary>
    bool IsNeuralModelInstalled { get; }

    /// <summary>
    /// True if Direct3D 12 hardware acceleration is supported and accessible on the local system.
    /// </summary>
    bool IsDirectMlSupported { get; }

    /// <summary>
    /// Returns the user-facing capability status badge text (e.g. "Ready (Lexical Only)" or "Neural Engine Active").
    /// In accordance with INV-W3D-28, when the neural model is absent, this MUST return "Ready (Lexical Only)".
    /// </summary>
    string GetCapabilityStatusBadgeText();

    /// <summary>
    /// Detailed diagnostic description of the active provider (e.g. "DirectML (GPU accelerated)", "CPU ONNX", etc.).
    /// </summary>
    string GetActiveProviderDescription();
}
