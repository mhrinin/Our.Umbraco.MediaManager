using Ideo.Umbraco.MediaManager.Models;

namespace Ideo.Umbraco.MediaManager.Interfaces;

public interface IOrphanedMediaScanner
{
    Task<IReadOnlyList<MediaCandidate>> ScanAsync(IProgress<int>? progress, CancellationToken cancellationToken);
}
