using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Users;

namespace Alexandria.Common.Services;

public interface IUserManagementService
{
    Task<UserDetailsDto> CreateUserAsync(string username, string email, string password, UserRole userRole,
        CancellationToken ct = default);

    Task<PaginatedResult<UserDetailsDto>> GetUsersAsync(UserQueryDto query, CancellationToken ct = default);
    Task DeleteUsersAsync(Guid[] userIds, CancellationToken ct = default);
    Task<UserDetailsDto> UpdateUserAsync(Guid userId, UpdateUserDto dto, CancellationToken ct = default);
    Task RestrictUserAsync(Guid userId, DateTime restrictionEndDate, CancellationToken ct = default);
    Task<UserProfileDto> GetUserProfileAsync(Guid userId, CancellationToken ct = default);
    Task SetupProfileAsync(Guid userId, CancellationToken ct = default);
    Task FinishTourAsync(Guid userId, CancellationToken ct = default);
    Task<OnboardingStep?> GetOnboardingStepAsync(Guid userId, CancellationToken ct);
}