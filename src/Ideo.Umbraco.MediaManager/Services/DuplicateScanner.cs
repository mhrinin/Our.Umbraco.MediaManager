using System.Security.Cryptography;
using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Services;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Ideo.Umbraco.MediaManager.Services;

/// <summary>
/// Finds byte-identical media files. Files are grouped by size first — a cheap pre-filter, since only
/// equal-sized files can be identical — and the SHA-256 hash is computed only within those groups, so
/// most files are never read. Within each set of identical files a referenced node is kept in
/// preference to an unreferenced one (falling back to the oldest), and the rest are reported as
/// redundant copies.
/// </summary>
public sealed class DuplicateScanner(
    IEntityService entityService,
    IRelationService relationService,
    MediaFileManager mediaFileManager) : IMediaScan
{
    public ScanType Type => ScanType.Duplicates;

    public Task<ScanResult> RunAsync(Guid jobId, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var referencedIds = GetReferencedMediaIds();
        var fileSystem = mediaFileManager.FileSystem;
        var files = new List<MediaFile>();

        foreach (var media in MediaEntityPager.Page(entityService, includeTrashed: false, progress, cancellationToken))
        {
            var relativePath = ToRelativePath(fileSystem, media.MediaPath!);
            if (relativePath is null || !fileSystem.FileExists(relativePath))
            {
                continue;
            }

            files.Add(new MediaFile(media.Id, media.Key, media.Name ?? string.Empty, media.MediaPath!, relativePath, fileSystem.GetSize(relativePath)));
        }

        var duplicates = new List<MediaCandidate>();

        foreach (var sizeGroup in files.GroupBy(file => file.Size).Where(group => group.Count() > 1))
        {
            var byHash = new Dictionary<string, List<MediaFile>>();
            foreach (var file in sizeGroup)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var hash = ComputeHash(fileSystem, file.RelativePath);
                if (hash is null)
                {
                    continue;
                }

                if (!byHash.TryGetValue(hash, out var identical))
                {
                    identical = [];
                    byHash[hash] = identical;
                }
                identical.Add(file);
            }

            foreach (var identical in byHash.Values.Where(group => group.Count > 1))
            {
                // Two nodes can point at the same physical file (programmatic imports): deleting
                // the "redundant" node and emptying the bin would destroy the kept node's file too,
                // so such groups are never flagged.
                if (identical.Select(file => file.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() < identical.Count)
                {
                    continue;
                }

                // Keep a referenced copy when there is one (deleting it would break content even
                // though an identical file exists elsewhere), otherwise the oldest node.
                foreach (var redundant in identical
                    .OrderByDescending(file => referencedIds.Contains(file.Id))
                    .ThenBy(file => file.Id)
                    .Skip(1))
                {
                    duplicates.Add(new MediaCandidate(redundant.Key, redundant.Name, redundant.MediaPath, redundant.Size));
                }
            }
        }

        return Task.FromResult(new ScanResult(jobId, Type, duplicates, [], duplicates.Sum(candidate => candidate.SizeBytes)));
    }

    private HashSet<int> GetReferencedMediaIds()
    {
        var relations = relationService.GetByRelationTypeAlias(UmbracoConstants.Conventions.RelationTypes.RelatedMediaAlias)
            ?? [];

        return relations.Select(relation => relation.ChildId).ToHashSet();
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

    private static string? ComputeHash(IFileSystem fileSystem, string relativePath)
    {
        try
        {
            using var stream = fileSystem.OpenFile(relativePath);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch
        {
            return null;
        }
    }

    private readonly record struct MediaFile(int Id, Guid Key, string Name, string MediaPath, string RelativePath, long Size);
}
