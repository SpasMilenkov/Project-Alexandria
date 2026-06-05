using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.GetManifest;

internal sealed class GetManifestRequest
{
    public Guid Id { get; set; }
}

sealed class GetManifestEndpoint(IStorageService storageService) : Endpoint<GetManifestRequest, string>
{
    public override void Configure()
    {
        Get("/streaming/manifest/{Id}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetManifestRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await storageService.GetStreamManifest(req.Id, userId, ct), ct);
    }
}