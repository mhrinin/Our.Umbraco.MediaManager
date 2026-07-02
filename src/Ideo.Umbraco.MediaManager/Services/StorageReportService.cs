using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Ideo.Umbraco.MediaManager.Services;

/// <summary>
/// Builds a storage report primarily from sizes stored in Umbraco's database (<c>umbracoBytes</c>).
/// Reading the DB keeps the report provider-agnostic and avoids per-file calls to remote storage
/// (Azure Blob / S3); a filesystem stat is only used as a fallback for media that have no stored
/// size (rare on healthy sites). Runs as a background job like the scanners, so large libraries
/// never block an HTTP request thread.
/// </summary>
public sealed class StorageReportService(
    IMediaService mediaService,
    MediaFileManager mediaFileManager,
    MediaUrlGeneratorCollection mediaUrlGenerators) : IMediaScan
{
    private const int PageSize = 200;
    private const int TopCount = 20;

    public ScanType Type => ScanType.StorageReport;

    public async Task<ScanResult> RunAsync(Guid jobId, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var report = await GenerateAsync(progress, cancellationToken);
        return new ScanResult(jobId, Type, [], [], 0, report);
    }

    private Task<StorageReport> GenerateAsync(IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var fileSystem = mediaFileManager.FileSystem;
        var byType = new Dictionary<string, TypeAggregate>();
        var largest = new PriorityQueue<MediaCandidate, long>(); // min-heap: smallest size at the front
        long totalBytes = 0;
        var totalCount = 0;
        long pageIndex = 0;
        long total;
        var processed = 0;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = mediaService.GetPagedDescendants(UmbracoConstants.System.Root, pageIndex, PageSize, out total);

            foreach (var media in page)
            {
                processed++;

                if (media.Trashed)
                {
                    // The report covers the active library only; recycle-bin items are excluded.
                    continue;
                }

                var path = ResolvePath(media);
                if (string.IsNullOrEmpty(path))
                {
                    // Folders and file-less media carry no file.
                    continue;
                }

                var bytes = media.GetValue<long?>(UmbracoConstants.Conventions.Media.Bytes) ?? 0;
                if (bytes <= 0)
                {
                    bytes = FileSize(fileSystem, path);
                }
                if (bytes <= 0)
                {
                    continue;
                }

                totalBytes += bytes;
                totalCount++;

                var alias = media.ContentType.Alias;
                if (!byType.TryGetValue(alias, out var aggregate))
                {
                    aggregate = new TypeAggregate(media.ContentType.Icon ?? "icon-document");
                    byType[alias] = aggregate;
                }
                aggregate.Count++;
                aggregate.Bytes += bytes;

                TrackLargest(largest, media.Key, media.Name ?? string.Empty, path, bytes);
            }

            progress?.Report(processed);
            pageIndex++;
        }
        while (pageIndex * PageSize < total);

        var byTypeList = byType
            .Select(entry => new StorageTypeBreakdown(entry.Key, entry.Value.Icon, entry.Value.Count, entry.Value.Bytes))
            .OrderByDescending(breakdown => breakdown.Bytes)
            .ToList();

        var largestList = largest.UnorderedItems
            .Select(item => item.Element)
            .OrderByDescending(candidate => candidate.SizeBytes)
            .ToList();

        return Task.FromResult(new StorageReport(totalBytes, totalCount, byTypeList, largestList));
    }

    private string? ResolvePath(IContentBase media)
    {
        foreach (var property in media.Properties)
        {
            if (mediaUrlGenerators.TryGetMediaPath(property.PropertyType.PropertyEditorAlias, property.GetValue(), out var mediaPath)
                && !string.IsNullOrEmpty(mediaPath))
            {
                return mediaPath;
            }
        }

        return null;
    }

    private static long FileSize(IFileSystem fileSystem, string mediaPath)
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

    private static void TrackLargest(PriorityQueue<MediaCandidate, long> largest, Guid key, string name, string? path, long bytes)
    {
        if (largest.Count < TopCount)
        {
            largest.Enqueue(new MediaCandidate(key, name, path, bytes), bytes);
        }
        else if (largest.TryPeek(out _, out var smallest) && bytes > smallest)
        {
            largest.Dequeue();
            largest.Enqueue(new MediaCandidate(key, name, path, bytes), bytes);
        }
    }

    private sealed class TypeAggregate(string icon)
    {
        public string Icon { get; } = icon;

        public int Count { get; set; }

        public long Bytes { get; set; }
    }
}
