using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Users;

namespace Alexandria.Common.Repositories;

public interface IUserRepository : IRepository<ApplicationUser>
{
    Task<PaginatedResult<UserDetailsDto>> GetUsersAsync(UserQueryDto query, CancellationToken ct = default);
    Task DeleteUsersAsync(Guid[] userIds, CancellationToken ct = default);
    Task<UserProfileDto?> GetUserProfileAsync(Guid userId, CancellationToken ct = default);
    Task SetupProfileAsync(Guid userId, CancellationToken ct = default);
    Task<OnboardingStep?> GetOnboardingStatusAsync(Guid userId, CancellationToken ct);
}