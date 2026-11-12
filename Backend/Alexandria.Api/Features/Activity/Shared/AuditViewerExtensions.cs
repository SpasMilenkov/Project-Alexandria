using System.Security.Claims;
using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Dto.Audit;

namespace Alexandria.Api.Features.Activity.Shared;

public static class AuditViewerExtensions
{
    public static AuditViewer ToAuditViewer(this ClaimsPrincipal user) =>
        new(user.GetUserId(), user.IsInRole(Common.Seeding.Roles.Admin));
}
