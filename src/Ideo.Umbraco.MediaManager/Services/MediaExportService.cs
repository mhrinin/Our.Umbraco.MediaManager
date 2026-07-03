using System.IO.Compression;
using Ideo.Umbraco.MediaManager.Interfaces;
using Ideo.Umbraco.MediaManager.Models;
using Umbraco.Cms.Core.IO;

namespace Ideo.Umbraco.MediaManager.Services;

/// <summary>
/// Packages the entire media filesystem into a ZIP in the export temp folder, preserving exact
/// relative paths (so the extracted tree drops into a new environment's media root or bucket) and
/// excluding the regenerable image cache. Reads only through <see cref="IFileSystem"/>, so local
/// disk, Azure Blob and S3 providers all work. Media is already compressed, hence
/// <see cref="CompressionLevel.NoCompression"/>; zip64 engages automatically past 4 GB; sync IO
/// is fine because the target is a file, not a response stream.
/// </summary>
public sealed class MediaExportService(
    MediaFileManager mediaFileManager,
    IExportStore exportStore) : IMediaScan
{
    private const int MaxCollectedErrors = 20;

    public ScanType Type => ScanType.Export;

    public Task<ScanResult> RunAsync(Guid jobId, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var fileSystem = mediaFileManager.FileSystem;
        var zipPath = exportStore.CreateExportFile(jobId);
        var errors = new List<string>();
        var fileCount = 0;
        var skippedCount = 0;

        try
        {
            using (var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                foreach (var relativePath in MediaFileWalker.Walk(fileSystem, cancellationToken))
                {
                    try
                    {
                        using var source = fileSystem.OpenFile(relativePath);
                        var entry = archive.CreateEntry(MediaScanLogic.ToZipEntryName(relativePath), CompressionLevel.NoCompression);
                        using var target = entry.Open();
                        source.CopyTo(target);
                        fileCount++;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        skippedCount++;
                        if (errors.Count < MaxCollectedErrors)
                        {
                            errors.Add($"{relativePath}: {ex.Message}");
                        }
                    }

                    progress?.Report(fileCount + skippedCount);
                }
            }

            if (fileCount == 0 && skippedCount > 0)
            {
                throw new InvalidOperationException("No files could be read; check the media filesystem/provider.");
            }
        }
        catch
        {
            TryDelete(zipPath);
            throw;
        }

        var token = exportStore.Complete(jobId, zipPath);
        var export = new ExportInfo(
            fileCount,
            new FileInfo(zipPath).Length,
            DateTime.UtcNow,
            token,
            errors,
            skippedCount);

        return Task.FromResult(new ScanResult(jobId, Type, [], 0, null, export));
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best effort; the export folder is wiped on the next export and on startup.
        }
    }
}
