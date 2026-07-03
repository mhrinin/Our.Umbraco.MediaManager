using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Ideo.Umbraco.MediaManager.Services;
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
/// anti-forgery token on every action; the whole API requires Settings section access — the
/// dashboard lives in the Settings section, mirroring the v16/17 line. Deletion targets are always
/// resolved against the stored scan result — it is the server-side allowlist.
/// </summary>
[PluginController(Constants.PluginName)]
[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
public sealed class MediaManagerController(
    IScanJobManager scanJobManager,
    ICleanupService cleanupService) : UmbracoAuthorizedJsonController
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 500;

    [HttpPost]
    public IActionResult StartScan(ScanType type)
        => Enum.IsDefined(type)
            ? Ok(new StartScanResponse(scanJobManager.StartScan(type)))
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
        return result is null ? NotFound() : Ok(ScanResultSummary.From(result));
    }

    [HttpGet]
    public IActionResult GetResultItems(Guid jobId, int skip = 0, int take = DefaultPageSize)
    {
        var result = scanJobManager.GetResult(jobId);
        if (result is null)
        {
            return NotFound();
        }

        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, MaxPageSize);

        return Ok(new ScanResultItems(result.Items.Count, result.Items.Skip(skip).Take(take).ToArray()));
    }

    [HttpGet]
    public IActionResult GetLatestResult(ScanType type)
    {
        var result = scanJobManager.GetLatestResult(type);
        return result is null ? NotFound() : Ok(ScanResultSummary.From(result));
    }

    [HttpGet]
    public IActionResult GetReclaimableBytes()
        => Ok(new ReclaimableSpaceResponse(MediaScanLogic.ComputeReclaimableBytes(
            scanJobManager.GetLatestResult(ScanType.UnusedMedia),
            scanJobManager.GetLatestResult(ScanType.OrphanedFiles),
            scanJobManager.GetLatestResult(ScanType.Duplicates))));

    [HttpPost]
    public IActionResult CancelScan(Guid jobId) => Ok(new { cancelled = scanJobManager.Cancel(jobId) });

    [HttpPost]
    public async Task<IActionResult> DeleteItems(Guid jobId, [FromBody] CleanupItemsRequest request)
        => await DeleteAsync(jobId, request.Ids, request.DryRun);

    [HttpPost]
    public async Task<IActionResult> DeleteAll(Guid jobId, [FromBody] CleanupAllRequest request)
        => await DeleteAsync(jobId, null, request.DryRun);

    private async Task<IActionResult> DeleteAsync(Guid jobId, IReadOnlyList<string>? ids, bool dryRun)
    {
        if (scanJobManager.GetResult(jobId) is not { } scanResult)
        {
            return Ok(new CleanupResult(0, ["The scan result is no longer available. Rescan and try again."]));
        }

        ids ??= scanResult.Items.Select(item => item.Id).ToArray();

        return Ok(await cleanupService.DeleteItemsAsync(scanResult, ids, dryRun));
    }
}
