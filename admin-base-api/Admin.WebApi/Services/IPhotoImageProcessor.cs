namespace Admin.WebApi.Services;

public interface IPhotoImageProcessor
{
    Task<Stream> CreateThumbnailAsync(Stream originalImage, CancellationToken cancellationToken = default);
    Task<Stream> CreateWatermarkedAsync(Stream originalImage, string watermarkText, CancellationToken cancellationToken = default);
}
