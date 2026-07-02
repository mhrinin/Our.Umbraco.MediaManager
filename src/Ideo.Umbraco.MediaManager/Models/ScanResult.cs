namespace Ideo.Umbraco.MediaManager.Models;

public sealed record ScanResult(
    Guid JobId,
    ScanType Type,
    IReadOnlyList<MediaCandidate> Media,
    IReadOnlyList<FileCandidate> Files,
    long ReclaimableBytes,
    StorageReport? Report = null);
