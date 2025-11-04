using Common;
using FastEndpoints;

namespace API.Features.Tags.DeleteTag;


public class DeleteTagEndpoint(IFileTagService tagService) : Endpoint<DeleteTagRequest>
{
    public override void Configure()
    {
        Delete("/tags/{TagId}");
        AllowAnonymous(); // TODO: Replace with proper authorization
        
        Summary(s =>
        {
            s.Summary = "Delete a tag";
            s.Description = "Soft-deletes a tag. User must own the tag.";
            s.Responses[204] = "Tag deleted successfully";
            s.Responses[401] = "Unauthorized - user doesn't own this tag";
            s.Responses[404] = "Tag not found";
        });
    }

    public override async Task HandleAsync(DeleteTagRequest req, CancellationToken ct)
    {
        // TODO: Extract from JWT token
        var userId = Guid.Parse("019a3f05-659b-7628-9102-1eef78035977"); // Placeholder

        try
        {
            await tagService.DeleteTagAsync(req.TagId, userId, ct);
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