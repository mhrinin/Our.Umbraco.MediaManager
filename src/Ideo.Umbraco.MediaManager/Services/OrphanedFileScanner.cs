using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Services;

namespace Ideo.Umbraco.MediaManager.Services;

/// <summary>
/// Finds physical files on the media filesystem that no media node owns. Owned paths include the
/// recycle bin (whose files are still on disk). The filesystem itself resolves stored media URLs
/// (e.g. "/media/xyz/f.jpg" — or a custom media root like "/assets/…") to its own relative paths,
/// so owned and walked paths always agree regardless of the configured UmbracoMediaPath.
/// </summary>
public sealed class OrphanedFileScanner(
    MediaFileManager mediaFileManager,
    IEntityService entityService) : IMediaScan
{
    public ScanType Type => ScanType.OrphanedFiles;

    public Task<ScanResult> RunAsync(Guid jobId, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var fileSystem = mediaFileManager.FileSystem;

        var ownedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var media in MediaEntityPager.Page(entityService, includeTrashed: true, progress, cancellationToken))
        {
            if (ToRelativePath(fileSystem, media.MediaPath!) is { } relativePath)
            {
                ownedPaths.Add(NormalizeSeparators(relativePath));
            }
        }

        var candidates = new List<ScanItem>();
        foreach (var relativePath in MediaFileWalker.Walk(fileSystem, cancellationToken))
        {
            if (ownedPaths.Contains(NormalizeSeparators(relativePath)))
            {
                continue;
            }

            candidates.Add(ScanItem.ForFile(relativePath, fileSystem.GetSize(relativePath)));
        }

        return Task.FromResult(new ScanResult(jobId, Type, candidates, candidates.Sum(candidate => candidate.SizeBytes)));
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

    private static string NormalizeSeparators(string path) => path.Replace('\\', '/');
}
