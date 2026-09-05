using System;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Downloads external extension installers securely over HTTPS into isolated staged cache directories.
/// </summary>
public interface IExtensionDownloader
{
    Task<string> DownloadExtensionAsync(
        ExtensionModel extension,
        IProgress<double>? progress = null,
        CancellationToken ct = default);
}