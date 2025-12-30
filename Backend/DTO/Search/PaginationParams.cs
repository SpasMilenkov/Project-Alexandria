using Models.Enumerators;

namespace DTO.Search;

public class PaginationParams
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public SortBy SortBy { get; set; }
    public SortDirection SortDirection { get; set; }
}