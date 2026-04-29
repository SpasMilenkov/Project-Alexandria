using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using Alexandria.Dto.Tags;
using FastEndpoints;

namespace Alexandria.Api.Features.Tags.SearchTags;

public class SearchTagsEndpoint(IFileTagService tagService) : Endpoint<SearchTagsRequest, PaginatedResult<TagDto>>
{
    public override void Configure()
    {
        Get("/tags/search");

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
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(SearchTagsRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            var query = new TagSearchQuery
            {
                UserId = userId,
                CreatedBy = req.CreatedBy,
                UpdatedBy = req.UpdatedBy,
                CreatedAfter = req.CreatedAfter,
                CreatedBefore = req.CreatedBefore,
                UpdatedAfter = req.UpdatedAfter,
                UpdatedBefore = req.UpdatedBefore,
                NameContains = req.NameContains,
                ExcludeOnFile = req.ExcludeOnFile,
                HasFiles = req.HasFiles,
                CurrentPage = req.Page,
                PageSize = req.PageSize
            };

            var result = await tagService.FindTagsAsync(query, ct);

            await Send.OkAsync(result, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 400);
        }
    }
}