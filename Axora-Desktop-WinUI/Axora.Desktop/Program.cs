using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Axora.Desktop;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
        void Log(string msg) => System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");

        Log("Program.Main started.");

        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Log("ComWrappersSupport initialized.");

            Application.Start((p) =>
            {
                try
                {
                    Log("Application.Start callback invoked.");
                    var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                    System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                    _ = new App();
                    Log("App instance created successfully.");
                }
                catch (Exception appEx)
                {
                    Log($"Exception creating App: {appEx}");
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            Log($"Application.Start exception: {ex}");
            throw;
        }
    }
}
