using Common;
using DTO;
using FastEndpoints;
using Models.Enumerators;

namespace API.Features.Tags.SearchFileByTags;

public class SearchFilesByTagsEndpoint(IFileTagService tagService) : Endpoint<SearchFilesByTagsRequest, SearchFilesByTagsResponse>
{
    public override void Configure()
    {
        Post("/files/search/tags");
        AllowAnonymous(); // TODO: Replace with proper authorization
        
        Summary(s =>
        {
            s.Summary = "Search files by tags";
            s.Description = "Find files matching tag criteria with advanced filtering. Match types: 'any' (OR), 'all' (AND), 'exact' (exactly these tags).";
            s.Responses[200] = "Files found successfully";
            s.Responses[400] = "Bad request - invalid search parameters";
            s.ExampleRequest = new SearchFilesByTagsRequest
            {
                TagIds = new List<Guid> { Guid.NewGuid() },
                MatchType = "any",
                Page = 0,
                PageSize = 20
            };
        });
    }

    public override async Task HandleAsync(SearchFilesByTagsRequest req, CancellationToken ct)
    {
        try
        {
            var matchType = req.MatchType.ToLowerInvariant() switch
            {
                "all" => TagMatchType.All,
                "exact" => TagMatchType.Exact,
                _ => TagMatchType.Any
            };

            var query = new FileTagSearchQuery
            {
                TagIds = req.TagIds,
                MatchType = matchType,
                UserId = req.UserId,
                CurrentPage = req.Page,
                PageSize = req.PageSize,
                MinFileSize = req.MinFileSize,
                MaxFileSize = req.MaxFileSize,
                MimeTypePrefix = req.MimeTypePrefix,
                CreatedAfter = req.CreatedAfter,
                CreatedBefore = req.CreatedBefore
            };

            var result = await tagService.FindFilesByTagsAsync(query, ct);
            
            await Send.OkAsync(new SearchFilesByTagsResponse
            {
                Files = result.Items.Select(f => new FileDto
                {
                    Id = f.Id,
                    Name = f.Name,
                    Path = f.Path,
                    MimeType = f.MimeType,
                    Size = f.Size.ToString(),
                    CreatedAt = f.CreatedAt,
                    HasPreview = f.HasPreview,
                    Tags = f.Tags.Where(t => t.DeletedAt == null).Select(t => new TagDto
                    {
                        Id = t.Id,
                        Name = t.Name,
                        UserId = t.UserId,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt
                    }).ToList()
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
