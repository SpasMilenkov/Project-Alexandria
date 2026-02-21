using Models.Enumerators;

namespace DTO.Users;

public class UpdateUserDto
{
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public UserRole? Role { get; set; }
}
