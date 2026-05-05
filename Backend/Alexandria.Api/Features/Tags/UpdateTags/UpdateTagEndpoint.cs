using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Tags.UpdateTags;

public class UpdateTagEndpoint(IFileTagService tagService) : Endpoint<UpdateTagRequest, UpdateTagResponse>
{
    public override void Configure()
    {
        Patch("/tags/{TagId}");

        Summary(s =>
        {
            s.Summary = "Update a tag";
            s.Description = "Updates an existing tag's name. User must own the tag.";
            s.Responses[200] = "Tag updated successfully";
            s.Responses[400] = "Bad request - invalid data";
            s.Responses[401] = "Unauthorized - user doesn't own this tag";
            s.Responses[404] = "Tag not found";
        });
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(UpdateTagRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            var tag = await tagService.UpdateTagAsync(req.TagId,
                userId,
                name: req.Name,
                color: req.Color,
                icon: req.Icon,
                description: req.Description,
                ct);

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