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
/// deleted from disk. Every action is audited.
/// </summary>
public sealed class CleanupService(
    IMediaService mediaService,
    MediaFileManager mediaFileManager,
    IAuditService auditService,
    IBackOfficeSecurityAccessor backOfficeSecurityAccessor) : ICleanupService
{
    private const string MediaEntityType = "media";
    private const string MediaFileEntityType = "media-file";

    public Task<CleanupResult> DeleteMediaAsync(IReadOnlyList<Guid> keys, bool dryRun)
    {
        if (dryRun)
        {
            return Task.FromResult(new CleanupResult(keys.Count, []));
        }

        var userId = CurrentUserId();
        var errors = new List<string>();
        var affected = 0;

        foreach (var key in keys)
        {
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

        return Task.FromResult(new CleanupResult(affected, errors));
    }

    public Task<CleanupResult> DeleteFilesAsync(IReadOnlyList<string> paths, bool dryRun)
    {
        var fileSystem = mediaFileManager.FileSystem;

        if (dryRun)
        {
            return Task.FromResult(new CleanupResult(paths.Count(fileSystem.FileExists), []));
        }

        var userId = CurrentUserId();
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
            auditService.Add(
                AuditType.Delete,
                userId,
                UmbracoConstants.System.Root,
                MediaFileEntityType,
                $"Media Manager: deleted orphaned physical file '{path}'.",
                string.Empty);
            affected++;
        }

        return Task.FromResult(new CleanupResult(affected, errors));
    }

#pragma warning disable CS0618 // SuperUserId is a last-resort fallback; the sync audit API takes an int user id.
    private int CurrentUserId()
        => backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Id ?? UmbracoConstants.Security.SuperUserId;
#pragma warning restore CS0618
}
