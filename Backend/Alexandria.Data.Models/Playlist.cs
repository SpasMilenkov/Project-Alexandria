namespace Alexandria.Data.Models;

public class Playlist : IBase
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool HasCover { get; set; } = false;
    public string? AmbientTheme { get; set; }

    public Guid OwnerId { get; set; }
    public ApplicationUser? Owner { get; set; }
    public ICollection<PlaylistItem>? PlaylistItems { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}