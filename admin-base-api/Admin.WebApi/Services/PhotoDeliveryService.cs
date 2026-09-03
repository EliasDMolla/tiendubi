using Admin.Entities;
using Admin.Entities.Entities;
using Microsoft.EntityFrameworkCore;

namespace Admin.WebApi.Services;

public interface IPhotoDeliveryService
{
    Task<(bool Success, string Message)> SendPurchasedPhotosAsync(int checkoutSessionId, CancellationToken cancellationToken = default);
}

public class PhotoDeliveryService : IPhotoDeliveryService
{
    private readonly Context _context;
    private readonly IR2StorageService _storageService;
    private readonly IEmailService _emailService;
    private readonly ILogger<PhotoDeliveryService> _logger;

    public PhotoDeliveryService(
        Context context,
        IR2StorageService storageService,
        IEmailService emailService,
        ILogger<PhotoDeliveryService> logger)
    {
        _context = context;
        _storageService = storageService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<(bool Success, string Message)> SendPurchasedPhotosAsync(int checkoutSessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.PhotoCheckoutSessions
            .FirstOrDefaultAsync(s => s.Id == checkoutSessionId, cancellationToken);

        if (session == null)
            return (false, "No se encontró la sesión de compra");

        if (!string.Equals(session.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            return (false, "La compra todavía no está pagada");

        if (string.Equals(session.DeliveryEmailStatus, "Sent", StringComparison.OrdinalIgnoreCase))
            return (true, "El email de entrega ya fue enviado");

        session.DeliveryEmailAttempts += 1;
        session.DeliveryEmailLastAttemptAt = DateTime.UtcNow;

        try
        {
            var photoIds = ParsePhotoIds(session.PhotoIdsCsv);
            if (photoIds.Count == 0)
            {
                session.DeliveryEmailStatus = "Failed";
                session.DeliveryEmailError = "No hay fotos válidas para enviar";
                await _context.SaveChangesAsync(cancellationToken);
                return (false, session.DeliveryEmailError);
            }

            var eventName = await _context.PhotographerEvents
                .AsNoTracking()
                .Where(e => e.Id == session.EventId)
                .Select(e => e.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "tu evento";

            var photos = await _context.EventPhotos
                .AsNoTracking()
                .Where(p => p.PhotographerEventId == session.EventId && photoIds.Contains(p.Id))
                .Select(p => new { p.Id, p.OriginalFileName, p.OriginalPath, p.RelativePath })
                .ToListAsync(cancellationToken);

            if (photos.Count == 0)
            {
                session.DeliveryEmailStatus = "Failed";
                session.DeliveryEmailError = "No se encontraron las fotos compradas";
                await _context.SaveChangesAsync(cancellationToken);
                return (false, session.DeliveryEmailError);
            }

            var links = photos
                .Select(photo =>
                {
                    var objectKey = ResolveObjectKey(photo.OriginalPath, photo.RelativePath);
                    var presignedUrl = _storageService.GeneratePresignedGetUrl(objectKey, TimeSpan.FromHours(24));
                    return new PhotoDeliveryLink(photo.Id, photo.OriginalFileName, presignedUrl);
                })
                .ToList();

            var buyerName = string.IsNullOrWhiteSpace(session.BuyerName) ? "Comprador" : session.BuyerName!;
            await _emailService.SendPurchasedPhotosEmailAsync(session.BuyerEmail, buyerName, eventName, session.ExternalReference, links);

            session.DeliveryEmailStatus = "Sent";
            session.DeliveryEmailSentAt = DateTime.UtcNow;
            session.DeliveryEmailError = null;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Email de entrega enviado correctamente. SessionId={SessionId}, ExternalReference={ExternalReference}", session.Id, session.ExternalReference);
            return (true, "Email de entrega enviado");
        }
        catch (Exception ex)
        {
            session.DeliveryEmailStatus = "Failed";
            session.DeliveryEmailError = TruncateError(ex.Message);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Error enviando email de entrega de fotos. SessionId={SessionId}, ExternalReference={ExternalReference}", session.Id, session.ExternalReference);
            return (false, "No se pudo enviar el email de entrega");
        }
    }

    private static List<int> ParsePhotoIds(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return new List<int>();

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private static string ResolveObjectKey(string? originalPath, string? relativePath)
    {
        var source = !string.IsNullOrWhiteSpace(originalPath)
            ? originalPath
            : relativePath;

        if (string.IsNullOrWhiteSpace(source))
            throw new InvalidOperationException("La foto no tiene ruta en almacenamiento");

        return source.Trim().TrimStart('/');
    }

    private static string TruncateError(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Error de envío no especificado";

        return raw.Length <= 1000 ? raw : raw[..1000];
    }
}
