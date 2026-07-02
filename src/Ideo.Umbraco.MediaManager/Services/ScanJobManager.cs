using System.Collections.Concurrent;
using System.Threading.Channels;
using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ideo.Umbraco.MediaManager.Services;

/// <summary>
/// Runs scans as cancellable background jobs with progress. Exactly one job per scan type is
/// retained (the latest), so memory stays bounded no matter how often dashboards are opened, and
/// starting a scan whose type is already queued/running joins that job instead of stacking a
/// duplicate. State is in-memory, so this is single-instance only (documented limitation). Scoped
/// Umbraco services are resolved per job via <see cref="IServiceScopeFactory"/> — never captured
/// on this singleton.
/// </summary>
public sealed class ScanJobManager(
    IServiceScopeFactory scopeFactory,
    ILogger<ScanJobManager> logger) : BackgroundService, IScanJobManager
{
    private readonly Channel<ScanType> queue = Channel.CreateUnbounded<ScanType>();
    private readonly ConcurrentDictionary<ScanType, ScanJob> jobs = new();

    public Guid StartScan(ScanType type)
    {
        while (true)
        {
            if (jobs.TryGetValue(type, out var existing) && existing.Status.State is ScanState.Queued or ScanState.Running)
            {
                return existing.Status.Id;
            }

            var job = new ScanJob
            {
                Status = new ScanJobStatus { Id = Guid.NewGuid(), Type = type },
                Cancellation = new CancellationTokenSource(),
            };

            var stored = existing is null ? jobs.TryAdd(type, job) : jobs.TryUpdate(type, job, existing);
            if (stored)
            {
                queue.Writer.TryWrite(type);
                return job.Status.Id;
            }

            job.Cancellation.Dispose();
        }
    }

    public ScanJobStatus? GetStatus(Guid jobId) => Find(jobId)?.Status;

    public ScanResult? GetResult(Guid jobId) => Find(jobId)?.Result;

    public bool Cancel(Guid jobId)
    {
        var job = Find(jobId);
        if (job is null || job.Status.State is not (ScanState.Queued or ScanState.Running))
        {
            return false;
        }

        try
        {
            job.Cancellation.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            // The job reached a terminal state (and disposed its source) between the check and the cancel.
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var type in queue.Reader.ReadAllAsync(stoppingToken))
        {
            if (jobs.TryGetValue(type, out var job) && job.Status.State == ScanState.Queued)
            {
                await RunJobAsync(job, stoppingToken);
            }
        }
    }

    private async Task RunJobAsync(ScanJob job, CancellationToken stoppingToken)
    {
        var status = job.Status;

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, job.Cancellation.Token);
            linked.Token.ThrowIfCancellationRequested();
            status.State = ScanState.Running;

            using var scope = scopeFactory.CreateScope();
            var scan = scope.ServiceProvider.GetServices<IMediaScan>().FirstOrDefault(s => s.Type == status.Type)
                ?? throw new NotSupportedException($"No scan registered for type {status.Type}.");

            var result = await scan.RunAsync(status.Id, new StatusProgress(status), linked.Token);

            job.Result = result;
            status.FoundCount = result.Media.Count + result.Files.Count;
            status.State = ScanState.Completed;
        }
        catch (OperationCanceledException)
        {
            status.State = ScanState.Cancelled;
        }
        catch (Exception ex)
        {
            status.State = ScanState.Failed;
            status.Error = ex.Message;
            logger.LogError(ex, "Media Manager scan {JobId} failed.", status.Id);
        }
        finally
        {
            job.Cancellation.Dispose();
        }
    }

    private ScanJob? Find(Guid jobId) => jobs.Values.FirstOrDefault(job => job.Status.Id == jobId);

    /// <summary>
    /// Writes progress straight onto the status from the job thread, keeping updates ordered
    /// (<see cref="Progress{T}"/> would post to the thread pool and could regress between polls).
    /// </summary>
    private sealed class StatusProgress(ScanJobStatus status) : IProgress<int>
    {
        public void Report(int value) => status.Processed = value;
    }

    private sealed class ScanJob
    {
        public required ScanJobStatus Status { get; init; }

        public required CancellationTokenSource Cancellation { get; init; }

        public ScanResult? Result { get; set; }
    }
}
