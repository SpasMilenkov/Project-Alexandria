namespace Alexandria.Common.Config;

/// <summary>
/// S3 Storage configuration options
/// </summary>
public class S3Config
{
    /// <summary>
    /// S3 provider type: Garage, RustFS, or MinIO
    /// </summary>
    public S3Provider Provider { get; set; } = S3Provider.Garage;

    /// <summary>
    /// Whether or not the traffic should go through over HTTP or HTTPS
    /// HTTP can be used on dev for direct work with garage 
    /// </summary>
    public bool UseHttps { get; set; } = true;

    /// <summary>
    /// S3 endpoint URL or internal calls from backend to garage directly
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// S3 endpoint URL used for presigned URL to go through NGINX
    /// </summary>
    public string PublicEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// The public NGINX URL. Used for streaming content.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// S3 access key
    /// </summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>
    /// S3 secret key
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// S3 region (provider-specific)
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Bucket for user uploads
    /// </summary>
    public string UploadBucket { get; set; } = "user-uploads";

    /// <summary>
    /// Bucket for preview/thumbnail files
    /// </summary>
    public string PreviewBucket { get; set; } = "user-previews";

    /// <summary>
    /// Bucket for temporary files
    /// </summary>
    public string TempBucket { get; set; } = "alexandria-temp";

    /// <summary>
    /// Bucket for storing background images, cors is enabled
    /// </summary>
    public string ImagesBucket { get; set; } = default!; // user images — CORS enabled

    /// <summary>
    /// Bucket for storing audio and video formatted for streaming
    /// </summary>
    public string StreamingBucket { get; set; } = "alexandria-streaming";

    /// <summary>
    /// Provider-specific settings
    /// </summary>
    public Dictionary<string, ProviderSettings> ProviderSettings { get; set; } = new();

    /// <summary>
    /// Gets the provider-specific settings for the current provider
    /// </summary>
    public ProviderSettings GetResolvedProviderSettings()
    {
        var providerKey = Provider.ToString();
        ProviderSettings.TryGetValue(providerKey, out var provider);

        static string? Coerce(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

        return new ProviderSettings
        {
            Endpoint = Coerce(provider?.Endpoint) ?? Coerce(Endpoint),
            AccessKey = Coerce(provider?.AccessKey) ?? Coerce(AccessKey),
            SecretKey = Coerce(provider?.SecretKey) ?? Coerce(SecretKey),
            Region = Coerce(provider?.Region) ?? Coerce(Region),
            ForcePathStyle = provider?.ForcePathStyle ?? true,
            UseAccelerateEndpoint = provider?.UseAccelerateEndpoint ?? false,
            UseDualstackEndpoint = provider?.UseDualstackEndpoint ?? false,
            MetricsToken = provider?.MetricsToken,
            MetricsUrl = provider?.MetricsUrl
        };
    }

    /// <summary>
    /// Gets the effective region for the current provider
    /// </summary>
    public string GetEffectiveRegion()
    {
        var providerSettings = GetResolvedProviderSettings();
        return !string.IsNullOrEmpty(providerSettings.Region)
            ? providerSettings.Region
            : Region;
    }
}

/// <summary>
/// S3 provider types
/// </summary>
public enum S3Provider
{
    /// <summary>
    /// Garage object storage
    /// </summary>
    Garage,

    /// <summary>
    /// RustFS object storage
    /// </summary>
    RustFS,

    /// <summary>
    /// MinIO object storage (legacy)
    /// </summary>
    MinIO
}

/// <summary>
/// Provider-specific S3 settings
/// </summary>
public class ProviderSettings
{
    public string? Endpoint { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }

    public string? Region { get; set; }
    public bool ForcePathStyle { get; set; } = true;
    public bool UseAccelerateEndpoint { get; set; } = false;
    public bool UseDualstackEndpoint { get; set; } = false;
    public string? MetricsUrl { get; set; }
    public string? MetricsToken { get; set; }
}