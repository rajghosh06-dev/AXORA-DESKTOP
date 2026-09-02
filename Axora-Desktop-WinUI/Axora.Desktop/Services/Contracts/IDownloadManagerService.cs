using System.Collections.ObjectModel;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

public interface IDownloadManagerService
{
    ObservableCollection<QuickDropItem> Transfers { get; }
    void AddTransfer(QuickDropItem item);
    void UpdateProgress(string itemId, double progress, double speedBps);
    void CompleteTransfer(string itemId, string localPath);
    void FailTransfer(string itemId, string reason);
    void OpenFile(QuickDropItem item);
    void ShowInExplorer(QuickDropItem item);
}
