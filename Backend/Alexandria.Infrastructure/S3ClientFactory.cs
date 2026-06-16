using Alexandria.Common.Config;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Infrastructure;

/// <summary>
/// Implementation of S3 client factory
/// </summary>
public class S3ClientFactory(
    IOptions<S3Config> options,
    ILogger<S3ClientFactory> logger)
    : IS3ClientFactory
{
    private readonly S3Config _options = options.Value;

    public IAmazonS3 CreateClient()
    {
        var resolved = _options.GetResolvedProviderSettings();
        return BuildClient(resolved.Endpoint, _options.UseHttps, resolved);
    }

    public IAmazonS3 CreatePublicClient()
    {
        var resolved = _options.GetResolvedProviderSettings();
        // Public client always uses HTTPS (going through nginx),
        // but endpoint is the public-facing URL
        return BuildClient(_options.PublicEndpoint, useHttp: false, resolved);
    }

    private AmazonS3Client BuildClient(string endpoint, bool useHttp, ProviderSettings resolved)
    {
        if (string.IsNullOrWhiteSpace(resolved.AccessKey) || string.IsNullOrWhiteSpace(resolved.SecretKey))
            throw new InvalidOperationException($"Missing S3 credentials for provider {_options.Provider}");

        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException($"Missing S3 endpoint for provider {_options.Provider}");
        

        var credentials = new BasicAWSCredentials(resolved.AccessKey, resolved.SecretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = resolved.ForcePathStyle,
            UseAccelerateEndpoint = resolved.UseAccelerateEndpoint,
            UseDualstackEndpoint = resolved.UseDualstackEndpoint,
            UseHttp = useHttp,
            AuthenticationRegion = resolved.Region
        };

        logger.LogInformation(
            "Creating S3 client | Provider={Provider} Endpoint={Endpoint} Region={Region}",
            _options.Provider, endpoint, resolved.Region);

        return new AmazonS3Client(credentials, config);
    }

    public S3Provider GetProvider() => _options.Provider;
}