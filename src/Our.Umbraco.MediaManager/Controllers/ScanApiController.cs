using Asp.Versioning;
using Our.Umbraco.MediaManager.Interfaces;
using Our.Umbraco.MediaManager.Models;
using Our.Umbraco.MediaManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace Our.Umbraco.MediaManager.Controllers;

[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = Constants.ApiName)]
public class ScanApiController(IScanJobManager jobManager) : MediaManagerApiControllerBase
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 500;

    [HttpPost("scan")]
    public IActionResult StartScan([FromQuery] ScanType type)
        => Enum.IsDefined(type)
            ? Ok(new StartScanResponse(jobManager.StartScan(type)))
            : BadRequest($"Unknown scan type '{type}'.");

    [HttpGet("scan/{jobId:guid}/status")]
    public IActionResult GetStatus(Guid jobId)
        => jobManager.GetStatus(jobId) is { } status ? Ok(status) : NotFound();

    [HttpGet("scan/{jobId:guid}/result")]
    public IActionResult GetResult(Guid jobId)
        => jobManager.GetResult(jobId) is { } result ? Ok(ScanResultSummary.From(result)) : NotFound();

    [HttpGet("scan/{jobId:guid}/result/items")]
    public IActionResult GetResultItems(Guid jobId, [FromQuery] int skip = 0, [FromQuery] int take = DefaultPageSize)
    {
        if (jobManager.GetResult(jobId) is not { } result)
        {
            return NotFound();
        }

        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, MaxPageSize);

        return Ok(new ScanResultItems(result.Items.Count, [.. result.Items.Skip(skip).Take(take)]));
    }

    [HttpGet("scan/latest")]
    public IActionResult GetLatestResult([FromQuery] ScanType type)
        => jobManager.GetLatestResult(type) is { } result ? Ok(ScanResultSummary.From(result)) : NotFound();

    [HttpGet("scan/reclaimable")]
    public IActionResult GetReclaimableBytes()
        => Ok(new ReclaimableSpaceResponse(MediaScanLogic.ComputeReclaimableBytes(
            jobManager.GetLatestResult(ScanType.UnusedMedia),
            jobManager.GetLatestResult(ScanType.OrphanedFiles),
            jobManager.GetLatestResult(ScanType.Duplicates))));

    [HttpPost("scan/{jobId:guid}/cancel")]
    public IActionResult Cancel(Guid jobId) => jobManager.Cancel(jobId) ? Ok() : NotFound();
}
