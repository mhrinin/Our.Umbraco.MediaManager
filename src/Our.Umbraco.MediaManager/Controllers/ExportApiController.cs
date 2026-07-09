using Asp.Versioning;
using Our.Umbraco.MediaManager.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Our.Umbraco.MediaManager.Controllers;

/// <summary>
/// Serves the finished media export. The action is anonymous at the ASP.NET level because a
/// browser download (<c>&lt;a href&gt;</c>) cannot carry the Management API's bearer token — the
/// URL itself is the credential: a cryptographically random capability token, obtainable only via
/// the authorized scan-result flow, valid until the next export or an app restart, and reusable so
/// HTTP Range resume (multiple GETs) works. Every failure mode is a uniform 404, never confirming
/// a job's existence; the token parameter is nullable so a missing token cannot surface as a
/// distinguishable 400.
/// </summary>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = Constants.ApiName)]
public class ExportApiController(IExportStore exportStore) : MediaManagerApiControllerBase
{
    [AllowAnonymous]
    [HttpGet("export/{jobId:guid}/file")]
    [HttpHead("export/{jobId:guid}/file")] // download managers probe with HEAD before resuming
    public IActionResult Download(Guid jobId, [FromQuery] string? token)
        => !string.IsNullOrEmpty(token) && exportStore.Resolve(jobId, token) is { } export
            ? PhysicalFile(
                export.ZipPath,
                "application/zip",
                $"media-export-{export.CreatedUtc:yyyyMMdd}.zip",
                enableRangeProcessing: true)
            : NotFound();
}
