using Admin.Entities;
using Microsoft.EntityFrameworkCore;

namespace Admin.WebApi.Services;

public class PhotoProcessingWorker : BackgroundService
{
    private readonly IPhotoProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PhotoProcessingWorker> _logger;

    public PhotoProcessingWorker(IPhotoProcessingQueue queue, IServiceScopeFactory scopeFactory, ILogger<PhotoProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await EnqueuePendingPhotosAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inicial reencolando fotos pendientes");
        }

        var recoveryTask = RunPeriodicRecoveryAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var photoId = await _queue.DequeueAsync(stoppingToken);
                try
                {
                    await ProcessPhotoAsync(photoId, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error procesando foto {PhotoId}", photoId);
                }
                finally
                {
                    _queue.MarkCompleted(photoId);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en worker de procesamiento de fotos");
            }
        }

        await recoveryTask;
    }

    private async Task RunPeriodicRecoveryAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await EnqueuePendingPhotosAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reencolando fotos pendientes");
            }
        }
    }

    private async Task EnqueuePendingPhotosAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();

        var pendingPhotoIds = await context.EventPhotos
            .AsNoTracking()
            .Where(p => !p.IsProcessed && !p.ProcessingFailed)
            .OrderBy(p => p.CreatedAt)
            .Select(p => p.Id)
            .Take(500)
            .ToListAsync(cancellationToken);

        if (pendingPhotoIds.Count == 0)
        {
            return;
        }

        foreach (var pendingPhotoId in pendingPhotoIds)
        {
            await _queue.EnqueueAsync(pendingPhotoId, cancellationToken);
        }

        _logger.LogInformation("Reencoladas fotos pendientes para procesamiento. Count={Count}", pendingPhotoIds.Count);
    }

    private async Task ProcessPhotoAsync(int photoId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();
        var storage = scope.ServiceProvider.GetRequiredService<IR2StorageService>();
        var processor = scope.ServiceProvider.GetRequiredService<IPhotoImageProcessor>();

        var photo = await context.EventPhotos
            .Include(p => p.PhotographerEvent)
            .FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);

        if (photo == null)
        {
            _logger.LogWarning("Foto {PhotoId} no encontrada para procesar", photoId);
            return;
        }

        const int maxAttempts = 6;
        var delayMs = 1000;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var originalStream = await storage.DownloadAsync(photo.OriginalPath, cancellationToken);

                await using var thumbStream = await processor.CreateThumbnailAsync(originalStream, cancellationToken);

                thumbStream.Position = 0;
                await storage.UploadAsync(photo.ThumbnailPath!, thumbStream, "image/jpeg", cancellationToken);

                thumbStream.Position = 0;
                await using var watermarkStream = await processor.CreateWatermarkedAsync(thumbStream, "CAPTURAR", cancellationToken);
                await storage.UploadAsync(photo.WatermarkedPath!, watermarkStream, "image/jpeg", cancellationToken);

                photo.IsProcessed = true;
                photo.ProcessingFailed = false;
                photo.ProcessingError = null;
                photo.WatermarkApplied = true;
                photo.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Foto {PhotoId} procesada correctamente en intento {Attempt}", photoId, attempt);
                return;
            }
            catch (Exception ex)
            {
                if (attempt < maxAttempts)
                {
                    _logger.LogWarning(ex, "Reintento automático de procesamiento para foto {PhotoId}. Attempt={Attempt}/{MaxAttempts}", photoId, attempt, maxAttempts);
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs = Math.Min(delayMs * 2, 8000);
                    continue;
                }

                photo.IsProcessed = false;
                photo.ProcessingFailed = true;
                photo.ProcessingError = TruncateError(ex.Message);
                photo.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync(cancellationToken);

                _logger.LogError(ex, "Foto {PhotoId} marcada como fallida en procesamiento luego de {Attempts} intentos", photoId, maxAttempts);
            }
        }
    }

    private static string TruncateError(string message)
    {
        const int maxLength = 500;
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Error de procesamiento sin detalle";
        }

        return message.Length <= maxLength ? message : message[..maxLength];
    }
}
