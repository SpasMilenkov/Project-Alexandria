using API.Features.Auth.Extensions;
using FastEndpoints;
using DTO.Metrics;
using Common.Services;

namespace API.Features.Storage.Metrics.Storage.MyStorage;

sealed class StorageBreakdownEndpoint(IStorageService storageService) : EndpointWithoutRequest<StorageBreakdown>
{
    public override void Configure()
    {
        Get("/storage/my-storage");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await storageService.GetStorageBreakdown(userId, ct), ct);
    }
}
