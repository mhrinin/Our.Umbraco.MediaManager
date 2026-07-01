using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Attributes;

namespace Ideo.Umbraco.MediaManager.Controllers;

/// <summary>
/// Umbraco 13 legacy backoffice API. Routes resolve to
/// <c>/umbraco/backoffice/MediaManager/MediaManager/{action}</c> and are protected by the backoffice
/// authentication that <see cref="UmbracoAuthorizedApiController"/> enforces.
/// </summary>
[PluginController("MediaManager")]
public sealed class MediaManagerController(
    IScanJobManager scanJobManager,
    ICleanupService cleanupService,
    IStorageReportService storageReportService) : UmbracoAuthorizedApiController
{
    [HttpPost]
    public IActionResult StartScan(ScanType type) => Ok(new { jobId = scanJobManager.StartScan(type) });

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
    public async Task<IActionResult> DeleteFiles([FromBody] DeleteFilesRequest request)
        => Ok(await cleanupService.DeleteFilesAsync(request.Paths, request.DryRun));

    [HttpGet]
    public async Task<IActionResult> StorageReport(CancellationToken cancellationToken)
        => Ok(await storageReportService.GenerateAsync(null, cancellationToken));
}
