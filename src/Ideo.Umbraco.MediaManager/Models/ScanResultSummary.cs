namespace Ideo.Umbraco.MediaManager.Models;

/// <summary>
/// What <c>GET scan/{jobId}/result</c> returns: everything about a finished scan except the item
/// list, which can be huge and is served page-by-page via <c>result/items</c>.
/// </summary>
public sealed record ScanResultSummary(
    Guid JobId,
    ScanType Type,
    int TotalItems,
    long ReclaimableBytes,
    StorageReport? Report,
    ExportInfo? Export)
{
    public static ScanResultSummary From(ScanResult result)
        => new(
            result.JobId,
            result.Type,
            result.Items.Count,
            result.ReclaimableBytes,
            result.Report,
            result.Export);
}
