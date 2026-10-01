using Axora.Studio.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Axora.Studio;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        App? app = null;
        StudioDiagnostics? log = null;
        try
        {
            var paths = new StudioPathService();
            log = new StudioDiagnostics(paths);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => log.Write($"Unhandled failure; terminating={e.IsTerminating}");
            TaskScheduler.UnobservedTaskException += (_, e) => log.Write($"Unobserved task failure: {e.Exception.GetType().Name}");
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(_ =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                app = new App(paths, log);
            });
        }
        catch (Exception ex)
        {
            Environment.ExitCode = 1;
            log?.Write($"Fatal entry failure: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            try
            {
                if (app is not null) app.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
                log?.Write("Program fallback settled");
            }
            catch (Exception ex) { Environment.ExitCode = 1; log?.Write($"Program fallback failed: {ex.GetType().Name}"); }
        }
    }
}
