using Asp.Versioning;
using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ideo.Umbraco.MediaManager.Controllers;

[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = Constants.ApiName)]
public class ScanApiController(IScanJobManager jobManager) : MediaManagerApiControllerBase
{
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
        => jobManager.GetResult(jobId) is { } result ? Ok(result) : NotFound();

    [HttpPost("scan/{jobId:guid}/cancel")]
    public IActionResult Cancel(Guid jobId) => jobManager.Cancel(jobId) ? Ok() : NotFound();
}
