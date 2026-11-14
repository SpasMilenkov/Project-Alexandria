using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Builder.Tests.Integration;

internal static class S3PresignedRequest
{
    public static Uri Sign(Uri origin, string bucket, string key, string method, string accessKey, string secretKey,
        IReadOnlyDictionary<string, string>? responseParameters = null)
    {
        var now = DateTimeOffset.UtcNow;
        var day = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var scope = day + "/garage/s3/aws4_request";
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-Amz-Algorithm"] = "AWS4-HMAC-SHA256",
            ["X-Amz-Credential"] = accessKey + "/" + scope,
            ["X-Amz-Date"] = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture),
            ["X-Amz-Expires"] = "300",
            ["X-Amz-SignedHeaders"] = "host",
        };

        if (responseParameters is not null)
        {
            foreach (var pair in responseParameters) query[pair.Key] = pair.Value;
        }

        var path = "/" + bucket + "/" + string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
        var canonicalQuery = string.Join('&', query.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
        var canonicalRequest = method + "\n" + path + "\n" + canonicalQuery + "\n" +
            "host:" + origin.Authority + "\n\nhost\nUNSIGNED-PAYLOAD";
        var toSign = "AWS4-HMAC-SHA256\n" + query["X-Amz-Date"] + "\n" + scope + "\n" +
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));
        var dateKey = Mac(Encoding.UTF8.GetBytes("AWS4" + secretKey), day);
        var regionKey = Mac(dateKey, "garage");
        var serviceKey = Mac(regionKey, "s3");
        var signingKey = Mac(serviceKey, "aws4_request");
        var signature = Convert.ToHexStringLower(Mac(signingKey, toSign));

        return new Uri(origin.GetLeftPart(UriPartial.Authority) + path + "?" + canonicalQuery + "&X-Amz-Signature=" + signature);
    }

    private static byte[] Mac(byte[] key, string value) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value));
}
