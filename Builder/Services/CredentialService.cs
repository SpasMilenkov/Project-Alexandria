using System.Security.Cryptography;
using Builder.Models;

namespace Builder.Services;

public interface ICredentialService
{
    Dictionary<string, string> GenerateAllCredentials(FeatureSelection features);
    string GenerateHexSecret(int bytes = 32);
    string GeneratePassword(int length = 24);
    string GenerateAccessKey(int length = 20);
}

public class CredentialService : ICredentialService
{
    private const string PasswordChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
    private const string AccessKeyChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public Dictionary<string, string> GenerateAllCredentials(FeatureSelection features)
    {
        var credentials = new Dictionary<string, string>
        {
            ["DB_PASSWORD"] = GeneratePassword(),
            ["GARAGE_RPC_SECRET"] = GenerateHexSecret(),
            ["GARAGE_ADMIN_TOKEN"] = GenerateHexSecret(),
            ["GARAGE_METRICS_TOKEN"] = GenerateHexSecret(),
            ["GARAGE_S3_ACCESS_KEY"] = GenerateAccessKey(),
            ["GARAGE_S3_SECRET_KEY"] = GeneratePassword(),
            ["RABBITMQ_USER"] = "alexandria",
            ["RABBITMQ_PASSWORD"] = GeneratePassword(),
            ["RABBITMQ_VHOST"] = "alexandria",
            ["JWT_SECRET"] = GenerateHexSecret(),
            ["JWT_ISSUER"] = "alexandria",
            ["JWT_AUDIENCE"] = "alexandria-users",
            ["CSRF_SECRET"] = GenerateHexSecret(),
            ["CORS_ALLOWED_ORIGINS"] = "",
        };

        if (features.Monitoring)
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

    public string GenerateAccessKey(int length = 20)
    {
        return GenerateFromCharset(AccessKeyChars, length);
    }

    private static string GenerateFromCharset(string charset, int length)
    {
        var result = new char[length];
        var bytes = RandomNumberGenerator.GetBytes(length);

        for (var i = 0; i < length; i++)
        {
            result[i] = charset[bytes[i] % charset.Length];
        }

        return new string(result);
    }
}
