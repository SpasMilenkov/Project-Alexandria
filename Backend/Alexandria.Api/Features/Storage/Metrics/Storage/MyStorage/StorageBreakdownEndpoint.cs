using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Metrics;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Metrics.Storage.MyStorage;

sealed class StorageBreakdownEndpoint(IStorageService storageService) : EndpointWithoutRequest<StorageBreakdown>
{
    public override void Configure()
    {
        Get("/storage/my-storage");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await storageService.GetStorageBreakdown(userId, ct), ct);
    }
}