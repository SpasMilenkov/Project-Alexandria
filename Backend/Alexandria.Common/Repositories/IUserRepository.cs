using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Users;

namespace Alexandria.Common.Repositories;

public interface IUserRepository : IRepository<ApplicationUser>
{
    Task<PaginatedResult<UserDetailsDto>> GetUsersAsync(UserQueryDto query, CancellationToken ct = default);
    Task DeleteUsersAsync(Guid[] userIds, CancellationToken ct = default);

    /// <summary>Sum of designated quotas over live accounts, plus the account count.</summary>
    Task<(long TotalQuotaBytes, int UserCount)> GetTotalStorageQuotaAsync(CancellationToken ct = default);

    /// <summary>Every live account except the system account, with its lockout end.</summary>
    Task<IReadOnlyList<AccountStateRow>> GetAccountStatesAsync(CancellationToken ct = default);

    Task<UserProfileDto?> GetUserProfileAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Totals about a live account's own library in one round trip. Null when the account is gone.</summary>
    Task<UserProfileStatsDto?> GetProfileStatsAsync(Guid userId, DateTime now, CancellationToken ct = default);

    /// <summary>Points a live account at a new avatar (or none). False when the account is gone.</summary>
    Task<bool> SetAvatarAsync(Guid userId, Guid? avatarId, CancellationToken ct = default);

    Task SetupProfileAsync(Guid userId, CancellationToken ct = default);
    Task<OnboardingStep?> GetOnboardingStatusAsync(Guid userId, CancellationToken ct);
}