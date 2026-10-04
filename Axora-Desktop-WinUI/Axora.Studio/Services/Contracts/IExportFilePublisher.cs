using Axora.Studio.Models;

namespace Axora.Studio.Services.Contracts;

public interface IExportFilePublisher
{
    // Read-only. An existing-file plan must subsequently be approved by the future consent owner.
    ExportPreparation Prepare(string destination, FlashcardExportFormat format, CancellationToken token = default);
    // Caller owns/awaits the complete worker, including noncancelable post-admission settlement.
    Task<ExportPublicationResult> PublishAsync(ExportDestinationPlan plan, FlashcardExportSnapshot snapshot,
        CancellationToken token = default);
}
