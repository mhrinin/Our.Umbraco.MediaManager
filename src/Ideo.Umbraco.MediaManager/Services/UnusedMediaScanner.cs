using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Services;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Ideo.Umbraco.MediaManager.Services;

/// <summary>
/// Finds media nodes that nothing references. A media node counts as used if either
/// <see cref="IRelationService"/> (fast, published only) or the deep content scan
/// (<see cref="IMediaReferenceCollector"/>, published + draft) reports a reference — the union
/// avoids false positives on draft-heavy or freshly imported sites. File sizes are read only for
/// the unused set.
/// </summary>
public sealed class UnusedMediaScanner(
    IEntityService entityService,
    IRelationService relationService,
    IMediaReferenceCollector referenceCollector,
    MediaFileManager mediaFileManager,
    IOptions<MediaManagerOptions> options) : IMediaScan
{
    public ScanType Type => ScanType.UnusedMedia;

    public Task<ScanResult> RunAsync(Guid jobId, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var referencedIds = GetReferencedMediaIds();
        HashSet<Guid> referencedKeys = options.Value.DeepReferenceScan
            ? referenceCollector.Collect(cancellationToken)
            : [];
        var fileSystem = mediaFileManager.FileSystem;

        var candidates = new List<ScanItem>();
        foreach (var media in MediaEntityPager.Page(entityService, includeTrashed: false, progress, cancellationToken))
        {
            if (!MediaScanLogic.IsUnusedMedia(media.Id, media.MediaPath, media.Trashed, referencedIds))
            {
                continue;
            }

            // Deep-scan safety net: skip media referenced from any content value (published or draft).
            if (referencedKeys.Contains(media.Key))
            {
                continue;
            }

            candidates.Add(ScanItem.ForMedia(
                media.Key,
                media.Name ?? string.Empty,
                media.MediaPath,
                GetSize(fileSystem, media.MediaPath!)));
        }

        return Task.FromResult(new ScanResult(jobId, Type, candidates, candidates.Sum(candidate => candidate.SizeBytes)));
    }

    private HashSet<int> GetReferencedMediaIds()
    {
        var relations = relationService.GetByRelationTypeAlias(UmbracoConstants.Conventions.RelationTypes.RelatedMediaAlias)
            ?? [];

        return relations.Select(relation => relation.ChildId).ToHashSet();
    }

    private static long GetSize(IFileSystem fileSystem, string mediaPath)
    {
        try
        {
            var relativePath = fileSystem.GetRelativePath(mediaPath);
            return fileSystem.FileExists(relativePath) ? fileSystem.GetSize(relativePath) : 0;
        }
        catch
        {
            return 0;
        }
    }
}
