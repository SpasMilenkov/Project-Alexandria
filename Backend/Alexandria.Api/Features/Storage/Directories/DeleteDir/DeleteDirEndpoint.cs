using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Services.Storage.Directories.Exceptions;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Directories.DeleteDir;

public class DeleteDirEndpoint(IDirectoryService dirService) : Endpoint<DeleteDirRequest>
{
    public override void Configure()
    {
        Delete("/directories/{id}");
        Description(x => x.WithTags("Directories"));
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(DeleteDirRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            await dirService.DeleteDirectoryAsync(req.Id, userId, req.Force, ct);
            await Send.OkAsync(cancellation: ct);
        }
        catch (DirectoryNotEmptyException)
        {
            await Send.ResultAsync(
                Results.Conflict(new
                {
                    error = "DIRECTORY_NOT_EMPTY",
                    message = "The directory is not empty. Delete its contents first or use hard delete."
                }));
        }
    }
}