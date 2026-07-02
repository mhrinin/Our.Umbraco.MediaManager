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
        builder.Services.AddScoped<IMediaScan, UnusedMediaScanner>();
        builder.Services.AddScoped<IMediaScan, OrphanedFileScanner>();
        builder.Services.AddScoped<IMediaScan, BrokenMediaScanner>();
        builder.Services.AddScoped<IMediaScan, DuplicateScanner>();
        builder.Services.AddScoped<IMediaScan, StorageReportService>();
        builder.Services.AddScoped<ICleanupService, CleanupService>();

        builder.Services.AddSingleton<ScanJobManager>();
        builder.Services.AddSingleton<IScanJobManager>(provider => provider.GetRequiredService<ScanJobManager>());
        builder.Services.AddHostedService(provider => provider.GetRequiredService<ScanJobManager>());
    }
}
