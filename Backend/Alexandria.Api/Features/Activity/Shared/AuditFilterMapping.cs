using Alexandria.Dto.Audit;

namespace Alexandria.Api.Features.Activity.Shared;

public static class AuditFilterMapping
{
    public static AuditLogFilter ToFilter(this AuditFilterRequest request, Guid? userId, Guid? ownerId) => new()
    {
        UserId = userId,
        OwnerId = ownerId,
        EntityType = request.EntityType,
        EntityId = request.EntityId,
        IncludeChildren = request.EntityId.HasValue,
        EventCodes = request.EventCodes,
        OperationType = request.OperationType,
        Source = request.Source,
        IpAddress = string.IsNullOrWhiteSpace(request.IpAddress) ? null : request.IpAddress.Trim(),
        From = request.From,
        To = request.To,
        CorrelationId = request.CorrelationId,
    };

    public static AuditLogFilter ToExportFilter(this AuditExportRequest request, Guid? userId, Guid? ownerId) =>
        request.ToFilter(userId, ownerId) with
        {
            UpTo = request.StartCursor(),
            After = request.EndCursor(),
        };
}
