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
            dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () => action());
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

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () =>
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
        dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
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
        return tcs.Task;
    }
}
