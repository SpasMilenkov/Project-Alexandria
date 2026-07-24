using System.Threading.RateLimiting;

namespace Alexandria.Services.Streaming.Lyrics;

public sealed class RateLimitingHandler(RateLimiter limiter) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        using var lease = await limiter.AcquireAsync(1, ct);

        if (!lease.IsAcquired)
            throw new HttpRequestException("LRCLIB rate limit exceeded, request was rejected.");

        return await base.SendAsync(request, ct);
    }
}