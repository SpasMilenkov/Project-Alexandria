using Amazon.Runtime;
using Amazon.S3;
using Common.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure;

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

        if (string.IsNullOrWhiteSpace(resolved.AccessKey) ||
            string.IsNullOrWhiteSpace(resolved.SecretKey))
        {
            throw new InvalidOperationException(
                $"Missing S3 credentials for provider {_options.Provider}"
            );
        }

        if (string.IsNullOrWhiteSpace(resolved.Endpoint))
        {
            throw new InvalidOperationException(
                $"Missing S3 endpoint for provider {_options.Provider}"
            );
        }

        var credentials = new BasicAWSCredentials(
            resolved.AccessKey,
            resolved.SecretKey
        );

        var config = new AmazonS3Config
        {
            ServiceURL = resolved.Endpoint,
            ForcePathStyle = resolved.ForcePathStyle,
            UseAccelerateEndpoint = resolved.UseAccelerateEndpoint,
            UseDualstackEndpoint = resolved.UseDualstackEndpoint,
            UseHttp = !resolved.Endpoint.StartsWith("https", StringComparison.OrdinalIgnoreCase),
            AuthenticationRegion = resolved.Region
        };

        logger.LogInformation(
            "Creating S3 client | Provider={Provider} Endpoint={Endpoint} Region={Region}",
            _options.Provider,
            resolved.Endpoint,
            resolved.Region
        );

        return new AmazonS3Client(credentials, config);
    }

    public S3Provider GetProvider() => _options.Provider;
}