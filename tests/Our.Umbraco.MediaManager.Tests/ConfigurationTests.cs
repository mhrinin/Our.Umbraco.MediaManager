using System.Security.Claims;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Our.Umbraco.MediaManager.Authorization;
using Our.Umbraco.MediaManager.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security.Authorization;

namespace Our.Umbraco.MediaManager.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void MediaManagerOptions_DefaultsToSettingsSection()
        => Assert.Equal("Umb.Section.Settings", new MediaManagerOptions().Section);

    [Fact]
    public void MediaManagerOptions_Binding_ReadsSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{MediaManagerOptions.SectionName}:{nameof(MediaManagerOptions.Section)}"] = "My.Custom.Section",
            })
            .Build();

        var options = new MediaManagerOptions();
        configuration.GetSection(MediaManagerOptions.SectionName).Bind(options);

        Assert.Equal("My.Custom.Section", options.Section);
    }

    [Theory]
    [InlineData("Umb.Section.Settings")]
    [InlineData("Umb.Section.Media")]
    [InlineData("My.Custom.Section")]
    public async Task ManifestReader_RegistersDashboardInConfiguredSection(string section)
    {
        var fileVersionProvider = new Mock<IFileVersionProvider>();
        fileVersionProvider
            .Setup(provider => provider.AddFileVersionToPath(It.IsAny<PathString>(), It.IsAny<string>()))
            .Returns<PathString, string>((_, path) => $"{path}?v=hash");
        var reader = new MediaManagerManifestReader(
            Options.Create(new MediaManagerOptions { Section = section }),
            fileVersionProvider.Object);

        var manifest = Assert.Single(await reader.ReadPackageManifestsAsync());
        var dashboard = JsonSerializer.SerializeToNode(Assert.Single(manifest.Extensions))!;

        Assert.Equal("dashboard", dashboard["type"]!.GetValue<string>());
        Assert.Equal("/App_Plugins/MediaManager/media-manager.js?v=hash", dashboard["element"]!.GetValue<string>());
        var condition = Assert.Single(dashboard["conditions"]!.AsArray())!;
        Assert.Equal("Umb.Condition.SectionAlias", condition["alias"]!.GetValue<string>());
        Assert.Equal(section, condition["match"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("Umb.Section.Settings", "settings", true)]
    [InlineData("Umb.Section.Media", "media", true)]
    [InlineData("My.Custom.Section", "My.Custom.Section", true)]
    [InlineData("Umb.Section.Settings", "media", false)]
    [InlineData("My.Custom.Section", "settings", false)]
    public async Task SectionAccessHandler_RequiresAccessToConfiguredSection(string section, string allowedSection, bool expected)
    {
        var user = new Mock<IUser>();
        user.SetupGet(u => u.AllowedSections).Returns([allowedSection]);
        var userObject = user.Object;
        var authorizationHelper = new Mock<IAuthorizationHelper>();
        authorizationHelper
            .Setup(helper => helper.TryGetUmbracoUser(It.IsAny<IPrincipal>(), out userObject))
            .Returns(true);

        var handler = new SectionAccessHandler(
            authorizationHelper.Object,
            Options.Create(new MediaManagerOptions { Section = section }));
        var context = new AuthorizationHandlerContext([new SectionAccessRequirement()], new ClaimsPrincipal(), null);

        await handler.HandleAsync(context);

        Assert.Equal(expected, context.HasSucceeded);
    }
}
