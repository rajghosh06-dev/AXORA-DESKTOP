using Axora.Studio.Services.Contracts;

namespace Axora.Studio.Services;

/// <summary>One native owner, one latest pending intent, and an independently retained cleanup driver.</summary>
public sealed class FlashcardReadAloudService : IFlashcardReadAloudService
{
    private readonly object _gate = new();
    private readonly IFlashcardReadAloudBackend _backend;
    private readonly Func<CancellationToken, Task> _deadline;
    private readonly Action<string>? _log;
    private Operation? _active, _pending;
    private Task? _driver;
    private Task<ReadAloudResult>? _stop;
    private long _generation;
    private bool _closed, _unavailable;

    private sealed class Operation(ReadAloudRequest request, long generation, Action<ReadAloudProgress>? progress)
    {
        private readonly object _cancelGate = new();
        private bool _disposed;
        public ReadAloudRequest Request { get; } = request;
        public long Generation { get; } = generation;
        public Action<ReadAloudProgress>? Progress { get; } = progress;
        public CancellationTokenSource Cancel { get; } = new();
        public TaskCompletionSource CancelRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ReadAloudResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ReadAloudOutcome? CancelReason;
        public Task<ReadAloudResult>? NativeSettlement;
        public void RequestCancel()
        {
            lock (_cancelGate)
            {
                if (_disposed) return;
                CancelRequested.TrySetResult();
                try { Cancel.Cancel(); } catch (Exception) { /* Ownership remains retained if a signal observer fails. */ }
            }
        }
        public void ReleaseCancellation() { lock (_cancelGate) { _disposed = true; Cancel.Dispose(); } }
    }

