using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Ideo.Umbraco.MediaManager.Services;

public sealed class OrphanedMediaScanner(
    IMediaService mediaService,
    IRelationService relationService) : IOrphanedMediaScanner
{
    private const int PageSize = 100;

    public Task<IReadOnlyList<MediaCandidate>> ScanAsync(IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var referencedIds = GetReferencedMediaIds();

        var candidates = new List<MediaCandidate>();
        long pageIndex = 0;
        long total;
        var processed = 0;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = mediaService.GetPagedDescendants(UmbracoConstants.System.Root, pageIndex, PageSize, out total);

            foreach (var media in page)
            {
                // Only files carry an umbracoFile value; skip folders (handled by a separate feature).
                var path = media.GetValue<string>(UmbracoConstants.Conventions.Media.File);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                processed++;

                if (!MediaScanLogic.IsOrphanMedia(media.Id, path, referencedIds))
                {
                    continue;
                }

                var size = media.GetValue<long?>(UmbracoConstants.Conventions.Media.Bytes) ?? 0;
                candidates.Add(new MediaCandidate(media.Key, media.Name ?? string.Empty, path, size));
            }

            progress?.Report(processed);
            pageIndex++;
        }
        while (pageIndex * PageSize < total);

        return Task.FromResult<IReadOnlyList<MediaCandidate>>(candidates);
    }

    private HashSet<int> GetReferencedMediaIds()
    {
        var relations = relationService.GetByRelationTypeAlias(UmbracoConstants.Conventions.RelationTypes.RelatedMediaAlias)
            ?? [];

        return relations.Select(relation => relation.ChildId).ToHashSet();
    }
}
