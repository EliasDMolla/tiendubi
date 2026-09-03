using Admin.Entities;
using Admin.Entities.Entities;
using Admin.WebApi.Models;
using Admin.WebApi.Services;
using Amazon.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Amazon.S3.Model;

namespace Admin.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PhotosController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly Context _context;
    private readonly IR2StorageService _storageService;
    private readonly IPhotoProcessingQueue _processingQueue;
    private readonly ILogger<PhotosController> _logger;
    private readonly FeatureSettings _featureSettings;

    public PhotosController(
        Context context,
        IR2StorageService storageService,
        IPhotoProcessingQueue processingQueue,
        ILogger<PhotosController> logger,
        IOptions<FeatureSettings> featureSettings)
    {
        _context = context;
        _storageService = storageService;
        _processingQueue = processingQueue;
        _logger = logger;
        _featureSettings = featureSettings.Value;
    }

    [HttpPost("presigned-urls")]
    public async Task<IActionResult> GetPresignedUrls([FromBody] PresignedUrlsRequest request)
    {
        var blockedResponse = EnsurePhotoUploadEnabled();
        if (blockedResponse is not null)
            return blockedResponse;

        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        if (request.EventId <= 0 || request.FileNames.Count == 0)
            return BadRequest(new { message = "Debes enviar eventId y al menos un archivo" });

        var ownsEvent = await _context.PhotographerEvents.AnyAsync(e => e.Id == request.EventId && e.UserId == userId.Value);
        if (!ownsEvent)
            return NotFound(new { message = "Evento no encontrado" });

        var response = new PresignedUrlsResponse
        {
            EventId = request.EventId
        };

        foreach (var fileName in request.FileNames.Where(f => !string.IsNullOrWhiteSpace(f)))
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                continue;

            var objectKey = $"events/{request.EventId}/originals/{Guid.NewGuid():N}{ext}";
            var uploadUrl = _storageService.GeneratePresignedPutUrl(objectKey, TimeSpan.FromMinutes(15));

            response.Files.Add(new PresignedUrlItem
            {
                FileName = fileName,
                ObjectKey = objectKey,
                UploadUrl = uploadUrl
            });
        }

        return Ok(response);
    }

    [HttpPost("confirm-upload")]
    public async Task<IActionResult> ConfirmUpload([FromBody] ConfirmUploadRequest request)
    {
        var blockedResponse = EnsurePhotoUploadEnabled();
        if (blockedResponse is not null)
            return blockedResponse;

        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        if (request.EventId <= 0 || request.Files.Count == 0)
            return BadRequest(new { message = "Debes enviar eventId y archivos subidos" });

        var dbEvent = await _context.PhotographerEvents
            .FirstOrDefaultAsync(e => e.Id == request.EventId && e.UserId == userId.Value);

        if (dbEvent == null)
            return NotFound(new { message = "Evento no encontrado" });

        var photosToPersist = new List<EventPhoto>();
        var missingFiles = new List<UploadedFileItem>();

        foreach (var file in request.Files)
        {
            if (string.IsNullOrWhiteSpace(file.ObjectKey) || string.IsNullOrWhiteSpace(file.FileName))
                continue;

            if (!file.ObjectKey.StartsWith($"events/{request.EventId}/originals/", StringComparison.OrdinalIgnoreCase))
            {
                missingFiles.Add(file);
                continue;
            }

            var existsInR2 = await VerifyObjectExistsQuickAsync(file.ObjectKey, HttpContext.RequestAborted);
            if (!existsInR2)
            {
                missingFiles.Add(file);
                continue;
            }

            var baseName = Path.GetFileNameWithoutExtension(file.ObjectKey);

            var thumbPath = $"events/{request.EventId}/thumbs/{baseName}.jpg";
            var watermarkedPath = $"events/{request.EventId}/watermarked/{baseName}.jpg";

            var photo = new EventPhoto
            {
                PhotographerEventId = request.EventId,
                OriginalFileName = Path.GetFileName(file.FileName),
                StoredFileName = Path.GetFileName(file.ObjectKey),
                RelativePath = file.ObjectKey,
                OriginalPath = file.ObjectKey,
                ThumbnailPath = thumbPath,
                WatermarkedPath = watermarkedPath,
                SizeBytes = file.SizeBytes,
                IsProcessed = false,
                ProcessingFailed = false,
                ProcessingError = null,
                WatermarkApplied = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            photosToPersist.Add(photo);
            _context.EventPhotos.Add(photo);
        }

        await _context.SaveChangesAsync();

        var photoIds = photosToPersist.Select(p => p.Id).ToList();
        foreach (var photoId in photoIds)
        {
            await _processingQueue.EnqueueAsync(photoId);
        }

        return Ok(new ConfirmUploadResponse
        {
            EventId = request.EventId,
            SavedCount = photoIds.Count,
            PhotoIds = photoIds,
            MissingFiles = missingFiles
        });
    }

    [HttpPost("upload-proxy")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> UploadProxy([FromForm] ProxyUploadRequest request, CancellationToken cancellationToken)
    {
        var blockedResponse = EnsurePhotoUploadEnabled();
        if (blockedResponse is not null)
            return blockedResponse;

        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        if (request.EventId <= 0 || request.File is null || request.File.Length <= 0)
            return BadRequest(new { message = "Debes enviar eventId, objectKey y archivo" });

        if (string.IsNullOrWhiteSpace(request.ObjectKey) || string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest(new { message = "Debes enviar objectKey y fileName" });

        var ownsEvent = await _context.PhotographerEvents.AnyAsync(e => e.Id == request.EventId && e.UserId == userId.Value, cancellationToken);
        if (!ownsEvent)
            return NotFound(new { message = "Evento no encontrado" });

        if (!request.ObjectKey.StartsWith($"events/{request.EventId}/originals/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "objectKey inválido para el evento" });

        var ext = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { message = "Tipo de archivo no soportado" });

        await using var stream = request.File.OpenReadStream();
        var contentType = string.IsNullOrWhiteSpace(request.File.ContentType)
            ? ResolveContentType(ext)
            : request.File.ContentType;

        await _storageService.UploadAsync(request.ObjectKey, stream, contentType, cancellationToken);

        return Ok(new ProxyUploadResponse
        {
            EventId = request.EventId,
            ObjectKey = request.ObjectKey,
            FileName = request.FileName,
            SizeBytes = request.File.Length
        });
    }

    [HttpGet("{photoId:int}/download")]
    public async Task<IActionResult> GetDownloadUrl(int photoId)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var photo = await _context.EventPhotos
            .Include(p => p.PhotographerEvent)
            .FirstOrDefaultAsync(p => p.Id == photoId);

        if (photo == null)
            return NotFound(new { message = "Foto no encontrada" });

        if (!photo.IsProcessed)
            return Conflict(new { message = "La foto aún está en procesamiento" });

        var isEventOwner = photo.PhotographerEvent.UserId == userId.Value;
        var requesterEmail = User.FindFirst(ClaimTypes.Email)?.Value;

        var hasPaidAccess = !string.IsNullOrWhiteSpace(requesterEmail)
            && await HasPaidAccessToPhotoAsync(photo.PhotographerEventId, photo.Id, requesterEmail!, HttpContext.RequestAborted);

        if (!isEventOwner && !hasPaidAccess)
            return Forbid();

        var downloadUrl = _storageService.GeneratePresignedGetUrl(photo.OriginalPath, TimeSpan.FromMinutes(5));

        return Ok(new DownloadPhotoResponse
        {
            PhotoId = photo.Id,
            DownloadUrl = downloadUrl,
            ExpiresInSeconds = 300
        });
    }

    [HttpGet("events/{eventId:int}/processing-status")]
    public async Task<IActionResult> GetProcessingStatus(int eventId)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var ownsEvent = await _context.PhotographerEvents.AnyAsync(e => e.Id == eventId && e.UserId == userId.Value);
        if (!ownsEvent)
            return NotFound(new { message = "Evento no encontrado" });

        var photos = await _context.EventPhotos
            .AsNoTracking()
            .Where(p => p.PhotographerEventId == eventId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var total = photos.Count;
        var processed = photos.Count(p => p.IsProcessed);
        var failed = photos.Count(p => p.ProcessingFailed && !p.IsProcessed);
        var pending = total - processed - failed;
        var completed = processed + failed;
        var percent = total == 0 ? 0 : (int)Math.Round((completed * 100d) / total);

        var response = new PhotoProcessingStatusResponse
        {
            EventId = eventId,
            TotalPhotos = total,
            ProcessedPhotos = processed,
            FailedPhotos = failed,
            PendingPhotos = pending,
            ProgressPercent = Math.Clamp(percent, 0, 100),
            RecentPhotos = photos
                .Take(30)
                .Select(p => new PhotoProcessingItemDto
                {
                    PhotoId = p.Id,
                    FileName = p.OriginalFileName,
                    IsProcessed = p.IsProcessed,
                    IsFailed = p.ProcessingFailed && !p.IsProcessed,
                    FailureReason = p.ProcessingFailed && !p.IsProcessed ? p.ProcessingError : null,
                    CreatedAt = p.CreatedAt
                })
                .ToList()
        };

        return Ok(response);
    }

    [HttpPost("events/{eventId:int}/batch-processing-status")]
    public async Task<IActionResult> GetBatchProcessingStatus(int eventId, [FromBody] BatchProcessingStatusRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var ownsEvent = await _context.PhotographerEvents.AnyAsync(e => e.Id == eventId && e.UserId == userId.Value);
        if (!ownsEvent)
            return NotFound(new { message = "Evento no encontrado" });

        var photoIds = (request?.PhotoIds ?? new List<int>())
            .Where(id => id > 0)
            .Distinct()
            .Take(1000)
            .ToList();

        if (photoIds.Count == 0)
        {
            return Ok(new PhotoProcessingStatusResponse
            {
                EventId = eventId,
                TotalPhotos = 0,
                ProcessedPhotos = 0,
                FailedPhotos = 0,
                PendingPhotos = 0,
                ProgressPercent = 0,
                RecentPhotos = new List<PhotoProcessingItemDto>()
            });
        }

        var photos = await _context.EventPhotos
            .AsNoTracking()
            .Where(p => p.PhotographerEventId == eventId && photoIds.Contains(p.Id))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var total = photos.Count;
        var processed = photos.Count(p => p.IsProcessed);
        var failed = photos.Count(p => p.ProcessingFailed && !p.IsProcessed);
        var pending = total - processed - failed;
        var completed = processed + failed;
        var percent = total == 0 ? 0 : (int)Math.Round((completed * 100d) / total);

        var response = new PhotoProcessingStatusResponse
        {
            EventId = eventId,
            TotalPhotos = total,
            ProcessedPhotos = processed,
            FailedPhotos = failed,
            PendingPhotos = pending,
            ProgressPercent = Math.Clamp(percent, 0, 100),
            RecentPhotos = photos
                .Take(30)
                .Select(p => new PhotoProcessingItemDto
                {
                    PhotoId = p.Id,
                    FileName = p.OriginalFileName,
                    IsProcessed = p.IsProcessed,
                    IsFailed = p.ProcessingFailed && !p.IsProcessed,
                    FailureReason = p.ProcessingFailed && !p.IsProcessed ? p.ProcessingError : null,
                    CreatedAt = p.CreatedAt
                })
                .ToList()
        };

        return Ok(response);
    }

    [HttpGet("events/{eventId:int}/gallery")]
    public async Task<IActionResult> GetPhotographerGallery(int eventId, [FromQuery] int page = 1, [FromQuery] int pageSize = 24, [FromQuery] string? search = null)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var ownsEvent = await _context.PhotographerEvents.AnyAsync(e => e.Id == eventId && e.UserId == userId.Value);
        if (!ownsEvent)
            return NotFound(new { message = "Evento no encontrado" });

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var baseQuery = _context.EventPhotos
            .AsNoTracking()
            .Where(p => p.PhotographerEventId == eventId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            baseQuery = baseQuery.Where(p =>
                p.OriginalFileName.ToLower().Contains(normalizedSearch) ||
                (p.Tags != null && p.Tags.ToLower().Contains(normalizedSearch)));
        }

        baseQuery = baseQuery.OrderByDescending(p => p.CreatedAt);

        var totalCount = await baseQuery.CountAsync();
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)safePageSize);
        safePage = Math.Min(safePage, totalPages);

        var photos = await baseQuery
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        var items = photos.Select(p =>
        {
            var thumbnailPath = !string.IsNullOrWhiteSpace(p.ThumbnailPath)
                ? p.ThumbnailPath!
                : p.WatermarkedPath ?? p.OriginalPath;

            var previewPath = !string.IsNullOrWhiteSpace(p.WatermarkedPath)
                ? p.WatermarkedPath!
                : p.OriginalPath;

            return new PhotographerGalleryPhotoDto
            {
                PhotoId = p.Id,
                FileName = p.OriginalFileName,
                IsProcessed = p.IsProcessed,
                IsFailed = p.ProcessingFailed && !p.IsProcessed,
                FailureReason = p.ProcessingFailed && !p.IsProcessed ? p.ProcessingError : null,
                ThumbnailUrl = _storageService.GeneratePresignedGetUrl(thumbnailPath, TimeSpan.FromHours(6)),
                PreviewUrl = _storageService.GeneratePresignedGetUrl(previewPath, TimeSpan.FromHours(6)),
                Tags = ParseTags(p.Tags).ToList(),
                CreatedAt = p.CreatedAt
            };
        }).ToList();

        return Ok(new PhotographerGalleryResponse
        {
            EventId = eventId,
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = items
        });
    }

    [HttpPost("events/{eventId:int}/retry-failed")]
    public async Task<IActionResult> RetryFailed(int eventId)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var ownsEvent = await _context.PhotographerEvents.AnyAsync(e => e.Id == eventId && e.UserId == userId.Value);
        if (!ownsEvent)
            return NotFound(new { message = "Evento no encontrado" });

        var failedPhotos = await _context.EventPhotos
            .Where(p => p.PhotographerEventId == eventId && p.ProcessingFailed && !p.IsProcessed)
            .ToListAsync();

        if (failedPhotos.Count == 0)
            return Ok(new { eventId, retriedCount = 0 });

        foreach (var photo in failedPhotos)
        {
            photo.ProcessingFailed = false;
            photo.ProcessingError = null;
            photo.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        foreach (var photo in failedPhotos)
        {
            await _processingQueue.EnqueueAsync(photo.Id);
        }

        return Ok(new { eventId, retriedCount = failedPhotos.Count });
    }

    [HttpDelete("events/{eventId:int}/failed")]
    public async Task<IActionResult> DeleteFailed(int eventId)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var ownsEvent = await _context.PhotographerEvents.AnyAsync(e => e.Id == eventId && e.UserId == userId.Value);
        if (!ownsEvent)
            return NotFound(new { message = "Evento no encontrado" });

        var failedPhotos = await _context.EventPhotos
            .Where(p => p.PhotographerEventId == eventId && p.ProcessingFailed && !p.IsProcessed)
            .ToListAsync();

        if (failedPhotos.Count == 0)
            return Ok(new { eventId, deletedCount = 0 });

        _context.EventPhotos.RemoveRange(failedPhotos);
        await _context.SaveChangesAsync();

        return Ok(new { eventId, deletedCount = failedPhotos.Count });
    }

    [HttpPut("events/{eventId:int}/photos/{photoId:int}/tags")]
    public async Task<IActionResult> UpdatePhotoTags(int eventId, int photoId, [FromBody] UpdatePhotoTagsRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var photo = await _context.EventPhotos
            .Include(p => p.PhotographerEvent)
            .FirstOrDefaultAsync(p => p.Id == photoId && p.PhotographerEventId == eventId, cancellationToken);

        if (photo == null)
            return NotFound(new { message = "Foto no encontrada" });

        if (photo.PhotographerEvent.UserId != userId.Value)
            return Forbid();

        var normalizedTags = NormalizeTags(request?.Tags);
        photo.Tags = SerializeTags(normalizedTags);
        photo.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            eventId,
            photoId,
            tags = normalizedTags
        });
    }

    [HttpDelete("events/{eventId:int}/photos/{photoId:int}")]
    public async Task<IActionResult> DeletePhoto(int eventId, int photoId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var photo = await _context.EventPhotos
            .Include(p => p.PhotographerEvent)
            .FirstOrDefaultAsync(p => p.Id == photoId && p.PhotographerEventId == eventId, cancellationToken);

        if (photo == null)
            return NotFound(new { message = "Foto no encontrada" });

        if (photo.PhotographerEvent.UserId != userId.Value)
            return Forbid();

        var objectKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizeObjectKey(photo.OriginalPath),
            NormalizeObjectKey(photo.ThumbnailPath),
            NormalizeObjectKey(photo.WatermarkedPath),
            NormalizeObjectKey(photo.RelativePath)
        };

        foreach (var key in objectKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            try
            {
                await _storageService.DeleteAsync(key, cancellationToken);
            }
            catch (AmazonS3Exception ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
            {
            }
        }

        _context.EventPhotos.Remove(photo);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { eventId, photoId, deleted = true });
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private static string ResolveContentType(string extension) => extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };

    private IActionResult? EnsurePhotoUploadEnabled()
    {
        if (_featureSettings.PhotoUploadEnabled)
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, new { message = "La subida de fotos está deshabilitada temporalmente" });
    }

    private static string NormalizeObjectKey(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Trim();
        if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        normalized = normalized.TrimStart('/');
        if (normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring("uploads/".Length);
        }

        return normalized;
    }

    private static IReadOnlyList<string> ParseTags(string? rawTags)
    {
        if (string.IsNullOrWhiteSpace(rawTags))
        {
            return Array.Empty<string>();
        }

        return rawTags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags == null)
        {
            return new List<string>();
        }

        return tags
            .Select(t => t?.Trim() ?? string.Empty)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Length > 40 ? t[..40] : t)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }

    private static string? SerializeTags(IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
        {
            return null;
        }

        var value = string.Join(',', tags);
        return value.Length <= 500 ? value : value[..500];
    }

    private async Task<bool> VerifyObjectExistsQuickAsync(string objectKey, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        var delayMs = 120;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var exists = await _storageService.ExistsAsync(objectKey, cancellationToken);
                if (exists)
                {
                    return true;
                }
            }
            catch (AmazonS3Exception ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
            {
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(delayMs, cancellationToken);
                delayMs = Math.Min(delayMs * 2, 600);
            }
        }

        return false;
    }

    private async Task<bool> HasPaidAccessToPhotoAsync(int eventId, int photoId, string buyerEmail, CancellationToken cancellationToken)
    {
        if (eventId <= 0 || photoId <= 0 || string.IsNullOrWhiteSpace(buyerEmail))
            return false;

        var normalizedEmail = buyerEmail.Trim().ToLowerInvariant();

        var paidSessions = await _context.PhotoCheckoutSessions
            .AsNoTracking()
            .Where(s =>
                s.EventId == eventId
                && s.Status == "Paid"
                && s.BuyerEmail.ToLower() == normalizedEmail)
            .Select(s => s.PhotoIdsCsv)
            .ToListAsync(cancellationToken);

        if (paidSessions.Count == 0)
            return false;

        return paidSessions
            .SelectMany(ParsePhotoIdsCsv)
            .Any(id => id == photoId);
    }

    private static IEnumerable<int> ParsePhotoIdsCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return Array.Empty<int>();

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0);
    }

}
