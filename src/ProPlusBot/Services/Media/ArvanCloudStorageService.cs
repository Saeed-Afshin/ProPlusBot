using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;

namespace ProPlusBot.Services.Media;

public class ArvanCloudStorageService(
    IOptions<ArvanCloudStorageOptions> options,
    ILogger<ArvanCloudStorageService> logger)
{
    private readonly ArvanCloudStorageOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public async Task<string> UploadPublicAsync(
        string localFilePath,
        string objectKey,
        string contentType,
        CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("ArvanCloud storage is not configured.");

        var fullKey = BuildObjectKey(objectKey);
        await using var stream = File.OpenRead(localFilePath);

        using var client = CreateClient();
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = fullKey,
            InputStream = stream,
            ContentType = contentType,
            CannedACL = S3CannedACL.PublicRead,
            AutoCloseStream = false
        };

        await client.PutObjectAsync(request, ct);
        var publicUrl = BuildPublicUrl(fullKey);
        logger.LogInformation("Uploaded fallback object {Key} ({Size} bytes)", fullKey, stream.Length);
        return publicUrl;
    }

    public async Task DeleteAsync(string storageObjectKey, CancellationToken ct)
    {
        if (!IsConfigured)
            return;

        try
        {
            using var client = CreateClient();
            await client.DeleteObjectAsync(_options.BucketName, storageObjectKey, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete Arvan object {Key}", storageObjectKey);
        }
    }

    public string BuildObjectKey(string fileName)
    {
        var prefix = string.IsNullOrWhiteSpace(_options.KeyPrefix)
            ? ""
            : _options.KeyPrefix.Trim().TrimEnd('/') + "/";
        return prefix + fileName;
    }

    private string BuildPublicUrl(string objectKey)
    {
        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
            return $"{NormalizeUrl(_options.PublicBaseUrl).TrimEnd('/')}/{objectKey}";

        var endpoint = ResolveS3ApiEndpoint(out _);
        if (endpoint.Contains(_options.BucketName, StringComparison.OrdinalIgnoreCase))
            return $"{endpoint}/{objectKey}";

        var host = new Uri(endpoint).Host;
        return $"https://{_options.BucketName}.{host}/{objectKey}";
    }

    private AmazonS3Client CreateClient()
    {
        var apiEndpoint = ResolveS3ApiEndpoint(out var region);
        var config = new AmazonS3Config
        {
            ServiceURL = apiEndpoint,
            ForcePathStyle = true,
            AuthenticationRegion = region
        };

        return new AmazonS3Client(_options.AccessKey, _options.SecretKey, config);
    }

    /// <summary>
    /// Arvan exposes S3 API at s3.&lt;region&gt;.arvanstorage.ir.
    /// Public URLs use &lt;bucket&gt;.hot.&lt;region&gt;.arvanstorage.ir — using those for API causes HTTP 301.
    /// </summary>
    private string ResolveS3ApiEndpoint(out string authenticationRegion)
    {
        var normalized = NormalizeUrl(_options.Endpoint);
        var host = new Uri(normalized).Host;

        if (host.StartsWith("s3.", StringComparison.OrdinalIgnoreCase))
        {
            authenticationRegion = ResolveRegion(host, _options.Region) ?? "us-east-1";
            return normalized;
        }

        var region = !string.IsNullOrWhiteSpace(_options.Region)
            ? _options.Region.Trim()
            : TryExtractRegionFromArvanHost(host);

        if (!string.IsNullOrWhiteSpace(region))
        {
            var apiEndpoint = $"https://s3.{region}.arvanstorage.ir";
            if (!string.Equals(normalized, apiEndpoint, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "ArvanCloud Endpoint {Configured} is a public/CDN host, not the S3 API. Using {ApiEndpoint}. " +
                    "Set Endpoint to the S3 API URL (s3.<region>.arvanstorage.ir) in appsettings.",
                    _options.Endpoint,
                    apiEndpoint);
            }

            authenticationRegion = region;
            return apiEndpoint;
        }

        authenticationRegion = "us-east-1";
        logger.LogWarning(
            "ArvanCloud Endpoint {Endpoint} does not look like an S3 API host. Set Region or use s3.<region>.arvanstorage.ir",
            _options.Endpoint);
        return normalized;
    }

    private static string? ResolveRegion(string s3Host, string? configuredRegion)
    {
        if (!string.IsNullOrWhiteSpace(configuredRegion))
            return configuredRegion.Trim();

        return TryExtractRegionFromArvanHost(s3Host);
    }

    private static string? TryExtractRegionFromArvanHost(string host)
    {
        var parts = host.Split('.');
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Equals("hot", StringComparison.OrdinalIgnoreCase)
                || parts[i].Equals("cold", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < parts.Length)
                    return parts[i + 1];
            }
        }

        if (parts.Length >= 2 && parts[0].Equals("s3", StringComparison.OrdinalIgnoreCase))
            return parts[1];

        return null;
    }

    private static string NormalizeUrl(string value)
    {
        var trimmed = value.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(trimmed))
            throw new InvalidOperationException("ArvanCloud endpoint URL is empty.");

        if (!trimmed.Contains("://", StringComparison.Ordinal))
            trimmed = "https://" + trimmed;

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"ArvanCloud endpoint is not a valid URL: {value}. Use e.g. https://s3.ir-central1.arvanstorage.ir");
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }

    public static string GuessContentType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".mp4" => "video/mp4",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".ogg" => "audio/ogg",
            ".opus" => "audio/opus",
            ".wav" => "audio/wav",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
}
