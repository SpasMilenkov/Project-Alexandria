using System.Security.Claims;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Files.DeleteFile;

public class DeleteFileEndpoint(IStorageService storage) : Endpoint<DeleteFileRequest>
{
    public override void Configure()
    {
        Delete("/files/{id}");
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
    }

    public override async Task HandleAsync(DeleteFileRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
        var userId = Guid.Parse(userIdString);

        try
        {
            //TODO: pass a hard delete parameter as well
            await storage.DeleteFile(req.Id, userId, ct, false);
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