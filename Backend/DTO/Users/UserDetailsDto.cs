using Models.Enumerators;

namespace DTO.Users;

public class UserDetailsDto
{
    public required Guid Id { get; set; }
    public required string UserName { get; set; }
    public required string Email { get; set; }
    public bool IsLockedOut { get; set; } = false;
    public UserRole? Role { get; set; }
    public DateTime? LockedOutStarted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
