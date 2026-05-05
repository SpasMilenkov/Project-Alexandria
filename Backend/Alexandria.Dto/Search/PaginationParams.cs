using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Search;

public class PaginationParams
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public SortBy SortBy { get; set; }
    public SortDirection SortDirection { get; set; }
}