using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Our.Umbraco.MediaManager.Controllers;
using Our.Umbraco.MediaManager.Models;

namespace Our.Umbraco.MediaManager.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void MediaManagerOptions_DefaultsToSettingsSection()
    {
        var options = new MediaManagerOptions();

        Assert.Equal(MediaManagerSection.Settings, options.Section);
        Assert.Equal("Umb.Section.Settings", options.SectionAlias);
    }

    [Theory]
    [InlineData(MediaManagerSection.Settings, "Umb.Section.Settings")]
    [InlineData(MediaManagerSection.Media, "Umb.Section.Media")]
    public void MediaManagerOptions_SectionAlias_MatchesSection(MediaManagerSection section, string expectedAlias)
    {
        var options = new MediaManagerOptions { Section = section };

        Assert.Equal(expectedAlias, options.SectionAlias);
    }

    [Theory]
    [InlineData("Settings", MediaManagerSection.Settings)]
    [InlineData("settings", MediaManagerSection.Settings)]
    [InlineData("Media", MediaManagerSection.Media)]
    [InlineData("media", MediaManagerSection.Media)]
    public void MediaManagerOptions_Binding_ParsesSection(string configuredValue, MediaManagerSection expectedSection)
    {
        var options = BindOptions(configuredValue);

        Assert.Equal(expectedSection, options.Section);
    }

    [Theory]
    [InlineData("Medai")]
    [InlineData("Umb.Section.Media")]
    public void MediaManagerOptions_Binding_RejectsUnknownSection(string configuredValue)
        => Assert.Throws<InvalidOperationException>(() => BindOptions(configuredValue));

    [Fact]
    public void ConfigurationApiController_ReturnsConfiguredSectionAlias()
    {
        var options = Options.Create(new MediaManagerOptions { Section = MediaManagerSection.Media });
        var controller = new ConfigurationApiController(options);

        var result = Assert.IsType<OkObjectResult>(controller.GetConfig());
        var response = Assert.IsType<ConfigurationResponse>(result.Value);
        Assert.Equal("Umb.Section.Media", response.Section);
    }

    private static MediaManagerOptions BindOptions(string configuredValue)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{MediaManagerOptions.SectionName}:{nameof(MediaManagerOptions.Section)}"] = configuredValue,
            })
            .Build();

        var options = new MediaManagerOptions();
        configuration.GetSection(MediaManagerOptions.SectionName).Bind(options);
        return options;
    }
}
