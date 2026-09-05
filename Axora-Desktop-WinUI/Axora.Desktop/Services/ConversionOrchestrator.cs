using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Production-grade Conversion Orchestrator implementing bounded, resource-aware concurrency,
/// deterministic engine routing, centralized collision resolution, atomic staging, and failure isolation.
/// </summary>
public sealed class ConversionOrchestrator : IConversionOrchestrator
{
    public enum RoutingStatus
    {
        Supported,
        Unsupported,
        Ambiguous,
        Invalid
    }

    public sealed record RoutingDecision(
        RoutingStatus Status,
        IConversionEngine? Engine,
        string? Reason = null);

    private readonly List<IConversionEngine> _engines = new();
    private readonly ILogger<ConversionOrchestrator>? _logger;

    private readonly List<ConversionJob> _queue = new();
    private readonly object _queueLock = new();

    private readonly HashSet<string> _reservedDestinationPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _reservationLock = new();

    // Resource affinity limiters
    private readonly SemaphoreSlim _cpuLimiter;
    private readonly SemaphoreSlim _memoryLimiter;
    private readonly SemaphoreSlim _processLimiter;
    private readonly SemaphoreSlim _exclusiveLimiter;
    private readonly SemaphoreSlim _globalLimiter;

    private readonly AutoResetEvent _dispatchSignal = new(false);
    private CancellationTokenSource? _processingCts;
    private Task? _schedulerTask;

    private volatile bool _isProcessing;
    private volatile bool _isPaused;
    private volatile bool _isDisposed;

    private readonly ConcurrentDictionary<string, Task> _runningJobTasks = new();

    public IReadOnlyList<IConversionEngine> RegisteredEngines
    {
        get
        {
            lock (_engines)
            {
                return _engines.ToList().AsReadOnly();
            }
        }
    }

    public IReadOnlyList<ConversionJob> CurrentQueue
    {
        get
        {
            lock (_queueLock)
            {
                return _queue.ToList().AsReadOnly();
            }
        }
    }

    public QueueProgressReport CurrentProgress => ComputeProgressReport();

    public bool IsProcessing => _isProcessing;

    public bool IsPaused => _isPaused;

    public int MaxTotalConcurrency { get; }

    public event EventHandler<QueueProgressReport>? ProgressChanged;
    public event EventHandler<ConversionJob>? JobStateChanged;

    public ConversionOrchestrator(
        IEnumerable<IConversionEngine>? engines = null,
        int? maxTotalConcurrency = null,
        ILogger<ConversionOrchestrator>? logger = null)
    {
        _logger = logger;

        MaxTotalConcurrency = maxTotalConcurrency.HasValue && maxTotalConcurrency.Value > 0
            ? maxTotalConcurrency.Value
            : Math.Clamp(Environment.ProcessorCount, 2, 8);

        _globalLimiter = new SemaphoreSlim(MaxTotalConcurrency, MaxTotalConcurrency);
        _cpuLimiter = new SemaphoreSlim(MaxTotalConcurrency, MaxTotalConcurrency);
        _memoryLimiter = new SemaphoreSlim(2, 2);
        _processLimiter = new SemaphoreSlim(2, 2);
        _exclusiveLimiter = new SemaphoreSlim(1, 1);

        if (engines != null)
        {
            foreach (var engine in engines)
            {
                RegisterEngine(engine);
            }
        }
    }

    public void RegisterEngine(IConversionEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        lock (_engines)
        {
            if (!_engines.Any(e => e.EngineId.Equals(engine.EngineId, StringComparison.OrdinalIgnoreCase)))
            {
                _engines.Add(engine);
            }
        }
    }

    public IConversionEngine? ResolveEngine(string sourceExtension, string targetExtension)
    {
        var routing = RouteJob(sourceExtension, targetExtension);
        return routing.Status == RoutingStatus.Supported ? routing.Engine : null;
    }

