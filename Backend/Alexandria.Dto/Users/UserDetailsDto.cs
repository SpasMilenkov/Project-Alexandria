using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Users;

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

    /// <summary>Designated quota in bytes. 0 means unlimited.</summary>
    public long StorageQuota { get; set; }
}