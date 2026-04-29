using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Users;

namespace Alexandria.Common.Repositories;

public interface IUserRepository : IRepository<ApplicationUser>
{
    Task<PaginatedResult<UserDetailsDto>> GetUsers(UserQueryDto query, CancellationToken ct = default);
    Task DeleteUsers(Guid[] userIds, CancellationToken ct = default);
    Task<UserProfileDto?> GetUserProfile(Guid userId, CancellationToken ct = default);
    Task SetupProfile(Guid userId, CancellationToken ct = default);
    Task<OnboardingStep?> GetOnboardingStatus(Guid userId, CancellationToken ct);
}