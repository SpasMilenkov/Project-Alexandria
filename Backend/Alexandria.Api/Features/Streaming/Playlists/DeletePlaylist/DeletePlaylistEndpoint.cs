using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.DeletePlaylist;

internal sealed class DeletePlaylistRequest
{
    public Guid Id { get; init; }
}
 
internal sealed class DeletePlaylistEndpoint(IPlaylistService playlistService)
    : Endpoint<DeletePlaylistRequest>
{
    public override void Configure()
    {
        Delete("/playlists/{id}");
        Policies(Common.Auth.Policies.RequireUser);
    }
 
    public override async Task HandleAsync(DeletePlaylistRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await playlistService.DeleteAsync(req.Id, userId, ct);
        await Send.NoContentAsync(ct);
    }
}

