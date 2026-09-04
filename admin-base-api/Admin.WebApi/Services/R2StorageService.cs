using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Admin.WebApi.Services;

public class R2StorageService : IR2StorageService
{
    private readonly AmazonS3Client? _client;
    private readonly R2StorageOptions _options;
    private readonly ILogger<R2StorageService> _logger;
    private readonly bool _isConfigured;

    public R2StorageService(IOptions<R2StorageOptions> options, ILogger<R2StorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        _isConfigured = HasRealValue(_options.AccountId)
                        && HasRealValue(_options.AccessKeyId)
                        && HasRealValue(_options.SecretAccessKey)
                        && HasRealValue(_options.BucketName);

        if (!_isConfigured)
        {
            _logger.LogWarning("R2 deshabilitado o incompleto. Faltan credenciales/configuración (AccountId, AccessKeyId, SecretAccessKey o BucketName).");
            return;
        }

        var config = new AmazonS3Config
        {
            ServiceURL = _options.ServiceUrl,
            ForcePathStyle = true,
            SignatureVersion = "4",
            AuthenticationRegion = "auto"
        };

        var credentials = new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey);
        _client = new AmazonS3Client(credentials, config);
    }

    public string GeneratePresignedPutUrl(string objectKey, TimeSpan expiresIn, string? contentType = null)
    {
        EnsureConfigured();

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiresIn),
            ContentType = contentType
        };

        return _client!.GetPreSignedURL(request);
    }

    public string GeneratePresignedGetUrl(string objectKey, TimeSpan expiresIn)
    {
        if (!_isConfigured || _client == null)
        {
            var normalizedKey = objectKey.Trim().TrimStart('/').Replace('\\', '/');
            return $"/uploads/{Uri.EscapeDataString(normalizedKey).Replace("%2F", "/")}";
        }

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiresIn)
        };

        return _client!.GetPreSignedURL(request);
    }

    public async Task UploadAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var putRequest = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = content,
            AutoCloseStream = false,
            ContentType = contentType,
            UseChunkEncoding = false,
            DisablePayloadSigning = true
        };

        await _client!.PutObjectAsync(putRequest, cancellationToken);
    }

    public async Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var response = await _client!.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey
        }, cancellationToken);

        var ms = new MemoryStream();
        await response.ResponseStream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;
        return ms;
    }

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        try
        {
            await _client!.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey
            }, cancellationToken);

            return true;
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
            string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("R2 metadata check: object not found. Bucket={BucketName}, ObjectKey={ObjectKey}", _options.BucketName, objectKey);
            return false;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(
                ex,
                "R2 metadata check failed. Bucket={BucketName}, ObjectKey={ObjectKey}, StatusCode={StatusCode}, ErrorCode={ErrorCode}",
                _options.BucketName,
                objectKey,
                ex.StatusCode,
                ex.ErrorCode);

            throw;
        }
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        await _client!.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey
        }, cancellationToken);
    }

    private void EnsureConfigured()
    {
        if (_isConfigured && _client != null)
            return;

        throw new InvalidOperationException("R2 no está configurado. Definí R2__AccountId, R2__AccessKeyId, R2__SecretAccessKey y R2__BucketName.");
    }

    private static bool HasRealValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        return !normalized.Equals("REPLACE", StringComparison.OrdinalIgnoreCase)
               && !normalized.Equals("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
               && !normalized.StartsWith("your-", StringComparison.OrdinalIgnoreCase);
    }
}
