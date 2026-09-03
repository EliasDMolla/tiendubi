namespace Admin.WebApi.Services;

public interface IR2StorageService
{
    string GeneratePresignedPutUrl(string objectKey, TimeSpan expiresIn, string? contentType = null);
    string GeneratePresignedGetUrl(string objectKey, TimeSpan expiresIn);
    Task UploadAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}
