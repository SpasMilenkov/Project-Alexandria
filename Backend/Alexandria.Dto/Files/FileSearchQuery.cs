using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files;

public class FileSearchQuery
{
    // Structure
    public Guid? ParentDirectoryId { get; set; }

    // The folder and everything below it, at any depth. Notes-only search passes the Notes folder.
    public Guid? WithinDirectoryId { get; set; }

    // Text search
    public string? NameContains { get; set; }

    // File specific
    public long? MinSize { get; set; }
    public long? MaxSize { get; set; }
    public string? MimeType { get; set; }

    // Every listed tag must be on the file
    public ICollection<Guid> TagIds { get; set; } = [];

    // Whole UTC days, inclusive on both ends
    public DateOnly? CreatedAfter { get; set; }
    public DateOnly? CreatedBefore { get; set; }
    public DateOnly? UpdatedAfter { get; set; }
    public DateOnly? UpdatedBefore { get; set; }
    public DateOnly? DeletedAfter { get; set; }
    public DateOnly? DeletedBefore { get; set; }

    // Trash: true lists only deleted files, false only live ones
    public bool IsDeleted { get; set; }

    // Paging & sorting; sorting applies only without search text, which ranks by relevance
    public int CurrentPage { get; set; } = 0;
    public int PageSize { get; set; } = 20;
    public SortBy SortBy { get; set; } = SortBy.Name;
    public SortDirection SortDirection { get; set; } = SortDirection.Asc;
}
