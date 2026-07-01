namespace Ideo.Umbraco.MediaManager.Services;

/// <summary>
/// Pure, Umbraco-free decision logic for the scanners, extracted so it can be unit-tested
/// without mocking Umbraco's services.
/// </summary>
public static class MediaScanLogic
{
    public const string CacheDirectoryName = "cache";

    /// <summary>A media node is orphaned when it holds a file (not a folder) and nothing references it.</summary>
    public static bool IsOrphanMedia(int mediaId, string? filePath, ISet<int> referencedMediaIds)
        => !string.IsNullOrEmpty(filePath) && !referencedMediaIds.Contains(mediaId);

    /// <summary>Normalizes a media path for comparison: forward slashes, no leading slash or "media/" prefix, lower-cased.</summary>
    public static string NormalizeMediaPath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
        if (normalized.StartsWith("media/", StringComparison.Ordinal))
        {
            normalized = normalized["media/".Length..];
        }

        return normalized;
    }

    /// <summary>The ImageSharp/media cache directory holds regenerable variants and must never be treated as orphaned.</summary>
    public static bool IsCacheDirectory(string directory)
    {
        var name = directory.TrimEnd('/', '\\');
        var separatorIndex = name.LastIndexOfAny(['/', '\\']);
        if (separatorIndex >= 0)
        {
            name = name[(separatorIndex + 1)..];
        }

        return string.Equals(name, CacheDirectoryName, StringComparison.OrdinalIgnoreCase);
    }
}
