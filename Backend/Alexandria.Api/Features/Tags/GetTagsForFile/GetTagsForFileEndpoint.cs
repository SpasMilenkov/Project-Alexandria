using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Tags;
using FastEndpoints;

namespace Alexandria.Api.Features.Tags.GetTagsForFile;

//TODO THIS SHOULD USE THE USER ID TO VALIDATE OWNERSHIP, ALL TAG ENDPOINTS AND SERVICES NEED TO DO THIS 
public class GetTagsForFileEndpoint(IFileTagService tagService)
    : Endpoint<GetTagsForFileRequest, GetTagsForFileResponse>
{
    public override void Configure()
    {
        Get("/files/{FileId}/tags");
        ResponseCache(30);
        Summary(s =>
        {
            s.Summary = "Get all tags for a file";
            s.Description = "Retrieves all tags associated with a specific file.";
            s.Responses[200] = "Tags retrieved successfully";
            s.Responses[404] = "File not found";
        });
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetTagsForFileRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        try
        {
            var tags = await tagService.GetTagsForFileAsync(userId, req.FileId, ct);

            await Send.OkAsync(new GetTagsForFileResponse
            {
                FileId = req.FileId,
                Tags = tags.Select(t => new TagDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    UserId = t.UserId,
                    Color = t.Color,
                    Description = t.Description,
                    Icon = t.Icon,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt
                }).ToList()
            }, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
    }
}