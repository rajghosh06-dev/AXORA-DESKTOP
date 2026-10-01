using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;
using Axora.Desktop.Models.Voice;
using Axora.Desktop.Services;
using Axora.Desktop.Services.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace Axora.Desktop.Tests;

public partial class Program
{
    private static async Task RunP3BLifecycleTests()
    {
        var failedHost = new LifecycleHost { FailStart = true };
        var failedStartup = new AppLifecycle(failedHost);
        bool shown = false;
        bool initialized = false;
        try { await failedStartup.StartAsync(() => { initialized = true; return Task.CompletedTask; }, () => shown = true); }
        catch (InvalidOperationException) { }
        Assert(!shown && !initialized, "P3B-01: failed host start never initializes or presents the window");
        await failedStartup.ShutdownAsync();
        Assert(failedHost.StopCount == 0 && failedHost.DisposeCount == 1,
            "P3B-02: failed host start only disposes the partially initialized host");

        var partialHost = new LifecycleHost();
        var partial = new AppLifecycle(partialHost);
        var partialTray = new LifecycleTray();
        var unstartedP2p = new LifecycleP2p();
        shown = false;
        try
        {
            await partial.StartAsync(() =>
            {
                partial.TrackTray(partialTray);
                throw new InvalidOperationException("simulated mandatory initialization failure");
            }, () => shown = true);
        }
        catch (InvalidOperationException) { }
        await partial.ShutdownAsync();
        Assert(!shown, "P3B-03: critical pre-activation failure cannot present a healthy window");
        Assert(partialTray.RemoveCount == 1 && unstartedP2p.StopCount == 0 &&
               partialHost.StopCount == 1 && partialHost.DisposeCount == 1,
            "P3B-04: partial startup cleans only tracked resources and the started host");

        var host = new LifecycleHost();
        var lifecycle = new AppLifecycle(host);
        var tray = new LifecycleTray();
        var p2p = new LifecycleP2p();
        var voice = new LifecycleVoice { FailStop = true };
        lifecycle.TrackTray(tray);
        lifecycle.CreateP2p(() => p2p);
        lifecycle.CreateVoice(() => voice);
        shown = false;
        await lifecycle.StartAsync(() => Task.CompletedTask, () => shown = true);
        Assert(shown && host.StartCount == 1, "P3B-05: healthy activation follows completed host startup");
        Task firstShutdown = lifecycle.ShutdownAsync();
        Task secondShutdown = lifecycle.ShutdownAsync();
        Assert(ReferenceEquals(firstShutdown, secondShutdown), "P3B-06: duplicate shutdown calls share one operation");
        bool reportedFailure = false;
        try { await firstShutdown; }
        catch (AggregateException ex) { reportedFailure = ex.InnerExceptions.Any(e => e.Message.Contains("Voice operational stop", StringComparison.Ordinal)); }
        Assert(reportedFailure, "P3B-07: operational stop failure remains visible to caller");
        Assert(voice.StopCount == 1 && p2p.StopCount == 1 && tray.RemoveCount == 1 &&
               host.StopCount == 0 && host.DisposeCount == 0,
            "P3B-08: failed voice stop does not dispose services it may still be using");

        var closingHost = new LifecycleHost();
        var closingLifecycle = new AppLifecycle(closingHost);
        var startupGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        shown = false;
        Task startup = closingLifecycle.StartAsync(() => startupGate.Task, () => shown = true);
        Task closing = closingLifecycle.ShutdownAsync();
        Assert(!closing.IsCompleted, "P3B-09: shutdown waits for in-flight startup before disposing the host");
        startupGate.SetResult();
        bool closedBeforeActivation = false;
        try { await startup; }
        catch (OperationCanceledException) { closedBeforeActivation = true; }
        await closing;
        Assert(closedBeforeActivation && !shown && closingHost.StopCount == 1 && closingHost.DisposeCount == 1,
            "P3B-10: close during startup prevents activation and still releases host once");

        int constructedAfterShutdown = 0;
        try { closingLifecycle.CreateVoice(() => { constructedAfterShutdown++; return new LifecycleVoice(); }); }
        catch (InvalidOperationException) { }
        try { closingLifecycle.CreateP2p(() => { constructedAfterShutdown++; return new LifecycleP2p(); }); }
        catch (InvalidOperationException) { }
        Assert(constructedAfterShutdown == 0,
            "P3B-11: shutdown rejects lazy voice/P2P construction before constructor side effects");

        var loggingHost = new LifecycleHost();
        var loggingTray = new LifecycleTray();
        var loggingLifecycle = new AppLifecycle(loggingHost, _ => throw new IOException("injected log failure"));
        loggingLifecycle.TrackTray(loggingTray);
        await loggingLifecycle.StartAsync(() => Task.CompletedTask, () => { });
        bool loggingFailureReported = false;
        try { await loggingLifecycle.ShutdownAsync(); }
        catch (AggregateException ex) { loggingFailureReported = ex.InnerExceptions.Count > 0; }
        Assert(loggingFailureReported && loggingTray.RemoveCount == 1 &&
               loggingHost.StopCount == 1 && loggingHost.DisposeCount == 1,
            "P3B-12: logging failure is reported without skipping owned cleanup");

        var trayFailureHost = new LifecycleHost();
        var trayFailure = new LifecycleTray { FailRemove = true };
        var trayFailureLifecycle = new AppLifecycle(trayFailureHost);
        trayFailureLifecycle.TrackTray(trayFailure);
        await trayFailureLifecycle.StartAsync(() => Task.CompletedTask, () => { });
        bool trayFailureReported = false;
        try { await trayFailureLifecycle.ShutdownAsync(); }
        catch (AggregateException ex) { trayFailureReported = ex.InnerExceptions.Count == 1; }
        Assert(trayFailureReported && trayFailure.RemoveCount == 1 &&
               trayFailureHost.StopCount == 1 && trayFailureHost.DisposeCount == 1,
            "P3B-13: failed tray removal remains visible without skipping host disposal");

        var transcriber = new CountingTranscriber();
        var monitor = new CountingAudioMonitor();
        string settingsPath = Path.Combine(Path.GetTempPath(), "AxoraP3B_" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var diHost = Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) =>
                {
                    services.AddSingleton<IVoiceTranscriberService>(_ => transcriber);
                    services.AddSingleton<ISpeechSynthesisService, W4MockSynthesizer>();
                    services.AddSingleton<IVoiceCommandRouter, VoiceCommandRouter>();
                    services.AddSingleton<IAudioDeviceMonitor>(_ => monitor);
                    services.AddSingleton<IVoiceTextFormatter, VoiceTextFormatter>();
                    services.AddSingleton<IAppSettingsService>(new AppSettingsService(customDirectory: settingsPath));
                    services.AddSingleton<IVoiceCoordinator, VoiceCoordinator>();
                }).Build())
            {
                var coordinator = diHost.Services.GetRequiredService<IVoiceCoordinator>();
                await coordinator.RequestStartDictationAsync(_ => { });
                await coordinator.StopAsync();
                await coordinator.StopAsync();
                Assert(transcriber.StopCount == 1 && !transcriber.IsRecording,
                    "P3B-14: repeated voice stop terminates dictation exactly once");
                Assert(transcriber.DisposeCount == 0,
                    "P3B-15: coordinator operational stop does not dispose injected DI service");
                Assert(monitor.SubscriptionAdds == 1 && monitor.SubscriptionRemoves == 1 && monitor.DisposeCount == 0,
                    "P3B-16: coordinator detaches its watcher subscription once without disposing the DI monitor");
                Assert(await coordinator.RequestStartDictationAsync(_ => { }) == VoiceRecognitionStartResult.Unavailable,
                    "P3B-17: voice work cannot restart after operational stop");
            }
            Assert(transcriber.DisposeCount == 1 && monitor.DisposeCount == 1 && monitor.SubscriptionRemoves == 1,
                "P3B-18: DI host alone final-disposes injected services exactly once");

            var deferredSpeech = new DeferredSynthesizer();
            var speechCoordinator = new VoiceCoordinator(
                new W4MockTranscriber(), deferredSpeech, new VoiceCommandRouter(),
                new W4MockAudioMonitor(), new VoiceTextFormatter(),
                new AppSettingsService(customDirectory: settingsPath));
            Task<SpeechPlaybackResult> speechRequest = speechCoordinator.RequestSpeakAsync("pending speech");
            await deferredSpeech.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Task stoppingSpeech = speechCoordinator.StopAsync();
            Assert(await speechCoordinator.RequestSpeakAsync("new work") == SpeechPlaybackResult.Unavailable,
                "P3B-R1-01: voice admission closes as soon as operational stop begins");
            Assert(!speechRequest.IsCompleted && !stoppingSpeech.IsCompleted,
                "P3B-19: voice stop waits for an in-flight synthesis task before host disposal");
            deferredSpeech.Release.SetResult();
            await speechRequest.WaitAsync(TimeSpan.FromSeconds(2));
            await stoppingSpeech.WaitAsync(TimeSpan.FromSeconds(2));
            speechCoordinator.Dispose();
            Assert(deferredSpeech.LatePlaybackCount == 0 && deferredSpeech.StopCount == 1 && deferredSpeech.DisposeCount == 0,
                "P3B-20: shutdown cancellation prevents late playback and leaves injected synthesizer disposal to its owner");
            deferredSpeech.Dispose();

            var callbackSpeech = new DeferredSynthesizer();
            var callbackCoordinator = new VoiceCoordinator(
                new W4MockTranscriber(), callbackSpeech, new VoiceCommandRouter(),
                new W4MockAudioMonitor(), new VoiceTextFormatter(),
                new AppSettingsService(customDirectory: settingsPath));
            Task<SpeechPlaybackResult> callbackRequest = callbackCoordinator.RequestSpeakAsync("callback race");
            await callbackSpeech.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var stateLock = (SemaphoreSlim)typeof(VoiceCoordinator)
                .GetField("_stateLock", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(callbackCoordinator)!;
            await stateLock.WaitAsync();
            Task stoppingCallback = callbackCoordinator.StopAsync();
            stateLock.Release();
            callbackSpeech.Release.SetResult();
            await callbackRequest.WaitAsync(TimeSpan.FromSeconds(2));
            await stoppingCallback.WaitAsync(TimeSpan.FromSeconds(2));
            callbackCoordinator.Dispose();
            Assert(callbackRequest.IsCompletedSuccessfully && stoppingCallback.IsCompletedSuccessfully,
                "P3B-R1-08: pre-shutdown terminal speech work is tracked and drained before semaphore disposal");
            callbackSpeech.Dispose();

            var hungHost = new LifecycleHost();
            var hungVoice = new LifecycleVoice { BlockStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
            var hungLifecycle = new AppLifecycle(hungHost, voiceStopTimeout: TimeSpan.FromMilliseconds(40));
            hungLifecycle.CreateVoice(() => hungVoice);
            await hungLifecycle.StartAsync(() => Task.CompletedTask, () => { });
            bool timedOut = false;
            try { await hungLifecycle.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(2)); }
            catch (AggregateException ex) { timedOut = ex.ToString().Contains("TimeoutException", StringComparison.Ordinal); }
            Assert(timedOut && hungHost.StopCount == 0 && hungHost.DisposeCount == 0,
                "P3B-R1-02: hung voice stop is bounded, fails truthfully and retains DI host ownership");
            hungVoice.BlockStop.SetResult();

            var optionalHost = new LifecycleHost();
            var optionalLifecycle = new AppLifecycle(optionalHost);
            var failedP2p = new LifecycleP2p { FailStart = true };
            bool optionalShown = false;
            bool optionalReady = true;
            await optionalLifecycle.StartAsync(async () =>
                optionalReady = await optionalLifecycle.StartOptionalP2pAsync(() => optionalLifecycle.CreateP2p(() => failedP2p)),
                () => optionalShown = true);
            Assert(optionalShown && !optionalReady && optionalLifecycle.LastOptionalP2pStartupError is InvalidOperationException && !failedP2p.IsRunning,
                "P3B-R1-03: optional P2P start failure does not suppress shell activation or claim healthy state");
            await optionalLifecycle.ShutdownAsync();
            Assert(failedP2p.StopCount == 1 && optionalHost.DisposeCount == 1,
                "P3B-R1-04: failed but constructed P2P is tracked for partial cleanup");

            var unresolvedHost = new LifecycleHost();
            var unresolvedLifecycle = new AppLifecycle(unresolvedHost);
            bool unresolvedShown = false;
            await unresolvedLifecycle.StartAsync(async () =>
                await unresolvedLifecycle.StartOptionalP2pAsync(() => throw new InvalidOperationException("P2P construction failed")),
                () => unresolvedShown = true);
            await unresolvedLifecycle.ShutdownAsync();
            Assert(unresolvedShown && unresolvedHost.DisposeCount == 1 && unresolvedLifecycle.LastOptionalP2pStartupError is not null,
                "P3B-R1-05: optional resolution failure creates no fictitious P2P stop obligation");

            var fakeNative = new FakeTrayNative();
            var ownedTray = new TrayService(NullLogger<TrayService>.Instance, fakeNative);
            ownedTray.Initialize(new IntPtr(1));
            fakeNative.FailDelete = true;
            bool deletionFailed = false;
            try { ownedTray.Remove(); } catch (System.ComponentModel.Win32Exception) { deletionFailed = true; }
            Assert(deletionFailed && fakeNative.DeleteCount == 1 && fakeNative.DestroyCount == 0,
                "P3B-R1-06: failed tray delete retains native icon ownership without early destruction");
            fakeNative.FailDelete = false;
            ownedTray.Dispose();
            ownedTray.Dispose();
            Assert(fakeNative.DeleteCount == 2 && fakeNative.DestroyCount == 1,
                "P3B-R1-07: successful retry removes registration and destroys HICON exactly once");

            var destroyNative = new FakeTrayNative { FailDestroy = true };
            var destroyTray = new TrayService(NullLogger<TrayService>.Instance, destroyNative);
            destroyTray.Initialize(new IntPtr(2));
            bool iconReleaseFailed = false;
            try { destroyTray.Remove(); } catch (System.ComponentModel.Win32Exception) { iconReleaseFailed = true; }
            destroyNative.FailDestroy = false;
            destroyTray.Dispose();
            Assert(iconReleaseFailed && destroyNative.DeleteCount == 1 && destroyNative.DestroyCount == 2,
                "P3B-R1-12: failed HICON release retains its handle for retry without duplicate NIM_DELETE");

            var addNative = new FakeTrayNative { FailAdd = true };
            var addTray = new TrayService(NullLogger<TrayService>.Instance, addNative);
            addTray.Initialize(new IntPtr(3));
            addTray.Dispose();
            Assert(addNative.DeleteCount == 0 && addNative.DestroyCount == 1,
                "P3B-R1-13: failed native add releases only its partially loaded HICON");

            await VerifyVoiceTaskPublishedBeforeNativeStopAsync(settingsPath);
            await VerifyActiveSendStopBoundAsync();
            await VerifyP2pLifecycleLockBoundAsync();
            await VerifyP2pIncompleteStopRetryAsync();
        }
        finally
        {
            if (Directory.Exists(settingsPath)) Directory.Delete(settingsPath, recursive: true);
        }
    }

    private static async Task VerifyVoiceTaskPublishedBeforeNativeStopAsync(string settingsPath)
    {
        var nativeStop = new BlockingStopTranscriber();
        var coordinator = new VoiceCoordinator(nativeStop, new W4MockSynthesizer(),
            new VoiceCommandRouter(), new W4MockAudioMonitor(), new VoiceTextFormatter(),
            new AppSettingsService(customDirectory: settingsPath));
        var host = new LifecycleHost();
        var lifecycle = new AppLifecycle(host, voiceStopTimeout: TimeSpan.FromMilliseconds(80));
        lifecycle.CreateVoice(() => coordinator);
        await lifecycle.StartAsync(() => Task.CompletedTask, () => { });

        // StartNew preserves the outer Task: if StopAsync synchronously enters
        // a native stop before publishing its Task, this assertion times out
        // without wedging the whole test runner.
        Task<Task> publication = Task.Factory.StartNew(() => lifecycle.ShutdownAsync(),
            CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        try
        {
            Task shutdown = await publication.WaitAsync(TimeSpan.FromSeconds(2));
            await nativeStop.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert(!shutdown.IsCompleted,
                "P3B-R2-01: production VoiceCoordinator publishes the stop Task before native stop completes");
            bool timedOut = false;
            try { await shutdown.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch (AggregateException ex) { timedOut = ex.ToString().Contains("TimeoutException", StringComparison.Ordinal); }
            Assert(timedOut && host.StopCount == 0 && host.DisposeCount == 0,
                "P3B-R2-02: lifecycle observes the real coordinator timeout and withholds host disposal");
        }
        finally { nativeStop.ReleaseStop(); }

        await coordinator.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        coordinator.Dispose();
        Assert(coordinator.CurrentState == VoiceSessionState.Idle && nativeStop.DisposeCount == 0,
            "P3B-R2-03: late native completion safely finishes before owner disposal");
        nativeStop.Dispose();
    }

    private static async Task VerifyActiveSendStopBoundAsync()
    {
        var p2p = new P2pSyncService(NullLogger<P2pSyncService>.Instance,
            backgroundShutdownTimeout: TimeSpan.FromMilliseconds(80));
        var socket = new BlockingSendWebSocket();
        var serviceType = typeof(P2pSyncService);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        serviceType.GetField("_serverCts", flags)!.SetValue(p2p, new CancellationTokenSource());
        object entry = serviceType.GetMethod("RegisterSocket", flags)!.Invoke(p2p, [socket])!;
        var ownerRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task ClientOwnerAsync()
        {
            await ownerRelease.Task;
            await (Task)serviceType.GetMethod("RetireAndDrainSocketAsync", flags)!.Invoke(p2p, [entry])!;
            socket.Dispose();
        }
        Task owner = ClientOwnerAsync();
        var clients = (ConcurrentDictionary<long, Task>)serviceType.GetField("_clientTasks", flags)!.GetValue(p2p)!;
        clients[1] = owner;

        Task outbound = p2p.BroadcastAsync([1, 2, 3]);
        await socket.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        bool incomplete = false;
        try { await p2p.StopAsync().WaitAsync(TimeSpan.FromSeconds(2)); }
        catch (InvalidOperationException) { incomplete = true; }
        await socket.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await socket.AbortStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert(incomplete && !socket.IsDisposed && !outbound.IsCompleted && socket.AbortCount == 1,
            "P3B-R2-04: active send and synchronous socket abort are both bounded; service cancellation reaches the send and ownership remains");

        socket.ReleaseSend.TrySetResult();
        socket.ReleaseAbort.TrySetResult();
        ownerRelease.TrySetResult();
        await Task.WhenAll(outbound, owner).WaitAsync(TimeSpan.FromSeconds(2));
        await p2p.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Assert(socket.IsDisposed && !socket.DisposedDuringSend && !socket.DisposedDuringAbort,
            "P3B-R2-05: completed send/abort leases precede client disposal and a second stop finishes cleanup");
        p2p.Dispose();
    }

    private static async Task VerifyP2pLifecycleLockBoundAsync()
    {
        var p2p = new P2pSyncService(NullLogger<P2pSyncService>.Instance,
            backgroundShutdownTimeout: TimeSpan.FromMilliseconds(80));
        var lifecycleLock = (SemaphoreSlim)typeof(P2pSyncService)
            .GetField("_lifecycleLock", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(p2p)!;
        await lifecycleLock.WaitAsync();
        bool boundedFailure = false;
        try { await p2p.StopAsync().WaitAsync(TimeSpan.FromSeconds(2)); }
        catch (TimeoutException) { boundedFailure = true; }
        finally { lifecycleLock.Release(); }
        Assert(boundedFailure, "P3B-R2-06: pre-drain P2P lifecycle-lock contention is bounded and reported incomplete");
        await p2p.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        bool stillIncomplete = (bool)typeof(P2pSyncService)
            .GetField("_backgroundTasksIncomplete", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(p2p)!;
        p2p.Dispose();
        Assert(!stillIncomplete, "P3B-R2-07: a later stop clears incomplete ownership after lifecycle-lock contention ends");
    }

    private static async Task VerifyP2pIncompleteStopRetryAsync()
    {
        var p2p = new P2pSyncService(NullLogger<P2pSyncService>.Instance,
            backgroundShutdownTimeout: TimeSpan.FromMilliseconds(40));
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cts = new CancellationTokenSource();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var serviceType = typeof(P2pSyncService);
        serviceType.GetField("_serverCts", flags)!.SetValue(p2p, cts);
        serviceType.GetField("_acceptTask", flags)!.SetValue(p2p, pending.Task);
        bool incomplete = false;
        try { await p2p.StopAsync().WaitAsync(TimeSpan.FromSeconds(2)); }
        catch (InvalidOperationException) { incomplete = true; }
        Assert(incomplete && ReferenceEquals(serviceType.GetField("_serverCts", flags)!.GetValue(p2p), cts) &&
               ReferenceEquals(serviceType.GetField("_acceptTask", flags)!.GetValue(p2p), pending.Task),
            "P3B-R1-09: timed-out P2P stop retains CTS and pending task for retry");
        bool disposalRejected = false;
        try { p2p.Dispose(); } catch (InvalidOperationException) { disposalRejected = true; }
        Assert(disposalRejected && ReferenceEquals(serviceType.GetField("_serverCts", flags)!.GetValue(p2p), cts),
            "P3B-R1-15: incomplete P2P work rejects disposal without losing retry ownership");
        bool restartBlocked = false;
        try { await p2p.StartAsync(); } catch (InvalidOperationException) { restartBlocked = true; }
        Assert(restartBlocked, "P3B-R1-10: P2P cannot restart while prior work remains incomplete");
        pending.SetResult();
        await p2p.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Assert(serviceType.GetField("_serverCts", flags)!.GetValue(p2p) is null &&
               serviceType.GetField("_acceptTask", flags)!.GetValue(p2p) is null,
            "P3B-R1-11: later P2P stop completes and releases retained lifecycle state");
        var clients = (ConcurrentDictionary<long, Task>)serviceType.GetField("_clientTasks", flags)!.GetValue(p2p)!;
        clients[1] = Task.CompletedTask;
        p2p.Dispose();
        bool disposedRejectedRestart = false;
        try { await p2p.StartAsync(); } catch (ObjectDisposedException) { disposedRejectedRestart = true; }
        Assert(disposedRejectedRestart,
            "P3B-R1-14: a completed client awaiting dictionary removal does not block final disposal");
    }

    private sealed class LifecycleHost : IHost
    {
        public IServiceProvider Services { get; } = new ServiceCollection().BuildServiceProvider();
        public bool FailStart { get; set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }
        public Task StartAsync(CancellationToken ct = default)
        {
            StartCount++;
            if (FailStart) throw new InvalidOperationException("injected host start failure");
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken ct = default) { StopCount++; return Task.CompletedTask; }
        public void Dispose() => DisposeCount++;
    }

    private sealed class LifecycleTray : ITrayService
    {
        public int RemoveCount { get; private set; }
        public bool FailRemove { get; set; }
        public void Initialize(IntPtr hwnd) { }
        public void ShowNotification(string title, string message) { }
        public void Remove()
        {
            RemoveCount++;
            if (FailRemove) throw new InvalidOperationException("injected tray removal failure");
        }
    }

    private sealed class LifecycleP2p : IP2pSyncService
    {
        public string PairingQrJson => "";
        public bool IsRunning => false;
        public int ConnectedDeviceCount => 0;
        public int StopCount { get; private set; }
        public bool FailStart { get; set; }
        public event EventHandler<AxoraDevice>? DeviceConnected { add { } remove { } }
        public event EventHandler<AxoraDevice>? DeviceDisconnected { add { } remove { } }
        public event EventHandler<QuickDropItem>? FileReceived { add { } remove { } }
        public Task StartAsync(CancellationToken ct = default) => FailStart
            ? Task.FromException(new InvalidOperationException("injected optional P2P startup failure"))
            : Task.CompletedTask;
        public Task StopAsync(CancellationToken ct = default) { StopCount++; return Task.CompletedTask; }
        public Task BroadcastAsync(byte[] payload, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class LifecycleVoice : IVoiceCoordinator
    {
        public VoiceSessionState CurrentState => VoiceSessionState.Idle;
        public bool IsVoiceNavigationEnabled { get; set; }
        public bool IsVoiceNavigationDesired => false;
        public AudioCaptureHealth CaptureHealth => AudioCaptureHealth.Healthy;
        public TimeSpan AcousticDebounceInterval { get; set; }
        public bool FailStop { get; set; }
        public TaskCompletionSource? BlockStop { get; set; }
        public int StopCount { get; private set; }
        public event EventHandler<VoiceSessionStateChangedEventArgs>? StateChanged { add { } remove { } }
        public Task<VoiceRecognitionStartResult> RequestStartDictationAsync(Action<string> callback, CancellationToken ct = default) => Task.FromResult(VoiceRecognitionStartResult.Unavailable);
        public Task RequestStopDictationAsync() => Task.CompletedTask;
        public Task<SpeechPlaybackResult> RequestSpeakAsync(string text, double? pitch = null, double? rate = null, CancellationToken ct = default) => Task.FromResult(SpeechPlaybackResult.Unavailable);
        public void RequestStopSpeech() { }
        public Task<VoiceRecognitionStartResult> StartVoiceNavigationAsync(CancellationToken ct = default) => Task.FromResult(VoiceRecognitionStartResult.Unavailable);
        public Task StopVoiceNavigationAsync() => Task.CompletedTask;
        public Task StopAsync()
        {
            StopCount++;
            if (FailStop) throw new InvalidOperationException("injected voice stop failure");
            return BlockStop?.Task ?? Task.CompletedTask;
        }
        public void Dispose() { }
    }

    private sealed class FakeTrayNative : TrayService.ITrayIconNative
    {
        public bool FailAdd { get; set; }
        public bool FailDelete { get; set; }
        public bool FailDestroy { get; set; }
        public int DeleteCount { get; private set; }
        public int DestroyCount { get; private set; }
        public IntPtr LoadFromFile(string path) => new(1234);
        public IntPtr LoadFallback() => new(32512);
        public bool Add(IntPtr hwnd, IntPtr icon) => !FailAdd;
        public bool Delete(IntPtr hwnd) { DeleteCount++; return !FailDelete; }
        public bool Destroy(IntPtr icon) { DestroyCount++; return !FailDestroy; }
    }

    private sealed class CountingTranscriber : IVoiceTranscriberService
    {
        private readonly W4MockTranscriber _inner = new();
        public bool IsRecording => _inner.IsRecording;
        public AudioCaptureHealth DeviceHealth => _inner.DeviceHealth;
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }
        public event EventHandler<VoiceTranscriberStateChangedEventArgs>? StateChanged
        {
            add => _inner.StateChanged += value;
            remove => _inner.StateChanged -= value;
        }
        public Task<bool> CheckPrerequisitesAsync(CancellationToken ct = default) => _inner.CheckPrerequisitesAsync(ct);
        public Task<VoiceRecognitionStartResult> StartDictationAsync(Action<TranscriptionChunk> callback, CancellationToken ct = default) => _inner.StartDictationAsync(callback, ct);
        public Task StopDictationAsync() { StopCount++; return _inner.StopDictationAsync(); }
        public void Dispose() { DisposeCount++; _inner.Dispose(); }
    }

    private sealed class BlockingStopTranscriber : IVoiceTranscriberService
    {
        private readonly W4MockTranscriber _inner = new();
        private readonly ManualResetEventSlim _release = new(false);
        public TaskCompletionSource StopEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int DisposeCount { get; private set; }
        public bool IsRecording => _inner.IsRecording;
        public AudioCaptureHealth DeviceHealth => _inner.DeviceHealth;
        public event EventHandler<VoiceTranscriberStateChangedEventArgs>? StateChanged
        {
            add => _inner.StateChanged += value;
            remove => _inner.StateChanged -= value;
        }
        public Task<bool> CheckPrerequisitesAsync(CancellationToken ct = default) => _inner.CheckPrerequisitesAsync(ct);
        public Task<VoiceRecognitionStartResult> StartDictationAsync(Action<TranscriptionChunk> callback, CancellationToken ct = default) => _inner.StartDictationAsync(callback, ct);
        public Task StopDictationAsync()
        {
            StopEntered.TrySetResult();
            _release.Wait(); // models a native invocation that blocks before returning its Task
            return Task.CompletedTask;
        }
        public void ReleaseStop() => _release.Set();
        public void Dispose() { DisposeCount++; _inner.Dispose(); _release.Dispose(); }
    }

    private sealed class BlockingSendWebSocket : WebSocket
    {
        private int _sendPending;
        private int _abortPending;
        public TaskCompletionSource SendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AbortStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseAbort { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsDisposed { get; private set; }
        public bool DisposedDuringSend { get; private set; }
        public bool DisposedDuringAbort { get; private set; }
        public int AbortCount { get; private set; }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => IsDisposed ? WebSocketState.Closed : WebSocketState.Open;
        public override string? SubProtocol => null;
        public override void Abort()
        {
            AbortCount++;
            Interlocked.Exchange(ref _abortPending, 1);
            AbortStarted.TrySetResult();
            try { ReleaseAbort.Task.GetAwaiter().GetResult(); }
            finally { Interlocked.Exchange(ref _abortPending, 0); }
        }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
            Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) =>
            BlockSendAsync(cancellationToken);
        public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) =>
            new(BlockSendAsync(cancellationToken));
        private async Task BlockSendAsync(CancellationToken ct)
        {
            Interlocked.Exchange(ref _sendPending, 1);
            using var registration = ct.Register(() => CancellationObserved.TrySetResult());
            SendStarted.TrySetResult();
            try { await ReleaseSend.Task; }
            finally { Interlocked.Exchange(ref _sendPending, 0); }
        }
        public override void Dispose()
        {
            DisposedDuringSend = Volatile.Read(ref _sendPending) != 0;
            DisposedDuringAbort = Volatile.Read(ref _abortPending) != 0;
            IsDisposed = true;
        }
    }

    private sealed class CountingAudioMonitor : IAudioDeviceMonitor
    {
        public bool HasMicrophone => true;
        public bool IsPermissionGranted => true;
        public string? DefaultCaptureDeviceName => "P3B mock microphone";
        public AudioCaptureHealth CurrentHealth => AudioCaptureHealth.Healthy;
        public int SubscriptionAdds { get; private set; }
        public int SubscriptionRemoves { get; private set; }
        public int DisposeCount { get; private set; }
        public event EventHandler<AudioDeviceStatusChangedEventArgs>? DeviceStatusChanged
        {
            add { SubscriptionAdds++; }
            remove { SubscriptionRemoves++; }
        }
        public Task RefreshStatusAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() => DisposeCount++;
    }

    private sealed class DeferredSynthesizer : ISpeechSynthesisService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsSpeaking => false;
        public VoiceInfo? CurrentVoice => null;
        public IReadOnlyList<VoiceInfo> AvailableVoices => Array.Empty<VoiceInfo>();
        public double SpeechRate { get; set; } = 1;
        public double SpeechPitch { get; set; } = 1;
        public double SpeechVolume { get; set; } = 1;
        public int LatePlaybackCount { get; private set; }
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }
        public event EventHandler<SpeechPlaybackStateChangedEventArgs>? PlaybackStateChanged;
        public void RaisePlaybackEnded() => PlaybackStateChanged?.Invoke(this,
            new SpeechPlaybackStateChangedEventArgs(false, null));
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public async Task<SpeechPlaybackResult> SpeakTextAsync(string text, double pitch = 1, double rate = 1, CancellationToken ct = default)
        {
            Started.TrySetResult();
            PlaybackStateChanged?.Invoke(this, new SpeechPlaybackStateChangedEventArgs(true, null));
            await Release.Task;
            if (ct.IsCancellationRequested) return SpeechPlaybackResult.Canceled;
            LatePlaybackCount++;
            PlaybackStateChanged?.Invoke(this, new SpeechPlaybackStateChangedEventArgs(false, null));
            return SpeechPlaybackResult.Completed;
        }
        public void Stop() => StopCount++;
        public void Pause() { }
        public void Resume() { }
        public void SetVoice(string voiceId) { }
        public void Dispose() => DisposeCount++;
    }
}
