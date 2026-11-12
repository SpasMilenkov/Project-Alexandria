using Alexandria.Api.Features.Activity.Shared;

namespace Alexandria.Api.Features.Admin.Audit.PlanAuditExport;

sealed class AdminAuditExportPlanRequest : AuditFilterRequest
{
    public Guid? UserId { get; set; }
    public Guid? OwnerId { get; set; }
}
