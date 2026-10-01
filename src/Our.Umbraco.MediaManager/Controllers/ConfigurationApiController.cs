using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Our.Umbraco.MediaManager.Models;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Common.Filters;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Our.Umbraco.MediaManager.Controllers;

[ApiController]
[BackOfficeRoute($"{Constants.BackOfficeRoute}/api/v{{version:apiVersion}}")]
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
[MapToApi(Constants.ApiName)]
[JsonOptionsName(UmbracoConstants.JsonOptionsNames.BackOffice)]
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = Constants.ApiName)]
public class ConfigurationApiController(IOptions<MediaManagerOptions> options) : ControllerBase
{
    [HttpGet("config")]
    [ProducesResponseType<ConfigurationResponse>(StatusCodes.Status200OK)]
    public IActionResult GetConfig() => Ok(new ConfigurationResponse(options.Value.SectionAlias));
}
