using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Directories;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Directories.UpdateDir;

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

        try
        {
            var dir = await dirService.UpdateDirectoryAsync(req.DirectoryId, req.Name, userId, ct);
            await Send.OkAsync(new UpdateDirResponse
            {
                Directory = dir
            }, ct);
        }
        catch (SystemDirectoryProtectedException ex)
        {
            AddError(r => r.DirectoryId, ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
        }
    }
}