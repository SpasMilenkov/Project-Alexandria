using Alexandria.Common.Audit;

namespace Alexandria.Tests.Integration.Audit;

internal sealed class FixedAuditActor(Guid? userId, string? ipAddress, Guid correlationId) : IAuditActorAccessor
{
    public Guid? UserId { get; } = userId;
    public string? IpAddress { get; } = ipAddress;
    public string? UserAgent => null;
    public Guid CorrelationId { get; } = correlationId;
}
