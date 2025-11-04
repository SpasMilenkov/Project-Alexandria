using API.Features.Tags.GetAllTags;
using Common;
using DTO;
using FastEndpoints;

namespace API.Features.Tags.SearchTags;




public class SearchTagsEndpoint(IFileTagService tagService) : Endpoint<SearchTagsRequest, SearchTagsResponse>
{
    public override void Configure()
    {
        Post("/tags/search");
        AllowAnonymous(); // TODO: Replace with proper authorization
        
        Summary(s =>
        {
            s.Summary = "Advanced tag search";
            s.Description = "Search tags with multiple filters including user, dates, name, and file associations.";
            s.Responses[200] = "Tags found successfully";
            s.Responses[400] = "Bad request - invalid search parameters";
            s.ExampleRequest = new SearchTagsRequest
            {
                NameContains = "important",
                HasFiles = true,
                Page = 0,
                PageSize = 20
            };
        });
    }

    public override async Task HandleAsync(SearchTagsRequest req, CancellationToken ct)
    {
        try
        {
            var query = new TagSearchQuery
            {
                UserId = req.UserId,
                CreatedBy = req.CreatedBy,
                UpdatedBy = req.UpdatedBy,
                CreatedAfter = req.CreatedAfter,
                CreatedBefore = req.CreatedBefore,
                UpdatedAfter = req.UpdatedAfter,
                UpdatedBefore = req.UpdatedBefore,
                NameContains = req.NameContains,
                HasFiles = req.HasFiles,
                CurrentPage = req.Page,
                PageSize = req.PageSize
            };

            var result = await tagService.FindTagsAsync(query, ct);
            
            await Send.OkAsync(new SearchTagsResponse
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