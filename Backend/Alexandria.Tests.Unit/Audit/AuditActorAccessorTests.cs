using System.Net;
using System.Security.Claims;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Data.Audit;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Alexandria.Tests.Unit.Audit;

public class AuditActorAccessorTests
{
    private readonly IHttpContextAccessor _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
    private readonly AuditContext _auditContext = new();

    private AuditActorAccessor CreateSut() => new(_httpContextAccessor, _auditContext);

    private DefaultHttpContext SignedInContext(Guid userId)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())]))
        };

        _httpContextAccessor.HttpContext.Returns(context);
        return context;
    }

    [Fact]
    public void UserId_signedInRequest_returnsClaim()
    {
        var userId = Guid.NewGuid();
        SignedInContext(userId);

        CreateSut().UserId.Should().Be(userId);
    }

    [Fact]
    public void UserId_runAsSystem_overridesClaim()
    {
        SignedInContext(Guid.NewGuid());
        _auditContext.RunAsSystem();

        CreateSut().UserId.Should().Be(SystemConfig.SystemId);
    }

    [Fact]
    public void UserId_noHttpContext_returnsNull()
    {
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        CreateSut().UserId.Should().BeNull();
    }

    [Fact]
    public void IpAddress_forwardedHeaderFromClient_isIgnored()
    {
        var context = SignedInContext(Guid.NewGuid());
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.20");
        context.Request.Headers["X-Forwarded-For"] = "6.6.6.6";

        CreateSut().IpAddress.Should().Be("192.168.1.20");
    }

    [Fact]
    public void IpAddress_ipv4MappedAddress_isUnwrapped()
    {
        var context = SignedInContext(Guid.NewGuid());
        context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:10.0.0.5");

        CreateSut().IpAddress.Should().Be("10.0.0.5");
    }

    [Fact]
    public void UserAgent_overlong_isTruncated()
    {
        var context = SignedInContext(Guid.NewGuid());
        context.Request.Headers["User-Agent"] = new string('a', 400);

        CreateSut().UserAgent.Should().HaveLength(255);
    }

    [Fact]
    public void CorrelationId_sameScope_isStable()
    {
        var sut = CreateSut();

        sut.CorrelationId.Should().Be(sut.CorrelationId);
        sut.CorrelationId.Should().NotBe(CreateSut().CorrelationId);
    }
}
