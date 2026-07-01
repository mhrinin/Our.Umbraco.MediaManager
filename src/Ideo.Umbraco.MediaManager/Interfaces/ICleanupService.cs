using Ideo.Umbraco.MediaManager.Models;

namespace Ideo.Umbraco.MediaManager.Interfaces;

public interface ICleanupService
{
    /// <summary>Moves orphaned media nodes to the recycle bin (reversible). Dry-run mutates nothing.</summary>
    Task<CleanupResult> DeleteMediaAsync(IReadOnlyList<Guid> keys, bool dryRun);

    /// <summary>Hard-deletes orphaned physical files. Dry-run mutates nothing.</summary>
    Task<CleanupResult> DeleteFilesAsync(IReadOnlyList<string> paths, bool dryRun);
}
