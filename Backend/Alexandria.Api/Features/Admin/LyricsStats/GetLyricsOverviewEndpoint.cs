using Alexandria.Common.Services;
using Alexandria.Dto.LyricsStats;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.LyricsStats;

sealed class GetLyricsOverviewEndpoint(ILyricsStatsService statsService)
    : EndpointWithoutRequest<LyricsOverviewResponse>
{
    public override void Configure()
    {
        Get("/admin/lyrics/overview");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(10);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await statsService.GetOverviewAsync(ct);
        await Send.OkAsync(result, ct);
    }
}