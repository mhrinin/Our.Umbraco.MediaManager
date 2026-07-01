using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;

namespace Ideo.Umbraco.MediaManager.Services;

/// <summary>
/// Finds media nodes whose physical file is missing on disk (the reverse of an orphaned file).
/// Pages lightweight <see cref="IMediaEntitySlim"/> rows (no property hydration) and excludes the
/// recycle bin.
/// </summary>
public sealed class BrokenMediaScanner(
    IEntityService entityService,
    MediaFileManager mediaFileManager) : IBrokenMediaScanner
{
    private const int PageSize = 500;

    public Task<IReadOnlyList<MediaCandidate>> ScanAsync(IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var fileSystem = mediaFileManager.FileSystem;
        var candidates = new List<MediaCandidate>();
        long pageIndex = 0;
        long total;
        var processed = 0;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = entityService.GetPagedDescendants(
                UmbracoObjectTypes.Media,
                pageIndex,
                PageSize,
                out total,
                filter: null,
                ordering: null,
                includeTrashed: false);

            foreach (var entity in page)
            {
                if (entity is not IMediaEntitySlim media || string.IsNullOrEmpty(media.MediaPath))
                {
                    continue;
                }

                processed++;

                var relativePath = ToRelativePath(fileSystem, media.MediaPath);
                // If we cannot resolve the path we cannot prove it is missing, so do not flag it.
                if (relativePath is null || fileSystem.FileExists(relativePath))
                {
                    continue;
                }

                candidates.Add(new MediaCandidate(media.Key, media.Name ?? string.Empty, media.MediaPath, 0));
            }

            progress?.Report(processed);
            pageIndex++;
        }
        while (pageIndex * PageSize < total);

        return Task.FromResult<IReadOnlyList<MediaCandidate>>(candidates);
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
