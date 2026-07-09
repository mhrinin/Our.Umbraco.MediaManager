namespace Our.Umbraco.MediaManager.Interfaces;

public interface IMediaReferenceCollector
{
    HashSet<Guid> Collect(CancellationToken cancellationToken);
}
