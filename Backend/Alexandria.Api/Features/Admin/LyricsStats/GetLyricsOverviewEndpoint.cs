using Alexandria.Common.Services;
using Alexandria.Dto.LyricsStats;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Admin.LyricsStats;

sealed class GetLyricsOverviewEndpoint(ILyricsStatsService statsService)
    : EndpointWithoutRequest<LyricsOverviewResponse>
{
    public override void Configure()
    {
        Get("/admin/lyrics/overview");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await statsService.GetOverviewAsync(ct);
        await Send.OkAsync(result, ct);
    }
}