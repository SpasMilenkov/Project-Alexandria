using Alexandria.Common.Services;
using Alexandria.Dto.TranspilationStats;
using Alexandria.Services.Monitoring;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.TranspilationStats;

sealed class GetTranspilationOverviewEndpoint(ITranspilationStatsService statsService)
    : EndpointWithoutRequest<TranspilationOverviewResponse>
{
    public override void Configure()
    {
        Get("/admin/transpilation/overview");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(10);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await statsService.GetOverviewAsync(
            TranspilationStatsService.DefaultDurationWindowDays, ct);
        await Send.OkAsync(result, ct);
    }
}