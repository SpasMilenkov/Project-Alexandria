using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Tags;
using FastEndpoints;

namespace Alexandria.Api.Features.Tags.SearchFileByTags;

public class SearchFilesByTagsEndpoint(IFileTagService tagService)
    : Endpoint<SearchFilesByTagsRequest, PaginatedResult<FileResult>>
{
    public override void Configure()
    {
        Get("/files/search/tags");

        Summary(s =>
        {
            s.Summary = "Search files by tags";
            s.Description =
                "Find files matching tag criteria with advanced filtering. Match types: 'any' (OR), 'all' (AND), 'exact' (exactly these tags).";
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
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(SearchFilesByTagsRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
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
                UserId = userId,
                CurrentPage = req.Page,
                PageSize = req.PageSize,
                MinFileSize = req.MinFileSize,
                MaxFileSize = req.MaxFileSize,
                MimeTypePrefix = req.MimeTypePrefix,
                CreatedAfter = req.CreatedAfter,
                CreatedBefore = req.CreatedBefore
            };

            var result = await tagService.FindFilesByTagsAsync(query, ct);

            await Send.OkAsync(result, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 400);
        }
    }
}