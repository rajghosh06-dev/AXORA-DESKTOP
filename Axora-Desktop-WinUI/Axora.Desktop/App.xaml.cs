using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.XamlTypeInfo;
using Axora.Desktop.Services;
using Axora.Desktop.Services.Contracts;
using Axora.Desktop.ViewModels;

namespace Axora.Desktop;

/// <summary>
/// Axora Desktop application entry point.
/// Manages Microsoft.Extensions.Hosting DI, XAML metadata provider, and background P2P synchronization.
/// </summary>
public sealed partial class App : Application
{
    public static IHost AppHost { get; private set; } = null!;
    public static MainWindow? MainAppWindow { get; private set; }
    public static IntPtr MainWindowHandle { get; private set; } = IntPtr.Zero;

    public App()
    {
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
        void Log(string msg) => File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");

        Log("App constructor started.");

        UnhandledException += (s, e) =>
        {
            Log($"[App] UnhandledException: {e.Message} - {e.Exception}");
            e.Handled = true;
        };

        try
        {
            Log("Calling App.InitializeComponent()...");
            InitializeComponent();
            Log("App.InitializeComponent() completed.");
        }
        catch (Exception ex)
        {
            Log($"[App] InitializeComponent failed: {ex}");
            if (ex.InnerException != null)
            {
                Log($"[App] Inner exception: {ex.InnerException}");
            }
        }

        Log("Building AppHost...");
        AppHost = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .ConfigureServices((_, services) =>
            {
                // ── Core Infrastructure Services (Singletons) ──────────────────────
                services.AddSingleton<IAppSettingsService, AppSettingsService>();
                services.AddSingleton<IDownloadManagerService, DownloadManagerService>();
                services.AddSingleton<IDocumentProcessorService, DocumentProcessorService>();
                services.AddSingleton<IBatchImageProcessorService, BatchImageProcessorService>();
                services.AddSingleton<IIntelligentCompressorService, IntelligentCompressorService>();
                services.AddSingleton<ISpeechSynthesisService, SpeechSynthesisService>();
                services.AddSingleton<IVoiceTranscriberService, VoiceTranscriberService>();
                services.AddSingleton<IDocumentChatService, DocumentChatService>();
                services.AddSingleton<ITrayService, TrayService>();
                services.AddSingleton<IP2pSyncService, P2pSyncService>();
                services.AddSingleton<ISecurityVaultService, StreamingVaultService>();
                services.AddSingleton<ITpmSecurityProfileService, TpmSecurityProfileService>();
                services.AddSingleton<IPdfAnnotationService, PdfAnnotationService>();
                services.AddSingleton<IWindowsAiService, DirectMlEmbeddingService>();
                services.AddSingleton<IOcrService, WinRtOcrService>();
                services.AddSingleton<IPdfExtractionService, PdfExtractionService>();
                services.AddSingleton<IScannerService, WiaScannerService>();
                services.AddSingleton<IResumePdfCompilerService, ResumePdfCompilerService>();
                services.AddSingleton<IAtsOptimizerService, AtsOptimizerService>();

                // ── ViewModels (Singletons for state retention across navigation) ─────
                services.AddSingleton<ShellViewModel>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<ScholarKitViewModel>();
                services.AddSingleton<ResumeStudioViewModel>();
                services.AddSingleton<BatchImageViewModel>();
                services.AddSingleton<CompressorViewModel>();
                services.AddSingleton<VaultViewModel>();
                services.AddSingleton<FlashcardsViewModel>();
                services.AddSingleton<MobileLinkViewModel>();
                services.AddSingleton<SettingsViewModel>();
            })
            .Build();
        Log("AppHost built.");
    }

    public static T GetService<T>() where T : class
    {
        return AppHost.Services.GetRequiredService<T>();
    }

    public static T? TryGetService<T>() where T : class
    {
        return AppHost.Services.GetService<T>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
        void Log(string msg) => File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");

        Log("App.OnLaunched invoked.");
        try
        {
            MainAppWindow = new MainWindow();
            Log("MainWindow instantiated.");

            MainAppWindow.Activate();
            Log("MainWindow activated.");

            _ = AppHost.StartAsync();
            Log("AppHost.StartAsync dispatched.");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainAppWindow);
            MainWindowHandle = hwnd;
            var tray = GetService<ITrayService>();
            tray.Initialize(hwnd);
            Log("System Tray service initialized.");

            var settings = GetService<IAppSettingsService>();
            if (settings.AutoStartP2pEngine)
            {
                var p2p = GetService<IP2pSyncService>();
                _ = p2p.StartAsync();
                Log("P2P background sync service auto-started on launch.");
            }
        }
        catch (Exception ex)
        {
            Log($"OnLaunched exception: {ex}");
            throw;
        }
    }
}
