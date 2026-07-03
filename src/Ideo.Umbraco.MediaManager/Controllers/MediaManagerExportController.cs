using Ideo.Umbraco.MediaManager.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Controllers;

namespace Ideo.Umbraco.MediaManager.Controllers;

/// <summary>
/// Serves the finished media export at <c>/Umbraco/Api/MediaManagerExport/Download</c>. The action
/// is anonymous at the ASP.NET level because a browser download cannot carry backoffice
/// authentication headers — the URL itself is the credential: a cryptographically random capability
/// token, obtainable only via the authorized scan-result flow, valid until the next export or an app
/// restart, and reusable so HTTP Range resume (multiple GETs) works. Every failure mode is a uniform
/// 404, never confirming a job's existence; the token parameter is nullable so a missing token
/// cannot surface as a distinguishable 400.
/// </summary>
public sealed class MediaManagerExportController(IExportStore exportStore) : UmbracoApiController
{
    [HttpGet]
    [HttpHead] // download managers probe with HEAD before resuming
    public IActionResult Download(Guid jobId, string? token)
        => !string.IsNullOrEmpty(token) && exportStore.Resolve(jobId, token) is { } export
            ? PhysicalFile(
                export.ZipPath,
                "application/zip",
                $"media-export-{export.CreatedUtc:yyyyMMdd}.zip",
                enableRangeProcessing: true)
            : NotFound();
}
