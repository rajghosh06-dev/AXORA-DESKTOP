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
            Log($"[App.UnhandledException] Message: {e.Message} | HResult: 0x{e.Exception.HResult:X8}\n{e.Exception}");
            if (e.Exception is not (OutOfMemoryException or AccessViolationException))
            {
                e.Handled = true;
            }
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
                services.AddSingleton<IVoiceCoordinator, VoiceCoordinator>();
                services.AddSingleton<IDocumentChatService, DocumentChatService>();
                services.AddSingleton<ITrayService, TrayService>();
                services.AddSingleton<IP2pSyncService, P2pSyncService>();
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

    private static readonly object _shutdownLock = new();
    private static System.Threading.Tasks.Task? _shutdownTask;

    public static System.Threading.Tasks.Task ShutdownAsync()
    {
        lock (_shutdownLock)
        {
            _shutdownTask ??= DoShutdownAsync();
            return _shutdownTask;
        }
    }

    private static async System.Threading.Tasks.Task DoShutdownAsync()
    {
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
        void Log(string msg) => File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");

        Log("App.ShutdownAsync initiated.");

        try
        {
            var tray = TryGetService<ITrayService>();
            if (tray != null)
            {
                try
                {
                    tray.Remove();
                    if (tray is IDisposable disposableTray)
                    {
                        disposableTray.Dispose();
                    }
                    Log("TrayService removed and disposed.");
                }
                catch (Exception ex)
                {
                    Log($"Tray disposal error: {ex.Message}");
                }
            }

            var p2p = TryGetService<IP2pSyncService>();
            if (p2p != null)
            {
                try
                {
                    await p2p.StopAsync().ConfigureAwait(false);
                    Log("P2pSyncService stopped.");
                }
                catch (Exception ex)
                {
                    Log($"P2P stop error: {ex.Message}");
                }
            }

            var voiceCoord = TryGetService<IVoiceCoordinator>();
            if (voiceCoord != null)
            {
                try
                {
                    voiceCoord.Dispose();
                    Log("VoiceCoordinator disposed.");
                }
                catch (Exception ex)
                {
                    Log($"VoiceCoordinator disposal error: {ex.Message}");
                }
            }

            if (AppHost != null)
            {
                try
                {
                    await AppHost.StopAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                    AppHost.Dispose();
                    Log("AppHost stopped and disposed.");
                }
                catch (Exception ex)
                {
                    Log($"AppHost shutdown error: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"ShutdownAsync fatal error: {ex}");
        }
        finally
        {
            Log("App.ShutdownAsync completed.");
        }
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

            MainAppWindow.Closed += async (_, _) =>
            {
                Log("MainWindow.Closed triggered. Commencing graceful shutdown.");
                await ShutdownAsync();
            };

            var themeService = GetService<IThemeService>();
            themeService.Initialize(MainAppWindow);
            Log("ThemeService initialized.");

            MainAppWindow.Activate();
            Log("MainWindow activated.");

            AppHost.StartAsync().ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    Log($"[AppHost] Background StartAsync failed: {t.Exception.Flatten()}");
                }
            }, System.Threading.Tasks.TaskScheduler.Default);
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
                p2p.StartAsync().ContinueWith(t =>
                {
                    if (t.IsFaulted && t.Exception != null)
                    {
                        Log($"[P2P] Background StartAsync failed: {t.Exception.Flatten()}");
                    }
                }, System.Threading.Tasks.TaskScheduler.Default);
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
