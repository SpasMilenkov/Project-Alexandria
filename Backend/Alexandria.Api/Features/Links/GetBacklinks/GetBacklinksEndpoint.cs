using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Links;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Links.GetBacklinks;

internal sealed class GetBacklinksRequest
{
    [QueryParam]
    public LinkTargetKind Kind { get; set; }

    [QueryParam]
    public Guid Id { get; set; }
}

internal sealed class GetBacklinksRequestValidator : Validator<GetBacklinksRequest>
{
    public GetBacklinksRequestValidator()
    {
        RuleFor(x => x.Kind)
            .IsInEnum()
            .WithMessage("Unknown link target kind.");

        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty)
            .WithMessage("A link target needs an id.");
    }
}

/// <summary>
/// "Mentioned in": the viewer's own notes that link to an item. Only notes are searched, so the
/// answer never reveals anything about another account, whatever id is asked for.
/// </summary>
internal sealed class GetBacklinksEndpoint(INoteLinkIndexService linkIndex)
    : Endpoint<GetBacklinksRequest, NoteBacklinksResponse>
{
    // The browser cache is shared by every account signed in on it.
    private const string NoStore = "no-store";

    public override void Configure()
    {
        Get("/links/backlinks");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetBacklinksRequest req, CancellationToken ct)
    {
        var notes = await linkIndex.GetBacklinksAsync(req.Kind, req.Id, User.GetUserId(), ct);

        HttpContext.Response.Headers.CacheControl = NoStore;

        await Send.OkAsync(new NoteBacklinksResponse(notes), ct);
    }
}
