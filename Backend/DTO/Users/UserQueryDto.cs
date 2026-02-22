using DTO.Search;
using Models.Enumerators;

namespace DTO.Users;

public class UserQueryDto
{
    public int Page { get; set; } = 0;
    public int PageSize { get; set; } = 20;
    public SortBy SortBy { get; set; } = SortBy.CreatedAt;
    public SortDirection SortDirection { get; set; } = SortDirection.Desc;

    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public UserRole? Role { get; set; }
    public bool? IsLockedOut { get; set; }
    public bool ShowDeleted { get; set; } = false;
    public bool ShowDeletedOnly { get; set; } = false;
    public DateTime? CreatedBefore { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? UpdatedBefore { get; set; }
    public DateTime? UpdatedAfter { get; set; }
    public DateTime? DeletedBefore { get; set; }
    public DateTime? DeletedAfter { get; set; }
    public DateTime? LockedOutBefore { get; set; }
    public DateTime? LockedOutAfter { get; set; }
}

