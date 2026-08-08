using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

public class FileTag
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public File File { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;

    public required TagSource Source { get; set; }
    public double? Confidence { get; set; } // null for User/Embedded, populated for Auto

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}