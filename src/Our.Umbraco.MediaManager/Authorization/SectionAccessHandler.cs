using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Our.Umbraco.MediaManager.Models;
using Umbraco.Cms.Api.Management.Mapping;
using Umbraco.Cms.Core.Security.Authorization;

namespace Our.Umbraco.MediaManager.Authorization;

public sealed class SectionAccessHandler(
    IAuthorizationHelper authorizationHelper,
    IOptions<MediaManagerOptions> options) : AuthorizationHandler<SectionAccessRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SectionAccessRequirement requirement)
    {
        if (authorizationHelper.TryGetUmbracoUser(context.User, out var user)
            && user.AllowedSections.Contains(SectionMapper.GetAlias(options.Value.Section)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