    public FlashcardReadAloudService(IFlashcardReadAloudBackend backend,
        Func<CancellationToken, Task>? deadline = null, Action<string>? log = null)
    {
        _backend = backend;
        _deadline = deadline ?? (token => Task.Delay(TimeSpan.FromSeconds(5), token));
        _log = log;
    }
    public bool IsActive { get { lock (_gate) return _active is not null; } }
    public Task<ReadAloudResult> ReadAsync(ReadAloudRequest request, Action<ReadAloudProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OperationId == Guid.Empty || !ReadAloudText.IsValid(request.Text))
            return Result(request.OperationId, ReadAloudOutcome.InvalidText, "InvalidText");
        Operation? cancel = null, superseded = null;
        Operation operation;
        lock (_gate)
        {
            if (_closed || _unavailable) return Result(request.OperationId, ReadAloudOutcome.Unavailable,
                _closed ? "AdmissionClosed" : "BackendUnavailable");
            operation = new(request, ++_generation, progress);
            if (_active is null)
            {
                _active = operation;
                _driver = Task.Run(DriveAsync); // Session retains this task through late native cleanup.
            }
            else
            {
                superseded = _pending;
                _pending = operation;
                cancel = _active;
                cancel.CancelReason ??= ReadAloudOutcome.Replaced;
            }
        }
        FinishPending(superseded, ReadAloudOutcome.Replaced, "SupersededBeforeStart");
        cancel?.RequestCancel(); // Registered backend callbacks signal managed tasks only.
        return operation.Completion.Task;
    }
    public Task<ReadAloudResult> CancelCurrentAsync() => Cancel(permanent: false);
    public Task<ReadAloudResult> StopAsync() => Cancel(permanent: true);
    private Task<ReadAloudResult> Cancel(bool permanent)
    {
        Operation? active, pending;
        Task<ReadAloudResult> task;
        lock (_gate)
        {
            if (permanent && _stop is not null) return _stop;
            if (permanent) _closed = true;
            ++_generation;
            pending = _pending; _pending = null;
            active = _active;
            if (active is not null) active.CancelReason = ReadAloudOutcome.Canceled;
            task = active?.Completion.Task ?? Result(Guid.Empty, ReadAloudOutcome.Canceled, "Idle");
            if (permanent) _stop = task;
        }
        FinishPending(pending, ReadAloudOutcome.Canceled, "CanceledBeforeStart");
        active?.RequestCancel();
        return task;
    }
    private static Task<ReadAloudResult> Result(Guid id, ReadAloudOutcome outcome, string reason) => Task.FromResult(new ReadAloudResult(id, outcome, reason));
    private static void FinishPending(Operation? operation, ReadAloudOutcome outcome, string reason)
    {
        if (operation is null) return;
        operation.Completion.TrySetResult(new(operation.Request.OperationId, outcome, reason));
        operation.ReleaseCancellation();
    }
    private bool MayPlay(Operation operation)
    {
        lock (_gate) return ReferenceEquals(_active, operation) && operation.Generation == _generation
            && !_closed && !_unavailable && operation.CancelReason is null;
    }
    private void Publish(Operation operation, ReadAloudProgress progress)
    {
        if (progress.ReasonCode == "CleanupBlocked")
        {
            Operation? pending;
            lock (_gate)
            {
                if (!ReferenceEquals(_active, operation)) return;
                _unavailable = true; ++_generation; pending = _pending; _pending = null;
            }
            FinishPending(pending, ReadAloudOutcome.Unavailable, "CleanupBlocked");
            operation.RequestCancel(); // Starts bounded observation while the backend retains its cleanup driver.
            try { operation.Progress?.Invoke(progress); } catch (Exception) { }
            return;
        }
        if (!MayPlay(operation)) return;
        try { operation.Progress?.Invoke(progress); }
        catch (Exception) { /* A disconnected UI observer cannot compromise native resource ownership. */ }
    }
    private void Log(string message)
    {
        try { _log?.Invoke(message); } catch (Exception) { /* Diagnostics are optional. */ }
    }
    private async Task DriveAsync()
    {
        while (true)
        {
            Operation operation;
            lock (_gate) operation = _active!;
            ReadAloudResult result;
            Task<ReadAloudResult> native;
            try { native = _backend.RunAsync(operation.Request, operation.Cancel.Token, () => MayPlay(operation), p => Publish(operation, p)); }
            catch (Exception) { native = Task.FromResult(new ReadAloudResult(operation.Request.OperationId, ReadAloudOutcome.Unavailable, "BackendStartFailed", ReadAloudCleanup.Deferred)); }
            operation.NativeSettlement = native;
            bool deferred = false;
            try
            {
                var first = await Task.WhenAny(native, operation.CancelRequested.Task).ConfigureAwait(false);
                if (first != native)
                {
                    using var deadlineCancel = new CancellationTokenSource();
                    Task deadline = _deadline(deadlineCancel.Token);
                    if (await Task.WhenAny(native, deadline).ConfigureAwait(false) != native)
                    {
                        deferred = true;
                        Operation? pending;
                        lock (_gate) { _unavailable = true; ++_generation; pending = _pending; _pending = null; }
                        FinishPending(pending, ReadAloudOutcome.Unavailable, "CleanupDeferred");
                        operation.Completion.TrySetResult(new(operation.Request.OperationId, ReadAloudOutcome.Unavailable,
                            "CleanupDeferred", ReadAloudCleanup.Deferred));
                        Log($"ReadAloud operation={operation.Request.OperationId:N}; phase=Terminal; outcome=Unavailable; cleanup=Deferred");
                    }
                    await deadlineCancel.CancelAsync().ConfigureAwait(false);
                }
                result = await native.ConfigureAwait(false); // Never abandon late stream/player cleanup.
            }
            catch (Exception ex)
            {
                result = new(operation.Request.OperationId, ReadAloudOutcome.Unavailable, "BackendSettlementFailed", ReadAloudCleanup.Deferred);
                Log($"ReadAloud operation={operation.Request.OperationId:N}; exceptionType={ex.GetType().Name}; hresult={ex.HResult}");
            }
            Operation? rejected = null;
            bool next;
            lock (_gate)
            {
                if (result.Outcome == ReadAloudOutcome.Unavailable || result.Cleanup == ReadAloudCleanup.Deferred) _unavailable = true;
                if (result.Cleanup == ReadAloudCleanup.Released && result.Outcome is ReadAloudOutcome.Completed or ReadAloudOutcome.Canceled
                    && operation.CancelReason is { } canceled)
                    result = result with { Outcome = canceled, ReasonCode = canceled.ToString() };
                if (_closed || _unavailable) { rejected = _pending; _pending = null; }
                _active = result.Cleanup == ReadAloudCleanup.Deferred ? operation : _pending;
                _pending = null;
                next = result.Cleanup == ReadAloudCleanup.Released && _active is not null;
                operation.Completion.TrySetResult(result);
            }
            operation.ReleaseCancellation();
            FinishPending(rejected, ReadAloudOutcome.Unavailable, "AdmissionClosed");
            if (!deferred) Log($"ReadAloud operation={operation.Request.OperationId:N}; phase=Terminal; outcome={result.Outcome}; cleanup={result.Cleanup}");
            else Log($"ReadAloud operation={operation.Request.OperationId:N}; phase=LateCleanup; cleanup={result.Cleanup}");
            if (!next) return;
        }
    }
}

