using DTO;
using Models;

namespace Common;

public interface IAuthService
{
    Task<AuthResult> AuthenticateAsync(string email, string password);
    Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<bool> RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    string GenerateAccessToken(ApplicationUser user);
    Task<string> GenerateRefreshTokenAsync(ApplicationUser user, CancellationToken ct = default);
    /**TODO: Call this from an endpoint later on,
     * I want it to require some additional auth step before implementing it
     */
    Task RevokeAllUserTokensAsync(Guid userId, CancellationToken ct = default);
}