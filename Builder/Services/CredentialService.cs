using System.Security.Cryptography;
using Builder.Models;

namespace Builder.Services;

public interface ICredentialService
{
    Dictionary<string, string> GenerateAllCredentials(FeatureSelection features);

    string GenerateHexSecret(int bytes = 32);

    string GeneratePassword(int length = 24);
}

// Random secret source only. Garage S3 keys are intentionally NOT produced
// here: they are created inside the cluster by garage-init and merged into
// the .env afterwards (locked decision D6).
public class CredentialService : ICredentialService
{
    private const string PasswordChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-";

    public Dictionary<string, string> GenerateAllCredentials(FeatureSelection features)
    {
        var credentials = new Dictionary<string, string>
        {
            // Database
            ["DB_PASSWORD"] = GeneratePassword(),

            // Garage cluster secrets (S3 application keys come from garage-init)
            ["GARAGE_RPC_SECRET"] = GenerateHexSecret(),
            ["GARAGE_ADMIN_TOKEN"] = GenerateHexSecret(),
            ["GARAGE_METRICS_TOKEN"] = GenerateHexSecret(),

            // RabbitMQ — one identity per consumer, rendered into broker
            // definitions by the rabbitmq-config container
            ["RABBITMQ_USER"] = "alexandria",
            ["RABBITMQ_PASSWORD"] = GeneratePassword(),
            ["RABBITMQ_VHOST"] = "alexandria",
            ["API_RABBITMQ_USER"] = "api_user",
            ["API_RABBITMQ_PASSWORD"] = GeneratePassword(),
            ["RABBITMQ_DOCUMENT_WORKER"] = "document_worker_user",
            ["DOCUMENT_WORKER_RABBITMQ_PASSWORD"] = GeneratePassword(),
            ["RABBITMQ_MEDIA_WORKER"] = "media_worker_user",
            ["MEDIA_WORKER_RABBITMQ_PASSWORD"] = GeneratePassword(),
            ["RABBITMQ_TRANSPILATION_WORKER"] = "media_transpilation_worker",
            ["TRANSPILATION_WORKER_RABBITMQ_PASSWORD"] = GeneratePassword(),
            ["RABBITMQ_MEDIA_METADATA_WORKER"] = "media_metadata_worker_user",
            ["MEDIA_METADATA_WORKER_RABBITMQ_PASSWORD"] = GeneratePassword(),
            ["RABBITMQ_LYRICS_WORKER"] = "lyrics_worker_user",
            ["LYRICS_WORKER_RABBITMQ_PASSWORD"] = GeneratePassword(),
            ["RABBITMQ_PLAYLIST_WORKER"] = "playlist_worker_user",
            ["PLAYLIST_WORKER_RABBITMQ_PASSWORD"] = GeneratePassword(),
            ["RABBITMQ_ESSENTIA_WORKER"] = "essentia_worker_user",
            ["ESSENTIA_WORKER_RABBITMQ_PASSWORD"] = GeneratePassword(),

            // Application secrets
            ["JWT_SECRET"] = GenerateHexSecret(64),
            ["JWT_ISSUER"] = "alexandria",
            ["JWT_AUDIENCE"] = "alexandria-users",
            ["CSRF_SECRET"] = GenerateHexSecret(16),

            // Monitoring
            ["GRAFANA_USER"] = "admin",
            ["POSTGRES_EXPORTER_PASSWORD"] = GeneratePassword(),
        };

        if (features.IsEnabled(FeatureCatalog.Monitoring.Id))
        {
            credentials["GRAFANA_ADMIN_PASSWORD"] = GeneratePassword();
        }

        return credentials;
    }

    public string GenerateHexSecret(int bytes = 32)
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();
    }

    public string GeneratePassword(int length = 24)
    {
        return GenerateFromCharset(PasswordChars, length);
    }

    private static string GenerateFromCharset(string charset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        var result = new char[length];

        for (var i = 0; i < length; i++)
        {
            result[i] = charset[RandomNumberGenerator.GetInt32(charset.Length)];
        }

        return new string(result);
    }
}
