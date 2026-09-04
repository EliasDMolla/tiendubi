using Admin.Entities;
using Admin.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Admin.WebApi.Services;

namespace Admin.WebApi.Controllers
{
    [ApiController]
    [Route("api/public")]
    [AllowAnonymous]
    public class PublicGalleryController : ControllerBase
    {
        private readonly Context _context;
        private readonly IR2StorageService _storageService;

        public PublicGalleryController(Context context, IR2StorageService storageService)
        {
            _context = context;
            _storageService = storageService;
        }

        [HttpGet("{slug}")]
        public async Task<IActionResult> GetStudio(string slug)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.PublicSlug == slug && u.IsActive);

            if (user == null)
                return NotFound(new { message = "Perfil público no encontrado" });

            var events = await _context.PhotographerEvents
                .AsNoTracking()
                .Where(e => e.UserId == user.Id && e.IsPublished)
                .Include(e => e.Photos)
                .Include(e => e.ProductAssets)
                .OrderByDescending(e => e.EventDate)
                .Select(e => new PublicEventCardDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    Description = e.Description,
                    EventDate = e.EventDate,
                    PricePerPhoto = e.PricePerPhoto,
                    OriginalPrice = e.OriginalPrice,
                    PriceType = e.PriceType,
                    ProductType = e.ProductType,
                    PaymentMethods = e.PaymentMethods,
                    PhotoCount = e.Photos.Count(p => p.IsProcessed && (!string.IsNullOrWhiteSpace(p.WatermarkedPath) || !string.IsNullOrWhiteSpace(p.ThumbnailPath))),
                    DigitalAssetCount = e.ProductAssets.Count(a => a.Kind == "digital_file"),
                    CoverPhotoUrl = !string.IsNullOrWhiteSpace(e.CoverImagePath) ? e.CoverImagePath : e.Photos
                        .Where(p => p.IsProcessed)
                        .OrderByDescending(p => p.CreatedAt)
                        .Select(p => !string.IsNullOrWhiteSpace(p.WatermarkedPath) ? p.WatermarkedPath : p.ThumbnailPath)
                        .FirstOrDefault()
                })
                .ToListAsync();

            foreach (var eventCard in events)
            {
                eventCard.CoverPhotoUrl = BuildPhotoUrl(eventCard.CoverPhotoUrl ?? string.Empty);
            }

            return Ok(new PublicStudioDto
            {
                UserId = user.Id,
                StudioName = user.FullName ?? user.Email,
                Slug = user.PublicSlug ?? slug,
                Theme = SiteThemeStore.Normalize(SiteThemeStore.Parse(user.PublicSiteThemeJson)),
                Events = events
            });
        }

        [HttpGet("{slug}/events/{eventId:int}")]
        public async Task<IActionResult> GetEvent(
            string slug,
            int eventId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 60,
            [FromQuery] string? q = null,
            [FromQuery] string? tag = null)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.PublicSlug == slug && u.IsActive);

            if (user == null)
                return NotFound(new { message = "Perfil público no encontrado" });

            var eventData = await _context.PhotographerEvents
                .AsNoTracking()
                .Where(e => e.Id == eventId && e.UserId == user.Id && e.IsPublished)
                .Select(e => new PublicEventDetailDto
                {
                    Id = e.Id,
                    StudioName = user.FullName ?? user.Email,
                    StudioSlug = user.PublicSlug ?? slug,
                    Name = e.Name,
                    Description = e.Description,
                    EventDate = e.EventDate,
                    PricePerPhoto = e.PricePerPhoto,
                    OriginalPrice = e.OriginalPrice,
                    PriceType = e.PriceType,
                    ProductType = e.ProductType,
                    PaymentMethods = e.PaymentMethods,
                    DigitalAssetCount = e.ProductAssets.Count(a => a.Kind == "digital_file"),
                    CoverPhotoUrl = e.CoverImagePath
                })
                .FirstOrDefaultAsync();

            if (eventData == null)
                return NotFound(new { message = "Evento público no encontrado" });

            eventData.CoverPhotoUrl = BuildPhotoUrl(eventData.CoverPhotoUrl ?? string.Empty);

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var photosQuery = _context.EventPhotos
                .AsNoTracking()
                .Where(p => p.PhotographerEventId == eventId)
                .Where(p => p.IsProcessed)
                .Where(p => !string.IsNullOrWhiteSpace(p.WatermarkedPath) || !string.IsNullOrWhiteSpace(p.ThumbnailPath));

            if (!string.IsNullOrWhiteSpace(tag))
            {
                var tagTerm = tag.Trim().ToLower();
                photosQuery = photosQuery.Where(p => (p.Tags ?? string.Empty).ToLower().Contains(tagTerm));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var searchTerm = q.Trim().ToLower();

                if (int.TryParse(searchTerm, out var photoIdTerm))
                {
                    photosQuery = photosQuery.Where(p =>
                        p.Id == photoIdTerm ||
                        p.OriginalFileName.ToLower().Contains(searchTerm) ||
                        (p.Tags ?? string.Empty).ToLower().Contains(searchTerm));
                }
                else
                {
                    photosQuery = photosQuery.Where(p =>
                        p.OriginalFileName.ToLower().Contains(searchTerm) ||
                        (p.Tags ?? string.Empty).ToLower().Contains(searchTerm));
                }
            }

            var totalPhotos = await photosQuery.CountAsync();
            var photos = await photosQuery
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PublicPhotoDto
                {
                    Id = p.Id,
                    OriginalFileName = p.OriginalFileName,
                    Url = !string.IsNullOrWhiteSpace(p.WatermarkedPath) ? p.WatermarkedPath! : p.ThumbnailPath!,
                    Tags = ParseTags(p.Tags).ToList()
                })
                .ToListAsync();

            eventData.TotalPhotos = totalPhotos;
            eventData.Page = page;
            eventData.PageSize = pageSize;
            eventData.HasMore = page * pageSize < totalPhotos;
            eventData.Photos = photos;

            foreach (var photo in eventData.Photos)
            {
                photo.Url = BuildPhotoUrl(photo.Url);
            }

            return Ok(eventData);
        }

        private string BuildPhotoUrl(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            var normalizedPath = path.TrimStart('/');
            try
            {
                return _storageService.GeneratePresignedGetUrl(normalizedPath, TimeSpan.FromHours(12));
            }
            catch
            {
                return string.Empty;
            }
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
    }
}
