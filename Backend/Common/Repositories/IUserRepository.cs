using DTO.Files;
using DTO.Users;
using Models;

namespace Common.Repositories;

public interface IUserRepository : IRepository<ApplicationUser>
{
    Task<PaginatedResult<UserDetailsDto>> GetUsers(UserQueryDto query, CancellationToken ct = default);
    Task DeleteUsers(Guid[] userIds, CancellationToken ct = default);
}
