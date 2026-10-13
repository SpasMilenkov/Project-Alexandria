using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Directories;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Directories.SetDirectoryFavorite;

public sealed class SetDirectoryFavoriteRequest
{
    public Guid DirectoryId { get; set; }
    public bool? IsStarred { get; set; }
}

public sealed class SetDirectoryFavoriteValidator : Validator<SetDirectoryFavoriteRequest>
{
    public SetDirectoryFavoriteValidator()
    {
        RuleFor(x => x.DirectoryId).NotEmpty();
        RuleFor(x => x.IsStarred).NotNull();
    }
}

public sealed class SetDirectoryFavoriteEndpoint(IDirectoryService directoryService)
    : Endpoint<SetDirectoryFavoriteRequest, DirectoryFavoriteResponse>
{
    public override void Configure()
    {
        Patch("/directories/{DirectoryId}/favorite");
        Description(x => x.WithTags("Directories"));
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(SetDirectoryFavoriteRequest req, CancellationToken ct)
    {
        var isStarred = req.IsStarred!.Value;
        var updated = await directoryService.SetDirectoryFavoriteAsync(req.DirectoryId, User.GetUserId(),
            isStarred, ct);

        if (!updated)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new DirectoryFavoriteResponse(req.DirectoryId, isStarred), ct);
    }
}
