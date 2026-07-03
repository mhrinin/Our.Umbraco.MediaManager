namespace Ideo.Umbraco.MediaManager.Models;

/// <summary>
/// Result payload of an <see cref="ScanType.Export"/> job. <see cref="DownloadToken"/> is the raw
/// capability token — the frontend composes the download URL from it, so the server never has to
/// know the configured backoffice path. <see cref="Errors"/> is capped (first 20);
/// <see cref="SkippedCount"/> carries the true total of unreadable files.
/// </summary>
public sealed record ExportInfo(
    int FileCount,
    long ZipSizeBytes,
    DateTime CreatedUtc,
    string DownloadToken,
    IReadOnlyList<string> Errors,
    int SkippedCount);
