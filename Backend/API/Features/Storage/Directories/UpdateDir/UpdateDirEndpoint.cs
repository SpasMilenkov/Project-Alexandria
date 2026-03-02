using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Directories.UpdateDir;

public class UpdateDirEndpoint(IDirectoryService dirService) : Endpoint<UpdateDirRequest, UpdateDirResponse>
{
    public override void Configure()
    {
        Patch("/directories/{directoryId}");
        Description(x => x.WithTags("Directories"));
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(UpdateDirRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var dir = await dirService.UpdateDirectoryAsync(req.DirectoryId, req.Name, userId, ct);
        await Send.OkAsync(new UpdateDirResponse
        {
            Directory = dir
        }, ct);
    }
}
