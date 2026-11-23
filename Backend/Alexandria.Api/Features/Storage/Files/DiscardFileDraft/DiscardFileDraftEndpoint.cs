using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Services;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.DiscardFileDraft;

internal sealed class DiscardFileDraftRequest
{
    public Guid Id { get; set; }

    /// <summary>The revision being thrown away; a newer draft written elsewhere is kept.</summary>
    [QueryParam]
    public Guid Revision { get; set; }
}

internal sealed class DiscardFileDraftRequestValidator : Validator<DiscardFileDraftRequest>
{
    public DiscardFileDraftRequestValidator()
    {
        RuleFor(x => x.Revision)
            .NotEqual(Guid.Empty)
            .WithMessage("The draft revision to discard must be given.");
    }
}

/// <summary>
/// Throws away a draft the editor decided against, such as one made from an older version. 204
/// whether or not the draft was still there at that revision.
/// </summary>
internal sealed class DiscardFileDraftEndpoint(IFileDraftService drafts)
    : Endpoint<DiscardFileDraftRequest>
{
    public override void Configure()
    {
        Delete("/files/{id}/draft");
        Policies(Common.Auth.Policies.CanUpload);
    }

    public override async Task HandleAsync(DiscardFileDraftRequest req, CancellationToken ct)
    {
        try
        {
            await drafts.DiscardAsync(req.Id, req.Revision, User.GetUserId(), ct);
        }
        catch (FileRecordNotFoundException)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
