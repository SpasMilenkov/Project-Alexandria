using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto;
using Alexandria.Dto.Sessions;

namespace Alexandria.Common.Services;

public interface IAuthService
{
    /// <summary>Checks the credentials and records the attempt in the audit log either way.</summary>
    Task<AuthResult> AuthenticateAsync(string email, string password, CancellationToken ct = default);

    Task<AuthResult> GetSessionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Rotates the token. A token issued to the other kind of client is refused, and a native token
    /// spent past the grace period ends its whole session.
    /// </summary>
    Task<AuthResult> RefreshTokenAsync(string refreshToken, SessionClient client, CancellationToken ct = default);

    Task<bool> RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Records the sign-out and revokes the session's refresh token when there is one.</summary>
    Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken ct = default);

    /// <summary>
    /// The account's live sessions; the one whose id is <paramref name="currentSessionId"/>, taken
    /// from the caller's access token, is marked as the caller's own.
    /// </summary>
    Task<IReadOnlyList<UserSessionDto>> GetSessionsAsync(Guid userId, Guid? currentSessionId,
        CancellationToken ct = default);

    /// <summary>False when the session does not exist, belongs to someone else or already ended.</summary>
    Task<bool> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// An access token for <paramref name="user"/> naming <paramref name="sessionId"/>, which stays
    /// the same across refresh rotations, as its session.
    /// </summary>
    string GenerateAccessToken(ApplicationUser user, Guid sessionId);

    Task<RefreshToken> GenerateRefreshTokenAsync(ApplicationUser user, SessionClient client,
        CancellationToken ct = default);

    /**TODO: Call this from an endpoint later on,
     * I want it to require some additional auth step before implementing it
     */
    Task RevokeAllUserTokensAsync(Guid userId, CancellationToken ct = default);

    Task ChangeInitialPasswordAsync(Guid userId, string oldPassword, string newPassword,
        CancellationToken ct = default);
}