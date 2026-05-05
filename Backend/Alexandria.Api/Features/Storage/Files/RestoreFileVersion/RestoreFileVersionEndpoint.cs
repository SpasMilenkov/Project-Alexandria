using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.RestoreFileVersion;

internal sealed class RestoreFileVersionRequest
{
    public Guid Id { get; set; }
}

internal sealed class RestoreFileVersionEndpoint(IFileService fileService) : Endpoint<RestoreFileVersionRequest>
{
    public override void Configure()
    {
        Patch("/files/versions/restore/{id}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(RestoreFileVersionRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await fileService.RestoreFileVersionAsync(req.Id, userId, ct);

        await Send.NoContentAsync(cancellation: ct);
    }
}