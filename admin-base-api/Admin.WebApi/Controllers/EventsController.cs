using Admin.Entities;
using Admin.Entities.Entities;
using Admin.WebApi.Models;
using Admin.WebApi.Services;
using Amazon.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Admin.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EventsController : ControllerBase
    {
        private readonly Context _context;
        private readonly ILogger<EventsController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IR2StorageService _storageService;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        private static readonly HashSet<string> AllowedDigitalExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".zip", ".rar", ".7z", ".mp4", ".mov", ".mp3", ".wav", ".jpg", ".jpeg", ".png", ".webp", ".txt", ".csv", ".xlsx", ".doc", ".docx"
        };

        private const int FreePlanMaxItems = 3;
        private const int ProPlanMaxItems = 50;
        private const long FreePlanMaxDigitalFileBytes = 500L * 1024 * 1024;
        private const long ProPlanMaxDigitalFileBytes = 1L * 1024 * 1024 * 1024;
        private const long MaxUploadRequestBytes = 1100L * 1024 * 1024;

        public EventsController(Context context, ILogger<EventsController> logger, IWebHostEnvironment environment, IR2StorageService storageService)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
            _storageService = storageService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyEvents()
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var events = await _context.PhotographerEvents
                .AsNoTracking()
                .Where(e => e.UserId == userId.Value)
                .Include(e => e.Photos)
                .Include(e => e.ProductAssets)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();

            var mappedEvents = events.Select(MapEvent).ToList();

            return Ok(mappedEvents);
        }

        [HttpPost]
        public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var planAccess = await ResolvePlanAccessAsync(userId.Value);
            var itemCount = await _context.PhotographerEvents.CountAsync(e => e.UserId == userId.Value);
            if (itemCount >= planAccess.MaxItems)
            {
                return BadRequest(new
                {
                    message = planAccess.IsPro
                        ? $"Alcanzaste el límite de {ProPlanMaxItems} productos del plan Pro."
                        : $"El plan gratuito permite hasta {FreePlanMaxItems} productos. Actualizá a Pro para crear más."
                });
            }

            var validationError = ValidateProductRequest(request.Name, request.Description, request.PriceType, request.ProductType, request.PricePerPhoto, request.OriginalPrice, request.DeliveryLink);
            if (validationError is not null)
                return BadRequest(new { message = validationError });

            var priceType = NormalizePriceType(request.PriceType);
            var productType = NormalizeProductType(request.ProductType);
            var paymentMethods = NormalizePaymentMethods(request.PaymentMethods, priceType);

            var newEvent = new PhotographerEvent
            {
                UserId = userId.Value,
                Name = request.Name.Trim(),
                Description = NormalizeOptional(request.Description),
                EventDate = request.EventDate == default ? DateTime.UtcNow : request.EventDate.ToUniversalTime(),
                PricePerPhoto = priceType == "free" ? 0 : request.PricePerPhoto,
                OriginalPrice = request.OriginalPrice,
                PriceType = priceType,
                ProductType = productType,
                PaymentMethods = paymentMethods,
                BuyerInstructions = NormalizeOptional(request.BuyerInstructions),
                DeliveryLink = productType == "digital_link" ? NormalizeOptional(request.DeliveryLink) : null,
                IsPublished = request.IsPublished,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.PhotographerEvents.Add(newEvent);
            await _context.SaveChangesAsync();

            return Ok(MapEvent(newEvent));
        }

        [HttpPut("{eventId:int}")]
        public async Task<IActionResult> UpdateEvent(int eventId, [FromBody] UpdateEventRequest request)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var planAccess = await ResolvePlanAccessAsync(userId.Value);
            if (!planAccess.IsPro)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "El plan gratuito no permite editar productos. Actualizá a Pro para modificarlos."
                });
            }

            var dbEvent = await _context.PhotographerEvents
                .Include(e => e.Photos)
                .Include(e => e.ProductAssets)
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId.Value);

            if (dbEvent == null)
                return NotFound(new { message = "Evento no encontrado" });

            var validationError = ValidateProductRequest(request.Name, request.Description, request.PriceType, request.ProductType, request.PricePerPhoto, request.OriginalPrice, request.DeliveryLink);
            if (validationError is not null)
                return BadRequest(new { message = validationError });

            var priceType = NormalizePriceType(request.PriceType);
            var productType = NormalizeProductType(request.ProductType);
            var paymentMethods = NormalizePaymentMethods(request.PaymentMethods, priceType);

            dbEvent.Name = request.Name.Trim();
            dbEvent.Description = NormalizeOptional(request.Description);
            dbEvent.EventDate = request.EventDate == default ? dbEvent.EventDate : request.EventDate.ToUniversalTime();
            dbEvent.PricePerPhoto = priceType == "free" ? 0 : request.PricePerPhoto;
            dbEvent.OriginalPrice = request.OriginalPrice;
            dbEvent.PriceType = priceType;
            dbEvent.ProductType = productType;
            dbEvent.PaymentMethods = paymentMethods;
            dbEvent.BuyerInstructions = NormalizeOptional(request.BuyerInstructions);
            dbEvent.DeliveryLink = productType == "digital_link" ? NormalizeOptional(request.DeliveryLink) : null;
            dbEvent.IsPublished = request.IsPublished;
            dbEvent.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(MapEvent(dbEvent));
        }

        [HttpDelete("{eventId:int}")]
        public async Task<IActionResult> DeleteEvent(int eventId, CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var planAccess = await ResolvePlanAccessAsync(userId.Value);
            if (!planAccess.IsPro)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "El plan gratuito no permite eliminar productos. Actualizá a Pro."
                });
            }

            var dbEvent = await _context.PhotographerEvents
                .Include(e => e.Photos)
                .Include(e => e.ProductAssets)
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId.Value, cancellationToken);

            if (dbEvent == null)
                return NotFound(new { message = "Producto no encontrado" });

            var hasSales = await _context.PhotoSales.AnyAsync(s => s.PhotographerEventId == eventId, cancellationToken);
            var hasOrders = await _context.Orders.AnyAsync(o => o.EventId == eventId, cancellationToken);
            var hasCheckoutSessions = await _context.PhotoCheckoutSessions.AnyAsync(s => s.EventId == eventId, cancellationToken);
            if (hasSales || hasOrders || hasCheckoutSessions)
                return BadRequest(new { message = "No se puede eliminar un producto con ventas o compras iniciadas. Podés dejarlo como borrador." });

            var objectKeys = dbEvent.ProductAssets
                .Select(a => a.ObjectKey)
                .Append(dbEvent.CoverImagePath)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key!.Trim().TrimStart('/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var objectKey in objectKeys)
            {
                try
                {
                    await _storageService.DeleteAsync(objectKey, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo eliminar objeto R2 al borrar producto. EventId={EventId}, ObjectKey={ObjectKey}", eventId, objectKey);
                }
            }

            DeleteLocalPhotoFiles(dbEvent.Photos);

            _context.PhotographerEvents.Remove(dbEvent);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new { message = "Producto eliminado" });
        }

        [HttpPost("{eventId:int}/assets")]
        [RequestSizeLimit(MaxUploadRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadRequestBytes)]
        public async Task<IActionResult> UploadProductAsset(int eventId, [FromForm] ProductAssetUploadRequest request, CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var dbEvent = await _context.PhotographerEvents
                .Include(e => e.ProductAssets)
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId.Value, cancellationToken);

            if (dbEvent == null)
                return NotFound(new { message = "Producto no encontrado" });

            if (request.File is null || request.File.Length <= 0)
                return BadRequest(new { message = "Debes enviar un archivo" });

            var kind = string.Equals(request.Kind, "cover", StringComparison.OrdinalIgnoreCase)
                ? "cover"
                : "digital_file";

            var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
            if (kind == "cover")
            {
                if (!AllowedExtensions.Contains(extension))
                    return BadRequest(new { message = "La portada debe ser PNG, JPG o WEBP" });

                if (request.File.Length > 5 * 1024 * 1024)
                    return BadRequest(new { message = "La portada no puede superar 5MB" });
            }
            else
            {
                if (!AllowedDigitalExtensions.Contains(extension))
                    return BadRequest(new { message = $"Formato no soportado: {request.File.FileName}" });

                var planAccess = await ResolvePlanAccessAsync(userId.Value);
                var maxFileBytes = planAccess.IsPro
                    ? ProPlanMaxDigitalFileBytes
                    : FreePlanMaxDigitalFileBytes;
                var maxFileLabel = planAccess.IsPro ? "1 GB" : "500 MB";

                if (request.File.Length > maxFileBytes)
                {
                    return BadRequest(new
                    {
                        message = $"{request.File.FileName} supera el límite de {maxFileLabel} de tu plan."
                    });
                }

                var digitalAssetCount = dbEvent.ProductAssets.Count(a => a.Kind == "digital_file");
                if (digitalAssetCount >= 3)
                    return BadRequest(new { message = "Puedes subir hasta 3 archivos digitales por producto" });
            }

            var objectKey = kind == "cover"
                ? $"products/{eventId}/cover/{Guid.NewGuid():N}{extension}"
                : $"products/{eventId}/files/{Guid.NewGuid():N}{extension}";

            await using var stream = request.File.OpenReadStream();
            var contentType = string.IsNullOrWhiteSpace(request.File.ContentType)
                ? ResolveContentType(extension)
                : request.File.ContentType;

            try
            {
                await _storageService.UploadAsync(objectKey, stream, contentType, cancellationToken);
            }
            catch (AmazonS3Exception ex) when (string.Equals(ex.ErrorCode, "NoSuchBucket", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(ex, "No se pudo subir asset de producto porque el bucket R2 configurado no existe. EventId={EventId}, ObjectKey={ObjectKey}", eventId, objectKey);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "El bucket configurado en R2 no existe. Revisá R2:BucketName y que pertenezca al R2:AccountId configurado."
                });
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "No se pudo conectar con R2 al subir un asset de producto. EventId={EventId}, ObjectKey={ObjectKey}, ErrorCode={ErrorCode}", eventId, objectKey, ex.ErrorCode);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "No se pudo conectar con el almacenamiento en la nube. Revisá las credenciales y el bucket configurados en el servidor."
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "No se pudo subir asset de producto por configuracion de R2. EventId={EventId}, ObjectKey={ObjectKey}", eventId, objectKey);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = ex.Message
                });
            }

            if (kind == "cover")
            {
                dbEvent.CoverImagePath = objectKey;
            }

            var asset = new ProductAsset
            {
                PhotographerEventId = eventId,
                Kind = kind,
                OriginalFileName = Path.GetFileName(request.File.FileName),
                ObjectKey = objectKey,
                ContentType = contentType,
                SizeBytes = request.File.Length,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ProductAssets.Add(asset);
            dbEvent.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(MapProductAsset(asset, includeUrl: true));
        }

        [HttpPost("{eventId:int}/photos")]
        [RequestSizeLimit(200_000_000)]
        public async Task<IActionResult> UploadPhotos(int eventId, [FromForm] List<IFormFile> files)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var dbEvent = await _context.PhotographerEvents
                .Include(e => e.Photos)
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId.Value);

            if (dbEvent == null)
                return NotFound(new { message = "Evento no encontrado" });

            if (files == null || files.Count == 0)
                return BadRequest(new { message = "Debes enviar al menos una foto" });

            var rootPath = _environment.ContentRootPath;
            var eventFolder = Path.Combine(rootPath, "uploads", "events", userId.Value.ToString(), eventId.ToString());
            Directory.CreateDirectory(eventFolder);

            var uploaded = new List<EventPhotoDto>();

            foreach (var file in files)
            {
                if (file.Length == 0)
                    continue;

                var extension = Path.GetExtension(file.FileName);
                if (!AllowedExtensions.Contains(extension))
                    return BadRequest(new { message = $"Formato no soportado: {file.FileName}" });

                if (file.Length > 20 * 1024 * 1024)
                    return BadRequest(new { message = $"{file.FileName} supera 20MB" });

                var safeName = Path.GetFileName(file.FileName);
                var storedFileName = $"{Guid.NewGuid():N}{extension}";
                var fullPath = Path.Combine(eventFolder, storedFileName);

                await using (var stream = System.IO.File.Create(fullPath))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = $"events/{userId.Value}/{eventId}/{storedFileName}";

                var photo = new EventPhoto
                {
                    PhotographerEventId = dbEvent.Id,
                    OriginalFileName = safeName,
                    StoredFileName = storedFileName,
                    RelativePath = relativePath.Replace('\\', '/'),
                    SizeBytes = file.Length,
                    WatermarkApplied = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.EventPhotos.Add(photo);
                dbEvent.Photos.Add(photo);

                uploaded.Add(MapPhoto(photo));
            }

            dbEvent.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { photos = uploaded, uploadedCount = uploaded.Count });
        }

        [HttpDelete("{eventId:int}/photos/{photoId:int}")]
        public async Task<IActionResult> DeletePhoto(int eventId, int photoId)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var photo = await _context.EventPhotos
                .Include(p => p.PhotographerEvent)
                .FirstOrDefaultAsync(p => p.Id == photoId && p.PhotographerEventId == eventId && p.PhotographerEvent.UserId == userId.Value);

            if (photo == null)
                return NotFound(new { message = "Foto no encontrada" });

            var absolutePath = Path.Combine(_environment.ContentRootPath, "uploads", photo.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(absolutePath))
            {
                System.IO.File.Delete(absolutePath);
            }

            _context.EventPhotos.Remove(photo);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Foto eliminada" });
        }

        private void DeleteLocalPhotoFiles(IEnumerable<EventPhoto> photos)
        {
            foreach (var photo in photos)
            {
                DeleteLocalUploadFile(photo.RelativePath);
                DeleteLocalUploadFile(photo.OriginalPath);
                DeleteLocalUploadFile(photo.ThumbnailPath);
                DeleteLocalUploadFile(photo.WatermarkedPath);
            }
        }

        private void DeleteLocalUploadFile(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            var normalizedPath = relativePath.Trim().TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var absolutePath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "uploads", normalizedPath));
            var uploadsRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "uploads"));

            if (!absolutePath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
                return;

            if (System.IO.File.Exists(absolutePath))
                System.IO.File.Delete(absolutePath);
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(claim, out var id))
                return id;
            return null;
        }

        private async Task<(bool IsPro, int MaxItems)> ResolvePlanAccessAsync(int userId)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return (false, 0);

            var isAdmin = user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin;
            var isPro = isAdmin || user.IsProActive;
            var maxItems = isAdmin ? int.MaxValue : (isPro ? ProPlanMaxItems : FreePlanMaxItems);

            return (isPro, maxItems);
        }

        private PhotographerEventDto MapEvent(PhotographerEvent dbEvent)
        {
            var photos = dbEvent.Photos?.OrderByDescending(p => p.CreatedAt).Select(MapPhoto).ToList() ?? new List<EventPhotoDto>();
            var assets = dbEvent.ProductAssets?.OrderByDescending(a => a.CreatedAt).Select(a => MapProductAsset(a, includeUrl: false)).ToList() ?? new List<ProductAssetDto>();

            return new PhotographerEventDto
            {
                Id = dbEvent.Id,
                Name = dbEvent.Name,
                Description = dbEvent.Description,
                EventDate = dbEvent.EventDate,
                PricePerPhoto = dbEvent.PricePerPhoto,
                OriginalPrice = dbEvent.OriginalPrice,
                PriceType = dbEvent.PriceType,
                ProductType = dbEvent.ProductType,
                PaymentMethods = dbEvent.PaymentMethods,
                BuyerInstructions = dbEvent.BuyerInstructions,
                DeliveryLink = dbEvent.DeliveryLink,
                CoverImageUrl = BuildProductAssetUrl(dbEvent.CoverImagePath),
                IsPublished = dbEvent.IsPublished,
                PhotoCount = photos.Count,
                CreatedAt = dbEvent.CreatedAt,
                Photos = photos,
                ProductAssets = assets
            };
        }

        private EventPhotoDto MapPhoto(EventPhoto photo)
        {
            var previewUrl = BuildSecurePreviewUrl(photo);

            return new EventPhotoDto
            {
                Id = photo.Id,
                OriginalFileName = photo.OriginalFileName,
                Url = previewUrl,
                SizeBytes = photo.SizeBytes,
                WatermarkApplied = photo.WatermarkApplied,
                CreatedAt = photo.CreatedAt
            };
        }

        private string BuildSecurePreviewUrl(EventPhoto photo)
        {
            var previewPath = !string.IsNullOrWhiteSpace(photo.WatermarkedPath)
                ? photo.WatermarkedPath
                : photo.ThumbnailPath;

            if (string.IsNullOrWhiteSpace(previewPath))
                return string.Empty;

            var objectKey = previewPath.Trim().TrimStart('/');

            try
            {
                return _storageService.GeneratePresignedGetUrl(objectKey, TimeSpan.FromMinutes(30));
            }
            catch
            {
                return string.Empty;
            }
        }

        private ProductAssetDto MapProductAsset(ProductAsset asset, bool includeUrl)
        {
            return new ProductAssetDto
            {
                Id = asset.Id,
                Kind = asset.Kind,
                OriginalFileName = asset.OriginalFileName,
                ObjectKey = asset.ObjectKey,
                Url = includeUrl ? BuildProductAssetUrl(asset.ObjectKey) : null,
                ContentType = asset.ContentType,
                SizeBytes = asset.SizeBytes,
                CreatedAt = asset.CreatedAt
            };
        }

        private string? BuildProductAssetUrl(string? objectKey)
        {
            if (string.IsNullOrWhiteSpace(objectKey))
                return null;

            try
            {
                return _storageService.GeneratePresignedGetUrl(objectKey.Trim().TrimStart('/'), TimeSpan.FromHours(2));
            }
            catch
            {
                return null;
            }
        }

        private static string? ValidateProductRequest(
            string name,
            string? description,
            string? priceType,
            string? productType,
            decimal price,
            decimal? originalPrice,
            string? deliveryLink)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "El nombre del producto es obligatorio";

            if (name.Trim().Length > 100)
                return "El nombre no puede superar 100 caracteres";

            if (!string.IsNullOrWhiteSpace(description) && description.Trim().Length > 3000)
                return "La descripción no puede superar 3000 caracteres";

            var normalizedPriceType = NormalizePriceType(priceType);
            if (normalizedPriceType == "paid" && price <= 0)
                return "El precio debe ser mayor a 0";

            if (price < 0)
                return "El precio no puede ser negativo";

            if (originalPrice.HasValue && originalPrice.Value < 0)
                return "El precio original no puede ser negativo";

            if (originalPrice.HasValue && normalizedPriceType == "paid" && originalPrice.Value <= price)
                return "El precio original debe ser mayor al precio actual";

            var normalizedProductType = NormalizeProductType(productType);
            if (normalizedProductType == "digital_link" && string.IsNullOrWhiteSpace(deliveryLink))
                return "Agrega el link de entrega para el producto digital";

            return null;
        }

        private static string NormalizePriceType(string? value)
        {
            return string.Equals(value, "free", StringComparison.OrdinalIgnoreCase) ? "free" : "paid";
        }

        private static string NormalizeProductType(string? value)
        {
            return value?.Trim().ToLowerInvariant() switch
            {
                "digital_link" => "digital_link",
                "physical" => "physical",
                _ => "digital_file"
            };
        }

        private static string NormalizePaymentMethods(string? value, string priceType)
        {
            if (priceType == "free")
                return "free";

            var methods = (value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(method => method.ToLowerInvariant())
                .Where(method => method is "mercadopago" or "transfer")
                .Distinct()
                .ToList();

            return methods.Count == 0 ? "mercadopago" : string.Join(',', methods);
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string ResolveContentType(string extension) => extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            ".csv" => "text/csv",
            ".txt" => "text/plain",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };
    }
}
