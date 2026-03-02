using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Files.DeleteFile;

public class DeleteFileEndpoint(IFileService fileService) : Endpoint<DeleteFileRequest>
{
    public override void Configure()
    {
        Delete("/files/");
        Description(x => x.WithTags("Files"));

        Summary(s =>
        {
            s.Summary = "Soft delete a file (marks as deleted, keeps in storage)";
            s.Description =
                "Performs a soft delete - marks the file as deleted in the database but keeps it in MinIO storage";
            s.Responses[200] = "File deleted successfully";
            s.Responses[404] = "File not found";
            s.Responses[500] = "Internal server error";
        });

        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(DeleteFileRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            await fileService.DeleteFiles(req.Ids, userId, req.HardDelete, ct);
            await Send.OkAsync(new { Message = "File soft deleted successfully" }, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
        catch (Exception ex)
        {
            ThrowError($"Delete failed: {ex.Message}");
        }
    }
}
