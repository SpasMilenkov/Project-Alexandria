using Alexandria.Common;
using Alexandria.Dto.Metrics;
using Alexandria.Services.Storage;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.Storage;

sealed class GetAdminStorageOverviewEndpoint(MetricsService metricsService, IUnitOfWork unitOfWork)
    : EndpointWithoutRequest<AdminStorageOverview>
{
    public override void Configure()
    {
        Get("/admin/storage/overview");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(30);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var capacity = await metricsService.GetStorageInfoAsync();
            var (totalAssignedQuotaBytes, totalUsers) =
                await unitOfWork.Users.GetTotalStorageQuotaAsync(ct);

            await Send.OkAsync(new AdminStorageOverview
            {
                Capacity = capacity,
                TotalAssignedQuotaBytes = totalAssignedQuotaBytes,
                TotalUsers = totalUsers
            }, ct);
        }
        catch (Exception ex)
        {
            await Send.ErrorsAsync(500, ct);
            ThrowError(ex.Message);
        }
    }
}