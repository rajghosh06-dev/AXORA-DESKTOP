using Axora.Studio.Models;
using Axora.Studio.Services.Contracts;

namespace Axora.Studio.Services;

public sealed record FlashcardExportOutcome(ExportResultState State, string ReasonCode, string Message, ExportPublicationResult? Publication = null);

/// <summary>Owns the entire export operation, including dialog release and publisher safety settlement.</summary>
public sealed class FlashcardExportCoordinator(IStudioSavePicker picker, IExportFilePublisher publisher)
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cancel;
    private Task<FlashcardExportOutcome>? _active;
    private Task? _driver;
    private Task? _stop;
    private bool _closed;
    public bool IsActive { get { lock (_gate) return _active is { IsCompleted: false }; } }

    public Task<FlashcardExportOutcome> ExportAsync(nint owner, FlashcardExportFormat format, Func<ExportSnapshotResult> capture)
    {
        TaskCompletionSource<FlashcardExportOutcome> completion;
        CancellationTokenSource cancel;
        lock (_gate)
        {
            if (_closed) return Task.FromResult(Failure("AdmissionClosed", "Studio is closing; export is unavailable."));
            if (_active is { IsCompleted: false }) return Task.FromResult(Failure("Busy", "An export is already in progress."));
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            cancel = _cancel = new();
            _active = completion.Task; // Publish ownership before capture, dialogs or cancellation can reenter.
        }
        ExportSnapshotResult snapshot;
        try { snapshot = capture(); }
        catch (Exception) { snapshot = new(null, ExportResultState.Rejected, "SnapshotCaptureFailed"); }
        _driver = RunAsync(owner, format, snapshot, cancel, completion);
        return completion.Task;
    }

    private async Task RunAsync(nint owner, FlashcardExportFormat format, ExportSnapshotResult captured,
        CancellationTokenSource cancel, TaskCompletionSource<FlashcardExportOutcome> completion)
    {
        var outcome = Failure("Unsettled", "Export did not complete.");
        bool publisherStarted = false;
        try
        {
            if (captured.Snapshot is not { } snapshot) { outcome = Failure(captured.ReasonCode, "Select a valid deck before exporting."); return; }
            cancel.Token.ThrowIfCancellationRequested();
            Guid operationId = Guid.NewGuid();
            var selected = await picker.SelectAsync(owner, format, ExportFileNameSanitizer.Suggest(snapshot.Deck.Title, format), cancel.Token).ConfigureAwait(false);
            if (selected.State != StudioPickerState.Selected)
            {
                outcome = selected.State == StudioPickerState.Canceled ? Canceled() : Failure("PickerFailed", "Save As could not open or select a safe path."); return;
            }
            var preparation = await Task.Run(() => publisher.Prepare(selected.Path!, format, cancel.Token)).ConfigureAwait(false);
            if (preparation.Plan is not { } plan)
            { outcome = new(preparation.Failure ?? ExportResultState.InvalidDestination, preparation.ReasonCode, Status(preparation.Failure ?? ExportResultState.InvalidDestination, snapshot.Deck.Cards.Count)); return; }
            if (plan.Existing is not null)
            {
                var consent = await picker.ConfirmReplacementAsync(owner, operationId, plan, cancel.Token).ConfigureAwait(false);
                if (consent != StudioPickerState.Selected) { outcome = consent == StudioPickerState.Canceled ? Canceled() : Failure("ConsentFailed", "Replacement confirmation could not complete."); return; }
                plan = plan.ApproveReplacement(); // Local to this operation; preserves this exact canonical path/fingerprint.
            }
            cancel.Token.ThrowIfCancellationRequested();
            publisherStarted = true;
            var published = await publisher.PublishAsync(plan, snapshot, cancel.Token).ConfigureAwait(false);
            outcome = new(published.State, published.ReasonCode, Status(published.State, snapshot.Deck.Cards.Count, published.CleanupWarnings.Count > 0), published);
        }
        catch (OperationCanceledException) when (!publisherStarted) { outcome = Canceled(); }
        catch (Exception)
        { outcome = publisherStarted ? new(ExportResultState.Indeterminate, "UnexpectedPublisherFailure", "Indeterminate file state; review the destination and any recovery files.")
            : Failure("ExportFailed", "Export could not complete; no publication was attempted."); }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_cancel, cancel)) _cancel = null;
                cancel.Dispose();
                completion.TrySetResult(outcome); // Admission stays occupied until all awaited safety work has settled.
            }
        }
    }
    public Task StopAsync()
    {
        lock (_gate)
        {
            if (_stop is not null) return _stop;
            _closed = true;
            _stop = _active ?? Task.CompletedTask;
            try { _cancel?.Cancel(); } catch (Exception) { /* Retain active task ownership even if a cancellation observer failed. */ }
            try { picker.RequestCancel(); } catch (Exception) { /* Never tear down underneath an unsettled dialog/publisher. */ }
            return _stop;
        }
    }
    private static FlashcardExportOutcome Failure(string code, string message) => new(ExportResultState.Rejected, code, message);
    private static FlashcardExportOutcome Canceled() => new(ExportResultState.Canceled, "CanceledBeforePublication", Status(ExportResultState.Canceled, 0));
    public static string Status(ExportResultState state, int cards, bool cleanupWarning = false) => state switch
    {
        ExportResultState.Published => $"Exported successfully: {cards} cards." + (cleanupWarning ? " Cleanup warning; review retained recovery files." : ""),
        ExportResultState.Canceled => "Export canceled — no destination file changed by Studio.",
        ExportResultState.InvalidDestination => "Unsupported safe location. Choose an ordinary writable file on a local fixed NTFS drive.",
        ExportResultState.DestinationChanged => "Destination changed — nothing was overwritten by Studio.",
        ExportResultState.PublishedButVerificationFailed => "Publication completed but verification failed; recovery retained where available.",
        ExportResultState.Indeterminate => "Indeterminate file state; review the destination and recovery files.",
        _ => "Export failed before verified completion. Review any disclosed recovery files."
    };
}

