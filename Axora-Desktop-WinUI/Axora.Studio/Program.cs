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
            System.Diagnostics.Debug.WriteLine($"Fatal entry failure: {ex.GetType().Name}; HRESULT={ex.HResult}");
        }
        finally
        {
            // Application.Start has returned; a DispatcherQueue context must not capture fallback continuations.
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                if (app is not null)
                {
                    Task settlement = app.ShutdownAsync();
                    if (app.HasActiveIntegrityCriticalFilePublication)
                    {
                        log?.Write("Program fallback retains integrity-critical file settlement");
                        settlement.GetAwaiter().GetResult(); // No observation timeout may abandon file-integrity work.
                    }
                    else settlement.WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
                }
                log?.Write("Program fallback settled");
            }
            catch (Exception ex) { Environment.ExitCode = 1; log?.Write($"Program fallback failed: {ex.GetType().Name}"); }
        }
    }
}
