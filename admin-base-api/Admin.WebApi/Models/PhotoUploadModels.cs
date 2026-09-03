using Microsoft.AspNetCore.Http;

namespace Admin.WebApi.Models;

public class PresignedUrlsRequest
{
    public int EventId { get; set; }
    public List<string> FileNames { get; set; } = new();
}

public class PresignedUrlItem
{
    public string FileName { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string UploadUrl { get; set; } = string.Empty;
}

public class PresignedUrlsResponse
{
    public int EventId { get; set; }
    public List<PresignedUrlItem> Files { get; set; } = new();
}

public class ConfirmUploadRequest
{
    public int EventId { get; set; }
    public List<UploadedFileItem> Files { get; set; } = new();
}

public class UploadedFileItem
{
    public string FileName { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

public class ConfirmUploadResponse
{
    public int EventId { get; set; }
    public int SavedCount { get; set; }
    public List<int> PhotoIds { get; set; } = new();
    public List<UploadedFileItem> MissingFiles { get; set; } = new();
}

public class DownloadPhotoResponse
{
    public int PhotoId { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public int ExpiresInSeconds { get; set; }
}

public class PhotoProcessingStatusResponse
{
    public int EventId { get; set; }
    public int TotalPhotos { get; set; }
    public int ProcessedPhotos { get; set; }
    public int FailedPhotos { get; set; }
    public int PendingPhotos { get; set; }
    public int ProgressPercent { get; set; }
    public List<PhotoProcessingItemDto> RecentPhotos { get; set; } = new();
}

public class BatchProcessingStatusRequest
{
    public List<int> PhotoIds { get; set; } = new();
}

public class PhotoProcessingItemDto
{
    public int PhotoId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public bool IsProcessed { get; set; }
    public bool IsFailed { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProxyUploadRequest
{
    public int EventId { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public IFormFile? File { get; set; }
}

public class ProxyUploadResponse
{
    public int EventId { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

public class PhotographerGalleryPhotoDto
{
    public int PhotoId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public bool IsProcessed { get; set; }
    public bool IsFailed { get; set; }
    public string? FailureReason { get; set; }
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string PreviewUrl { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class UpdatePhotoTagsRequest
{
    public List<string> Tags { get; set; } = new();
}

public class PhotographerGalleryResponse
{
    public int EventId { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<PhotographerGalleryPhotoDto> Items { get; set; } = new();
}
