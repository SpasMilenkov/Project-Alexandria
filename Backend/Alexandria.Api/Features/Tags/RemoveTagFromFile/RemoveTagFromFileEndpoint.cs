using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Tags.RemoveTagFromFile;

public class RemoveTagFromFileEndpoint(IFileTagService tagService) : Endpoint<RemoveTagFromFileRequest>
{
    public override void Configure()
    {
        Delete("/files/{FileId}/tags/{TagId}");

        Summary(s =>
        {
            s.Summary = "Remove a tag from a file";
            s.Description = "Removes a tag association from a file. User must own the tag.";
            s.Responses[204] = "Tag removed successfully";
            s.Responses[401] = "Unauthorized - user doesn't own this tag";
            s.Responses[404] = "File not found";
        });
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(RemoveTagFromFileRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            await tagService.RemoveTagFromFileAsync(req.FileId, req.TagId, userId, ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
        catch (UnauthorizedAccessException ex)
        {
            ThrowError(ex.Message, 401);
        }
    }
}