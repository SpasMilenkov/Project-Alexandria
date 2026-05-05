using Alexandria.Data.Models;

namespace Alexandria.Common.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenWithUserAsync(string token, CancellationToken ct = default);
    Task<RefreshToken> AddAsync(RefreshToken entity, CancellationToken ct = default);
    void Update(RefreshToken entity);
    Task<bool> RevokeTokenAsync(string token, CancellationToken ct = default);
    Task RevokeUserTokensAsync(Guid userId, CancellationToken ct = default);
}