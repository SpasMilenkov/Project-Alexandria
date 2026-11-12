using Alexandria.Api.Features.Activity.Shared;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Api.Features.Admin.Audit.SearchAudit;

sealed class AdminAuditQuery : AuditFilterRequest
{
    public Guid? UserId { get; set; }
    public Guid? OwnerId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public AuditSortBy SortBy { get; set; } = AuditSortBy.Timestamp;
    public SortDirection SortDirection { get; set; } = SortDirection.Desc;
}
