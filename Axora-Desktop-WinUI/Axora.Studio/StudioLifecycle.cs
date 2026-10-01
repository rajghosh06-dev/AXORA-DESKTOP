using Microsoft.Extensions.Hosting;

namespace Axora.Studio;

/// <summary>Operational coordinator only. DI owns all container-created disposables.</summary>
public sealed class StudioLifecycle
{
    public static readonly TimeSpan ShutdownBudget = TimeSpan.FromSeconds(10);
    private readonly IHost _host;
    private readonly Func<Task> _stopOwnedWork;
    private readonly Action<string> _log;
    private readonly TimeSpan _budget;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _startupCancellation = new();
    private readonly TaskCompletionSource _startupSettled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _shutdown;
    private bool _started;
    private bool _hostStartAttempted;

    public StudioLifecycle(IHost host, Func<Task> stopOwnedWork, Action<string>? log = null, TimeSpan? budget = null)
    {
        (_host, _stopOwnedWork, _log, _budget) = (host, stopOwnedWork, log ?? (_ => { }), budget ?? ShutdownBudget);
        if (_budget <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(budget));
    }

    public async Task StartAsync(Func<CancellationToken, Task> initialize, Action activate)
    {
        lock (_gate)
        {
            if (_started || _shutdown is not null) throw new InvalidOperationException("Startup is not admissible.");
            _started = _hostStartAttempted = true;
        }
        try
        {
            await _host.StartAsync(_startupCancellation.Token);
            Log("Host started");
            await initialize(_startupCancellation.Token);
            lock (_gate)
            {
                if (_shutdown is not null) throw new OperationCanceledException("Close requested during startup.");
                _startupCancellation.Token.ThrowIfCancellationRequested();
                activate();
                Log("Window activated");
            }
        }
        finally { _startupSettled.TrySetResult(); }
    }

    public Task ShutdownAsync()
    {
        TaskCompletionSource completion;
        lock (_gate)
        {
            if (_shutdown is not null) return _shutdown;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _shutdown = completion.Task; // Publish before cancellation/teardown can reenter.
        }
        _ = ShutdownCoreAsync(completion);
        return completion.Task;
    }

    private async Task ShutdownCoreAsync(TaskCompletionSource completion)
    {
        await Task.Yield(); // Publish owned task before running teardown on its caller's context.
        using var deadline = new CancellationTokenSource(_budget);
        try
        {
            Log("Shutdown requested; admission closed");
            _startupCancellation.Cancel();
            if (_started) await _startupSettled.Task.WaitAsync(deadline.Token);
            await _stopOwnedWork().WaitAsync(deadline.Token);
            Log("Owned work stopped");
            Exception? stopError = null;
            if (_hostStartAttempted)
            {
                try { await _host.StopAsync(deadline.Token).WaitAsync(deadline.Token); }
                catch (OperationCanceledException) when (deadline.IsCancellationRequested) { throw; }
                catch (Exception ex) { stopError = ex; }
            }
            Log("Host stop settled");
            // A settled stop failure must not skip disposal. A timed-out, still-live
            // operation instead takes the failure path above and retains its host.
            if (_host is IAsyncDisposable asyncHost) await asyncHost.DisposeAsync().AsTask().WaitAsync(deadline.Token);
            else _host.Dispose();
            _startupCancellation.Dispose();
            Log("Host disposed");
            if (stopError is not null) throw new InvalidOperationException("Host stop failed.", stopError);
            Log("Shutdown completed");
            completion.TrySetResult();
        }
        catch (Exception ex)
        {
            Log($"Shutdown failed: {ex.GetType().Name}; clean completion not established");
            completion.TrySetException(ex);
        }
    }

    private void Log(string message)
    {
        try { _log(message); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lifecycle diagnostics failed: {ex.GetType().Name}"); }
    }
}
