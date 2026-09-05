using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Validates extension executable integrity, architecture, and installer packages.
/// </summary>
public interface IExtensionValidator
{
    Task<ExtensionValidationResult> ValidateExtensionAsync(
        ExtensionModel extension,
        CancellationToken ct = default);

    Task<bool> ValidateInstallerAsync(
        string installerPath,
        string? expectedSha256,
        string supportedArch,
        CancellationToken ct = default);
}