using Ideo.Umbraco.MediaManager.Models;

namespace Ideo.Umbraco.MediaManager.Interfaces;

public interface ICleanupService
{
    /// <summary>
    /// Deletes scan-result items by id: media scans move nodes to the recycle bin (reversible),
    /// the orphaned-files scan hard-deletes physical files. Every id is validated against
    /// <paramref name="scanResult"/> — the scan result is the allowlist, so arbitrary targets are
    /// rejected. Dry-run mutates nothing.
    /// </summary>
    Task<CleanupResult> DeleteItemsAsync(ScanResult scanResult, IReadOnlyList<string> ids, bool dryRun);
}