/// <summary>App-owned lazy session and shutdown gate. Closing an unused session never invokes its factory.</summary>
public sealed class StudioExportSession(Func<FlashcardExportCoordinator> factory)
{
    private readonly object _gate = new();
    private readonly Lazy<FlashcardExportCoordinator> _feature = new(factory);
    private bool _closed;
    private Task? _shutdown;
    private Task? _shutdownDriver;
    public bool IsCreated { get { lock (_gate) return _feature.IsValueCreated; } }
    public bool IsActive { get { lock (_gate) return _feature.IsValueCreated && _feature.Value.IsActive; } }
    public Task<FlashcardExportOutcome> ExportAsync(nint owner, FlashcardExportFormat format, Func<ExportSnapshotResult> capture)
    {
        lock (_gate)
        {
            if (_closed) return Task.FromResult(new FlashcardExportOutcome(ExportResultState.Rejected, "AdmissionClosed", "Studio is closing; export is unavailable."));
            return _feature.Value.ExportAsync(owner, format, capture);
        }
    }
    public Task ShutdownAsync(Func<Task> shutdownHost)
    {
        lock (_gate)
        {
            if (_shutdown is not null) return _shutdown;
            _closed = true;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _shutdown = completion.Task; // Publish the one stop task before cancellation can reenter.
            var settlement = _feature.IsValueCreated ? _feature.Value.StopAsync() : Task.CompletedTask;
            _shutdownDriver = ShutdownCoreAsync(settlement, shutdownHost, completion);
            return _shutdown;
        }
    }
    private static async Task ShutdownCoreAsync(Task settlement, Func<Task> shutdownHost, TaskCompletionSource completion)
    {
        try
        {
            await settlement.ConfigureAwait(false); // Settlement must survive loss of the UI message loop.
            await shutdownHost().ConfigureAwait(false);
            completion.TrySetResult();
        }
        catch (Exception ex) { completion.TrySetException(ex); }
    }
}
