namespace Our.Umbraco.MediaManager.Models;

public sealed class ScanJobStatus
{
    // net6.0 lacks the runtime attributes that back C# 'required' members.
    public Guid Id { get; init; }

    public ScanType Type { get; init; }

    public ScanState State { get; set; } = ScanState.Queued;

    public int Processed { get; set; }

    public int FoundCount { get; set; }

    public string? Error { get; set; }
}
