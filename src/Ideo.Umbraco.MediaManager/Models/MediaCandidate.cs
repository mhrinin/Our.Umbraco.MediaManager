namespace Ideo.Umbraco.MediaManager.Models;

public sealed record MediaCandidate(Guid Key, string Name, string? Path, long SizeBytes);
