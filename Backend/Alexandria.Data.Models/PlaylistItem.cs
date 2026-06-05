namespace Alexandria.Data.Models;

public class PlaylistItem : IBase
{
    public Guid Id { get; set; }
    public int Position { get; set; }

    public Guid PlaylistId { get; set; }
    public Playlist? Playlist { get; set; }

    public Guid TranspilationJobId { get; set; }
    public TranspilationJob? TranspilationJob { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}