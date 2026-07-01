using Ideo.Umbraco.MediaManager.Models;

namespace Ideo.Umbraco.MediaManager.Interfaces;

public interface IOrphanedFileScanner
{
    Task<IReadOnlyList<FileCandidate>> ScanAsync(IProgress<int>? progress, CancellationToken cancellationToken);
}
