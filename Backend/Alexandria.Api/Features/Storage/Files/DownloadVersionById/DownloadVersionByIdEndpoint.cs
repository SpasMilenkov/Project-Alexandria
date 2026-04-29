using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.DownloadVErsionById;

sealed class DownloadVersionByIdRequest
{
    public Guid Id { get; set; }
}

sealed class DownloadVersionByIdEndpoint(IStorageService storageService)
    : Endpoint<DownloadVersionByIdRequest, DownloadInfo>
{
    public override void Configure()
    {
        Get("/files/versions/{id}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(DownloadVersionByIdRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await storageService.GetFilVersioneDownloadDetails(req.Id, userId, ct), ct);
    }
}