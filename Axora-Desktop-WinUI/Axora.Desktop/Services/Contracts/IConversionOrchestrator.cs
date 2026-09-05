using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// High-level coordinator that manages engine resolution, format compatibility discovery,
/// and batch queue execution with resource-profile-aware concurrency.
/// </summary>
public interface IConversionOrchestrator : IAsyncDisposable, IDisposable
{
    /// <summary>List of all registered conversion engine adapters.</summary>
    IReadOnlyList<IConversionEngine> RegisteredEngines { get; }

    /// <summary>Resolves the most appropriate available conversion engine for the specified format pair.</summary>
    IConversionEngine? ResolveEngine(string sourceExtension, string targetExtension);

    /// <summary>Discovers all target extensions supported for a given source format.</summary>
    IReadOnlyList<string> GetSupportedTargetExtensions(string sourceExtension);

    /// <summary>Returns true if any registered, available engine can convert from source to target.</summary>
    bool CanConvert(string sourceExtension, string targetExtension);

    /// <summary>Current active snapshot of jobs registered in the orchestrator.</summary>
    IReadOnlyList<ConversionJob> CurrentQueue { get; }

    /// <summary>Current aggregated progress report.</summary>
    QueueProgressReport CurrentProgress { get; }

    /// <summary>Indicates whether the orchestrator is actively processing jobs.</summary>
    bool IsProcessing { get; }

    /// <summary>Indicates whether the orchestrator queue is paused.</summary>
    bool IsPaused { get; }

    /// <summary>Raised whenever aggregate queue progress changes.</summary>
    event EventHandler<QueueProgressReport>? ProgressChanged;

    /// <summary>Raised whenever an individual job's state or status transitions.</summary>
    event EventHandler<ConversionJob>? JobStateChanged;

    /// <summary>Enqueues a single conversion job.</summary>
    void Enqueue(ConversionJob job);

    /// <summary>Enqueues a collection of conversion jobs.</summary>
    void EnqueueRange(IEnumerable<ConversionJob> jobs);

    /// <summary>Starts processing queued jobs asynchronously.</summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>Pauses dequeuing new jobs; currently running jobs are allowed to complete.</summary>
    Task PauseAsync();

    /// <summary>Resumes processing pending jobs in the queue.</summary>
    Task ResumeAsync();

    /// <summary>Cancels a specific job by its unique JobId.</summary>
    void CancelJob(string jobId);

    /// <summary>Cancels all pending and actively running jobs.</summary>
    void CancelAll();

    /// <summary>Clears non-running jobs from the queue.</summary>
    void ClearQueue();

    /// <summary>
    /// Executes a batch queue of conversion jobs with bounded concurrency and progress reporting.
    /// </summary>
    Task ExecuteQueueAsync(
        IReadOnlyList<ConversionJob> queue,
        IProgress<QueueProgressReport>? progress = null,
        CancellationToken ct = default);
}
