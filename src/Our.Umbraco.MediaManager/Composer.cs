using Our.Umbraco.MediaManager.Interfaces;
using Our.Umbraco.MediaManager.Models;
using Our.Umbraco.MediaManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.Authorization;

namespace Our.Umbraco.MediaManager;

public class Composer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<MediaManagerOptions>()
            .Bind(builder.Config.GetSection(MediaManagerOptions.SectionName))
            .Validate(
                options => Enum.IsDefined(options.Section),
                $"{MediaManagerOptions.SectionName}:{nameof(MediaManagerOptions.Section)} must be one of: {string.Join(", ", Enum.GetNames<MediaManagerSection>())}.")
            .ValidateOnStart();

        builder.Services.AddOptions<AuthorizationOptions>()
            .PostConfigure<IOptions<MediaManagerOptions>>((authorizationOptions, mediaManagerOptions) =>
            {
                var sectionPolicyName = mediaManagerOptions.Value.Section switch
                {
                    MediaManagerSection.Media => AuthorizationPolicies.SectionAccessMedia,
                    _ => AuthorizationPolicies.SectionAccessSettings,
                };

                var sectionPolicy = authorizationOptions.GetPolicy(sectionPolicyName)
                    ?? throw new InvalidOperationException($"Umbraco authorization policy '{sectionPolicyName}' is not registered; the Media Manager API cannot be secured.");

                authorizationOptions.AddPolicy(Constants.AuthorizationPolicy, sectionPolicy);
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
