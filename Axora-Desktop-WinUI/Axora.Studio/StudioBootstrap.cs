using Axora.Studio.Services;
using Axora.Studio.Services.Contracts;
using Axora.Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Axora.Studio;

public static class StudioBootstrap
{
    public static IHost BuildHost(StudioPathService paths, Action<IServiceCollection>? configure = null)
    {
        return new HostBuilder()
            .UseDefaultServiceProvider(options => { options.ValidateOnBuild = true; options.ValidateScopes = true; })
            .ConfigureLogging(logging => { logging.ClearProviders(); logging.AddDebug(); })
            .ConfigureServices(services =>
            {
                // No default configuration files, background features or console lifetime.
                services.AddSingleton<IHostLifetime, StudioHostLifetime>();
                services.AddSingleton(paths);
                services.AddSingleton<StudioWriterLease>();
                services.AddSingleton<ISettingsFilePublisher, SettingsFilePublisher>();
                services.AddSingleton<StudioSettingsService>();
                services.AddSingleton<ShellViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton<FlashcardReviewPolicy>();
                services.AddSingleton<FlashcardTextGenerator>();
                services.AddSingleton<FlashcardsViewModel>();
                // Resolving the delegate constructs no feature. Only invoking it resolves the session singleton.
                services.AddSingleton<Func<FlashcardsViewModel>>(sp => () => sp.GetRequiredService<FlashcardsViewModel>());
                services.AddSingleton<IStudioSavePicker, StudioSavePicker>();
                services.AddSingleton<IExportFilePublisher>(sp => new ExportFilePublisher(sp.GetRequiredService<StudioPathService>()));
                services.AddSingleton<FlashcardExportCoordinator>();
                services.AddSingleton<Func<FlashcardExportCoordinator>>(sp => () => sp.GetRequiredService<FlashcardExportCoordinator>());
                // Delegate only: the App session owns the instance; Host never owns native speech cleanup.
                services.AddSingleton<Func<IFlashcardReadAloudService>>(_ => () => new FlashcardReadAloudService(new WindowsFlashcardReadAloudBackend()));
                services.AddSingleton<ResumeCodec>();
                services.AddSingleton<ResumeStore>();
                services.AddSingleton<IResumeFilePublisher, ResumeFilePublisher>();
                services.AddSingleton<IResumeFilePicker, ResumeFilePicker>();
                services.AddSingleton<ResumeSession>();
                services.AddSingleton<ResumeViewModel>();
                services.AddSingleton<Func<ResumeViewModel>>(sp => () => sp.GetRequiredService<ResumeViewModel>());
                configure?.Invoke(services);
            }).Build();
    }

    private sealed class StudioHostLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
