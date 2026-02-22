namespace DTO.Extensions;

using DTO.Users;
using Models;

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
