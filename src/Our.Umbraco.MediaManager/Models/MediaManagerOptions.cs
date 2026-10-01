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

    /// <summary>
    /// Backoffice section where the Media Manager dashboard is displayed.
    /// Allowed values: "Settings" (default) or "Media".
    /// </summary>
    public string Section { get; set; } = "Settings";

    public bool IsMediaSection =>
        string.Equals(Section, "Media", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Section, "Umb.Section.Media", StringComparison.OrdinalIgnoreCase);

    public string SectionAlias => IsMediaSection ? "Umb.Section.Media" : "Umb.Section.Settings";
}

