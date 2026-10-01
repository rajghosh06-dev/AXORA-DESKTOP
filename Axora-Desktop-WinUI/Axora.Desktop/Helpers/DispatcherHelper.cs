using Microsoft.UI.Dispatching;

namespace Axora.Desktop.Helpers;

/// <summary>
/// Convenience extensions for marshalling work to the WinUI 3 XAML UI thread via DispatcherQueue.
/// </summary>
public static class DispatcherHelper
{
    /// <summary>
    /// Enqueues an action on the given DispatcherQueue at Normal priority.
    /// Safe to call from any thread.
    /// </summary>
    public static void RunOnUiThread(this DispatcherQueue dispatcher, Action action)
    {
        if (dispatcher.HasThreadAccess)
        {
            action();
        }
        else
        {
            if (!dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () => action()))
                throw new InvalidOperationException("The WinUI dispatcher rejected the requested action.");
        }
    }

    /// <summary>
    /// Enqueues an action on the given DispatcherQueue and returns a Task that completes
    /// when the action has been executed on the UI thread.
    /// </summary>
    public static Task RunOnUiThreadAsync(this DispatcherQueue dispatcher, Action action)
    {
        if (dispatcher.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        return RunWithEnqueueAsync(
            callback => dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () => callback()),
            action);
    }

    /// <summary>
    /// Testable enqueue seam used by the DispatcherQueue extension. A rejected enqueue
    /// is represented by a faulted task rather than an operation that never completes.
    /// </summary>
    public static Task RunWithEnqueueAsync(Func<Action, bool> tryEnqueue, Action action)
    {
        ArgumentNullException.ThrowIfNull(tryEnqueue);
        ArgumentNullException.ThrowIfNull(action);
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool enqueued = tryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        if (!enqueued)
            tcs.TrySetException(new InvalidOperationException("The WinUI dispatcher rejected the requested action."));
        return tcs.Task;
    }

    /// <summary>
    /// Enqueues an async delegate on the UI thread and returns a Task that completes
    /// when the delegate's task completes.
    /// </summary>
    public static Task RunOnUiThreadAsync(this DispatcherQueue dispatcher, Func<Task> asyncAction)
    {
        if (dispatcher.HasThreadAccess)
            return asyncAction();

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool enqueued = dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
        {
            try
            {
                await asyncAction();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        if (!enqueued)
            tcs.TrySetException(new InvalidOperationException("The WinUI dispatcher rejected the requested asynchronous action."));
        return tcs.Task;
    }
}
