using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Api.Features.Storage.Directories.SearchDirectory;

public class SearchDirectoryRequest
{
    // Structure
    public Guid? ParentDirectoryId { get; set; }

    // Text search
    public string? NameContains { get; set; }

    // Whole UTC days, inclusive on both ends
    public DateOnly? CreatedAfter { get; set; }
    public DateOnly? CreatedBefore { get; set; }
    public DateOnly? UpdatedAfter { get; set; }
    public DateOnly? UpdatedBefore { get; set; }
    public DateOnly? DeletedAfter { get; set; }
    public DateOnly? DeletedBefore { get; set; }

    // Contents
    public bool? HasFiles { get; set; }
    public bool? HasSubdirectories { get; set; }

    // Flags
    public bool IsDeleted { get; set; }
    public bool IsStarred { get; set; }

    // Paging & sorting
    public int Page { get; set; } = 0;
    public int PageSize { get; set; } = 20;
    public SortBy SortBy { get; set; } = SortBy.Name;
    public SortDirection SortDirection { get; set; } = SortDirection.Asc;
}
