namespace Axora.Desktop.Models;

/// <summary>
/// Simplified dependency readiness state for consumer feature pages and banners.
/// </summary>
public enum DependencyUiState
{
    Ready,
    Missing,
    UpdateAvailable,
    Broken
}