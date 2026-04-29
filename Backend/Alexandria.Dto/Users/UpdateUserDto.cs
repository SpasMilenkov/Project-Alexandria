using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Users;

public class UpdateUserDto
{
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public UserRole? Role { get; set; }
}