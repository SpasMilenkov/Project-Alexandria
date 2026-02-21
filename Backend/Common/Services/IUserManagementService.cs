using DTO.Users;
using DTO.Files;

namespace Common.Services;

public interface IUserManagementService
{
    Task<PaginatedResult<UserDetailsDto>> GetUsers(UserQueryDto query, CancellationToken ct = default);
    Task DeleteUsers(Guid[] userIds, CancellationToken ct = default);
    Task<UserDetailsDto> UpdateUser(Guid userId, UpdateUserDto dto, CancellationToken ct = default);
    Task RestrictUser(Guid userId, DateTime restrictionEndDate, CancellationToken ct = default);
}
