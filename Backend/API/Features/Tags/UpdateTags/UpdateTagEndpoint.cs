using Common;
using Common.Services;
using FastEndpoints;

namespace API.Features.Tags.UpdateTags;

public class UpdateTagEndpoint(IFileTagService tagService) : Endpoint<UpdateTagRequest, UpdateTagResponse>
{
    public override void Configure()
    {
        Put("/tags/{TagId}");
        AllowAnonymous(); // TODO: Replace with proper authorization
        
        Summary(s =>
        {
            s.Summary = "Update a tag";
            s.Description = "Updates an existing tag's name. User must own the tag.";
            s.Responses[200] = "Tag updated successfully";
            s.Responses[400] = "Bad request - invalid data";
            s.Responses[401] = "Unauthorized - user doesn't own this tag";
            s.Responses[404] = "Tag not found";
        });
    }

    public override async Task HandleAsync(UpdateTagRequest req, CancellationToken ct)
    {
        // TODO: Extract from JWT token
        var userId = Guid.Parse("019a3f05-659b-7628-9102-1eef78035977"); // Placeholder

        try
        {
            var tag = await tagService.UpdateTagAsync(req.TagId, req.Name, userId, ct);
            
            await Send.OkAsync(new UpdateTagResponse
            {
                Id = tag.Id,
                Name = tag.Name,
                UpdatedAt = tag.UpdatedAt ?? DateTime.UtcNow
            }, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
        catch (UnauthorizedAccessException ex)
        {
            ThrowError(ex.Message, 401);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 400);
        }
    }
}