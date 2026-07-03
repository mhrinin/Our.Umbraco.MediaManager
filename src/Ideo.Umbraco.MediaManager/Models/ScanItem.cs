namespace Ideo.Umbraco.MediaManager.Models;

/// <summary>
/// One row in any scan result. For media scans <see cref="Id"/> is the media key; for the
/// orphaned-files scan it is the filesystem-relative path. The id doubles as the deletion target,
/// validated against the owning scan result.
/// </summary>
public sealed record ScanItem(string Id, string Name, string? Path, long SizeBytes)
{
    public static ScanItem ForMedia(Guid key, string name, string? path, long sizeBytes)
        => new(key.ToString(), name, path, sizeBytes);

    public static ScanItem ForFile(string path, long sizeBytes)
        => new(path, path[(path.LastIndexOfAny(['/', '\\']) + 1)..], path, sizeBytes);
}
