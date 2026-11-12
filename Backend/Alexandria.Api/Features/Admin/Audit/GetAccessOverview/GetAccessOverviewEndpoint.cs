using Alexandria.Common.Services;
using Alexandria.Dto.Audit;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Admin.Audit.GetAccessOverview;

sealed class GetAccessOverviewEndpoint(IAccessOverviewService overviewService)
    : Endpoint<AccessOverviewRequest, AccessOverview>
{
    public override void Configure()
    {
        Get("/admin/audit/overview");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
        Summary(s => s.Summary = "Account and sign-in statistics across every account");
    }

    public override async Task HandleAsync(AccessOverviewRequest req, CancellationToken ct)
    {
        var overview = await overviewService.GetOverviewAsync(req.Days, ct);
        await Send.OkAsync(overview, ct);
    }
}