/// <summary>App ownership only; factory and backend are independent of Host and transient pages.</summary>
public sealed class StudioReadAloudSession(Func<IFlashcardReadAloudService> factory)
{
    private readonly object _gate = new();
    private readonly Lazy<IFlashcardReadAloudService> _feature = new(factory);
    private bool _closed;
    private Task<ReadAloudResult>? _stop;
    private Task? _shutdown, _shutdownDriver;
    public bool IsCreated => _feature.IsValueCreated;
    public bool IsActive => _feature.IsValueCreated && _feature.Value.IsActive;
    public Task<ReadAloudResult> ReadAsync(ReadAloudRequest request, Action<ReadAloudProgress>? progress = null)
    {
        if (request.OperationId == Guid.Empty || !ReadAloudText.IsValid(request.Text))
            return Task.FromResult(new ReadAloudResult(request.OperationId, ReadAloudOutcome.InvalidText, "InvalidText"));
        lock (_gate) if (_closed) return Closed(request.OperationId);
        IFlashcardReadAloudService service;
        try { service = _feature.Value; }
        catch (Exception) { return Task.FromResult(new ReadAloudResult(request.OperationId, ReadAloudOutcome.Unavailable, "FactoryUnavailable")); }
        lock (_gate) if (_closed) return Closed(request.OperationId);
        return service.ReadAsync(request, progress);
    }
    private static Task<ReadAloudResult> Closed(Guid id) => Task.FromResult(new ReadAloudResult(id, ReadAloudOutcome.Unavailable, "AdmissionClosed"));
    public Task<ReadAloudResult> CancelCurrentAsync() => _feature.IsValueCreated ? _feature.Value.CancelCurrentAsync()
        : Task.FromResult(new ReadAloudResult(Guid.Empty, ReadAloudOutcome.Canceled, "Idle"));
    public Task<ReadAloudResult> StopAsync()
    {
        TaskCompletionSource<ReadAloudResult> completion;
        lock (_gate)
        {
            if (_stop is not null) return _stop;
            _closed = true;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _stop = completion.Task;
        }
        Task<ReadAloudResult> settlement = _feature.IsValueCreated ? _feature.Value.StopAsync()
            : Task.FromResult(new ReadAloudResult(Guid.Empty, ReadAloudOutcome.Canceled, "Unused"));
        _stopDriver = ObserveStopAsync(settlement, completion);
        return completion.Task;
    }
    private Task? _stopDriver;
    private static async Task ObserveStopAsync(Task<ReadAloudResult> settlement, TaskCompletionSource<ReadAloudResult> completion)
    {
        try { completion.TrySetResult(await settlement.ConfigureAwait(false)); }
        catch (Exception) { completion.TrySetResult(new(Guid.Empty, ReadAloudOutcome.Unavailable, "StopFailed", ReadAloudCleanup.Deferred)); }
    }
    public Task ShutdownAsync(StudioExportSession exports, Func<Task> shutdownHost)
    {
        TaskCompletionSource completion;
        lock (_gate)
        {
            if (_shutdown is not null) return _shutdown;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _shutdown = completion.Task;
        }
        Task audio = StopAsync();
        Task export = exports.ShutdownAsync(shutdownHost); // File settlement and H0 start without awaiting optional audio.
        _shutdownDriver = ObserveShutdownAsync(audio, export, completion);
        return completion.Task;
    }
    private static async Task ObserveShutdownAsync(Task audio, Task export, TaskCompletionSource completion)
    {
        try { await Task.WhenAll(audio, export).ConfigureAwait(false); completion.TrySetResult(); }
        catch (Exception ex) { completion.TrySetException(ex); }
    }
}
