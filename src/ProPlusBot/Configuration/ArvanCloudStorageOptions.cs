namespace ProPlusBot.Configuration;

public class ArvanCloudStorageOptions
{
    public const string SectionName = "ArvanCloudStorage";

    /// <summary>S3 API endpoint, e.g. https://s3.ir-central1.arvanstorage.ir (not the public CDN URL).</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>Arvan region code, e.g. ir-central1 — used when Endpoint is a public host.</summary>
    public string? Region { get; set; }

    public string AccessKey { get; set; } = "";

    public string SecretKey { get; set; } = "";

    public string BucketName { get; set; } = "";

    /// <summary>Optional prefix for object keys (no leading slash).</summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// Public URL base for downloaded files, e.g. https://mybucket.s3.ir-thr-at1.arvanstorage.ir
    /// When empty, derived from endpoint and bucket name.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(AccessKey)
        && !string.IsNullOrWhiteSpace(SecretKey)
        && !string.IsNullOrWhiteSpace(BucketName);
}
