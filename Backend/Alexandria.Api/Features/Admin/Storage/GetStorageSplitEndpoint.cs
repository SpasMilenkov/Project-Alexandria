using Alexandria.Common.Services;
using Alexandria.Dto.StorageStats;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.Storage;

sealed class GetStorageSplitEndpoint(IAdminStorageStatsService statsService)
    : EndpointWithoutRequest<StorageSplitResponse>
{
    public override void Configure()
    {
        Get("/admin/storage/split");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(30);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(await statsService.GetSplitAsync(ct), ct);
    }
}