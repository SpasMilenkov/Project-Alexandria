using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.RemovePlaylistItemEndpoint;

internal sealed class RemovePlaylistItemRequest
{
    public Guid PlaylistId { get; init; }
    public Guid ItemId { get; init; }
}
 
internal sealed class RemovePlaylistItemEndpoint(IPlaylistService playlistService)
    : Endpoint<RemovePlaylistItemRequest>
{
    public override void Configure()
    {
        Delete("/playlists/{playlistId}/items/{itemId}");
        Policies(Common.Auth.Policies.RequireUser);
    }
 
    public override async Task HandleAsync(RemovePlaylistItemRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await playlistService.RemoveItemAsync(req.PlaylistId, req.ItemId, userId, ct);
        await Send.NoContentAsync(ct);
    }
}

