using Admin.Entities;
using Admin.Entities.Entities;
using Microsoft.EntityFrameworkCore;

namespace Admin.WebApi.Services;

public interface IPhotoDeliveryService
{
    Task<(bool Success, string Message)> DeliverAsync(int checkoutSessionId, CancellationToken cancellationToken = default);
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

    public async Task<(bool Success, string Message)> DeliverAsync(int checkoutSessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.PhotoCheckoutSessions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == checkoutSessionId, cancellationToken);

        if (session == null)
            return (false, "No se encontró la sesión de compra");

        if (!string.Equals(session.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            return (false, "La compra todavía no está pagada");

        if (string.Equals(session.DeliveryEmailStatus, "Sent", StringComparison.OrdinalIgnoreCase))
            return (true, "El email de entrega ya fue enviado");

        if (string.Equals(session.DeliveryEmailStatus, "NotRequired", StringComparison.OrdinalIgnoreCase))
            return (true, "Este producto no requiere entrega automática");

        var photoIds = ParsePhotoIds(session.PhotoIdsCsv);
        var productType = (session.Event?.ProductType ?? string.Empty).Trim().ToLowerInvariant();

        if (photoIds.Count == 0 && productType == "physical")
        {
            session.DeliveryEmailStatus = "NotRequired";
            session.DeliveryEmailError = null;
            await _context.SaveChangesAsync(cancellationToken);
            return (true, "Entrega física registrada para coordinar manualmente");
        }

        session.DeliveryEmailAttempts += 1;
        session.DeliveryEmailLastAttemptAt = DateTime.UtcNow;

        try
        {
            var result = photoIds.Count > 0
                ? await SendPhotosAsync(session, photoIds, cancellationToken)
                : await SendProductAsync(session, productType, cancellationToken);

            if (!result.Success)
            {
                session.DeliveryEmailStatus = "Failed";
                session.DeliveryEmailError = TruncateError(result.Message);
                await _context.SaveChangesAsync(cancellationToken);
                return result;
            }

            session.DeliveryEmailStatus = "Sent";
            session.DeliveryEmailSentAt = DateTime.UtcNow;
            session.DeliveryEmailError = null;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Entrega completada. SessionId={SessionId}, ExternalReference={ExternalReference}, Message={Message}", session.Id, session.ExternalReference, result.Message);
            return result;
        }
        catch (Exception ex)
        {
            session.DeliveryEmailStatus = "Failed";
            session.DeliveryEmailError = TruncateError(ex.Message);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Error entregando compra. SessionId={SessionId}, ExternalReference={ExternalReference}", session.Id, session.ExternalReference);
            return (false, "No se pudo completar la entrega");
        }
    }

    private async Task<(bool Success, string Message)> SendPhotosAsync(PhotoCheckoutSession session, List<int> photoIds, CancellationToken cancellationToken)
    {
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
            return (false, "No se encontraron las fotos compradas");

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

        return (true, "Email de entrega enviado");
    }

    private async Task<(bool Success, string Message)> SendProductAsync(PhotoCheckoutSession session, string productType, CancellationToken cancellationToken)
    {
        var eventInfo = await _context.PhotographerEvents
            .AsNoTracking()
            .Where(e => e.Id == session.EventId)
            .Select(e => new { e.Name, e.DeliveryLink, e.BuyerInstructions })
            .FirstOrDefaultAsync(cancellationToken);

        var productName = eventInfo?.Name ?? "tu producto";
        var buyerName = string.IsNullOrWhiteSpace(session.BuyerName) ? "Comprador" : session.BuyerName!;

        if (productType == "digital_link")
        {
            if (string.IsNullOrWhiteSpace(eventInfo?.DeliveryLink))
                return (false, "El producto no tiene link de entrega configurado");

            await _emailService.SendDigitalProductDeliveryEmailAsync(
                session.BuyerEmail,
                buyerName,
                productName,
                eventInfo!.DeliveryLink!,
                eventInfo.BuyerInstructions);

            return (true, "Link de entrega enviado al comprador");
        }

        if (productType == "digital_file")
        {
            var assets = await _context.ProductAssets
                .AsNoTracking()
                .Where(a => a.PhotographerEventId == session.EventId && a.Kind == "digital_file")
                .OrderBy(a => a.Id)
                .Select(a => new { a.Id, a.OriginalFileName, a.ObjectKey })
                .ToListAsync(cancellationToken);

            if (assets.Count == 0)
                return (false, "No se encontraron los archivos digitales del producto");

            var links = assets
                .Select(asset =>
                {
                    var presignedUrl = _storageService.GeneratePresignedGetUrl(asset.ObjectKey, TimeSpan.FromHours(24));
                    return new PhotoDeliveryLink(asset.Id, asset.OriginalFileName, presignedUrl);
                })
                .ToList();

            await _emailService.SendDigitalAssetsDeliveryEmailAsync(
                session.BuyerEmail,
                buyerName,
                productName,
                session.ExternalReference,
                links);

            return (true, "Archivos digitales enviados al comprador");
        }

        return (true, "Compra registrada");
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
