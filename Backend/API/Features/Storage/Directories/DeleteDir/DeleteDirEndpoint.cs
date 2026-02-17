using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;
using Storage.Directories.Exceptions;

namespace API.Features.Storage.Directories.DeleteDir;

public class DeleteDirEndpoint(IDirectoryService dirService) : Endpoint<DeleteDirRequest>
{
    public override void Configure()
    {
        Delete("/directories/{id}");
        Description(x => x.WithTags("Directories"));
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
