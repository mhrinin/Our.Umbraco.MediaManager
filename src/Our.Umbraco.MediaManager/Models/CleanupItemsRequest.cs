namespace Our.Umbraco.MediaManager.Models;

public sealed record CleanupItemsRequest(IReadOnlyList<string> Ids, bool DryRun);
