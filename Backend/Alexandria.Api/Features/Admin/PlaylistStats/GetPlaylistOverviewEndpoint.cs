using Alexandria.Common.Services;
using Alexandria.Dto.PlaylistStats;
using Alexandria.Services.Monitoring;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.PlaylistStats;

sealed class GetPlaylistOverviewEndpoint(IPlaylistStatsService statsService)
    : EndpointWithoutRequest<PlaylistOverviewResponse>
{
    public override void Configure()
    {
        Get("/admin/playlists/overview");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(10);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await statsService.GetOverviewAsync(
            PlaylistStatsService.DefaultDurationWindowDays, ct);
        await Send.OkAsync(result, ct);
    }
}