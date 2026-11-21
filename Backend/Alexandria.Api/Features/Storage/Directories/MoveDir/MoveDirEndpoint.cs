using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Directories;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Directories.MoveDir;

public class MoveDirEndpoint(IDirectoryService dirService) : Endpoint<MoveDirRequest>
{
    public override void Configure()
    {
        Post("/directories/move");
        Description(x => x.WithTags("Directories"));
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(MoveDirRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            await dirService.MoveDirectoryAsync(req.DirectoryIds, req.DestinationId, userId, ct);
        }
        catch (SystemDirectoryProtectedException ex)
        {
            AddError(r => r.DirectoryIds, ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
            return;
        }

        await Send.OkAsync(cancellation: ct);
    }
}