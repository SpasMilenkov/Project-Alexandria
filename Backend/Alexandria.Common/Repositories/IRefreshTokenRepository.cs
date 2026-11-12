using Alexandria.Data.Models;

namespace Alexandria.Common.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenWithUserAsync(string token, CancellationToken ct = default);
    Task<RefreshToken> AddAsync(RefreshToken entity, CancellationToken ct = default);
    void Update(RefreshToken entity);
    Task<bool> RevokeTokenAsync(string token, CancellationToken ct = default);
    Task RevokeUserTokensAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Unrevoked, unexpired tokens of the user: one per signed-in device or browser.</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, DateTime now, CancellationToken ct = default);

    Task<RefreshToken?> GetActiveByIdAsync(Guid id, Guid userId, DateTime now, CancellationToken ct = default);
}