using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Metrics;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Metrics.Storage.MyStorage;

sealed class StorageBreakdownRequest
{
    public bool Summary { get; set; }
}

sealed class StorageBreakdownEndpoint(IContentStorageService storageService)
    : Endpoint<StorageBreakdownRequest, StorageBreakdown>
{
    public override void Configure()
    {
        Get("/storage/my-storage");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(StorageBreakdownRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await storageService.GetStorageBreakdown(userId, req.Summary, ct), ct);
    }
}