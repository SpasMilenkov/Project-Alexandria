using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.Playlists.GetPlaylistCoverAsync;

internal sealed class GetPlaylistCoverAsyncRequest
{
    public required Guid PlaylistId { get; set; }
}

internal sealed class GetPlaylistCoverAsyncEndpoint(IStorageService storageService)
    : Endpoint<GetPlaylistCoverAsyncRequest, string>
{
    public override void Configure()
    {
        Get("streaming/playlists/cover/{playlistId}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetPlaylistCoverAsyncRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await storageService.GetPlaylistCoverUrlAsync(req.PlaylistId, userId, ct), ct);
    }
}