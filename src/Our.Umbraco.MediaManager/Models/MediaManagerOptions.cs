namespace Our.Umbraco.MediaManager.Models;

public sealed class MediaManagerOptions
{
    public const string SectionName = "MediaManager";

    /// <summary>
    /// When enabled (default), unused-media detection also scans content property values — published
    /// and draft — for references, on top of Umbraco relations. Disable on very large sites to avoid
    /// hydrating all content on each scan; detection then relies on relations only (published references).
    /// </summary>
    public bool DeepReferenceScan { get; set; } = true;
}
