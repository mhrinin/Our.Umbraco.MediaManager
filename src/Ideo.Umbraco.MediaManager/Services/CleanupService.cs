using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Ideo.Umbraco.MediaManager.Services;

/// <summary>
/// Umbraco 13 cleanup: the legacy backoffice has no <c>IMediaEditingService</c>/async audit APIs, so
/// this uses the synchronous, int-user-id services (<see cref="IMediaService.MoveToRecycleBin"/> and
/// <see cref="IAuditService.Add"/>). Media nodes go to the Recycle Bin (reversible); orphan files are
/// hard-deleted. Every action is audited.
/// </summary>
public sealed class CleanupService(
    IMediaService mediaService,
    MediaFileManager mediaFileManager,
    IAuditService auditService,
    IBackOfficeSecurityAccessor backOfficeSecurityAccessor) : ICleanupService
{
    private const string MediaEntityType = "media";
    private const string MediaFileEntityType = "media-file";

    public Task<CleanupResult> DeleteItemsAsync(ScanResult scanResult, IReadOnlyList<string> ids, bool dryRun)
        => Task.FromResult(scanResult.Type == ScanType.OrphanedFiles
            ? DeleteFiles(scanResult, ids, dryRun)
            : DeleteMedia(scanResult, ids, dryRun));

    private CleanupResult DeleteMedia(ScanResult scanResult, IReadOnlyList<string> ids, bool dryRun)
    {
        var allowedIds = scanResult.Items.Select(item => item.Id).ToHashSet();
        var userId = CurrentUserId();
        var errors = new List<string>();
        var affected = 0;

        foreach (var id in ids)
        {
            if (!allowedIds.Contains(id) || !Guid.TryParse(id, out var key))
            {
                errors.Add($"Media '{id}' is not part of the scan result and was skipped.");
                continue;
            }

            if (dryRun)
            {
                affected++;
                continue;
            }

            var media = mediaService.GetById(key);
            if (media is null)
            {
                errors.Add($"Media '{key}' was not found.");
                continue;
            }

            var attempt = mediaService.MoveToRecycleBin(media, userId);
            if (!attempt.Success)
            {
                errors.Add($"Media '{media.Name}' could not be moved to the recycle bin.");
                continue;
            }

            auditService.Add(
                AuditType.Delete,
                userId,
                media.Id,
                MediaEntityType,
                $"Media Manager: moved unused media '{media.Name}' to the recycle bin.",
                string.Empty);
            affected++;
        }

        return new CleanupResult(affected, errors);
    }

    private CleanupResult DeleteFiles(ScanResult scanResult, IReadOnlyList<string> ids, bool dryRun)
    {
        var allowedPaths = scanResult.Items.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var userId = CurrentUserId();
        var errors = new List<string>();
        var affected = 0;

        foreach (var path in ids)
        {
            if (!allowedPaths.Contains(path))
            {
                errors.Add($"File '{path}' is not part of the scan result and was skipped.");
                continue;
            }

            try
            {
                var fileSystem = mediaFileManager.FileSystem;
                if (!fileSystem.FileExists(path))
                {
                    errors.Add($"File '{path}' was not found.");
                    continue;
                }

                if (!dryRun)
                {
                    fileSystem.DeleteFile(path);
                    auditService.Add(
                        AuditType.Delete,
                        userId,
                        UmbracoConstants.System.Root,
                        MediaFileEntityType,
                        $"Media Manager: deleted orphaned physical file '{path}'.",
                        string.Empty);
                }

                affected++;
            }
            catch (Exception ex)
            {
                errors.Add($"File '{path}' could not be deleted: {ex.Message}");
            }
        }

        return new CleanupResult(affected, errors);
    }

#pragma warning disable CS0618 // SuperUserId is a last-resort fallback; the sync audit API takes an int user id.
    private int CurrentUserId()
        => backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Id ?? UmbracoConstants.Security.SuperUserId;
#pragma warning restore CS0618
}
