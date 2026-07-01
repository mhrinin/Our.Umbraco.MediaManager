using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Ideo.Umbraco.MediaManager.Services;

public sealed class OrphanedFileScanner(
    MediaFileManager mediaFileManager,
    MediaUrlGeneratorCollection mediaUrlGenerators,
    IMediaService mediaService) : IOrphanedFileScanner
{
    private const int PageSize = 100;

    public Task<IReadOnlyList<FileCandidate>> ScanAsync(IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var ownedPaths = CollectOwnedPaths(progress, cancellationToken);
        var fileSystem = mediaFileManager.FileSystem;

        var candidates = new List<FileCandidate>();
        foreach (var relativePath in WalkFiles(fileSystem, string.Empty, cancellationToken))
        {
            if (ownedPaths.Contains(MediaScanLogic.NormalizeMediaPath(relativePath)))
            {
                continue;
            }

            candidates.Add(new FileCandidate(relativePath, fileSystem.GetSize(relativePath)));
        }

        return Task.FromResult<IReadOnlyList<FileCandidate>>(candidates);
    }

    private HashSet<string> CollectOwnedPaths(IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var owned = new HashSet<string>();
        long pageIndex = 0;
        long total;
        var processed = 0;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = mediaService.GetPagedDescendants(UmbracoConstants.System.Root, pageIndex, PageSize, out total);

            foreach (var media in page)
            {
                AddOwnedPaths(media, owned);
                processed++;
            }

            progress?.Report(processed);
            pageIndex++;
        }
        while (pageIndex * PageSize < total);

        return owned;
    }

    private void AddOwnedPaths(IContentBase media, HashSet<string> owned)
    {
        foreach (var property in media.Properties)
        {
            var value = property.GetValue();
            if (mediaUrlGenerators.TryGetMediaPath(property.PropertyType.PropertyEditorAlias, value, out var mediaPath)
                && !string.IsNullOrEmpty(mediaPath))
            {
                owned.Add(MediaScanLogic.NormalizeMediaPath(mediaPath));
            }
        }
    }

    private static IEnumerable<string> WalkFiles(IFileSystem fileSystem, string path, CancellationToken cancellationToken)
    {
        foreach (var file in fileSystem.GetFiles(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return file;
        }

        foreach (var directory in fileSystem.GetDirectories(path))
        {
            if (MediaScanLogic.IsCacheDirectory(directory))
            {
                continue;
            }

            foreach (var file in WalkFiles(fileSystem, directory, cancellationToken))
            {
                yield return file;
            }
        }
    }
}
