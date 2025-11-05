using Common;
using Common.Services;
using FastEndpoints;

namespace API.Features.Tags.CrteateTag;

public class CreateTagEndpoint(IFileTagService tagService) : Endpoint<CreateTagRequest, CreateTagResponse>
{
    public override void Configure()
    {
        Post("/tags");
        AllowAnonymous(); // TODO: Replace with proper authorization
        
        Summary(s =>
        {
            s.Summary = "Create a new tag";
            s.Description = "Creates a new tag for the current user. Tag names must be unique per user.";
            s.Responses[200] = "Tag created successfully";
            s.Responses[400] = "Bad request - invalid tag name or duplicate";
            s.Responses[500] = "Internal server error";
            s.ExampleRequest = new CreateTagRequest { Name = "Important" };
        });
    }

    public override async Task HandleAsync(CreateTagRequest req, CancellationToken ct)
    {
        // TODO: Extract from JWT token after implementing authentication
        var userId = Guid.Parse("019a3f05-659b-7628-9102-1eef78035977"); // Placeholder

        try
        {
            var tag = await tagService.CreateAsync(req.Name, userId, ct);
            
            await Send.OkAsync(new CreateTagResponse
            {
                Id = tag.Id,
                Name = tag.Name,
                UserId = tag.UserId,
                CreatedAt = tag.CreatedAt
            }, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 400);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 400);
        }
    }
}