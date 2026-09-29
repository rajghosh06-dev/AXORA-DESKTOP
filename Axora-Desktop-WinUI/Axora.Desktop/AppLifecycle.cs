using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Axora.Desktop.Services.Contracts;
using Microsoft.Extensions.Hosting;

namespace Axora.Desktop;

/// <summary>
/// Coordinates operational startup/stop. The host remains the sole final-disposal owner
/// of services created by its container, including services tracked here.
/// </summary>
public sealed class AppLifecycle
{
    // Engineering bound for active native voice teardown, not a guarantee that
    // Windows speech APIs will complete within this interval.
    public static readonly TimeSpan DefaultVoiceStopTimeout = TimeSpan.FromSeconds(10);
    private readonly IHost _host;
    private readonly Action<string>? _log;
    private readonly TimeSpan _voiceStopTimeout;
    private readonly object _gate = new();
    private Task? _shutdownTask;
    private ITrayService? _tray;
    private IP2pSyncService? _p2p;
    private IVoiceCoordinator? _voice;
    private bool _hostStarted;
    private bool _startupEntered;
    private readonly TaskCompletionSource _startupCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Exception? LastOptionalP2pStartupError { get; private set; }

    public AppLifecycle(IHost host, Action<string>? log = null, TimeSpan? voiceStopTimeout = null)
    {
        _host = host;
        _log = log;
        _voiceStopTimeout = voiceStopTimeout ?? DefaultVoiceStopTimeout;
        if (_voiceStopTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(voiceStopTimeout));
    }

    public void TrackTray(ITrayService tray) => Track(() => _tray = tray);

    // Resolve lazy services atomically with shutdown admission. Otherwise a
    // constructor could start a watcher after shutdown began and never be tracked.
    public IVoiceCoordinator CreateVoice(Func<IVoiceCoordinator> create)
    {
        lock (_gate)
        {
            EnsureNotShuttingDown();
            return _voice = create();
        }
    }

    public IP2pSyncService CreateP2p(Func<IP2pSyncService> create)
    {
        lock (_gate)
        {
            EnsureNotShuttingDown();
            return _p2p = create();
        }
    }

    private void Track(Action assign)
    {
        lock (_gate)
        {
            EnsureNotShuttingDown();
            assign();
        }
    }

    private void EnsureNotShuttingDown()
    {
        if (_shutdownTask is not null)
            throw new InvalidOperationException("Cannot start a service after application shutdown began.");
    }

    /// <summary>
    /// Optional Mobile Link startup remains awaited and lifecycle-owned, but cannot
    /// prevent the normal shell from activating. A failed service remains tracked
    /// so partial startup can be stopped during shutdown.
    /// </summary>
    public async Task<bool> StartOptionalP2pAsync(Func<IP2pSyncService> resolveP2p)
    {
        try
        {
            var p2p = resolveP2p();
            await p2p.StartAsync();
            LastOptionalP2pStartupError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastOptionalP2pStartupError = ex;
            try { _log?.Invoke($"Optional P2P startup failed; Mobile Link unavailable: {ex}"); }
            catch (Exception loggingError)
            {
                System.Diagnostics.Debug.WriteLine($"Optional P2P startup and logging failed: {ex}; {loggingError}");
            }
            return false;
        }
    }

    /// <summary>Only activates the window after all mandatory startup work has completed.</summary>
    public async Task StartAsync(Func<Task> initializeBeforeActivation, Action activate)
    {
        lock (_gate)
        {
            if (_shutdownTask is not null)
                throw new InvalidOperationException("Application is already shutting down.");
            if (_startupEntered)
                throw new InvalidOperationException("Application startup was already requested.");
            _startupEntered = true;
        }
        try
        {
            await _host.StartAsync();
            lock (_gate) _hostStarted = true;
            await initializeBeforeActivation();
            lock (_gate)
            {
                if (_shutdownTask is not null)
                    throw new OperationCanceledException("Application closed during startup.");
                activate();
            }
        }
        finally
        {
            _startupCompleted.TrySetResult();
        }
    }

    public Task ShutdownAsync()
    {
        lock (_gate)
            return _shutdownTask ??= ShutdownCoreAsync();
    }

    private async Task ShutdownCoreAsync()
    {
        if (_startupEntered) await _startupCompleted.Task;
        var errors = new List<Exception>();
        void Record(string message)
        {
            try { _log?.Invoke(message); }
            catch (Exception ex)
            {
                errors.Add(new InvalidOperationException("Application lifecycle logging failed.", ex));
            }
        }
        Record("App shutdown initiated.");
        async Task<bool> Step(string name, Func<Task> action)
        {
            try { await action(); Record($"{name} completed."); return true; }
            catch (Exception ex)
            {
                errors.Add(new InvalidOperationException($"{name} failed.", ex));
                Record($"{name} failed: {ex}");
                return false;
            }
        }

        bool ownedWorkStopped = true;
        if (_voice is not null)
            ownedWorkStopped &= await Step("Voice operational stop", () => _voice.StopAsync().WaitAsync(_voiceStopTimeout));
        if (_p2p is not null)
            ownedWorkStopped &= await Step("P2P operational stop", () => _p2p.StopAsync());
        if (_tray is not null) await Step("Tray removal", () => { _tray.Remove(); return Task.CompletedTask; });
        if (ownedWorkStopped)
        {
            if (_hostStarted) await Step("AppHost stop", () => _host.StopAsync(TimeSpan.FromSeconds(3)));
            await Step("AppHost final disposal", () => { _host.Dispose(); return Task.CompletedTask; });
        }
        else
        {
            // Native work may still access DI-owned services. Keep the host alive
            // until process exit; the caller receives a failing shutdown result.
            errors.Add(new InvalidOperationException("Host disposal postponed because voice or P2P operational stop is incomplete."));
            Record("AppHost stop/disposal postponed; owned work may still be active.");
        }

        Record(errors.Count == 0 ? "App shutdown completed." : "App shutdown completed with failures.");
        if (errors.Count > 0)
            throw new AggregateException("Application shutdown had one or more failures; all cleanup steps were attempted.", errors);
    }
}
