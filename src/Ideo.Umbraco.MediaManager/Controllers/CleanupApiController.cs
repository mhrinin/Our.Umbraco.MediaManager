using Asp.Versioning;
using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Authorization;

namespace Ideo.Umbraco.MediaManager.Controllers;

[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = Constants.ApiName)]
public class CleanupApiController(ICleanupService cleanupService) : MediaManagerApiControllerBase
{
    [HttpPost("cleanup/media")]
    public async Task<IActionResult> DeleteMedia([FromBody] DeleteMediaRequest request)
        => Ok(await cleanupService.DeleteMediaAsync(request.Keys, request.DryRun));

    // Physical file deletion is irreversible (no recycle bin), so require elevated access.
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    [HttpPost("cleanup/files")]
    public async Task<IActionResult> DeleteFiles([FromBody] DeleteFilesRequest request)
        => Ok(await cleanupService.DeleteFilesAsync(request.Paths, request.DryRun));
}
