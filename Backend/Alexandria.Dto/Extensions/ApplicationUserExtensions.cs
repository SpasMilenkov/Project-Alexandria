using Alexandria.Data.Models;
using Alexandria.Dto.Users;

namespace Alexandria.Dto.Extensions;

public static class ApplicationUserExtensions
{
    public static UserDetailsDto ToDto(this ApplicationUser user) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        Email = user.Email,
        CreatedAt = user.CreatedAt,
    };
}