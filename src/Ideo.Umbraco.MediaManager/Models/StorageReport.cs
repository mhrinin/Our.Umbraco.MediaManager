namespace Ideo.Umbraco.MediaManager.Models;

public sealed record StorageReport(
    long TotalBytes,
    int TotalCount,
    IReadOnlyList<StorageTypeBreakdown> ByType,
    IReadOnlyList<ScanItem> Largest);
