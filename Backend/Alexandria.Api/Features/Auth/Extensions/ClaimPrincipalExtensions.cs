using System.Security.Claims;

namespace Alexandria.Api.Features.Auth.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var userIdString =
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            user.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(userIdString))
            throw new UnauthorizedAccessException("User ID not found in token");

        return Guid.Parse(userIdString);
    }

    /// <summary>
    /// The session the access token was issued for, or null for a token from before tokens named
    /// their session.
    /// </summary>
    public static Guid? GetSessionId(this ClaimsPrincipal user)
    {
        var sessionId = user.FindFirst(ClaimTypes.Sid)?.Value ?? user.FindFirst("sid")?.Value;

        if (Guid.TryParse(sessionId, out var parsed))
            return parsed;

        return null;
    }
}