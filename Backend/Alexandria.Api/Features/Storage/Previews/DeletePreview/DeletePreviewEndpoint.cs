using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Previews.DeletePreview;

public sealed class DeletePreviewResponse
{
    public long FreedBytes { get; set; }
}

public class DeletePreviewEndpoint(IStorageService storageService)
    : Endpoint<DeletePreviewRequest, DeletePreviewResponse>
{
    public override void Configure()
    {
        Delete("/storage/previews/{previewId}");
        Description(x => x.WithTags("Storage"));
        Summary(s =>
        {
            s.Summary = "Delete a preview artifact (regenerable from the file)";
            s.Description = "Removes the preview S3 object and its record. Job history is untouched.";
            s.Responses[200] = "Preview deleted";
            s.Responses[403] = "Not the owner";
            s.Responses[404] = "Preview not found";
        });
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(DeletePreviewRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var isAdmin = User.IsInRole(Common.Seeding.Roles.Admin);

        try
        {
            var freedBytes = await storageService.DeletePreviewAsync(req.PreviewId, userId, isAdmin, ct);
            await Send.OkAsync(new DeletePreviewResponse { FreedBytes = freedBytes }, ct);
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