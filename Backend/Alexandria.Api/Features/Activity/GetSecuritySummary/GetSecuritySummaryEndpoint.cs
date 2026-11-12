using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Audit;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Activity.GetSecuritySummary;

sealed class GetSecuritySummaryEndpoint(IAuditService auditService) : EndpointWithoutRequest<SecuritySummary>
{
    public override void Configure()
    {
        Get("/activity/security");
        Policies(Common.Auth.Policies.RequireUser);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
        Summary(s => s.Summary = "Recent sign-ins, failed attempts and known IP addresses for the caller");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(await auditService.GetSecuritySummaryAsync(User.GetUserId(), ct), ct);
    }
}
