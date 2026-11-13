namespace Alexandria.Dto.Users;

/// <summary>The signed-in account as the login and session responses describe it.</summary>
public class SessionUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}
