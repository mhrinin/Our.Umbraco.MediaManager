namespace Ideo.Umbraco.MediaManager.Models;

public sealed class ScanJobStatus
{
    public required Guid Id { get; init; }

    public required ScanType Type { get; init; }

    public ScanState State { get; set; } = ScanState.Queued;

    public int Processed { get; set; }

    public int FoundCount { get; set; }

    public string? Error { get; set; }
}
