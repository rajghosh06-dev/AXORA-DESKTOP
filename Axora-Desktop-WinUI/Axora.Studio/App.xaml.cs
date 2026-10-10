using Axora.Studio.Services;
using Axora.Studio.Services.Contracts;
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
    private readonly StudioExportSession _exports;
    private readonly StudioReadAloudSession _readAloud;
    private readonly Lazy<ResumeViewModel> _resume;
    private MainWindow? _window;
    private Task? _launch;
    private StudioSettingsService? _settings;
    public App(StudioPathService paths, StudioDiagnostics log, Action<IServiceCollection>? configureForTest = null)
    {
        _log = log;
        InitializeComponent();
        UnhandledException += (_, args) => _log.Write($"Unhandled XAML failure: {args.Exception.GetType().Name}");
        _host = StudioBootstrap.BuildHost(paths, services =>
        {
            services.AddSingleton<IStudioSavePicker>(_ => new StudioSavePicker(_log.Write));
            services.AddSingleton<Func<IFlashcardReadAloudService>>(_ => () =>
                new FlashcardReadAloudService(new WindowsFlashcardReadAloudBackend(_log.Write), log: _log.Write));
            services.AddSingleton(sp => new ResumeSession(sp.GetRequiredService<ResumeStore>(), sp.GetRequiredService<ResumeCodec>(), sp.GetRequiredService<IResumeFilePublisher>(), _log.Write));
            configureForTest?.Invoke(services);
        });
        var speechFactory = _host.Services.GetRequiredService<Func<IFlashcardReadAloudService>>();
        var resumeFactory = _host.Services.GetRequiredService<Func<ResumeViewModel>>();
        _resume = new(() => { var feature = resumeFactory(); _log.Write("Resume session created; count=1"); return feature; });
        _readAloud = new(() =>
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var service = speechFactory();
            _log.Write($"ReadAloud service created; count=1; elapsedMs={timer.Elapsed.TotalMilliseconds:0.00}");
            return service;
        });
        var exportFactory = _host.Services.GetRequiredService<Func<FlashcardExportCoordinator>>();
        _exports = new(() =>
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var coordinator = exportFactory();
            _log.Write($"Export coordinator created; count=1; elapsedMs={timer.Elapsed.TotalMilliseconds:0.00}");
            return coordinator;
        });
        _lifecycle = new StudioLifecycle(_host, () => _settings?.StopAsync() ?? Task.CompletedTask, _log.Write);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        _launch = LaunchAsync();
    }
    public bool HasActiveIntegrityCriticalFilePublication => HasActiveFileWork(_exports, _resume);
    public static bool HasActiveFileWork(StudioExportSession exports, Lazy<ResumeViewModel> resume) =>
        exports.IsActive || (resume.IsValueCreated && resume.Value.Session.IsActive);
    public static Task RetainFileWorkAsync(StudioReadAloudSession audio, StudioExportSession exports, Lazy<ResumeViewModel> resume, Func<Task> shutdownHost)
    {
        Task resumeSettlement = resume.IsValueCreated ? resume.Value.Session.StopAsync() : Task.CompletedTask;
        return audio.ShutdownAsync(exports, async () => { await resumeSettlement.ConfigureAwait(false); await shutdownHost().ConfigureAwait(false); });
    }
    public Task ShutdownAsync() => RetainFileWorkAsync(_readAloud, _exports, _resume, _lifecycle.ShutdownAsync);
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
                    _host.Services.GetRequiredService<Func<FlashcardsViewModel>>(), _exports, _readAloud, _resume);
                bool closePending = false;
                bool shutdownFinished = false;
                _window.AppWindow.Closing += async (_, closing) =>
                {
                    if (shutdownFinished) return;
                    closing.Cancel = true;
                    if (closePending) return;
                    closePending = true;
                    _log.Write("Window close requested");
                    _log.Write($"Window close context; thread={Environment.CurrentManagedThreadId}; apartment={Thread.CurrentThread.GetApartmentState()}");
                    _window.ShowFileClosePending();
                    Task<ReadAloudResult> pendingAudioCancellation = _readAloud.CancelCurrentAsync();
                    try
                    {
                        if (!await _window.PrepareCloseAsync()) { closePending = false; _window.ClearClosePendingStatus(); _log.Write("Window close cancelled by Resume guard"); return; }
                    }
                    catch (Exception ex) { closePending = false; _window.ClearClosePendingStatus(); _log.Write($"Resume close guard failed: {ex.GetType().Name}"); return; }
                    _log.Write("Export close status updated; requesting session shutdown");
                    try
                    {
                        var settlement = ShutdownAsync();
                        _log.Write("Export shutdown task retained");
                        await settlement;
                    }
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
