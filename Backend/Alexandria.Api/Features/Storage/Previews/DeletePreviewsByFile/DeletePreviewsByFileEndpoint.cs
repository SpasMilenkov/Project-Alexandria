using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Previews.DeletePreviewsByFile;

public sealed class DeletePreviewsByFileResponse
{
    public int DeletedCount { get; set; }
    public long FreedBytes { get; set; }
}

public class DeletePreviewsByFileEndpoint(IStorageService storageService)
    : Endpoint<DeletePreviewsByFileRequest, DeletePreviewsByFileResponse>
{
    public override void Configure()
    {
        Delete("/storage/previews/by-file/{fileId}");
        Description(x => x.WithTags("Storage"));
        Summary(s =>
        {
            s.Summary = "Delete a file's preview artifacts, optionally only older ones";
            s.Description = "Removes matching preview S3 objects and records. Objects still " +
                            "referenced by other previews are kept. Job history is untouched.";
            s.Responses[200] = "Previews deleted";
            s.Responses[403] = "Not the owner";
            s.Responses[404] = "File not found";
        });
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(DeletePreviewsByFileRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var isAdmin = User.IsInRole(Common.Seeding.Roles.Admin);

        try
        {
            var (deletedCount, freedBytes) = await storageService.DeletePreviewsByFileAsync(
                req.FileId, userId, isAdmin, req.CreatedBefore?.UtcDateTime, ct);
            await Send.OkAsync(new DeletePreviewsByFileResponse
            {
                DeletedCount = deletedCount,
                FreedBytes = freedBytes
            }, ct);
        }
        catch (KeyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (UnauthorizedAccessException)
        {
            await Send.ForbiddenAsync(ct);
        }
    }
}