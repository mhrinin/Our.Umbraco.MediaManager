using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Common.Filters;
using Umbraco.Cms.Web.Common.Routing;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Our.Umbraco.MediaManager.Controllers;

[ApiController]
[BackOfficeRoute($"{Constants.BackOfficeRoute}/api/v{{version:apiVersion}}")]
[Authorize(Policy = Constants.AuthorizationPolicy)]
[MapToApi(Constants.ApiName)]
[JsonOptionsName(UmbracoConstants.JsonOptionsNames.BackOffice)]
public abstract class MediaManagerApiControllerBase : ControllerBase;
