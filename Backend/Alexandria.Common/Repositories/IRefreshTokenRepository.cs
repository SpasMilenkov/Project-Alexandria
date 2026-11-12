using Alexandria.Data.Models;

namespace Alexandria.Common.Repositories;

public interface IRefreshTokenRepository
{
    /// <summary>
    /// The token with its user, revoked ones included, so a refresh that lost a race to another
    /// tab can still find which session it belonged to.
    /// </summary>
    Task<RefreshToken?> GetByTokenWithUserAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Revokes the token only if nothing else has, in one statement. Exactly one of any number of
    /// concurrent callers gets true; the rest wait on the row lock and then see it revoked.
    /// </summary>
    Task<bool> RevokeIfActiveAsync(Guid id, DateTime revokedAt, CancellationToken ct = default);

    /// <summary>
    /// The newest live token of the session that started at <paramref name="sessionStartedAt"/>,
    /// issued after <paramref name="issuedAfter"/>: what a just-rotated token was replaced by.
    /// </summary>
    Task<RefreshToken?> GetLiveSuccessorAsync(Guid userId, DateTime sessionStartedAt, DateTime issuedAfter,
        DateTime now, CancellationToken ct = default);
    Task<RefreshToken> AddAsync(RefreshToken entity, CancellationToken ct = default);
    void Update(RefreshToken entity);
    Task<bool> RevokeTokenAsync(string token, CancellationToken ct = default);
    Task RevokeUserTokensAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Unrevoked, unexpired tokens of the user: one per signed-in device or browser.</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, DateTime now, CancellationToken ct = default);

    Task<RefreshToken?> GetActiveByIdAsync(Guid id, Guid userId, DateTime now, CancellationToken ct = default);
}