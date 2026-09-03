using System.Threading.Channels;
using System.Collections.Concurrent;

namespace Admin.WebApi.Services;

public class PhotoProcessingQueue : IPhotoProcessingQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();
    private readonly ConcurrentDictionary<int, byte> _queuedPhotoIds = new();

    public ValueTask EnqueueAsync(int photoId, CancellationToken cancellationToken = default)
    {
        if (!_queuedPhotoIds.TryAdd(photoId, 0))
        {
            return ValueTask.CompletedTask;
        }

        return _channel.Writer.WriteAsync(photoId, cancellationToken);
    }

    public ValueTask<int> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken);

    public void MarkCompleted(int photoId)
    {
        _queuedPhotoIds.TryRemove(photoId, out _);
    }
}
