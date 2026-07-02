using Asp.Versioning;
using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Extensions;

namespace Ideo.Umbraco.MediaManager.Controllers;

/// <summary>
/// Deletes what a scan found. Targets are always resolved against the stored scan result — it is
/// the server-side allowlist, so clients can never delete arbitrary media or files. "Select all"
/// is its own endpoint: results are paged, so the client never holds the full id list.
/// </summary>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = Constants.ApiName)]
public class CleanupApiController(
    ICleanupService cleanupService,
    IAuthorizationService authorizationService,
    IScanJobManager scanJobManager) : MediaManagerApiControllerBase
{
    [HttpPost("cleanup/scan/{jobId:guid}")]
    public async Task<IActionResult> DeleteItems(Guid jobId, [FromBody] CleanupItemsRequest request)
        => await DeleteAsync(jobId, request.Ids, request.DryRun);

    [HttpPost("cleanup/scan/{jobId:guid}/all")]
    public async Task<IActionResult> DeleteAll(Guid jobId, [FromBody] CleanupAllRequest request)
        => await DeleteAsync(jobId, null, request.DryRun);

    private async Task<IActionResult> DeleteAsync(Guid jobId, IReadOnlyList<string>? ids, bool dryRun)
    {
        if (scanJobManager.GetResult(jobId) is not { } scanResult)
        {
            return Ok(new CleanupResult(0, ["The scan result is no longer available. Rescan and try again."]));
        }

        ids ??= [.. scanResult.Items.Select(item => item.Id)];

        if (scanResult.Type != ScanType.OrphanedFiles)
        {
            // Section access alone is not enough: honour the user's media start nodes and per-node
            // permissions, exactly like Umbraco's own media delete endpoints.
            var keys = ids
                .Select(id => Guid.TryParse(id, out var key) ? key : Guid.Empty)
                .Where(key => key != Guid.Empty)
                .ToArray();

            var authorization = await authorizationService.AuthorizeResourceAsync(
                User,
                MediaPermissionResource.WithKeys(keys),
                AuthorizationPolicies.MediaPermissionByResource);

            if (!authorization.Succeeded)
            {
                return Forbid();
            }
        }

        return Ok(await cleanupService.DeleteItemsAsync(scanResult, ids, dryRun));
    }
}
