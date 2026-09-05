using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Executes staged installation, deployment, and removal of external dependencies.
/// </summary>
public interface IExtensionInstaller
{
    Task<bool> InstallAsync(
        ExtensionModel extension,
        string stagedInstallerPath,
        CancellationToken ct = default);

    Task<bool> UninstallAsync(
        ExtensionModel extension,
        CancellationToken ct = default);
}