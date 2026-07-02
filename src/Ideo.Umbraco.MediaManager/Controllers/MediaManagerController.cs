using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;

namespace Ideo.Umbraco.MediaManager.Controllers;

/// <summary>
/// Umbraco 13 legacy backoffice API. Routes resolve to
/// <c>/umbraco/backoffice/MediaManager/MediaManager/{action}</c>. Deriving from
/// <see cref="UmbracoAuthorizedJsonController"/> enforces backoffice authentication plus the Angular
/// anti-forgery token on every action; the whole API additionally requires Media section access, and
/// physical file deletion (irreversible) requires Settings section access, mirroring the v16/17 line.
/// </summary>
[PluginController("MediaManager")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessMedia)]
public sealed class MediaManagerController(
    IScanJobManager scanJobManager,
    ICleanupService cleanupService,
    IStorageReportService storageReportService) : UmbracoAuthorizedJsonController
{
    [HttpPost]
    public IActionResult StartScan(ScanType type)
        => Enum.IsDefined(type)
            ? Ok(new { jobId = scanJobManager.StartScan(type) })
            : BadRequest($"Unknown scan type '{type}'.");

    [HttpGet]
    public IActionResult GetStatus(Guid jobId)
    {
        var status = scanJobManager.GetStatus(jobId);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpGet]
    public IActionResult GetResult(Guid jobId)
    {
        var result = scanJobManager.GetResult(jobId);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public IActionResult CancelScan(Guid jobId) => Ok(new { cancelled = scanJobManager.Cancel(jobId) });

    [HttpPost]
    public async Task<IActionResult> DeleteMedia([FromBody] DeleteMediaRequest request)
        => Ok(await cleanupService.DeleteMediaAsync(request.Keys, request.DryRun));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    public async Task<IActionResult> DeleteFiles([FromBody] DeleteFilesRequest request)
        => Ok(await cleanupService.DeleteFilesAsync(request.Paths, request.DryRun));

    [HttpGet]
    public async Task<IActionResult> StorageReport(CancellationToken cancellationToken)
        => Ok(await storageReportService.GenerateAsync(null, cancellationToken));
}
