using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Our.Umbraco.MediaManager.Interfaces;
using Our.Umbraco.MediaManager.Models;
using Our.Umbraco.MediaManager.Services;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.Authorization;

namespace Our.Umbraco.MediaManager;

public class Composer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<MediaManagerOptions>()
            .Bind(builder.Config.GetSection(MediaManagerOptions.SectionName));

        builder.Services.AddOptions<AuthorizationOptions>()
            .PostConfigure<IOptions<MediaManagerOptions>>((authOptions, mmOptions) =>
            {
                var policyName = mmOptions.Value.IsMediaSection
                    ? AuthorizationPolicies.SectionAccessMedia
                    : AuthorizationPolicies.SectionAccessSettings;

                var basePolicy = authOptions.GetPolicy(policyName);
                if (basePolicy is not null)
                {
                    authOptions.AddPolicy(Constants.AuthorizationPolicy, basePolicy);
                }
                else
                {
                    authOptions.AddPolicy(Constants.AuthorizationPolicy, policy => policy.RequireAuthenticatedUser());
                }
            });

        builder.Services.AddScoped<IMediaReferenceCollector, MediaReferenceCollector>();

        builder.Services.AddScoped<IMediaScan, UnusedMediaScanner>();
        builder.Services.AddScoped<IMediaScan, OrphanedFileScanner>();
        builder.Services.AddScoped<IMediaScan, BrokenMediaScanner>();
        builder.Services.AddScoped<IMediaScan, DuplicateScanner>();
        builder.Services.AddScoped<IMediaScan, StorageReportService>();
        builder.Services.AddScoped<IMediaScan, MediaExportService>();
        builder.Services.AddScoped<ICleanupService, CleanupService>();

        builder.Services.AddSingleton<ScanJobManager>();
        builder.Services.AddSingleton<IScanJobManager>(provider => provider.GetRequiredService<ScanJobManager>());
        builder.Services.AddHostedService(provider => provider.GetRequiredService<ScanJobManager>());

        builder.Services.AddSingleton<ExportStore>();
        builder.Services.AddSingleton<IExportStore>(provider => provider.GetRequiredService<ExportStore>());
        builder.Services.AddHostedService(provider => provider.GetRequiredService<ExportStore>());
    }
}
