using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Provides distinct Repair, Reinstall, and Clean Reinstall lifecycle operations.
/// </summary>
public interface IExtensionRepairService
{
    Task<bool> RepairAsync(ExtensionModel extension, CancellationToken ct = default);
    Task<bool> ReinstallAsync(ExtensionModel extension, CancellationToken ct = default);
    Task<bool> CleanReinstallAsync(ExtensionModel extension, CancellationToken ct = default);
}