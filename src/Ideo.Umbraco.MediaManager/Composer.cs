using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Ideo.Umbraco.MediaManager.Services;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Ideo.Umbraco.MediaManager;

public class Composer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<MediaManagerOptions>()
            .Bind(builder.Config.GetSection(MediaManagerOptions.SectionName));

        builder.Services.AddScoped<IMediaReferenceCollector, MediaReferenceCollector>();
        builder.Services.AddScoped<IUnusedMediaScanner, UnusedMediaScanner>();
        builder.Services.AddScoped<IOrphanedFileScanner, OrphanedFileScanner>();
        builder.Services.AddScoped<IBrokenMediaScanner, BrokenMediaScanner>();
        builder.Services.AddScoped<ICleanupService, CleanupService>();
        builder.Services.AddScoped<IStorageReportService, StorageReportService>();

        builder.Services.AddSingleton<ScanJobManager>();
        builder.Services.AddSingleton<IScanJobManager>(provider => provider.GetRequiredService<ScanJobManager>());
        builder.Services.AddHostedService(provider => provider.GetRequiredService<ScanJobManager>());
    }
}
