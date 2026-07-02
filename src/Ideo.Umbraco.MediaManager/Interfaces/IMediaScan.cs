using Ideo.Umbraco.MediaManager.Models;

namespace Ideo.Umbraco.MediaManager.Interfaces;

/// <summary>
/// A single scan the job manager can run. Implementations are registered as scoped
/// <see cref="IMediaScan"/> services and resolved by <see cref="Type"/>.
/// </summary>
public interface IMediaScan
{
    ScanType Type { get; }

    Task<ScanResult> RunAsync(Guid jobId, IProgress<int>? progress, CancellationToken cancellationToken);
}
