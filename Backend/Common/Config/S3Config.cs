namespace Common.Config;

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
    /// S3 endpoint URL
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

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

        return new ProviderSettings
        {
            Endpoint = provider?.Endpoint ?? Endpoint,
            AccessKey = provider?.AccessKey ?? AccessKey,
            SecretKey = provider?.SecretKey ?? SecretKey,
            Region = provider?.Region ?? Region,
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
