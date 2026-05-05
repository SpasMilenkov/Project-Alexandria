using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files;

public class FileSearchQuery
{
    // Identity & structure
    public Guid? DirectoryId { get; set; }
    public Guid? ParentDirectoryId { get; set; }

    // Text search
    public string? NameContains { get; set; }

    // File specific
    public long? MinSize { get; set; }
    public long? MaxSize { get; set; }
    public string? MimeType { get; set; }

    // Ownership & sharing
    public Guid? OwnerId { get; set; }
    public bool? IsShared { get; set; }

    // Time filters
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public DateTime? UpdatedAfter { get; set; }
    public DateTime? UpdatedBefore { get; set; }
    public DateTime? DeletedBefore { get; set; }

    public DateTime? DeletedAfter { get; set; }

    // Flags
    public bool IsDeleted { get; set; } = false;
    public bool IsStarred { get; set; } = false;
    public bool OnlyDeleted { get; set; } = false;

    // Paging & sorting
    public int CurrentPage { get; set; } = 0;
    public int PageSize { get; set; } = 20;
    public SortBy SortBy { get; set; } = SortBy.Name;
    public SortDirection SortDirection { get; set; } = SortDirection.Asc;
}