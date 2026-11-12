using Alexandria.Data.Models;
using Alexandria.Dto;

namespace Alexandria.Common.Services;

public interface IAuthService
{
    /// <summary>Checks the credentials and records the attempt in the audit log either way.</summary>
    Task<AuthResult> AuthenticateAsync(string email, string password, CancellationToken ct = default);
    Task<AuthResult> GetSessionAsync(Guid userId, CancellationToken ct = default);
    Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<bool> RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Records the sign-out and revokes the session's refresh token when there is one.</summary>
    Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken ct = default);
    string GenerateAccessToken(ApplicationUser user);
    Task<string> GenerateRefreshTokenAsync(ApplicationUser user, CancellationToken ct = default);

    /**TODO: Call this from an endpoint later on,
     * I want it to require some additional auth step before implementing it
     */
    Task RevokeAllUserTokensAsync(Guid userId, CancellationToken ct = default);

    Task ChangeInitialPasswordAsync(Guid userId, string oldPassword, string newPassword,
        CancellationToken ct = default);
}