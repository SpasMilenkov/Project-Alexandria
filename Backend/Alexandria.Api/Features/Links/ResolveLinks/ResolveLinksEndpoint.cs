using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Links;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Links.ResolveLinks;

internal sealed class ResolveLinksRequest
{
    public List<LinkTargetRef> Targets { get; set; } = [];
}

internal sealed class ResolveLinksRequestValidator : Validator<ResolveLinksRequest>
{
    // The client batches the chips on screen into requests of at most this many targets.
    public const int MaxTargets = 200;

    public ResolveLinksRequestValidator()
    {
        RuleFor(x => x.Targets)
            .NotEmpty()
            .WithMessage("At least one link target must be given.")
            .Must(targets => targets.Count <= MaxTargets)
            .WithMessage($"At most {MaxTargets} link targets can be resolved at once.");

        RuleForEach(x => x.Targets).ChildRules(target =>
        {
            target.RuleFor(t => t.Kind)
                .IsInEnum()
                .WithMessage("Unknown link target kind.");

            target.RuleFor(t => t.Id)
                .NotEqual(Guid.Empty)
                .WithMessage("A link target needs an id.");
        });
    }
}

/// <summary>
/// Live names and states for the links in a note, in one round trip for every chip on screen.
/// A POST because a long note's ids would not fit in a query string.
/// </summary>
internal sealed class ResolveLinksEndpoint(ILinkTargetService linkTargetService)
    : Endpoint<ResolveLinksRequest, ResolveLinksResponse>
{
    public override void Configure()
    {
        Post("/links/resolve");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(ResolveLinksRequest req, CancellationToken ct)
    {
        var targets = await linkTargetService.ResolveAsync(req.Targets, User.GetUserId(), ct);

        await Send.OkAsync(new ResolveLinksResponse(targets), ct);
    }
}
