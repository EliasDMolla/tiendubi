using Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Admin.WebApi.Services;

public class PhotoDeliveryRetryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PhotoDeliveryRetryWorker> _logger;
    private readonly PhotoDeliveryRetrySettings _settings;

    public PhotoDeliveryRetryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<PhotoDeliveryRetrySettings> settings,
        ILogger<PhotoDeliveryRetryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("PhotoDeliveryRetryWorker deshabilitado por configuración");
            return;
        }

        var pollIntervalSeconds = Math.Max(10, _settings.PollIntervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(pollIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RetryPendingDeliveriesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en reintento automático de entrega de fotos");
            }
        }
    }

    private async Task RetryPendingDeliveriesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();
        var photoDeliveryService = scope.ServiceProvider.GetRequiredService<IPhotoDeliveryService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var maxAttempts = Math.Clamp(_settings.MaxAttempts, 1, 20);
        var backoff = NormalizeBackoff(_settings.BackoffMinutes);
        var now = DateTime.UtcNow;

        var candidates = await context.PhotoCheckoutSessions
            .AsNoTracking()
            .Where(s =>
                s.Status == "Paid"
                && (s.DeliveryEmailStatus == "Failed" || s.DeliveryEmailStatus == "NotSent")
                && s.DeliveryEmailAttempts < maxAttempts)
            .OrderBy(s => s.DeliveryEmailLastAttemptAt ?? s.PaidAt ?? s.CreatedAt)
            .Take(50)
            .Select(s => new
            {
                s.Id,
                s.ExternalReference,
                s.BuyerEmail,
                s.BuyerName,
                s.DeliveryEmailAttempts,
                s.DeliveryEmailLastAttemptAt,
                s.PaidAt,
                s.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return;

        var reattempted = 0;

        foreach (var candidate in candidates)
        {
            var delayMinutes = ResolveDelayMinutes(backoff, candidate.DeliveryEmailAttempts);
            var lastAttempt = candidate.DeliveryEmailLastAttemptAt ?? candidate.PaidAt ?? candidate.CreatedAt;
            if (now < lastAttempt.AddMinutes(delayMinutes))
                continue;

            var (success, message) = await photoDeliveryService.DeliverAsync(candidate.Id, cancellationToken);
            reattempted += 1;

            if (success)
            {
                _logger.LogInformation(
                    "Reintento automático de entrega exitoso. SessionId={SessionId}, ExternalReference={ExternalReference}, AttemptsBeforeRetry={Attempts}",
                    candidate.Id,
                    candidate.ExternalReference,
                    candidate.DeliveryEmailAttempts);
            }
            else
            {
                if (candidate.DeliveryEmailAttempts >= maxAttempts - 1 && !string.IsNullOrWhiteSpace(candidate.BuyerEmail))
                {
                    var buyerName = string.IsNullOrWhiteSpace(candidate.BuyerName) ? "Comprador" : candidate.BuyerName!;
                    try
                    {
                        await emailService.SendPhotoDeliveryExhaustedEmailAsync(candidate.BuyerEmail, buyerName, candidate.ExternalReference);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "No se pudo enviar email final de entrega en revisión. SessionId={SessionId}, ExternalReference={ExternalReference}",
                            candidate.Id,
                            candidate.ExternalReference);
                    }
                }

                _logger.LogWarning(
                    "Reintento automático de entrega falló. SessionId={SessionId}, ExternalReference={ExternalReference}, AttemptsBeforeRetry={Attempts}, Message={Message}",
                    candidate.Id,
                    candidate.ExternalReference,
                    candidate.DeliveryEmailAttempts,
                    message);
            }
        }

        if (reattempted > 0)
        {
            _logger.LogInformation("PhotoDeliveryRetryWorker ejecutó {Count} reintentos en este ciclo", reattempted);
        }
    }

    private static List<int> NormalizeBackoff(List<int>? raw)
    {
        if (raw == null || raw.Count == 0)
            return new List<int> { 0, 1, 5, 30, 120 };

        return raw
            .Select(value => Math.Clamp(value, 0, 24 * 60))
            .ToList();
    }

    private static int ResolveDelayMinutes(IReadOnlyList<int> backoff, int attemptsMade)
    {
        var index = Math.Clamp(attemptsMade, 0, backoff.Count - 1);
        return backoff[index];
    }
}
