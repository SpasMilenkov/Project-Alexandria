using System.Security.Claims;
using API.Features.Tags.CrteateTag;
using Common.Services;
using FastEndpoints;

namespace API.Features.Tags.CreateTag;

public class CreateTagEndpoint(IFileTagService tagService) : Endpoint<CreateTagRequest, CreateTagResponse>
{
    public override void Configure()
    {
        Post("/tags");

        Summary(s =>
        {
            s.Summary = "Create a new tag";
            s.Description = "Creates a new tag for the current user. Tag names must be unique per user.";
            s.Responses[200] = "Tag created successfully";
            s.Responses[400] = "Bad request - invalid tag name or duplicate";
            s.Responses[500] = "Internal server error";
            s.ExampleRequest = new CreateTagRequest
            {
                Name = "Important",
                Icon = "document",
                Color = "#FFA500",
                Description = "This is an example of a tag description"
            };
        });
    }

    public override async Task HandleAsync(CreateTagRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");

        var userId = Guid.Parse(userIdString);

        try
        {
            var tag = await tagService.CreateAsync(name: req.Name,
                color: req.Color,
                icon: req.Icon,
                description: req.Description,
                userId, ct);

            await Send.OkAsync(new CreateTagResponse
            {
                Id = tag.Id,
                Name = tag.Name,
                UserId = tag.OwnerId,
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