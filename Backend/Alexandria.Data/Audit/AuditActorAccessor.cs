using System.Security.Claims;
using Alexandria.Common.Audit;
using Alexandria.Data.Models;
using Microsoft.AspNetCore.Http;

namespace Alexandria.Data.Audit;

public sealed class AuditActorAccessor(IHttpContextAccessor httpContextAccessor, AuditContext auditContext)
    : IAuditActorAccessor
{
    private Guid? _correlationId;

    public Guid? UserId
    {
        get
        {
            if (auditContext.UserId.HasValue)
                return auditContext.UserId;

            var user = httpContextAccessor.HttpContext?.User;
            var raw = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value;

            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    // UseForwardedHeaders has already replaced RemoteIpAddress with the client address when the
    // request came through the trusted proxy. Reading X-Forwarded-For here would let any client
    // write whatever address it likes into the log.
    public string? IpAddress
    {
        get
        {
            var address = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
            if (address is null)
                return null;

            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            return address.ToString();
        }
    }

    public string? UserAgent
    {
        get
        {
            var agent = httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();
            if (string.IsNullOrEmpty(agent))
                return null;

            var max = ValidationConstants.StringLengths.MediumString;
            return agent.Length > max ? agent[..max] : agent;
        }
    }

    public Guid CorrelationId => _correlationId ??= Guid.CreateVersion7();
}
