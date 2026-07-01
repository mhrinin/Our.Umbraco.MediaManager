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

            await auditService.AddAsync(
                AuditType.Delete,
                userKey,
                attempt.Result.Id,
                MediaEntityType,
                $"Media Manager: moved orphaned media '{attempt.Result.Name}' to the recycle bin.",
                string.Empty);
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

        var userKey = CurrentUserKey();
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
            await auditService.AddAsync(
                AuditType.Delete,
                userKey,
                UmbracoConstants.System.Root,
                MediaFileEntityType,
                $"Media Manager: deleted orphaned physical file '{path}'.",
                string.Empty);
            affected++;
        }

        return new CleanupResult(affected, errors);
    }

    private Guid CurrentUserKey()
        => backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key ?? UmbracoConstants.Security.SuperUserKey;
}
