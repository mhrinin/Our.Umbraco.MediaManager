using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Our.Umbraco.MediaManager.Controllers;
using Our.Umbraco.MediaManager.Models;

namespace Our.Umbraco.MediaManager.Tests;

public sealed class ConfigurationTests
{
    [Theory]
    [InlineData("Settings", false, "Umb.Section.Settings")]
    [InlineData("settings", false, "Umb.Section.Settings")]
    [InlineData("Umb.Section.Settings", false, "Umb.Section.Settings")]
    [InlineData("Media", true, "Umb.Section.Media")]
    [InlineData("media", true, "Umb.Section.Media")]
    [InlineData("Umb.Section.Media", true, "Umb.Section.Media")]
    public void MediaManagerOptions_SectionParsing_ResolvesCorrectSection(string section, bool expectedIsMedia, string expectedAlias)
    {
        var options = new MediaManagerOptions { Section = section };

        Assert.Equal(expectedIsMedia, options.IsMediaSection);
        Assert.Equal(expectedAlias, options.SectionAlias);
    }

    [Fact]
    public void ConfigurationApiController_ReturnsConfiguredSection()
    {
        var options = Options.Create(new MediaManagerOptions { Section = "Media" });
        var controller = new ConfigurationApiController(options);

        var result = Assert.IsType<OkObjectResult>(controller.GetConfig());
        Assert.NotNull(result.Value);
    }
}
