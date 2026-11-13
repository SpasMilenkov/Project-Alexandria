using Alexandria.Common.Exceptions;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Users;

namespace Alexandria.Common.Services;

public interface IUserManagementService
{
    Task<UserDetailsDto> CreateUserAsync(string username, string email, string password, UserRole userRole,
        long? storageQuotaBytes = null, CancellationToken ct = default);

    Task<PaginatedResult<UserDetailsDto>> GetUsersAsync(UserQueryDto query, CancellationToken ct = default);
    Task DeleteUsersAsync(Guid[] userIds, CancellationToken ct = default);
    Task<UserDetailsDto> UpdateUserAsync(Guid userId, UpdateUserDto dto, CancellationToken ct = default);
    Task RestrictUserAsync(Guid userId, DateTime restrictionEndDate, CancellationToken ct = default);
    Task<UserProfileDto> GetUserProfileAsync(Guid userId, CancellationToken ct = default);

    Task<UserProfileStatsDto> GetProfileStatsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Renames the account the caller is signed in as.</summary>
    /// <exception cref="UserNameTakenException">Another account already uses the name.</exception>
    Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto, CancellationToken ct = default);

    Task SetupProfileAsync(Guid userId, CancellationToken ct = default);
    Task FinishTourAsync(Guid userId, CancellationToken ct = default);
    Task<OnboardingStep?> GetOnboardingStepAsync(Guid userId, CancellationToken ct);
}