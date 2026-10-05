using Our.Umbraco.MediaManager.Authorization;
using Our.Umbraco.MediaManager.Interfaces;
using Our.Umbraco.MediaManager.Models;
using Our.Umbraco.MediaManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Validation.AspNetCore;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Infrastructure.Manifest;

namespace Our.Umbraco.MediaManager;

public class Composer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<MediaManagerOptions>()
            .Bind(builder.Config.GetSection(MediaManagerOptions.SectionName));

        builder.Services.AddSingleton<IPackageManifestReader, MediaManagerManifestReader>();

        builder.Services.AddSingleton<IAuthorizationHandler, SectionAccessHandler>();
        builder.Services.AddAuthorization(options => options.AddPolicy(Constants.AuthorizationPolicy, policy =>
        {
            policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            policy.Requirements.Add(new SectionAccessRequirement());
        }));

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
