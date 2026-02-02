using System.Security.Claims;

namespace API.Features.Auth.Extensions;

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
}