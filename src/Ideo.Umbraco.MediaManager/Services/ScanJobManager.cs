using System.Collections.Concurrent;
using System.Threading.Channels;
using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ideo.Umbraco.MediaManager.Services;

/// <summary>
/// Runs scans as cancellable background jobs with progress. State is in-memory, so this is
/// single-instance only (documented limitation). Scoped Umbraco services are resolved per job
/// via <see cref="IServiceScopeFactory"/> — never captured on this singleton.
/// </summary>
public sealed class ScanJobManager(
    IServiceScopeFactory scopeFactory,
    ILogger<ScanJobManager> logger) : BackgroundService, IScanJobManager
{
    private readonly Channel<Guid> queue = Channel.CreateUnbounded<Guid>();
    private readonly ConcurrentDictionary<Guid, ScanJobStatus> statuses = new();
    private readonly ConcurrentDictionary<Guid, ScanResult> results = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> cancellations = new();

    public Guid StartScan(ScanType type)
    {
        var jobId = Guid.NewGuid();
        statuses[jobId] = new ScanJobStatus { Id = jobId, Type = type };
        cancellations[jobId] = new CancellationTokenSource();
        queue.Writer.TryWrite(jobId);
        return jobId;
    }

    public ScanJobStatus? GetStatus(Guid jobId) => statuses.GetValueOrDefault(jobId);

    public ScanResult? GetResult(Guid jobId) => results.GetValueOrDefault(jobId);

    public bool Cancel(Guid jobId)
    {
        if (cancellations.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            return true;
        }

        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var jobId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            await RunJobAsync(jobId, stoppingToken);
        }
    }

    private async Task RunJobAsync(Guid jobId, CancellationToken stoppingToken)
    {
        if (!statuses.TryGetValue(jobId, out var status))
        {
            return;
        }

        cancellations.TryGetValue(jobId, out var jobCts);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, jobCts?.Token ?? CancellationToken.None);
        var token = linked.Token;

        status.State = ScanState.Running;
        var progress = new Progress<int>(processed => status.Processed = processed);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var result = status.Type switch
            {
                ScanType.OrphanedMedia => await RunMediaScanAsync(scope, jobId, progress, token),
                ScanType.OrphanedFiles => await RunFileScanAsync(scope, jobId, progress, token),
                _ => throw new NotSupportedException($"Unknown scan type {status.Type}."),
            };

            results[jobId] = result;
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
            logger.LogError(ex, "Media Manager scan {JobId} failed.", jobId);
        }
    }

    private static async Task<ScanResult> RunMediaScanAsync(IServiceScope scope, Guid jobId, IProgress<int> progress, CancellationToken token)
    {
        var scanner = scope.ServiceProvider.GetRequiredService<IOrphanedMediaScanner>();
        var media = await scanner.ScanAsync(progress, token);
        return new ScanResult(jobId, ScanType.OrphanedMedia, media, [], media.Sum(candidate => candidate.SizeBytes));
    }

    private static async Task<ScanResult> RunFileScanAsync(IServiceScope scope, Guid jobId, IProgress<int> progress, CancellationToken token)
    {
        var scanner = scope.ServiceProvider.GetRequiredService<IOrphanedFileScanner>();
        var files = await scanner.ScanAsync(progress, token);
        return new ScanResult(jobId, ScanType.OrphanedFiles, [], files, files.Sum(candidate => candidate.SizeBytes));
    }
}
