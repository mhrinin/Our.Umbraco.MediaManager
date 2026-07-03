using Ideo.Umbraco.MediaManager.Models;

namespace Ideo.Umbraco.MediaManager.Interfaces;

public interface IScanJobManager
{
    Guid StartScan(ScanType type);

    ScanJobStatus? GetStatus(Guid jobId);

    ScanResult? GetResult(Guid jobId);

    /// <summary>The retained (latest) result for a scan type, or null if it never completed.</summary>
    ScanResult? GetLatestResult(ScanType type);

    bool Cancel(Guid jobId);
}
