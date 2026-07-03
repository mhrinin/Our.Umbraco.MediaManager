using Ideo.Umbraco.MediaManager.Models;

namespace Ideo.Umbraco.MediaManager.Interfaces;

public interface IExportStore
{
    /// <summary>
    /// Prepares a fresh export target: best-effort wipes the export folder (invalidating the
    /// previous export's download immediately) and returns the full path the new zip must be
    /// written to.
    /// </summary>
    string CreateExportFile(Guid jobId);

    /// <summary>Marks the export complete and returns the capability token guarding its download.</summary>
    string Complete(Guid jobId, string zipPath);

    /// <summary>Resolves a jobId + token pair to the downloadable file, or null when invalid/expired.</summary>
    ExportFile? Resolve(Guid jobId, string token);
}
