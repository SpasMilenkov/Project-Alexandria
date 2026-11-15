using NpgsqlTypes;

namespace Alexandria.Data.Models;

public class Directory : IBase, ISearchableByName
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string NormalizedName { get; set; } = null!;
    public NpgsqlTsVector SearchVector { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public Directory? Parent { get; set; }
    public List<Directory>? Children { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsStarred { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Bytes of the current versions of the live files sitting directly in this folder, without
    /// subfolders. A database trigger owns it because uploads, moves, trash and version switches
    /// write files through many paths, so EF never writes it. Subfolder totals are summed on read,
    /// which keeps moving a folder free.
    /// </summary>
    public long DirectFilesSize { get; set; }

    public List<File>? Files { get; set; }
    public ApplicationUser? Owner { get; set; }
    public Guid OwnerId { get; set; }
}