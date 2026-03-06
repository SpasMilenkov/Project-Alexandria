using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Files.DownloadVErsionById;

sealed class DownloadVersionByIdRequest
{
    public Guid Id { get; set; }
}

sealed class DownloadVersionByIdEndpoint(IStorageService storageService) : Endpoint<DownloadVersionByIdRequest>
{
    public override void Configure()
    {
        Get("/files/versions/{id}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(DownloadVersionByIdRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await storageService.GetVersionPresignedUrl(fileVersionId: req.Id, ownerId: userId, ct), ct);
    }
}