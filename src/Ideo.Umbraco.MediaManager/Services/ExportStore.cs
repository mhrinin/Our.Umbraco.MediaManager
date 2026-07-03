using System.Security.Cryptography;
using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IUmbracoHostingEnvironment = Umbraco.Cms.Core.Hosting.IHostingEnvironment;

namespace Ideo.Umbraco.MediaManager.Services;

/// <summary>
/// Owns the export temp folder and the capability token guarding the current export's download.
/// Exactly one export is retained (matching the job manager's one-job-per-type rule): starting a
/// new export invalidates the previous download immediately. Tokens live in memory only, so an
/// application restart invalidates everything — <see cref="StartAsync"/> reclaims the disk.
/// </summary>
public sealed class ExportStore(
    IUmbracoHostingEnvironment hostingEnvironment,
    ILogger<ExportStore> logger) : IExportStore, IHostedService
{
    private sealed record CurrentExport(Guid JobId, byte[] TokenBytes, string ZipPath, DateTime CreatedUtc);

    private readonly object gate = new();
    private CurrentExport? current;

    private string ExportFolder => Path.Combine(hostingEnvironment.LocalTempPath, Constants.PluginName);

    public string CreateExportFile(Guid jobId)
    {
        lock (gate)
        {
            current = null;
        }

        var folder = ExportFolder;
        Directory.CreateDirectory(folder);
        WipeFolder(folder);

        return Path.Combine(folder, $"media-export-{jobId:N}.zip");
    }

    public string Complete(Guid jobId, string zipPath)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        lock (gate)
        {
            current = new CurrentExport(jobId, tokenBytes, zipPath, DateTime.UtcNow);
        }

        return Convert.ToHexString(tokenBytes);
    }

    public ExportFile? Resolve(Guid jobId, string token)
    {
        CurrentExport? snapshot;
        lock (gate)
        {
            snapshot = current;
        }

        if (snapshot is null || snapshot.JobId != jobId)
        {
            return null;
        }

        byte[] tokenBytes;
        try
        {
            tokenBytes = Convert.FromHexString(token);
        }
        catch (FormatException)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(tokenBytes, snapshot.TokenBytes) || !File.Exists(snapshot.ZipPath))
        {
            return null;
        }

        return new ExportFile(snapshot.ZipPath, snapshot.CreatedUtc);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // In-memory tokens cannot outlive a restart, so any leftover zip is unreachable garbage.
        if (Directory.Exists(ExportFolder))
        {
            WipeFolder(ExportFolder);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void WipeFolder(string folder)
    {
        foreach (var file in Directory.GetFiles(folder))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                // A zip still being streamed to a client can be locked (Windows); the jobId-named
                // new file avoids collision and the leftover is reclaimed next export/restart.
                logger.LogWarning(ex, "Could not delete old export file {File}.", file);
            }
        }
    }
}
