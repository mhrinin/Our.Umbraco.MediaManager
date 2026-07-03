namespace Ideo.Umbraco.MediaManager.Models;

public sealed record ScanResult(
    Guid JobId,
    ScanType Type,
    IReadOnlyList<ScanItem> Items,
    long ReclaimableBytes,
    StorageReport? Report = null,
    ExportInfo? Export = null);
