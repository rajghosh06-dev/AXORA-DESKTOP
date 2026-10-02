using Axora.Studio.Services;
using Axora.Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

namespace Axora.Studio;

public sealed partial class App : Application
{
    private readonly IHost _host;
    private readonly StudioLifecycle _lifecycle;
    private readonly StudioDiagnostics _log;
    private MainWindow? _window;
    private Task? _launch;
    private StudioSettingsService? _settings;
    public App(StudioPathService paths, StudioDiagnostics log)
    {
        _log = log;
        InitializeComponent();
        UnhandledException += (_, args) => _log.Write($"Unhandled XAML failure: {args.Exception.GetType().Name}");
        _host = StudioBootstrap.BuildHost(paths);
        _lifecycle = new StudioLifecycle(_host, () => _settings?.StopAsync() ?? Task.CompletedTask, _log.Write);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        _launch = LaunchAsync();
    }
    public Task ShutdownAsync() => _lifecycle.ShutdownAsync();
    private async Task LaunchAsync()
    {
        try
        {
            await _lifecycle.StartAsync(async token =>
            {
                var paths = _host.Services.GetRequiredService<StudioPathService>();
                Directory.CreateDirectory(paths.Root);
                _log.Write("Studio root ready");
                _host.Services.GetRequiredService<StudioWriterLease>().Acquire();
                _log.Write("Writer lease acquired");
                _settings = _host.Services.GetRequiredService<StudioSettingsService>();
                var loaded = await _settings.LoadAsync(token);
                _log.Write($"Settings loaded; theme={loaded.Settings.Theme}; writable={loaded.CanSave}");
                _window = new MainWindow(_host.Services.GetRequiredService<ShellViewModel>(),
                    _host.Services.GetRequiredService<SettingsViewModel>(), _log,
                    _host.Services.GetRequiredService<Func<FlashcardsViewModel>>());
                bool closePending = false;
                bool shutdownFinished = false;
                _window.AppWindow.Closing += async (_, closing) =>
                {
                    if (shutdownFinished) return;
                    closing.Cancel = true;
                    if (closePending) return;
                    closePending = true;
                    _log.Write("Window close requested");
                    try { await ShutdownAsync(); }
                    catch (Exception ex) { Environment.ExitCode = 1; _log.Write($"Window shutdown failed: {ex.GetType().Name}"); }
                    finally
                    {
                        shutdownFinished = true;
                        try { _window.Close(); }
                        catch (Exception ex) { Environment.ExitCode = 1; _log.Write($"Window close failed: {ex.GetType().Name}"); Exit(); }
                    }
                };
                _window.Closed += (_, _) => _log.Write("Window closed");
                _window.ApplyTheme(loaded.Settings.Theme);
                if (WinRT.Interop.WindowNative.GetWindowHandle(_window) == IntPtr.Zero)
                    throw new InvalidOperationException("Studio main window has no native handle.");
                token.ThrowIfCancellationRequested();
            }, () => _window!.Activate());
        }
        catch (Exception ex)
        {
            Environment.ExitCode = 1;
            _log.Write($"Mandatory startup failed: {ex.GetType().Name}; no healthy window activation");
            try { await ShutdownAsync(); }
            catch (Exception cleanup) { _log.Write($"Startup cleanup failed: {cleanup.GetType().Name}"); }
            Exit();
        }
    }
}
