using Umbraco.Cms.Core.IO;

namespace Ideo.Umbraco.MediaManager.Services;

/// <summary>
/// Recursively yields every file on the media filesystem, skipping the regenerable image cache
/// directory. Shared by the orphaned-file scan and the media export.
/// </summary>
internal static class MediaFileWalker
{
    public static IEnumerable<string> Walk(IFileSystem fileSystem, CancellationToken cancellationToken)
        => Walk(fileSystem, string.Empty, cancellationToken);

    private static IEnumerable<string> Walk(IFileSystem fileSystem, string path, CancellationToken cancellationToken)
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

            foreach (var file in Walk(fileSystem, directory, cancellationToken))
            {
                yield return file;
            }
        }
    }
}
