using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Playlist;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.Playlists.SetPlaylistFavorite;

internal sealed class SetPlaylistFavoriteRequest
{
    public Guid PlaylistId { get; set; }
    public bool? IsStarred { get; set; }
}

internal sealed class SetPlaylistFavoriteValidator : Validator<SetPlaylistFavoriteRequest>
{
    public SetPlaylistFavoriteValidator()
    {
        RuleFor(x => x.PlaylistId).NotEmpty();
        RuleFor(x => x.IsStarred).NotNull();
    }
}

internal sealed class SetPlaylistFavoriteEndpoint(IPlaylistService playlistService)
    : Endpoint<SetPlaylistFavoriteRequest, PlaylistFavoriteResponse>
{
    public override void Configure()
    {
        Patch("/playlists/{PlaylistId}/favorite");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(SetPlaylistFavoriteRequest req, CancellationToken ct)
    {
        var isStarred = req.IsStarred!.Value;
        var updated = await playlistService.SetPlaylistFavoriteAsync(req.PlaylistId, User.GetUserId(),
            isStarred, ct);

        if (!updated)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new PlaylistFavoriteResponse(req.PlaylistId, isStarred), ct);
    }
}