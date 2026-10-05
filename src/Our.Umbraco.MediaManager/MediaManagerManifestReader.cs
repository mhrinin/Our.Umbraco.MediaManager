using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using Our.Umbraco.MediaManager.Models;
using Umbraco.Cms.Core.Manifest;
using Umbraco.Cms.Infrastructure.Manifest;

namespace Our.Umbraco.MediaManager;

public sealed class MediaManagerManifestReader(
    IOptions<MediaManagerOptions> options,
    IFileVersionProvider fileVersionProvider) : IPackageManifestReader
{
    private const string DashboardElementPath = $"/App_Plugins/{Constants.PluginName}/media-manager.js";

    public Task<IEnumerable<PackageManifest>> ReadPackageManifestsAsync()
    {
        var version = typeof(MediaManagerManifestReader).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];

        PackageManifest manifest = new()
        {
            Id = "Our.Umbraco.MediaManager",
            Name = "Umbraco Media Manager",
            Version = version,
            Extensions =
            [
                new
                {
                    type = "dashboard",
                    alias = "Our.Umbraco.MediaManager.Dashboard",
                    name = "Media Manager Dashboard",
                    element = fileVersionProvider.AddFileVersionToPath(PathString.Empty, DashboardElementPath),
                    elementName = "media-manager-dashboard",
                    weight = 10,
                    meta = new { label = "Media Manager", pathname = "media-manager" },
                    conditions = new[] { new { alias = "Umb.Condition.SectionAlias", match = options.Value.Section } },
                },
            ],
        };

        return Task.FromResult<IEnumerable<PackageManifest>>([manifest]);
    }
}
