namespace Admin.WebApi.Services;

public class R2StorageOptions
{
    public string AccountId { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;

    public string ServiceUrl => $"https://{AccountId}.r2.cloudflarestorage.com";
}
