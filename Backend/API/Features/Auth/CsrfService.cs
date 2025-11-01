using System.Security.Cryptography;
using System.Text;

namespace API.Features.Auth;

public class CsrfService(IConfiguration config)
{
    private readonly string _secretKey = config["Csrf:SecretKey"] ?? throw new InvalidOperationException("CSRF secret key not configured");
    private readonly int _tokenExpiryMinutes = config.GetValue<int>("Csrf:ExpiryMinutes", 60);

    // Generate CSRF token bound to user ID and time window
    public (string cookieValue, string headerValue) GenerateToken(string userId)
    {
        var randomToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expiry = DateTimeOffset.UtcNow.AddMinutes(_tokenExpiryMinutes).ToUnixTimeSeconds();
        
        var signature = ComputeSignature(userId, randomToken, expiry);
        
        var cookieValue = $"{randomToken}.{expiry}.{signature}";
        
        var headerValue = randomToken;
        
        return (cookieValue, headerValue);
    }

    public bool ValidateToken(string? cookieValue, string? headerValue, string userId)
    {
        if (string.IsNullOrEmpty(cookieValue) || string.IsNullOrEmpty(headerValue))
            return false;

        var parts = cookieValue.Split('.');
        if (parts.Length != 3)
            return false;

        var randomToken = parts[0];
        var expiryStr = parts[1];
        var signature = parts[2];

        if (randomToken != headerValue)
            return false;

        if (!long.TryParse(expiryStr, out var expiry))
            return false;

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry)
            return false;

        var expectedSignature = ComputeSignature(userId, randomToken, expiry);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature),
            Encoding.UTF8.GetBytes(expectedSignature)
        );
    }

    private string ComputeSignature(string userId, string token, long expiry)
    {
        var message = $"{userId}:{token}:{expiry}";
        var keyBytes = Encoding.UTF8.GetBytes(_secretKey);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        
        var hash = HMACSHA256.HashData(keyBytes, messageBytes);
        return Convert.ToBase64String(hash);
    }
    
    public (string cookieValue, string headerValue) RegenerateToken(string userId)
    {
        return GenerateToken(userId);
    }
}