using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Directories.MoveDir;

public class MoveDirEndpoint(IDirectoryService dirService) : Endpoint<MoveDirRequest>
{
    public override void Configure()
    {
        Put("/directories/move");
        Description(x => x.WithTags("Directories"));
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(MoveDirRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await dirService.MoveDirectoryAsync(req.DirectoryIds, req.DestinationId, userId, ct);

        await Send.OkAsync(cancellation: ct);
    }
}