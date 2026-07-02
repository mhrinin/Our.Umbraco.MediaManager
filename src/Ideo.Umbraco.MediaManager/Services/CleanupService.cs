using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Ideo.Umbraco.MediaManager.Services;

public sealed class CleanupService(
    IMediaEditingService mediaEditingService,
    MediaFileManager mediaFileManager,
    IAuditService auditService,
    IBackOfficeSecurityAccessor backOfficeSecurityAccessor) : ICleanupService
{
    private const string MediaEntityType = "media";
    private const string MediaFileEntityType = "media-file";

    public async Task<CleanupResult> DeleteItemsAsync(ScanResult scanResult, IReadOnlyList<string> ids, bool dryRun)
        => scanResult.Type == ScanType.OrphanedFiles
            ? await DeleteFilesAsync(scanResult, ids, dryRun)
            : await DeleteMediaAsync(scanResult, ids, dryRun);

    private async Task<CleanupResult> DeleteMediaAsync(ScanResult scanResult, IReadOnlyList<string> ids, bool dryRun)
    {
        var allowedIds = scanResult.Items.Select(item => item.Id).ToHashSet();
        var userKey = CurrentUserKey();
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

            var attempt = await mediaEditingService.MoveToRecycleBinAsync(key, userKey);
            if (!attempt.Success || attempt.Result is null)
            {
                errors.Add($"Media '{key}' could not be moved to the recycle bin ({attempt.Status}).");
                continue;
            }

            await AuditDeleteAsync(
                attempt.Result.Id,
                MediaEntityType,
                $"Media Manager: moved unused media '{attempt.Result.Name}' to the recycle bin.");
            affected++;
        }

        return new CleanupResult(affected, errors);
    }

    private async Task<CleanupResult> DeleteFilesAsync(ScanResult scanResult, IReadOnlyList<string> ids, bool dryRun)
    {
        var allowedPaths = scanResult.Items.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
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
                    await AuditDeleteAsync(
                        UmbracoConstants.System.Root,
                        MediaFileEntityType,
                        $"Media Manager: deleted orphaned physical file '{path}'.");
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

    // Umbraco 17 audits via the async, key-based API; Umbraco 14–16 only expose the synchronous,
    // int-user-id Add. Isolate that single difference here so the callers stay version-agnostic.
    private Task AuditDeleteAsync(int entityId, string entityType, string comment)
    {
#if UMBRACO_17
        return auditService.AddAsync(AuditType.Delete, CurrentUserKey(), entityId, entityType, comment, string.Empty);
#else
        auditService.Add(AuditType.Delete, CurrentUserId(), entityId, entityType, comment, string.Empty);
        return Task.CompletedTask;
#endif
    }

    private Guid CurrentUserKey()
        => backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key ?? UmbracoConstants.Security.SuperUserKey;

#if !UMBRACO_17
#pragma warning disable CS0618 // SuperUserId is obsolete-but-present on Umbraco 16; the sync audit API needs an int user id.
    private int CurrentUserId()
        => backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Id ?? UmbracoConstants.Security.SuperUserId;
#pragma warning restore CS0618
#endif
}