    public RoutingDecision RouteJob(string sourceExtension, string targetExtension)
    {
        if (string.IsNullOrWhiteSpace(sourceExtension) || string.IsNullOrWhiteSpace(targetExtension))
        {
            return new RoutingDecision(RoutingStatus.Invalid, null, "Source or target extension is empty.");
        }

        var src = NormalizeExt(sourceExtension);
        var tgt = NormalizeExt(targetExtension);

        List<IConversionEngine> candidates;
        lock (_engines)
        {
            candidates = _engines
                .Where(e => e.IsAvailable && e.CanConvert(src, tgt))
                .ToList();
        }

        if (candidates.Count == 0)
        {
            return new RoutingDecision(
                RoutingStatus.Unsupported,
                null,
                $"No available registered engine supports conversion from '{src}' to '{tgt}'.");
        }

        if (candidates.Count == 1)
        {
            return new RoutingDecision(RoutingStatus.Supported, candidates[0]);
        }

        // Multiple candidates: resolve preference (prefer native/specialized over generic)
        var preferred = candidates.FirstOrDefault(c => c.ResourceProfile.Affinity != EngineExecutionAffinity.ProcessBound)
                        ?? candidates[0];

        return new RoutingDecision(RoutingStatus.Supported, preferred);
    }

    public IReadOnlyList<string> GetSupportedTargetExtensions(string sourceExtension)
    {
        if (string.IsNullOrWhiteSpace(sourceExtension)) return Array.Empty<string>();
        var src = NormalizeExt(sourceExtension);

        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        lock (_engines)
        {
            foreach (var engine in _engines.Where(e => e.IsAvailable))
            {
                // Test against standard known formats
                string[] candidates = [".png", ".jpg", ".webp", ".bmp", ".tiff", ".pdf", ".txt", ".md", ".html"];
                foreach (var candidate in candidates)
                {
                    if (engine.CanConvert(src, candidate))
                    {
                        targets.Add(candidate);
                    }
                }
            }
        }
        return targets.OrderBy(t => t).ToList();
    }

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        return ResolveEngine(sourceExtension, targetExtension) != null;
    }

    public void Enqueue(ConversionJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        ThrowIfDisposed();

        LogOrch($"Enqueue called for {job.JobId} (state: {job.State}, source: {job.SourceFilePath})");

        lock (_queueLock)
        {
            if (!_queue.Any(j => j.JobId == job.JobId))
            {
                job.TryTransitionTo(ConversionJobState.Pending);
                _queue.Add(job);
                LogOrch($"Job {job.JobId} added to _queue. State={job.State}, TotalQueueCount={_queue.Count}");
            }
            else
            {
                LogOrch($"Job {job.JobId} already exists in _queue.");
            }
        }

        NotifyJobChanged(job);
        NotifyProgressChanged();
        _dispatchSignal.Set();
    }

    public void EnqueueRange(IEnumerable<ConversionJob> jobs)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        ThrowIfDisposed();

        var jobList = jobs.ToList();
        lock (_queueLock)
        {
            foreach (var job in jobList)
            {
                if (!_queue.Any(j => j.JobId == job.JobId))
                {
                    job.TryTransitionTo(ConversionJobState.Pending);
                    _queue.Add(job);
                }
            }
        }

        foreach (var job in jobList)
        {
            NotifyJobChanged(job);
        }

        NotifyProgressChanged();
        _dispatchSignal.Set();
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        LogOrch($"StartAsync called. _isProcessing={_isProcessing}, _isPaused={_isPaused}");

        if (_isProcessing)
        {
            _dispatchSignal.Set();
            return Task.CompletedTask;
        }

        _processingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _isProcessing = true;
        _isPaused = false;

        _schedulerTask = Task.Run(() => SchedulerLoopAsync(_processingCts.Token));
        _dispatchSignal.Set();

        return Task.CompletedTask;
    }

    public Task PauseAsync()
    {
        _isPaused = true;
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        _isPaused = false;
        _dispatchSignal.Set();
        return Task.CompletedTask;
    }

    public void CancelJob(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId)) return;

        ConversionJob? job;
        lock (_queueLock)
        {
            job = _queue.FirstOrDefault(j => j.JobId == jobId);
        }

        if (job != null)
        {
            job.Cancel();
            NotifyJobChanged(job);
            NotifyProgressChanged();
        }
    }

    public void CancelAll()
    {
        List<ConversionJob> snapshot;
        lock (_queueLock)
        {
            snapshot = _queue.ToList();
        }

        foreach (var job in snapshot)
        {
            job.Cancel();
            NotifyJobChanged(job);
        }

        NotifyProgressChanged();
    }

    public void ClearQueue()
    {
        lock (_queueLock)
        {
            // Remove non-running jobs
            _queue.RemoveAll(j => j.State != ConversionJobState.Running && j.State != ConversionJobState.Cancelling);
            if (_queue.Count == 0)
            {
                lock (_reservationLock)
                {
                    _reservedDestinationPaths.Clear();
                }
            }
        }

        NotifyProgressChanged();
    }

    public async Task ExecuteQueueAsync(
        IReadOnlyList<ConversionJob> queue,
        IProgress<QueueProgressReport>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ThrowIfDisposed();

        if (queue.Count == 0)
        {
            progress?.Report(new QueueProgressReport { TotalJobs = 0 });
            return;
        }

        EventHandler<QueueProgressReport>? handler = null;
        if (progress != null)
        {
            handler = (_, report) => progress.Report(report);
            ProgressChanged += handler;
        }

        try
        {
            EnqueueRange(queue);
            await StartAsync(ct);

            // Wait until all enqueued jobs are finished
            while (!ct.IsCancellationRequested)
            {
                var report = ComputeProgressReport();
                if (report.FinishedJobs >= report.TotalJobs)
                {
                    break;
                }

                await Task.Delay(25, ct);
            }
        }
        finally
        {
            if (handler != null)
            {
                ProgressChanged -= handler;
            }
        }
    }

    private async Task SchedulerLoopAsync(CancellationToken ct)
    {
        LogOrch("SchedulerLoopAsync loop entered.");
        while (!ct.IsCancellationRequested && !_isDisposed)
        {
            try
            {
                if (_isPaused)
                {
                    await Task.Delay(50, ct);
                    continue;
                }

                ConversionJob? nextJob = null;
                IConversionEngine? allocatedEngine = null;
                SemaphoreSlim? acquiredAffinityLimiter = null;

                lock (_queueLock)
                {
                    // 1. First validate any Pending jobs
                    var pendingJobs = _queue.Where(j => j.State == ConversionJobState.Pending).ToList();
                    if (pendingJobs.Count > 0)
                    {
                        LogOrch($"SchedulerLoopAsync validating {pendingJobs.Count} pending jobs.");
                    }
                    foreach (var pj in pendingJobs)
                    {
                        ValidateAndPrepareJob(pj);
                    }

                    // 2. Select next Queued job that can acquire its resource limiter (Starvation Prevention)
                    var queuedJobs = _queue.Where(j => j.State == ConversionJobState.Queued).ToList();
                    if (queuedJobs.Count > 0)
                    {
                        LogOrch($"SchedulerLoopAsync found {queuedJobs.Count} queued jobs.");
                    }
                    foreach (var candidate in queuedJobs)
                    {
                        if (candidate.IsCancellationRequested)
                        {
                            candidate.TryTransitionTo(ConversionJobState.Cancelled);
                            NotifyJobChanged(candidate);
                            continue;
                        }

                        var engine = ResolveEngine(candidate.SourceExtension, candidate.TargetExtension);
                        if (engine == null)
                        {
                            LogOrch($"SchedulerLoopAsync: ResolveEngine returned null for {candidate.SourceExtension} -> {candidate.TargetExtension}");
                            candidate.ErrorMessage = "No suitable engine resolved during scheduling.";
                            candidate.TryTransitionTo(ConversionJobState.Failed);
                            NotifyJobChanged(candidate);
                            continue;
                        }

                        var limiter = GetAffinityLimiter(engine.ResourceProfile.Affinity);

                        // Try to acquire slots without blocking the loop
                        if (_globalLimiter.Wait(0))
                        {
                            if (limiter.Wait(0))
                            {
                                nextJob = candidate;
                                allocatedEngine = engine;
                                acquiredAffinityLimiter = limiter;
                                nextJob.TryTransitionTo(ConversionJobState.Running);
                                LogOrch($"Job {nextJob.JobId} acquired limiter and transitioned to Running.");
                                NotifyJobChanged(nextJob);
                                break;
                            }
                            else
                            {
                                LogOrch($"Limiter for {engine.EngineId} busy.");
                                _globalLimiter.Release();
                            }
                        }
                        else
                        {
                            LogOrch($"Global limiter busy.");
                        }
                    }
                }

                if (nextJob != null && allocatedEngine != null && acquiredAffinityLimiter != null)
                {
                    NotifyProgressChanged();
                    var jobToRun = nextJob;
                    var engineToUse = allocatedEngine;
                    var limiterToRelease = acquiredAffinityLimiter;
                    LogOrch($"SchedulerLoopAsync dispatching job {jobToRun.JobId} (source: {jobToRun.SourceFilePath}) to {engineToUse.EngineId}");

                    var jobTask = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessJobAsync(jobToRun, engineToUse, ct);
                        }
                        finally
                        {
                            limiterToRelease.Release();
                            _globalLimiter.Release();
                            _runningJobTasks.TryRemove(jobToRun.JobId, out _);
                            NotifyJobChanged(jobToRun);
                            NotifyProgressChanged();
                            _dispatchSignal.Set();
                        }
                    }, ct);

                    _runningJobTasks[jobToRun.JobId] = jobTask;
                }
                else
                {
                    // Wait for new jobs or job completion
                    await Task.Run(() => _dispatchSignal.WaitOne(50), ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogOrch($"CRITICAL Exception in SchedulerLoopAsync: {ex}");
                try { await Task.Delay(100, ct); } catch { }
            }
        }
    }

    private void ValidateAndPrepareJob(ConversionJob job)
    {
        try
        {
            LogOrch($"ValidateAndPrepareJob started for {job.JobId} (state: {job.State})");
            if (job.IsCancellationRequested)
            {
                job.TryTransitionTo(ConversionJobState.Cancelled);
                NotifyJobChanged(job);
                return;
            }

            job.TryTransitionTo(ConversionJobState.Validating);
            NotifyJobChanged(job);

            // Pre-flight routing check
            var routing = RouteJob(job.SourceExtension, job.TargetExtension);
            if (routing.Status != RoutingStatus.Supported || routing.Engine == null)
            {
                job.ErrorMessage = routing.Reason ?? "Unsupported format conversion.";
                job.TryTransitionTo(ConversionJobState.Failed);
                NotifyJobChanged(job);
                return;
            }

            // Input existence check
            if (string.IsNullOrWhiteSpace(job.SourceFilePath) || !File.Exists(job.SourceFilePath))
            {
                job.ErrorMessage = $"Source file was not found: '{job.SourceFilePath}'";
                job.TryTransitionTo(ConversionJobState.Failed);
                NotifyJobChanged(job);
                return;
            }

            // Determine destination file path if not explicit
            var tgtExt = NormalizeExt(job.TargetExtension);
            var baseDir = !string.IsNullOrWhiteSpace(job.DestinationDirectory)
                ? job.DestinationDirectory
                : (Path.GetDirectoryName(job.SourceFilePath) ?? Environment.CurrentDirectory);

            var baseFileName = Path.GetFileNameWithoutExtension(job.SourceFilePath);
            var requestedTarget = !string.IsNullOrWhiteSpace(job.OutputFilePath)
                ? job.OutputFilePath
                : Path.Combine(baseDir, $"{baseFileName}{tgtExt}");

            bool isSourceIdentical = string.Equals(
                Path.GetFullPath(job.SourceFilePath),
                Path.GetFullPath(requestedTarget),
                StringComparison.OrdinalIgnoreCase);

            var policy = job.Profile.CollisionMode;

            // If requested target is identical to source file:
            // Overwriting source file directly is strictly prohibited to guarantee source immutability.
            if (isSourceIdentical && policy == CollisionPolicy.Overwrite)
            {
                job.ErrorMessage = "Destination output cannot overwrite the source file.";
                job.TryTransitionTo(ConversionJobState.Failed);
                NotifyJobChanged(job);
                return;
            }

            // Centralized Collision Policy Resolution
            string approvedDestination;

            lock (_reservationLock)
            {
                bool exists = isSourceIdentical || File.Exists(requestedTarget) || _reservedDestinationPaths.Contains(requestedTarget);

                if (exists)
                {
                    if (policy == CollisionPolicy.Skip)
                    {
                        job.OutputFilePath = requestedTarget;
                        job.ErrorMessage = "Skipped because destination file already exists.";
                        job.TryTransitionTo(ConversionJobState.Skipped);
                        NotifyJobChanged(job);
                        return;
                    }
                    else if (policy == CollisionPolicy.Overwrite && !isSourceIdentical)
                    {
                        approvedDestination = requestedTarget;
                    }
                    else // AutoRename (default, or when isSourceIdentical)
                    {
                        int index = 1;
                        string candidate;
                        do
                        {
                            candidate = Path.Combine(baseDir, $"{baseFileName} ({index}){tgtExt}");
                            index++;
                        } while (File.Exists(candidate) || _reservedDestinationPaths.Contains(candidate));

                        approvedDestination = candidate;
                    }
                }
                else
                {
                    approvedDestination = requestedTarget;
                }

                _reservedDestinationPaths.Add(approvedDestination);
            }

            job.OutputFilePath = approvedDestination;
            job.TryTransitionTo(ConversionJobState.Queued);
            NotifyJobChanged(job);
            LogOrch($"ValidateAndPrepareJob completed for {job.JobId}. OutputFilePath={approvedDestination}, State={job.State}");
        }
        catch (Exception ex)
        {
            LogOrch($"CRITICAL ValidateAndPrepareJob failed for {job.JobId}: {ex}");
            job.ErrorMessage = $"Validation failure: {ex.Message}";
            job.TryTransitionTo(ConversionJobState.Failed);
            NotifyJobChanged(job);
        }
    }

    private static void LogOrch(string msg)
    {
        try
        {
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] [ConversionOrchestrator] {msg}\n");
        }
        catch { }
    }

    private async Task ProcessJobAsync(ConversionJob job, IConversionEngine engine, CancellationToken orchestratorCt)
    {
        var sw = Stopwatch.StartNew();
        LogOrch($"ProcessJobAsync started for {job.JobId} (target: {job.TargetExtension})");
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(orchestratorCt, job.CancellationToken);
        var token = linkedCts.Token;

        var finalApprovedPath = job.OutputFilePath;
        var tgtExt = NormalizeExt(job.TargetExtension);
        var outDir = Path.GetDirectoryName(finalApprovedPath) ?? Environment.CurrentDirectory;

        // Create staging scratch path
        var stagingPath = Path.Combine(outDir, $".tmp_axora_{job.JobId}_{Guid.NewGuid():N}{tgtExt}");

        try
        {
            if (token.IsCancellationRequested)
            {
                job.TryTransitionTo(ConversionJobState.Cancelled);
                return;
            }

            // Capture source hash for immutability validation
            var sourceHashBefore = await ConversionOutputValidator.ComputeFileSha256Async(job.SourceFilePath, token);

            // Execute engine directed to staging scratch path
            job.OutputFilePath = stagingPath;

            var progressBridge = new Progress<double>(pct =>
            {
                job.ProgressPercentage = pct;
                NotifyProgressChanged();
            });

            ConversionResult result;
            int attempt = 0;
            const int maxRetries = 1; // Bounded retry policy

            while (true)
            {
                attempt++;
                job.RetryAttempt = attempt - 1;

                LogOrch($"Calling engine.ConvertAsync for {job.JobId} attempt {attempt}");
                result = await engine.ConvertAsync(job, progressBridge, token);
                LogOrch($"engine.ConvertAsync returned for {job.JobId} in {sw.ElapsedMilliseconds}ms with status {result.Status}, IsSuccess={result.IsSuccess}");

                if (result.IsSuccess) break;

                if (result.Status == ConversionJobStatus.Cancelled || token.IsCancellationRequested)
                {
                    break;
                }

                // Deterministic transient retry condition
                if (attempt <= maxRetries && IsTransientRetryable(result.ErrorCode))
                {
                    _logger?.LogWarning("Retrying job {JobId} after transient failure {Code}", job.JobId, result.ErrorCode);
                    await Task.Delay(100, token);
                    continue;
                }

                break;
            }

            if (result.Status == ConversionJobStatus.Cancelled || token.IsCancellationRequested)
            {
                job.TryTransitionTo(ConversionJobState.Cancelled);
                return;
            }

            if (!result.IsSuccess)
            {
                job.ErrorMessage = result.ErrorMessage ?? "Conversion failed.";
                job.DiagnosticDetails = result.DiagnosticDetails;
                job.TryTransitionTo(ConversionJobState.Failed);
                return;
            }

            // Post-flight output validation
            var validation = await ConversionOutputValidator.ValidateStagedOutputAsync(stagingPath, tgtExt, token);
            if (!validation.IsValid)
            {
                job.ErrorMessage = validation.ErrorMessage ?? "Staged output failed validation.";
                job.TryTransitionTo(ConversionJobState.Failed);
                return;
            }

            // Source immutability verification
            var sourceHashAfter = await ConversionOutputValidator.ComputeFileSha256Async(job.SourceFilePath, token);
            if (!string.Equals(sourceHashBefore, sourceHashAfter, StringComparison.Ordinal))
            {
                job.ErrorMessage = "Source file was modified during conversion! Source immutability violated.";
                job.TryTransitionTo(ConversionJobState.Failed);
                return;
            }

            // Atomic commit: move staging to final destination
            if (File.Exists(finalApprovedPath))
            {
                File.Delete(finalApprovedPath);
            }

            File.Move(stagingPath, finalApprovedPath);

            var fi = new FileInfo(finalApprovedPath);
            job.OutputFilePath = finalApprovedPath;
            job.OutputSizeBytes = fi.Length;
            job.ElapsedTime = sw.Elapsed;
            job.ProgressPercentage = 100.0;
            job.TryTransitionTo(ConversionJobState.Succeeded);
        }
        catch (OperationCanceledException)
        {
            job.TryTransitionTo(ConversionJobState.Cancelled);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected exception during job execution: {JobId}", job.JobId);
            job.ErrorMessage = ex.Message;
            job.DiagnosticDetails = ex.ToString();
            job.TryTransitionTo(ConversionJobState.Failed);
        }
        finally
        {
            // Always clean up staging file
            try
            {
                if (File.Exists(stagingPath))
                {
                    File.Delete(stagingPath);
                }
            }
            catch { }

            // Unreserve in-flight destination
            lock (_reservationLock)
            {
                _reservedDestinationPaths.Remove(finalApprovedPath);
            }
        }
    }

    private static bool IsTransientRetryable(string? errorCode)
    {
        return errorCode == "ERR_INPUT_READ_FAILED" || errorCode == "ERR_INSUFFICIENT_MEMORY";
    }

    private SemaphoreSlim GetAffinityLimiter(EngineExecutionAffinity affinity) => affinity switch
    {
        EngineExecutionAffinity.CpuBound => _cpuLimiter,
        EngineExecutionAffinity.MemoryBound => _memoryLimiter,
        EngineExecutionAffinity.ProcessBound => _processLimiter,
        EngineExecutionAffinity.ExclusiveSingleThreaded => _exclusiveLimiter,
        _ => _cpuLimiter
    };

    public QueueProgressReport ComputeProgressReport()
    {
        lock (_queueLock)
        {
            int total = _queue.Count;
            int succeeded = _queue.Count(j => j.State == ConversionJobState.Succeeded);
            int failed = _queue.Count(j => j.State == ConversionJobState.Failed);
            int cancelled = _queue.Count(j => j.State == ConversionJobState.Cancelled);
            int skipped = _queue.Count(j => j.State == ConversionJobState.Skipped);
            int running = _queue.Count(j => j.State == ConversionJobState.Running || j.State == ConversionJobState.Cancelling);

            int finished = succeeded + failed + cancelled + skipped;
            double overallPct = total > 0 ? ((double)finished / total) * 100.0 : 0.0;

            long totalBytes = _queue.Where(j => j.State == ConversionJobState.Succeeded).Sum(j => j.OutputSizeBytes);
            var currentRunning = _queue.FirstOrDefault(j => j.State == ConversionJobState.Running);

            return new QueueProgressReport
            {
                TotalJobs = total,
                CompletedJobs = succeeded,
                FailedJobs = failed,
                CancelledJobs = cancelled,
                SkippedJobs = skipped,
                RunningJobs = running,
                OverallProgressPercentage = overallPct,
                TotalBytesProcessed = totalBytes,
                CurrentJobName = currentRunning?.SourceFileName
            };
        }
    }

    private void NotifyJobChanged(ConversionJob job)
    {
        try
        {
            JobStateChanged?.Invoke(this, job);
        }
        catch (Exception ex)
        {
            LogOrch($"NotifyJobChanged exception for {job.JobId}: {ex}");
        }
    }

    private void NotifyProgressChanged()
    {
        try
        {
            var report = ComputeProgressReport();
            ProgressChanged?.Invoke(this, report);
        }
        catch (Exception ex)
        {
            LogOrch($"NotifyProgressChanged exception: {ex}");
        }
    }

    private static string NormalizeExt(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return string.Empty;
        var normalized = ext.Trim().ToLowerInvariant();
        if (!normalized.StartsWith('.')) normalized = "." + normalized;
        if (normalized == ".jpeg") return ".jpg";
        if (normalized == ".tif") return ".tiff";
        if (normalized == ".markdown") return ".md";
        return normalized;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ConversionOrchestrator));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;

        CancelAll();

        try { _processingCts?.Cancel(); } catch { }

        // Await active workers with bounded timeout
        try
        {
            var runningTasks = _runningJobTasks.Values.ToArray();
            if (runningTasks.Length > 0)
            {
                await Task.WhenAny(Task.WhenAll(runningTasks), Task.Delay(3000));
            }
        }
        catch { }

        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_isDisposed) return;
        _isDisposed = true;

        CancelAll();

        try { _processingCts?.Cancel(); } catch { }
        try { _processingCts?.Dispose(); } catch { }
        _processingCts = null;

        try { _dispatchSignal.Set(); } catch { }
        try { _dispatchSignal.Dispose(); } catch { }

        try { _globalLimiter.Dispose(); } catch { }
        try { _cpuLimiter.Dispose(); } catch { }
        try { _memoryLimiter.Dispose(); } catch { }
        try { _processLimiter.Dispose(); } catch { }
        try { _exclusiveLimiter.Dispose(); } catch { }
    }
}
