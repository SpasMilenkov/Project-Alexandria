using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Api.Features.Storage.Directories.GetRootDirectories;

public class GetRootDirRequest
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public SortBy SortBy { get; set; }
    public SortDirection SortDirection { get; set; }
}