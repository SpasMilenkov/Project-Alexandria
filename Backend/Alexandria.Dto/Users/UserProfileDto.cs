namespace Alexandria.Dto.Users;

public record UserProfileDto
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required DateTime CreatedAt { get; init; }
    public string? AvatarUrl { get; init; }
}
