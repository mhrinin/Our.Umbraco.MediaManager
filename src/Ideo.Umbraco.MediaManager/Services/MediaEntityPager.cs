using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;

namespace Ideo.Umbraco.MediaManager.Services;

/// <summary>
/// Shared paging loop for the scanners: yields file-backed media rows as lightweight
/// <see cref="IMediaEntitySlim"/> (no property hydration), skipping folders, and reports
/// progress per page.
/// </summary>
internal static class MediaEntityPager
{
    private const int PageSize = 500;

    public static IEnumerable<IMediaEntitySlim> Page(
        IEntityService entityService,
        bool includeTrashed,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
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
                includeTrashed: includeTrashed);

            foreach (var entity in page)
            {
                if (entity is not IMediaEntitySlim media || string.IsNullOrEmpty(media.MediaPath))
                {
                    continue;
                }

                processed++;
                yield return media;
            }

            progress?.Report(processed);
            pageIndex++;
        }
        while (pageIndex * PageSize < total);
    }
}
