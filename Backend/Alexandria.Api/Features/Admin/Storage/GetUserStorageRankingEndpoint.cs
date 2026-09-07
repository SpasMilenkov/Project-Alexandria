using Alexandria.Common.Services;
using Alexandria.Dto.StorageStats;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.Storage;

sealed class GetUserStorageRankingEndpoint(IAdminStorageStatsService statsService)
    : Endpoint<GetUserStorageRankingRequest, IReadOnlyList<UserStorageRankDto>>
{
    public override void Configure()
    {
        Get("/admin/storage/users");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(30);
    }

    public override async Task HandleAsync(GetUserStorageRankingRequest req, CancellationToken ct)
    {
        await Send.OkAsync(await statsService.GetUserRankingAsync(req.Top, ct), ct);
    }
}