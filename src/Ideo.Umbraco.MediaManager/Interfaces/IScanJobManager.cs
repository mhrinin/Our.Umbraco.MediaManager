using Ideo.Umbraco.MediaManager.Models;

namespace Ideo.Umbraco.MediaManager.Interfaces;

public interface IScanJobManager
{
    Guid StartScan(ScanType type);

    ScanJobStatus? GetStatus(Guid jobId);

    ScanResult? GetResult(Guid jobId);

    bool Cancel(Guid jobId);
}
