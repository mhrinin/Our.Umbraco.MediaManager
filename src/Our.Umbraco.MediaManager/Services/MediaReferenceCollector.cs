using Our.Umbraco.MediaManager.Interfaces;
using Umbraco.Cms.Core.Services;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Our.Umbraco.MediaManager.Services;

/// <summary>
/// Collects every media key referenced from content property values — <b>published and draft</b> —
/// by parsing the serialized values. This is the safety net unioned with <see cref="IRelationService"/>:
/// relations only capture references from <i>published</i> content, so on draft-heavy or freshly
/// imported/migrated sites they under-report and healthy media would look unused.
/// </summary>
public sealed class MediaReferenceCollector(IContentService contentService) : IMediaReferenceCollector
{
    private const int PageSize = 100;

    public HashSet<Guid> Collect(CancellationToken cancellationToken)
    {
        var referenced = new HashSet<Guid>();
        long pageIndex = 0;
        long total;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = contentService.GetPagedDescendants(UmbracoConstants.System.Root, pageIndex, PageSize, out total);

            foreach (var content in page)
            {
                foreach (var property in content.Properties)
                {
                    foreach (var propertyValue in property.Values)
                    {
                        AddKeys(propertyValue.EditedValue?.ToString(), referenced);
                        AddKeys(propertyValue.PublishedValue?.ToString(), referenced);
                    }
                }
            }

            pageIndex++;
        }
        while (pageIndex * PageSize < total);

        return referenced;
    }

    private static void AddKeys(string? value, HashSet<Guid> referenced)
    {
        foreach (var key in MediaScanLogic.ExtractMediaKeys(value))
        {
            referenced.Add(key);
        }
    }
}
