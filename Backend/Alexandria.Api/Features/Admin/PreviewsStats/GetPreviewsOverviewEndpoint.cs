using Alexandria.Common.Services;
using Alexandria.Dto.PreviewsStats;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.PreviewsStats;

sealed class GetPreviewsOverviewEndpoint(IPreviewStatsService statsService)
    : EndpointWithoutRequest<PreviewsOverviewResponse>
{
    public override void Configure()
    {
        Get("/admin/previews/overview");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(10);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await statsService.GetOverviewAsync(ct);
        await Send.OkAsync(result, ct);
    }
}