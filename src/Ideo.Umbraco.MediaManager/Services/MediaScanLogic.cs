using System.Text.RegularExpressions;
using Ideo.Umbraco.MediaManager.Models;

namespace Ideo.Umbraco.MediaManager.Services;

/// <summary>
/// Pure, Umbraco-free decision logic for the scanners, extracted so it can be unit-tested
/// without mocking Umbraco's services.
/// </summary>
public static partial class MediaScanLogic
{
    public const string CacheDirectoryName = "cache";

    [GeneratedRegex("umb://media/([0-9a-fA-F]{32})", RegexOptions.IgnoreCase)]
    private static partial Regex MediaUdiRegex();

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();

    /// <summary>
    /// Extracts media keys referenced in a serialized property value — both <c>umb://media/{guid}</c>
    /// UDIs and bare GUIDs (rich text, block editors, media pickers). Over-matching non-media GUIDs is
    /// harmless: they simply will not match any media key, and the union only ever protects media from
    /// being flagged, never the reverse.
    /// </summary>
    public static IEnumerable<Guid> ExtractMediaKeys(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (Match match in MediaUdiRegex().Matches(value))
        {
            if (Guid.TryParseExact(match.Groups[1].Value, "N", out var key))
            {
                yield return key;
            }
        }

        foreach (Match match in GuidRegex().Matches(value))
        {
            if (Guid.TryParse(match.Value, out var key))
            {
                yield return key;
            }
        }
    }

    /// <summary>
    /// A media node is orphaned when it holds a file (not a folder), is not already in the recycle
    /// bin, and nothing references it.
    /// </summary>
    public static bool IsUnusedMedia(int mediaId, string? filePath, bool trashed, ISet<int> referencedMediaIds)
        => !trashed && !string.IsNullOrEmpty(filePath) && !referencedMediaIds.Contains(mediaId);

    /// <summary>
    /// Normalizes a filesystem-relative path into a zip entry name: forward slashes, no leading
    /// slash — so the extracted tree drops straight into a media root or bucket.
    /// </summary>
    public static string ToZipEntryName(string relativePath)
        => relativePath.Replace('\\', '/').TrimStart('/');

    /// <summary>
    /// Total disk space recovered by cleaning everything up. An item can be both unused AND a
    /// duplicate copy; its size is counted once.
    /// </summary>
    public static long ComputeReclaimableBytes(ScanResult? unused, ScanResult? orphaned, ScanResult? duplicates)
    {
        var unusedIds = (unused?.Items ?? []).Select(item => item.Id).ToHashSet();
        var overlapBytes = (duplicates?.Items ?? [])
            .Where(item => unusedIds.Contains(item.Id))
            .Sum(item => item.SizeBytes);

        return (unused?.ReclaimableBytes ?? 0)
            + (orphaned?.ReclaimableBytes ?? 0)
            + (duplicates?.ReclaimableBytes ?? 0)
            - overlapBytes;
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
