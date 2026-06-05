using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Playlist;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.GetPlaylist;

internal sealed class GetPlaylistRequest
{
    public Guid Id { get; init; }
}
 
internal sealed class GetPlaylistEndpoint(IPlaylistService playlistService)
    : Endpoint<GetPlaylistRequest, PlaylistDetailDto>
{
    public override void Configure()
    {
        Get("/playlists/{id}");
        Policies(Common.Auth.Policies.RequireUser);
    }
 
    public override async Task HandleAsync(GetPlaylistRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await playlistService.GetByIdAsync(req.Id, userId, ct);
        await Send.OkAsync(result, ct);
    }
}

