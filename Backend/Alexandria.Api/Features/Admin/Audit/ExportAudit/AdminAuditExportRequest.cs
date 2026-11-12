using Alexandria.Api.Features.Activity.Shared;

namespace Alexandria.Api.Features.Admin.Audit.ExportAudit;

sealed class AdminAuditExportRequest : AuditExportRequest
{
    public Guid? UserId { get; set; }
    public Guid? OwnerId { get; set; }
}
