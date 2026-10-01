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
                configure?.Invoke(services);
            }).Build();
    }

    private sealed class StudioHostLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
