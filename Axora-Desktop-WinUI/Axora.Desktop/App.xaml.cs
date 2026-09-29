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
    private static AppLifecycle? _lifecycle;

    public App()
    {
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
        void Log(string msg) => File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");

        Log("App constructor started.");

        UnhandledException += (s, e) =>
        {
            Log($"[App.UnhandledException] Message: {e.Message} | HResult: 0x{e.Exception.HResult:X8}\n{e.Exception}");
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
            throw;
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
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<IDownloadManagerService, DownloadManagerService>();
                services.AddSingleton<IDocumentProcessorService, DocumentProcessorService>();
                services.AddSingleton<IBatchImageProcessorService, BatchImageProcessorService>();
                services.AddSingleton<IIntelligentCompressorService, IntelligentCompressorService>();
                services.AddSingleton<ISpeechSynthesisService, SpeechSynthesisService>();
                services.AddSingleton<IVoiceTranscriberService, VoiceTranscriberService>();
                services.AddSingleton<IVoiceTextFormatter, VoiceTextFormatter>();
                services.AddSingleton<IVoiceCommandRouter, VoiceCommandRouter>();
                services.AddSingleton<IAudioDeviceMonitor, AudioDeviceMonitor>();
                services.AddSingleton<IVoiceCoordinator>(sp =>
                    _lifecycle!.CreateVoice(() => ActivatorUtilities.CreateInstance<VoiceCoordinator>(sp)));
                services.AddSingleton<IDocumentChatService, DocumentChatService>();
                services.AddSingleton<ITrayService, TrayService>();
                services.AddSingleton<IP2pSyncService>(sp =>
                    _lifecycle!.CreateP2p(() => ActivatorUtilities.CreateInstance<P2pSyncService>(sp)));
                services.AddSingleton<ISecurityVaultService, StreamingVaultService>();
                services.AddSingleton<ITpmSecurityProfileService, TpmSecurityProfileService>();
                services.AddSingleton<IPdfAnnotationService, PdfAnnotationService>();
                services.AddSingleton<IWindowsAiService, DirectMlEmbeddingService>();
                services.AddSingleton<IOcrCapabilityStateProvider, WindowsOcrCapabilityStateProvider>();
                services.AddSingleton<IOcrEngine, WindowsMediaOcrEngine>();
                services.AddSingleton<IOcrService, WinRtOcrService>();
                services.AddSingleton<IPdfExtractionService, PdfExtractionService>();
                services.AddSingleton<IScannerService, WiaScannerService>();
                services.AddSingleton<IResumePdfCompilerService, ResumePdfCompilerService>();
                services.AddSingleton<IAtsOptimizerService, AtsOptimizerService>();
                services.AddSingleton<IScholarPersistenceMigrator, ScholarPersistenceMigrator>();
                services.AddSingleton<IScholarLibraryService, ScholarLibraryService>();

                // ── Scholar Document Extraction Services (Phase W3-C.2) ───────────
                services.AddSingleton<IDocumentPageBuilder, DocumentPageBuilder>();
                services.AddSingleton<IDocumentFormatDetector, DocumentFormatDetector>();
                services.AddSingleton<PlainTextExtractorEngine>();
                services.AddSingleton<IDocumentExtractorEngine>(sp => sp.GetRequiredService<PlainTextExtractorEngine>());
                services.AddSingleton<DelimitedTextExtractorEngine>();
                services.AddSingleton<IDocumentExtractorEngine>(sp => sp.GetRequiredService<DelimitedTextExtractorEngine>());
                services.AddSingleton<MarkdownExtractorEngine>();
                services.AddSingleton<IDocumentExtractorEngine>(sp => sp.GetRequiredService<MarkdownExtractorEngine>());
                services.AddSingleton<LocalHtmlExtractorEngine>();
                services.AddSingleton<IDocumentExtractorEngine>(sp => sp.GetRequiredService<LocalHtmlExtractorEngine>());

                // ── Scholar PDF Extraction & Page Rasterizer Services (Phase W3-C.3/C.5.4) ──
                services.AddSingleton<IPdfPageRasterizer, WindowsPdfPageRasterizer>();
                services.AddSingleton<PdfDocumentExtractorEngine>();
                services.AddSingleton<IPdfDocumentExtractorEngine>(sp => sp.GetRequiredService<PdfDocumentExtractorEngine>());
                services.AddSingleton<IDocumentExtractorEngine>(sp => sp.GetRequiredService<PdfDocumentExtractorEngine>());

                // ── Scholar Word DOCX Extraction Services (Phase W3-C.4) ───────────
                services.AddSingleton<DocxDocumentExtractorEngine>();
                services.AddSingleton<IDocumentExtractorEngine>(sp => sp.GetRequiredService<DocxDocumentExtractorEngine>());

                // ── Scholar Raster Image Extraction Services (Phase W3-C.5.3) ───────
                services.AddSingleton<RasterImageDocumentExtractorEngine>();
                services.AddSingleton<IDocumentExtractorEngine>(sp => sp.GetRequiredService<RasterImageDocumentExtractorEngine>());

                // ── Scholar Multi-Frame TIFF Extraction Services (Phase W3-C.5.5) ────
                services.AddSingleton<TiffDocumentExtractorEngine>();
                services.AddSingleton<IDocumentExtractorEngine>(sp => sp.GetRequiredService<TiffDocumentExtractorEngine>());

                // ── Scholar Document Normalization Foundation & Extraction Orchestrator (Phase W3-C.6.1/C.6.4) ────────
                services.AddSingleton<TextNormalizer>();
                services.AddSingleton<ITextNormalizer>(sp => sp.GetRequiredService<TextNormalizer>());
                services.AddSingleton<IPassageChunker, PassageChunker>();
                services.AddSingleton<IBoundedContextWindowBuilder, BoundedContextWindowBuilder>();
                services.AddSingleton<IScholarExtractionOrchestrator>(sp =>
                    new ScholarExtractionOrchestrator(
                        sp.GetRequiredService<IDocumentFormatDetector>(),
                        sp.GetServices<IDocumentExtractorEngine>(),
                        sp.GetRequiredService<ITextNormalizer>(),
                        sp.GetRequiredService<IDocumentPageBuilder>(),
                        sp.GetService<IPassageChunker>(),
                        sp.GetService<IScholarLibraryService>(),
                        sp.GetService<ILogger<ScholarExtractionOrchestrator>>()));
                services.AddSingleton<ScholarExtractionOrchestrator>(sp =>
                    (ScholarExtractionOrchestrator)sp.GetRequiredService<IScholarExtractionOrchestrator>());

                // ── Scholar Vector Embedding & Hybrid Indexing Services (Phase W3-D) ──
                services.AddSingleton<DirectMlEmbeddingEngine>();
                services.AddSingleton<IEmbeddingEngine>(sp => sp.GetRequiredService<DirectMlEmbeddingEngine>());
                services.AddSingleton<IEmbeddingCapabilityStateProvider>(sp => sp.GetRequiredService<DirectMlEmbeddingEngine>());
                services.AddSingleton<ScholarVectorIndexWriter>();
                services.AddSingleton<ScholarVectorIndexReader>();
                services.AddSingleton<IScholarIndexService, ScholarIndexService>();

                // ── Scholar Search & Retrieval Services (Phase W3-E) ───────────────
                services.AddSingleton<IScholarSearchService, ScholarSearchService>();

                // ── Scholar Study Synthesis Services (Phase W3-F) ─────────────────
                services.AddSingleton<IScholarSlmModelDriver, NullScholarSlmModelDriver>();
                services.AddSingleton<IScholarSynthesisEngine, ScholarSynthesisEngine>();

                // ── Extension & Dependency Manager Services (Phase W1.5) ──────────
                services.AddSingleton<IExtensionCacheService, ExtensionCacheService>();
                services.AddSingleton<IExtensionRegistry, ExtensionRegistry>();
                services.AddSingleton<IVersionDetector, VersionDetector>();
                services.AddSingleton<IExtensionValidator, ExtensionValidator>();
                services.AddSingleton<IExtensionDownloader, ExtensionDownloader>();
                services.AddSingleton<IExtensionInstaller, ExtensionInstaller>();
                services.AddSingleton<IExtensionRepairService, ExtensionRepairService>();
                services.AddSingleton<IDependencyManager, DependencyManager>();

                // ── Universal Converter Engines & Orchestrator (Phase W2-B/W2-C/W2-D) ──────────
                services.AddSingleton<WicImageConversionEngine>();
                services.AddSingleton<IConversionEngine>(sp => sp.GetRequiredService<WicImageConversionEngine>());
                services.AddSingleton<PdfDocumentConversionEngine>();
                services.AddSingleton<IConversionEngine>(sp => sp.GetRequiredService<PdfDocumentConversionEngine>());
                services.AddSingleton<TextMarkdownConversionEngine>();
                services.AddSingleton<IConversionEngine>(sp => sp.GetRequiredService<TextMarkdownConversionEngine>());
                services.AddSingleton<WindowsPdfRendererConversionEngine>();
                services.AddSingleton<IConversionEngine>(sp => sp.GetRequiredService<WindowsPdfRendererConversionEngine>());
                services.AddSingleton<IConversionOrchestrator, ConversionOrchestrator>(sp =>
                    new ConversionOrchestrator(
                        sp.GetServices<IConversionEngine>(),
                        logger: sp.GetService<ILogger<ConversionOrchestrator>>()));

                // ── ViewModels (Singletons for state retention across navigation) ─────
                services.AddSingleton<ShellViewModel>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<ScholarKitViewModel>();
                services.AddSingleton<ResumeStudioViewModel>();
                services.AddSingleton<BatchImageViewModel>();
                services.AddSingleton<CompressorViewModel>();
                services.AddSingleton<UniversalConverterViewModel>();
                services.AddSingleton<VaultViewModel>();
                services.AddSingleton<FlashcardsViewModel>();
                services.AddSingleton<MobileLinkViewModel>();
                services.AddSingleton<DownloadManagerViewModel>();
                services.AddSingleton<SettingsViewModel>();
            })
            .Build();
        _lifecycle = new AppLifecycle(AppHost, Log);
        Log("AppHost built.");
    }

    public static T GetService<T>() where T : class
    {
        return AppHost.Services.GetRequiredService<T>();
    }

    public static T? TryGetService<T>() where T : class
    {
        if (AppHost == null) return null;
        return AppHost.Services.GetService<T>();
    }

    public static System.Threading.Tasks.Task ShutdownAsync() =>
        _lifecycle?.ShutdownAsync() ?? System.Threading.Tasks.Task.CompletedTask;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
        void Log(string msg) => File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");

        Log("App.OnLaunched invoked.");
        _ = LaunchAsync();
    }

    private async System.Threading.Tasks.Task LaunchAsync()
    {
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
        void Log(string msg) => File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
        void LogSafely(string msg)
        {
            try { Log(msg); }
            catch (Exception loggingError)
            {
                Environment.ExitCode = 1;
                System.Diagnostics.Debug.WriteLine($"Lifecycle log failure: {loggingError}; original message: {msg}");
            }
        }

        try
        {
            await _lifecycle!.StartAsync(async () =>
            {
                Log("AppHost started.");
                MainAppWindow = new MainWindow();
                Log("MainWindow instantiated.");
                bool closePending = false;
                bool shutdownFinished = false;
                MainAppWindow.AppWindow.Closing += async (_, closingArgs) =>
                {
                    if (shutdownFinished) return;
                    closingArgs.Cancel = true;
                    if (closePending) return;
                    closePending = true;
                    LogSafely("MainWindow closing requested; waiting for graceful shutdown.");
                    try { await ShutdownAsync(); LogSafely("Application shutdown completed."); }
                    catch (Exception ex) { LogSafely($"Application shutdown failed: {ex}"); Environment.ExitCode = 1; }
                    finally
                    {
                        shutdownFinished = true;
                        try { MainAppWindow?.Close(); }
                        catch (Exception ex)
                        {
                            LogSafely($"MainWindow close after shutdown failed: {ex}");
                            Environment.ExitCode = 1;
                            Exit();
                        }
                    }
                };
                MainAppWindow.Closed += (_, _) => LogSafely("MainWindow.Closed triggered after shutdown.");

                GetService<IThemeService>().Initialize(MainAppWindow);
                Log("ThemeService initialized.");

                MainWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(MainAppWindow);
                var tray = GetService<ITrayService>();
                _lifecycle.TrackTray(tray);
                tray.Initialize(MainWindowHandle);
                Log("System Tray initialization attempted (optional).");

                if (GetService<IAppSettingsService>().AutoStartP2pEngine)
                {
                    if (await _lifecycle.StartOptionalP2pAsync(() => GetService<IP2pSyncService>()))
                        Log("P2P auto-start completed.");
                    else
                        LogSafely($"P2P auto-start unavailable; shell startup continues: {_lifecycle.LastOptionalP2pStartupError}");
                }
            }, () =>
            {
                MainAppWindow!.Activate();
                LogSafely("MainWindow activated.");
            });
        }
        catch (Exception ex)
        {
            Environment.ExitCode = 1;
            LogSafely($"Critical startup failure; window will not be presented: {ex}");
            try { await ShutdownAsync(); }
            catch (Exception shutdownEx) { LogSafely($"Startup cleanup failed: {shutdownEx}"); }
            Exit();
        }
    }
}
