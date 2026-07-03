using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Services;

namespace Ideo.Umbraco.MediaManager.Services;

/// <summary>
/// Finds media nodes whose physical file is missing on disk (the reverse of an orphaned file).
/// Excludes the recycle bin. Files are already missing, so nothing is reclaimed by cleaning these up.
/// </summary>
public sealed class BrokenMediaScanner(
    IEntityService entityService,
    MediaFileManager mediaFileManager) : IMediaScan
{
    public ScanType Type => ScanType.BrokenMedia;

    public Task<ScanResult> RunAsync(Guid jobId, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var fileSystem = mediaFileManager.FileSystem;
        var candidates = new List<ScanItem>();

        foreach (var media in MediaEntityPager.Page(entityService, includeTrashed: false, progress, cancellationToken))
        {
            var relativePath = ToRelativePath(fileSystem, media.MediaPath!);
            // If we cannot resolve the path we cannot prove it is missing, so do not flag it.
            if (relativePath is null || fileSystem.FileExists(relativePath))
            {
                continue;
            }

            candidates.Add(ScanItem.ForMedia(media.Key, media.Name ?? string.Empty, media.MediaPath, 0));
        }

        return Task.FromResult(new ScanResult(jobId, Type, candidates, 0));
    }

    private static string? ToRelativePath(IFileSystem fileSystem, string mediaPath)
    {
        try
        {
            return fileSystem.GetRelativePath(mediaPath);
        }
        catch
        {
            return null;
        }
    }
}
