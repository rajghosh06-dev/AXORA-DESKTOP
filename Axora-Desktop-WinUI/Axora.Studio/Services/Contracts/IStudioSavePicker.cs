using Axora.Studio.Models;

namespace Axora.Studio.Services.Contracts;

public enum StudioPickerState { Selected, Canceled, Failed }
public sealed record StudioPickerResult(StudioPickerState State, string? Path = null, string ReasonCode = "");

/// <summary>Studio export path selection and per-operation owner-bound consent only.</summary>
public interface IStudioSavePicker
{
    Task<StudioPickerResult> SelectAsync(nint owner, FlashcardExportFormat format, string suggestedName, CancellationToken token);
    Task<StudioPickerState> ConfirmReplacementAsync(nint owner, Guid operationId, ExportDestinationPlan plan, CancellationToken token);
    void RequestCancel();
}
