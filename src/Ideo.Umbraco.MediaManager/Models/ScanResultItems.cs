namespace Ideo.Umbraco.MediaManager.Models;

/// <summary>One page of a scan result's items.</summary>
public sealed record ScanResultItems(int Total, IReadOnlyList<ScanItem> Items);
