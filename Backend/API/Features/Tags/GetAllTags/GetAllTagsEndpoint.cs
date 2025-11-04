using Common;
using DTO;
using FastEndpoints;

namespace API.Features.Tags.GetAllTags;

public class GetTagsEndpoint(IFileTagService tagService) : Endpoint<GetTagsRequest, GetTagsResponse>
{
    public override void Configure()
    {
        Get("/tags");
        AllowAnonymous(); // TODO: Replace with proper authorization
        
        Summary(s =>
        {
            s.Summary = "Get all tags with pagination";
            s.Description = "Retrieves a paginated list of all tags.";
            s.Responses[200] = "Tags retrieved successfully";
            s.Responses[400] = "Bad request - invalid pagination parameters";
        });
    }

    public override async Task HandleAsync(GetTagsRequest req, CancellationToken ct)
    {
        try
        {
            var result = await tagService.GetTagsAsync(req.Page, req.PageSize, ct);
            
            await Send.OkAsync(new GetTagsResponse
            {
                Tags = result.Items.Select(t => new TagDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    UserId = t.UserId,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt
                }).ToList(),
                CurrentPage = result.CurrentPage,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                HasPrevious = result.HasPrevious,
                HasNext = result.HasNext
            }, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 400);
        }
    }
}
