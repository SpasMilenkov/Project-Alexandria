using DTO.Files;
using DTO.Users;
using Models;
using Models.Enumerators;

namespace Common.Repositories;

public interface IUserRepository : IRepository<ApplicationUser>
{
    Task<PaginatedResult<UserDetailsDto>> GetUsers(UserQueryDto query, CancellationToken ct = default);
    Task DeleteUsers(Guid[] userIds, CancellationToken ct = default);
    Task<UserProfileDto?> GetUserProfile(Guid userId, CancellationToken ct = default);
    Task SetupProfile(Guid userId, CancellationToken ct = default);
    Task<OnboardingStep?> GetOnboardingStatus(Guid userId, CancellationToken ct);
}
