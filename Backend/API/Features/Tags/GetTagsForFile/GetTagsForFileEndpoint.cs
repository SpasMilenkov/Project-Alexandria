using Common.Services;
using DTO.Tags;
using FastEndpoints;

namespace API.Features.Tags.GetTagsForFile;

public class GetTagsForFileEndpoint(IFileTagService tagService)
    : Endpoint<GetTagsForFileRequest, GetTagsForFileResponse>
{
    public override void Configure()
    {
        Get("/files/{FileId}/tags");

        Summary(s =>
        {
            s.Summary = "Get all tags for a file";
            s.Description = "Retrieves all tags associated with a specific file.";
            s.Responses[200] = "Tags retrieved successfully";
            s.Responses[404] = "File not found";
        });
    }

    public override async Task HandleAsync(GetTagsForFileRequest req, CancellationToken ct)
    {
        try
        {
            var tags = await tagService.GetTagsForFileAsync(req.FileId, ct);

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