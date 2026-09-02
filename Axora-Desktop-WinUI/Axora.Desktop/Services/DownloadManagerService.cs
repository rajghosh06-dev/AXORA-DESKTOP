using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.UI.Dispatching;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Download and Transfer Manager — manages incoming QuickDrop transfers,
/// live speed metrics, disk staging, and shell integrations.
///
/// Threading (FIX W-8):
///   DispatcherQueue is resolved lazily on first use rather than captured in the constructor.
///   This avoids the scenario where the singleton constructor is invoked during Host.Build()
///   before the WinUI XAML dispatcher has been initialized, which would cause
///   DispatcherQueue.GetForCurrentThread() to return null and all subsequent TryEnqueue
///   calls to fail silently.
/// </summary>
public sealed class DownloadManagerService : IDownloadManagerService
{
    private readonly IAppSettingsService _settings;

    // Lazily resolved to ensure we capture the UI thread's DispatcherQueue after WinUI activation.
    private DispatcherQueue? _dispatcher;

    public ObservableCollection<QuickDropItem> Transfers { get; } = [];

    public DownloadManagerService(IAppSettingsService settings)
    {
        _settings = settings;
        var downloadDir = _settings.DownloadDirectory;
        Directory.CreateDirectory(downloadDir);
    }

    /// <summary>
    /// Resolves the UI DispatcherQueue lazily on first use rather than captured in the constructor.
    /// This avoids the scenario where the singleton constructor is invoked during Host.Build()
    /// before the WinUI XAML dispatcher has been initialized, which would cause
    /// DispatcherQueue.GetForCurrentThread() to return null and all subsequent TryEnqueue
    /// calls to fail silently.
    /// </summary>
    private void EnqueueOnUiThread(Action action)
    {
        // Lazily capture the UI dispatcher on first use — by this point WinUI is always initialized
        _dispatcher ??= DispatcherQueue.GetForCurrentThread();

        if (_dispatcher is not null && !_dispatcher.HasThreadAccess)
        {
            // DispatcherQueueHandler is a WinRT delegate; wrap Action explicitly to satisfy the type system
            _dispatcher.TryEnqueue(new DispatcherQueueHandler(action));
        }
        else
        {
            // Either already on UI thread, or no dispatcher available — execute inline
            action();
        }
    }

    public void AddTransfer(QuickDropItem item)
    {
        EnqueueOnUiThread(() => Transfers.Insert(0, item));
    }

    public void UpdateProgress(string itemId, double progress, double speedBps)
    {
        EnqueueOnUiThread(() =>
        {
            var item = Transfers.FirstOrDefault(t => t.ItemId == itemId);
            if (item != null)
            {
                item.Progress = progress;
                item.TransferSpeedBps = speedBps;
                item.Status = item.IsOutgoing ? TransferStatus.Sending : TransferStatus.Receiving;
            }
        });
    }

    public void CompleteTransfer(string itemId, string localPath)
    {
        EnqueueOnUiThread(() =>
        {
            var item = Transfers.FirstOrDefault(t => t.ItemId == itemId);
            if (item != null)
            {
                item.Progress = 1.0;
                item.LocalPath = localPath;
                item.Status = TransferStatus.Completed;
            }
        });
    }

    public void FailTransfer(string itemId, string reason)
    {
        EnqueueOnUiThread(() =>
        {
            var item = Transfers.FirstOrDefault(t => t.ItemId == itemId);
            if (item != null)
            {
                item.Status = TransferStatus.Failed;
            }
        });
    }

    public void OpenFile(QuickDropItem item)
    {
        if (File.Exists(item.LocalPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = item.LocalPath,
                UseShellExecute = true
            });
        }
        else
        {
            ShowInExplorer(item);
        }
    }

    public void ShowInExplorer(QuickDropItem item)
    {
        var targetDir = !string.IsNullOrWhiteSpace(item.LocalPath)
            && Directory.Exists(Path.GetDirectoryName(item.LocalPath))
            ? Path.GetDirectoryName(item.LocalPath)!
            : _settings.DownloadDirectory;

        Directory.CreateDirectory(targetDir);
        Process.Start(new ProcessStartInfo
        {
            FileName = targetDir,
            UseShellExecute = true
        });
    }
}
