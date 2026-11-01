using Alexandria.Common.Services;
using Alexandria.Dto.PlaylistStats;
using Alexandria.Services.Monitoring;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Admin.PlaylistStats;

sealed class GetPlaylistOverviewEndpoint(IPlaylistStatsService statsService)
    : EndpointWithoutRequest<PlaylistOverviewResponse>
{
    public override void Configure()
    {
        Get("/admin/playlists/overview");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await statsService.GetOverviewAsync(
            PlaylistStatsService.DefaultDurationWindowDays, ct);
        await Send.OkAsync(result, ct);
    }
}