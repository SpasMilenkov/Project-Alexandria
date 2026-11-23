using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Notes;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Notes.GetNoteSummaries;

internal sealed class GetNoteSummariesRequest
{
    public List<Guid> FileIds { get; set; } = [];
}

internal sealed class GetNoteSummariesRequestValidator : Validator<GetNoteSummariesRequest>
{
    // One page of the Notes page's list, with room to spare.
    public const int MaxFiles = 100;

    public GetNoteSummariesRequestValidator()
    {
        RuleFor(x => x.FileIds)
            .NotEmpty()
            .WithMessage("At least one note must be given.")
            .Must(ids => ids.Count <= MaxFiles)
            .WithMessage($"At most {MaxFiles} notes can be summarised at once.");

        RuleForEach(x => x.FileIds)
            .NotEqual(Guid.Empty)
            .WithMessage("A note id cannot be an empty GUID.");
    }
}

/// <summary>
/// Excerpts and mention counts for a page of notes on the Notes page. The list itself comes from
/// file search, so its paging, sorting and text search stay the explorer's. A POST because a
/// page of ids would not fit a query string comfortably.
/// </summary>
internal sealed class GetNoteSummariesEndpoint(INoteService noteService)
    : Endpoint<GetNoteSummariesRequest, NoteSummariesResponse>
{
    // The browser cache is shared by every account signed in on it.
    private const string NoStore = "no-store";

    public override void Configure()
    {
        Post("/notes/summaries");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetNoteSummariesRequest req, CancellationToken ct)
    {
        var summaries = await noteService.GetSummariesAsync(req.FileIds, User.GetUserId(), ct);

        HttpContext.Response.Headers.CacheControl = NoStore;

        await Send.OkAsync(new NoteSummariesResponse(summaries), ct);
    }
}
