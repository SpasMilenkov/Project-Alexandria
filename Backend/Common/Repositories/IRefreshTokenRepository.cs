using System.Linq.Expressions;
using Models;

namespace Common.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<RefreshToken?> GetByTokenWithUserAsync(string token, CancellationToken ct = default);
    Task<RefreshToken?> FirstOrDefaultAsync(Expression<Func<RefreshToken, bool>> predicate, CancellationToken ct = default);
    Task<IEnumerable<RefreshToken>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<RefreshToken>> FindAsync(Expression<Func<RefreshToken, bool>> predicate, CancellationToken ct = default);
    Task<RefreshToken> AddAsync(RefreshToken entity, CancellationToken ct = default);
    Task<RefreshToken> UpdateAsync(RefreshToken entity, CancellationToken ct = default);
    void Update(RefreshToken entity);
    void Remove(RefreshToken entity);
    void RemoveRange(IEnumerable<RefreshToken> entities);
    Task<int> CountAsync(Expression<Func<RefreshToken, bool>>? predicate = null, CancellationToken ct = default);
    Task<bool> ExistsAsync(Expression<Func<RefreshToken, bool>> predicate, CancellationToken ct = default);
    Task<bool> RevokeTokenAsync(string token, CancellationToken ct = default);
    Task RevokeUserTokensAsync(Guid userId, CancellationToken ct = default);
}