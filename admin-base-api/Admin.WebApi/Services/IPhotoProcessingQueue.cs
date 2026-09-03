namespace Admin.WebApi.Services;

public interface IPhotoProcessingQueue
{
    ValueTask EnqueueAsync(int photoId, CancellationToken cancellationToken = default);
    ValueTask<int> DequeueAsync(CancellationToken cancellationToken);
    void MarkCompleted(int photoId);
}
