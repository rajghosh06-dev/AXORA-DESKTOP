using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Primary coordinator for external dependencies and extensions.
/// Enforces user-controlled updates (no silent installs or upgrades).
/// </summary>
public interface IDependencyManager
{
    IReadOnlyList<ExtensionModel> Extensions { get; }

    Task InitializeAsync(CancellationToken ct = default);
    Task RefreshAllStatusAsync(bool checkRemoteVersions = false, CancellationToken ct = default);
    Task<ExtensionStatus> CheckStatusAsync(string extensionId, bool forceRefresh = false, bool checkRemoteVersion = false, CancellationToken ct = default);

    bool IsDependencyReady(string extensionId);
    ExtensionModel? GetExtension(string extensionId);

    Task<bool> InstallExtensionAsync(string extensionId, CancellationToken ct = default);
    Task<bool> UpdateExtensionAsync(string extensionId, CancellationToken ct = default);
    Task<bool> RepairExtensionAsync(string extensionId, CancellationToken ct = default);
    Task<bool> ReinstallExtensionAsync(string extensionId, CancellationToken ct = default);
    Task<bool> CleanReinstallExtensionAsync(string extensionId, CancellationToken ct = default);
    Task<bool> RemoveExtensionAsync(string extensionId, CancellationToken ct = default);
    Task ClearAllCacheAsync(CancellationToken ct = default);

    event EventHandler<ExtensionStateChangedEventArgs>? DependencyStatusChanged;
}