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

    public async Task<CleanupResult> DeleteMediaAsync(IReadOnlyList<Guid> keys, bool dryRun)
    {
        if (dryRun)
        {
            return new CleanupResult(keys.Count, []);
        }

        var userKey = CurrentUserKey();
        var errors = new List<string>();
        var affected = 0;

        foreach (var key in keys)
        {
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

    public async Task<CleanupResult> DeleteFilesAsync(IReadOnlyList<string> paths, bool dryRun)
    {
        var fileSystem = mediaFileManager.FileSystem;

        if (dryRun)
        {
            return new CleanupResult(paths.Count(fileSystem.FileExists), []);
        }

        var errors = new List<string>();
        var affected = 0;

        foreach (var path in paths)
        {
            if (!fileSystem.FileExists(path))
            {
                errors.Add($"File '{path}' was not found.");
                continue;
            }

            fileSystem.DeleteFile(path);
            await AuditDeleteAsync(
                UmbracoConstants.System.Root,
                MediaFileEntityType,
                $"Media Manager: deleted orphaned physical file '{path}'.");
            affected++;
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
