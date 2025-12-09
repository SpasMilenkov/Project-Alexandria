using System.Security.Claims;
using Common;
using Common.Services;
using FastEndpoints;

namespace API.Features.Tags.DeleteTag;


public class DeleteTagEndpoint(IFileTagService tagService) : Endpoint<DeleteTagRequest>
{
    public override void Configure()
    {
        Delete("/tags/{TagId}");
        
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
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
    
        var userId = Guid.Parse(userIdString);

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