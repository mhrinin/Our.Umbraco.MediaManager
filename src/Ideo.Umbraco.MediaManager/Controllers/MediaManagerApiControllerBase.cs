using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;

namespace Ideo.Umbraco.MediaManager.Controllers;

[ApiController]
[BackOfficeRoute($"{Constants.BackOfficeRoute}/api/v{{version:apiVersion}}")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessMedia)]
[MapToApi(Constants.ApiName)]
public abstract class MediaManagerApiControllerBase : ControllerBase;
