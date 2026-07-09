namespace Our.Umbraco.MediaManager.Models;

public enum ScanState
{
    Queued,
    Running,
    Completed,
    Cancelled,
    Failed,
}
